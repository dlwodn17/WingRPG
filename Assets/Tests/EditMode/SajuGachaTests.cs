using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Gacha;
using RPG25D.Core.Saju;
using RPG25D.Data;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 5] SajuGachaTests.cs
    /// - SajuDiceGenerator 8회 주사위 눈금(D10 4회 + D12 4회) 범위 검증
    /// - SajuData 천간/지지 및 사주팔자 8글자 한글/한자 변환 검증
    /// - CharacterInstance 고유 GUID 및 사주 데이터 바인딩 검증
    /// - GachaRoller 80회 하드 천장(5성 확정) 및 확률 엔진 검증
    /// </summary>
    [TestFixture]
    public class SajuGachaTests
    {
        #region 1. SajuData 천간·지지 및 사주 모델 검증

        [Test]
        public void SajuData_EnumAndPillar_ConvertsAccuratelyToKoreanAndHanja()
        {
            // 갑자 (甲子)
            var p1 = new SajuPillar(HeavenlyStem.Gap, EarthlyBranch.Ja);
            Assert.AreEqual("갑자", p1.ToKoreanString());
            Assert.AreEqual("甲子", p1.ToHanjaString());

            // 병인 (丙寅)
            var p2 = new SajuPillar(HeavenlyStem.Byeong, EarthlyBranch.In);
            Assert.AreEqual("병인", p2.ToKoreanString());
            Assert.AreEqual("丙寅", p2.ToHanjaString());

            // 무진 (戊辰)
            var p3 = new SajuPillar(HeavenlyStem.Mu, EarthlyBranch.Jin);
            Assert.AreEqual("무진", p3.ToKoreanString());
            Assert.AreEqual("戊辰", p3.ToHanjaString());

            // 경오 (庚午)
            var p4 = new SajuPillar(HeavenlyStem.Gyeong, EarthlyBranch.O);
            Assert.AreEqual("경오", p4.ToKoreanString());
            Assert.AreEqual("庚午", p4.ToHanjaString());

            // 사주팔자 4개 기둥 결합
            var saju = new CharacterSaju(p1, p2, p3, p4);

            Assert.AreEqual("갑자병인무진경오", saju.ToEightCharacters(), "사주 8글자가 정확히 조합되어야 합니다.");
            Assert.AreEqual("甲子丙寅戊辰庚午", saju.ToEightHanjaCharacters(), "사주 8글자 한자가 정확히 조합되어야 합니다.");
            Assert.AreEqual("갑자년 병인월 무진일 경오시", saju.ToFullKoreanString(), "상세 한글 명식이 일치해야 합니다.");
        }

        [Test]
        public void SajuData_AllStemsAndBranches_HaveUniqueKoreanMappings()
        {
            var stems = System.Enum.GetValues(typeof(HeavenlyStem)).Cast<HeavenlyStem>().ToList();
            Assert.AreEqual(10, stems.Count, "천간은 정확히 10개여야 합니다.");
            var stemKoreans = stems.Select(s => s.ToKorean()).Distinct().ToList();
            Assert.AreEqual(10, stemKoreans.Count, "천간 10개의 한글 표기는 모두 중복 없이 고유해야 합니다.");

            var branches = System.Enum.GetValues(typeof(EarthlyBranch)).Cast<EarthlyBranch>().ToList();
            Assert.AreEqual(12, branches.Count, "지지는 정확히 12개여야 합니다.");
            var branchKoreans = branches.Select(b => b.ToKorean()).Distinct().ToList();
            Assert.AreEqual(12, branchKoreans.Count, "지지 12개의 한글 표기는 모두 중복 없이 고유해야 합니다.");
        }

        #endregion

        #region 2. SajuDiceGenerator 8회 주사위 사주 생성기 검증

        [Test]
        public void SajuDiceGenerator_RollSaju_AllEightDiceStayWithinValidRanges()
        {
            var generator = new SajuDiceGenerator(42);

            // 100회 반복 롤링 시 모든 주사위 눈금이 유효 범위 내에 있는지 전수 검사
            for (int i = 0; i < 100; i++)
            {
                var roll = generator.RollSajuDetailed();

                // 천간 D10 4개 눈금 (1 ~ 10)
                Assert.That(roll.YearStemDice, Is.InRange(1, 10));
                Assert.That(roll.MonthStemDice, Is.InRange(1, 10));
                Assert.That(roll.DayStemDice, Is.InRange(1, 10));
                Assert.That(roll.HourStemDice, Is.InRange(1, 10));

                // 지지 D12 4개 눈금 (1 ~ 12)
                Assert.That(roll.YearBranchDice, Is.InRange(1, 12));
                Assert.That(roll.MonthBranchDice, Is.InRange(1, 12));
                Assert.That(roll.DayBranchDice, Is.InRange(1, 12));
                Assert.That(roll.HourBranchDice, Is.InRange(1, 12));

                // 완성된 사주 객체 유효성
                Assert.IsNotNull(roll.Saju);
                Assert.AreEqual(8, roll.Saju.ToEightCharacters().Length, "사주팔자는 정확히 8글자여야 합니다.");
            }
        }

        [Test]
        public void SajuDiceGenerator_CreateFromDiceValues_BuildsExactExpectedSaju()
        {
            // 천간: 1(갑), 3(병), 5(무), 7(경)
            // 지지: 1(자), 3(인), 5(진), 7(오)
            int[] stems = { 1, 3, 5, 7 };
            int[] branches = { 1, 3, 5, 7 };

            var saju = SajuDiceGenerator.CreateFromDiceValues(stems, branches);

            Assert.AreEqual("갑자병인무진경오", saju.ToEightCharacters());
            Assert.AreEqual("甲子丙寅戊辰庚午", saju.ToEightHanjaCharacters());
        }

        #endregion

        #region 3. CharacterInstance 고유성 및 사주 각인 검증

        [Test]
        public void CharacterInstance_GeneratesDistinctGUIDsAndBindsSajuCorrectly()
        {
            var baseData = CharacterBaseData.Create("HERO_CHUN", "청룡의 무사", 5, 60, 150, 12);
            var generator = new SajuDiceGenerator(100);

            var instances = new List<CharacterInstance>();
            for (int i = 0; i < 20; i++)
            {
                var saju = generator.RollSaju();
                var instance = new CharacterInstance(baseData, saju);
                instances.Add(instance);
            }

            // 1. 모든 인스턴스의 GUID가 고유한지 검증
            var distinctGuids = instances.Select(inst => inst.InstanceID).Distinct().ToList();
            Assert.AreEqual(20, distinctGuids.Count, "모든 캐릭터 인스턴스의 GUID는 고유해야 합니다.");

            // 2. 각 인스턴스에 사주 객체가 바인딩되어 있는지 검증
            foreach (var inst in instances)
            {
                Assert.IsNotNull(inst.Saju);
                Assert.AreEqual("HERO_CHUN", inst.BaseDataID);
                Assert.AreEqual(5, inst.Rarity);
                Assert.AreEqual(8, inst.Saju.ToEightCharacters().Length);
            }

            // 메모리 해제
            Object.DestroyImmediate(baseData);
        }

        #endregion

        #region 4. GachaRoller 80회 하드 천장 및 확률 엔진 검증

        [Test]
        public void GachaRoller_PityCounter_IncreasesOnNon5Star()
        {
            var roller = new GachaRoller(randomSeed: 777);
            Assert.AreEqual(0, roller.PityCounter);

            // 강제 3성 5회 롤
            for (int i = 0; i < 5; i++)
            {
                roller.RollGacha(forcedRarity: 3);
            }

            Assert.AreEqual(5, roller.PityCounter, "비-5성 소환 시 천장 카운터가 1씩 증가해야 합니다.");
        }

        [Test]
        public void GachaRoller_Natural5Star_ResetsPityCounterToZero()
        {
            var roller = new GachaRoller();
            roller.PityCounter = 45; // 45회 미등장 상태

            // 5성 소환 강제
            var char5 = roller.RollGacha(forcedRarity: 5);

            Assert.AreEqual(5, char5.Rarity);
            Assert.AreEqual(0, roller.PityCounter, "5성 소환 성공 시 천장 카운터는 0으로 리셋되어야 합니다.");
        }

        [Test]
        public void GachaRoller_HardPity_Guarantees5StarAfter80Failures()
        {
            var roller = new GachaRoller(randomSeed: 1234);

            // 80회 연속 3성 강제 소환 -> PityCounter가 80에 도달
            for (int i = 0; i < 80; i++)
            {
                roller.RollGacha(forcedRarity: 3);
            }
            Assert.AreEqual(80, roller.PityCounter, "80회 연속 5성 미등장 시 카운터는 80이어야 합니다.");

            // 81번째 소환: 하드 천장 발동으로 확률과 관계없이 무조건 5성 확정 지급
            var pityPull = roller.RollGacha(); // 자연 롤링

            Assert.AreEqual(5, pityPull.Rarity, "80회 미등장 후 다음 1회는 하드 천장으로 5성이 확정 지급되어야 합니다.");
            Assert.AreEqual(0, roller.PityCounter, "천장 5성 지급 직후 카운터는 0으로 초기화되어야 합니다.");
        }

        [Test]
        public void GachaRoller_RollGachaTen_ProducesTenInstancesWithSaju()
        {
            var roller = new GachaRoller(randomSeed: 555);

            var results = roller.RollGachaTen();

            Assert.AreEqual(10, results.Count, "10연차 소환은 정확히 10개의 인스턴스를 반환해야 합니다.");
            foreach (var inst in results)
            {
                Assert.IsNotNull(inst);
                Assert.IsNotNull(inst.Saju);
                Assert.That(inst.Rarity, Is.InRange(3, 5));
                Assert.IsNotEmpty(inst.InstanceID);
            }
        }

        [Test]
        public void GachaRoller_Probabilities_DistributeCorrectlyOverLargeSamples()
        {
            // 5,000회 대규모 가챠 시뮬레이션
            var roller = new GachaRoller(randomSeed: 999);
            int count3 = 0;
            int count4 = 0;
            int count5 = 0;

            const int totalRolls = 5000;
            for (int i = 0; i < totalRolls; i++)
            {
                var inst = roller.RollGacha();
                if (inst.Rarity == 3) count3++;
                else if (inst.Rarity == 4) count4++;
                else if (inst.Rarity == 5) count5++;
            }

            double rate3 = (double)count3 / totalRolls * 100.0;
            double rate4 = (double)count4 / totalRolls * 100.0;
            double rate5 = (double)count5 / totalRolls * 100.0;

            Debug.Log($"[가챠 통계 검증 ({totalRolls}회)] 3성: {rate3:F2}% (목표 85%) | 4성: {rate4:F2}% (목표 12%) | 5성: {rate5:F2}% (목표 3% + 천장)");

            // 오차 범위 검증 (대수의 법칙: 3성은 80~88%, 4성은 9~15%, 5성은 2.5~6% (천장 포함))
            Assert.That(rate3, Is.InRange(78.0, 89.0), "3성 확률 분포가 대략 85% 근방이어야 합니다.");
            Assert.That(rate4, Is.InRange(9.0, 16.0), "4성 확률 분포가 대략 12% 근방이어야 합니다.");
            Assert.That(rate5, Is.InRange(2.0, 7.0), "5성 확률(천장 포함)이 유효 범위 내여야 합니다.");
        }

        #endregion
    }
}
