using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Inventory;
using RPG25D.Core.Saju;
using RPG25D.Data;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// [요구 산출물 3] 출전 파티 편성 시스템 (PartyFormationManager.cs)
    /// - 전투 및 필드 탐색에 출전할 4인 파티 슬롯 관리 (string[4] ActivePartySlotInstanceIDs)
    /// - 파티 슬롯 등록, 교체, 해제 메서드 (AssignToSlot, RemoveFromSlot)
    /// - 중복 출전 방지 검증: 동일한 CharacterInstance는 물론, 동일한 BaseDataID를 가진 캐릭터는 파티 내 1명만 편성 가능하도록 예외 처리
    /// - 현재 파티원의 CharacterInstance 및 바인딩된 CharacterSaju 목록을 리스트로 반환하는 헬퍼 메서드 제공
    /// </summary>
    public class PartyFormationManager
    {
        public const int PartySlotCount = 4;

        private static PartyFormationManager _instance;

        /// <summary>
        /// 런타임 전역 싱글톤 인스턴스
        /// </summary>
        public static PartyFormationManager Instance
        {
            get => _instance ??= new PartyFormationManager();
            set => _instance = value;
        }

        private readonly CharacterInventoryManager _inventory;
        public CharacterInventoryManager Inventory => _inventory;

        /// <summary>
        /// [세부 개발 명세 3] 4인 출전 파티 슬롯 (각 슬롯의 InstanceID 저장, 미편성 시 null)
        /// </summary>
        public string[] ActivePartySlotInstanceIDs { get; } = new string[PartySlotCount];

        // 이벤트 정의
        public event Action<int, CharacterInstance> OnSlotAssigned;
        public event Action<int> OnSlotRemoved;
        public event Action OnPartyChanged;

        public PartyFormationManager(CharacterInventoryManager inventory = null)
        {
            _inventory = inventory ?? CharacterInventoryManager.Instance;
        }

        /// <summary>
        /// 슬롯 인덱스 유효성 검사 (0 ~ 3)
        /// </summary>
        private void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= PartySlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"파티 슬롯 인덱스는 0 이상 {PartySlotCount - 1} 이하여야 합니다. (입력값: {slotIndex})");
            }
        }

        /// <summary>
        /// [세부 개발 명세 3] 파티 슬롯 등록 및 중복 출전 방지 검증
        /// - 동일한 InstanceID 중복 등록 차단 (InvalidOperationException)
        /// - 동일한 BaseDataID 중복 등록 차단 (InvalidOperationException)
        /// </summary>
        public void AssignToSlot(int slotIndex, string instanceId)
        {
            ValidateSlotIndex(slotIndex);

            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentException("등록할 캐릭터의 InstanceID가 유효하지 않습니다.", nameof(instanceId));
            }

            var targetCharacter = _inventory?.GetCharacter(instanceId);
            if (targetCharacter == null)
            {
                throw new ArgumentException($"인벤토리에 존재하지 않는 캐릭터 인스턴스 ID입니다: {instanceId}", nameof(instanceId));
            }

            // 중복 출전 검증: 다른 슬롯 확인
            for (int i = 0; i < PartySlotCount; i++)
            {
                if (i == slotIndex) continue;

                string existingId = ActivePartySlotInstanceIDs[i];
                if (string.IsNullOrEmpty(existingId)) continue;

                // 1. 동일한 CharacterInstance 중복 방지
                if (string.Equals(existingId, instanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"[PartyFormationManager] 캐릭터 '{targetCharacter.CharacterName}'(ID: {instanceId})는 이미 {i}번 슬롯에 등록되어 있습니다.");
                }

                // 2. 동일한 BaseDataID를 가진 캐릭터 중복 방지
                var otherChar = _inventory.GetCharacter(existingId);
                if (otherChar != null && string.Equals(otherChar.BaseDataID, targetCharacter.BaseDataID, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[PartyFormationManager] 동일한 베이스 캐릭터 (BaseID: '{targetCharacter.BaseDataID}', 이름: '{targetCharacter.CharacterName}')는 파티 내 중복 편성할 수 없습니다. (슬롯 {i}번에 '{otherChar.CharacterName}' 등록 중)");
                }
            }

            // 슬롯에 할당
            ActivePartySlotInstanceIDs[slotIndex] = instanceId;

            Debug.Log($"[PartyFormationManager] ⚔️ 파티 슬롯 [{slotIndex}] 등록: [{targetCharacter.CharacterName}] " +
                      $"(BaseID: {targetCharacter.BaseDataID}, ID: {instanceId.Substring(0, 8)}...)");

            OnSlotAssigned?.Invoke(slotIndex, targetCharacter);
            OnPartyChanged?.Invoke();
        }

        /// <summary>
        /// UI 등 예외를 던지지 않는 환경을 위한 안전한 슬롯 할당 메서드
        /// </summary>
        public bool TryAssignToSlot(int slotIndex, string instanceId, out string errorMessage)
        {
            try
            {
                AssignToSlot(slotIndex, instanceId);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// [세부 개발 명세 3] 파티 슬롯 해제
        /// </summary>
        public bool RemoveFromSlot(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);

            if (string.IsNullOrEmpty(ActivePartySlotInstanceIDs[slotIndex]))
            {
                return false;
            }

            ActivePartySlotInstanceIDs[slotIndex] = null;

            Debug.Log($"[PartyFormationManager] 🚫 파티 슬롯 [{slotIndex}] 해제 완료");

            OnSlotRemoved?.Invoke(slotIndex);
            OnPartyChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 전체 파티 슬롯 비우기
        /// </summary>
        public void ClearParty()
        {
            for (int i = 0; i < PartySlotCount; i++)
            {
                ActivePartySlotInstanceIDs[i] = null;
            }
            OnPartyChanged?.Invoke();
        }

        /// <summary>
        /// 특정 슬롯에 등록된 캐릭터 인스턴스 반환
        /// </summary>
        public CharacterInstance GetCharacterInSlot(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            string id = ActivePartySlotInstanceIDs[slotIndex];
            if (string.IsNullOrEmpty(id) || _inventory == null) return null;
            return _inventory.GetCharacter(id);
        }

        /// <summary>
        /// [세부 개발 명세 3] 현재 파티원의 CharacterInstance 목록 반환
        /// 빈 슬롯을 제외한 실제 등록된 파티원 목록을 슬롯 순서대로 반환합니다.
        /// </summary>
        public List<CharacterInstance> GetActivePartyCharacters()
        {
            var list = new List<CharacterInstance>();
            for (int i = 0; i < PartySlotCount; i++)
            {
                var ch = GetCharacterInSlot(i);
                if (ch != null)
                {
                    list.Add(ch);
                }
            }
            return list;
        }

        /// <summary>
        /// [세부 개발 명세 3] 현재 파티원에게 바인딩된 고유 CharacterSaju(사주팔자) 목록 반환
        /// </summary>
        public List<CharacterSaju> GetActivePartySajuList()
        {
            var list = new List<CharacterSaju>();
            foreach (var ch in GetActivePartyCharacters())
            {
                if (ch.Saju != null)
                {
                    list.Add(ch.Saju);
                }
            }
            return list;
        }

        /// <summary>
        /// 특정 슬롯이 비어있는지 여부
        /// </summary>
        public bool IsSlotEmpty(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            return string.IsNullOrEmpty(ActivePartySlotInstanceIDs[slotIndex]);
        }

        /// <summary>
        /// 현재 편성된 파티원 수 (0 ~ 4)
        /// </summary>
        public int ActiveMemberCount => ActivePartySlotInstanceIDs.Count(id => !string.IsNullOrEmpty(id));

        /// <summary>
        /// 4인 풀 파티 구성 여부
        /// </summary>
        public bool IsPartyFull => ActiveMemberCount == PartySlotCount;

        /// <summary>
        /// 파티원이 1명도 없는지 여부
        /// </summary>
        public bool IsPartyEmpty => ActiveMemberCount == 0;
    }
}
