using System;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Wings;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 4] WingAcquisitionPopup.cs
    /// 날개 획득 시 알림 배너 및 비주얼 피드백 연출 컴포넌트
    /// - 전투 중 D20 눈금 1(대실패) 또는 20(즉사), 적 처치, 필드 상자 개봉 시 실시간 알림 배너 출력
    /// - 배너 내용: "🎉 날개 획득! [5★ 청룡의 천공익 [주 날개(좌)] (+65 Atk, 세트: 청룡의 서약)]"
    /// - 등급별 비주얼 피드백 (3성 은빛/청색, 4성 보라색, 5성 황금/무지개 찬란한 광원)
    /// - 빠른 연속 획득 시 큐(Queue)를 통한 순차 출력 및 자동 페이드아웃(Fade-out)
    /// </summary>
    public class WingAcquisitionPopup : MonoBehaviour
    {
        private static WingAcquisitionPopup _instance;
        public static WingAcquisitionPopup Instance => _instance;

        private class DropBannerEntry
        {
            public WingItemInstance Wing;
            public string Reason;
            public float RemainingTime;
            public float TotalDuration;
        }

        [Header("배너 연출 설정")]
        [SerializeField] private float _displayDuration = 3.0f;
        [SerializeField] private bool _autoSubscribeToDropManager = true;

        // 획득 큐 및 현재 활성 배너
        private readonly Queue<DropBannerEntry> _bannerQueue = new Queue<DropBannerEntry>();
        private DropBannerEntry _currentBanner = null;

        // 중복 알림 방지용 최근 아이템 ID 캐시
        private readonly HashSet<string> _recentlyDisplayedIDs = new HashSet<string>();

        // IMGUI 스타일
        private GUIStyle _bannerBoxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _reasonStyle;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_autoSubscribeToDropManager)
            {
                SubscribeEvents();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_instance == this) _instance = null;
        }

        public void SubscribeEvents()
        {
            if (WingDropManager.Instance != null)
            {
                WingDropManager.Instance.OnWingDropped -= HandleWingDropped;
                WingDropManager.Instance.OnWingDropped += HandleWingDropped;
            }

            if (WingInventoryManager.Instance != null)
            {
                WingInventoryManager.Instance.OnWingAdded -= HandleWingAdded;
                WingInventoryManager.Instance.OnWingAdded += HandleWingAdded;
            }
        }

        public void UnsubscribeEvents()
        {
            if (WingDropManager.Instance != null)
            {
                WingDropManager.Instance.OnWingDropped -= HandleWingDropped;
            }

            if (WingInventoryManager.Instance != null)
            {
                WingInventoryManager.Instance.OnWingAdded -= HandleWingAdded;
            }
        }

        private void HandleWingDropped(WingItemInstance wing, string reason)
        {
            ShowAcquisition(wing, reason);
        }

        private void HandleWingAdded(WingItemInstance wing)
        {
            // DropManager에서 이미 배너를 띄운 경우 중복 방지
            if (wing != null && !_recentlyDisplayedIDs.Contains(wing.ItemInstanceID))
            {
                ShowAcquisition(wing, "인벤토리 획득");
            }
        }

        /// <summary>
        /// [세부 개발 명세 4] 날개 획득 배너 출력 트리거
        /// </summary>
        public void ShowAcquisition(WingItemInstance wing, string reason = "")
        {
            if (wing == null) return;

            _recentlyDisplayedIDs.Add(wing.ItemInstanceID);

            var entry = new DropBannerEntry
            {
                Wing = wing,
                Reason = reason,
                RemainingTime = _displayDuration,
                TotalDuration = _displayDuration
            };

            if (_currentBanner == null)
            {
                _currentBanner = entry;
            }
            else
            {
                _bannerQueue.Enqueue(entry);
            }

            Debug.Log($"🪽 [WingAcquisitionPopup] 배너 출력: {wing.WingName} ({reason})");
        }

        private void Update()
        {
            if (_currentBanner != null)
            {
                _currentBanner.RemainingTime -= Time.deltaTime;
                if (_currentBanner.RemainingTime <= 0f)
                {
                    _currentBanner = _bannerQueue.Count > 0 ? _bannerQueue.Dequeue() : null;
                }
            }
            else if (_bannerQueue.Count > 0)
            {
                _currentBanner = _bannerQueue.Dequeue();
            }
        }

        private void InitStyles()
        {
            if (_bannerBoxStyle != null) return;

            _bannerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 10, 10),
                alignment = TextAnchor.MiddleCenter
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subStyle.normal.textColor = Color.white;

            _reasonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            _reasonStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }

        private void OnGUI()
        {
            if (_currentBanner == null || Application.isBatchMode) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            var wing = _currentBanner.Wing;

            // 페이드 인/아웃 알파 계산 (등장 시 0.3초, 퇴장 시 0.5초)
            float elapsed = _currentBanner.TotalDuration - _currentBanner.RemainingTime;
            float fadeIn = Mathf.Clamp01(elapsed / 0.3f);
            float fadeOut = Mathf.Clamp01(_currentBanner.RemainingTime / 0.5f);
            float alpha = Mathf.Min(fadeIn, fadeOut);

            // 등급별 고유 테마 컬러
            Color rarityColor = GetRarityBannerColor(wing.Rarity);
            rarityColor.a = alpha;

            // 살짝 위아래로 뜨는 바운스 모션
            float yOffset = Mathf.Sin(elapsed * 4f) * 3f;

            // 화면 상단 중앙 배너 (가로 680 x 세로 90)
            Rect bannerRect = new Rect(300, 30 + yOffset, 680, 85);

            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = rarityColor;
            GUI.Box(bannerRect, GUIContent.none, _bannerBoxStyle);
            GUI.backgroundColor = origBg;

            GUILayout.BeginArea(new Rect(bannerRect.x + 10, bannerRect.y + 8, bannerRect.width - 20, bannerRect.height - 16));

            // 1. 배너 헤더
            string stars = new string('★', wing.Rarity);
            _titleStyle.normal.textColor = rarityColor;
            GUILayout.Label($"✨ [날개 획득!] {stars} {wing.WingName}", _titleStyle);

            // 2. 부위 및 추가 공격력 정보
            string slotName = WingSelectionPopup.GetSlotKoreanName(wing.SlotType);
            string setInfo = !string.IsNullOrEmpty(wing.SetID) ? $" | 세트: {wing.SetID}" : "";
            GUILayout.Label($"부위: {slotName}  |  기본 공격력 +{wing.BonusDamage}{setInfo}", _subStyle);

            // 3. 획득 경로 사유
            if (!string.IsNullOrEmpty(_currentBanner.Reason))
            {
                string reasonKor = FormatReasonText(_currentBanner.Reason);
                GUILayout.Label($"획득 경로: {reasonKor}", _reasonStyle);
            }

            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }

        private static Color GetRarityBannerColor(int rarity)
        {
            return rarity switch
            {
                5 => new Color(1.0f, 0.85f, 0.25f, 1.0f),  // 5성 황금/무지개 광원
                4 => new Color(0.85f, 0.5f, 1.0f, 1.0f),   // 4성 보라
                _ => new Color(0.45f, 0.85f, 1.0f, 1.0f)   // 3성 은백/청색
            };
        }

        private static string FormatReasonText(string reason)
        {
            if (reason.IndexOf("DiceCritSuccess_20", StringComparison.OrdinalIgnoreCase) >= 0)
                return "🌟 D20 절대 성공(20) - '절대 행운' 보상!";
            if (reason.IndexOf("DiceCritFail_1", StringComparison.OrdinalIgnoreCase) >= 0)
                return "💔 D20 대실패(1) - '불운의 위로' 보상!";
            if (reason.IndexOf("EnemyDefeated_Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return "👑 보스 몬스터 처치 보상!";
            if (reason.IndexOf("EnemyDefeated", StringComparison.OrdinalIgnoreCase) >= 0)
                return "⚔️ 몬스터 처치 전리품!";
            if (reason.IndexOf("ChestOpened", StringComparison.OrdinalIgnoreCase) >= 0)
                return "🎁 보물 상자 개봉 보상!";

            return reason;
        }
    }
}
