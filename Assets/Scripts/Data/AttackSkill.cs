using System;
using UnityEngine;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;

namespace RPG25D.Data
{
    /// <summary>
    /// 공격 결과 데이터 구조체
    /// </summary>
    public struct AttackExecutionResult
    {
        public string SkillName;
        public int D20Value;
        public D20Outcome Outcome;
        public int DamageDealt;
        public bool IsTargetKilled;
        public string LogMessage;
    }

    /// <summary>
    /// [요구 산출물 2] AttackSkill.cs
    /// 보호막/힐 로직을 완전히 배제하고, 기본 대미지와 D20 눈금에 따른 피해량을 계산하는 ScriptableObject 기반 공격 스킬 클래스입니다.
    /// 헤드리스 CLI 및 단위 테스트에서도 ScriptableObject.CreateInstance 또는 직접 생성하여 활용 가능합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttackSkill", menuName = "RPG25D/Attack Skill")]
    public class AttackSkill : ScriptableObject
    {
        [Header("스킬 기본 정보")]
        public string skillId = "SKILL_SLASH";
        public string skillName = "참격 (Slash)";
        [TextArea(2, 3)]
        public string description = "적 하나를 강하게 베어냅니다.";

        [Header("대미지 및 계수 설정")]
        [Tooltip("기본 대미지 (Base Damage)")]
        public int baseDamage = 15;

        [Tooltip("D20 주사위 눈금 스케일링 계수 (Scale Multiplier)")]
        public float scaleMultiplier = 2.0f;

        [Tooltip("타격 횟수 (1 = 단일 타격, >1 = 연속 타격)")]
        [Range(1, 5)]
        public int hitCount = 1;

        /// <summary>
        /// D20 눈금(1~20)에 따른 최종 피해량을 계산합니다.
        /// - 1 (대실패): 0
        /// - 2~19 (일반 적중): BaseDamage + (D20Value * ScaleMultiplier)
        /// - 20 (즉사): 대상 즉사 처리 (수치 연산 시에는 대상 최대 체력 이상 반환)
        /// </summary>
        public int CalculateDamage(int d20Value)
        {
            if (d20Value <= 1) return 0;
            if (d20Value >= 20) return 99999; // 즉사용 상징 수치

            float scaled = baseDamage + (d20Value * scaleMultiplier);
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        /// <summary>
        /// D20 굴림 결과를 적용하여 타깃에게 공격을 실행하고 상세 결과를 반환합니다.
        /// </summary>
        public AttackExecutionResult Execute(D20RollResult roll, BattleActor attacker, BattleActor target)
        {
            if (target == null)
            {
                return new AttackExecutionResult
                {
                    SkillName = skillName,
                    D20Value = roll.Value,
                    Outcome = roll.Outcome,
                    DamageDealt = 0,
                    IsTargetKilled = false,
                    LogMessage = $"[{attacker?.Name}]의 [{skillName}] 시전 실패: 대상이 없습니다."
                };
            }

            var result = new AttackExecutionResult
            {
                SkillName = skillName,
                D20Value = roll.Value,
                Outcome = roll.Outcome
            };

            switch (roll.Outcome)
            {
                case D20Outcome.Fail:
                    // 눈금 1 (대실패): 스킬 완전 실패 (Miss, 주는 대미지 0)
                    result.DamageDealt = 0;
                    result.IsTargetKilled = false;
                    result.LogMessage = $"[{attacker.Name}]의 [{skillName}]! D20 굴림: [{roll.Value}] ❌ [대실패 (Miss!)] 대상에게 빗나갔습니다! (대미지 0)";
                    break;

                case D20Outcome.InstantKill:
                    // 눈금 20 (절대 성공/즉사): 대상의 남은 HP와 상관없이 즉시 사망
                    target.KillInstantly();
                    result.DamageDealt = target.MaxHP;
                    result.IsTargetKilled = true;
                    result.LogMessage = $"[{attacker.Name}]의 [{skillName}]! D20 굴림: [{roll.Value}] 💀 [절대 성공 / 즉사 (Instant Kill!)] {target.Name}이(가) 즉시 사망했습니다!";
                    break;

                case D20Outcome.Hit:
                default:
                    // 눈금 2~19 (일반 판정): 기본 대미지 + (주사위 눈금 * 스케일링 계수)
                    int finalDamage = CalculateDamage(roll.Value);
                    target.TakeDamage(finalDamage);
                    result.DamageDealt = finalDamage;
                    result.IsTargetKilled = !target.IsAlive;
                    result.LogMessage = $"[{attacker.Name}]의 [{skillName}]! D20 굴림: [{roll.Value}] ⚔️ [적중 (Hit)] {target.Name}에게 {finalDamage} 피해! (남은 HP: {target.CurrentHP}/{target.MaxHP})";
                    break;
            }

            return result;
        }

        /// <summary>
        /// 순수 C# 인스턴스 팩토리 (테스트 러너 및 런타임 생성 편의)
        /// </summary>
        public static AttackSkill CreateInstance(string name, int baseDmg, float multiplier, int hits = 1)
        {
            var skill = ScriptableObject.CreateInstance<AttackSkill>();
            skill.skillId = name;
            skill.skillName = name;
            skill.baseDamage = baseDmg;
            skill.scaleMultiplier = multiplier;
            skill.hitCount = hits;
            return skill;
        }
    }
}
