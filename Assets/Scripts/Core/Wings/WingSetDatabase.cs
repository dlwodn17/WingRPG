using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Core.Wings
{
    /// <summary>
    /// [세부 개발 명세 2] 활성화된 세트 효과 결과 구조체
    /// 2세트 및 4세트 장착 여부, 총 추가 대미지, D20 판정 보정치 및 특수 기믹 플래그를 담습니다.
    /// </summary>
    [Serializable]
    public struct ActiveSetBonuses
    {
        public string SetID;
        public string SetName;
        public int EquippedCount;
        public bool Has2SetBonus;
        public bool Has4SetBonus;
        public int BonusDamage;
        public float CritRateBonus;
        public int D20RollBonus;
        public bool SpecialGimmickActive;
        public string Description;

        public override string ToString()
        {
            string bonus2 = Has2SetBonus ? " [2세트 활성]" : "";
            string bonus4 = Has4SetBonus ? " [4세트 완성!]" : "";
            return $"{SetName} ({SetID}, {EquippedCount}부위 장착){bonus2}{bonus4} -> +{BonusDamage} 대미지, D20 +{D20RollBonus}";
        }
    }

    /// <summary>
    /// [요구 산출물 2] WingSetDefinition.cs
    /// 날개 세트 효과 정의 ScriptableObject (2세트/4세트 보너스)
    /// </summary>
    [CreateAssetMenu(fileName = "WingSetDefinition", menuName = "RPG25D/Wings/WingSetDefinition")]
    public class WingSetDefinition : ScriptableObject
    {
        [Header("세트 기본 식별자")]
        public string SetID = "SET_BLUE_DRAGON";
        public string SetName = "청룡의 서약";

        [Header("2세트 효과")]
        public int BonusDamage2pc = 30;
        public float CritRateBonus2pc = 0.10f;
        public string Description2pc = "공격력 +30, 치명타 확률 +10%";

        [Header("4세트 효과")]
        public int BonusDamage4pc = 75;
        public int D20RollBonus4pc = 2; // D20 주사위 판정 +2 보정
        public bool SpecialGimmick4pc = true; // 특수 기믹(즉사 판정 확률 상승 또는 추가 타격)
        public string Description4pc = "공격력 +75, D20 주사위 판정 +2 보정치, 특수 기믹 활성화";

        /// <summary>
        /// 런타임 및 단위 테스트에서 메모리 상에 즉시 세트 정의를 생성하는 팩토리 메서드
        /// </summary>
        public static WingSetDefinition Create(
            string setId,
            string setName,
            int bonusDamage2pc = 30,
            float critRate2pc = 0.10f,
            int bonusDamage4pc = 75,
            int d20Bonus4pc = 2,
            bool specialGimmick4pc = true)
        {
            var def = CreateInstance<WingSetDefinition>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.SetID = setId;
            def.SetName = setName;
            def.BonusDamage2pc = bonusDamage2pc;
            def.CritRateBonus2pc = critRate2pc;
            def.BonusDamage4pc = bonusDamage4pc;
            def.D20RollBonus4pc = d20Bonus4pc;
            def.SpecialGimmick4pc = specialGimmick4pc;
            return def;
        }
    }

    /// <summary>
    /// [요구 산출물 2] WingSetDatabase.cs
    /// 날개 세트 효과 정의 데이터베이스 및 4개 슬롯 착용 현황 세트 효과 판정 엔진
    /// </summary>
    public static class WingSetDatabase
    {
        private static readonly Dictionary<string, WingSetDefinition> _setRegistry = new Dictionary<string, WingSetDefinition>(StringComparer.OrdinalIgnoreCase);

        static WingSetDatabase()
        {
            InitializeDefaultSets();
        }

        private static void InitializeDefaultSets()
        {
            RegisterSet(WingSetDefinition.Create(
                "SET_BLUE_DRAGON", "청룡의 서약",
                bonusDamage2pc: 30, critRate2pc: 0.10f,
                bonusDamage4pc: 75, d20Bonus4pc: 2, specialGimmick4pc: true));

            RegisterSet(WingSetDefinition.Create(
                "SET_SOLAR_PHOENIX", "태양의 불꽃",
                bonusDamage2pc: 40, critRate2pc: 0.12f,
                bonusDamage4pc: 90, d20Bonus4pc: 3, specialGimmick4pc: true));

            RegisterSet(WingSetDefinition.Create(
                "SET_WHITE_TIGER", "백호의 포효",
                bonusDamage2pc: 25, critRate2pc: 0.08f,
                bonusDamage4pc: 60, d20Bonus4pc: 1, specialGimmick4pc: false));

            RegisterSet(WingSetDefinition.Create(
                "SET_BLACK_TORTOISE", "현무의 수호",
                bonusDamage2pc: 20, critRate2pc: 0.05f,
                bonusDamage4pc: 50, d20Bonus4pc: 1, specialGimmick4pc: false));
        }

        public static void RegisterSet(WingSetDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.SetID))
            {
                _setRegistry[definition.SetID] = definition;
            }
        }

        public static WingSetDefinition GetSetDefinition(string setId)
        {
            if (string.IsNullOrEmpty(setId)) return null;
            if (!_setRegistry.TryGetValue(setId, out var def) || def == null)
            {
                InitializeDefaultSets();
                _setRegistry.TryGetValue(setId, out def);
            }
            return def;
        }

        /// <summary>
        /// [세부 개발 명세 2] 4개 장착 슬롯을 순회하여 동일 SetID 카운트를 계산하고,
        /// 2세트 및 4세트 활성화 여부와 수치를 담은 ActiveSetBonuses 목록을 반환합니다.
        /// </summary>
        public static List<ActiveSetBonuses> EvaluateActiveSetEffects(IEnumerable<WingItemInstance> equippedWings)
        {
            var activeList = new List<ActiveSetBonuses>();
            if (equippedWings == null) return activeList;

            // 1. SetID별 장착 개수 카운트 (null 또는 빈 문자열 제외)
            var setCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var wing in equippedWings)
            {
                if (wing == null || string.IsNullOrEmpty(wing.SetID)) continue;

                if (!setCounts.ContainsKey(wing.SetID))
                {
                    setCounts[wing.SetID] = 0;
                }
                setCounts[wing.SetID]++;
            }

            // 2. 2세트 이상 장착된 세트 판정
            foreach (var kvp in setCounts)
            {
                string setId = kvp.Key;
                int count = kvp.Value;

                if (count < 2) continue; // 2세트 미만은 활성화되지 않음

                var def = GetSetDefinition(setId);
                string setName = def != null ? def.SetName : $"세트({setId})";

                bool has2Set = count >= 2;
                bool has4Set = count >= 4;

                int bonusDmg = 0;
                float critRate = 0f;
                int d20Bonus = 0;
                bool gimmick = false;
                string desc = "";

                if (def != null)
                {
                    if (has2Set)
                    {
                        bonusDmg += def.BonusDamage2pc;
                        critRate += def.CritRateBonus2pc;
                        desc += def.Description2pc;
                    }
                    if (has4Set)
                    {
                        bonusDmg += def.BonusDamage4pc;
                        d20Bonus += def.D20RollBonus4pc;
                        gimmick = def.SpecialGimmick4pc;
                        desc += " | " + def.Description4pc;
                    }
                }
                else
                {
                    // 미등록 커스텀 세트 기본 수치 fallback
                    if (has2Set) bonusDmg += 25;
                    if (has4Set)
                    {
                        bonusDmg += 60;
                        d20Bonus += 2;
                        gimmick = true;
                    }
                }

                activeList.Add(new ActiveSetBonuses
                {
                    SetID = setId,
                    SetName = setName,
                    EquippedCount = count,
                    Has2SetBonus = has2Set,
                    Has4SetBonus = has4Set,
                    BonusDamage = bonusDmg,
                    CritRateBonus = critRate,
                    D20RollBonus = d20Bonus,
                    SpecialGimmickActive = gimmick,
                    Description = desc
                });
            }

            return activeList;
        }

        /// <summary>
        /// 장착된 날개들로부터 발생하는 모든 세트 효과 추가 공격력 총합 계산
        /// </summary>
        public static int GetTotalSetBonusDamage(IEnumerable<WingItemInstance> equippedWings)
        {
            var activeSets = EvaluateActiveSetEffects(equippedWings);
            return activeSets.Sum(s => s.BonusDamage);
        }

        /// <summary>
        /// 장착된 날개들로부터 발생하는 총 D20 주사위 판정 보정치 계산
        /// </summary>
        public static int GetTotalD20RollBonus(IEnumerable<WingItemInstance> equippedWings)
        {
            var activeSets = EvaluateActiveSetEffects(equippedWings);
            return activeSets.Sum(s => s.D20RollBonus);
        }
    }
}
