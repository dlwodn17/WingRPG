using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Gacha;
using RPG25D.Core.Inventory;
using RPG25D.Core.Saju;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.Tests
{
    /// <summary>
    /// 가챠 비주얼 연출 및 파티/인벤토리 UI 상호작용 단위 테스트
    /// </summary>
    [TestFixture]
    public class GachaVisualPartyTests
    {
        private GameObject _holderGo;
        private GachaVisualDirector _director;
        private GachaResultUI _resultUI;
        private PartyFormationUI _partyUI;
        private CharacterDetailModal _detailModal;
        private CharacterInventoryManager _inventory;
        private PartyFormationManager _partyManager;

        [SetUp]
        public void SetUp()
        {
            _inventory = new CharacterInventoryManager();
            CharacterInventoryManager.Instance = _inventory;
            _partyManager = new PartyFormationManager(_inventory);
            PartyFormationManager.Instance = _partyManager;
            GachaDuplicateHandler.Instance = new GachaDuplicateHandler(_inventory);

            _holderGo = new GameObject("[Test_GachaVisualParty_Holder]");
            _director = _holderGo.AddComponent<GachaVisualDirector>();
            _resultUI = _holderGo.AddComponent<GachaResultUI>();
            _partyUI = _holderGo.AddComponent<PartyFormationUI>();
            _partyUI.Inventory = _inventory;
            _partyUI.PartyManager = _partyManager;
            _detailModal = _holderGo.AddComponent<CharacterDetailModal>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_holderGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_holderGo);
            }
        }

        private CharacterInstance CreateSampleCharacter(string baseId, string name, int rarity, HeavenlyStem stem = HeavenlyStem.Byeong, EarthlyBranch branch = EarthlyBranch.In)
        {
            var baseData = CharacterBaseData.Create(baseId, name, rarity, 40);
            var saju = new CharacterSaju(
                new SajuPillar(HeavenlyStem.Gap, EarthlyBranch.Ja),
                new SajuPillar(HeavenlyStem.Eul, EarthlyBranch.Chuk),
                new SajuPillar(stem, branch), // 일주(본원)
                new SajuPillar(HeavenlyStem.Jeong, EarthlyBranch.Myo)
            );
            return new CharacterInstance(baseData, saju);
        }

        [Test]
        public void GachaVisualDirector_FiveElementColorMapping_MatchesSajuPhilosophy()
        {
            // 목(木) - 양목/음목
            Color wood1 = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Gap);
            Color wood2 = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Eul);
            Assert.AreEqual(wood1, wood2);
            Assert.IsTrue(wood1.g > wood1.r && wood1.g > wood1.b, "목(木)은 청/녹 계열이어야 합니다.");

            // 화(火) - 양화/음화
            Color fire1 = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Byeong);
            Color fire2 = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Jeong);
            Assert.AreEqual(fire1, fire2);
            Assert.IsTrue(fire1.r > fire1.g && fire1.r > fire1.b, "화(火)는 적색 계열이어야 합니다.");

            // 토(土) - 양토/음토
            Color earth = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Mu);
            Assert.IsTrue(earth.r > 0.8f && earth.g > 0.8f, "토(土)는 황색 계열이어야 합니다.");

            // 금(金) - 양금/음금
            Color metal = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Gyeong);
            Assert.IsTrue(metal.r > 0.85f && metal.g > 0.85f && metal.b > 0.85f, "금(金)은 백/은색 계열이어야 합니다.");

            // 수(水) - 양수/음수
            Color water = GachaVisualDirector.GetFiveElementColor(HeavenlyStem.Im);
            Assert.IsTrue(water.b > water.r && water.b > water.g, "수(水)는 흑/남색 계열이어야 합니다.");
        }

        [Test]
        public void CharacterDetailModal_OpenAndClose_TogglesStateCorrectly()
        {
            var character = CreateSampleCharacter("CHAR_TEST", "불꽃의 술사", 5, HeavenlyStem.Byeong, EarthlyBranch.O);

            Assert.IsFalse(_detailModal.IsOpen);

            _detailModal.Open(character);
            Assert.IsTrue(_detailModal.IsOpen);
            Assert.AreEqual(character.InstanceID, _detailModal.CurrentCharacter.InstanceID);
            Assert.AreEqual("불꽃의 술사", _detailModal.CurrentCharacter.CharacterName);

            _detailModal.Close();
            Assert.IsFalse(_detailModal.IsOpen);
        }

        [Test]
        public void GachaResultUI_ShowResult_OpensAndBindsCharacter()
        {
            var character = CreateSampleCharacter("CHAR_HERO", "태양의 검객", 5);

            Assert.IsFalse(_resultUI.IsOpen);

            _resultUI.ShowResult(character, null);
            Assert.IsTrue(_resultUI.IsOpen);

            _resultUI.Close();
            Assert.IsFalse(_resultUI.IsOpen);
        }

        [Test]
        public void PartyFormationUI_AssignCardClick_EquipsOrUnequips()
        {
            var char1 = CreateSampleCharacter("HERO_01", "전사", 5);
            _inventory.AddCharacter(char1);

            _partyUI.OpenUI();
            Assert.IsTrue(_partyUI.IsOpen);

            // 첫 클릭 -> 슬롯 0번에 장착
            _partyUI.HandleCharacterCardClick(char1);
            Assert.AreEqual(char1.InstanceID, _partyManager.ActivePartySlotInstanceIDs[0]);
            Assert.AreEqual(1, _partyManager.ActiveMemberCount);

            // 다시 클릭 -> 장착 해제
            _partyUI.HandleCharacterCardClick(char1);
            Assert.IsNull(_partyManager.ActivePartySlotInstanceIDs[0]);
            Assert.AreEqual(0, _partyManager.ActiveMemberCount);
        }

        [Test]
        public void PartyFormationUI_AssignDuplicateBaseCharacter_ShowsToastAndBlocks()
        {
            var charA = CreateSampleCharacter("HERO_BASE_SAME", "청룡 Ver.1", 5);
            var charB = CreateSampleCharacter("HERO_BASE_SAME", "청룡 Ver.2", 5);

            _inventory.AddCharacter(charA);
            _inventory.AddCharacter(charB);

            // charA 슬롯 0번에 장착
            _partyUI.HandleCharacterCardClick(charA);
            Assert.AreEqual(charA.InstanceID, _partyManager.ActivePartySlotInstanceIDs[0]);

            // 동일한 BaseDataID를 가진 charB 장착 시도 -> 차단되고 슬롯에 등록되지 않음
            _partyUI.HandleCharacterCardClick(charB);
            Assert.AreEqual(1, _partyManager.ActiveMemberCount, "동일 베이스 캐릭터는 파티에 중복 등록될 수 없습니다.");
            Assert.IsNull(_partyManager.ActivePartySlotInstanceIDs[1]);
        }

        [Test]
        public void PartyFormationUI_SaveAndLoadPartyData_IntegratesWithPersistence()
        {
            var char1 = CreateSampleCharacter("HERO_P1", "영웅1", 4);
            var char2 = CreateSampleCharacter("HERO_P2", "영웅2", 5);

            _inventory.AddCharacter(char1);
            _inventory.AddCharacter(char2);

            _partyUI.HandleCharacterCardClick(char1);
            _partyUI.HandleCharacterCardClick(char2);

            _partyUI.SavePartyData();

            // 새 인스턴스로 로드 검증
            var newInv = new CharacterInventoryManager();
            var newParty = new PartyFormationManager(newInv);
            CharacterInventoryManager.Instance = newInv;
            PartyFormationManager.Instance = newParty;

            UserDataPersistence.LoadFromPlayerPrefs(newInv, GachaDuplicateHandler.Instance, newParty);

            Assert.AreEqual(2, newParty.ActiveMemberCount);
            Assert.AreEqual(char1.InstanceID, newParty.ActivePartySlotInstanceIDs[0]);
            Assert.AreEqual(char2.InstanceID, newParty.ActivePartySlotInstanceIDs[1]);
        }
    }
}
