using System;
using UnityEngine;

namespace RPG25D.Data
{
    /// <summary>
    /// [세부 개발 명세 1] 날개 장착 부위 슬롯 열거형 (WingSlotType)
    /// 주 날개 좌/우, 부 날개 좌/우 총 4개 슬롯
    /// </summary>
    public enum WingSlotType
    {
        MainLeft = 0,   // 주 날개 좌
        MainRight = 1,  // 주 날개 우
        SubLeft = 2,    // 부 날개 좌
        SubRight = 3    // 부 날개 우
    }

    /// <summary>
    /// [요구 산출물 1] WingBaseData.cs
    /// 날개 원본 마스터 데이터 (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "WingBaseData", menuName = "RPG25D/Wings/WingBaseData")]
    public class WingBaseData : ScriptableObject
    {
        [Header("날개 기본 정보")]
        [Tooltip("날개 고유 마스터 ID")]
        public string WingID = "WING_001";

        [Tooltip("날개 이름")]
        public string WingName = "초심자의 깃털 날개";

        [Tooltip("희귀도 (3성, 4성, 5성)")]
        [Range(3, 5)]
        public int Rarity = 3;

        [Tooltip("장착 가능한 대상 슬롯 부위")]
        public WingSlotType TargetSlot = WingSlotType.MainLeft;

        [Tooltip("세트 효과 식별자 (4~5성일 경우 세트 ID, 3성은 빈 문자열)")]
        public string SetID = string.Empty;

        [Tooltip("기본 공격력 가산치")]
        public int FlatBonusDamage = 15;

        /// <summary>
        /// 런타임 및 단위 테스트에서 메모리 상에 인스턴스를 즉시 생성하는 팩토리 메서드
        /// </summary>
        public static WingBaseData Create(
            string id,
            string name,
            int rarity,
            WingSlotType targetSlot,
            string setId = "",
            int flatBonusDamage = 15)
        {
            var data = CreateInstance<WingBaseData>();
            data.WingID = id;
            data.WingName = name;
            data.Rarity = Mathf.Clamp(rarity, 3, 5);
            data.TargetSlot = targetSlot;
            data.SetID = setId ?? string.Empty;
            data.FlatBonusDamage = flatBonusDamage;
            return data;
        }

        public override string ToString()
        {
            string setStr = !string.IsNullOrEmpty(SetID) ? $" [Set: {SetID}]" : "";
            return $"[{Rarity}성] {WingName} ({TargetSlot}, +{FlatBonusDamage} Atk{setStr})";
        }
    }
}
