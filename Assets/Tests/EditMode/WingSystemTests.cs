using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Wings;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 5] WingSystemTests.cs
    /// 날개(Wing) 장비 및 드롭 시스템 코어 로직 단위 테스트
    /// - 4개 서로 다른 슬롯에 날개가 정상 장착/해제되는지 테스트
    /// - 동일 슬롯 타입이 아닌 날개를 장착하려 할 때 예외가 발생하는지 검증
    /// - 동일 세트 날개 2개 및 4개 장착 시 세트 효과 활성화 판정이 정확히 반환되는지 검증
    /// - 주사위 눈금 1 및 20 전달 시 WingDropManager에서 인벤토리로 날개가 즉시 추가되는지 테스트
    /// </summary>
    [TestFixture]
    public class WingSystemTests
    {
        private CharacterInstance _character;
        private WingInventoryManager _wingInventory;
        private WingDropManager _dropManager;

        [SetUp]
        public void SetUp()
        {
            var baseData = CharacterBaseData.Create("HERO_WING_TEST", "천공의 검사", 5, baseAttack: 50);
            _character = new CharacterInstance(baseData, null);

            _wingInventory = new WingInventoryManager();
            WingInventoryManager.Instance = _wingInventory;

            _dropManager = new WingDropManager(_wingInventory, seed: 42);
        }

        private WingItemInstance CreateTestWing(
            string name,
            WingSlotType slot,
            int rarity,
            string setId = "",
            int bonusDamage = 20)
        {
            return new WingItemInstance($"WING_{slot}_{rarity}S", name, slot, rarity, setId, bonusDamage);
        }

        #region 1. 날개 장착 및 슬롯 유효성 검증

        [Test]
        public void WingEquip_FourDistinctSlots_EquipAndUnequipCorrectly()
        {
            int baseAtk = _character.BaseAttack;

            var wingML = CreateTestWing("청룡의 주익(좌)", WingSlotType.MainLeft, 5, "SET_BLUE_DRAGON", 50);
            var wingMR = CreateTestWing("청룡의 주익(우)", WingSlotType.MainRight, 5, "SET_BLUE_DRAGON", 50);
            var wingSL = CreateTestWing("바람의 부익(좌)", WingSlotType.SubLeft, 3, "", 15);
            var wingSR = CreateTestWing("바람의 부익(우)", WingSlotType.SubRight, 3, "", 15);

            // 4개 슬롯 순차 장착
            Assert.IsTrue(_character.EquipWing(WingSlotType.MainLeft, wingML));
            Assert.IsTrue(_character.EquipWing(WingSlotType.MainRight, wingMR));
            Assert.IsTrue(_character.EquipWing(WingSlotType.SubLeft, wingSL));
            Assert.IsTrue(_character.EquipWing(WingSlotType.SubRight, wingSR));

            // 각 슬롯 조회 확인
            Assert.AreEqual(wingML, _character.GetEquippedWing(WingSlotType.MainLeft));
            Assert.AreEqual(wingMR, _character.GetEquippedWing(WingSlotType.MainRight));
            Assert.AreEqual(wingSL, _character.GetEquippedWing(WingSlotType.SubLeft));
            Assert.AreEqual(wingSR, _character.GetEquippedWing(WingSlotType.SubRight));

            // 기본 추가 공격력: 50 + 50 + 15 + 15 = 130
            // 2세트 효과(청룡의 서약): +30
            // 총 추가 공격력: 160
            Assert.AreEqual(160, _character.TotalWingBonusDamage);
            Assert.AreEqual(baseAtk + 160, _character.CurrentAttack, "캐릭터 최종 공격력에 날개 및 세트 공격력이 합산되어야 합니다.");

            // 슬롯 해제 테스트
            var unequipped = _character.UnequipWing(WingSlotType.MainLeft);
            Assert.AreEqual(wingML, unequipped);
            Assert.IsNull(_character.GetEquippedWing(WingSlotType.MainLeft));

            // 1부위 해제 후 청룡 세트는 1부위만 남아 2세트 효과 비활성화됨
            // 남은 공격력: 50 + 15 + 15 = 80
            Assert.AreEqual(80, _character.TotalWingBonusDamage);
            Assert.AreEqual(baseAtk + 80, _character.CurrentAttack);
        }

        [Test]
        public void WingEquip_SlotMismatch_ThrowsArgumentException()
        {
            // SubRight 부위의 날개 생성
            var wingSR = CreateTestWing("부 날개 우", WingSlotType.SubRight, 4);

            // MainLeft 슬롯에 SubRight 날개 장착 시도 -> ArgumentException 발생
            Assert.Throws<ArgumentException>(() =>
            {
                _character.EquipWing(WingSlotType.MainLeft, wingSR);
            }, "슬롯 부위가 불일치하는 날개를 장착하려 할 때 예외가 발생해야 합니다.");
        }

        #endregion

        #region 2. 2세트 및 4세트 효과 판정 검증

        [Test]
        public void WingSet_TwoPieceBonus_ActivatesWhenTwoOfSameSetEquipped()
        {
            var wing1 = CreateTestWing("청룡1", WingSlotType.MainLeft, 5, "SET_BLUE_DRAGON", 50);
            var wing2 = CreateTestWing("청룡2", WingSlotType.MainRight, 5, "SET_BLUE_DRAGON", 50);
            var wing3 = CreateTestWing("백호1", WingSlotType.SubLeft, 4, "SET_WHITE_TIGER", 25);
            var wing4 = CreateTestWing("일반1", WingSlotType.SubRight, 3, "", 10);

            _character.EquipWing(WingSlotType.MainLeft, wing1);
            _character.EquipWing(WingSlotType.MainRight, wing2);
            _character.EquipWing(WingSlotType.SubLeft, wing3);
            _character.EquipWing(WingSlotType.SubRight, wing4);

            var activeSets = WingSetDatabase.EvaluateActiveSetEffects(_character.EquippedWings.Values);

            // 청룡 2부위, 백호 1부위(미발동), 일반 0부위 -> 1개 세트만 활성화
            Assert.AreEqual(1, activeSets.Count);

            var blueDragonSet = activeSets[0];
            Assert.AreEqual("SET_BLUE_DRAGON", blueDragonSet.SetID);
            Assert.AreEqual(2, blueDragonSet.EquippedCount);
            Assert.IsTrue(blueDragonSet.Has2SetBonus, "2세트 보너스는 활성화되어야 합니다.");
            Assert.IsFalse(blueDragonSet.Has4SetBonus, "4세트 보너스는 비활성화 상태여야 합니다.");
            Assert.AreEqual(30, blueDragonSet.BonusDamage, "청룡 2세트 추가 대미지는 +30이어야 합니다.");
            Assert.AreEqual(0, blueDragonSet.D20RollBonus, "4세트 미완성 시 D20 보너스는 0이어야 합니다.");
        }

        [Test]
        public void WingSet_FourPieceBonus_ActivatesFullBonusAndD20Bonus()
        {
            // 태양의 불꽃(SET_SOLAR_PHOENIX) 4부위 풀세트 장착
            var w1 = CreateTestWing("태양1", WingSlotType.MainLeft, 5, "SET_SOLAR_PHOENIX", 60);
            var w2 = CreateTestWing("태양2", WingSlotType.MainRight, 5, "SET_SOLAR_PHOENIX", 60);
            var w3 = CreateTestWing("태양3", WingSlotType.SubLeft, 5, "SET_SOLAR_PHOENIX", 60);
            var w4 = CreateTestWing("태양4", WingSlotType.SubRight, 5, "SET_SOLAR_PHOENIX", 60);

            _character.EquipWing(WingSlotType.MainLeft, w1);
            _character.EquipWing(WingSlotType.MainRight, w2);
            _character.EquipWing(WingSlotType.SubLeft, w3);
            _character.EquipWing(WingSlotType.SubRight, w4);

            var activeSets = WingSetDatabase.EvaluateActiveSetEffects(_character.EquippedWings.Values);
            Assert.AreEqual(1, activeSets.Count);

            var phoenixSet = activeSets[0];
            Assert.AreEqual("SET_SOLAR_PHOENIX", phoenixSet.SetID);
            Assert.AreEqual(4, phoenixSet.EquippedCount);
            Assert.IsTrue(phoenixSet.Has2SetBonus, "2세트 효과 활성화");
            Assert.IsTrue(phoenixSet.Has4SetBonus, "4세트 효과 완성 활성화");

            // 2세트(+40) + 4세트(+90) = 총 +130 추가 공격력
            Assert.AreEqual(130, phoenixSet.BonusDamage);
            Assert.AreEqual(3, phoenixSet.D20RollBonus, "태양 4세트는 D20 주사위 판정 +3 보정치를 부여해야 합니다.");
            Assert.IsTrue(phoenixSet.SpecialGimmickActive);
        }

        #endregion

        #region 3. 주사위 극단값 및 드롭 트리거 연동 검증

        [Test]
        public void WingDrop_DiceValue1_GuaranteesWingDropToInventory()
        {
            Assert.AreEqual(0, _wingInventory.TotalWingCount);

            // D20 눈금 1 (대실패) 발생
            var droppedWing = _dropManager.OnDiceRolled(1);

            Assert.IsNotNull(droppedWing, "눈금 1 발생 시 '불운의 위로'로 날개가 확정 드롭되어야 합니다.");
            Assert.AreEqual(1, _wingInventory.TotalWingCount, "인벤토리에 즉시 추가되어야 합니다.");
            Assert.AreEqual(droppedWing.ItemInstanceID, _wingInventory.OwnedWings[0].ItemInstanceID);
        }

        [Test]
        public void WingDrop_DiceValue20_GuaranteesHighGradeWingDropToInventory()
        {
            Assert.AreEqual(0, _wingInventory.TotalWingCount);

            // D20 눈금 20 (절대 성공/즉사) 발생
            var droppedWing = _dropManager.OnDiceRolled(20);

            Assert.IsNotNull(droppedWing, "눈금 20 발생 시 '절대 행운'으로 날개가 확정 드롭되어야 합니다.");
            Assert.AreEqual(1, _wingInventory.TotalWingCount, "인벤토리에 즉시 추가되어야 합니다.");
            Assert.IsTrue(droppedWing.Rarity >= 4, $"고등급 부스트로 4성 또는 5성 날개여야 합니다. (실제 등급: {droppedWing.Rarity}성)");
            Assert.IsFalse(string.IsNullOrEmpty(droppedWing.SetID), "4~5성 날개는 세트 ID가 반드시 부여되어야 합니다.");
        }

        [Test]
        public void WingDrop_RegularDiceValues_DoNotTriggerGuaranteedDrop()
        {
            Assert.AreEqual(0, _wingInventory.TotalWingCount);

            // 2, 10, 19 등 일반 눈금은 확정 드롭 없음
            Assert.IsNull(_dropManager.OnDiceRolled(2));
            Assert.IsNull(_dropManager.OnDiceRolled(10));
            Assert.IsNull(_dropManager.OnDiceRolled(19));

            Assert.AreEqual(0, _wingInventory.TotalWingCount);
        }

        [Test]
        public void WingDrop_BossDefeatAndLegendaryChest_GuaranteesHighGradeDrop()
        {
            Assert.AreEqual(0, _wingInventory.TotalWingCount);

            // 보스 처치 시 100% 고등급 확정 드롭
            var bossWing = _dropManager.OnEnemyDefeated(EnemyType.Boss);
            Assert.IsNotNull(bossWing);
            Assert.IsTrue(bossWing.Rarity >= 4);
            Assert.AreEqual(1, _wingInventory.TotalWingCount);

            // 전설 상자 개봉 시 100% 고등급 확정 드롭
            var chestWing = _dropManager.OnChestOpened(ChestType.Legendary);
            Assert.IsNotNull(chestWing);
            Assert.IsTrue(chestWing.Rarity >= 4);
            Assert.AreEqual(2, _wingInventory.TotalWingCount);
        }

        [Test]
        public void WingInventoryManager_FilterBySlotAndRarity_WorksAccurately()
        {
            _wingInventory.AddWing(CreateTestWing("날개1", WingSlotType.MainLeft, 5));
            _wingInventory.AddWing(CreateTestWing("날개2", WingSlotType.MainLeft, 4));
            _wingInventory.AddWing(CreateTestWing("날개3", WingSlotType.SubRight, 3));

            var mlWings = _wingInventory.GetWingsBySlot(WingSlotType.MainLeft);
            Assert.AreEqual(2, mlWings.Count);

            var fiveStarWings = _wingInventory.GetWingsByRarity(5);
            Assert.AreEqual(1, fiveStarWings.Count);
            Assert.AreEqual("날개1", fiveStarWings[0].WingName);
        }

        #endregion
    }
}
