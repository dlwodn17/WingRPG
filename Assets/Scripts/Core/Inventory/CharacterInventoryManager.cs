using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Core.Inventory
{
    /// <summary>
    /// 캐릭터 정렬 기준 옵션
    /// </summary>
    public enum CharacterSortOption
    {
        Rarity,       // 희귀도(등급)순
        Level,        // 레벨순
        Attack,       // 공격력순
        Acquisition,  // 획득순
        Name          // 이름순
    }

    /// <summary>
    /// [요구 산출물 1] 인벤토리 및 캐릭터 보관소 (CharacterInventoryManager.cs)
    /// - 보유 캐릭터 목록 관리: Dictionary<string, CharacterInstance> OwnedCharacters (Key: InstanceID)
    /// - 베이스 캐릭터별 보유 현황 매핑: Dictionary<string, List<string>> BaseIdToInstanceIds (동일 베이스 캐릭터의 인스턴스 추적용)
    /// - 신규 획득 캐릭터 추가 메서드 (AddCharacter)
    /// - 인스턴스 검색, 정렬(등급순, 레벨순, 획득순 등), 필터링 메서드 구현
    /// - 순수 C# 기반으로 단위 테스트가 가능하며, 런타임용 싱글톤 인스턴스도 지원합니다.
    /// </summary>
    public class CharacterInventoryManager
    {
        private static CharacterInventoryManager _instance;

        /// <summary>
        /// 런타임 전역 싱글톤 인스턴스
        /// </summary>
        public static CharacterInventoryManager Instance
        {
            get => _instance ??= new CharacterInventoryManager();
            set => _instance = value;
        }

        // 1. 보유 캐릭터 목록 (Key: InstanceID)
        public Dictionary<string, CharacterInstance> OwnedCharacters { get; } = new Dictionary<string, CharacterInstance>();

        // 2. 베이스 캐릭터별 인스턴스 ID 목록 매핑 (Key: BaseDataID)
        public Dictionary<string, List<string>> BaseIdToInstanceIds { get; } = new Dictionary<string, List<string>>();

        // 획득 순서 추적용 리스트 (InstanceID 목록)
        private readonly List<string> _acquisitionOrder = new List<string>();
        public IReadOnlyList<string> AcquisitionOrder => _acquisitionOrder;

        public int TotalCharacterCount => OwnedCharacters.Count;

        // 이벤트 정의
        public event Action<CharacterInstance> OnCharacterAdded;
        public event Action<string> OnCharacterRemoved;
        public event Action OnInventoryChanged;

        public CharacterInventoryManager()
        {
        }

        /// <summary>
        /// 신규 획득 캐릭터를 인벤토리에 등록합니다.
        /// </summary>
        /// <param name="newChar">등록할 캐릭터 인스턴스</param>
        /// <returns>등록 성공 여부</returns>
        public bool AddCharacter(CharacterInstance newChar)
        {
            if (newChar == null || string.IsNullOrEmpty(newChar.InstanceID))
            {
                Debug.LogWarning("[CharacterInventoryManager] 유효하지 않은 캐릭터 인스턴스입니다.");
                return false;
            }

            if (OwnedCharacters.ContainsKey(newChar.InstanceID))
            {
                Debug.LogWarning($"[CharacterInventoryManager] 이미 등록된 인스턴스 ID입니다: {newChar.InstanceID}");
                return false;
            }

            // 1. 인스턴스 등록
            OwnedCharacters[newChar.InstanceID] = newChar;

            // 2. BaseDataID 매핑 등록
            string baseId = !string.IsNullOrEmpty(newChar.BaseDataID) ? newChar.BaseDataID : "UNKNOWN";
            if (!BaseIdToInstanceIds.TryGetValue(baseId, out var instanceList))
            {
                instanceList = new List<string>();
                BaseIdToInstanceIds[baseId] = instanceList;
            }
            if (!instanceList.Contains(newChar.InstanceID))
            {
                instanceList.Add(newChar.InstanceID);
            }

            // 3. 획득 순서 기록
            _acquisitionOrder.Add(newChar.InstanceID);

            Debug.Log($"[CharacterInventoryManager] 📥 캐릭터 인벤토리 등록 완료: {newChar.CharacterName} " +
                      $"(ID: {newChar.InstanceID.Substring(0, 8)}..., Base: {baseId})");

            OnCharacterAdded?.Invoke(newChar);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 인벤토리에서 특정 캐릭터 인스턴스를 제거합니다.
        /// </summary>
        public bool RemoveCharacter(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !OwnedCharacters.TryGetValue(instanceId, out var target))
            {
                return false;
            }

            OwnedCharacters.Remove(instanceId);

            if (!string.IsNullOrEmpty(target.BaseDataID) && BaseIdToInstanceIds.TryGetValue(target.BaseDataID, out var list))
            {
                list.Remove(instanceId);
                if (list.Count == 0)
                {
                    BaseIdToInstanceIds.Remove(target.BaseDataID);
                }
            }

            _acquisitionOrder.Remove(instanceId);

            Debug.Log($"[CharacterInventoryManager] 📤 캐릭터 인벤토리 제거 완료: {target.CharacterName} (ID: {instanceId})");

            OnCharacterRemoved?.Invoke(instanceId);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 인스턴스 ID로 캐릭터 인스턴스를 조회합니다.
        /// </summary>
        public CharacterInstance GetCharacter(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId) && OwnedCharacters.TryGetValue(instanceId, out var charInst))
            {
                return charInst;
            }
            return null;
        }

        /// <summary>
        /// 인스턴스 보유 여부 확인
        /// </summary>
        public bool HasCharacter(string instanceId)
        {
            return !string.IsNullOrEmpty(instanceId) && OwnedCharacters.ContainsKey(instanceId);
        }

        /// <summary>
        /// 베이스 캐릭터 ID 보유 여부 확인 (동일 베이스 캐릭터가 1개 이상 있는지)
        /// </summary>
        public bool HasBaseCharacter(string baseDataId)
        {
            return !string.IsNullOrEmpty(baseDataId) &&
                   BaseIdToInstanceIds.TryGetValue(baseDataId, out var list) &&
                   list.Count > 0;
        }

        /// <summary>
        /// 특정 베이스 캐릭터 ID에 해당하는 모든 인스턴스 목록 반환
        /// </summary>
        public List<CharacterInstance> GetInstancesByBaseId(string baseDataId)
        {
            var result = new List<CharacterInstance>();
            if (!string.IsNullOrEmpty(baseDataId) && BaseIdToInstanceIds.TryGetValue(baseDataId, out var idList))
            {
                foreach (var id in idList)
                {
                    if (OwnedCharacters.TryGetValue(id, out var inst))
                    {
                        result.Add(inst);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 특정 베이스 캐릭터의 첫 번째 인스턴스를 반환 (중복 처리 시 메인 인스턴스 타겟팅용)
        /// </summary>
        public CharacterInstance GetFirstInstanceByBaseId(string baseDataId)
        {
            if (!string.IsNullOrEmpty(baseDataId) && BaseIdToInstanceIds.TryGetValue(baseDataId, out var idList) && idList.Count > 0)
            {
                string firstId = idList[0];
                if (OwnedCharacters.TryGetValue(firstId, out var inst))
                {
                    return inst;
                }
            }
            return null;
        }

        /// <summary>
        /// 전체 보유 캐릭터 목록 반환
        /// </summary>
        public List<CharacterInstance> GetAllCharacters()
        {
            return OwnedCharacters.Values.ToList();
        }

        /// <summary>
        /// 캐릭터 목록 정렬 (등급순, 레벨순, 공격력순, 획득순, 이름순)
        /// </summary>
        public List<CharacterInstance> GetSortedCharacters(CharacterSortOption sortOption = CharacterSortOption.Rarity, bool descending = true)
        {
            IEnumerable<CharacterInstance> query = OwnedCharacters.Values;

            switch (sortOption)
            {
                case CharacterSortOption.Rarity:
                    query = descending
                        ? query.OrderByDescending(c => c.Rarity).ThenByDescending(c => c.Level).ThenByDescending(c => c.CurrentAttack)
                        : query.OrderBy(c => c.Rarity).ThenBy(c => c.Level).ThenBy(c => c.CurrentAttack);
                    break;

                case CharacterSortOption.Level:
                    query = descending
                        ? query.OrderByDescending(c => c.Level).ThenByDescending(c => c.Rarity).ThenByDescending(c => c.CurrentAttack)
                        : query.OrderBy(c => c.Level).ThenBy(c => c.Rarity).ThenBy(c => c.CurrentAttack);
                    break;

                case CharacterSortOption.Attack:
                    query = descending
                        ? query.OrderByDescending(c => c.CurrentAttack).ThenByDescending(c => c.Level)
                        : query.OrderBy(c => c.CurrentAttack).ThenBy(c => c.Level);
                    break;

                case CharacterSortOption.Acquisition:
                    if (descending)
                    {
                        var reverseOrder = _acquisitionOrder.AsEnumerable().Reverse().ToList();
                        query = reverseOrder.Select(id => GetCharacter(id)).Where(c => c != null);
                    }
                    else
                    {
                        query = _acquisitionOrder.Select(id => GetCharacter(id)).Where(c => c != null);
                    }
                    break;

                case CharacterSortOption.Name:
                    query = descending
                        ? query.OrderByDescending(c => c.CharacterName)
                        : query.OrderBy(c => c.CharacterName);
                    break;
            }

            return query.ToList();
        }

        /// <summary>
        /// 조건식 기반 캐릭터 필터링
        /// </summary>
        public List<CharacterInstance> FilterCharacters(Func<CharacterInstance, bool> predicate)
        {
            if (predicate == null) return GetAllCharacters();
            return OwnedCharacters.Values.Where(predicate).ToList();
        }

        /// <summary>
        /// 희귀도(등급)별 캐릭터 필터링
        /// </summary>
        public List<CharacterInstance> FilterByRarity(int rarity)
        {
            return FilterCharacters(c => c.Rarity == rarity);
        }

        /// <summary>
        /// 인벤토리 전체 초기화
        /// </summary>
        public void Clear()
        {
            OwnedCharacters.Clear();
            BaseIdToInstanceIds.Clear();
            _acquisitionOrder.Clear();
            OnInventoryChanged?.Invoke();
        }
    }
}
