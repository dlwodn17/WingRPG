using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG25D.Data
{
    /// <summary>
    /// [요구 산출물 2] GameManagerData.cs
    /// 탐색 씬과 전투 씬 간 전이 시 플레이어 위치, 처치된 적 심볼 ID 목록, 골드, 인벤토리 상태를 관리하는 영속성 데이터 저장소입니다.
    /// ScriptableObject 및 싱글톤 인스턴스로 관리되며, CLI 헤드리스 환경에서도 독립 인스턴스로 검증 가능합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameManagerData", menuName = "RPG25D/GameManagerData")]
    public class GameManagerData : ScriptableObject
    {
        private static GameManagerData _runtimeInstance;

        public static GameManagerData Instance
        {
            get
            {
                if (_runtimeInstance == null)
                {
#if UNITY_EDITOR
                    _runtimeInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameManagerData>("Assets/Data/GameManagerData.asset");
#endif
                    if (_runtimeInstance == null)
                    {
                        _runtimeInstance = Resources.Load<GameManagerData>("GameManager");
                    }
                    if (_runtimeInstance == null)
                    {
                        var found = Resources.FindObjectsOfTypeAll<GameManagerData>();
                        if (found != null && found.Length > 0)
                        {
                            _runtimeInstance = found[0];
                        }
                    }
                    if (_runtimeInstance == null)
                    {
                        _runtimeInstance = CreateInstance<GameManagerData>();
                        _runtimeInstance.name = "[Runtime_GameManagerData]";
                    }
                }
                return _runtimeInstance;
            }
            set => _runtimeInstance = value;
        }

        [Header("씬 설정")]
        [SerializeField] protected string _explorationSceneName = "Level01_Exploration";
        [SerializeField] protected string _battleSceneName = "SampleScene";

        [Header("영속 런타임 데이터")]
        [SerializeField] protected Vector3 _savedPlayerPosition = new Vector3(0f, 1f, 0f);
        [SerializeField] protected bool _hasSavedPosition = false;
        [SerializeField] protected int _currentGold = 100;
        [SerializeField] protected List<string> _inventoryItems = new List<string>();
        [SerializeField] protected List<int> _defeatedEncounterIds = new List<int>();

        [Header("적 처치 영속성 데이터 (세부 개발 명세 2)")]
        [Tooltip("현재 전투 중인 적 심볼 고유 ID")]
        public string CurrentEngagedEnemyID = string.Empty;

        [Tooltip("처치 완료된 적 심볼 고유 ID 목록")]
        public List<string> DefeatedEnemyIDs = new List<string>();

        [SerializeField] protected List<string> _triggeredEventIds = new List<string>();

        // 런타임 빠른 검색용 캐시
        protected readonly HashSet<string> _defeatedEnemyIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        protected readonly HashSet<int> _defeatedEncounterSet = new HashSet<int>();
        protected readonly HashSet<string> _triggeredEventSet = new HashSet<string>();

        // 공개 프로퍼티
        public string ExplorationSceneName => _explorationSceneName;
        public string BattleSceneName => _battleSceneName;
        public Vector3 SavedPlayerPosition => _savedPlayerPosition;
        public bool HasSavedPosition => _hasSavedPosition;
        public int CurrentGold => _currentGold;
        public IReadOnlyList<string> InventoryItems => _inventoryItems;
        public IReadOnlyCollection<int> DefeatedEncounterIds => _defeatedEncounterSet;
        public IReadOnlyCollection<string> DefeatedEnemyIDSet => _defeatedEnemyIdSet;
        public IReadOnlyCollection<string> TriggeredEventIds => _triggeredEventSet;

        // 하위 호환성 프로퍼티
        public string CurrentEnemyID
        {
            get => CurrentEngagedEnemyID;
            set => CurrentEngagedEnemyID = value;
        }
        public int LastEncounterId { get; protected set; } = -1;
        public bool IsInBattle { get; protected set; } = false;

        public event Action<int> OnEncounterDefeated;
        public event Action<string> OnEnemyDefeatedString;
        public event Action<string, int> OnItemAcquired;
        public event Action<string> OnBattleEntered;

        protected virtual void OnEnable()
        {
            SyncHashSetFromLists();
        }

        public virtual void SyncHashSetFromLists()
        {
            _defeatedEnemyIdSet.Clear();
            if (DefeatedEnemyIDs != null)
            {
                foreach (var id in DefeatedEnemyIDs)
                {
                    if (!string.IsNullOrEmpty(id)) _defeatedEnemyIdSet.Add(id);
                }
            }

            _defeatedEncounterSet.Clear();
            if (_defeatedEncounterIds != null)
            {
                foreach (var id in _defeatedEncounterIds) _defeatedEncounterSet.Add(id);
            }

            _triggeredEventSet.Clear();
            if (_triggeredEventIds != null)
            {
                foreach (var ev in _triggeredEventIds) _triggeredEventSet.Add(ev);
            }
        }

        /// <summary>
        /// [요구 산출물 2] 적 처치 확정 기록: 지정된 적 ID를 DefeatedEnemyIDs에 등록합니다.
        /// </summary>
        public virtual void RecordDefeatedEnemy(string enemyID)
        {
            if (string.IsNullOrEmpty(enemyID)) return;

            if (!_defeatedEnemyIdSet.Contains(enemyID))
            {
                _defeatedEnemyIdSet.Add(enemyID);
                if (!DefeatedEnemyIDs.Contains(enemyID))
                {
                    DefeatedEnemyIDs.Add(enemyID);
                }
                OnEnemyDefeatedString?.Invoke(enemyID);
                Debug.Log($"[GameManagerData] 💀 적 심볼 처치 기록: {enemyID}");
            }

            // 정수 ID가 매핑되는 경우 int 집합에도 동기화
            if (int.TryParse(enemyID, out int intId))
            {
                SyncIntDefeated(intId);
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(enemyID, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int extractedId))
                {
                    SyncIntDefeated(extractedId);
                }
            }
        }

        /// <summary>
        /// [요구 산출물 2] 해당 적 ID가 이미 처치되었는지 검사합니다.
        /// </summary>
        public virtual bool IsEnemyDefeated(string enemyID)
        {
            if (string.IsNullOrEmpty(enemyID)) return false;

            if (_defeatedEnemyIdSet.Contains(enemyID) || DefeatedEnemyIDs.Contains(enemyID)) return true;

            if (int.TryParse(enemyID, out int intId))
            {
                return IsEncounterDefeated(intId);
            }

            var match = System.Text.RegularExpressions.Regex.Match(enemyID, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int extractedId))
            {
                if (_defeatedEncounterSet.Contains(extractedId) || _defeatedEncounterIds.Contains(extractedId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// [요구 산출물 2] 진행 상황 초기화 (새 게임 또는 디버깅용)
        /// </summary>
        public virtual void ResetProgress()
        {
            CurrentEngagedEnemyID = string.Empty;
            DefeatedEnemyIDs.Clear();
            _defeatedEnemyIdSet.Clear();

            _savedPlayerPosition = Vector3.zero;
            _hasSavedPosition = false;
            _currentGold = 100;
            _inventoryItems.Clear();
            _defeatedEncounterIds.Clear();
            _triggeredEventIds.Clear();
            _defeatedEncounterSet.Clear();
            _triggeredEventSet.Clear();
            LastEncounterId = -1;
            IsInBattle = false;
            Debug.Log("[GameManagerData] 🔄 게임 진행 데이터 초기화 완료 (ResetProgress)");
        }

        // 기존 코드 호환용 메서드
        public void MarkEncounterDefeated(string enemyId) => RecordDefeatedEnemy(enemyId);
        public bool IsEncounterDefeated(string enemyId) => IsEnemyDefeated(enemyId);
        public void ResetGameData() => ResetProgress();

        public virtual void MarkEncounterDefeated(int encounterId)
        {
            SyncIntDefeated(encounterId);

            string strId = encounterId.ToString();
            if (!_defeatedEnemyIdSet.Contains(strId))
            {
                _defeatedEnemyIdSet.Add(strId);
                if (!DefeatedEnemyIDs.Contains(strId))
                {
                    DefeatedEnemyIDs.Add(strId);
                }
            }

            string symbolKey = $"Symbol_Enemy_{encounterId}";
            if (!_defeatedEnemyIdSet.Contains(symbolKey))
            {
                _defeatedEnemyIdSet.Add(symbolKey);
                if (!DefeatedEnemyIDs.Contains(symbolKey))
                {
                    DefeatedEnemyIDs.Add(symbolKey);
                }
            }
        }

        protected void SyncIntDefeated(int encounterId)
        {
            if (!_defeatedEncounterSet.Contains(encounterId))
            {
                _defeatedEncounterSet.Add(encounterId);
                if (!_defeatedEncounterIds.Contains(encounterId))
                {
                    _defeatedEncounterIds.Add(encounterId);
                }
                OnEncounterDefeated?.Invoke(encounterId);
            }
        }

        public virtual bool IsEncounterDefeated(int encounterId)
        {
            if (_defeatedEncounterSet.Contains(encounterId) || _defeatedEncounterIds.Contains(encounterId)) return true;
            if (_defeatedEnemyIdSet.Contains(encounterId.ToString()) || DefeatedEnemyIDs.Contains(encounterId.ToString())) return true;
            return false;
        }

        public virtual void SavePlayerPosition(Vector3 position)
        {
            _savedPlayerPosition = position;
            _hasSavedPosition = true;
            Debug.Log($"[GameManagerData] 플레이어 위치 저장: {position}");
        }

        public virtual void EnterBattle(int encounterId, string customBattleScene = null, string enemyId = null)
        {
            LastEncounterId = encounterId;
            CurrentEngagedEnemyID = !string.IsNullOrEmpty(enemyId) ? enemyId : $"Symbol_Enemy_{encounterId}";
            IsInBattle = true;

            string targetScene = string.IsNullOrEmpty(customBattleScene) ? _battleSceneName : customBattleScene;
            Debug.Log($"[GameManagerData] ⚔️ 심볼 인카운터 조우 (ID: {encounterId}, EnemyID: {CurrentEngagedEnemyID}) ➔ 전투 씬 '{targetScene}' 로드 시작");

            OnBattleEntered?.Invoke(targetScene);

            if (Application.isPlaying && !Application.isBatchMode)
            {
                SceneManager.LoadScene(targetScene);
            }
        }

        public virtual void OnBattleVictory(string customExplorationScene = null)
        {
            if (!string.IsNullOrEmpty(CurrentEngagedEnemyID))
            {
                RecordDefeatedEnemy(CurrentEngagedEnemyID);
            }

            if (LastEncounterId >= 0)
            {
                MarkEncounterDefeated(LastEncounterId);
            }

            IsInBattle = false;
            string targetScene = string.IsNullOrEmpty(customExplorationScene) ? _explorationSceneName : customExplorationScene;
            Debug.Log($"[GameManagerData] 🏆 전투 승리! 처치 심볼({CurrentEngagedEnemyID}) 등록 완료 ➔ 탐색 씬 '{targetScene}' 복귀");

            // 복귀 전 CurrentEngagedEnemyID 클리어
            CurrentEngagedEnemyID = string.Empty;

            if (Application.isPlaying && !Application.isBatchMode)
            {
                SceneManager.LoadSceneAsync(targetScene);
            }
        }

        public virtual void RecordEventTriggered(string eventId)
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

        public virtual bool IsEventTriggered(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && _triggeredEventSet.Contains(eventId);
        }

        public virtual void AddItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            _inventoryItems.Add(itemName);
            Debug.Log($"[GameManagerData] 🎁 아이템 획득: {itemName}");
            OnItemAcquired?.Invoke(itemName, _currentGold);
        }

        public virtual void AddGold(int amount)
        {
            _currentGold += amount;
            Debug.Log($"[GameManagerData] 💰 골드 획득: +{amount} (현재 보유: {_currentGold} G)");
            OnItemAcquired?.Invoke(string.Empty, _currentGold);
        }
    }
}
