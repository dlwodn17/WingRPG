using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPG25D.Core.Wings;
using RPG25D.Core.Inventory;
using RPG25D.Core.Gacha;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 3] WingSelectionPopup.cs
    /// 날개 인벤토리 선택 및 장착/교체/해제 전용 팝업 모달
    /// - 장착 슬롯 클릭 시 호출되며, 해당 부위(TargetSlot)에 부합하는 날개만 필터링하여 스크롤 그리드에 표시
    /// - 선택한 날개의 상세 스탯(기본 대미지 가산치, 세트명, 등급) 노출
    /// - [장착 (Equip)], [해제 (Unequip)], [취소] 버튼 제공
    /// - 장착 완료 시 CharacterInstance.EquipWing() 및 CharacterWingVisual.UpdateVisual() 트리거 및 영속 데이터 저장
    /// </summary>
    public class WingSelectionPopup : MonoBehaviour
    {
        [Header("팝업 상태")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private CharacterInstance _targetCharacter;
        private WingSlotType _targetSlot = WingSlotType.MainLeft;
        private CharacterWingVisual _wingVisual;
        private Action _onChangedCallback;

        private WingItemInstance _selectedWing;
        private Vector2 _scrollPos = Vector2.zero;

        // IMGUI 스타일
        private GUIStyle _popupBoxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _itemButtonStyle;
        private GUIStyle _selectedItemStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _cancelButtonStyle;

        public event Action<WingItemInstance> OnWingEquipped;
        public event Action<WingSlotType> OnWingUnequipped;
        public event Action OnPopupClosed;

        public void Open(
            CharacterInstance character,
            WingSlotType slot,
            CharacterWingVisual visual = null,
            Action onChangedCallback = null)
        {
            if (character == null) return;

            _targetCharacter = character;
            _targetSlot = slot;
            _wingVisual = visual;
            _onChangedCallback = onChangedCallback;

            // 현재 장착 중인 날개로 초기 선택
            _selectedWing = character.GetEquippedWing(slot);
            _isOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _isOpen = false;
            OnPopupClosed?.Invoke();
        }

        /// <summary>
        /// 선택한 날개 장착 실행
        /// </summary>
        public void ExecuteEquip(WingItemInstance wingToEquip)
        {
            if (_targetCharacter == null || wingToEquip == null) return;

            // 1. 기존 장착 날개 해제 및 인벤토리 회수
            var oldWing = _targetCharacter.GetEquippedWing(_targetSlot);
            if (oldWing != null)
            {
                _targetCharacter.UnequipWing(_targetSlot);
                WingInventoryManager.Instance.AddWing(oldWing);
            }

            // 2. 인벤토리에서 새 날개 제거 후 캐릭터에 장착
            WingInventoryManager.Instance.RemoveWing(wingToEquip.ItemInstanceID);
            _targetCharacter.EquipWing(_targetSlot, wingToEquip);

            // 3. 비주얼 갱신
            if (_wingVisual != null)
            {
                _wingVisual.UpdateVisual(_targetCharacter);
            }

            // 4. 영속 데이터 저장
            SaveData();

            OnWingEquipped?.Invoke(wingToEquip);
            _onChangedCallback?.Invoke();
            Close();
        }

        /// <summary>
        /// 현재 슬롯 날개 장착 해제
        /// </summary>
        public void ExecuteUnequip()
        {
            if (_targetCharacter == null) return;

            var oldWing = _targetCharacter.UnequipWing(_targetSlot);
            if (oldWing != null)
            {
                WingInventoryManager.Instance.AddWing(oldWing);
            }

            if (_wingVisual != null)
            {
                _wingVisual.UpdateVisual(_targetCharacter);
            }

            SaveData();

            OnWingUnequipped?.Invoke(_targetSlot);
            _onChangedCallback?.Invoke();
            Close();
        }

        private void SaveData()
        {
            var inv = CharacterInventoryManager.Instance;
            var gacha = GachaDuplicateHandler.Instance;
            var party = PartyFormationManager.Instance;
            UserDataPersistence.SaveToPlayerPrefs(inv, gacha, party);

            if (GameManagerData.Instance != null)
            {
                UserDataPersistence.SyncToGameManager(GameManagerData.Instance, inv, gacha, party);
            }
        }

        private void InitStyles()
        {
            if (_popupBoxStyle != null) return;

            _popupBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 20, 20)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.35f);

            _itemButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13
            };

            _selectedItemStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _statLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            _statLabelStyle.normal.textColor = Color.white;

            _actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            _cancelButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14
            };
        }

        private void OnGUI()
        {
            if (!_isOpen || _targetCharacter == null || Application.isBatchMode) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            // 배경 어둡게(Dimmed) 처리
            GUI.Box(new Rect(0, 0, 1280, 720), GUIContent.none);

            // 중앙 팝업 (가로 720 x 세로 540)
            Rect popupRect = new Rect(280, 90, 720, 540);
            GUI.Box(popupRect, GUIContent.none, _popupBoxStyle);

            GUILayout.BeginArea(new Rect(popupRect.x + 20, popupRect.y + 20, popupRect.width - 40, popupRect.height - 40));

            // 1. 헤더: 슬롯명 및 대상 캐릭터
            string slotKorean = GetSlotKoreanName(_targetSlot);
            GUILayout.Label($"🪽 [{slotKorean}] 날개 선택 및 변경", _titleStyle);
            GUILayout.Space(3);
            GUILayout.Label($"대상 영웅: {_targetCharacter.CharacterName}  |  현재 공격력: {_targetCharacter.CurrentAttack}", _statLabelStyle);
            GUILayout.Space(12);

            // 2. 보유 날개 목록 (해당 슬롯 부위만 필터링)
            var availableWings = WingInventoryManager.Instance.GetWingsBySlot(_targetSlot);
            var currentEquipped = _targetCharacter.GetEquippedWing(_targetSlot);

            GUILayout.BeginHorizontal();

            // 좌측: 날개 목록 스크롤 뷰 (가로 380)
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(380), GUILayout.Height(330));
            GUILayout.Label($"【 인벤토리 내 보유 날개 ({availableWings.Count}개) 】", GUI.skin.label);
            GUILayout.Space(4);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(290));

            // 현재 장착 중인 날개 표시 (있을 경우)
            if (currentEquipped != null)
            {
                bool isSel = (_selectedWing == currentEquipped);
                Color origCol = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);

                string star = new string('★', currentEquipped.Rarity);
                string text = $"[착용 중] {star} {currentEquipped.WingName} (+{currentEquipped.BonusDamage})";
                if (GUILayout.Button(text, isSel ? _selectedItemStyle : _itemButtonStyle, GUILayout.Height(36)))
                {
                    _selectedWing = currentEquipped;
                }
                GUI.backgroundColor = origCol;
                GUILayout.Space(4);
            }

            if (availableWings.Count == 0 && currentEquipped == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("보유 중인 해당 부위의 날개가 없습니다.", _statLabelStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                foreach (var wing in availableWings)
                {
                    bool isSel = (_selectedWing == wing);
                    Color origCol = GUI.backgroundColor;
                    if (isSel) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);

                    string star = new string('★', wing.Rarity);
                    string setTag = !string.IsNullOrEmpty(wing.SetID) ? $" [{wing.SetID}]" : "";
                    string text = $"{star} {wing.WingName} (+{wing.BonusDamage}{setTag})";

                    if (GUILayout.Button(text, isSel ? _selectedItemStyle : _itemButtonStyle, GUILayout.Height(34)))
                    {
                        _selectedWing = wing;
                    }

                    GUI.backgroundColor = origCol;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // 우측: 선택된 날개 상세 정보 프리뷰 (가로 270)
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(270), GUILayout.Height(330));
            GUILayout.Label("【 선택한 날개 정보 】", GUI.skin.label);
            GUILayout.Space(10);

            if (_selectedWing != null)
            {
                string starStr = new string('★', _selectedWing.Rarity);
                Color rColor = GetRarityColor(_selectedWing.Rarity);

                GUI.contentColor = rColor;
                GUILayout.Label($"{starStr} {_selectedWing.WingName}", _titleStyle);
                GUI.contentColor = Color.white;

                GUILayout.Space(10);
                GUILayout.Label($"부위: {GetSlotKoreanName(_selectedWing.SlotType)}", _statLabelStyle);
                GUILayout.Label($"기본 공격력 가산: +{_selectedWing.BonusDamage}", _statLabelStyle);

                if (!string.IsNullOrEmpty(_selectedWing.SetID))
                {
                    var setDef = WingSetDatabase.GetSetDefinition(_selectedWing.SetID);
                    string setName = setDef != null ? setDef.SetName : _selectedWing.SetID;
                    GUILayout.Label($"소속 세트: {setName}", _statLabelStyle);
                    if (setDef != null)
                    {
                        GUILayout.Space(4);
                        GUILayout.Label($"2세트: {setDef.Description2pc}", _statLabelStyle);
                        GUILayout.Label($"4세트: {setDef.Description4pc}", _statLabelStyle);
                    }
                }
                else
                {
                    GUILayout.Label("세트 효과: 없음 (3성 일반)", _statLabelStyle);
                }

                GUILayout.FlexibleSpace();

                bool isAlreadyEquippedOnThisSlot = (currentEquipped != null && currentEquipped.ItemInstanceID == _selectedWing.ItemInstanceID);
                if (isAlreadyEquippedOnThisSlot)
                {
                    GUI.contentColor = new Color(0.4f, 1f, 0.6f);
                    GUILayout.Label("현재 캐릭터가 착용 중입니다.", _statLabelStyle);
                    GUI.contentColor = Color.white;
                }
            }
            else
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("목록에서 날개를 선택하세요.", _statLabelStyle);
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // 3. 하단 액션 버튼 바
            GUILayout.BeginHorizontal();

            // 장착 해제 버튼 (현재 장착 중인 날개가 있을 때만 활성화)
            if (currentEquipped != null)
            {
                if (GUILayout.Button("장착 해제", _actionButtonStyle, GUILayout.Width(130), GUILayout.Height(42)))
                {
                    ExecuteUnequip();
                }
            }

            GUILayout.FlexibleSpace();

            // 취소 / 닫기
            if (GUILayout.Button("취소", _cancelButtonStyle, GUILayout.Width(110), GUILayout.Height(42)))
            {
                Close();
            }

            GUILayout.Space(10);

            // 장착 / 교체 버튼
            bool canEquip = _selectedWing != null && (_selectedWing != currentEquipped);
            GUI.enabled = canEquip;
            if (GUILayout.Button(currentEquipped != null ? "날개 교체" : "장착하기", _actionButtonStyle, GUILayout.Width(150), GUILayout.Height(42)))
            {
                ExecuteEquip(_selectedWing);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            GUI.matrix = prevMatrix;
        }

        public static string GetSlotKoreanName(WingSlotType slot)
        {
            return slot switch
            {
                WingSlotType.MainLeft => "주 날개 (좌)",
                WingSlotType.MainRight => "주 날개 (우)",
                WingSlotType.SubLeft => "부 날개 (좌)",
                _ => "부 날개 (우)"
            };
        }

        public static Color GetRarityColor(int rarity)
        {
            return rarity switch
            {
                5 => new Color(1.0f, 0.85f, 0.2f), // 5성 금색
                4 => new Color(0.85f, 0.5f, 1.0f), // 4성 보라색
                _ => new Color(0.85f, 0.9f, 0.95f) // 3성 은색
            };
        }
    }
}
