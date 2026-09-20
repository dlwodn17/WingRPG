using UnityEngine;

namespace RPG25D.Visual
{
    /// <summary>
    /// 2.5D 빌보드 컴포넌트 (BillboardActor25D.cs)
    /// 3D X-Z 평면 위에 서 있는 2D 스프라이트/쿼드가 카메라의 고정된 각도에 맞춰
    /// 올바른 원근감과 실시간 3D 그림자(Shadow Caster/Receiver)를 유지하도록 제어합니다.
    /// </summary>
    [ExecuteAlways]
    public class BillboardActor25D : MonoBehaviour
    {
        [Header("빌보드 설정")]
        [Tooltip("Y축 회전만 고정할지, 카메라 완전 정면을 바라볼지 여부")]
        [SerializeField] private bool _lockYAxis = true;
        [SerializeField] private Camera _targetCamera;

        [Header("비주얼 효과")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private float _hitShakeDuration = 0.2f;

        private Vector3 _originalVisualPos;
        private float _shakeTimer = 0f;

        private void Start()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }
            _originalVisualPos = _visualRoot.localPosition;
        }

        private void LateUpdate()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null) return;
            }

            // 카메라 시선 방향을 향하도록 회전 정렬
            Vector3 lookDirection = _targetCamera.transform.forward;

            if (_lockYAxis)
            {
                // Y축만 회전 (수직 기립 유지)
                lookDirection.y = 0;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
            else
            {
                // 카메라 완전 평행 정렬
                transform.rotation = _targetCamera.transform.rotation;
            }

            // 피격 흔들림 연출
            if (_shakeTimer > 0)
            {
                _shakeTimer -= Time.deltaTime;
                float shakeMagnitude = 0.1f * (_shakeTimer / _hitShakeDuration);
                _visualRoot.localPosition = _originalVisualPos + Random.insideUnitSphere * shakeMagnitude;
            }
            else
            {
                _visualRoot.localPosition = _originalVisualPos;
            }
        }

        public void PlayHitReaction()
        {
            _shakeTimer = _hitShakeDuration;
        }
    }
}
