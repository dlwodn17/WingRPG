using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG25D.Data
{
    /// <summary>
    /// [요구 산출물 1] GameManager.cs (ScriptableObject 기반 데이터 영속성 관리자)
    /// 탐색 씬과 전투 씬 간 전이 시 플레이어 위치, 처치된 몬스터 심볼, 획득 아이템, 골드 상태를 영구 보존합니다.
    /// CLI 헤드리스 환경에서도 독립적으로 인스턴스화되어 로직 검증이 가능합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameManager", menuName = "RPG25D/GameManager")]
    public class GameManager : ScriptableObject
    {
        private static GameManager _runtimeInstance;

        public static GameManager Instance
        {
            get
            {
                if (_runtimeInstance == null)
                {
#if UNITY_EDITOR
                    _runtimeInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameManager>("Assets/Data/GameManagerData.asset");
#endif
                    if (_runtimeInstance == null)
                    {
                        _runtimeInstance = Resources.Load<GameManager>("GameManager");
                    }
                    if (_runtimeInstance == null)
                    {
                        var found = Resources.FindObjectsOfTypeAll<GameManager>();
                        if (found != null && found.Length > 0)
                        {
                            _runtimeInstance = found[0];
                        }
                    }

                    if (_runtimeInstance == null)
                    {
                        _runtimeInstance = CreateInstance<GameManager>();
                        _runtimeInstance.name = "[Runtime_GameManager]";
                    }
                }
                return _runtimeInstance;
            }
            set => _runtimeInstance = value;
        }

        [Header("씬 설정")]
        [SerializeField] private string _explorationSceneName = "Level01_Exploration";
        [SerializeField] private string _battleSceneName = "SampleScene";

        [Header("영속 런타임 데이터")]
        [SerializeField] private Vector3 _savedPlayerPosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private bool _hasSavedPosition = false;
        [SerializeField] private int _currentGold = 100;
        [SerializeField] private List<string> _inventoryItems = new List<string>();
        [SerializeField] private List<int> _defeatedEncounterIds = new List<int>();
        [SerializeField] private List<string> _defeatedEnemyIDs = new List<string>();
        [SerializeField] private List<string> _triggeredEventIds = new List<string>();

        // 런타임 캐시
        private readonly HashSet<int> _defeatedEncounterSet = new HashSet<int>();
        private readonly HashSet<string> _defeatedEnemyIdSet = new HashSet<string>();
        private readonly HashSet<string> _triggeredEventSet = new HashSet<string>();

        public Vector3 SavedPlayerPosition => _savedPlayerPosition;
        public bool HasSavedPosition => _hasSavedPosition;
        public int CurrentGold => _currentGold;
        public IReadOnlyList<string> InventoryItems => _inventoryItems;
        public IReadOnlyCollection<int> DefeatedEncounterIds => _defeatedEncounterSet;
        public List<string> DefeatedEnemyIDs => _defeatedEnemyIDs;
        public IReadOnlyCollection<string> DefeatedEnemyIDSet => _defeatedEnemyIdSet;
        public IReadOnlyCollection<string> TriggeredEventIds => _triggeredEventSet;

        public int LastEncounterId { get; private set; } = -1;
        public string CurrentEnemyID { get; set; } = string.Empty;
        public bool IsInBattle { get; private set; } = false;

        public event Action<int> OnEncounterDefeated;
        public event Action<string> OnEnemyDefeatedString;
        public event Action<string, int> OnItemAcquired;
        public event Action<string> OnBattleEntered;

        private void OnEnable()
        {
            SyncHashSetFromLists();
        }

        public void SyncHashSetFromLists()
        {
            _defeatedEncounterSet.Clear();
            if (_defeatedEncounterIds != null)
            {
                foreach (var id in _defeatedEncounterIds) _defeatedEncounterSet.Add(id);
            }

            _defeatedEnemyIdSet.Clear();
            if (_defeatedEnemyIDs != null)
            {
                foreach (var id in _defeatedEnemyIDs)
                {
                    if (!string.IsNullOrEmpty(id)) _defeatedEnemyIdSet.Add(id);
                }
            }

            _triggeredEventSet.Clear();
            if (_triggeredEventIds != null)
            {
                foreach (var ev in _triggeredEventIds) _triggeredEventSet.Add(ev);
            }
        }

        /// <summary>
        /// 탐색 중 플레이어의 마지막 위치를 저장합니다.
        /// </summary>
        public void SavePlayerPosition(Vector3 position)
        {
            _savedPlayerPosition = position;
            _hasSavedPosition = true;
            Debug.Log($"[GameManager] 플레이어 위치 저장: {position}");
        }

        /// <summary>
        /// 심볼 인카운터와 조우하여 전투 씬으로 전이를 시작합니다.
        /// </summary>
        public void EnterBattle(int encounterId, string customBattleScene = null, string enemyId = null)
        {
            LastEncounterId = encounterId;
            CurrentEnemyID = !string.IsNullOrEmpty(enemyId) ? enemyId : $"Symbol_Enemy_{encounterId}";
            IsInBattle = true;

            string targetScene = string.IsNullOrEmpty(customBattleScene) ? _battleSceneName : customBattleScene;
            Debug.Log($"[GameManager] ⚔️ 심볼 인카운터 조우 (ID: {encounterId}, EnemyID: {CurrentEnemyID}) ➔ 전투 씬 '{targetScene}' 로드 시작");

            OnBattleEntered?.Invoke(targetScene);

            if (Application.isPlaying && !Application.isBatchMode)
            {
                SceneManager.LoadScene(targetScene);
            }
        }

        /// <summary>
        /// 전투 승리 후 해당 인카운터 심볼을 영구 처치 목록에 등록하고 탐색 씬으로 복귀합니다.
        /// </summary>
        public void OnBattleVictory(string customExplorationScene = null)
        {
            if (!string.IsNullOrEmpty(CurrentEnemyID))
            {
                MarkEncounterDefeated(CurrentEnemyID);
            }

            if (LastEncounterId >= 0)
            {
                MarkEncounterDefeated(LastEncounterId);
            }

            IsInBattle = false;
            string targetScene = string.IsNullOrEmpty(customExplorationScene) ? _explorationSceneName : customExplorationScene;
            Debug.Log($"[GameManager] 🏆 전투 승리! 처치 심볼({CurrentEnemyID ?? LastEncounterId.ToString()}) 등록 완료 ➔ 탐색 씬 '{targetScene}' 복귀");

            if (Application.isPlaying && !Application.isBatchMode)
            {
                SceneManager.LoadSceneAsync(targetScene);
            }
        }

        /// <summary>
        /// string 기반 적 심볼 고유 ID를 처치 목록에 등록합니다.
        /// </summary>
        public void MarkEncounterDefeated(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;

            if (!_defeatedEnemyIdSet.Contains(enemyId))
            {
                _defeatedEnemyIdSet.Add(enemyId);
                if (!_defeatedEnemyIDs.Contains(enemyId))
                {
                    _defeatedEnemyIDs.Add(enemyId);
                }
                OnEnemyDefeatedString?.Invoke(enemyId);
                Debug.Log($"[GameManager] 💀 적 심볼 처치 등록(string): {enemyId}");
            }

            // int 파싱 가능한 경우 _defeatedEncounterIds에도 동기화
            if (int.TryParse(enemyId, out int intId))
            {
                SyncIntDefeated(intId);
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(enemyId, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int extractedId))
                {
                    SyncIntDefeated(extractedId);
                }
            }
        }

        /// <summary>
        /// int 기반 인카운터 ID를 처치 목록에 등록합니다.
        /// </summary>
        public void MarkEncounterDefeated(int encounterId)
        {
            SyncIntDefeated(encounterId);

            string strId = encounterId.ToString();
            if (!_defeatedEnemyIdSet.Contains(strId))
            {
                _defeatedEnemyIdSet.Add(strId);
                if (!_defeatedEnemyIDs.Contains(strId))
                {
                    _defeatedEnemyIDs.Add(strId);
                }
            }

            string symbolKey = $"Symbol_Enemy_{encounterId}";
            if (!_defeatedEnemyIdSet.Contains(symbolKey))
            {
                _defeatedEnemyIdSet.Add(symbolKey);
                if (!_defeatedEnemyIDs.Contains(symbolKey))
                {
                    _defeatedEnemyIDs.Add(symbolKey);
                }
            }
        }

        private void SyncIntDefeated(int encounterId)
        {
            if (!_defeatedEncounterSet.Contains(encounterId))
            {
                _defeatedEncounterSet.Add(encounterId);
                if (!_defeatedEncounterIds.Contains(encounterId))
                {
                    _defeatedEncounterIds.Add(encounterId);
                }
                OnEncounterDefeated?.Invoke(encounterId);
                Debug.Log($"[GameManager] 💀 적 심볼 처치 등록(int): {encounterId}");
            }
        }

        public bool IsEncounterDefeated(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return false;
            if (_defeatedEnemyIdSet.Contains(enemyId) || _defeatedEnemyIDs.Contains(enemyId)) return true;

            if (int.TryParse(enemyId, out int intId))
            {
                return IsEncounterDefeated(intId);
            }

            var match = System.Text.RegularExpressions.Regex.Match(enemyId, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int extractedId))
            {
                if (_defeatedEncounterSet.Contains(extractedId) || _defeatedEncounterIds.Contains(extractedId))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsEncounterDefeated(int encounterId)
        {
            if (_defeatedEncounterSet.Contains(encounterId) || _defeatedEncounterIds.Contains(encounterId)) return true;
            if (_defeatedEnemyIdSet.Contains(encounterId.ToString()) || _defeatedEnemyIDs.Contains(encounterId.ToString())) return true;
            return false;
        }

        public void RecordEventTriggered(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId) && !_triggeredEventSet.Contains(eventId))
            {
                _triggeredEventSet.Add(eventId);
                if (!_triggeredEventIds.Contains(eventId))
                {
                    _triggeredEventIds.Add(eventId);
                }
            }
        }

        public bool IsEventTriggered(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && _triggeredEventSet.Contains(eventId);
        }

        public void AddItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            _inventoryItems.Add(itemName);
            Debug.Log($"[GameManager] 🎁 아이템 획득: {itemName}");
            OnItemAcquired?.Invoke(itemName, _currentGold);
        }

        public void AddGold(int amount)
        {
            _currentGold += amount;
            Debug.Log($"[GameManager] 💰 골드 획득: +{amount} (현재 보유: {_currentGold} G)");
            OnItemAcquired?.Invoke(string.Empty, _currentGold);
        }

        /// <summary>
        /// 테스트 및 신규 게임 시작용 초기화
        /// </summary>
        public void ResetGameData()
        {
            _savedPlayerPosition = Vector3.zero;
            _hasSavedPosition = false;
            _currentGold = 100;
            _inventoryItems.Clear();
            _defeatedEncounterIds.Clear();
            _defeatedEnemyIDs.Clear();
            _triggeredEventIds.Clear();
            _defeatedEncounterSet.Clear();
            _defeatedEnemyIdSet.Clear();
            _triggeredEventSet.Clear();
            LastEncounterId = -1;
            CurrentEnemyID = string.Empty;
            IsInBattle = false;
        }
    }
}
