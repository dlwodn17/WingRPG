using System;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Wings;
using RPG25D.Data;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// 적 타입 분류 (일반 몬스터, 엘리트, 보스)
    /// </summary>
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// 필드 상자 타입 분류 (일반, 희귀, 전설 상자)
    /// </summary>
    public enum ChestType
    {
        Normal,
        Rare,
        Legendary
    }

    /// <summary>
    /// [요구 산출물 4] WingDropManager.cs
    /// 날개 드롭 트리거 및 생성 엔진
    /// - 드롭 풀 관리: 등급별(3성 80%, 4성 18%, 5성 2%), 4슬롯 부위별 균등(25%씩) 생성
    /// - 트리거 연동 이벤트 핸들러:
    ///   1. OnEnemyDefeated: 적 처치 시 일정 확률(일반 30%, 보스 100%) 날개 드롭
    ///   2. OnDiceRolled: D20 판정 연동
    ///      - 눈금 1 (대실패): '불운의 위로' 보상으로 날개 1개 확정 획득
    ///      - 눈금 20 (절대 성공/즉사): '절대 행운' 보상으로 고등급(4~5성) 날개 1개 확정 획득
    ///   3. OnChestOpened: 필드 상자 개봉 시 테이블 기반 드롭
    /// - 드롭 발생 시 WingInventoryManager.AddWing() 자동 호출 및 콘솔 로그 출력
    /// </summary>
    public class WingDropManager
    {
        private static WingDropManager _instance;

        public static WingDropManager Instance
        {
            get => _instance ??= new WingDropManager();
            set => _instance = value;
        }

        // 기본 확률 테이블 (백분율)
        public const float Standard3StarProb = 80.0f; // 3성 80%
        public const float Standard4StarProb = 18.0f; // 4성 18%
        public const float Standard5StarProb = 2.0f;  // 5성 2%

        // 고등급 부스트 확률 테이블 (눈금 20 또는 보스 처치 등)
        public const float Boosted4StarProb = 80.0f;  // 4성 80%
        public const float Boosted5StarProb = 20.0f;  // 5성 20%

        private readonly System.Random _random;
        private readonly WingInventoryManager _inventory;

        public WingInventoryManager Inventory => _inventory;

        // 드롭 이벤트 (아이템, 드롭 발생 원인)
        public event Action<WingItemInstance, string> OnWingDropped;

        public WingDropManager(WingInventoryManager inventory = null, int? seed = null)
        {
            _inventory = inventory ?? WingInventoryManager.Instance;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// [세부 개발 명세 4] 확률 테이블 기반 날개 아이템 생성기
        /// </summary>
        public WingItemInstance GenerateWing(
            int? forcedRarity = null,
            WingSlotType? forcedSlot = null,
            bool isHighGradeBoosted = false)
        {
            // 1. 희귀도(등급) 결정
            int chosenRarity;
            if (forcedRarity.HasValue)
            {
                chosenRarity = Mathf.Clamp(forcedRarity.Value, 3, 5);
            }
            else if (isHighGradeBoosted)
            {
                // 고등급 부스트: 4성 80%, 5성 20%
                double roll = _random.NextDouble() * 100.0;
                chosenRarity = roll < Boosted5StarProb ? 5 : 4;
            }
            else
            {
                // 일반 확률: 3성 80%, 4성 18%, 5성 2%
                double roll = _random.NextDouble() * 100.0;
                if (roll < Standard5StarProb) chosenRarity = 5;
                else if (roll < Standard5StarProb + Standard4StarProb) chosenRarity = 4;
                else chosenRarity = 3;
            }

            // 2. 부위(SlotType) 결정 (4슬롯 25% 균등)
            WingSlotType chosenSlot;
            if (forcedSlot.HasValue)
            {
                chosenSlot = forcedSlot.Value;
            }
            else
            {
                int slotIndex = _random.Next(0, 4);
                chosenSlot = (WingSlotType)slotIndex;
            }

            // 3. 등급 및 부위별 데이터 설정 (세트 ID 및 추가 공격력)
            string setId = string.Empty;
            string wingName;
            int bonusDamage;
            string slotKorean = chosenSlot switch
            {
                WingSlotType.MainLeft => "주 날개(좌)",
                WingSlotType.MainRight => "주 날개(우)",
                WingSlotType.SubLeft => "부 날개(좌)",
                _ => "부 날개(우)"
            };

            if (chosenRarity == 5)
            {
                // 5성 세트
                string[] sets = new string[] { "SET_BLUE_DRAGON", "SET_SOLAR_PHOENIX" };
                setId = sets[_random.Next(0, sets.Length)];
                string prefix = setId == "SET_BLUE_DRAGON" ? "청룡의 천공익" : "태양의 신염익";
                wingName = $"{prefix} [{slotKorean}]";
                bonusDamage = _random.Next(50, 71); // 50 ~ 70
            }
            else if (chosenRarity == 4)
            {
                // 4성 세트
                string[] sets = new string[] { "SET_WHITE_TIGER", "SET_BLACK_TORTOISE" };
                setId = sets[_random.Next(0, sets.Length)];
                string prefix = setId == "SET_WHITE_TIGER" ? "백호의 서리날개" : "현무의 암석날개";
                wingName = $"{prefix} [{slotKorean}]";
                bonusDamage = _random.Next(25, 36); // 25 ~ 35
            }
            else
            {
                // 3성 일반 (세트 없음)
                setId = string.Empty;
                string[] prefixes = new string[] { "바람의 깃털", "여행자의 날개", "수호의 깃" };
                wingName = $"{prefixes[_random.Next(0, prefixes.Length)]} [{slotKorean}]";
                bonusDamage = _random.Next(10, 16); // 10 ~ 15
            }

            string baseWingId = $"WING_{chosenRarity}S_{chosenSlot}_{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            return new WingItemInstance(baseWingId, wingName, chosenSlot, chosenRarity, setId, bonusDamage);
        }

        /// <summary>
        /// [세부 개발 명세 4.1] 적 처치 트리거 핸들러
        /// 일반 적 처치 시 30% 확률, 엘리트 60%, 보스 100% 확정(고등급 부스트) 드롭
        /// </summary>
        public WingItemInstance OnEnemyDefeated(EnemyType enemyType = EnemyType.Normal)
        {
            float dropChance = enemyType switch
            {
                EnemyType.Boss => 1.0f,
                EnemyType.Elite => 0.6f,
                _ => 0.30f // 일반 적 처치 30%
            };

            double roll = _random.NextDouble();
            if (roll <= dropChance)
            {
                bool isBoosted = (enemyType == EnemyType.Boss);
                var wing = GenerateWing(isHighGradeBoosted: isBoosted);
                _inventory.AddWing(wing);

                OnWingDropped?.Invoke(wing, $"EnemyDefeated_{enemyType}");
                return wing;
            }

            return null;
        }

        public WingItemInstance OnEnemyDefeated(string enemyTypeStr)
        {
            if (Enum.TryParse<EnemyType>(enemyTypeStr, true, out var type))
            {
                return OnEnemyDefeated(type);
            }
            return OnEnemyDefeated(EnemyType.Normal);
        }

        /// <summary>
        /// [세부 개발 명세 4.2] D20 극단값 주사위 판정 연동 핸들러
        /// - 눈금 1 (대실패): '불운의 위로' 보상으로 날개 1개 확정 획득
        /// - 눈금 20 (즉사/대성공): '절대 행운' 보상으로 4~5성 고등급 날개 1개 확정 획득
        /// </summary>
        public WingItemInstance OnDiceRolled(int diceValue)
        {
            if (diceValue == 1)
            {
                // [눈금 1 대실패]: '불운의 위로' 날개 1개 확정 지급
                var wing = GenerateWing(isHighGradeBoosted: false);
                _inventory.AddWing(wing);

                Debug.Log($"🪽 [WingDropManager] 💔 D20 대실패(1) - '불운의 위로' 날개 확정 드롭! ({wing.WingName})");
                OnWingDropped?.Invoke(wing, "DiceCritFail_1");
                return wing;
            }
            else if (diceValue == 20)
            {
                // [눈금 20 절대 성공/즉사]: '절대 행운' 4~5성 고등급 날개 1개 확정 지급
                var wing = GenerateWing(isHighGradeBoosted: true);
                _inventory.AddWing(wing);

                Debug.Log($"🪽 [WingDropManager] 🌟 D20 절대 성공(20) - '절대 행운' 고등급(★{wing.Rarity}) 날개 확정 드롭! ({wing.WingName})");
                OnWingDropped?.Invoke(wing, "DiceCritSuccess_20");
                return wing;
            }

            // 일반 눈금(2~19)은 기본 주사위 확정 드롭 없음
            return null;
        }

        /// <summary>
        /// [세부 개발 명세 4.3] 필드 상자 상호작용 트리거 핸들러
        /// 일반 상자 50%, 희귀 상자 100%, 전설 상자 100% 확정(고등급 부스트) 드롭
        /// </summary>
        public WingItemInstance OnChestOpened(ChestType chestType = ChestType.Normal)
        {
            float dropChance = chestType switch
            {
                ChestType.Legendary => 1.0f,
                ChestType.Rare => 1.0f,
                _ => 0.50f
            };

            double roll = _random.NextDouble();
            if (roll <= dropChance)
            {
                bool isBoosted = (chestType == ChestType.Legendary);
                var wing = GenerateWing(isHighGradeBoosted: isBoosted);
                _inventory.AddWing(wing);

                OnWingDropped?.Invoke(wing, $"ChestOpened_{chestType}");
                return wing;
            }

            return null;
        }

        public WingItemInstance OnChestOpened(string chestTypeStr)
        {
            if (Enum.TryParse<ChestType>(chestTypeStr, true, out var type))
            {
                return OnChestOpened(type);
            }
            return OnChestOpened(ChestType.Normal);
        }

        /// <summary>
        /// 전투 컨트롤러(BattleTurnController)의 D20 주사위 굴림 이벤트를 자동 구독합니다.
        /// </summary>
        public void BindBattleTurnController(BattleTurnController controller)
        {
            if (controller != null)
            {
                controller.OnD20Rolled += (res) => OnDiceRolled(res.Value);
                Debug.Log("[WingDropManager] ⚔️ BattleTurnController의 OnD20Rolled 이벤트 구독 완료");
            }
        }
    }
}
