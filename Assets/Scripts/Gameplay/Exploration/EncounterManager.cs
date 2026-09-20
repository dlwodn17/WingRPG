using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Exploration;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.Gameplay.Exploration
{
    /// <summary>
    /// [요구 산출물 1] EncounterManager.cs
    /// 필드의 몬스터 심볼과 플레이어 간의 인카운터 판정을 총괄하며,
    /// 순수 C# 모델(IEncounterDetection)을 통해 단위 테스트 및 CLI 검증을 지원합니다.
    /// </summary>
    public class EncounterManager : MonoBehaviour
    {
        [Header("참조 설정")]
        [SerializeField] private ExplorationPlayerController _player;
        [SerializeField] private List<EnemySymbolActor> _symbols = new List<EnemySymbolActor>();

        [Header("전투 전이 설정")]
        [SerializeField] private string _battleSceneName = "SampleScene";
        [SerializeField] private float _transitionDelay = 0.5f;

        // 순수 C# 판정 모델
        private IEncounterDetection _detectionModel;
        private bool _isEncounterTriggered = false;

        public IEncounterDetection DetectionModel => _detectionModel;
        public IReadOnlyList<EnemySymbolActor> Symbols => _symbols;
        public bool IsEncounterTriggered => _isEncounterTriggered;

        public event Action<int> OnEncounterTriggered;

        private void Awake()
        {
            _detectionModel = new EncounterDetectionModel();
            if (_player == null)
            {
                _player = FindAnyObjectByType<ExplorationPlayerController>();
            }
            CollectSymbolsInScene();
        }

        private void OnEnable()
        {
            CollectSymbolsInScene();
            RefreshDefeatedSymbols();
        }

        private void Start()
        {
            RefreshDefeatedSymbols();
        }

        public void SetDetectionModel(IEncounterDetection model)
        {
            _detectionModel = model ?? new EncounterDetectionModel();
        }

        public void CollectSymbolsInScene()
        {
            var found = FindObjectsByType<EnemySymbolActor>(FindObjectsSortMode.None);
            _symbols.Clear();
            _symbols.AddRange(found);
        }

        /// <summary>
        /// [요구 산출물 4] GameManager의 DefeatedEnemyIDs를 조회하여 이미 처치된 적 심볼을 SetActive(false)로 비활성화합니다.
        /// </summary>
        public void RefreshDefeatedSymbols()
        {
            if (GameManager.Instance == null) return;

            var defeatedList = GameManager.Instance.DefeatedEnemyIDs;
            foreach (var symbol in _symbols)
            {
                if (symbol != null)
                {
                    bool isDefeated = (defeatedList != null && defeatedList.Contains(symbol.SymbolId)) ||
                                      GameManager.Instance.IsEncounterDefeated(symbol.SymbolId) ||
                                      GameManager.Instance.IsEncounterDefeated(symbol.EncounterId);

                    if (isDefeated)
                    {
                        symbol.gameObject.SetActive(false);
                        Debug.Log($"[EncounterManager] ❌ 처치된 적 심볼 비활성화: {symbol.SymbolId} (ID: {symbol.EncounterId})");
                    }
                    else
                    {
                        symbol.CheckDefeatedState();
                    }
                }
            }
        }

        private void Update()
        {
            if (_isEncounterTriggered || _player == null || !Application.isPlaying) return;

            // 플레이어 위치와 활성화된 적 심볼들 간 거리 판정
            CheckActiveEncounters(_player.transform.position);
        }

        /// <summary>
        /// 특정 위치를 기준으로 심볼 인카운터 발생 여부를 검사합니다 (CLI 테스트 지원).
        /// </summary>
        public int? CheckActiveEncounters(Vector3 playerPosition)
        {
            if (_detectionModel == null) _detectionModel = new EncounterDetectionModel();

            var activeSymbolsData = _symbols
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .Select(s => s.ToData())
                .ToList();

            IReadOnlyCollection<int> defeated = GameManager.Instance != null
                ? GameManager.Instance.DefeatedEncounterIds
                : Array.Empty<int>();

            int? triggeredId = _detectionModel.DetectNearestEncounter(playerPosition, activeSymbolsData, defeated);
            if (triggeredId.HasValue && !_isEncounterTriggered)
            {
                var targetActor = _symbols.FirstOrDefault(s => s.EncounterId == triggeredId.Value);
                TriggerEncounter(targetActor != null ? targetActor : null, triggeredId.Value);
            }

            return triggeredId;
        }

        /// <summary>
        /// 인카운터 발생 시 플레이어 위치를 영속 저장하고 전투 씬으로 전이합니다.
        /// </summary>
        public void TriggerEncounter(EnemySymbolActor symbol, int encounterId)
        {
            if (_isEncounterTriggered) return;
            _isEncounterTriggered = true;

            string monsterName = symbol != null ? symbol.MonsterName : $"Encounter_{encounterId}";
            string symbolId = symbol != null ? symbol.SymbolId : $"Symbol_Enemy_{encounterId}";

            Debug.Log($"==================================================");
            Debug.Log($"💥 [심볼 인카운터 발동!] 적 [{monsterName}] (ID: {encounterId}, SymbolId: {symbolId})과 충돌!");
            Debug.Log($"==================================================");

            // 1. 플레이어 위치 및 인카운터 ID 저장
            if (GameManager.Instance != null)
            {
                Vector3 playerPos = _player != null ? _player.transform.position : Vector3.zero;
                GameManager.Instance.SavePlayerPosition(playerPos);
                GameManager.Instance.CurrentEnemyID = symbolId;
            }

            // 2. 카메라 충격 셰이크 (존재 시)
            if (CameraShakeController.Instance != null)
            {
                CameraShakeController.Instance.Shake(0.5f, 0.4f);
            }

            OnEncounterTriggered?.Invoke(encounterId);

            // 3. 전투 씬 전환
            if (Application.isPlaying && !Application.isBatchMode)
            {
                StartCoroutine(TransitionToBattleRoutine(encounterId, symbolId));
            }
            else
            {
                // CLI 환경
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EnterBattle(encounterId, _battleSceneName, symbolId);
                }
            }
        }

        private System.Collections.IEnumerator TransitionToBattleRoutine(int encounterId, string symbolId)
        {
            yield return new WaitForSeconds(_transitionDelay);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.EnterBattle(encounterId, _battleSceneName, symbolId);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(_battleSceneName);
            }
        }
    }
}
