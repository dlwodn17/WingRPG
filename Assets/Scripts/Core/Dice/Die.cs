using System;

namespace RPG25D.Core.Dice
{
    /// <summary>
    /// 단일 주사위 모델 (순수 C# POCO)
    /// 1~6 눈금을 가지며, 슬롯 할당 및 소모 상태를 추적합니다.
    /// </summary>
    [Serializable]
    public class Die
    {
        public int Id { get; }
        public int Value { get; private set; }
        public bool IsLocked { get; set; }
        public bool IsConsumed { get; set; }

        public Die(int id, int initialValue = 1)
        {
            Id = id;
            SetValue(initialValue);
            IsLocked = false;
            IsConsumed = false;
        }

        public void SetValue(int value)
        {
            if (value < 1 || value > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "주사위 눈금은 1부터 6 사이여야 합니다.");
            }
            Value = value;
        }

        public void Roll(Random random)
        {
            if (IsLocked || IsConsumed) return;
            Value = random.Next(1, 7);
        }

        public void ResetTurn()
        {
            IsConsumed = false;
            IsLocked = false;
        }
    }
}
