#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RPG25D.Core.Wings;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.UI;
using RPG25D.Visual;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 5] WingSystemEditorSetup.cs
    /// 에디터 상에서 1클릭으로 날개 UI 시스템 구성 및 더미 데이터(3~5성 세트 날개) 지급/테스트 유틸리티
    /// </summary>
    public static class WingSystemEditorSetup
    {
        [MenuItem("RPG25D/Wings/Setup Wing UI in Current Scene", false, 100)]
        public static void SetupWingUIInScene()
        {
            // 1. WingAcquisitionPopup 보장
            var acq = Object.FindAnyObjectByType<WingAcquisitionPopup>();
            if (acq == null)
            {
                var go = new GameObject("WingAcquisitionPopup");
                acq = go.AddComponent<WingAcquisitionPopup>();
                Undo.RegisterCreatedObjectUndo(go, "Create WingAcquisitionPopup");
                Debug.Log("🪽 [WingSystemEditorSetup] WingAcquisitionPopup 생성 완료");
            }

            // 2. WingSelectionPopup 보장
            var sel = Object.FindAnyObjectByType<WingSelectionPopup>();
            if (sel == null)
            {
                var go = new GameObject("WingSelectionPopup");
                sel = go.AddComponent<WingSelectionPopup>();
                Undo.RegisterCreatedObjectUndo(go, "Create WingSelectionPopup");
                Debug.Log("🪽 [WingSystemEditorSetup] WingSelectionPopup 생성 완료");
            }

            // 3. WingEquipmentUI 보장
            var eq = Object.FindAnyObjectByType<WingEquipmentUI>();
            if (eq == null)
            {
                var go = new GameObject("WingEquipmentUI");
                eq = go.AddComponent<WingEquipmentUI>();
                Undo.RegisterCreatedObjectUndo(go, "Create WingEquipmentUI");
                Debug.Log("🪽 [WingSystemEditorSetup] WingEquipmentUI 생성 완료");
            }

            Debug.Log("🎉 [WingSystemEditorSetup] 현재 씬에 모든 날개 UI 매니저 오브젝트 세팅이 완료되었습니다!");
        }

        [MenuItem("RPG25D/Wings/Give Test Wings (3~5 Stars Set)", false, 101)]
        public static void GiveTestWings()
        {
            var inv = WingInventoryManager.Instance;

            // 5성 청룡 세트 4부위
            inv.AddWing(new WingItemInstance("W_5S_ML_01", "청룡의 천공익 [주 날개(좌)]", WingSlotType.MainLeft, 5, "SET_BLUE_DRAGON", 65));
            inv.AddWing(new WingItemInstance("W_5S_MR_01", "청룡의 천공익 [주 날개(우)]", WingSlotType.MainRight, 5, "SET_BLUE_DRAGON", 65));
            inv.AddWing(new WingItemInstance("W_5S_SL_01", "청룡의 천공익 [부 날개(좌)]", WingSlotType.SubLeft, 5, "SET_BLUE_DRAGON", 55));
            inv.AddWing(new WingItemInstance("W_5S_SR_01", "청룡의 천공익 [부 날개(우)]", WingSlotType.SubRight, 5, "SET_BLUE_DRAGON", 55));

            // 4성 백호 세트 2부위
            inv.AddWing(new WingItemInstance("W_4S_ML_01", "백호의 서리날개 [주 날개(좌)]", WingSlotType.MainLeft, 4, "SET_WHITE_TIGER", 32));
            inv.AddWing(new WingItemInstance("W_4S_MR_01", "백호의 서리날개 [주 날개(우)]", WingSlotType.MainRight, 4, "SET_WHITE_TIGER", 32));

            // 3성 일반 날개 2부위
            inv.AddWing(new WingItemInstance("W_3S_SL_01", "바람의 깃털 [부 날개(좌)]", WingSlotType.SubLeft, 3, "", 14));
            inv.AddWing(new WingItemInstance("W_3S_SR_01", "바람의 깃털 [부 날개(우)]", WingSlotType.SubRight, 3, "", 14));

            Debug.Log($"🪽 [WingSystemEditorSetup] 3~5성 테스트 날개 세트 8개 지급 완료! (현재 인벤토리 총 {inv.TotalWingCount}개)");
        }
    }
}
#endif
