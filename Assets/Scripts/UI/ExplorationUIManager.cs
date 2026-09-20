using System;
using UnityEngine;
using RPG25D.Data;
using RPG25D.Gameplay.Exploration;

namespace RPG25D.UI
{
    /// <summary>
    /// 2.5D 탐색 맵 인게임 HUD UI 매니저 (IMGUI 기반)
    /// 골드, 인벤토리 요약, 상호작용 힌트([E] 상자 열기 등)를 렌더링합니다.
    /// </summary>
    public class ExplorationUIManager : MonoBehaviour
    {
        [Header("참조 설정")]
        [SerializeField] private ExplorationPlayerController _player;
        [SerializeField] private EncounterManager _encounterManager;

        private GUIStyle _headerStyle;
        private GUIStyle _promptBoxStyle;
        private GUIStyle _warningStyle;

        private void Awake()
        {
            if (_player == null) _player = FindAnyObjectByType<ExplorationPlayerController>();
            if (_encounterManager == null) _encounterManager = FindAnyObjectByType<EncounterManager>();
        }

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = Color.white;

            _promptBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _promptBoxStyle.normal.textColor = new Color(1f, 0.95f, 0.3f);

            _warningStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _warningStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
        }

        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            InitStyles();

            Matrix4x4 origMatrix = GUI.matrix;
            float rx = (float)Screen.width / 1280f;
            float ry = (float)Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(rx, ry, 1f));

            // 1. 좌상단 탐색 정보 패널 (골드, 인벤토리, 처치 심볼)
            DrawTopInfoPanel();

            // 2. 상호작용 힌트 배너
            if (_player != null && _player.CurrentFocusInteractable != null)
            {
                DrawInteractionPrompt(_player.CurrentFocusInteractable.Prompt);
            }

            // 3. 인카운터 발생 알림
            if (_encounterManager != null && _encounterManager.IsEncounterTriggered)
            {
                DrawEncounterWarning();
            }

            GUI.matrix = origMatrix;
        }

        private void DrawTopInfoPanel()
        {
            int gold = GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0;
            int defeated = GameManager.Instance != null ? GameManager.Instance.DefeatedEncounterIds.Count : 0;
            int itemsCount = GameManager.Instance != null ? GameManager.Instance.InventoryItems.Count : 0;

            string info = $"  🧭 [필드 탐색 모드]   💰 골드: {gold} G   |   🎒 소지품: {itemsCount}개   |   ⚔️ 격파한 적: {defeated}마리";
            GUI.Box(new Rect(15, 15, 600, 36), info, _headerStyle);
        }

        private void DrawInteractionPrompt(string promptText)
        {
            string label = $"✨ [ {promptText} ]";
            GUI.Box(new Rect(440, 600, 400, 48), label, _promptBoxStyle);
        }

        private void DrawEncounterWarning()
        {
            GUI.Box(new Rect(340, 260, 600, 80), "⚠️ 몬스터와 조우했습니다!\n전투 씬으로 진입합니다...", _warningStyle);
        }
    }
}
