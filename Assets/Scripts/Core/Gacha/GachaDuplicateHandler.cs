using System;
using System.Collections.Generic;
using UnityEngine;
using RPG25D.Core.Inventory;
using RPG25D.Core.Saju;
using RPG25D.Data;

namespace RPG25D.Core.Gacha
{
    /// <summary>
    /// 중복 가챠 소환 결과 정보 (기존 사주 vs 신규 사주 비교 및 샤드 지급 정보)
    /// </summary>
    public class DuplicateGachaResult
    {
        public string BaseDataID { get; set; }
        public string TargetInstanceID { get; set; }
        public CharacterInstance TargetCharacter { get; set; }
        public CharacterSaju OldSaju { get; set; }
        public CharacterSaju NewSaju { get; set; }
        public int ShardsAwarded { get; set; }
        public int TotalShardsNow { get; set; }

        public override string ToString()
        {
            return $"[Duplicate] BaseID: {BaseDataID} | 기존 사주: {OldSaju?.ToEightCharacters()} vs 신규 사주: {NewSaju?.ToEightCharacters()} | 샤드: +{ShardsAwarded} (총 {TotalShardsNow})";
        }
    }

    /// <summary>
    /// [요구 산출물 2] 중복 소환 처리 및 사주 선택/초월 시스템 (GachaDuplicateHandler.cs)
    /// - 가챠 소환 시 이미 보유 중인 BaseDataID의 캐릭터가 다시 뽑혔을 때의 처리 로직
    ///   1. 돌파/초월 재화 지급: 해당 캐릭터의 전용 영혼 조각(Shard) 증가 (AddShards)
    ///   2. 사주 명식 선택권(비교/교체) 로직:
    ///      - 기존 보유 캐릭터의 사주(OldSaju)와 방금 새로 굴려진 사주(NewSaju) 정보 반환
    ///      - 플레이어 선택에 따라 기존 사주를 유지하거나 새 사주로 덮어쓰는(Overwrite) 기능 제공 (UpdateCharacterSaju)
    ///   3. 초월(돌파) 메서드: 보유 조각을 소모하여 대상 캐릭터의 Transcendence 단계를 1 증가 (AscendCharacter)
    /// </summary>
    public class GachaDuplicateHandler
    {
        private static GachaDuplicateHandler _instance;

        /// <summary>
        /// 런타임 전역 싱글톤 인스턴스
        /// </summary>
        public static GachaDuplicateHandler Instance
        {
            get => _instance ??= new GachaDuplicateHandler();
            set => _instance = value;
        }

        private readonly CharacterInventoryManager _inventory;
        public CharacterInventoryManager Inventory => _inventory;

        // 전용 영혼 조각 보관소 (Key: BaseDataID, Value: 보유 조각 수량)
        public Dictionary<string, int> CharacterShards { get; } = new Dictionary<string, int>();

        // 최대 초월 단계 상수
        public const int MaxTranscendence = 5;

        // 이벤트 정의
        public event Action<string, int> OnShardsUpdated;
        public event Action<CharacterInstance, int> OnCharacterAscended;
        public event Action<CharacterInstance, CharacterSaju> OnSajuUpdated;

        public GachaDuplicateHandler(CharacterInventoryManager inventory = null)
        {
            _inventory = inventory ?? CharacterInventoryManager.Instance;
        }

        /// <summary>
        /// 특정 캐릭터의 영혼 조각 보유량을 조회합니다.
        /// </summary>
        public int GetShards(string baseDataId)
        {
            if (string.IsNullOrEmpty(baseDataId)) return 0;
            return CharacterShards.TryGetValue(baseDataId, out var count) ? count : 0;
        }

        /// <summary>
        /// [세부 개발 명세 2.1] 해당 캐릭터의 전용 영혼 조각(Shard) 증가
        /// </summary>
        public void AddShards(string baseDataId, int count)
        {
            if (string.IsNullOrEmpty(baseDataId) || count <= 0) return;

            int current = GetShards(baseDataId);
            int updated = current + count;
            CharacterShards[baseDataId] = updated;

            Debug.Log($"[GachaDuplicateHandler] 💎 영혼 조각 획득: [{baseDataId}] +{count}개 (현재 총 {updated}개 보유)");

            OnShardsUpdated?.Invoke(baseDataId, updated);
        }

        /// <summary>
        /// 전용 영혼 조각을 소모합니다.
        /// </summary>
        public bool ConsumeShards(string baseDataId, int count)
        {
            if (string.IsNullOrEmpty(baseDataId) || count <= 0) return false;

            int current = GetShards(baseDataId);
            if (current < count)
            {
                Debug.LogWarning($"[GachaDuplicateHandler] 조각이 부족합니다. (필요: {count}, 보유: {current})");
                return false;
            }

            int updated = current - count;
            CharacterShards[baseDataId] = updated;

            OnShardsUpdated?.Invoke(baseDataId, updated);
            return true;
        }

        /// <summary>
        /// 희귀도(등급)별 기본 중복 조각 지급량 계산 (3성: 10개, 4성: 20개, 5성: 50개)
        /// </summary>
        public int GetDefaultDuplicateShards(int rarity)
        {
            return rarity switch
            {
                5 => 50,
                4 => 20,
                _ => 10
            };
        }

        /// <summary>
        /// 해당 캐릭터가 이미 인벤토리에 존재하는 중복 캐릭터인지 확인합니다.
        /// </summary>
        public bool IsDuplicate(string baseDataId)
        {
            return _inventory != null && _inventory.HasBaseCharacter(baseDataId);
        }

