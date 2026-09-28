using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RPG25D.Core.Inventory;
using RPG25D.Core.Gacha;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.Visual;

namespace RPG25D.UI
{
    /// <summary>
    /// [요구 산출물 2] PartyFormationUI.cs
    /// 파티 편성 및 캐릭터 인벤토리 보관소 UI 컨트롤러
    /// - 상단 (출전 파티 4개 슬롯): 캐릭터 카드, 이름, 일주(DayPillar, 본원) 한자, 클릭 시 편성 해제
    /// - 하단 (보유 캐릭터 인벤토리): 등급순/레벨순/공격력순/획득순 정렬 필터, 카드 클릭 시 파티 슬롯 배정
    /// - 중복 출전 방지: 동일 BaseDataID 중복 등록 시도 시 "이미 파티에 편성된 캐릭터입니다" 토스트 출력 및 차단
    /// - 캐릭터 상세 정보 모달(CharacterDetailModal) 연동
    /// - 씬 전환 및 탐색/전투 연동: [전투 준비 완료] 클릭 시 UserDataPersistence 및 GameManagerData 즉시 동기화
    /// </summary>
    public class PartyFormationUI : MonoBehaviour
    {
        [Header("연결 모달 및 디렉터")]
        [SerializeField] private CharacterDetailModal _detailModal;
        [SerializeField] private GachaVisualDirector _gachaDirector;

        [Header("UI 활성화 상태")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        private CharacterInventoryManager _inventory;
        private PartyFormationManager _partyManager;

        public CharacterInventoryManager Inventory
        {
            get => _inventory ??= CharacterInventoryManager.Instance;
            set => _inventory = value;
        }

        public PartyFormationManager PartyManager
        {
            get => _partyManager ??= PartyFormationManager.Instance;
            set => _partyManager = value;
        }

        // 정렬 및 스크롤 상태
        private CharacterSortOption _currentSort = CharacterSortOption.Rarity;
        private Vector2 _inventoryScrollPosition = Vector2.zero;

        // 토스트 알림 메시지 및 타이머
        private string _toastMessage = string.Empty;
        private float _toastTimer = 0f;
        private bool _toastIsError = false;

        // 선택된 타겟 슬롯 (기본: -1이면 빈 슬롯 자동 탐색)
        private int _selectedSlotIndex = -1;

        // IMGUI 스타일
        private GUIStyle _headerTitleStyle;
        private GUIStyle _slotBoxFilledStyle;
        private GUIStyle _slotBoxEmptyStyle;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _cardEquippedStyle;
        private GUIStyle _toastBoxStyle;
        private GUIStyle _tabButtonStyle;
        private GUIStyle _tabButtonSelectedStyle;
        private GUIStyle _actionButtonStyle;

        private void Awake()
        {
            _ = Inventory;
            _ = PartyManager;

            if (_detailModal == null) _detailModal = FindAnyObjectByType<CharacterDetailModal>();
            if (_gachaDirector == null) _gachaDirector = FindAnyObjectByType<GachaVisualDirector>();

            // 저장된 유저 데이터가 있다면 초기 로드
            LoadPartyData();
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f)
                {
                    _toastMessage = string.Empty;
                }
            }
        }

        public void OpenUI()
        {
            _isOpen = true;
            gameObject.SetActive(true);
            RefreshData();
        }

        public void CloseUI()
        {
            _isOpen = false;
            SavePartyData();
        }

        public void ToggleUI()
        {
            if (_isOpen) CloseUI();
            else OpenUI();
        }

        /// <summary>
        /// 토스트 알림 메시지 출력
        /// </summary>
        public void ShowToast(string message, bool isError = false, float duration = 2.5f)
        {
            _toastMessage = message;
            _toastIsError = isError;
            _toastTimer = duration;
            Debug.Log($"[PartyFormationUI] 📢 토스트: {message}");
        }

        /// <summary>
        /// 파티 데이터 로컬 및 GameManager 저장
        /// </summary>
        public void SavePartyData()
        {
            UserDataPersistence.SaveToPlayerPrefs(Inventory, GachaDuplicateHandler.Instance, PartyManager);
            if (GameManagerData.Instance != null)
            {
                UserDataPersistence.SyncToGameManager(GameManagerData.Instance, Inventory, GachaDuplicateHandler.Instance, PartyManager);
            }
            ShowToast("💾 파티 구성 및 인벤토리 데이터가 저장되었습니다!", false);
        }

