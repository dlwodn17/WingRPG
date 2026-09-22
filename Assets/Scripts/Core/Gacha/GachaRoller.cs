using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Saju;
using RPG25D.Data;

namespace RPG25D.Core.Gacha
{
    /// <summary>
    /// [요구 산출물 4] 가챠 확률 엔진 및 사주팔자 각인 소환기 (GachaRoller.cs)
    /// - 3성(85%), 4성(12%), 5성(3%)의 기본 가챠 확률 적용.
    /// - 80회 연속 5성 미등장 시 다음 1회 5성 확정 지급(하드 천장).
    /// - 소환 시 SajuDiceGenerator(D10 4회 + D12 4회)를 호출하여 고유 사주팔자를 영구 각인.
    /// - 콘솔(Debug.Log)에 소환 결과(이름, 등급, 사주팔자 8글자)를 실시간 출력.
    /// </summary>
    public class GachaRoller
    {
        // 등급 확률 상수 (백분율)
        public const float Rarity3Prob = 85.0f; // 3성: 85%
        public const float Rarity4Prob = 12.0f; // 4성: 12%
        public const float Rarity5Prob = 3.0f;  // 5성: 3%

        // 하드 천장 기준 횟수 (80회 미등장 시 다음 1회 5성 확정)
        public const int HardPityLimit = 80;

        private readonly System.Random _random;
        private readonly SajuDiceGenerator _sajuGenerator;

        // 등급별 캐릭터 풀
        private readonly Dictionary<int, List<CharacterBaseData>> _poolsByRarity = new Dictionary<int, List<CharacterBaseData>>();

        /// <summary>
        /// 5성 미등장 연속 횟수 (천장 카운터)
        /// </summary>
        public int PityCounter { get; set; } = 0;

        /// <summary>
        /// 총 소환 횟수
        /// </summary>
        public int TotalRollCount { get; private set; } = 0;

        public SajuDiceGenerator SajuGenerator => _sajuGenerator;

        public GachaRoller(List<CharacterBaseData> characterPool = null, int? randomSeed = null, SajuDiceGenerator diceGen = null)
        {
            _random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
            _sajuGenerator = diceGen ?? new SajuDiceGenerator(randomSeed);

            InitializePool(characterPool);
        }

        /// <summary>
        /// 캐릭터 풀 초기화 및 등급별 분류
        /// </summary>
        private void InitializePool(List<CharacterBaseData> characterPool)
        {
            _poolsByRarity[3] = new List<CharacterBaseData>();
            _poolsByRarity[4] = new List<CharacterBaseData>();
            _poolsByRarity[5] = new List<CharacterBaseData>();

            if (characterPool != null && characterPool.Count > 0)
            {
                foreach (var charData in characterPool)
                {
                    if (charData == null) continue;
                    int r = Mathf.Clamp(charData.BaseRarity, 3, 5);
                    _poolsByRarity[r].Add(charData);
                }
            }

            // 각 등급별 최소 1개 이상의 기본 폴백 캐릭터 보장
            EnsureFallbackCharacters();
        }

        private void EnsureFallbackCharacters()
        {
            if (_poolsByRarity[3].Count == 0)
            {
                _poolsByRarity[3].Add(CharacterBaseData.Create("CHAR_3S_01", "방랑 견습생", 3, baseAttack: 20));
                _poolsByRarity[3].Add(CharacterBaseData.Create("CHAR_3S_02", "초보 도적", 3, baseAttack: 22));
            }
            if (_poolsByRarity[4].Count == 0)
            {
                _poolsByRarity[4].Add(CharacterBaseData.Create("CHAR_4S_01", "왕실 근위병", 4, baseAttack: 35));
                _poolsByRarity[4].Add(CharacterBaseData.Create("CHAR_4S_02", "신비의 술사", 4, baseAttack: 38));
            }
            if (_poolsByRarity[5].Count == 0)
            {
                _poolsByRarity[5].Add(CharacterBaseData.Create("CHAR_5S_01", "청룡의 무사", 5, baseAttack: 55));
                _poolsByRarity[5].Add(CharacterBaseData.Create("CHAR_5S_02", "태양의 현자", 5, baseAttack: 58));
            }
        }

        /// <summary>
        /// [세부 개발 명세 4] 단일 소환 실행
        /// 1. 등급 및 캐릭터 추첨 (천장 로직 적용)
        /// 2. SajuDiceGenerator.RollSaju() 호출로 고유 사주팔자 확정
        /// 3. CharacterInstance 생성 및 반환
        /// 4. 콘솔에 캐릭터 이름, 등급, 사주팔자 8글자 출력
        /// </summary>
        public CharacterInstance RollGacha(int? forcedRarity = null)
        {
            TotalRollCount++;
            int chosenRarity;
            bool isPityTriggered = false;

            if (forcedRarity.HasValue)
            {
                chosenRarity = Mathf.Clamp(forcedRarity.Value, 3, 5);
            }
            else
            {
                // [천장 로직]: 80회 연속 5성이 안 나왔을 경우(PityCounter >= 80), 다음 1회 5성 확정 지급
                if (PityCounter >= HardPityLimit)
                {
                    chosenRarity = 5;
                    isPityTriggered = true;
                }
                else
                {
                    // 일반 확률 추첨: 0.0 ~ 100.0
                    double rollVal = _random.NextDouble() * 100.0;
                    if (rollVal < Rarity5Prob) // 0.0 ~ 3.0 (3%)
                    {
                        chosenRarity = 5;
                    }
                    else if (rollVal < Rarity5Prob + Rarity4Prob) // 3.0 ~ 15.0 (12%)
                    {
                        chosenRarity = 4;
                    }
                    else // 15.0 ~ 100.0 (85%)
                    {
                        chosenRarity = 3;
                    }
                }
            }

            // 천장 카운터 갱신
            if (chosenRarity == 5)
            {
                PityCounter = 0; // 5성 획득 시 리셋
            }
            else
            {
                PityCounter++; // 미등장 시 카운터 증가
            }

            // 1. 해당 등급 캐릭터 풀에서 무작위 1종 추첨
            var targetPool = _poolsByRarity[chosenRarity];
            var baseData = targetPool[_random.Next(0, targetPool.Count)];

            // 2. 8회 주사위 굴려 사주팔자 명식 확정
            var saju = _sajuGenerator.RollSaju();

            // 3. 사주가 각인된 캐릭터 인스턴스 생성
            var instance = new CharacterInstance(baseData, saju);

            // 4. 콘솔 로그 출력 (이름, 등급, 사주팔자 8글자)
            string pityTag = isPityTriggered ? " [⭐천장 5성 확정!]" : "";
            Debug.Log($"🔮 [가챠 소환 성공{pityTag}] 이름: {instance.CharacterName} | " +
                      $"등급: {instance.Rarity}성 | " +
                      $"사주팔자: {instance.Saju.ToEightCharacters()} ({instance.Saju.ToFullKoreanString()}) | " +
                      $"천장 카운터: {PityCounter}/{HardPityLimit}");

            return instance;
        }

        /// <summary>
        /// 10연속 소환 편의 메서드
        /// </summary>
        public List<CharacterInstance> RollGachaTen()
        {
            var list = new List<CharacterInstance>(10);
            for (int i = 0; i < 10; i++)
            {
                list.Add(RollGacha());
            }
            return list;
        }
    }
}
