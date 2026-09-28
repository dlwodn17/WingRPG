using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Wings;
using RPG25D.Data;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.Tests
{
    /// <summary>
    /// [단위 테스트] 2.5D 캐릭터 등 뒤 날개 렌더러 및 날개 장착/인벤토리 UI 검증 테스트 스위트
    /// </summary>
    public class WingVisualAndUITests
    {
        private GameObject _testRoot;
        private CharacterWingVisual _wingVisual;
        private CharacterInstance _sampleCharacter;

        [SetUp]
        public void SetUp()
        {
            _testRoot = new GameObject("[Test_Character_Visual]");
            _wingVisual = _testRoot.AddComponent<CharacterWingVisual>();
            _wingVisual.EnsureWingHierarchy();

            _sampleCharacter = new CharacterInstance(
                CharacterBaseData.Create("HERO_WING_TESTER", "수호기사", 5, 50),
                null);

            WingInventoryManager.Instance.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_testRoot);
            }
            WingInventoryManager.Instance.Clear();
        }

        [Test]
        public void CharacterWingVisual_EnsureWingHierarchy_Creates4RenderersAndAnchorWithCorrectSorting()
        {
            // 1. 앵커 및 4개 슬롯 스프라이트 렌더러 생성 검증
            Assert.IsNotNull(_wingVisual.WingAnchor, "WingAnchor 트랜스폼이 생성되어야 합니다.");
            Assert.IsNotNull(_wingVisual.MainWingLeftRenderer, "MainWingLeftRenderer가 생성되어야 합니다.");
            Assert.IsNotNull(_wingVisual.MainWingRightRenderer, "MainWingRightRenderer가 생성되어야 합니다.");
            Assert.IsNotNull(_wingVisual.SubWingLeftRenderer, "SubWingLeftRenderer가 생성되어야 합니다.");
            Assert.IsNotNull(_wingVisual.SubWingRightRenderer, "SubWingRightRenderer가 생성되어야 합니다.");

            // 2. 소팅 오더 검증 (부 날개: -2, 주 날개: -1 로 캐릭터 본체 뒤 정렬)
            Assert.AreEqual(-1, _wingVisual.MainWingLeftRenderer.sortingOrder);
            Assert.AreEqual(-1, _wingVisual.MainWingRightRenderer.sortingOrder);
            Assert.AreEqual(-2, _wingVisual.SubWingLeftRenderer.sortingOrder);
            Assert.AreEqual(-2, _wingVisual.SubWingRightRenderer.sortingOrder);

            // 좌측 날개는 flipX 적용 검증
            Assert.IsTrue(_wingVisual.MainWingLeftRenderer.flipX);
            Assert.IsTrue(_wingVisual.SubWingLeftRenderer.flipX);
            Assert.IsFalse(_wingVisual.MainWingRightRenderer.flipX);
            Assert.IsFalse(_wingVisual.SubWingRightRenderer.flipX);
        }

        [Test]
        public void CharacterWingVisual_UpdateVisual_EnablesEquippedRenderersAndDisablesUnequipped()
        {
            // 1. 주 날개(좌)와 부 날개(우) 2부위만 장착
            var mainLeft = new WingItemInstance("W_5S_ML", "청룡의 천공익[좌]", WingSlotType.MainLeft, 5, "SET_BLUE_DRAGON", 60);
            var subRight = new WingItemInstance("W_4S_SR", "백호의 서리날개[우]", WingSlotType.SubRight, 4, "SET_WHITE_TIGER", 30);

            _sampleCharacter.EquipWing(WingSlotType.MainLeft, mainLeft);
            _sampleCharacter.EquipWing(WingSlotType.SubRight, subRight);

            // 2. 비주얼 갱신 트리거
            _wingVisual.UpdateVisual(_sampleCharacter);

            // 3. 상태 검증
            Assert.IsTrue(_wingVisual.MainWingLeftRenderer.gameObject.activeSelf, "장착된 주 날개(좌)는 활성화되어야 합니다.");
            Assert.IsTrue(_wingVisual.SubWingRightRenderer.gameObject.activeSelf, "장착된 부 날개(우)는 활성화되어야 합니다.");
            Assert.IsFalse(_wingVisual.MainWingRightRenderer.gameObject.activeSelf, "미장착된 주 날개(우)는 비활성화되어야 합니다.");
            Assert.IsFalse(_wingVisual.SubWingLeftRenderer.gameObject.activeSelf, "미장착된 부 날개(좌)는 비활성화되어야 합니다.");

            // 스프라이트 할당 검증
            Assert.IsNotNull(_wingVisual.MainWingLeftRenderer.sprite, "장착된 날개는 스프라이트가 할당되어야 합니다.");
            Assert.IsNotNull(_wingVisual.SubWingRightRenderer.sprite, "장착된 날개는 스프라이트가 할당되어야 합니다.");
        }

        [Test]
        public void WingSelectionPopup_ExecuteEquipAndUnequip_TransfersItemAndUpdatesVisual()
        {
            var popupGo = new GameObject("[Test_WingSelectionPopup]");
            var popup = popupGo.AddComponent<WingSelectionPopup>();

            try
            {
                var inv = WingInventoryManager.Instance;
                var testWing = new WingItemInstance("W_TEST_ML", "천공의 깃[좌]", WingSlotType.MainLeft, 4, "SET_WHITE_TIGER", 35);
                inv.AddWing(testWing);

                Assert.AreEqual(1, inv.TotalWingCount);
                Assert.IsNull(_sampleCharacter.GetEquippedWing(WingSlotType.MainLeft));

                // 장착 팝업 오픈 및 장착 실행
                popup.Open(_sampleCharacter, WingSlotType.MainLeft, _wingVisual);
                popup.ExecuteEquip(testWing);

                // 인벤토리에서 차감되고 캐릭터에 장착되었는지 검증
                Assert.AreEqual(0, inv.TotalWingCount, "장착된 날개는 인벤토리에서 제거되어야 합니다.");
                Assert.AreEqual(testWing, _sampleCharacter.GetEquippedWing(WingSlotType.MainLeft), "캐릭터 슬롯에 장착되어야 합니다.");
                Assert.IsTrue(_wingVisual.MainWingLeftRenderer.gameObject.activeSelf, "비주얼 렌더러가 활성화되어야 합니다.");

                // 장착 해제 실행
                popup.Open(_sampleCharacter, WingSlotType.MainLeft, _wingVisual);
                popup.ExecuteUnequip();

                // 인벤토리로 회수되고 캐릭터 슬롯이 비워졌는지 검증
                Assert.AreEqual(1, inv.TotalWingCount, "해제된 날개는 인벤토리로 복귀해야 합니다.");
                Assert.IsNull(_sampleCharacter.GetEquippedWing(WingSlotType.MainLeft), "캐릭터 슬롯에서 제거되어야 합니다.");
                Assert.IsFalse(_wingVisual.MainWingLeftRenderer.gameObject.activeSelf, "비주얼 렌더러가 비활성화되어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupGo);
            }
        }

        [Test]
        public void WingEquipmentUI_OpenAndClose_TogglesStateCorrectly()
        {
            var uiGo = new GameObject("[Test_WingEquipmentUI]");
            var ui = uiGo.AddComponent<WingEquipmentUI>();

            try
            {
                Assert.IsFalse(ui.IsOpen);

                ui.Open(_sampleCharacter, _wingVisual);
                Assert.IsTrue(ui.IsOpen);
                Assert.AreEqual(_sampleCharacter, ui.CurrentCharacter);

                ui.Close();
                Assert.IsFalse(ui.IsOpen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        [Test]
        public void WingAcquisitionPopup_ShowAcquisition_QueuesBannerWithoutError()
        {
            var acqGo = new GameObject("[Test_WingAcquisitionPopup]");
            var acq = acqGo.AddComponent<WingAcquisitionPopup>();

            try
            {
                var dropWing = new WingItemInstance("W_DROP_01", "전설의 신염익 [주 날개(좌)]", WingSlotType.MainLeft, 5, "SET_SOLAR_PHOENIX", 70);

                Assert.DoesNotThrow(() =>
                {
                    acq.ShowAcquisition(dropWing, "DiceCritSuccess_20");
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(acqGo);
            }
        }
    }
}
