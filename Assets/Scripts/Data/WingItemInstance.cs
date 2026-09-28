using System;
using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// [요구 산출물 1] WingItemInstance.cs
    /// 날개 아이템의 런타임 영구 인스턴스 (직렬화 가능한 순수 C# 클래스)
    /// GUID 기반 고유 식별자, 원본 마스터 ID, 슬롯 부위, 세트 ID, 등급 및 추가 공격력을 관리합니다.
    /// </summary>
    [Serializable]
    public class WingItemInstance
    {
        [Tooltip("아이템 인스턴스 고유 식별자 (GUID)")]
        public string ItemInstanceID;

        [Tooltip("참조하는 베이스 날개 마스터 ID")]
        public string BaseWingID;

        [Tooltip("날개 아이템 이름")]
        public string WingName;

        [Tooltip("장착 슬롯 부위 (주날개 좌/우, 부날개 좌/우)")]
        public WingSlotType SlotType;

        [Tooltip("세트 효과 식별자 (없을 시 빈 문자열)")]
        public string SetID;

        [Tooltip("희귀도 (3, 4, 5성)")]
        public int Rarity;

        [Tooltip("기본 공격력 가산치")]
        public int BonusDamage;

        public WingItemInstance()
        {
            ItemInstanceID = Guid.NewGuid().ToString();
            BaseWingID = "UNKNOWN";
            WingName = "알 수 없는 날개";
            SlotType = WingSlotType.MainLeft;
            SetID = string.Empty;
            Rarity = 3;
            BonusDamage = 10;
        }

        public WingItemInstance(WingBaseData baseData)
        {
            ItemInstanceID = Guid.NewGuid().ToString();
            if (baseData != null)
            {
                BaseWingID = baseData.WingID;
                WingName = baseData.WingName;
                SlotType = baseData.TargetSlot;
                SetID = baseData.SetID ?? string.Empty;
                Rarity = baseData.Rarity;
                BonusDamage = baseData.FlatBonusDamage;
            }
            else
            {
                BaseWingID = "UNKNOWN";
                WingName = "알 수 없는 날개";
                SlotType = WingSlotType.MainLeft;
                SetID = string.Empty;
                Rarity = 3;
                BonusDamage = 10;
            }
        }

        public WingItemInstance(
            string baseWingId,
            string wingName,
            WingSlotType slotType,
            int rarity,
            string setId,
            int bonusDamage)
        {
            ItemInstanceID = Guid.NewGuid().ToString();
            BaseWingID = baseWingId;
            WingName = wingName;
            SlotType = slotType;
            Rarity = Mathf.Clamp(rarity, 3, 5);
            SetID = setId ?? string.Empty;
            BonusDamage = bonusDamage;
        }

        public override string ToString()
        {
            string setTag = !string.IsNullOrEmpty(SetID) ? $" [세트: {SetID}]" : "";
            return $"[{Rarity}성] {WingName} ({SlotType}, +{BonusDamage} Atk{setTag}, ID: {ItemInstanceID.Substring(0, 8)}...)";
        }
    }
}
