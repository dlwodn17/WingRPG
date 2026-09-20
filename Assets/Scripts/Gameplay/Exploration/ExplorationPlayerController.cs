using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RPG25D.Core.Exploration;
using RPG25D.Data;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// [요구 산출물 1] ExplorationPlayerController.cs
    /// 2.5D 필드에서 플레이어의 8방향 이동, 스프라이트 방향 플립, 상호작용(E키)을 총괄하는 컨트롤러입니다.
    /// 순수 C# 모델(ExplorationMovementModel)과 분리되어 CLI 환경에서도 단독 테스트가 가능합니다.
    /// </summary>
    public class ExplorationPlayerController : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float _moveSpeed = 4.8f;
        [SerializeField] private Transform _visualTransform;

        [Header("상호작용 감지")]
        [SerializeField] private float _interactionCheckRadius = 1.8f;
        [SerializeField] private LayerMask _interactableMask = ~0;

        // 순수 C# 이동 모델
        private ExplorationMovementModel _movementModel;

        public IExplorationMovementModel MovementModel => _movementModel;
        public Vector3 CurrentPosition => _movementModel != null ? _movementModel.Position : transform.position;
        public bool IsMoving => _movementModel != null && _movementModel.IsMoving;
        public bool IsFacingRight => _movementModel == null || _movementModel.IsFacingRight;

        public ExplorationInteractable CurrentFocusInteractable { get; private set; }

        public event Action<Vector3> OnPositionChanged;
        public event Action<ExplorationInteractable> OnInteractableFocusChanged;

        private void Awake()
        {
            InitializeMovementModel(transform.position, _moveSpeed);
            if (_visualTransform == null)
            {
                var child = transform.Find("Visual");
                if (child != null) _visualTransform = child;
                else _visualTransform = transform;
            }
        }

        private void Start()
        {
            // GameManager에 저장된 위치가 있다면 복원
            if (GameManager.Instance != null && GameManager.Instance.HasSavedPosition)
            {
                SetPosition(GameManager.Instance.SavedPlayerPosition);
            }
        }

        public void InitializeMovementModel(Vector3 startPos, float speed)
        {
            _movementModel = new ExplorationMovementModel(startPos, speed);
            _moveSpeed = speed;
        }

        private void Update()
        {
            // 헤드리스/배치 모드가 아닐 때 사용자 입력 처리
            if (Application.isPlaying && !Application.isBatchMode)
            {
                float h = 0f;
                float v = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
                }

                Vector2 inputAxis = new Vector2(h, v);
                UpdatePlayerMovement(inputAxis, Time.deltaTime);

                // 상호작용 키 (E 또는 Space)
                bool interactPressed = false;
                if (Keyboard.current != null)
                {
                    interactPressed = Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
                }

                if (interactPressed)
                {
                    TryInteract();
                }

                // 주변 상호작용 대상 탐색
                CheckNearbyInteractables();
            }
        }

        /// <summary>
        /// 이동 입력 및 델타타임을 전달받아 모델 및 월드 위치를 갱신합니다 (CLI 테스트 지원).
        /// </summary>
        public void UpdatePlayerMovement(Vector2 inputAxis, float deltaTime)
        {
            if (_movementModel == null)
            {
                InitializeMovementModel(transform.position, _moveSpeed);
            }

            _movementModel.UpdateMovement(inputAxis, deltaTime);
            transform.position = _movementModel.Position;

            UpdateVisualOrientation();
            OnPositionChanged?.Invoke(transform.position);
        }

        public void SetPosition(Vector3 newPosition)
        {
            if (_movementModel == null)
            {
                InitializeMovementModel(newPosition, _moveSpeed);
            }
            else
            {
                _movementModel.SetPosition(newPosition);
            }

            transform.position = newPosition;
            OnPositionChanged?.Invoke(newPosition);
        }

        private void UpdateVisualOrientation()
        {
            if (_visualTransform == null) return;

            Vector3 currentScale = _visualTransform.localScale;
            float targetSign = _movementModel.IsFacingRight ? 1f : -1f;

            if (Mathf.Sign(currentScale.x) != targetSign)
            {
                _visualTransform.localScale = new Vector3(Mathf.Abs(currentScale.x) * targetSign, currentScale.y, currentScale.z);
            }
        }

        /// <summary>
        /// 주변의 상호작용 오브젝트를 탐색하여 포커싱합니다.
        /// </summary>
        public void CheckNearbyInteractables()
        {
            var interactables = FindObjectsByType<ExplorationInteractable>(FindObjectsSortMode.None);
            ExplorationInteractable nearest = null;
            float minDistance = _interactionCheckRadius;

            Vector3 currentPos = transform.position;
            foreach (var inter in interactables)
            {
                if (inter == null || !inter.gameObject.activeInHierarchy) continue;

                float dist = Vector3.Distance(currentPos, inter.transform.position);
                if (dist <= inter.InteractRadius && dist < minDistance)
                {
                    minDistance = dist;
                    nearest = inter;
                }
            }

            if (CurrentFocusInteractable != nearest)
            {
                CurrentFocusInteractable = nearest;
                OnInteractableFocusChanged?.Invoke(nearest);
            }
        }

        /// <summary>
        /// 가장 가까운 상호작용 오브젝트와 상호작용을 실행합니다.
        /// </summary>
        public ExplorationEventResult TryInteract()
        {
            CheckNearbyInteractables();

            if (CurrentFocusInteractable != null)
            {
                Debug.Log($"[PlayerController] 🔍 [{CurrentFocusInteractable.gameObject.name}] 상호작용 시도");
                return CurrentFocusInteractable.TriggerInteraction(transform.position);
            }

            return new ExplorationEventResult(false, string.Empty, "상호작용 가능한 대상이 없습니다.");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactionCheckRadius);
        }
    }
}