        /// <summary>
        /// [세부 개발 명세 2] 중복 소환 처리 로직
        /// - 중복인 경우 전용 영혼 조각을 지급하고, 기존 사주와 신규 사주를 비교할 수 있는 결과 객체를 반환합니다.
        /// - 중복이 아닌 신규 캐릭터인 경우 null을 반환합니다.
        /// </summary>
        public DuplicateGachaResult HandleDuplicateRoll(CharacterInstance newlyRolledInstance, int? customShardCount = null)
        {
            if (newlyRolledInstance == null || string.IsNullOrEmpty(newlyRolledInstance.BaseDataID))
            {
                return null;
            }

            // 중복 여부 판정
            if (!IsDuplicate(newlyRolledInstance.BaseDataID))
            {
                return null; // 신규 캐릭터
            }

            // 1. 기존 메인 캐릭터 인스턴스 조회
            var existing = _inventory.GetFirstInstanceByBaseId(newlyRolledInstance.BaseDataID);
            if (existing == null)
            {
                return null;
            }

            // 2. 조각 지급
            int shardsToAward = customShardCount ?? GetDefaultDuplicateShards(newlyRolledInstance.Rarity);
            AddShards(newlyRolledInstance.BaseDataID, shardsToAward);

            // 3. 사주 비교 정보 반환
            var result = new DuplicateGachaResult
            {
                BaseDataID = existing.BaseDataID,
                TargetInstanceID = existing.InstanceID,
                TargetCharacter = existing,
                OldSaju = existing.Saju,
                NewSaju = newlyRolledInstance.Saju,
                ShardsAwarded = shardsToAward,
                TotalShardsNow = GetShards(existing.BaseDataID)
            };

            Debug.Log($"[GachaDuplicateHandler] 🔄 중복 소환 발생! [{existing.CharacterName}] " +
                      $"기존 사주: {result.OldSaju?.ToEightCharacters()} | 신규 사주: {result.NewSaju?.ToEightCharacters()} | " +
                      $"지급 조각: +{shardsToAward} (보유: {result.TotalShardsNow})");

            return result;
        }

        /// <summary>
        /// [세부 개발 명세 2.2] 사주 명식 교체/덮어쓰기 기능
        /// 플레이어가 새로 뽑힌 사주를 채택하기로 결정했을 때 호출합니다.
        /// </summary>
        public bool UpdateCharacterSaju(string instanceId, CharacterSaju newSaju)
        {
            if (string.IsNullOrEmpty(instanceId) || newSaju == null)
            {
                return false;
            }

            var target = _inventory.GetCharacter(instanceId);
            if (target == null)
            {
                Debug.LogWarning($"[GachaDuplicateHandler] 인스턴스를 찾을 수 없습니다: {instanceId}");
                return false;
            }

            string oldSajuStr = target.Saju != null ? target.Saju.ToEightCharacters() : "없음";
            target.Saju = newSaju;

            Debug.Log($"[GachaDuplicateHandler] 📜 사주 명식 교체 완료! [{target.CharacterName}] " +
                      $"({oldSajuStr} ➔ {newSaju.ToEightCharacters()})");

            OnSajuUpdated?.Invoke(target, newSaju);
            return true;
        }

        /// <summary>
        /// 초월 단계별 필요 조각 수량 계산: (현재 초월 단계 + 1) * 10
        /// (예: 0 -> 1단계: 10개, 1 -> 2단계: 20개, 2 -> 3단계: 30개 등)
        /// </summary>
        public int GetRequiredShardsForAscension(CharacterInstance character)
        {
            if (character == null) return 10;
            return (character.Transcendence + 1) * 10;
        }

        /// <summary>
        /// [세부 개발 명세 2.3] 초월(돌파) 메서드
        /// 보유 조각을 소모하여 대상 캐릭터의 Transcendence 단계를 1 증가시킵니다.
        /// </summary>
        public bool AscendCharacter(string instanceId, int? requiredShards = null)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;

            var target = _inventory.GetCharacter(instanceId);
            if (target == null)
            {
                Debug.LogWarning($"[GachaDuplicateHandler] 초월 대상 캐릭터를 찾을 수 없습니다: {instanceId}");
                return false;
            }

            if (target.Transcendence >= MaxTranscendence)
            {
                Debug.LogWarning($"[GachaDuplicateHandler] 이미 최대 초월({MaxTranscendence}단계)에 도달했습니다: {target.CharacterName}");
                return false;
            }

            int cost = requiredShards ?? GetRequiredShardsForAscension(target);
            if (!ConsumeShards(target.BaseDataID, cost))
            {
                Debug.LogWarning($"[GachaDuplicateHandler] 초월 조각이 부족하여 초월에 실패했습니다. (필요: {cost}개)");
                return false;
            }

            // 초월 성공 처리
            target.Transcendence++;

            Debug.Log($"[GachaDuplicateHandler] 🌟 [{target.CharacterName}] 초월 성공! " +
                      $"현재 초월: {target.Transcendence}단계 (소모 조각: {cost}개, 공격력: {target.CurrentAttack})");

            OnCharacterAscended?.Invoke(target, target.Transcendence);
            return true;
        }

        /// <summary>
        /// 조각 저장소 초기화
        /// </summary>
        public void Clear()
        {
            CharacterShards.Clear();
        }
    }
}
