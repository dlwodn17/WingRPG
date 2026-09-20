using System;

namespace RPG25D.Core.Dice
{
    /// <summary>
    /// D20 주사위 굴림 판정 결과 열거형
    /// </summary>
    public enum D20Outcome
    {
        Fail,        // 1: 대실패 (Miss, 대미지 0)
        Hit,         // 2~19: 일반 적중 (기본 대미지 + 눈금 * 스케일링)
        InstantKill  // 20: 절대 성공 / 즉사 (남은 HP 무관 사망)
    }

    /// <summary>
    /// D20 주사위 굴림 결과 데이터 (순수 C#)
    /// </summary>
    public readonly struct D20RollResult
    {
        public int Value { get; }
        public D20Outcome Outcome { get; }
        public bool IsPhysicalRoll { get; }

        public D20RollResult(int value, D20Outcome outcome, bool isPhysicalRoll = false)
        {
            Value = value;
            Outcome = outcome;
            IsPhysicalRoll = isPhysicalRoll;
        }

        public override string ToString()
        {
            switch (Outcome)
            {
                case D20Outcome.Fail:
                    return $"[D20: {Value}] ❌ 대실패 (Miss!)";
                case D20Outcome.InstantKill:
                    return $"[D20: {Value}] 💀 절대 성공 / 즉사 (Instant Kill!)";
                default:
                    return $"[D20: {Value}] ⚔️ 일반 적중 (Hit)";
            }
        }
    }

    /// <summary>
    /// [요구 산출물 1] D20DiceRoller.cs (리팩토링)
    /// 순수 C# 기반 1~20 난수 생성기이자, 물리 시뮬레이션 결과(DicePhysicsVisualizer)의 값을 수신하여
    /// 판정(대실패 1, 일반 적중 2~19, 즉사 20)을 확정하는 코어 룰 클래스입니다.
    /// </summary>
    public class D20DiceRoller
    {
        private readonly Random _random;

        public D20DiceRoller(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// 소프트웨어 난수로 D20 주사위를 굴립니다 (헤드리스/CLI 환경 및 기본 모드)
        /// </summary>
        public D20RollResult Roll()
        {
            int val = _random.Next(1, 21); // 1 ~ 20
            return Evaluate(val, isPhysicalRoll: false);
        }

        /// <summary>
        /// 물리 시뮬레이터(DicePhysicsVisualizer)에서 판독된 물리 눈금을 전달받아 최종 결과를 확정합니다.
        /// </summary>
        public D20RollResult ProcessPhysicsResult(int physicalDiceValue)
        {
            return Evaluate(physicalDiceValue, isPhysicalRoll: true);
        }

        /// <summary>
        /// D20 눈금을 판정(Fail/Hit/InstantKill)으로 변환하는 평가 메서드
        /// </summary>
        public static D20RollResult Evaluate(int diceValue, bool isPhysicalRoll = false)
        {
            if (diceValue < 1 || diceValue > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(diceValue), "D20 눈금은 1부터 20 사이여야 합니다.");
            }

            if (diceValue == 1)
            {
                return new D20RollResult(diceValue, D20Outcome.Fail, isPhysicalRoll);
            }

            if (diceValue == 20)
            {
                return new D20RollResult(diceValue, D20Outcome.InstantKill, isPhysicalRoll);
            }

            return new D20RollResult(diceValue, D20Outcome.Hit, isPhysicalRoll);
        }
    }
}
