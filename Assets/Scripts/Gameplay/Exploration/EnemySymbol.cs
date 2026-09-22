using System;
using UnityEngine;
using RPG25D.Data;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// [요구 산출물 1] EnemySymbol.cs
    /// 필드에 배치되는 각 적 심볼 오브젝트에 부착되는 고유 식별자(ID) 부여 컴포넌트입니다.
    /// 에디터 배치 시 고유 ID를 자동 생성/수동 발급하며, 인카운터 시 GameManagerData에 현재 전투 대상 ID로 등록하고,
    /// 씬 로드 시 처치 여부를 검사하여 자동 비활성화(SetActive(false))합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySymbol : MonoBehaviour
    {
        [Header("적 심볼 고유 식별자 (세부 개발 명세 1)")]
        [Tooltip("중복되지 않는 적 심볼 고유 식별자")]
        [SerializeField] private string _enemyID = string.Empty;

        public string EnemyID
        {
            get => _enemyID;
            set => _enemyID = value;
        }

        private void Reset()
        {
            if (string.IsNullOrEmpty(_enemyID))
            {
                GenerateUniqueID();
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_enemyID))
            {
                GenerateUniqueID();
            }
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(_enemyID))
            {
                GenerateUniqueID();
            }
        }

        private void OnEnable()
        {
            CheckDefeatedState();
        }

        private void Start()
        {
            CheckDefeatedState();
        }

        /// <summary>
        /// [에디터 유틸리티] GUID 기반 중복 없는 고유 식별자를 자동 생성합니다.
        /// 인스펙터 우클릭 컨텍스트 메뉴에서도 발급할 수 있습니다.
        /// </summary>
        [ContextMenu("Generate Unique ID")]
        public void GenerateUniqueID()
        {
            string cleanName = gameObject.name.Replace(" ", "_");
            _enemyID = $"Enemy_{cleanName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
            Debug.Log($"[EnemySymbol] 🔑 고유 ID 발급: {_enemyID} ({gameObject.name})");
        }

        /// <summary>
        /// 특정 지정 ID로 초기화합니다 (스크립트 생성 및 테스트용).
        /// </summary>
        public void Initialize(string customID)
        {
            if (!string.IsNullOrEmpty(customID))
            {
                _enemyID = customID;
            }
            else
            {
                GenerateUniqueID();
            }
        }

        /// <summary>
        /// [세부 개발 명세 1] 인카운터 진입 시 자신의 EnemyID를 영속 데이터 매니저의 CurrentEngagedEnemyID로 등록합니다.
        /// </summary>
        public void EngageBattle()
        {
            if (GameManagerData.Instance != null && !string.IsNullOrEmpty(_enemyID))
            {
                GameManagerData.Instance.CurrentEngagedEnemyID = _enemyID;
                Debug.Log($"[EnemySymbol] ⚔️ 인카운터 대상 ID 등록 완료: {_enemyID}");
            }
        }

        /// <summary>
        /// [세부 개발 명세 4] GameManagerData를 조회하여 이미 처치된 적 심볼이면 필드에서 비활성화합니다.
        /// </summary>
        public bool CheckDefeatedState()
        {
            if (GameManagerData.Instance != null && !string.IsNullOrEmpty(_enemyID))
            {
                if (GameManagerData.Instance.IsEnemyDefeated(_enemyID))
                {
                    gameObject.SetActive(false);
                    Debug.Log($"[EnemySymbol] 👻 처치 완료된 적 심볼 비활성화 처리: {_enemyID} ({gameObject.name})");
                    return true;
                }
            }
            return false;
        }
    }
}
