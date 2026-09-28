using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Core.Wings
{
    /// <summary>
    /// [요구 산출물 3] WingInventoryManager.cs
    /// 유저가 보유한 미장착/보유 날개 장비 인벤토리 관리 시스템
    /// - 보유 날개 목록 관리 (List<WingItemInstance> OwnedWings)
    /// - 신규 날개 획득 및 콘솔 드롭 로그 출력
    /// - 슬롯 부위별, 등급별, 세트별 검색/필터링 제공
    /// - 순수 C# 기반으로 단위 테스트 및 런타임 싱글톤 지원
    /// </summary>
    public class WingInventoryManager
    {
        private static WingInventoryManager _instance;

        public static WingInventoryManager Instance
        {
            get => _instance ??= new WingInventoryManager();
            set => _instance = value;
        }

        /// <summary>
        /// [세부 개발 명세 3] 유저가 보유한 날개 아이템 인벤토리 목록
        /// </summary>
        public List<WingItemInstance> OwnedWings { get; } = new List<WingItemInstance>();

        public int TotalWingCount => OwnedWings.Count;

        // 이벤트 정의
        public event Action<WingItemInstance> OnWingAdded;
        public event Action<string> OnWingRemoved;
        public event Action OnInventoryChanged;

        public WingInventoryManager()
        {
        }

        /// <summary>
        /// [세부 개발 명세 3] 신규 날개 아이템 인벤토리 획득/추가
        /// </summary>
        public bool AddWing(WingItemInstance newWing)
        {
            if (newWing == null || string.IsNullOrEmpty(newWing.ItemInstanceID))
            {
                Debug.LogWarning("[WingInventoryManager] 유효하지 않은 날개 아이템입니다.");
                return false;
            }

            OwnedWings.Add(newWing);

            string setTag = !string.IsNullOrEmpty(newWing.SetID) ? $", 세트: {newWing.SetID}" : "";
            Debug.Log($"🪽 [Wing Dropped] 아이템: {newWing.WingName} (★{newWing.Rarity}, {newWing.SlotType}, +{newWing.BonusDamage} Atk{setTag})");

            OnWingAdded?.Invoke(newWing);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 아이템 인스턴스 ID로 인벤토리에서 날개 제거 (캐릭터 장착 시 등)
        /// </summary>
        public bool RemoveWing(string itemInstanceId)
        {
            if (string.IsNullOrEmpty(itemInstanceId)) return false;

            int index = OwnedWings.FindIndex(w => string.Equals(w.ItemInstanceID, itemInstanceId, StringComparison.Ordinal));
            if (index >= 0)
            {
                var removed = OwnedWings[index];
                OwnedWings.RemoveAt(index);

                OnWingRemoved?.Invoke(itemInstanceId);
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 인스턴스 ID로 날개 조회
        /// </summary>
        public WingItemInstance GetWing(string itemInstanceId)
        {
            if (string.IsNullOrEmpty(itemInstanceId)) return null;
            return OwnedWings.FirstOrDefault(w => string.Equals(w.ItemInstanceID, itemInstanceId, StringComparison.Ordinal));
        }

        /// <summary>
        /// 특정 장착 부위(SlotType)별 날개 목록 필터링
        /// </summary>
        public List<WingItemInstance> GetWingsBySlot(WingSlotType slot)
        {
            return OwnedWings.Where(w => w.SlotType == slot).ToList();
        }

        /// <summary>
        /// 특정 희귀도(등급)별 날개 목록 필터링
        /// </summary>
        public List<WingItemInstance> GetWingsByRarity(int rarity)
        {
            return OwnedWings.Where(w => w.Rarity == rarity).ToList();
        }

        /// <summary>
        /// 특정 세트 ID를 가진 날개 목록 필터링
        /// </summary>
        public List<WingItemInstance> GetWingsBySet(string setId)
        {
            if (string.IsNullOrEmpty(setId)) return new List<WingItemInstance>();
            return OwnedWings.Where(w => string.Equals(w.SetID, setId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// 인벤토리 비우기
        /// </summary>
        public void Clear()
        {
            OwnedWings.Clear();
            OnInventoryChanged?.Invoke();
        }
    }
}