        /// <summary>
        /// 파티 데이터 로드
        /// </summary>
        public void LoadPartyData()
        {
            if (GameManagerData.Instance != null && !string.IsNullOrEmpty(GameManagerData.Instance.SerializedUserData))
            {
                UserDataPersistence.SyncFromGameManager(GameManagerData.Instance, Inventory, GachaDuplicateHandler.Instance, PartyManager);
            }
            else
            {
                UserDataPersistence.LoadFromPlayerPrefs(Inventory, GachaDuplicateHandler.Instance, PartyManager);
            }
        }

        public void RefreshData()
        {
            // 인벤토리가 비어있고 가챠 풀에 기본 캐릭터가 있다면 데모용 3인 초기 지급
            if (Inventory.TotalCharacterCount == 0)
            {
                EnsureStarterCharacters();
            }
        }

        private void EnsureStarterCharacters()
        {
            var diceGen = new SajuDiceGenerator(999);
            var hero1 = new CharacterInstance(CharacterBaseData.Create("HERO_STARTER_01", "청룡의 검객", 5, 50), diceGen.RollSaju());
            var hero2 = new CharacterInstance(CharacterBaseData.Create("HERO_STARTER_02", "태양의 도사", 4, 38), diceGen.RollSaju());
            var hero3 = new CharacterInstance(CharacterBaseData.Create("HERO_STARTER_03", "바람의 궁수", 3, 25), diceGen.RollSaju());

            Inventory.AddCharacter(hero1);
            Inventory.AddCharacter(hero2);
            Inventory.AddCharacter(hero3);

            PartyManager.AssignToSlot(0, hero1.InstanceID);
            PartyManager.AssignToSlot(1, hero2.InstanceID);
            PartyManager.AssignToSlot(2, hero3.InstanceID);

            SavePartyData();
        }

        /// <summary>
        /// 캐릭터 카드를 클릭했을 때 파티 슬롯 배정 시도
        /// </summary>
        public void HandleCharacterCardClick(CharacterInstance character)
        {
            if (character == null) return;

            // 이미 편성된 캐릭터인지 확인
            int currentSlot = -1;
            for (int i = 0; i < PartyFormationManager.PartySlotCount; i++)
            {
                if (PartyManager.ActivePartySlotInstanceIDs[i] == character.InstanceID)
                {
                    currentSlot = i;
                    break;
                }
            }

            if (currentSlot >= 0)
            {
                // 이미 편성되어 있으면 슬롯 해제
                PartyManager.RemoveFromSlot(currentSlot);
                ShowToast($"[{character.CharacterName}] 출전 슬롯 {currentSlot + 1}번에서 해제되었습니다.");
                SavePartyData();
                return;
            }

            // 배정할 대상 슬롯 결정
            int targetSlot = _selectedSlotIndex;
            if (targetSlot < 0 || targetSlot >= PartyFormationManager.PartySlotCount)
            {
                // 빈 슬롯 우선 탐색
                for (int i = 0; i < PartyFormationManager.PartySlotCount; i++)
                {
                    if (PartyManager.IsSlotEmpty(i))
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot < 0)
            {
                targetSlot = 0; // 모두 차있으면 1번 슬롯 교체
            }

            // 중복 출전 검증 및 배정 실행
            if (PartyManager.TryAssignToSlot(targetSlot, character.InstanceID, out string errorMsg))
            {
                ShowToast($"⚔️ [{character.CharacterName}] 슬롯 {targetSlot + 1}번에 편성 완료!");
                _selectedSlotIndex = -1;
                SavePartyData();
            }
            else
            {
                ShowToast($"⚠️ 이미 파티에 편성된 캐릭터입니다! ({character.CharacterName})", true);
            }
        }

        private void InitStyles()
        {
            if (_headerTitleStyle != null) return;

            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerTitleStyle.normal.textColor = new Color(1f, 0.95f, 0.35f);

            _slotBoxFilledStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            _slotBoxEmptyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _cardBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            _cardEquippedStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            _toastBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _tabButtonSelectedStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _tabButtonSelectedStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);

            _actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (!_isOpen || Application.isBatchMode) return;

            InitStyles();

            // 1280x720 가상 좌표계 변환
            float scaleX = Screen.width / 1280f;
            float scaleY = Screen.height / 720f;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            // 배경 박스 (전체 화면)
            GUI.Box(new Rect(30, 20, 1220, 680), GUIContent.none);

            GUILayout.BeginArea(new Rect(50, 35, 1180, 650));

            // 1. 최상단 헤더 및 내비게이션 버튼
            DrawHeaderNavigation();

            GUILayout.Space(15);

            // 2. 상단: 출전 파티 4개 슬롯
            DrawPartySlotsSection();

            GUILayout.Space(20);

            // 3. 중앙: 정렬 탭 버튼 바
            DrawSortTabBar();

            GUILayout.Space(10);

            // 4. 하단: 인벤토리 캐릭터 카드 그리드
            DrawInventoryGridSection();

            GUILayout.EndArea();

            // 5. 토스트 팝업 오버레이
            DrawToastOverlay();

            GUI.matrix = prevMatrix;
        }

