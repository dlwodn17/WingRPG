using System;
using UnityEngine;
using RPG25D.Core.Exploration;

namespace RPG25D.Data.Events
{
    /// <summary>
    /// 마을 주민 또는 동료 NPC와의 대화 이벤트 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "Event_NPCInteraction", menuName = "RPG25D/Events/NPCInteraction")]
    public class NPCInteractionEventSO : ScriptableObject, IExplorationEvent
    {
        [Header("NPC 식별자 및 이름")]
        [SerializeField] private string _npcId = "NPC_VILLAGER_01";
        [SerializeField] private string _npcName = "마을 경비병";

        [Header("대사 목록")]
        [SerializeField] private string[] _dialogueLines = new string[]
        {
            "조심하게! 서쪽 숲에 난폭한 몬스터들이 출몰하고 있어.",
            "준비가 덜 되었다면 상점에서 포션을 챙겨가도록 해."
        };

        public string EventId => _npcId;
        public string EventName => _npcName;
        public string[] DialogueLines => _dialogueLines;

        public void Initialize(string id, string name, string[] lines)
        {
            _npcId = id;
            _npcName = name;
            _dialogueLines = lines;
        }

        public bool CanExecute(ExplorationContext context)
        {
            return true; // 대화는 반복 실행 가능
        }

        public ExplorationEventResult Execute(ExplorationContext context)
        {
            string dialogText = _dialogueLines != null && _dialogueLines.Length > 0
                ? string.Join(" / ", _dialogueLines)
                : "......";

            string msg = $"[{_npcName}] 대화: \"{dialogText}\"";
            Debug.Log($"[NPCInteraction] 💬 {msg}");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecordEventTriggered(_npcId);
            }

            return new ExplorationEventResult(true, _npcId, msg);
        }
    }
}
