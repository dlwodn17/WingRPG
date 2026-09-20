using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Data;
using RPG25D.Gameplay;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 4] D20 기반 공격 턴제 전투 로직 단위 테스트
    /// 콘솔 환경에서 전체 전투 흐름(대실패, 일반 적중 대미지 스케일링, 즉사 케이스)을 헤드리스로 검증합니다.
    /// </summary>
    [TestFixture]
    public class D20BattleTests
    {
        #region 1. D20DiceRoller 판정 검증

        [Test]
        public void D20DiceRoller_CriticalFail_WhenValueIs1()
        {
            var result = D20DiceRoller.Evaluate(1);
            Assert.AreEqual(1, result.Value);
            Assert.AreEqual(D20Outcome.Fail, result.Outcome);
        }

        [TestCase(2)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(15)]
        [TestCase(19)]
        public void D20DiceRoller_NormalHit_WhenValueIs2To19(int rollVal)
        {
            var result = D20DiceRoller.Evaluate(rollVal);
            Assert.AreEqual(rollVal, result.Value);
            Assert.AreEqual(D20Outcome.Hit, result.Outcome);
        }

        [Test]
        public void D20DiceRoller_InstantKill_WhenValueIs20()
        {
            var result = D20DiceRoller.Evaluate(20);
            Assert.AreEqual(20, result.Value);
            Assert.AreEqual(D20Outcome.InstantKill, result.Outcome);
        }

        [Test]
        public void D20DiceRoller_Roll_GeneratesWithin1To20()
        {
            var roller = new D20DiceRoller(seed: 12345);
            for (int i = 0; i < 100; i++)
            {
                var roll = roller.Roll();
                Assert.GreaterOrEqual(roll.Value, 1);
                Assert.LessOrEqual(roll.Value, 20);
            }
        }

        #endregion

        #region 2. AttackSkill 판정 및 대미지 계산 검증

        [Test]
        public void AttackSkill_CriticalFail_DealsZeroDamage()
        {
            var skill = AttackSkill.CreateInstance("테스트 일격", baseDmg: 20, multiplier: 3.0f);
            var attacker = new BattleActor(1, "용사", maxHP: 100);
            var target = new BattleActor(2, "몬스터", maxHP: 100);

            var roll = D20DiceRoller.Evaluate(1); // 1 = 대실패
            var result = skill.Execute(roll, attacker, target);

            Assert.AreEqual(D20Outcome.Fail, result.Outcome);
            Assert.AreEqual(0, result.DamageDealt);
            Assert.AreEqual(100, target.CurrentHP);
            Assert.IsTrue(target.IsAlive);
        }

        [TestCase(2, 20, 2.0f, 24)]   // 20 + 2*2 = 24
        [TestCase(10, 15, 2.5f, 40)]  // 15 + 10*2.5 = 40
        [TestCase(19, 10, 3.0f, 67)]  // 10 + 19*3 = 67
        public void AttackSkill_NormalHit_CalculatesProportionalDamage(int diceVal, int baseDmg, float multiplier, int expectedDmg)
        {
            var skill = AttackSkill.CreateInstance("스케일링 공격", baseDmg, multiplier);
            var attacker = new BattleActor(1, "용사", maxHP: 100);
            var target = new BattleActor(2, "몬스터", maxHP: 200);

            var roll = D20DiceRoller.Evaluate(diceVal);
            var result = skill.Execute(roll, attacker, target);

            Assert.AreEqual(D20Outcome.Hit, result.Outcome);
            Assert.AreEqual(expectedDmg, result.DamageDealt);
            Assert.AreEqual(200 - expectedDmg, target.CurrentHP);
        }

        [Test]
        public void AttackSkill_InstantKill_ImmediatelyKillsTargetRegardlessOfHP()
        {
            var skill = AttackSkill.CreateInstance("참수 (Decapitation)", baseDmg: 10, multiplier: 1.0f);
            var attacker = new BattleActor(1, "용사", maxHP: 100);
            var boss = new BattleActor(2, "고대 드래곤", maxHP: 99999);

            var roll = D20DiceRoller.Evaluate(20); // 20 = 절대 성공/즉사
            var result = skill.Execute(roll, attacker, boss);

            Assert.AreEqual(D20Outcome.InstantKill, result.Outcome);
            Assert.IsTrue(result.IsTargetKilled);
            Assert.AreEqual(0, boss.CurrentHP);
            Assert.IsFalse(boss.IsAlive);
        }

        #endregion

        #region 3. 전투 전체 흐름 시뮬레이션 (헤드리스 CLI 호환)

        [Test]
        public void BattleTurnController_FullSimulation_PlayerVictoryViaInstantKill()
        {
            var go = new GameObject("[TestBattleController]");
            var controller = go.AddComponent<BattleTurnController>();

            controller.SetupBattle();
            Assert.AreEqual(3, controller.Enemies.Count, "3vs3 파티 전투에서는 적군이 3명이어야 합니다.");

            // 1턴: 적 1(고블린)에게 D20 = 20(즉사) 발동
            var result1 = controller.SimulatePlayerActionSync(skillIndex: 0, targetIndex: 0, d20Value: 20);
            Assert.AreEqual(D20Outcome.InstantKill, result1.Outcome);
            Assert.IsFalse(controller.Enemies[0].IsAlive);
            Assert.IsFalse(controller.CheckBattleEnd(), "적 2명 생존 중이므로 전투가 계속되어야 합니다.");

            // 2턴: 적 2(오크)에게 D20 = 20(즉사) 발동
            var result2 = controller.SimulatePlayerActionSync(skillIndex: 0, targetIndex: 1, d20Value: 20);
            Assert.AreEqual(D20Outcome.InstantKill, result2.Outcome);
            Assert.IsFalse(controller.Enemies[1].IsAlive);
            Assert.IsFalse(controller.CheckBattleEnd(), "적 1명 생존 중이므로 전투가 계속되어야 합니다.");

            // 3턴: 적 3(슬라임)에게 D20 = 19(결전 강타) 발동 -> 슬라임 HP(50) 초과 피해로 격파
            var result3 = controller.SimulatePlayerActionSync(skillIndex: 2, targetIndex: 2, d20Value: 19);
            Assert.IsFalse(controller.Enemies[2].IsAlive);

            // 모든 적 격파 -> 승리 확인
            Assert.IsTrue(controller.CheckBattleEnd());
            Assert.AreEqual(BattlePhase.BattleVictory, controller.CurrentPhase);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BattleTurnController_FullSimulation_EnemyTurnAndPlayerDefeat()
        {
            var go = new GameObject("[TestBattleController]");
            var controller = go.AddComponent<BattleTurnController>();

            controller.SetupBattle();
            Assert.AreEqual(3, controller.Allies.Count, "3vs3 파티 전투에서는 아군이 3명이어야 합니다.");

            // 적이 아군 3인에게 차례로 D20 = 20(즉사) 공격을 가하여 전멸시킴
            for (int i = 0; i < controller.Allies.Count; i++)
            {
                var enemyAttack = controller.SimulateEnemyActionSync(enemyIndex: 0, skillIndex: 0, d20Value: 20, targetAllyIndex: i);
                Assert.AreEqual(D20Outcome.InstantKill, enemyAttack.Outcome);
                Assert.IsFalse(controller.Allies[i].IsAlive);
            }

            Assert.IsFalse(controller.Player.IsAlive);

            // 아군 전멸 -> 패배 확인
            Assert.IsTrue(controller.CheckBattleEnd());
            Assert.AreEqual(BattlePhase.BattleDefeat, controller.CurrentPhase);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BattleTurnController_FullBattleSync_CompletesWithinMaxRounds()
        {
            var go = new GameObject("[TestBattleController]");
            var controller = go.AddComponent<BattleTurnController>();

            var finalPhase = controller.SimulateFullBattleSync(maxRounds: 50);
            Assert.IsTrue(finalPhase == BattlePhase.BattleVictory || finalPhase == BattlePhase.BattleDefeat,
                $"동기 전체 전투 시뮬레이션 결과는 Victory 또는 Defeat여야 합니다. (실제: {finalPhase})");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region 4. D20 물리 결과 판정 및 3D 메시 상향면 판독 검증

        [Test]
        public void D20DiceRoller_ProcessPhysicsResult_SetsPhysicalFlagAndOutcome()
        {
            var roller = new D20DiceRoller();

            var failResult = roller.ProcessPhysicsResult(1);
            Assert.AreEqual(1, failResult.Value);
            Assert.AreEqual(D20Outcome.Fail, failResult.Outcome);
            Assert.IsTrue(failResult.IsPhysicalRoll);

            var hitResult = roller.ProcessPhysicsResult(14);
            Assert.AreEqual(14, hitResult.Value);
            Assert.AreEqual(D20Outcome.Hit, hitResult.Outcome);
            Assert.IsTrue(failResult.IsPhysicalRoll);

            var instantKillResult = roller.ProcessPhysicsResult(20);
            Assert.AreEqual(20, instantKillResult.Value);
            Assert.AreEqual(D20Outcome.InstantKill, instantKillResult.Outcome);
            Assert.IsTrue(instantKillResult.IsPhysicalRoll);
        }

        [Test]
        public void D20MeshGenerator_GeneratesValid20FaceMesh()
        {
            var mesh = RPG25D.Visual.D20MeshGenerator.GenerateD20Mesh(0.8f);

            Assert.IsNotNull(mesh);
            Assert.AreEqual(60, mesh.vertexCount); // 20 faces * 3 vertices (flat-shaded)
            Assert.AreEqual(60, mesh.triangles.Length);

            var faceInfos = RPG25D.Visual.D20MeshGenerator.FaceInfos;
            Assert.AreEqual(20, faceInfos.Count);

            // 1부터 20까지의 숫자가 중복 없이 모두 존재하는지 검증
            var presentNumbers = new HashSet<int>();
            foreach (var info in faceInfos)
            {
                Assert.IsTrue(info.FaceNumber >= 1 && info.FaceNumber <= 20);
                presentNumbers.Add(info.FaceNumber);
            }
            Assert.AreEqual(20, presentNumbers.Count);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(14)]
        [TestCase(20)]
        public void D20MeshGenerator_ReadTopFace_MatchesTargetRotation(int targetFace)
        {
            var go = new GameObject("[TestDiceTransform]");
            go.transform.rotation = RPG25D.Visual.D20MeshGenerator.GetRotationForFaceUp(targetFace);

            int readFace = RPG25D.Visual.D20MeshGenerator.ReadTopFace(go.transform);
            Assert.AreEqual(targetFace, readFace, $"D20 상향면 회전값 설정 후 판독된 숫자가 일치해야 합니다: 목표 {targetFace} vs 판독 {readFace}");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region 5. 프로시저럴 오디오 실시간 생성 검증 (순수 C# 합성)

        [Test]
        public void ProceduralAudioGenerator_GeneratesValidAudioClipsForAllOutcomes()
        {
            var bounce = RPG25D.Visual.ProceduralAudioGenerator.CreateDiceBounceClip();
            Assert.IsNotNull(bounce);
            Assert.Greater(bounce.samples, 0);

            var critFail = RPG25D.Visual.ProceduralAudioGenerator.CreateCritFailClip();
            Assert.IsNotNull(critFail);
            Assert.Greater(critFail.samples, 0);

            var hitLow = RPG25D.Visual.ProceduralAudioGenerator.CreateHitClip(2);
            var hitHigh = RPG25D.Visual.ProceduralAudioGenerator.CreateHitClip(19);
            Assert.IsNotNull(hitLow);
            Assert.IsNotNull(hitHigh);
            Assert.Greater(hitLow.samples, 0);
            Assert.Greater(hitHigh.samples, 0);

            var fanfare = RPG25D.Visual.ProceduralAudioGenerator.CreateInstantKillFanfareClip();
            Assert.IsNotNull(fanfare);
            Assert.Greater(fanfare.samples, 0);
        }

        #endregion
    }
}
