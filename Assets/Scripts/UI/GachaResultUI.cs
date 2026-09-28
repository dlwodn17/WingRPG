using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RPG25D.Core.Gacha;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 1] GachaResultUI.cs
    /// 가챠 소환 결과 창 및 중복 획득 시 사주 비교/선택 UI
    /// - 확정된 캐릭터 이름, 등급(3~5성 별 개수), 사주팔자 8글자 표시
    /// - 중복 획득 시: [기존 사주] vs [새로 굴린 사주] 비교 및 [유지] / [교체] 선택권 제공
    /// - [확인], [다시 뽑기], [파티 편성] 버튼 제공
    /// - IMGUI 1280x720 가상 좌표계 및 UGUI 이벤트 듀얼 지원
    /// </summary>
    public class GachaResultUI : MonoBehaviour
    {
        [Header("연결 디렉터")]
        [SerializeField] private GachaVisualDirector _director;

        [Header("결과 창 상태")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private CharacterInstance _currentResult;
        private DuplicateGachaResult _duplicateResult;

        private bool _isDuplicateChoiceActive = false;
        private string _statusMessage = string.Empty;

        // IMGUI 스타일
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _rarityStyle;
        private GUIStyle _sajuStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _dupBoxStyle;

        private void Start()
        {
            if (_director == null) _director = FindAnyObjectByType<GachaVisualDirector>();
            if (_director != null)
            {
                _director.OnGachaCompleted += ShowResult;
            }
        }

        private void OnDestroy()
        {
            if (_director != null)
            {
                _director.OnGachaCompleted -= ShowResult;
            }
        }

        /// <summary>
        /// 결과 창 노출
        /// </summary>
        public void ShowResult(CharacterInstance character, DuplicateGachaResult duplicateResult)
        {
            _currentResult = character;
            _duplicateResult = duplicateResult;
            _isDuplicateChoiceActive = duplicateResult != null;
            _statusMessage = string.Empty;
            _isOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _isOpen = false;
            _currentResult = null;
            _duplicateResult = null;
            _isDuplicateChoiceActive = false;
            _statusMessage = string.Empty;
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(25, 25, 25, 25)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = Color.white;

            _rarityStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _sajuStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _sajuStyle.normal.textColor = new Color(1f, 0.95f, 0.35f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            _actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            _dupBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 15, 15)
            };
        }

        private void OnGUI()
        {
            if (!_isOpen || _currentResult == null || Application.isBatchMode) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            // 배경 딤드(Dimmed) 처리
            GUI.Box(new Rect(0, 0, 1280, 720), GUIContent.none);

            // 중앙 결과 팝업 윈도우
            Rect popupRect = new Rect(340, 80, 600, 560);
            GUI.Box(popupRect, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(popupRect.x + 20, popupRect.y + 20, popupRect.width - 40, popupRect.height - 40));

            // 1. 등급 및 이름
            Color gradeColor = _currentResult.Rarity switch
            {
                5 => new Color(1f, 0.85f, 0.2f),
                4 => new Color(0.75f, 0.4f, 1f),
                _ => new Color(0.35f, 0.75f, 1f)
            };

            _rarityStyle.normal.textColor = gradeColor;
            string stars = new string('★', _currentResult.Rarity);
            GUILayout.Label($"{stars} [{_currentResult.Rarity}성 소환 성공!]", _rarityStyle);
            GUILayout.Space(5);
            GUILayout.Label(_currentResult.CharacterName, _titleStyle);

            GUILayout.Space(15);

            // 2. 사주팔자 각인 결과
            if (_currentResult.Saju != null)
            {
                GUILayout.Label($"고유 사주: {_currentResult.Saju.ToEightCharacters()} ({_currentResult.Saju.ToEightHanjaCharacters()})", _sajuStyle, GUILayout.Height(45));
                GUILayout.Space(5);
                GUILayout.Label(_currentResult.Saju.ToFullKoreanString(), GUI.skin.label);
            }

            GUILayout.Space(15);

            // 3. 중복 획득 시 사주 명식 비교/교체 패널
            if (_isDuplicateChoiceActive && _duplicateResult != null)
            {
                GUILayout.BeginVertical(_dupBoxStyle);
                GUI.contentColor = new Color(1f, 0.95f, 0.3f);
                GUILayout.Label($"✨ [중복 획득] 전용 영혼 조각 +{_duplicateResult.ShardsAwarded}개 적립! (보유: {_duplicateResult.TotalShardsNow}개)", _titleStyle);
                GUI.contentColor = Color.white;
                GUILayout.Label("이미 보유 중인 영웅입니다. 새로 굴린 사주로 교체하시겠습니까?", GUI.skin.label);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();

                // 기존 사주
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(250));
                GUILayout.Label("【기존 사주】", GUI.skin.label);
                GUILayout.Label(_duplicateResult.OldSaju?.ToEightCharacters() ?? "미지정", _sajuStyle, GUILayout.Height(35));
                GUILayout.Label(_duplicateResult.OldSaju?.ToFullKoreanString() ?? "", GUI.skin.label);
                if (GUILayout.Button("기존 사주 유지", _buttonStyle, GUILayout.Height(36)))
                {
                    _statusMessage = "기존 사주를 유지하기로 결정했습니다.";
                    _isDuplicateChoiceActive = false;
                }
                GUILayout.EndVertical();

                GUILayout.Space(15);

                // 신규 사주
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(250));
                GUILayout.Label("【신규 사주】", GUI.skin.label);
                GUILayout.Label(_duplicateResult.NewSaju?.ToEightCharacters() ?? "미지정", _sajuStyle, GUILayout.Height(35));
                GUILayout.Label(_duplicateResult.NewSaju?.ToFullKoreanString() ?? "", GUI.skin.label);
                if (GUILayout.Button("새 사주로 교체(Overwrite)", _buttonStyle, GUILayout.Height(36)))
                {
                    bool replaced = GachaDuplicateHandler.Instance.UpdateCharacterSaju(_duplicateResult.TargetInstanceID, _duplicateResult.NewSaju);
                    _statusMessage = replaced ? "새로운 사주팔자로 교체되었습니다!" : "사주 교체에 실패했습니다.";
                    _isDuplicateChoiceActive = false;
                }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(8);
                GUI.contentColor = new Color(0.2f, 1f, 0.5f);
                GUILayout.Label(_statusMessage, GUI.skin.label);
                GUI.contentColor = Color.white;
            }

            GUILayout.FlexibleSpace();

            // 4. 하단 액션 버튼
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("확인", _actionButtonStyle, GUILayout.Height(45)))
            {
                Close();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("다시 뽑기 (100G)", _actionButtonStyle, GUILayout.Height(45)))
            {
                Close();
                if (_director != null)
                {
                    _director.RollSingle();
                }
            }

            GUILayout.Space(10);

            var partyUI = FindAnyObjectByType<PartyFormationUI>();
            if (partyUI != null)
            {
                if (GUILayout.Button("파티 편성 가기", _actionButtonStyle, GUILayout.Height(45)))
                {
                    Close();
                    partyUI.OpenUI();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }
    }
}
