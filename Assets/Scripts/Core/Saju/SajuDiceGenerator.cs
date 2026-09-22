using System;
using UnityEngine;

namespace RPG25D.Core.Saju
{
    /// <summary>
    /// [요구 산출물 2] 8회 주사위 사주 생성 결과 상세 데이터
    /// 천간 D10 4회 눈금 및 지지 D12 4회 눈금을 보관합니다.
    /// </summary>
    public struct SajuDiceRollResult
    {
        // 천간 D10 눈금 (1 ~ 10)
        public int YearStemDice;
        public int MonthStemDice;
        public int DayStemDice;
        public int HourStemDice;

        // 지지 D12 눈금 (1 ~ 12)
        public int YearBranchDice;
        public int MonthBranchDice;
        public int DayBranchDice;
        public int HourBranchDice;

        public CharacterSaju Saju;

        public int[] GetAllStemDice() => new[] { YearStemDice, MonthStemDice, DayStemDice, HourStemDice };
        public int[] GetAllBranchDice() => new[] { YearBranchDice, MonthBranchDice, DayBranchDice, HourBranchDice };
        public int[] GetAllEightDice() => new[]
        {
            YearStemDice, MonthStemDice, DayStemDice, HourStemDice,
            YearBranchDice, MonthBranchDice, DayBranchDice, HourBranchDice
        };
    }

    /// <summary>
    /// [요구 산출물 2] 8회 주사위 사주 생성기 (SajuDiceGenerator.cs)
    /// 천간(10간) D10 4회 + 지지(12지) D12 4회 총 8회 주사위를 굴려
    /// 캐릭터의 영구적인 사주팔자(CharacterSaju)를 결정 및 발급합니다.
    /// </summary>
    public class SajuDiceGenerator
    {
        private readonly System.Random _random;

        public SajuDiceRollResult LastRollResult { get; private set; }

        public SajuDiceGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// D10 주사위(1~10) 1회 굴림
        /// </summary>
        public int RollD10()
        {
            return _random.Next(1, 11);
        }

        /// <summary>
        /// D12 주사위(1~12) 1회 굴림
        /// </summary>
        public int RollD12()
        {
            return _random.Next(1, 13);
        }

        /// <summary>
        /// [세부 개발 명세 2] 총 8회의 주사위(D10 4회 + D12 4회)를 굴려 완성된 CharacterSaju를 반환합니다.
        /// </summary>
        public CharacterSaju RollSaju()
        {
            var roll = RollSajuDetailed();
            return roll.Saju;
        }

        /// <summary>
        /// 주사위 눈금 상세 결과와 함께 사주를 생성합니다.
        /// </summary>
        public SajuDiceRollResult RollSajuDetailed()
        {
            // 1. 천간 D10 4회 굴림 (년, 월, 일, 시)
            int yearStem = RollD10();
            int monthStem = RollD10();
            int dayStem = RollD10();
            int hourStem = RollD10();

            // 2. 지지 D12 4회 굴림 (년, 월, 일, 시)
            int yearBranch = RollD12();
            int monthBranch = RollD12();
            int dayBranch = RollD12();
            int hourBranch = RollD12();

            // 3. 4개 기둥(사주) 조합
            var yearPillar = new SajuPillar((HeavenlyStem)yearStem, (EarthlyBranch)yearBranch);
            var monthPillar = new SajuPillar((HeavenlyStem)monthStem, (EarthlyBranch)monthBranch);
            var dayPillar = new SajuPillar((HeavenlyStem)dayStem, (EarthlyBranch)dayBranch);
            var hourPillar = new SajuPillar((HeavenlyStem)hourStem, (EarthlyBranch)hourBranch);

            var saju = new CharacterSaju(yearPillar, monthPillar, dayPillar, hourPillar);

            var result = new SajuDiceRollResult
            {
                YearStemDice = yearStem,
                MonthStemDice = monthStem,
                DayStemDice = dayStem,
                HourStemDice = hourStem,
                YearBranchDice = yearBranch,
                MonthBranchDice = monthBranch,
                DayBranchDice = dayBranch,
                HourBranchDice = hourBranch,
                Saju = saju
            };

            LastRollResult = result;
            return result;
        }

        /// <summary>
        /// 특정 지정 주사위 눈금으로 사주를 생성합니다 (단위 테스트 및 결정론적 검증 지원).
        /// </summary>
        public static CharacterSaju CreateFromDiceValues(int[] stems, int[] branches)
        {
            if (stems == null || stems.Length != 4)
                throw new ArgumentException("stems 배열은 정확히 4개의 요소를 가져야 합니다 (년, 월, 일, 시).");
            if (branches == null || branches.Length != 4)
                throw new ArgumentException("branches 배열은 정확히 4개의 요소를 가져야 합니다 (년, 월, 일, 시).");

            for (int i = 0; i < 4; i++)
            {
                if (stems[i] < 1 || stems[i] > 10)
                    throw new ArgumentOutOfRangeException($"stems[{i}]는 1~10 범위여야 합니다. (값: {stems[i]})");
                if (branches[i] < 1 || branches[i] > 12)
                    throw new ArgumentOutOfRangeException($"branches[{i}]는 1~12 범위여야 합니다. (값: {branches[i]})");
            }

            var yearPillar = new SajuPillar((HeavenlyStem)stems[0], (EarthlyBranch)branches[0]);
            var monthPillar = new SajuPillar((HeavenlyStem)stems[1], (EarthlyBranch)branches[1]);
            var dayPillar = new SajuPillar((HeavenlyStem)stems[2], (EarthlyBranch)branches[2]);
            var hourPillar = new SajuPillar((HeavenlyStem)stems[3], (EarthlyBranch)branches[3]);

            return new CharacterSaju(yearPillar, monthPillar, dayPillar, hourPillar);
        }
    }
}
