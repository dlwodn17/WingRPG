using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Core.Skills;

namespace RPG25D.Tests
{
    [TestFixture]
    public class BattleLogicTests
    {
        #region 1. 주사위 풀 (DicePool) 단위 테스트

        [Test]
        public void DicePool_InitializesWithCorrectCount()
        {
            var pool = new DicePool(4, seed: 42);
            Assert.AreEqual(4, pool.AllDice.Count);
            Assert.AreEqual(4, pool.AvailableDice.Count());
        }

        [Test]
        public void DicePool_RollAll_GeneratesValuesBetween1And6()
        {
            var pool = new DicePool(4, seed: 123);
            pool.RollAll();

            foreach (var die in pool.AllDice)
            {
                Assert.GreaterOrEqual(die.Value, 1);
                Assert.LessOrEqual(die.Value, 6);
                Assert.IsFalse(die.IsConsumed);
            }
        }

        [Test]
        public void DicePool_Reroll_OnlyRerollsSpecifiedDice()
        {
            var pool = new DicePool(4, seed: 999);
            pool.RollAll();

            var die1 = pool.GetDieById(1);
            var die2 = pool.GetDieById(2);

            // die1만 리롤
            bool rerolled = pool.Reroll(new[] { 1 });
            Assert.IsTrue(rerolled);
        }

        [Test]
        public void DicePool_ConsumeDie_RemovesFromAvailable()
        {
            var pool = new DicePool(4);
            pool.ConsumeDie(1);

            var die = pool.GetDieById(1);
            Assert.IsTrue(die.IsConsumed);
            Assert.AreEqual(3, pool.AvailableDice.Count());
        }

        #endregion

        #region 2. 스킬 조건 (Skill Conditions) 단위 테스트

        [Test]
        public void SkillA_MinSingleDieCondition_ValidatesCorrectly()
        {
            var condition = new MinSingleDieCondition(minValue: 4);

            var dieLow = new Die(1, 3);
            var dieExact = new Die(2, 4);
            var dieHigh = new Die(3, 6);

            Assert.IsFalse(condition.CanActivate(new List<Die> { dieLow }, out _));
            Assert.IsTrue(condition.CanActivate(new List<Die> { dieExact }, out _));
            Assert.IsTrue(condition.CanActivate(new List<Die> { dieHigh }, out _));

            // 주사위 2개 할당 시 실패 검증
            Assert.IsFalse(condition.CanActivate(new List<Die> { dieExact, dieHigh }, out _));
        }

        [Test]
        public void SkillB_SumMinCondition_ValidatesCorrectly()
        {
            var condition = new SumMinCondition(requiredDiceCount: 2, minSum: 8);

            var diceSum7 = new List<Die> { new Die(1, 3), new Die(2, 4) }; // 합 7
            var diceSum8 = new List<Die> { new Die(1, 4), new Die(2, 4) }; // 합 8
            var diceSum11 = new List<Die> { new Die(1, 5), new Die(2, 6) }; // 합 11

            Assert.IsFalse(condition.CanActivate(diceSum7, out _));
            Assert.IsTrue(condition.CanActivate(diceSum8, out _));
            Assert.IsTrue(condition.CanActivate(diceSum11, out _));
        }

        [Test]
        public void SkillC_EvenNumberCondition_ValidatesCorrectly()
        {
            var condition = new EvenNumberCondition();

            var dieOdd = new Die(1, 5);
            var dieEven = new Die(2, 6);

            Assert.IsFalse(condition.CanActivate(new List<Die> { dieOdd }, out _));
            Assert.IsTrue(condition.CanActivate(new List<Die> { dieEven }, out _));
        }

        #endregion

        #region 3. 전투 참가자 (BattleActor) 단위 테스트

        [Test]
        public void BattleActor_ShieldAbsorbsDamageFirst()
        {
            var actor = new BattleActor(1, "Hero", maxHP: 100);
            actor.AddShield(20);

            // 실드 내 피해
            actor.TakeDamage(15);
            Assert.AreEqual(5, actor.Shield);
            Assert.AreEqual(100, actor.CurrentHP);

            // 실드 초과 피해 (남은 실드 5, 피해 15 -> HP 10 감소)
            actor.TakeDamage(15);
            Assert.AreEqual(0, actor.Shield);
            Assert.AreEqual(90, actor.CurrentHP);
        }

        [Test]
        public void BattleActor_DeathWhenHPIsZero()
        {
            var actor = new BattleActor(1, "Goblin", maxHP: 30);
            actor.TakeDamage(35);

            Assert.AreEqual(0, actor.CurrentHP);
            Assert.IsFalse(actor.IsAlive);
        }

        #endregion

        #region 4. 전투 상태 머신 (BattleStateMachine) 단위 테스트

        [Test]
        public void BattleStateMachine_FullTurnCycleAndVictory()
        {
            var player = new BattleActor(1, "Hero", maxHP: 100, baseAttack: 10, isPlayer: true);
            var enemy = new BattleActor(2, "Slime", maxHP: 20, baseAttack: 5, isPlayer: false);

            var bsm = new BattleStateMachine(player, new[] { enemy }, diceCount: 4, seed: 1);
            bsm.StartBattle();

            Assert.AreEqual(BattleTurnState.AssignPhase, bsm.CurrentState);

            // 스킬 A (단일 공격, 주사위 1개 4 이상) 모델 준비
            var skillA = new SkillModel(
                "SKILL_A", "강타", "설명",
                SkillType.SingleAttack,
                new MinSingleDieCondition(4),
                baseValue: 15,
                multiplier: 2f
            );

            var die6 = new Die(1, 6);
            var actions = new List<(SkillModel skill, List<Die> dice, BattleActor target)>
            {
                (skillA, new List<Die> { die6 }, enemy)
            };

            // 위력: 15 + (6 * 2) = 27 -> 체력 20인 슬라임 일격 격파
            bsm.ExecuteActions(actions);

            Assert.IsFalse(enemy.IsAlive);
            Assert.AreEqual(BattleTurnState.Victory, bsm.CurrentState);
        }

        #endregion
    }
}
