using System.Collections.Generic;
using RPG25D.Core.Dice;

namespace RPG25D.Core.Skills
{
    /// <summary>
    /// 스킬 발동 조건 검증 인터페이스 (순수 C#)
    /// </summary>
    public interface ISkillCondition
    {
        int RequiredDiceCount { get; }
        string ConditionDescription { get; }
        bool CanActivate(IReadOnlyList<Die> assignedDice, out string failureReason);
    }
}
