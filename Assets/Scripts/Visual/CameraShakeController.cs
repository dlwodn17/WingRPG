using System.Collections;
using UnityEngine;

namespace RPG25D.Visual
{
    /// <summary>
    /// 카메라 셰이크(Camera Shake) 및 히트스톱(Hit-Stop) 컨트롤러
    /// 시네머신 의존성 유무와 상관없이 독립적으로 부드러운 댐핑 셰이크와 타격감 프리징을 제공합니다.
    /// </summary>
    public class CameraShakeController : MonoBehaviour
    {
        private static CameraShakeController _instance;
        public static CameraShakeController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CameraShakeController>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[CameraShakeController]");
                        _instance = go.AddComponent<CameraShakeController>();
                    }
                }
                return _instance;
            }
        }

        [Header("타깃 카메라")]
        [SerializeField] private Camera _targetCamera;

        private Vector3 _originalCamPos;
        private Coroutine _shakeCoroutine;
        private Coroutine _hitStopCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_targetCamera == null) _targetCamera = Camera.main;
            if (_targetCamera != null) _originalCamPos = _targetCamera.transform.localPosition;
        }

        /// <summary>
        /// D20 눈금 크기에 비례한 카메라 흔들림을 트리거합니다.
        /// </summary>
        public void Shake(float intensity = 0.3f, float duration = 0.35f)
        {
            if (_targetCamera == null) _targetCamera = Camera.main;
            if (_targetCamera == null) return;

            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;
            Vector3 basePos = _originalCamPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damp = 1f - Mathf.Clamp01(elapsed / duration);

                // Perlin 노이즈 기반 부드러운 흔들림
                float x = (Mathf.PerlinNoise(Time.unscaledTime * 35f, 0f) * 2f - 1f) * intensity * damp;
                float y = (Mathf.PerlinNoise(0f, Time.unscaledTime * 35f) * 2f - 1f) * intensity * damp;

                _targetCamera.transform.localPosition = basePos + new Vector3(x, y, 0f);
                yield return null;
            }

            _targetCamera.transform.localPosition = basePos;
            _shakeCoroutine = null;
        }

        /// <summary>
        /// 고눈금(15+) 및 즉사(20) 발동 시 프레임을 일시 동결하는 히트 스톱(Hit-Stop)
        /// </summary>
        public void HitStop(float duration = 0.15f)
        {
            if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = originalTimeScale;
            _hitStopCoroutine = null;
        }
    }
}