        private void DrawHeaderNavigation()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("🛡️ 원정대 파티 편성 및 영웅 보관소", _headerTitleStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔮 사주 가챠 소환", _actionButtonStyle, GUILayout.Width(130), GUILayout.Height(38)))
            {
                if (_gachaDirector != null)
                {
                    _gachaDirector.RollSingle();
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("💾 파티 저장", _actionButtonStyle, GUILayout.Width(110), GUILayout.Height(38)))
            {
                SavePartyData();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("⚔️ 모험 탐색 출발", _actionButtonStyle, GUILayout.Width(140), GUILayout.Height(38)))
            {
                SavePartyData();
                SceneManager.LoadScene("Level01_Exploration");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("✕ 닫기", _actionButtonStyle, GUILayout.Width(80), GUILayout.Height(38)))
            {
                CloseUI();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPartySlotsSection()
        {
            GUILayout.Label("【 출전 파티 편성 (최대 4인) 】", GUI.skin.label);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            for (int i = 0; i < PartyFormationManager.PartySlotCount; i++)
            {
                var charInst = PartyManager.GetCharacterInSlot(i);
                bool isSelected = (_selectedSlotIndex == i);

                Color origColor = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);

                GUILayout.BeginVertical(charInst != null ? _slotBoxFilledStyle : _slotBoxEmptyStyle, GUILayout.Width(280), GUILayout.Height(150));
                GUI.backgroundColor = origColor;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"슬롯 {i + 1}", GUI.skin.label);
                GUILayout.FlexibleSpace();
                if (isSelected) GUILayout.Label("【선택됨】", GUI.skin.label);
                GUILayout.EndHorizontal();

                if (charInst != null)
                {
                    // 편성된 캐릭터 정보
                    string stars = new string('★', charInst.Rarity);
                    Color gradeCol = charInst.Rarity == 5 ? new Color(1f, 0.85f, 0.2f) : Color.white;
                    GUI.contentColor = gradeCol;
                    GUILayout.Label($"{stars} {charInst.CharacterName}", GUI.skin.label);
                    GUI.contentColor = Color.white;

                    GUILayout.Label($"Lv.{charInst.Level} (초월 {charInst.Transcendence}단) | 공격력 {charInst.CurrentAttack}", GUI.skin.label);

                    // 일주(본원) 한자 표기
                    if (charInst.Saju != null)
                    {
                        var day = charInst.Saju.DayPillar;
                        Color elemColor = GachaVisualDirector.GetFiveElementColor(day.Stem);
                        GUI.contentColor = elemColor;
                        GUILayout.Label($"일주(본원): {day.ToHanjaString()} ({day.Stem.ToKorean()} {day.Branch.ToKorean()})", GUI.skin.label);
                        GUI.contentColor = Color.white;
                    }

                    GUILayout.FlexibleSpace();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("상세보기", GUILayout.Height(28)))
                    {
                        if (_detailModal != null) _detailModal.Open(charInst);
                    }
                    if (GUILayout.Button("편성 해제", GUILayout.Height(28)))
                    {
                        PartyManager.RemoveFromSlot(i);
                        SavePartyData();
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    // 빈 슬롯
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("+ 빈 슬롯", GUI.skin.label);
                    GUILayout.Label("아래 영웅 목록에서 선택하여 배정", GUI.skin.label);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(isSelected ? "선택 해제" : "이 슬롯 선택", GUILayout.Height(28)))
                    {
                        _selectedSlotIndex = isSelected ? -1 : i;
                    }
                }

                GUILayout.EndVertical();

                if (i < PartyFormationManager.PartySlotCount - 1) GUILayout.Space(12);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSortTabBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"【 보유 영웅 보관소 (총 {Inventory.TotalCharacterCount}명) 】", GUI.skin.label);
            GUILayout.FlexibleSpace();

            GUILayout.Label("정렬 기준: ", GUI.skin.label);

            if (GUILayout.Button("등급순", _currentSort == CharacterSortOption.Rarity ? _tabButtonSelectedStyle : _tabButtonStyle, GUILayout.Width(75), GUILayout.Height(28)))
                _currentSort = CharacterSortOption.Rarity;

            if (GUILayout.Button("레벨순", _currentSort == CharacterSortOption.Level ? _tabButtonSelectedStyle : _tabButtonStyle, GUILayout.Width(75), GUILayout.Height(28)))
                _currentSort = CharacterSortOption.Level;

            if (GUILayout.Button("공격력순", _currentSort == CharacterSortOption.Attack ? _tabButtonSelectedStyle : _tabButtonStyle, GUILayout.Width(75), GUILayout.Height(28)))
                _currentSort = CharacterSortOption.Attack;

            if (GUILayout.Button("획득순", _currentSort == CharacterSortOption.Acquisition ? _tabButtonSelectedStyle : _tabButtonStyle, GUILayout.Width(75), GUILayout.Height(28)))
                _currentSort = CharacterSortOption.Acquisition;

            GUILayout.EndHorizontal();
        }

