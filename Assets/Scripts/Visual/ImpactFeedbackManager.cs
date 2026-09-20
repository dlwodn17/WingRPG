using System;
using System.Collections;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Visual
{
    /// <summary>
    /// [요구 산출물 1] ImpactFeedbackManager.cs
    /// D20 주사위 눈금 결과에 따른 히트 스톱(Time Halt)과 2.5D 카메라 셰이크를 총괄 제어하는 매니저 클래스입니다.
    /// 3D 배경과 2D 스프라이트가 공존하는 2.5D 환경에 최적화된 흔들림 감쇄 및 안전 클램프를 적용하며,
    /// 헤드리스 CLI 배치 모드(-nographics)에서는 시각 연출을 생략하고 로직만 즉시 검증되도록 설계되었습니다.
    /// </summary>
    public class ImpactFeedbackManager : MonoBehaviour
    {
        private static ImpactFeedbackManager _instance;
        public static ImpactFeedbackManager Instance => _instance;

        [Header("설정 에셋")]
        [SerializeField] private ImpactFeedbackConfigSO _config;

        [Header("타깃 카메라 (미지정 시 Camera.main 자동 바인딩)")]
        [SerializeField] private Camera _targetCamera;

        // 2.5D 카메라 원래 로컬 위치 보존
        private Vector3 _originalCamLocalPos;
        private bool _hasOriginalCamPos = false;

        // 코루틴 핸들러
        private Coroutine _hitStopCoroutine;
        private Coroutine _shakeCoroutine;

        // 히트스톱 복구 시 사용할 원래 timeScale 보존 (슬로우모션 등과의 충돌 방지)
        private float _preHitStopTimeScale = 1.0f;
        private bool _isHitStopActive = false;

        // 화면 암전 레터박스 연출 상태
        private bool _isLetterboxActive = false;
        private float _letterboxTimer = 0f;
        private float _letterboxDuration = 0f;

        public bool IsHitStopActive => _isHitStopActive;
        public ImpactFeedbackConfigSO Config => _config;

        public event Action<int, ImpactProfile> OnImpactTriggered;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<ImpactFeedbackConfigSO>();
            }

            EnsureCamera();
        }

        private void EnsureCamera()
        {
            if (_targetCamera == null) _targetCamera = Camera.main;
            if (_targetCamera != null && !_hasOriginalCamPos)
            {
                _originalCamLocalPos = _targetCamera.transform.localPosition;
                _hasOriginalCamPos = true;
            }
        }

        /// <summary>
        /// D20 눈금(1~20)에 맞는 타격감(히트 스톱 + 2.5D 카메라 셰이크)을 즉시 트리거합니다.
        /// 캐릭터 공격 애니메이션이 적에게 적중하는 정확한 프레임에 호출됩니다.
        /// </summary>
        public void TriggerImpact(int d20Value)
        {
            // 1. CLI 헤드리스 모드(-batchmode, -nographics) 감지 시 비주얼 연출 즉시 우회
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            if (_config == null) _config = ScriptableObject.CreateInstance<ImpactFeedbackConfigSO>();

            ImpactProfile profile = _config.GetProfileForRoll(d20Value);
            TriggerImpact(profile);

            OnImpactTriggered?.Invoke(d20Value, profile);
        }

        /// <summary>
        /// 지정된 ImpactProfile 커스텀 설정으로 타격감을 트리거합니다.
        /// </summary>
        public void TriggerImpact(ImpactProfile profile)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            EnsureCamera();

            // A. 히트 스톱 (Time Halt) 실행
            if (profile.hitStopDuration > 0f)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine(profile.hitStopDuration, profile.hitStopTimeScale));
            }

            // B. 2.5D 카메라 셰이크 실행
            if (profile.shakeDuration > 0f && profile.shakeAmplitude > 0f)
            {
                if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = StartCoroutine(ShakeRoutine(profile));
            }

            // C. 시네마틱 레터박스 실행 (눈금 1 대실패 및 20 즉사)
            if (profile.useLetterbox)
            {
                StartCoroutine(LetterboxRoutine(profile.hitStopDuration + 0.35f));
            }
        }

        private IEnumerator HitStopRoutine(float duration, float stopTimeScale)
        {
            // 현재 활성화된 정상 timeScale 저장 (기존 슬로우 모션 등이 적용되어 있다면 해당 값 보존)
            if (!_isHitStopActive)
            {
                _preHitStopTimeScale = Time.timeScale > 0f ? Time.timeScale : 1.0f;
            }

            _isHitStopActive = true;
            Time.timeScale = stopTimeScale;

            // 실시간(Unscaled)으로 대기하여 timeScale=0 상태에서도 정확히 복구
            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = _preHitStopTimeScale;
            _isHitStopActive = false;
            _hitStopCoroutine = null;
        }

        private IEnumerator ShakeRoutine(ImpactProfile profile)
        {
            if (_targetCamera == null) yield break;

            float elapsed = 0f;
            Vector3 basePos = _originalCamLocalPos;
            Vector3 axis = profile.axisMultiplier;
            Vector3 clampMax = _config != null ? _config.maxShakeClamp : new Vector3(1.2f, 0.5f, 0.9f);

            // Perlin 노이즈 시드 무작위화
            float seedX = UnityEngine.Random.Range(0f, 100f);
            float seedY = UnityEngine.Random.Range(100f, 200f);
            float seedZ = UnityEngine.Random.Range(200f, 300f);

            while (elapsed < profile.shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / profile.shakeDuration;

                // 감쇠 커브: 지수 감쇠로 타격 직후 강렬하고 끝은 부드럽게 복구
                float damping = Mathf.Pow(1f - Mathf.Clamp01(progress), 1.5f);

                float freq = profile.shakeFrequency;
                float amp = profile.shakeAmplitude * damping;

                // 2.5D 최적화 노이즈 샘플링 (X: 좌우 펀치, Y: 상하 미세, Z: 카메라 광축 전후 진동)
                float noiseX = (Mathf.PerlinNoise(seedX + elapsed * freq, 0f) * 2f - 1f) * amp * axis.x;
                float noiseY = (Mathf.PerlinNoise(0f, seedY + elapsed * freq) * 2f - 1f) * amp * axis.y;
                float noiseZ = (Mathf.PerlinNoise(seedZ + elapsed * freq, seedZ) * 2f - 1f) * amp * axis.z;

                // 2.5D 안전 범위 클램핑 (3D 배경 및 빌보드 레이어링 왜곡 방지)
                noiseX = Mathf.Clamp(noiseX, -clampMax.x, clampMax.x);
                noiseY = Mathf.Clamp(noiseY, -clampMax.y, clampMax.y);
                noiseZ = Mathf.Clamp(noiseZ, -clampMax.z, clampMax.z);

                _targetCamera.transform.localPosition = basePos + new Vector3(noiseX, noiseY, noiseZ);

                yield return null;
            }

            // 정확한 원래 기준 좌표로 복구
            _targetCamera.transform.localPosition = basePos;
            _shakeCoroutine = null;
        }

        private IEnumerator LetterboxRoutine(float duration)
        {
            _isLetterboxActive = true;
            _letterboxDuration = duration;
            _letterboxTimer = 0f;

            while (_letterboxTimer < _letterboxDuration)
            {
                _letterboxTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            _isLetterboxActive = false;
        }

        private void OnDisable()
        {
            // 비활성화 시 timeScale 및 카메라 위치 안전 원복
            if (_isHitStopActive)
            {
                Time.timeScale = _preHitStopTimeScale;
                _isHitStopActive = false;
            }

            if (_hasOriginalCamPos && _targetCamera != null)
            {
                _targetCamera.transform.localPosition = _originalCamLocalPos;
            }
        }

        private void OnGUI()
        {
            // 눈금 1 대실패 및 20 즉사 시네마틱 레터박스 (상/하단 블랙 바)
            if (_isLetterboxActive)
            {
                int sw = Screen.width;
                int sh = Screen.height;
                int barH = Mathf.RoundToInt(sh * 0.12f);

                float alpha = 0.88f;
                if (_letterboxTimer > _letterboxDuration - 0.25f)
                {
                    alpha = Mathf.Lerp(0.88f, 0f, (_letterboxTimer - (_letterboxDuration - 0.25f)) / 0.25f);
                }

                Color orig = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0, 0, sw, barH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, sh - barH, sw, barH), Texture2D.whiteTexture);
                GUI.color = orig;
            }
        }
    }
}
