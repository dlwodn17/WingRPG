using System;
using System.Collections.Generic;
using System.Linq;
using RPG25D.Core.Dice;
using RPG25D.Core.Skills;

namespace RPG25D.Core.Battle
{
    /// <summary>
    /// 순수 C# 전투 상태 머신 (BattleStateMachine)
    /// GUI나 MonoBehaviour 없이 턴 사이클, 행동 해결, 승리/패배 상태를 완벽히 통제합니다.
    /// </summary>
    public class BattleStateMachine
    {
        public BattleTurnState CurrentState { get; private set; } = BattleTurnState.None;
        public int TurnNumber { get; private set; } = 0;

        public BattleActor Player { get; }
        public List<BattleActor> Enemies { get; } = new List<BattleActor>();
        public DicePool DicePool { get; }

        public event Action<BattleTurnState> OnStateChanged;
        public event Action<string> OnBattleLog;

        public BattleStateMachine(BattleActor player, IEnumerable<BattleActor> enemies, int diceCount = 4, int? seed = null)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (enemies != null) Enemies.AddRange(enemies);
            DicePool = new DicePool(diceCount, seed);
        }

        public void StartBattle()
        {
            SetState(BattleTurnState.BattleStart);
            OnBattleLog?.Invoke($"전투가 시작되었습니다! 적 {Enemies.Count}명이 출현했습니다.");
            StartPlayerTurn();
        }

        public void StartPlayerTurn()
        {
            TurnNumber++;
            Player.ClearShield(); // 턴 시작 시 이전 턴 임시 실드 초기화 (선택적 룰)
            SetState(BattleTurnState.PlayerTurnStart);
            OnBattleLog?.Invoke($"--- 턴 {TurnNumber}: 플레이어 턴 시작 ---");

            // 턴 시작 즉시 주사위 굴림 단계로 진입
            BeginRollPhase();
        }

        public void BeginRollPhase()
        {
            SetState(BattleTurnState.RollPhase);
            DicePool.RollAll();
            string diceValues = string.Join(", ", DicePool.AllDice.Select(d => $"[{d.Value}]"));
            OnBattleLog?.Invoke($"주사위를 굴렸습니다! 결과: {diceValues}");

            // 굴림 후 슬롯 배치 단계로 진입
            SetState(BattleTurnState.AssignPhase);
        }

        public bool RerollDice(IEnumerable<int> dieIds)
        {
            if (CurrentState != BattleTurnState.AssignPhase && CurrentState != BattleTurnState.RollPhase)
            {
                return false;
            }

            bool rolled = DicePool.Reroll(dieIds);
            if (rolled)
            {
                string diceValues = string.Join(", ", DicePool.AllDice.Select(d => $"[{d.Value}]"));
                OnBattleLog?.Invoke($"주사위를 다시 굴렸습니다. 현재: {diceValues}");
            }
            return rolled;
        }

        /// <summary>
        /// 조건이 만족된 스킬들을 실행 단계로 넘겨 순차 해결합니다.
        /// </summary>
        public void ExecuteActions(IEnumerable<(SkillModel skill, List<Die> assignedDice, BattleActor target)> actions)
        {
            if (CurrentState != BattleTurnState.AssignPhase)
            {
                throw new InvalidOperationException($"현재 상태({CurrentState})에서는 스킬을 실행할 수 없습니다.");
            }

            SetState(BattleTurnState.ExecutePlayerActions);

            foreach (var (skill, assignedDice, target) in actions)
            {
                if (!skill.Condition.CanActivate(assignedDice, out string failureReason))
                {
                    OnBattleLog?.Invoke($"[{skill.Name}] 발동 실패: {failureReason}");
                    continue;
                }

                // 주사위 소모
                foreach (var die in assignedDice)
                {
                    DicePool.ConsumeDie(die.Id);
                }

                int power = skill.CalculatePower(assignedDice);

                switch (skill.Type)
                {
                    case SkillType.SingleAttack:
                        var singleTarget = target ?? Enemies.FirstOrDefault(e => e.IsAlive);
                        if (singleTarget != null)
                        {
                            singleTarget.TakeDamage(power);
                            OnBattleLog?.Invoke($"플레이어가 [{skill.Name}]을(를) 사용하여 {singleTarget.Name}에게 {power}의 피해를 입혔습니다! (남은 HP: {singleTarget.CurrentHP}/{singleTarget.MaxHP})");
                        }
                        break;

                    case SkillType.AoEAttack:
                        OnBattleLog?.Invoke($"플레이어가 [{skill.Name}]을(를) 발동하여 모든 적을 강타합니다! (위력: {power})");
                        foreach (var enemy in Enemies.Where(e => e.IsAlive).ToList())
                        {
                            enemy.TakeDamage(power);
                            OnBattleLog?.Invoke($"- {enemy.Name}에게 {power}의 광역 피해 (남은 HP: {enemy.CurrentHP}/{enemy.MaxHP})");
                        }
                        break;

                    case SkillType.Shield:
                        Player.AddShield(power);
                        OnBattleLog?.Invoke($"플레이어가 [{skill.Name}]을(를) 사용하여 실드 +{power}를 획득했습니다! (현재 실드: {Player.Shield})");
                        break;

                    case SkillType.Heal:
                        Player.Heal(power);
                        OnBattleLog?.Invoke($"플레이어가 [{skill.Name}]을(를) 사용하여 체력 +{power}를 회복했습니다! (현재 HP: {Player.CurrentHP}/{Player.MaxHP})");
                        break;
                }

                // 적 사망 여부 체크
                if (CheckBattleEnd()) return;
            }

            // 플레이어 행동 완료 후 적 턴으로 전환
            ExecuteEnemyTurn();
        }

        public void ExecuteEnemyTurn()
        {
            if (CheckBattleEnd()) return;

            SetState(BattleTurnState.EnemyTurn);
            OnBattleLog?.Invoke("--- 적 턴 시작 ---");

            var aliveEnemies = Enemies.Where(e => e.IsAlive).ToList();
            foreach (var enemy in aliveEnemies)
            {
                if (!Player.IsAlive) break;

                int damage = enemy.BaseAttack;
                Player.TakeDamage(damage);
                OnBattleLog?.Invoke($"{enemy.Name}의 공격! 플레이어에게 {damage}의 피해! (플레이어 HP: {Player.CurrentHP}/{Player.MaxHP}, 실드: {Player.Shield})");
            }

            if (CheckBattleEnd()) return;

            // 턴 종료 처리
            EndTurn();
        }

        public void EndTurn()
        {
            SetState(BattleTurnState.TurnEnd);
            OnBattleLog?.Invoke($"턴 {TurnNumber} 종료.");

            if (!CheckBattleEnd())
            {
                StartPlayerTurn();
            }
        }

        public bool CheckBattleEnd()
        {
            if (!Player.IsAlive)
            {
                SetState(BattleTurnState.Defeat);
                OnBattleLog?.Invoke("플레이어가 쓰러졌습니다... 전투 패배!");
                return true;
            }

            if (Enemies.All(e => !e.IsAlive))
            {
                SetState(BattleTurnState.Victory);
                OnBattleLog?.Invoke("모든 적을 격파했습니다! 전투 승리!");
                return true;
            }

            return false;
        }

        private void SetState(BattleTurnState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
