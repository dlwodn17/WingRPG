using System;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Saju;

namespace RPG25D.Data
{
    /// <summary>
    /// [요구 산출물 3] 소환된 캐릭터의 런타임 영구 인스턴스 (CharacterInstance.cs)
    /// GUID 기반 고유 식별자, 원본 마스터 데이터 참조, 레벨, 한계돌파,
    /// 그리고 소환 시 8회 주사위로 각인된 고유 사주팔자(CharacterSaju)를 보유합니다.
    /// </summary>
    [Serializable]
    public class CharacterInstance
    {
        [Tooltip("인스턴스 고유 식별자 (GUID)")]
        public string InstanceID;

        [Tooltip("참조하는 베이스 캐릭터 마스터 ID")]
        public string BaseDataID;

        [Tooltip("캐릭터 이름")]
        public string CharacterName;

        [Tooltip("캐릭터 희귀도 (3, 4, 5성)")]
        public int Rarity;

        [Tooltip("기본 공격력")]
        public int BaseAttack;

        [Tooltip("캐릭터 레벨")]
        public int Level = 1;

        [Tooltip("캐릭터 초월/한계돌파 단계")]
        public int Transcendence = 0;

        [Tooltip("소환 시 각인된 고유 사주팔자 명식 (四柱八字)")]
        public CharacterSaju Saju;

        /// <summary>
        /// [세부 개발 명세 3] 캐릭터 날개 장착 슬롯 4부위 (MainLeft, MainRight, SubLeft, SubRight)
        /// </summary>
        public Dictionary<WingSlotType, WingItemInstance> EquippedWings { get; } = new Dictionary<WingSlotType, WingItemInstance>
        {
            { WingSlotType.MainLeft, null },
            { WingSlotType.MainRight, null },
            { WingSlotType.SubLeft, null },
            { WingSlotType.SubRight, null }
        };

        /// <summary>
        /// 장착된 4개 날개 슬롯 및 활성화된 세트 효과로부터 얻는 총 추가 공격력
        /// </summary>
        public int TotalWingBonusDamage
        {
            get
            {
                if (EquippedWings == null) return 0;
                int flat = 0;
                foreach (var wing in EquippedWings.Values)
                {
                    if (wing != null) flat += wing.BonusDamage;
                }
                int setBonus = RPG25D.Core.Wings.WingSetDatabase.GetTotalSetBonusDamage(EquippedWings.Values);
                return flat + setBonus;
            }
        }

        /// <summary>
        /// 레벨, 초월, 그리고 장착된 날개 및 세트 효과가 모두 반영된 최종 공격력 계산식
        /// </summary>
        public int CurrentAttack => BaseAttack + (Level - 1) * 3 + Transcendence * 15 + TotalWingBonusDamage;

        /// <summary>
        /// [세부 개발 명세 3] 특정 슬롯에 날개를 장착합니다.
        /// 슬롯 타입 불일치 시 장착을 차단하고 ArgumentException 예외를 발생시킵니다.
        /// </summary>
        public bool EquipWing(WingSlotType slot, WingItemInstance wing)
        {
            if (wing == null)
            {
                UnequipWing(slot);
                return true;
            }

            if (wing.SlotType != slot)
            {
                throw new ArgumentException($"[CharacterInstance] 장착 부위가 일치하지 않습니다. (목표 슬롯: {slot}, 날개 부위: {wing.SlotType})");
            }

            EquippedWings[slot] = wing;
            return true;
        }

        /// <summary>
        /// [세부 개발 명세 3] 특정 슬롯의 날개를 장착 해제하고 해제된 인스턴스를 반환합니다.
        /// </summary>
        public WingItemInstance UnequipWing(WingSlotType slot)
        {
            if (EquippedWings.TryGetValue(slot, out var existing) && existing != null)
            {
                EquippedWings[slot] = null;
                return existing;
            }
            return null;
        }

        public WingItemInstance GetEquippedWing(WingSlotType slot)
        {
            return EquippedWings.TryGetValue(slot, out var wing) ? wing : null;
        }

        public CharacterInstance()
        {
            InstanceID = Guid.NewGuid().ToString();
            Level = 1;
            Transcendence = 0;
        }

        public CharacterInstance(CharacterBaseData baseData, CharacterSaju saju)
        {
            InstanceID = Guid.NewGuid().ToString();
            BaseDataID = baseData != null ? baseData.CharacterID : "UNKNOWN";
            CharacterName = baseData != null ? baseData.CharacterName : "무명";
            Rarity = baseData != null ? baseData.BaseRarity : 3;
            BaseAttack = baseData != null ? baseData.BaseAttack : 20;
            Saju = saju;
            Level = 1;
            Transcendence = 0;
        }

        /// <summary>
        /// 캐릭터 요약 문자열 출력
        /// </summary>
        public override string ToString()
        {
            string sajuStr = Saju != null ? Saju.ToEightCharacters() : "사주 미부여";
            return $"[{Rarity}성] {CharacterName} (Lv.{Level}, 사주: {sajuStr}, ID: {InstanceID.Substring(0, 8)}...)";
        }
    }
}
