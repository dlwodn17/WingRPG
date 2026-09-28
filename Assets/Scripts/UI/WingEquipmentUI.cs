using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPG25D.Core.Wings;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 2] WingEquipmentUI.cs
    /// 날개 장착 및 상세 세트 정보 UI 컨트롤러
    /// - 캐릭터 상세 모달 확장:
    ///   - 중앙 캐릭터 프리뷰 정보를 기준으로 좌우 4개 장착 슬롯 버튼 배치
    ///     - 좌측 상단: 주 날개 좌 (MainLeft)
    ///     - 우측 상단: 주 날개 우 (MainRight)
    ///     - 좌측 하단: 부 날개 좌 (SubLeft)
    ///     - 우측 하단: 부 날개 우 (SubRight)
    ///   - 각 슬롯에 장착된 날개 아이콘/이름, 등급 테두리(3성 은색, 4성 보라색, 5성 금색) 및 추가 공격력 표시
    ///   - 빈 슬롯은 점선 윤곽선 플레이스홀더 표시
    /// - 세트 효과 인디케이터:
    ///   - 장착된 날개 세트 정보(예: '청룡의 서약 2/4' 또는 '4/4 활성화')를 텍스트 및 활성화 불빛 아이콘으로 표시
    ///   - 활성화된 2세트/4세트 효과 상세 설명 박스 제공
    /// - 슬롯 클릭 시 WingSelectionPopup을 호출하여 인벤토리에서 실시간 교체/장착 지원
    /// </summary>
    public class WingEquipmentUI : MonoBehaviour
    {
        [Header("연결 팝업 및 비주얼")]
        [SerializeField] private WingSelectionPopup _selectionPopup;
        [SerializeField] private CharacterWingVisual _wingVisual;

        [Header("UI 활성화 상태")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private CharacterInstance _currentCharacter;
        public CharacterInstance CurrentCharacter => _currentCharacter;

        // IMGUI 스타일
        private GUIStyle _modalBoxStyle;
        private GUIStyle _headerTitleStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _characterPreviewBoxStyle;
        private GUIStyle _slotFilledBoxStyle;
        private GUIStyle _slotEmptyBoxStyle;
        private GUIStyle _slotTitleStyle;
        private GUIStyle _setBoxStyle;
        private GUIStyle _closeButtonStyle;

        public event Action OnUIClosed;

        private void Awake()
        {
            if (_selectionPopup == null)
            {
                _selectionPopup = FindAnyObjectByType<WingSelectionPopup>();
            }
        }

        public void Open(CharacterInstance character, CharacterWingVisual visual = null)
        {
            if (character == null) return;

            _currentCharacter = character;
            if (visual != null) _wingVisual = visual;

            _isOpen = true;
            gameObject.SetActive(true);

            // 비주얼 동기화
            if (_wingVisual != null)
            {
                _wingVisual.UpdateVisual(_currentCharacter);
            }
        }

        public void Close()
        {
            _isOpen = false;
            if (_selectionPopup != null && _selectionPopup.IsOpen)
            {
                _selectionPopup.Close();
            }
            OnUIClosed?.Invoke();
        }

        public void Toggle(CharacterInstance character, CharacterWingVisual visual = null)
        {
            if (_isOpen) Close();
            else Open(character, visual);
        }

        private void HandleSlotClick(WingSlotType slot)
        {
            if (_selectionPopup == null)
            {
                _selectionPopup = FindAnyObjectByType<WingSelectionPopup>();
                if (_selectionPopup == null)
                {
                    var popGo = new GameObject("WingSelectionPopup");
                    popGo.transform.SetParent(transform.parent, false);
                    _selectionPopup = popGo.AddComponent<WingSelectionPopup>();
                }
            }

            _selectionPopup.Open(_currentCharacter, slot, _wingVisual, () =>
            {
                // 장착 변경 후 UI 및 비주얼 갱신
                if (_wingVisual != null)
                {
                    _wingVisual.UpdateVisual(_currentCharacter);
                }
            });
        }

        private void InitStyles()
        {
            if (_modalBoxStyle != null) return;

            _modalBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 20, 20)
            };

            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerTitleStyle.normal.textColor = new Color(1f, 0.9f, 0.35f);

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            _subHeaderStyle.normal.textColor = Color.white;

            _characterPreviewBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(15, 15, 15, 15)
            };

            _slotFilledBoxStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10)
            };

            _slotEmptyBoxStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _slotTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _slotTitleStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

            _setBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 12, 12)
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

            // 배경 어둡게(Dimmed) 처리
            GUI.Box(new Rect(0, 0, 1280, 720), GUIContent.none);

            // 중앙 모달창 (가로 840 x 세로 620)
            Rect modalRect = new Rect(220, 50, 840, 620);
            GUI.Box(modalRect, GUIContent.none, _modalBoxStyle);

            GUILayout.BeginArea(new Rect(modalRect.x + 20, modalRect.y + 15, modalRect.width - 40, modalRect.height - 30));

            // 1. 헤더 타이틀
            string starStr = new string('★', _currentCharacter.Rarity);
            GUILayout.Label($"🪽 {starStr} {_currentCharacter.CharacterName} - 날개 장비 공방", _headerTitleStyle);
            GUILayout.Space(2);
            GUILayout.Label($"레벨: {_currentCharacter.Level}  |  초월: {_currentCharacter.Transcendence}단  |  최종 공격력: {_currentCharacter.CurrentAttack} (기본 {_currentCharacter.BaseAttack} + 날개 보너스 +{_currentCharacter.TotalWingBonusDamage})", _subHeaderStyle);

            GUILayout.Space(15);

            // 2. 중앙 레이아웃: 좌측 2슬롯 - 중앙 프리뷰 - 우측 2슬롯
            GUILayout.BeginHorizontal();

            // [좌측 열]: 주 날개(좌), 부 날개(좌)
            GUILayout.BeginVertical(GUILayout.Width(250));
            DrawSlotButton(WingSlotType.MainLeft, "주 날개 (좌)", "Top-Left");
            GUILayout.Space(15);
            DrawSlotButton(WingSlotType.SubLeft, "부 날개 (좌)", "Bottom-Left");
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // [중앙 열]: 캐릭터 2.5D 프리뷰 및 스탯 카드
            GUILayout.BeginVertical(_characterPreviewBoxStyle, GUILayout.Width(270), GUILayout.Height(255));
            GUILayout.Label("【 영웅 2.5D 외형 프리뷰 】", _subHeaderStyle);
            GUILayout.FlexibleSpace();

            GUI.contentColor = _currentCharacter.Rarity == 5 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GUILayout.Label($"[ {_currentCharacter.CharacterName} ]", _headerTitleStyle);
            GUI.contentColor = Color.white;

            if (_currentCharacter.Saju != null)
            {
                var day = _currentCharacter.Saju.DayPillar;
                Color elemCol = GachaVisualDirector.GetFiveElementColor(day.Stem);
                GUI.contentColor = elemCol;
                GUILayout.Label($"본원(일주): {day.ToHanjaString()} ({day.Stem.ToKorean()}{day.Branch.ToKorean()})", _subHeaderStyle);
                GUI.contentColor = Color.white;
            }

            GUILayout.Space(8);
            int equippedCount = 0;
            foreach (var w in _currentCharacter.EquippedWings.Values) if (w != null) equippedCount++;
            GUILayout.Label($"장착 날개: {equippedCount} / 4 부위", _subHeaderStyle);

            int d20Bonus = WingSetDatabase.GetTotalD20RollBonus(_currentCharacter.EquippedWings.Values);
            if (d20Bonus > 0)
            {
                GUI.contentColor = new Color(0.4f, 1f, 0.6f);
                GUILayout.Label($"세트 효과 D20 보정치: +{d20Bonus}", _subHeaderStyle);
                GUI.contentColor = Color.white;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // [우측 열]: 주 날개(우), 부 날개(우)
            GUILayout.BeginVertical(GUILayout.Width(250));
            DrawSlotButton(WingSlotType.MainRight, "주 날개 (우)", "Top-Right");
            GUILayout.Space(15);
            DrawSlotButton(WingSlotType.SubRight, "부 날개 (우)", "Bottom-Right");
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(18);

            // 3. 하단: 세트 효과 인디케이터
            DrawSetBonusSection();

            GUILayout.FlexibleSpace();

            // 4. 하단 버튼 바
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("확인 / 닫기", _closeButtonStyle, GUILayout.Width(180), GUILayout.Height(42)))
            {
                Close();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }

        private void DrawSlotButton(WingSlotType slot, string slotLabel, string posTag)
        {
            var wing = _currentCharacter.GetEquippedWing(slot);
            bool isFilled = (wing != null);

            Color origBg = GUI.backgroundColor;
            if (isFilled)
            {
                // 등급별 배경/테두리 틴트 (5성 금, 4성 보라, 3성 은백)
                GUI.backgroundColor = WingSelectionPopup.GetRarityColor(wing.Rarity);
            }

            GUILayout.BeginVertical(isFilled ? _slotFilledBoxStyle : _slotEmptyBoxStyle, GUILayout.Height(120));
            GUI.backgroundColor = origBg;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"[{slotLabel}]", _slotTitleStyle);
            GUILayout.FlexibleSpace();
            if (isFilled)
            {
                string star = new string('★', wing.Rarity);
                GUI.contentColor = WingSelectionPopup.GetRarityColor(wing.Rarity);
                GUILayout.Label(star, _slotTitleStyle);
                GUI.contentColor = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (isFilled)
            {
                GUILayout.Label(wing.WingName, GUI.skin.label);
                GUILayout.Label($"공격력: +{wing.BonusDamage}", GUI.skin.label);

                if (!string.IsNullOrEmpty(wing.SetID))
                {
                    var setDef = WingSetDatabase.GetSetDefinition(wing.SetID);
                    string sName = setDef != null ? setDef.SetName : wing.SetID;
                    GUI.contentColor = new Color(0.4f, 0.85f, 1f);
                    GUILayout.Label($"세트: {sName}", GUI.skin.label);
                    GUI.contentColor = Color.white;
                }
                else
                {
                    GUILayout.Label("일반 (단일)", GUI.skin.label);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("교체 / 해제", GUILayout.Height(24)))
                {
                    HandleSlotClick(slot);
                }
            }
            else
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("── 빈 슬롯 ──", _subHeaderStyle);
                GUILayout.Label("[ + 날개 장착 ]", _subHeaderStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("장착하기", GUILayout.Height(26)))
                {
                    HandleSlotClick(slot);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSetBonusSection()
        {
            GUILayout.BeginVertical(_setBoxStyle, GUILayout.Height(135));

            GUILayout.BeginHorizontal();
            GUILayout.Label("✨ 활성 날개 세트 효과 (Set Bonuses)", _slotTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            var activeSets = WingSetDatabase.EvaluateActiveSetEffects(_currentCharacter.EquippedWings.Values);

            if (activeSets.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("활성화된 세트 효과가 없습니다.", _subHeaderStyle);
                GUILayout.Label("💡 동일한 4~5성 세트 날개를 2개 또는 4개 착용하면 강력한 세트 보너스가 발동합니다.", _subHeaderStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                foreach (var set in activeSets)
                {
                    GUILayout.BeginHorizontal();

                    // 세트명 및 장착 부위 인디케이터
                    string bulb2 = set.Has2SetBonus ? "● 2세트" : "○ 2세트";
                    string bulb4 = set.Has4SetBonus ? "● 4세트 [완성!]" : "○ 4세트";

                    GUI.contentColor = set.Has4SetBonus ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 0.9f, 1f);
                    GUILayout.Label($"【{set.SetName}】 ({set.EquippedCount}/4 장착)", _slotTitleStyle, GUILayout.Width(220));

                    GUI.contentColor = set.Has2SetBonus ? new Color(0.3f, 1f, 0.5f) : Color.gray;
                    GUILayout.Label($"[{bulb2}]", _slotTitleStyle, GUILayout.Width(90));

                    GUI.contentColor = set.Has4SetBonus ? new Color(1f, 0.85f, 0.2f) : Color.gray;
                    GUILayout.Label($"[{bulb4}]", _slotTitleStyle, GUILayout.Width(130));

                    GUI.contentColor = Color.white;
                    GUILayout.Label($"총 공격력 +{set.BonusDamage}  |  D20 +{set.D20RollBonus}", GUI.skin.label);

                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);
                    GUILayout.Label($"   효과: {set.Description}", GUI.skin.label);
                }
            }

            GUILayout.EndVertical();
        }
    }
}
