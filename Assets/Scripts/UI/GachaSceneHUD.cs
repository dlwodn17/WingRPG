using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// 가챠 씬 메인 인터랙션 HUD (소환 실행, 파티 편성 토글, 골드 표시, 탐색 씬 전환)
    /// </summary>
    public class GachaSceneHUD : MonoBehaviour
    {
        [Header("연결 컨트롤러")]
        [SerializeField] private GachaVisualDirector _director;
        [SerializeField] private PartyFormationUI _partyUI;
        [SerializeField] private GachaResultUI _resultUI;

        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _goldStyle;

        private void Start()
        {
            if (_director == null) _director = FindAnyObjectByType<GachaVisualDirector>();
            if (_partyUI == null) _partyUI = FindAnyObjectByType<PartyFormationUI>();
            if (_resultUI == null) _resultUI = FindAnyObjectByType<GachaResultUI>();
        }

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(1f, 0.95f, 0.4f);

            _goldStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _goldStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            // 다른 모달(결과창이나 파티편성)이 크게 열려있으면 메인 HUD 최소화
            if (_partyUI != null && _partyUI.IsOpen) return;
            if (_resultUI != null && _resultUI.IsOpen) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            // 1. 상단 바 (타이틀 및 보유 골드)
            GUILayout.BeginArea(new Rect(40, 25, 1200, 50));
            GUILayout.BeginHorizontal();

            GUILayout.Label("🔮 천간·지지(10간 12지) 사주팔자 가챠 신단", _headerStyle, GUILayout.Width(450), GUILayout.Height(40));

            GUILayout.FlexibleSpace();

            int gold = GameManagerData.Instance != null ? GameManagerData.Instance.CurrentGold : 500;
            GUILayout.Label($"💰 보유 골드: {gold:N0} G", _goldStyle, GUILayout.Width(220), GUILayout.Height(40));

            GUILayout.Space(10);

            if (GUILayout.Button("+1,000G 충전", GUILayout.Width(110), GUILayout.Height(40)))
            {
                if (GameManagerData.Instance != null)
                {
                    GameManagerData.Instance.AddGold(1000);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 2. 하단 컨트롤 바
            bool isRolling = _director != null && _director.IsSequenceRunning;

            GUILayout.BeginArea(new Rect(340, 610, 600, 75));
            GUILayout.BeginHorizontal();

            GUI.enabled = !isRolling;

            if (GUILayout.Button("🔮 1회 소환 (100G)", _buttonStyle, GUILayout.Width(190), GUILayout.Height(55)))
            {
                if (_director != null)
                {
                    _director.RollSingle();
                }
            }

            GUILayout.Space(15);

            if (GUILayout.Button("🛡️ 파티 편성 / 보관소", _buttonStyle, GUILayout.Width(190), GUILayout.Height(55)))
            {
                if (_partyUI != null)
                {
                    _partyUI.OpenUI();
                }
            }

            GUILayout.Space(15);

            if (GUILayout.Button("🗺️ 탐색 필드로 이동", _buttonStyle, GUILayout.Width(170), GUILayout.Height(55)))
            {
                SceneManager.LoadScene("Level01_Exploration");
            }

            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }
    }
}
