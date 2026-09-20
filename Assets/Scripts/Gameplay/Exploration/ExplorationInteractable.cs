using System;
using UnityEngine;
using RPG25D.Core.Exploration;
using RPG25D.Data;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// 탐색 필드에서 플레이어와 상호작용 가능한 월드 오브젝트 (보물상자, NPC 등)
    /// </summary>
    public class ExplorationInteractable : MonoBehaviour
    {
        [Header("이벤트 데이터 (ScriptableObject)")]
        [SerializeField] private ScriptableObject _eventObject;

        [Header("상호작용 설정")]
        [SerializeField] private float _interactRadius = 1.6f;
        [SerializeField] private string _interactionPrompt = "E 키로 상호작용";

        [Header("시각 연출 (선택)")]
        [SerializeField] private GameObject _openedVisual;
        [SerializeField] private GameObject _closedVisual;

        public IExplorationEvent ExplorationEvent => _eventObject as IExplorationEvent;
        public float InteractRadius => _interactRadius;
        public string Prompt => _interactionPrompt;
        public bool IsInteractable { get; private set; } = true;

        public event Action<ExplorationEventResult> OnInteracted;

        private void Start()
        {
            RefreshVisualState();
        }

        public void SetEvent(ScriptableObject eventSO)
        {
            _eventObject = eventSO;
            RefreshVisualState();
        }

        public void RefreshVisualState()
        {
            if (ExplorationEvent == null) return;

            bool isAlreadyTriggered = GameManager.Instance != null && GameManager.Instance.IsEventTriggered(ExplorationEvent.EventId);
            if (isAlreadyTriggered)
            {
                if (_closedVisual != null) _closedVisual.SetActive(false);
                if (_openedVisual != null) _openedVisual.SetActive(true);
            }
            else
            {
                if (_closedVisual != null) _closedVisual.SetActive(true);
                if (_openedVisual != null) _openedVisual.SetActive(false);
            }
        }

        public ExplorationEventResult TriggerInteraction(Vector3 playerPosition)
        {
            if (ExplorationEvent == null)
            {
                Debug.LogWarning($"[ExplorationInteractable] {gameObject.name}에 IExplorationEvent가 할당되지 않았습니다.");
                return new ExplorationEventResult(false, string.Empty, "이벤트 없음");
            }

            // 거리 검증
            float dist = Vector3.Distance(playerPosition, transform.position);
            if (dist > _interactRadius * 1.5f)
            {
                return new ExplorationEventResult(false, ExplorationEvent.EventId, "상호작용 거리가 너무 멉니다.");
            }

            // 탐색 컨텍스트 구성
            var context = new ExplorationContext(playerPosition, GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0);
            if (GameManager.Instance != null)
            {
                foreach (var ev in GameManager.Instance.TriggeredEventIds) context.TriggeredEventIds.Add(ev);
                foreach (var it in GameManager.Instance.InventoryItems) context.Inventory.Add(it);
            }

            var result = ExplorationEvent.Execute(context);
            if (result.Success)
            {
                RefreshVisualState();
            }

            OnInteracted?.Invoke(result);
            return result;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
