using System.Collections.Generic;
using System.Linq;
using RPG25D.Core.Dice;

namespace RPG25D.Core.Skills
{
    /// <summary>
    /// 스킬 A 조건: 단일 주사위 N 이상 (기본: 1개, 눈금 4 이상)
    /// </summary>
    public class MinSingleDieCondition : ISkillCondition
    {
        public int RequiredDiceCount => 1;
        public int MinValue { get; }
        public string ConditionDescription => $"주사위 1개 (눈금 {MinValue} 이상)";

        public MinSingleDieCondition(int minValue = 4)
        {
            MinValue = minValue;
        }

        public bool CanActivate(IReadOnlyList<Die> assignedDice, out string failureReason)
        {
            if (assignedDice == null || assignedDice.Count != 1)
            {
                failureReason = "주사위가 정확히 1개 필요합니다.";
                return false;
            }

            if (assignedDice[0].Value < MinValue)
            {
                failureReason = $"주사위 눈금이 {MinValue} 이상이어야 합니다. (현재: {assignedDice[0].Value})";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 스킬 B 조건: N개 주사위 합계 M 이상 (기본: 2개, 합산 8 이상)
    /// </summary>
    public class SumMinCondition : ISkillCondition
    {
        public int RequiredDiceCount { get; }
        public int MinSum { get; }
        public string ConditionDescription => $"주사위 {RequiredDiceCount}개 (합산 {MinSum} 이상)";

        public SumMinCondition(int requiredDiceCount = 2, int minSum = 8)
        {
            RequiredDiceCount = requiredDiceCount;
            MinSum = minSum;
        }

        public bool CanActivate(IReadOnlyList<Die> assignedDice, out string failureReason)
        {
            if (assignedDice == null || assignedDice.Count != RequiredDiceCount)
            {
                failureReason = $"주사위가 정확히 {RequiredDiceCount}개 필요합니다. (현재: {assignedDice?.Count ?? 0}개)";
                return false;
            }

            int sum = assignedDice.Sum(d => d.Value);
            if (sum < MinSum)
            {
                failureReason = $"주사위 눈금의 합이 {MinSum} 이상이어야 합니다. (현재 합: {sum})";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 스킬 C 조건: 짝수 눈금 주사위 N개 (기본: 1개, 짝수 2, 4, 6)
    /// </summary>
    public class EvenNumberCondition : ISkillCondition
    {
        public int RequiredDiceCount => 1;
        public string ConditionDescription => "짝수 눈금 주사위 1개 (2, 4, 6)";

        public bool CanActivate(IReadOnlyList<Die> assignedDice, out string failureReason)
        {
            if (assignedDice == null || assignedDice.Count != 1)
            {
                failureReason = "주사위가 정확히 1개 필요합니다.";
                return false;
            }

            if (assignedDice[0].Value % 2 != 0)
            {
                failureReason = $"주사위 눈금이 짝수여야 합니다. (현재: {assignedDice[0].Value})";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
