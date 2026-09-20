using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.Visual
{
    /// <summary>
    /// [요구 산출물 3] BattleFeedbackManager.cs
    /// BattleTurnController의 상태 전이를 구독하여 D20 3D 물리 주사위 굴림, 눈금별 극단적 연출
    /// (대실패 산산조각+붉은연기, 적중 홀로그램+비례파티클, 즉사 슬로우모션+레터박스+황금빛기둥),
    /// 대상 위 플로팅 대미지 텍스트(폰트 크기/색상 눈금 비례), 카메라 셰이크, 히트스톱, 실시간 사운드를 총괄 제어하는 오케스트레이터입니다.
    /// 헤드리스 CLI 환경(-batchmode, -nographics)에서는 물리 연출을 즉시 우회하여 빠른 로직 검증을 지원합니다.
    /// </summary>
    public class BattleFeedbackManager : MonoBehaviour
    {
        private static BattleFeedbackManager _instance;
        public static BattleFeedbackManager Instance => _instance;

        [Header("연결 컴포넌트")]
        [SerializeField] private DicePhysicsVisualizer _diceVisualizer;
        [SerializeField] private BattleTurnController _turnController;
        [SerializeField] private D20FeedbackConfigSO _config;

        [Header("오디오")]
        [SerializeField] private AudioSource _audioSource;

        // 화면 중앙 플로팅 텍스트 연출 데이터
        private string _overlayAnnouncementText = string.Empty;
        private Color _overlayTextColor = Color.white;
        private int _overlayFontSize = 28;
        private float _overlayAlpha = 0f;
        private bool _isLetterboxActive = false;

        // 대상 머리 위 플로팅 대미지 텍스트 항목
        private class FloatingTextItem
        {
            public string text;
            public Color color;
            public int fontSize;
            public Vector3 worldPos;
            public float elapsed;
            public float duration;
        }
        private readonly List<FloatingTextItem> _floatingTexts = new List<FloatingTextItem>();

        private GUIStyle _announcementStyle;
        private GUIStyle _floatingDamageStyle;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_turnController == null) _turnController = FindAnyObjectByType<BattleTurnController>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;

            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<D20FeedbackConfigSO>();
            }

            EnsureDiceVisualizer();
        }

        private void Start()
        {
            SubscribeTurnController();
        }

        private void OnDestroy()
        {
            UnsubscribeTurnController();
        }

        private void SubscribeTurnController()
        {
            if (_turnController == null) _turnController = FindAnyObjectByType<BattleTurnController>();
            if (_turnController != null)
            {
                _turnController.OnStateChanged += HandleStateChanged;
                _turnController.OnActionExecuted += HandleActionExecuted;
            }
        }

        private void UnsubscribeTurnController()
        {
            if (_turnController != null)
            {
                _turnController.OnStateChanged -= HandleStateChanged;
                _turnController.OnActionExecuted -= HandleActionExecuted;
            }
        }

        /// <summary>
        /// BattleTurnController의 상태 전이를 구독하여 필요한 비주얼을 트리거합니다.
        /// </summary>
        private void HandleStateChanged(BattlePhase state)
        {
            // 헤드리스/배치 모드 시 시각 연출 무시
            if (Application.isBatchMode || !SystemInfo.supportsAudio) return;

            switch (state)
            {
                case BattlePhase.BattleVictory:
                    ShowAnnouncement("🎉 승리! (VICTORY!)", new Color(0.2f, 1f, 0.4f), 40, 2.5f);
                    break;
                case BattlePhase.BattleDefeat:
                    ShowAnnouncement("💀 패배 (DEFEAT...)", new Color(0.9f, 0.2f, 0.2f), 40, 2.5f);
                    break;
            }
        }

        /// <summary>
        /// 타격 실행 완료 이벤트 수신 시 대상 위에 눈금 비례 플로팅 대미지 텍스트를 생성합니다.
        /// </summary>
        private void HandleActionExecuted(AttackExecutionResult result)
        {
            if (Application.isBatchMode) return;

            // 대상의 2.5D 위치 탐색 (PartyManager 연동)
            Vector3 targetWorldPos = Vector3.zero;
            if (_turnController != null && _turnController.SelectedTarget != null)
            {
                if (_turnController.Party != null)
                {
                    targetWorldPos = _turnController.Party.GetActorWorldPosition(_turnController.SelectedTarget) + Vector3.up * 1.8f;
                }
                else
                {
                    targetWorldPos = new Vector3(2.5f, 2.0f, 0f);
                }
            }

            int d20 = result.D20Value;
            int fontSize = _config.GetFontSizeForRoll(d20);
            Color color = _config.GetColorForRoll(d20);

            string text = string.Empty;
            switch (result.Outcome)
            {
                case D20Outcome.Fail:
                    text = "❌ 빗나감 (MISS!)";
                    color = _config.failColor;
                    break;
                case D20Outcome.InstantKill:
                    text = "💀 즉사 (INSTANT KILL!)";
                    color = _config.instantKillColor;
                    fontSize = _config.maxFontSize + 6;
                    break;
                case D20Outcome.Hit:
                default:
                    text = $"-{result.DamageDealt}";
                    break;
            }

            SpawnFloatingText(text, color, fontSize, targetWorldPos, 1.4f);
        }

        public void SpawnFloatingText(string text, Color color, int fontSize, Vector3 worldPos, float duration = 1.2f)
        {
            _floatingTexts.Add(new FloatingTextItem
            {
                text = text,
                color = color,
                fontSize = fontSize,
                worldPos = worldPos,
                elapsed = 0f,
                duration = duration
            });
        }

        private void EnsureDiceVisualizer()
        {
            if (_diceVisualizer == null)
            {
                var existing = FindAnyObjectByType<DicePhysicsVisualizer>();
                if (existing != null)
                {
                    _diceVisualizer = existing;
                }
                else
                {
                    var diceObj = new GameObject("[DicePhysicsVisualizer]");
                    diceObj.transform.position = new Vector3(0f, 5f, 0f);
                    _diceVisualizer = diceObj.AddComponent<DicePhysicsVisualizer>();
                }
            }
        }

        /// <summary>
        /// 물리 주사위를 투척하고, 멈춘 결과를 판독하여 눈금별 극단적 피드백 시퀀스를 진행합니다.
        /// forcedValue가 null이면 순수 물리 시뮬레이션 결과로 진행됩니다.
        /// </summary>
        public void ExecuteDiceRollSequence(int? forcedValue, Action<D20RollResult> onComplete)
        {
            // 헤드리스/배치모드(-batchmode, -nographics) 감지 시 물리 연출 즉시 스킵
            if (Application.isBatchMode || !SystemInfo.supportsAudio || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                int val = forcedValue ?? UnityEngine.Random.Range(1, 21);
                var roll = D20DiceRoller.Evaluate(val, isPhysicalRoll: false);
                onComplete?.Invoke(roll);
                return;
            }

            EnsureDiceVisualizer();
            _diceVisualizer.ThrowDice(forcedValue, (settledFace) =>
            {
                var roll = D20DiceRoller.Evaluate(settledFace, isPhysicalRoll: true);
                StartCoroutine(PlayDopamineFeedbackRoutine(roll, () =>
                {
                    onComplete?.Invoke(roll);
                }));
            });
        }

        /// <summary>
        /// 눈금별 극단적 비주얼/사운드 피드백 시퀀스 (도파민 연출)
        /// </summary>
        public IEnumerator PlayDopamineFeedbackRoutine(D20RollResult roll, Action onFinished = null)
        {
            int d20 = roll.Value;
            D20Outcome outcome = roll.Outcome;

            _overlayFontSize = _config.GetFontSizeForRoll(d20);
            _overlayTextColor = _config.GetColorForRoll(d20);

            switch (outcome)
            {
                case D20Outcome.Fail:
                    // 1. 대실패 (눈금 1)
                    // 사운드: 유리가 깨지는 듯한 불협화음 SFX
                    _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateCritFailClip(), 1.0f);
                    // 비주얼: 주사위 산산조각 및 붉은 연기 발생
                    _diceVisualizer.PlayShatterEffect();
                    // 눈금 비례 타격감 (둔탁한 저주파 셰이크 + 히트스톱 + 레터박스)
                    if (ImpactFeedbackManager.Instance != null)
                    {
                        ImpactFeedbackManager.Instance.TriggerImpact(1);
                    }
                    else
                    {
                        CameraShakeController.Instance.Shake(_config.failCameraShake, 0.4f);
                    }

                    ShowAnnouncement("❌ 대실패 (CRITICAL MISS!)", _config.failColor, _overlayFontSize, 1.2f);
                    yield return new WaitForSeconds(0.65f);
                    break;

                case D20Outcome.InstantKill:
                    // 2. 절대 성공 / 즉사 (눈금 20)
                    // 사운드: 웅장한 팡파르 + 심장 박동음
                    _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateInstantKillFanfareClip(), 1.0f);

                    // 비주얼: 황금빛 빛기둥 및 황금 파티클 연출
                    _diceVisualizer.PlayGoldenPillarEffect(_config.slowMotionDuration);

                    // 비주얼: 화면 암전 (레터박스) 및 슬로우 모션
                    _isLetterboxActive = true;
                    Time.timeScale = _config.slowMotionScale;

                    // 최대 강도 카메라 셰이크 & 히트 스톱 (찢어지는 듯한 셰이크 + 0.25s 히트스톱)
                    if (ImpactFeedbackManager.Instance != null)
                    {
                        ImpactFeedbackManager.Instance.TriggerImpact(20);
                    }
                    else
                    {
                        CameraShakeController.Instance.Shake(_config.instantKillShake, 0.6f);
                        CameraShakeController.Instance.HitStop(_config.instantKillHitStop);
                    }

                    ShowAnnouncement("💀 절대 성공 / 즉사 (INSTANT KILL!)", _config.instantKillColor, _overlayFontSize, 1.8f);

                    yield return new WaitForSecondsRealtime(_config.slowMotionDuration);

                    Time.timeScale = 1.0f;
                    _isLetterboxActive = false;
                    break;

                case D20Outcome.Hit:
                default:
                    // 3. 일반 성공 (눈금 2~19)
                    // 사운드: 눈금 크기에 비례한 피치 상승 타격음
                    _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateHitClip(d20), 0.9f);

                    // 비주얼: 홀로그램 숫자 텍스트 & 눈금 비례 파티클 폭발
                    _diceVisualizer.PlayHologramEffect(d20);

                    // 눈금 비례 타격감 (눈금 크기 비례 진폭/주파수 셰이크 + 11~19 히트스톱)
                    if (ImpactFeedbackManager.Instance != null)
                    {
                        ImpactFeedbackManager.Instance.TriggerImpact(d20);
                    }
                    else
                    {
                        float shakeIntensity = Mathf.Lerp(_config.hitShakeMin, _config.hitShakeMax, (d20 - 2) / 17f);
                        CameraShakeController.Instance.Shake(shakeIntensity, 0.3f);

                        if (d20 >= _config.hitStopThreshold)
                        {
                            CameraShakeController.Instance.HitStop(_config.hitStopDuration);
                        }
                    }

                    string hitTitle = d20 >= 15 ? $"🔥 회심의 강타! [D20: {d20}]" : $"⚔️ 일반 적중 [D20: {d20}]";
                    ShowAnnouncement(hitTitle, _overlayTextColor, _overlayFontSize, 0.9f);
                    yield return new WaitForSeconds(0.45f);
                    break;
            }

            onFinished?.Invoke();
        }

        private void ShowAnnouncement(string text, Color color, int fontSize, float duration)
        {
            _overlayAnnouncementText = text;
            _overlayTextColor = color;
            _overlayFontSize = fontSize;
            StopCoroutine(nameof(FadeAnnouncementRoutine));
            StartCoroutine(FadeAnnouncementRoutine(duration));
        }

        private IEnumerator FadeAnnouncementRoutine(float duration)
        {
            _overlayAlpha = 1.0f;
            float elapsed = 0f;

            // 표시 유지
            yield return new WaitForSecondsRealtime(duration * 0.6f);

            // 페이드아웃
            float fadeTime = duration * 0.4f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlayAlpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
                yield return null;
            }

            _overlayAlpha = 0f;
            _overlayAnnouncementText = string.Empty;
        }

        private void Update()
        {
            // 플로팅 대미지 텍스트 시뮬레이션 갱신
            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var item = _floatingTexts[i];
                item.elapsed += Time.unscaledDeltaTime;
                item.worldPos += Vector3.up * Time.unscaledDeltaTime * 1.2f; // 위로 서서히 부유

                if (item.elapsed >= item.duration)
                {
                    _floatingTexts.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            InitStyles();

            int sw = Screen.width;
            int sh = Screen.height;

            // 1. 즉사 시네마틱 레터박스 (상/하단 블랙 바)
            if (_isLetterboxActive)
            {
                Color orig = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.88f);
                int barH = Mathf.RoundToInt(sh * 0.12f);
                GUI.DrawTexture(new Rect(0, 0, sw, barH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, sh - barH, sw, barH), Texture2D.whiteTexture);
                GUI.color = orig;
            }

            // 2. 화면 중앙 극단적 텍스트 알림 출력
            if (!string.IsNullOrEmpty(_overlayAnnouncementText) && _overlayAlpha > 0f)
            {
                _announcementStyle.fontSize = _overlayFontSize;

                // 텍스트 그림자
                _announcementStyle.normal.textColor = new Color(0f, 0f, 0f, _overlayAlpha * 0.95f);
                GUI.Label(new Rect(0, sh * 0.35f + 2, sw, 80), _overlayAnnouncementText, _announcementStyle);

                // 본문 텍스트
                Color textColor = _overlayTextColor;
                textColor.a = _overlayAlpha;
                _announcementStyle.normal.textColor = textColor;
                GUI.Label(new Rect(0, sh * 0.35f, sw, 80), _overlayAnnouncementText, _announcementStyle);
            }

            // 3. 대상 머리 위 플로팅 대미지 텍스트 렌더링
            Camera cam = Camera.main;
            if (cam != null)
            {
                for (int i = 0; i < _floatingTexts.Count; i++)
                {
                    var item = _floatingTexts[i];
                    Vector3 screenPoint = cam.WorldToScreenPoint(item.worldPos);
                    if (screenPoint.z > 0)
                    {
                        float alpha = 1f - Mathf.Clamp01(item.elapsed / item.duration);
                        float scalePulse = 1f + Mathf.Sin((item.elapsed / item.duration) * Mathf.PI) * 0.25f;
                        int size = Mathf.RoundToInt(item.fontSize * scalePulse);

                        _floatingDamageStyle.fontSize = size;

                        float guiX = screenPoint.x - 150;
                        float guiY = sh - screenPoint.y - 40;

                        // 그림자
                        _floatingDamageStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.9f);
                        GUI.Label(new Rect(guiX + 2, guiY + 2, 300, 50), item.text, _floatingDamageStyle);

                        // 본문
                        Color c = item.color;
                        c.a = alpha;
                        _floatingDamageStyle.normal.textColor = c;
                        GUI.Label(new Rect(guiX, guiY, 300, 50), item.text, _floatingDamageStyle);
                    }
                }
            }
        }

        private void InitStyles()
        {
            if (_announcementStyle == null)
            {
                _announcementStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_floatingDamageStyle == null)
            {
                _floatingDamageStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }
    }
}
