using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Inventory;
using RPG25D.Core.Gacha;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 5] InventoryPartyTests.cs
    /// - 신규 캐릭터 획득 시 인벤토리에 정상 등록되는지 테스트
    /// - 동일 캐릭터 중복 소환 시 조각 지급 및 사주 데이터 교체/유지 로직 검증
    /// - 동일 베이스 캐릭터를 2개 이상 파티 슬롯에 중복 등록하려 할 때 차단되는지 검증
    /// - 세이브/로드 사이클 후 파티 슬롯 및 캐릭터 인스턴스의 사주팔자 데이터가 손실 없이 복원되는지 검증
    /// </summary>
    [TestFixture]
    public class InventoryPartyTests
    {
        private CharacterInventoryManager _inventory;
        private GachaDuplicateHandler _duplicateHandler;
        private PartyFormationManager _partyManager;
        private SajuDiceGenerator _diceGen;

        [SetUp]
        public void SetUp()
        {
            _inventory = new CharacterInventoryManager();
            _duplicateHandler = new GachaDuplicateHandler(_inventory);
            _partyManager = new PartyFormationManager(_inventory);
            _diceGen = new SajuDiceGenerator(12345);
        }

        private CharacterInstance CreateSampleCharacter(string baseId, string name, int rarity, int baseAttack = 30)
        {
            var baseData = CharacterBaseData.Create(baseId, name, rarity, baseAttack);
            var saju = _diceGen.RollSaju();
            return new CharacterInstance(baseData, saju);
        }

        #region 1. 인벤토리 및 캐릭터 보관소 검증

        [Test]
        public void Inventory_AddNewCharacter_RegistersSuccessfully()
        {
            var char1 = CreateSampleCharacter("CHAR_WARRIOR", "청룡의 무사", 5, 50);

            bool added = _inventory.AddCharacter(char1);

            Assert.IsTrue(added, "신규 캐릭터는 정상적으로 추가되어야 합니다.");
            Assert.AreEqual(1, _inventory.TotalCharacterCount, "인벤토리 총 캐릭터 수는 1이어야 합니다.");
            Assert.IsTrue(_inventory.HasCharacter(char1.InstanceID), "인스턴스 ID 조회가 참이어야 합니다.");
            Assert.IsTrue(_inventory.HasBaseCharacter("CHAR_WARRIOR"), "베이스 ID 조회가 참이어야 합니다.");

            var retrieved = _inventory.GetCharacter(char1.InstanceID);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(char1.InstanceID, retrieved.InstanceID);
            Assert.AreEqual("청룡의 무사", retrieved.CharacterName);
            Assert.AreEqual(char1.Saju.ToEightCharacters(), retrieved.Saju.ToEightCharacters());

            var byBase = _inventory.GetInstancesByBaseId("CHAR_WARRIOR");
            Assert.AreEqual(1, byBase.Count);
            Assert.AreEqual(char1.InstanceID, byBase[0].InstanceID);
        }

        [Test]
        public void Inventory_DuplicateInstanceID_CannotBeAddedTwice()
        {
            var char1 = CreateSampleCharacter("CHAR_WARRIOR", "청룡의 무사", 5);

            Assert.IsTrue(_inventory.AddCharacter(char1));
            Assert.IsFalse(_inventory.AddCharacter(char1), "동일한 인스턴스는 중복 등록될 수 없습니다.");
            Assert.AreEqual(1, _inventory.TotalCharacterCount);
        }

        [Test]
        public void Inventory_SortAndFilter_WorksAccurately()
        {
            var char3s = CreateSampleCharacter("CHAR_3S", "초보 도적", 3, 20);
            char3s.Level = 10;
            var char4s = CreateSampleCharacter("CHAR_4S", "신비의 술사", 4, 35);
            char4s.Level = 25;
            var char5s = CreateSampleCharacter("CHAR_5S", "태양의 현자", 5, 60);
            char5s.Level = 5;

            _inventory.AddCharacter(char3s);
            _inventory.AddCharacter(char4s);
            _inventory.AddCharacter(char5s);

            // 희귀도(등급) 내림차순 정렬
            var byRarity = _inventory.GetSortedCharacters(CharacterSortOption.Rarity, descending: true);
            Assert.AreEqual(5, byRarity[0].Rarity, "첫 번째는 5성이어야 합니다.");
            Assert.AreEqual(4, byRarity[1].Rarity, "두 번째는 4성이어야 합니다.");
            Assert.AreEqual(3, byRarity[2].Rarity, "세 번째는 3성이어야 합니다.");

            // 레벨 내림차순 정렬
            var byLevel = _inventory.GetSortedCharacters(CharacterSortOption.Level, descending: true);
            Assert.AreEqual(25, byLevel[0].Level, "가장 높은 레벨(25)이 첫 번째여야 합니다.");
            Assert.AreEqual(10, byLevel[1].Level, "두 번째 레벨(10)");
            Assert.AreEqual(5, byLevel[2].Level, "세 번째 레벨(5)");

            // 획득순 정렬 (char3s -> char4s -> char5s)
            var byAcq = _inventory.GetSortedCharacters(CharacterSortOption.Acquisition, descending: false);
            Assert.AreEqual(char3s.InstanceID, byAcq[0].InstanceID);
            Assert.AreEqual(char4s.InstanceID, byAcq[1].InstanceID);
            Assert.AreEqual(char5s.InstanceID, byAcq[2].InstanceID);

            // 필터링: 4성만 필터
            var filtered4s = _inventory.FilterByRarity(4);
            Assert.AreEqual(1, filtered4s.Count);
            Assert.AreEqual("CHAR_4S", filtered4s[0].BaseDataID);
        }

        [Test]
        public void Inventory_RemoveCharacter_UpdatesMappingsAndAcquisitionOrder()
        {
            var char1 = CreateSampleCharacter("CHAR_01", "영웅1", 4);
            var char2 = CreateSampleCharacter("CHAR_02", "영웅2", 5);

            _inventory.AddCharacter(char1);
            _inventory.AddCharacter(char2);

            bool removed = _inventory.RemoveCharacter(char1.InstanceID);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, _inventory.TotalCharacterCount);
            Assert.IsFalse(_inventory.HasCharacter(char1.InstanceID));
            Assert.IsFalse(_inventory.HasBaseCharacter("CHAR_01"));
            Assert.IsTrue(_inventory.HasCharacter(char2.InstanceID));
        }

        #endregion

        #region 2. 중복 소환 처리 및 사주 선택/초월 시스템 검증

        [Test]
        public void GachaDuplicate_FirstPull_IsNotDuplicate()
        {
            var char1 = CreateSampleCharacter("HERO_001", "청룡의 무사", 5);

            Assert.IsFalse(_duplicateHandler.IsDuplicate(char1.BaseDataID));
            var result = _duplicateHandler.HandleDuplicateRoll(char1);
            Assert.IsNull(result, "최초 획득 시 중복 처리 결과는 null이어야 합니다.");
        }

        [Test]
        public void GachaDuplicate_DuplicatePull_AwardsShardsAndReturnsOldAndNewSaju()
        {
            // 1. 최초 획득 및 인벤토리 등록
            var charOriginal = CreateSampleCharacter("HERO_001", "청룡의 무사", 5);
            _inventory.AddCharacter(charOriginal);

            // 2. 동일한 BaseDataID의 캐릭터 중복 소환 (새로운 사주)
            var charDuplicate = CreateSampleCharacter("HERO_001", "청룡의 무사", 5);

            Assert.IsTrue(_duplicateHandler.IsDuplicate("HERO_001"), "이미 인벤토리에 있으므로 중복이어야 합니다.");

            // 3. 중복 처리 실행
            var result = _duplicateHandler.HandleDuplicateRoll(charDuplicate);

            Assert.IsNotNull(result, "중복 처리 결과 객체가 반환되어야 합니다.");
            Assert.AreEqual("HERO_001", result.BaseDataID);
            Assert.AreEqual(charOriginal.InstanceID, result.TargetInstanceID);
            Assert.AreEqual(charOriginal.Saju.ToEightCharacters(), result.OldSaju.ToEightCharacters(), "기존 사주 정보가 포함되어야 합니다.");
            Assert.AreEqual(charDuplicate.Saju.ToEightCharacters(), result.NewSaju.ToEightCharacters(), "새 사주 정보가 포함되어야 합니다.");

            // 5성이므로 기본 50개 조각 지급
            Assert.AreEqual(50, result.ShardsAwarded);
            Assert.AreEqual(50, _duplicateHandler.GetShards("HERO_001"), "조각 저장소에 50개가 적립되어야 합니다.");

            // 인벤토리는 늘어나지 않음
            Assert.AreEqual(1, _inventory.TotalCharacterCount);
        }

        [Test]
        public void GachaDuplicate_SajuOverwrite_UpdatesCharacterSaju()
        {
            var charOriginal = CreateSampleCharacter("HERO_001", "청룡의 무사", 5);
            _inventory.AddCharacter(charOriginal);

            var newSaju = new CharacterSaju(
                new SajuPillar(HeavenlyStem.Gap, EarthlyBranch.Ja),
                new SajuPillar(HeavenlyStem.Eul, EarthlyBranch.Chuk),
                new SajuPillar(HeavenlyStem.Byeong, EarthlyBranch.In),
                new SajuPillar(HeavenlyStem.Jeong, EarthlyBranch.Myo)
            );

            // 플레이어가 새 사주 채택 결정
            bool updated = _duplicateHandler.UpdateCharacterSaju(charOriginal.InstanceID, newSaju);

            Assert.IsTrue(updated);
            Assert.AreEqual("갑자을축병인정묘", charOriginal.Saju.ToEightCharacters(), "캐릭터의 사주가 새 사주로 갱신되어야 합니다.");
        }

        [Test]
        public void GachaDuplicate_SajuRetain_KeepsOriginalSaju()
        {
            var charOriginal = CreateSampleCharacter("HERO_001", "청룡의 무사", 5);
            string originalSajuStr = charOriginal.Saju.ToEightCharacters();
            _inventory.AddCharacter(charOriginal);

            // 플레이어가 기존 사주 유지를 결정한 경우 UpdateCharacterSaju를 호출하지 않음
            Assert.AreEqual(originalSajuStr, charOriginal.Saju.ToEightCharacters(), "기존 사주가 유지되어야 합니다.");
        }

        [Test]
        public void GachaDuplicate_AscendCharacter_ConsumesShardsAndIncreasesTranscendence()
        {
            var hero = CreateSampleCharacter("HERO_001", "청룡의 무사", 5, baseAttack: 50);
            _inventory.AddCharacter(hero);

            Assert.AreEqual(0, hero.Transcendence);
            int initialAttack = hero.CurrentAttack; // 50 + 0 = 50

            // 0 -> 1단계 초월 요구량: 10개
            _duplicateHandler.AddShards("HERO_001", 25);
            Assert.AreEqual(25, _duplicateHandler.GetShards("HERO_001"));

            // 1단계 초월 실행
            bool ascended1 = _duplicateHandler.AscendCharacter(hero.InstanceID);

            Assert.IsTrue(ascended1, "초월이 성공해야 합니다.");
            Assert.AreEqual(1, hero.Transcendence, "초월 단계가 1로 증가해야 합니다.");
            Assert.AreEqual(15, _duplicateHandler.GetShards("HERO_001"), "조각 10개가 차감되어 15개가 남아야 합니다.");
            Assert.AreEqual(initialAttack + 15, hero.CurrentAttack, "초월 1단계 시 공격력이 15 증가해야 합니다.");

            // 1 -> 2단계 초월 요구량: (1 + 1) * 10 = 20개 (현재 15개로 부족)
            bool ascended2 = _duplicateHandler.AscendCharacter(hero.InstanceID);

            Assert.IsFalse(ascended2, "조각이 부족하므로 초월에 실패해야 합니다.");
            Assert.AreEqual(1, hero.Transcendence, "초월 단계가 유지되어야 합니다.");
            Assert.AreEqual(15, _duplicateHandler.GetShards("HERO_001"), "조각이 차감되지 않아야 합니다.");
        }

        #endregion

        #region 3. 출전 파티 편성 시스템 검증

        [Test]
        public void PartyFormation_AssignAndRemove_WorksAccurately()
        {
            var char1 = CreateSampleCharacter("HERO_01", "영웅1", 5);
            var char2 = CreateSampleCharacter("HERO_02", "영웅2", 4);
            var char3 = CreateSampleCharacter("HERO_03", "영웅3", 3);
            var char4 = CreateSampleCharacter("HERO_04", "영웅4", 5);

            _inventory.AddCharacter(char1);
            _inventory.AddCharacter(char2);
            _inventory.AddCharacter(char3);
            _inventory.AddCharacter(char4);

            _partyManager.AssignToSlot(0, char1.InstanceID);
            _partyManager.AssignToSlot(1, char2.InstanceID);
            _partyManager.AssignToSlot(2, char3.InstanceID);
            _partyManager.AssignToSlot(3, char4.InstanceID);

            Assert.AreEqual(4, _partyManager.ActiveMemberCount);
            Assert.IsTrue(_partyManager.IsPartyFull);

            var activeChars = _partyManager.GetActivePartyCharacters();
            Assert.AreEqual(4, activeChars.Count);
            Assert.AreEqual("HERO_01", activeChars[0].BaseDataID);
            Assert.AreEqual("HERO_04", activeChars[3].BaseDataID);

            var sajuList = _partyManager.GetActivePartySajuList();
            Assert.AreEqual(4, sajuList.Count);

            // 슬롯 1번 해제
            bool removed = _partyManager.RemoveFromSlot(1);
            Assert.IsTrue(removed);
            Assert.AreEqual(3, _partyManager.ActiveMemberCount);
            Assert.IsTrue(_partyManager.IsSlotEmpty(1));
            Assert.AreEqual(3, _partyManager.GetActivePartyCharacters().Count);
        }

        [Test]
        public void PartyFormation_DuplicateInstanceID_ThrowsInvalidOperationException()
        {
            var char1 = CreateSampleCharacter("HERO_01", "영웅1", 5);
            _inventory.AddCharacter(char1);

            _partyManager.AssignToSlot(0, char1.InstanceID);

            // 동일한 인스턴스를 슬롯 1에 중복 등록 시도
            Assert.Throws<InvalidOperationException>(() =>
            {
                _partyManager.AssignToSlot(1, char1.InstanceID);
            }, "동일한 인스턴스 ID는 파티 내 중복 등록될 수 없습니다.");
        }

        [Test]
        public void PartyFormation_DuplicateBaseDataID_ThrowsInvalidOperationException()
        {
            // 동일한 베이스 데이터 ID를 가진 서로 다른 두 인스턴스
            var char1 = CreateSampleCharacter("HERO_SAME_BASE", "청룡의 무사 Ver.1", 5);
            var char2 = CreateSampleCharacter("HERO_SAME_BASE", "청룡의 무사 Ver.2", 5);

            _inventory.AddCharacter(char1);
            _inventory.AddCharacter(char2);

            _partyManager.AssignToSlot(0, char1.InstanceID);

            // 동일한 BaseDataID를 가진 다른 인스턴스를 슬롯 2에 등록 시도
            Assert.Throws<InvalidOperationException>(() =>
            {
                _partyManager.AssignToSlot(2, char2.InstanceID);
            }, "동일한 BaseDataID를 가진 캐릭터는 파티 내 중복 출전할 수 없습니다.");
        }

        #endregion

        #region 4. 데이터 직렬화 및 영속성 동기화 검증

        [Test]
        public void Persistence_SaveAndLoadCycle_RestoresAllDataAndSajuWithoutLoss()
        {
            // 1. 초기 데이터 구성
            var char1 = CreateSampleCharacter("HERO_01", "전사", 5, 55);
            char1.Level = 12;
            char1.Transcendence = 2;

            var char2 = CreateSampleCharacter("HERO_02", "마법사", 4, 40);
            char2.Level = 8;

            var char3 = CreateSampleCharacter("HERO_03", "사제", 3, 25);

            _inventory.AddCharacter(char1);
            _inventory.AddCharacter(char2);
            _inventory.AddCharacter(char3);

            _duplicateHandler.AddShards("HERO_01", 45);
            _duplicateHandler.AddShards("HERO_02", 20);

            _partyManager.AssignToSlot(0, char1.InstanceID);
            _partyManager.AssignToSlot(1, char2.InstanceID);

            // 2. 직렬화 (JSON)
            string json = UserDataPersistence.ToJson(_inventory, _duplicateHandler, _partyManager, prettyPrint: true);
            Assert.IsFalse(string.IsNullOrEmpty(json), "JSON 문자열이 생성되어야 합니다.");

            // 3. 신규 매니저 인스턴스 생성 및 역직렬화 복원
            var freshInventory = new CharacterInventoryManager();
            var freshDuplicateHandler = new GachaDuplicateHandler(freshInventory);
            var freshPartyManager = new PartyFormationManager(freshInventory);

            var loadedData = UserDataPersistence.FromJson(json);
            Assert.IsNotNull(loadedData);

            UserDataPersistence.ApplySaveData(loadedData, freshInventory, freshDuplicateHandler, freshPartyManager);

            // 4. 무결성 검증
            Assert.AreEqual(3, freshInventory.TotalCharacterCount, "복원된 캐릭터 수가 일치해야 합니다.");

            var restoredHero1 = freshInventory.GetCharacter(char1.InstanceID);
            Assert.IsNotNull(restoredHero1);
            Assert.AreEqual("전사", restoredHero1.CharacterName);
            Assert.AreEqual(12, restoredHero1.Level);
            Assert.AreEqual(2, restoredHero1.Transcendence);
            Assert.AreEqual(char1.CurrentAttack, restoredHero1.CurrentAttack);

            // 사주팔자 8글자 일치 확인
            Assert.AreEqual(char1.Saju.ToEightCharacters(), restoredHero1.Saju.ToEightCharacters(), "사주팔자 명식이 100% 일치해야 합니다.");
            Assert.AreEqual(char1.Saju.YearPillar, restoredHero1.Saju.YearPillar);
            Assert.AreEqual(char1.Saju.MonthPillar, restoredHero1.Saju.MonthPillar);
            Assert.AreEqual(char1.Saju.DayPillar, restoredHero1.Saju.DayPillar);
            Assert.AreEqual(char1.Saju.HourPillar, restoredHero1.Saju.HourPillar);

            // 샤드 보유량 검증
            Assert.AreEqual(45, freshDuplicateHandler.GetShards("HERO_01"));
            Assert.AreEqual(20, freshDuplicateHandler.GetShards("HERO_02"));

            // 파티 편성 검증
            Assert.AreEqual(2, freshPartyManager.ActiveMemberCount);
            Assert.AreEqual(char1.InstanceID, freshPartyManager.ActivePartySlotInstanceIDs[0]);
            Assert.AreEqual(char2.InstanceID, freshPartyManager.ActivePartySlotInstanceIDs[1]);
            Assert.IsTrue(freshPartyManager.IsSlotEmpty(2));
            Assert.IsTrue(freshPartyManager.IsSlotEmpty(3));
        }

        [Test]
        public void Persistence_GameManagerData_BridgeSyncsStateAccurately()
        {
            var char1 = CreateSampleCharacter("HERO_01", "전사", 5);
            _inventory.AddCharacter(char1);
            _partyManager.AssignToSlot(0, char1.InstanceID);
            _duplicateHandler.AddShards("HERO_01", 30);

            var gmData = ScriptableObject.CreateInstance<GameManagerData>();

            // GameManagerData로 동기화
            UserDataPersistence.SyncToGameManager(gmData, _inventory, _duplicateHandler, _partyManager);
            Assert.IsFalse(string.IsNullOrEmpty(gmData.SerializedUserData), "GameManagerData에 직렬화 문자열이 기록되어야 합니다.");

            // 새 매니저로 복원
            var freshInv = new CharacterInventoryManager();
            var freshDup = new GachaDuplicateHandler(freshInv);
            var freshParty = new PartyFormationManager(freshInv);

            bool synced = UserDataPersistence.SyncFromGameManager(gmData, freshInv, freshDup, freshParty);
            Assert.IsTrue(synced);
            Assert.AreEqual(1, freshInv.TotalCharacterCount);
            Assert.AreEqual(30, freshDup.GetShards("HERO_01"));
            Assert.AreEqual(char1.InstanceID, freshParty.ActivePartySlotInstanceIDs[0]);
        }

        #endregion
    }
}