        private void DrawInventoryGridSection()
        {
            var characters = Inventory.GetSortedCharacters(_currentSort, descending: true);

            _inventoryScrollPosition = GUILayout.BeginScrollView(_inventoryScrollPosition, GUI.skin.box, GUILayout.Height(260));

            int columns = 4;
            int total = characters.Count;

            for (int r = 0; r < total; r += columns)
            {
                GUILayout.BeginHorizontal();

                for (int c = 0; c < columns; c++)
                {
                    int index = r + c;
                    if (index < total)
                    {
                        var ch = characters[index];
                        DrawCharacterCard(ch);
                    }
                    else
                    {
                        GUILayout.Space(280);
                    }

                    if (c < columns - 1) GUILayout.Space(12);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            GUILayout.EndScrollView();
        }

        private void DrawCharacterCard(CharacterInstance ch)
        {
            // 현재 파티에 편성되어 있는지 확인
            bool isEquipped = PartyManager.ActivePartySlotInstanceIDs.Contains(ch.InstanceID);

            Color origBg = GUI.backgroundColor;
            if (isEquipped) GUI.backgroundColor = new Color(0.25f, 0.45f, 0.7f);

            GUILayout.BeginVertical(isEquipped ? _cardEquippedStyle : _cardBoxStyle, GUILayout.Width(280), GUILayout.Height(115));
            GUI.backgroundColor = origBg;

            GUILayout.BeginHorizontal();
            string stars = new string('★', ch.Rarity);
            Color gradeColor = ch.Rarity == 5 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GUI.contentColor = gradeColor;
            GUILayout.Label($"{stars} {ch.CharacterName}", GUI.skin.label);
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();
            if (isEquipped)
            {
                GUI.contentColor = new Color(0.3f, 1f, 0.6f);
                GUILayout.Label("[출전 중]", GUI.skin.label);
                GUI.contentColor = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Lv.{ch.Level} (초월 {ch.Transcendence}단) | 공격력: {ch.CurrentAttack}", GUI.skin.label);

            if (ch.Saju != null)
            {
                var day = ch.Saju.DayPillar;
                Color elemColor = GachaVisualDirector.GetFiveElementColor(day.Stem);
                GUI.contentColor = elemColor;
                GUILayout.Label($"일주(본원): {day.ToHanjaString()} | 사주: {ch.Saju.ToEightCharacters()}", GUI.skin.label);
                GUI.contentColor = Color.white;
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isEquipped ? "편성 해제" : "출전 배치", GUILayout.Height(26)))
            {
                HandleCharacterCardClick(ch);
            }

            if (GUILayout.Button("상세 (사주/스탯)", GUILayout.Height(26)))
            {
                if (_detailModal != null) _detailModal.Open(ch);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawToastOverlay()
        {
            if (string.IsNullOrEmpty(_toastMessage) || _toastTimer <= 0f) return;

            float alpha = Mathf.Clamp01(_toastTimer);
            _toastBoxStyle.normal.textColor = _toastIsError ? new Color(1f, 0.35f, 0.35f, alpha) : new Color(0.3f, 1f, 0.5f, alpha);

            Rect toastRect = new Rect(390, 610, 500, 50);
            GUI.Box(toastRect, _toastMessage, _toastBoxStyle);
        }
    }
}
