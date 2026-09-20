using System;
using System.Collections.Generic;
using System.Linq;
using RPG25D.Core.Dice;

namespace RPG25D.Core.Skills
{
    public enum SkillType
    {
        SingleAttack, // 단일 공격 (스킬 A: 눈금 비례 피해)
        AoEAttack,    // 광역 공격 (스킬 B: 적 전체 피해)
        Shield,       // 방어/실드 (스킬 C: 실드 부여)
        Heal          // 회복 (스킬 C 대안 또는 확장: HP 회복)
    }

    /// <summary>
    /// 스킬 실행 결과 모델
    /// </summary>
    public class SkillExecutionResult
    {
        public string SkillName { get; set; }
        public SkillType Type { get; set; }
        public int TotalPower { get; set; }
        public string Message { get; set; }
        public List<int> AffectedActorIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// 스킬 로직 도메인 모델 (순수 C#)
    /// </summary>
    public class SkillModel
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public SkillType Type { get; }
        public ISkillCondition Condition { get; }
        public int BaseValue { get; }
        public float Multiplier { get; }

        public SkillModel(string id, string name, string description, SkillType type, ISkillCondition condition, int baseValue, float multiplier = 1f)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            Condition = condition;
            BaseValue = baseValue;
            Multiplier = multiplier;
        }

        public int CalculatePower(IReadOnlyList<Die> dice)
        {
            int diceSum = dice != null ? dice.Sum(d => d.Value) : 0;
            switch (Type)
            {
                case SkillType.SingleAttack:
                    // 주사위 눈금에 비례한 피해: Base + (DieValue * Multiplier)
                    return BaseValue + (int)(diceSum * Multiplier);
                case SkillType.AoEAttack:
                    // 전체 피해: Base + (DiceSum * Multiplier)
                    return BaseValue + (int)(diceSum * Multiplier);
                case SkillType.Shield:
                case SkillType.Heal:
                    return BaseValue + (int)(diceSum * Multiplier);
                default:
                    return BaseValue;
            }
        }
    }
}
