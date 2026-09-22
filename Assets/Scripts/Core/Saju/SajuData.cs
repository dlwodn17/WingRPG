using System;
using UnityEngine;

namespace RPG25D.Core.Saju
{
    /// <summary>
    /// [요구 산출물 1] 천간 (10간, Heavenly Stem)
    /// 갑(1), 을(2), 병(3), 정(4), 무(5), 기(6), 경(7), 신(8), 임(9), 계(10)
    /// </summary>
    public enum HeavenlyStem
    {
        Gap = 1,      // 갑 (甲) - 양목
        Eul = 2,      // 을 (乙) - 음목
        Byeong = 3,   // 병 (丙) - 양화
        Jeong = 4,    // 정 (丁) - 음화
        Mu = 5,       // 무 (戊) - 양토
        Gi = 6,       // 기 (己) - 음토
        Gyeong = 7,   // 경 (庚) - 양금
        Sin = 8,      // 신 (辛) - 음금
        Im = 9,       // 임 (壬) - 양수
        Gye = 10      // 계 (癸) - 음수
    }

    /// <summary>
    /// [요구 산출물 1] 지지 (12지, Earthly Branch)
    /// 자(1), 축(2), 인(3), 묘(4), 진(5), 사(6), 오(7), 미(8), 신(9), 유(10), 술(11), 해(12)
    /// </summary>
    public enum EarthlyBranch
    {
        Ja = 1,       // 자 (子) - 쥐
        Chuk = 2,     // 축 (丑) - 소
        In = 3,       // 인 (寅) - 호랑이
        Myo = 4,      // 묘 (卯) - 토끼
        Jin = 5,      // 진 (辰) - 용
        Sa = 6,       // 사 (巳) - 뱀
        O = 7,        // 오 (午) - 말
        Mi = 8,       // 미 (未) - 양
        Sin_B = 9,    // 신 (申) - 원숭이 (HeavenlyStem.Sin과의 명칭 충돌 방지)
        Yu = 10,      // 유 (酉) - 닭
        Sul = 11,     // 술 (戌) - 개
        Hae = 12      // 해 (亥) - 돼지
    }

    /// <summary>
    /// 천간 및 지지 한글/한자 변환 헬퍼 확장 메서드
    /// </summary>
    public static class SajuExtensions
    {
        public static string ToKorean(this HeavenlyStem stem)
        {
            return stem switch
            {
                HeavenlyStem.Gap => "갑",
                HeavenlyStem.Eul => "을",
                HeavenlyStem.Byeong => "병",
                HeavenlyStem.Jeong => "정",
                HeavenlyStem.Mu => "무",
                HeavenlyStem.Gi => "기",
                HeavenlyStem.Gyeong => "경",
                HeavenlyStem.Sin => "신",
                HeavenlyStem.Im => "임",
                HeavenlyStem.Gye => "계",
                _ => "?"
            };
        }

        public static string ToHanja(this HeavenlyStem stem)
        {
            return stem switch
            {
                HeavenlyStem.Gap => "甲",
                HeavenlyStem.Eul => "乙",
                HeavenlyStem.Byeong => "丙",
                HeavenlyStem.Jeong => "丁",
                HeavenlyStem.Mu => "戊",
                HeavenlyStem.Gi => "己",
                HeavenlyStem.Gyeong => "庚",
                HeavenlyStem.Sin => "辛",
                HeavenlyStem.Im => "壬",
                HeavenlyStem.Gye => "癸",
                _ => "?"
            };
        }

        public static string ToKorean(this EarthlyBranch branch)
        {
            return branch switch
            {
                EarthlyBranch.Ja => "자",
                EarthlyBranch.Chuk => "축",
                EarthlyBranch.In => "인",
                EarthlyBranch.Myo => "묘",
                EarthlyBranch.Jin => "진",
                EarthlyBranch.Sa => "사",
                EarthlyBranch.O => "오",
                EarthlyBranch.Mi => "미",
                EarthlyBranch.Sin_B => "신",
                EarthlyBranch.Yu => "유",
                EarthlyBranch.Sul => "술",
                EarthlyBranch.Hae => "해",
                _ => "?"
            };
        }

        public static string ToHanja(this EarthlyBranch branch)
        {
            return branch switch
            {
                EarthlyBranch.Ja => "子",
                EarthlyBranch.Chuk => "丑",
                EarthlyBranch.In => "寅",
                EarthlyBranch.Myo => "卯",
                EarthlyBranch.Jin => "辰",
                EarthlyBranch.Sa => "巳",
                EarthlyBranch.O => "午",
                EarthlyBranch.Mi => "未",
                EarthlyBranch.Sin_B => "申",
                EarthlyBranch.Yu => "酉",
                EarthlyBranch.Sul => "戌",
                EarthlyBranch.Hae => "亥",
                _ => "?"
            };
        }
    }

