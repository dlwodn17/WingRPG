using System;
using UnityEngine;
using UnityEngine.UI;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 2] CharacterDetailModal.cs
    /// 캐릭터 상세 정보 팝업 모달
    /// - 기본 공격력, 레벨, 초월 단계(Transcendence) 표시
    /// - 사주팔자 4개 기둥(년/월/일/시)의 천간·지지 한자 및 오행 색상(목:청, 화:적, 토:황, 금:백, 수:흑) 시각화
    /// - 일주(DayPillar, 본원) 강조 표시
    /// - UGUI 및 IMGUI 듀얼 지원으로 헤드리스 및 런타임 독립 동작 보장
    /// </summary>
    public class CharacterDetailModal : MonoBehaviour
    {
        [Header("모달 상태")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private CharacterInstance _currentCharacter;
        public CharacterInstance CurrentCharacter => _currentCharacter;

        // IMGUI 스타일
        private GUIStyle _modalBoxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _pillarHeaderStyle;
        private GUIStyle _pillarHanjaStyle;
        private GUIStyle _closeButtonStyle;

        public event Action OnModalClosed;

        public void Open(CharacterInstance character)
        {
            if (character == null) return;
            _currentCharacter = character;
            _isOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _isOpen = false;
            OnModalClosed?.Invoke();
        }

        private void InitStyles()
        {
            if (_modalBoxStyle != null) return;

            _modalBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 20, 20)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            _subStyle.normal.textColor = Color.white;

            _pillarHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _pillarHeaderStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _pillarHanjaStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (!_isOpen || _currentCharacter == null || Application.isBatchMode) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            // 배경 딤드(Dimmed) 처리
            GUI.Box(new Rect(0, 0, 1280, 720), GUIContent.none);

            // 중앙 모달 윈도우 (가로 640 x 세로 520)
            Rect modalRect = new Rect(320, 100, 640, 520);
            GUI.Box(modalRect, GUIContent.none, _modalBoxStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 20, modalRect.y + 20, modalRect.width - 40, modalRect.height - 40));

            // 1. 캐릭터 기본 정보
            string starStr = new string('★', _currentCharacter.Rarity);
            GUILayout.Label($"{starStr} {_currentCharacter.CharacterName}", _titleStyle);
            GUILayout.Space(5);
            GUILayout.Label($"레벨: {_currentCharacter.Level}  |  초월: {_currentCharacter.Transcendence}단계  |  공격력: {_currentCharacter.CurrentAttack} (기본 {_currentCharacter.BaseAttack})", _subStyle);

            GUILayout.Space(25);
            GUILayout.Label("─── 고유 사주팔자(四柱八字) 명식 ───", _subStyle);
            GUILayout.Space(15);

            // 2. 4개 기둥 시각화 (시주 -> 일주 -> 월주 -> 년주 전통 배치)
            if (_currentCharacter.Saju != null)
            {
                var saju = _currentCharacter.Saju;
                SajuPillar[] pillars = new SajuPillar[] { saju.HourPillar, saju.DayPillar, saju.MonthPillar, saju.YearPillar };
                string[] titles = new string[] { "시주 (時柱)", "★ 일주 (日柱·본원)", "월주 (月柱)", "년주 (年柱)" };

                GUILayout.BeginHorizontal();
                for (int i = 0; i < 4; i++)
                {
                    var p = pillars[i];
                    bool isDayPillar = (i == 1); // 일주 강조

                    GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(140), GUILayout.Height(190));

                    // 기둥 제목
                    GUI.contentColor = isDayPillar ? new Color(1f, 0.95f, 0.2f) : Color.white;
                    GUILayout.Label(titles[i], _pillarHeaderStyle);

                    // 천간 (Heavenly Stem)
                    Color stemColor = GachaVisualDirector.GetFiveElementColor(p.Stem);
                    GUI.contentColor = stemColor;
                    GUILayout.Label($"{p.Stem.ToHanja()} ({p.Stem.ToKorean()})", _pillarHanjaStyle, GUILayout.Height(55));

                    // 지지 (Earthly Branch)
                    Color branchColor = GetBranchFiveElementColor(p.Branch);
                    GUI.contentColor = branchColor;
                    GUILayout.Label($"{p.Branch.ToHanja()} ({p.Branch.ToKorean()})", _pillarHanjaStyle, GUILayout.Height(55));

                    // 오행 표기
                    GUI.contentColor = Color.white;
                    GUILayout.Label($"{GetStemElementName(p.Stem)} / {GetBranchElementName(p.Branch)}", _subStyle);

                    GUILayout.EndVertical();

                    if (i < 3) GUILayout.Space(8);
                }
                GUILayout.EndHorizontal();

                GUI.contentColor = Color.white;
                GUILayout.Space(12);
                GUILayout.Label($"전체 명식: {saju.ToFullKoreanString()} ({saju.ToEightHanjaCharacters()})", _subStyle);
            }

            GUILayout.FlexibleSpace();

            // 3. 닫기 버튼
            if (GUILayout.Button("확인 / 닫기", _closeButtonStyle, GUILayout.Height(45)))
            {
                Close();
            }

            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }

        public static Color GetBranchFiveElementColor(EarthlyBranch branch)
        {
            return branch switch
            {
                EarthlyBranch.In or EarthlyBranch.Myo => new Color(0.2f, 0.85f, 0.4f),     // 인/묘: 목(木)
                EarthlyBranch.Sa or EarthlyBranch.O => new Color(0.95f, 0.3f, 0.2f),       // 사/오: 화(火)
                EarthlyBranch.Jin or EarthlyBranch.Sul or EarthlyBranch.Chuk or EarthlyBranch.Mi => new Color(0.95f, 0.85f, 0.25f), // 진/술/축/미: 토(土)
                EarthlyBranch.Sin_B or EarthlyBranch.Yu => new Color(0.9f, 0.95f, 1f),     // 신/유: 금(金)
                EarthlyBranch.Hae or EarthlyBranch.Ja => new Color(0.25f, 0.45f, 0.95f),   // 해/자: 수(水)
                _ => Color.white
            };
        }

        public static string GetStemElementName(HeavenlyStem stem)
        {
            return stem switch
            {
                HeavenlyStem.Gap => "양목(陽木)",
                HeavenlyStem.Eul => "음목(陰木)",
                HeavenlyStem.Byeong => "양화(陽火)",
                HeavenlyStem.Jeong => "음화(陰火)",
                HeavenlyStem.Mu => "양토(陽土)",
                HeavenlyStem.Gi => "음토(陰土)",
                HeavenlyStem.Gyeong => "양금(陽金)",
                HeavenlyStem.Sin => "음금(陰金)",
                HeavenlyStem.Im => "양수(陽水)",
                HeavenlyStem.Gye => "음수(陰水)",
                _ => "미상"
            };
        }

        public static string GetBranchElementName(EarthlyBranch branch)
        {
            return branch switch
            {
                EarthlyBranch.In or EarthlyBranch.Myo => "목(木)",
                EarthlyBranch.Sa or EarthlyBranch.O => "화(火)",
                EarthlyBranch.Jin or EarthlyBranch.Sul or EarthlyBranch.Chuk or EarthlyBranch.Mi => "토(土)",
                EarthlyBranch.Sin_B or EarthlyBranch.Yu => "금(金)",
                EarthlyBranch.Hae or EarthlyBranch.Ja => "수(水)",
                _ => "미상"
            };
        }
    }
}
