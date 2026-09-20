using System;
using UnityEngine;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// 2.5D 쿼터뷰 탐색 추적 카메라 (Perspective / Low FOV)
    /// 플레이어 이동에 맞춰 부드럽게 추적(SmoothDamp)하며 카메라 셰이크와 자연스럽게 공존합니다.
    /// </summary>
    public class ExplorationCameraFollow : MonoBehaviour
    {
        [Header("추적 대상")]
        [SerializeField] private Transform _target;

        [Header("2.5D 오프셋 및 앵글")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 7.5f, -8.0f);
        [SerializeField] private Vector3 _rotationAngle = new Vector3(34f, 0f, 0f);
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private float _fieldOfView = 34f;

        private Vector3 _currentVelocity;
        private Camera _cam;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.orthographic = false;
                _cam.fieldOfView = _fieldOfView;
            }
            transform.rotation = Quaternion.Euler(_rotationAngle);
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = FindAnyObjectByType<ExplorationPlayerController>();
                if (player != null) _target = player.transform;
            }

            if (_target != null)
            {
                transform.position = _target.position + _offset;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);
        }

        public void SnapToTarget()
        {
            if (_target != null)
            {
                transform.position = _target.position + _offset;
                _currentVelocity = Vector3.zero;
            }
        }
    }
}
