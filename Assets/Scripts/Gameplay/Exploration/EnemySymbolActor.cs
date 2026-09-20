using System;
using UnityEngine;
using RPG25D.Core.Exploration;
using RPG25D.Data;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// 필드 위에 배치되는 2.5D 적 심볼(몬스터) 액터
    /// 플레이어와 접촉 시 D20 전투 씬으로 전이되는 트리거 역할을 합니다.
    /// </summary>
    public class EnemySymbolActor : MonoBehaviour
    {
        [Header("인카운터 식별자 및 정보")]
        [SerializeField] private int _encounterId = 1;
        [SerializeField] private string _symbolId = "";
        [SerializeField] private string _monsterName = "고블린 정찰병";
        [SerializeField] private float _triggerRadius = 1.25f;

        [Header("배회(Patrol) 설정")]
        [SerializeField] private bool _enablePatrol = true;
        [SerializeField] private float _patrolDistance = 2.0f;
        [SerializeField] private float _patrolSpeed = 1.2f;

        private Vector3 _originPosition;
        private bool _isPatrolForward = true;

        public int EncounterId => _encounterId;
        public string SymbolId
        {
            get => !string.IsNullOrEmpty(_symbolId) ? _symbolId : (_encounterId > 0 ? $"Symbol_Enemy_{_encounterId}" : gameObject.name);
            set => _symbolId = value;
        }
        public string MonsterName => _monsterName;
        public float TriggerRadius => _triggerRadius;

        public void Initialize(int id, string name, float radius = 1.25f, string symbolId = null)
        {
            _encounterId = id;
            _monsterName = name;
            _triggerRadius = radius;
            if (!string.IsNullOrEmpty(symbolId))
            {
                _symbolId = symbolId;
            }
        }

        private void Awake()
        {
            _originPosition = transform.position;
        }

        private void Start()
        {
            CheckDefeatedState();
        }

        /// <summary>
        /// GameManager의 처치 목록(DefeatedEnemyIDs)을 조회하여 이미 처치된 심볼이면 비활성화합니다.
        /// </summary>
        public void CheckDefeatedState()
        {
            if (GameManager.Instance != null)
            {
                bool isDefeated = GameManager.Instance.IsEncounterDefeated(SymbolId) ||
                                  GameManager.Instance.IsEncounterDefeated(_encounterId) ||
                                  (GameManager.Instance.DefeatedEnemyIDs != null && GameManager.Instance.DefeatedEnemyIDs.Contains(SymbolId));

                if (isDefeated)
                {
                    gameObject.SetActive(false);
                    Debug.Log($"[EnemySymbolActor] 👻 처치된 적 심볼 비활성화: {SymbolId} (ID: {_encounterId})");
                }
            }
        }

        private void Update()
        {
            if (!_enablePatrol || !Application.isPlaying) return;

            // X축 기준 간단한 왕복 배회
            float offset = transform.position.x - _originPosition.x;
            if (_isPatrolForward)
            {
                transform.position += Vector3.right * (_patrolSpeed * Time.deltaTime);
                if (offset >= _patrolDistance) _isPatrolForward = false;
            }
            else
            {
                transform.position += Vector3.left * (_patrolSpeed * Time.deltaTime);
                if (offset <= -_patrolDistance) _isPatrolForward = true;
            }
        }

        public EncounterSymbolData ToData()
        {
            return new EncounterSymbolData(_encounterId, _monsterName, transform.position, _triggerRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _triggerRadius);
        }
    }
}
