using System;
using System.Collections.Generic;
using System.Linq;

namespace RPG25D.Core.Dice
{
    /// <summary>
    /// 주사위 풀 도메인 모델 (순수 C#)
    /// 주사위 생성, 굴리기, 리롤, 사용 가능한 주사위 조회 로직을 담당합니다.
    /// </summary>
    public class DicePool
    {
        private readonly List<Die> _dice = new List<Die>();
        private readonly Random _random;

        public IReadOnlyList<Die> AllDice => _dice;
        public IEnumerable<Die> AvailableDice => _dice.Where(d => !d.IsConsumed && !d.IsLocked);

        public int DefaultCount { get; }

        public DicePool(int diceCount = 4, int? seed = null)
        {
            DefaultCount = diceCount;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

            for (int i = 0; i < diceCount; i++)
            {
                _dice.Add(new Die(i + 1, 1));
            }
        }

        /// <summary>
        /// 새로운 턴을 시작할 때 주사위 상태를 리셋하고 전체 굴림을 수행합니다.
        /// </summary>
        public void RollAll()
        {
            foreach (var die in _dice)
            {
                die.ResetTurn();
                die.Roll(_random);
            }
        }

        /// <summary>
        /// 특정 주사위들을 지정하여 다시 굴립니다 (Reroll).
        /// </summary>
        public bool Reroll(IEnumerable<int> dieIds)
        {
            bool rolledAny = false;
            foreach (var id in dieIds)
            {
                var die = _dice.FirstOrDefault(d => d.Id == id);
                if (die != null && !die.IsConsumed && !die.IsLocked)
                {
                    die.Roll(_random);
                    rolledAny = true;
                }
            }
            return rolledAny;
        }

        public Die GetDieById(int id) => _dice.FirstOrDefault(d => d.Id == id);

        public void ConsumeDie(int id)
        {
            var die = GetDieById(id);
            if (die != null)
            {
                die.IsConsumed = true;
            }
        }
    }
}