    /// <summary>
    /// [요구 산출물 1] 사주 기둥 구조체 (SajuPillar)
    /// 천간(Stem)과 지지(Branch) 한 쌍으로 구성되는 사주 1기둥입니다.
    /// </summary>
    [Serializable]
    public struct SajuPillar : IEquatable<SajuPillar>
    {
        public HeavenlyStem Stem;
        public EarthlyBranch Branch;

        public SajuPillar(HeavenlyStem stem, EarthlyBranch branch)
        {
            Stem = stem;
            Branch = branch;
        }

        /// <summary>
        /// 한글 표현 (예: "갑자", "병인")
        /// </summary>
        public string ToKoreanString()
        {
            return $"{Stem.ToKorean()}{Branch.ToKorean()}";
        }

        /// <summary>
        /// 한자 표현 (예: "甲子", "丙寅")
        /// </summary>
        public string ToHanjaString()
        {
            return $"{Stem.ToHanja()}{Branch.ToHanja()}";
        }

        public override string ToString() => ToKoreanString();

        public bool Equals(SajuPillar other)
        {
            return Stem == other.Stem && Branch == other.Branch;
        }

        public override bool Equals(object obj)
        {
            return obj is SajuPillar other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Stem, (int)Branch);
        }

        public static bool operator ==(SajuPillar left, SajuPillar right) => left.Equals(right);
        public static bool operator !=(SajuPillar left, SajuPillar right) => !left.Equals(right);
    }

    /// <summary>
    /// [요구 산출물 1] 캐릭터 사주팔자 명식 데이터 (CharacterSaju)
    /// 년주(YearPillar), 월주(MonthPillar), 일주(DayPillar), 시주(HourPillar) 총 4개 기둥(8글자)을 직렬화하여 관리합니다.
    /// </summary>
    [Serializable]
    public class CharacterSaju
    {
        [Tooltip("년주 (태어난 해)")]
        public SajuPillar YearPillar;

        [Tooltip("월주 (태어난 달)")]
        public SajuPillar MonthPillar;

        [Tooltip("일주 (태어난 날, 본원/일간)")]
        public SajuPillar DayPillar;

        [Tooltip("시주 (태어난 시)")]
        public SajuPillar HourPillar;

        public CharacterSaju() { }

        public CharacterSaju(SajuPillar year, SajuPillar month, SajuPillar day, SajuPillar hour)
        {
            YearPillar = year;
            MonthPillar = month;
            DayPillar = day;
            HourPillar = hour;
        }

        /// <summary>
        /// 사주팔자 8글자 한글 문자열 (예: "갑자병인무진경오")
        /// </summary>
        public string ToEightCharacters()
        {
            return $"{YearPillar.ToKoreanString()}{MonthPillar.ToKoreanString()}{DayPillar.ToKoreanString()}{HourPillar.ToKoreanString()}";
        }

        /// <summary>
        /// 사주팔자 8글자 한자 문자열 (예: "甲子丙寅戊辰庚午")
        /// </summary>
        public string ToEightHanjaCharacters()
        {
            return $"{YearPillar.ToHanjaString()}{MonthPillar.ToHanjaString()}{DayPillar.ToHanjaString()}{HourPillar.ToHanjaString()}";
        }

        /// <summary>
        /// 상세 한글 명식 표현 (예: "갑자년 병인월 무진일 경오시")
        /// </summary>
        public string ToFullKoreanString()
        {
            return $"{YearPillar.ToKoreanString()}년 {MonthPillar.ToKoreanString()}월 {DayPillar.ToKoreanString()}일 {HourPillar.ToKoreanString()}시";
        }

        /// <summary>
        /// 한글+한자 혼용 상세 명식 표현 (예: "갑자(甲子)년 병인(丙寅)월 무진(戊辰)일 경오(庚午)시")
        /// </summary>
        public string ToDetailedString()
        {
            return $"{YearPillar.ToKoreanString()}({YearPillar.ToHanjaString()})년 " +
                   $"{MonthPillar.ToKoreanString()}({MonthPillar.ToHanjaString()})월 " +
                   $"{DayPillar.ToKoreanString()}({DayPillar.ToHanjaString()})일 " +
                   $"{HourPillar.ToKoreanString()}({HourPillar.ToHanjaString()})시";
        }

        public override string ToString() => ToFullKoreanString();
    }
}
