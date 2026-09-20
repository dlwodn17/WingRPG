using System;
using UnityEngine;
using RPG25D.Core.Exploration;

namespace RPG25D.Data.Events
{
    /// <summary>
    /// [요구 산출물 2] TreasureChestEventSO.cs
    /// IExplorationEvent를 구현한 보물상자 개봉 이벤트 ScriptableObject입니다.
    /// CLI 헤드리스 환경에서도 로직이 단독으로 검증될 수 있도록 설계되었습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_TreasureChest", menuName = "RPG25D/Events/TreasureChest")]
    public class TreasureChestEventSO : ScriptableObject, IExplorationEvent
    {
        [Header("이벤트 고유 식별자")]
        [SerializeField] private string _chestId = "CHEST_001";
        [SerializeField] private string _chestName = "고대의 보물상자";

        [Header("보상 목록")]
        [SerializeField] private string _itemReward = "마법 물약 (Potion)";
        [SerializeField] private int _goldReward = 150;

        public string EventId => _chestId;
        public string EventName => _chestName;
        public string ItemReward => _itemReward;
        public int GoldReward => _goldReward;

        public void Initialize(string id, string name, string item, int gold)
        {
            _chestId = id;
            _chestName = name;
            _itemReward = item;
            _goldReward = gold;
        }

        public bool CanExecute(ExplorationContext context)
        {
            if (context == null) return true;
            // 이미 개봉된 상자인지 검사
            return !context.TriggeredEventIds.Contains(_chestId);
        }

        public ExplorationEventResult Execute(ExplorationContext context)
        {
            if (!CanExecute(context))
            {
                string failMsg = $"[{_chestName}] 이미 열려 비어있는 상자입니다.";
                Debug.Log($"[TreasureChest] ⚠️ {failMsg}");
                return new ExplorationEventResult(false, _chestId, failMsg);
            }

            // 1. 컨텍스트 데이터 갱신
            if (context != null)
            {
                context.TriggeredEventIds.Add(_chestId);
                if (!string.IsNullOrEmpty(_itemReward))
                {
                    context.Inventory.Add(_itemReward);
                }
                context.Gold += _goldReward;
            }

            // 2. GameManager 영속 데이터 동기화
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordEventTriggered(_chestId);
                if (!string.IsNullOrEmpty(_itemReward))
                {
                    GameManager.Instance.AddItem(_itemReward);
                }
                if (_goldReward > 0)
                {
                    GameManager.Instance.AddGold(_goldReward);
                }
            }

            string successMsg = $"[{_chestName}] 개봉 완료! 획득: '{_itemReward}', +{_goldReward} G";
            Debug.Log($"[TreasureChest] 🎁 {successMsg}");

            return new ExplorationEventResult(true, _chestId, successMsg, _itemReward, _goldReward);
        }
    }
}
