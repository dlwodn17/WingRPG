using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Dice;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// [요구 산출물 1] DiceManager.cs
    /// 주사위 풀 생성, 굴리기, 리롤(다시 굴리기) 로직을 관리하는 매니저 컴포넌트입니다.
    /// 순수 C# DicePool 모델을 래핑하여 유니티 라이프사이클 및 UI 이벤트와 연동합니다.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        [Header("주사위 설정")]
        [Range(1, 10)]
        [SerializeField] private int _defaultDiceCount = 4;
        [SerializeField] private int _maxRerollsPerTurn = 1;

        private DicePool _dicePool;
        private int _currentRerollCount = 0;

        public DicePool Pool => _dicePool;
        public int RemainingRerolls => Mathf.Max(0, _maxRerollsPerTurn - _currentRerollCount);
        public bool CanReroll => RemainingRerolls > 0;

        public event Action<IReadOnlyList<Die>> OnDiceRolled;
        public event Action<Die> OnDieChanged;
        public event Action<int> OnRerollsChanged;

        private void Awake()
        {
            InitializePool(_defaultDiceCount);
        }

        public void InitializePool(int count, int? seed = null)
        {
            _defaultDiceCount = count;
            _dicePool = new DicePool(_defaultDiceCount, seed);
            _currentRerollCount = 0;
        }

        /// <summary>
        /// 턴 시작 시 주사위 풀 전체를 리셋하고 새로 굴립니다.
        /// </summary>
        public void RollTurnDice()
        {
            _currentRerollCount = 0;
            _dicePool.RollAll();
            OnRerollsChanged?.Invoke(RemainingRerolls);
            OnDiceRolled?.Invoke(_dicePool.AllDice);
        }

        /// <summary>
        /// 선택된 주사위들을 리롤합니다.
        /// </summary>
        public bool RerollSelected(IEnumerable<int> dieIds)
        {
            if (!CanReroll)
            {
                Debug.LogWarning("[DiceManager] 이번 턴의 남은 리롤 횟수가 없습니다.");
                return false;
            }

            var ids = dieIds?.ToList();
            if (ids == null || ids.Count == 0) return false;

            bool rolled = _dicePool.Reroll(ids);
            if (rolled)
            {
                _currentRerollCount++;
                OnRerollsChanged?.Invoke(RemainingRerolls);
                OnDiceRolled?.Invoke(_dicePool.AllDice);
            }
            return rolled;
        }

        public Die GetDie(int id) => _dicePool?.GetDieById(id);

        public void ConsumeDie(int id)
        {
            _dicePool?.ConsumeDie(id);
            var die = GetDie(id);
            if (die != null)
            {
                OnDieChanged?.Invoke(die);
            }
        }

        public IEnumerable<Die> GetAvailableDice() => _dicePool?.AvailableDice ?? Enumerable.Empty<Die>();
    }
}
