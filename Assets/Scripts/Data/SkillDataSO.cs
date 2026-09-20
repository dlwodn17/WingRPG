using UnityEngine;
using RPG25D.Core.Skills;

namespace RPG25D.Data
{
    public enum SkillConditionType
    {
        MinSingleDie,   // 스킬 A: 주사위 1개, 눈금 N 이상
        SumMin,         // 스킬 B: 주사위 N개, 합산 M 이상
        EvenNumber      // 스킬 C: 짝수 눈금 주사위 1개
    }

    [CreateAssetMenu(fileName = "SkillData", menuName = "RPG25D/Skill Data")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("스킬 메타 정보")]
        public string skillId = "SKILL_A";
        public string skillName = "정밀 타격";
        [TextArea(2, 4)]
        public string description = "단일 적에게 주사위 눈금에 비례한 강한 피해를 입힙니다.";
        public SkillType skillType = SkillType.SingleAttack;

        [Header("발동 조건 설정")]
        public SkillConditionType conditionType = SkillConditionType.MinSingleDie;
        [Tooltip("MinSingleDie: 최소 눈금 (예: 4)")]
        public int singleMinDieValue = 4;

        [Tooltip("SumMin: 필요 주사위 개수 (예: 2)")]
        public int sumRequiredDiceCount = 2;
        [Tooltip("SumMin: 최소 합계 (예: 8)")]
        public int sumMinValue = 8;

        [Header("수치 설정")]
        public int basePower = 10;
        public float powerMultiplier = 2.5f;

        public ISkillCondition CreateCondition()
        {
            switch (conditionType)
            {
                case SkillConditionType.MinSingleDie:
                    return new MinSingleDieCondition(singleMinDieValue);
                case SkillConditionType.SumMin:
                    return new SumMinCondition(sumRequiredDiceCount, sumMinValue);
                case SkillConditionType.EvenNumber:
                    return new EvenNumberCondition();
                default:
                    return new MinSingleDieCondition(1);
            }
        }

        public SkillModel CreateModel()
        {
            return new SkillModel(
                skillId,
                skillName,
                description,
                skillType,
                CreateCondition(),
                basePower,
                powerMultiplier
            );
        }
    }
}
