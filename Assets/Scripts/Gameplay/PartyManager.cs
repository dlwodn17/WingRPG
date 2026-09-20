using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPG25D.Core.Battle;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.Gameplay
{
    /// <summary>
    /// [요구 산출물 1] PartyManager.cs
    /// 아군 3인 vs 적군 3인의 파티 데이터 모델, 3D 필드 배치(X-Z 평면), 속도(Speed) 기반 턴 순서 타임라인,
    /// 생존/전멸 상태 판정 및 월드 오브젝트 연동을 총괄 관리하는 매니저 클래스입니다.
    /// CLI 헤드리스 환경에서는 3D GameObject 생성 없이 순수 데이터 모델만으로 즉시 시뮬레이션 가능합니다.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        [Header("아군 파티 설정 (3인)")]
        [SerializeField] private List<CharacterDataSO> _allyDataList = new List<CharacterDataSO>();

        [Header("적군 파티 설정 (3인)")]
        [SerializeField] private List<CharacterDataSO> _enemyDataList = new List<CharacterDataSO>();

        [Header("3D 필드 배치 좌표 (X-Z 평면)")]
        [SerializeField] private Vector3[] _allySpawnPositions = new Vector3[]
        {
            new Vector3(-3.2f, 1.0f,  1.4f),  // 아군 1 (상단 후열)
            new Vector3(-4.0f, 1.0f,  0.0f),  // 아군 2 (중앙 본대)
            new Vector3(-3.2f, 1.0f, -1.4f)   // 아군 3 (하단 후열)
        };

        [SerializeField] private Vector3[] _enemySpawnPositions = new Vector3[]
        {
            new Vector3( 2.8f, 1.0f,  1.4f),  // 적군 1 (상단 전열)
            new Vector3( 3.8f, 1.0f,  0.0f),  // 적군 2 (중앙 본대)
            new Vector3( 2.8f, 1.0f, -1.4f)   // 적군 3 (하단 전열)
        };

        // 런타임 액터 목록
        private readonly List<BattleActor> _allies = new List<BattleActor>();
        private readonly List<BattleActor> _enemies = new List<BattleActor>();

        // 액터와 3D 월드 게임오브젝트 간 매핑
        private readonly Dictionary<BattleActor, GameObject> _actorObjectMap = new Dictionary<BattleActor, GameObject>();
        private GameObject _partyRootObject;

        // 액터별 현재 턴 표시 인디케이터
        private readonly Dictionary<BattleActor, GameObject> _turnIndicatorMap = new Dictionary<BattleActor, GameObject>();

        public IReadOnlyList<BattleActor> Allies => _allies;
        public IReadOnlyList<BattleActor> Enemies => _enemies;

        public bool IsAlliesDefeated => _allies.Count > 0 && _allies.All(a => !a.IsAlive);
        public bool IsEnemiesDefeated => _enemies.Count > 0 && _enemies.All(a => !a.IsAlive);

        public event Action OnPartyUpdated;

        /// <summary>
        /// 아군 3인 및 적군 3인 파티를 초기화하고 3D 필드에 배치합니다.
        /// </summary>
        public void InitializeParties(List<CharacterDataSO> customAllies = null, List<CharacterDataSO> customEnemies = null)
        {
            ClearParties();

            if (customAllies != null && customAllies.Count > 0) _allyDataList = customAllies;
            if (customEnemies != null && customEnemies.Count > 0) _enemyDataList = customEnemies;

            // 1. 아군 3인 생성
            EnsureDefaultAllyData();
            for (int i = 0; i < _allyDataList.Count && i < 3; i++)
            {
                var data = _allyDataList[i];
                var actor = data.CreateActor(10 + i);
                _allies.Add(actor);
            }

            // 2. 적군 3인 생성
            EnsureDefaultEnemyData();
            for (int i = 0; i < _enemyDataList.Count && i < 3; i++)
            {
                var data = _enemyDataList[i];
                var actor = data.CreateActor(100 + i);
                _enemies.Add(actor);
            }

            // 3. 그래픽 및 PlayMode 환경인 경우 3D 필드에 2.5D 빌보드 액터 배치
            if (Application.isPlaying && !Application.isBatchMode && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                SpawnPartyVisuals();
            }

            // 4. 액터 이벤트 구독
            foreach (var actor in _allies.Concat(_enemies))
            {
                actor.OnDeath += () => HandleActorDeath(actor);
                actor.OnHPChanged += (cur, max) => OnPartyUpdated?.Invoke();
            }

            OnPartyUpdated?.Invoke();
        }

        private void ClearParties()
        {
            _allies.Clear();
            _enemies.Clear();
            _actorObjectMap.Clear();
            _turnIndicatorMap.Clear();

            if (_partyRootObject != null)
            {
                if (Application.isPlaying) Destroy(_partyRootObject);
                else DestroyImmediate(_partyRootObject);
            }
        }

        private void EnsureDefaultAllyData()
        {
            if (_allyDataList == null) _allyDataList = new List<CharacterDataSO>();
            while (_allyDataList.Count < 3)
            {
                int idx = _allyDataList.Count + 1;
                var fallback = ScriptableObject.CreateInstance<CharacterDataSO>();
                if (idx == 1)
                {
                    fallback.characterName = "용사 (Knight)";
                    fallback.isPlayer = true;
                    fallback.maxHP = 130;
                    fallback.baseAttack = 16;
                    fallback.speed = 12;
                    fallback.themeColor = new Color(0.2f, 0.7f, 1.0f);
                }
                else if (idx == 2)
                {
                    fallback.characterName = "마법사 (Mage)";
                    fallback.isPlayer = true;
                    fallback.maxHP = 90;
                    fallback.baseAttack = 22;
                    fallback.speed = 15; // 고속
                    fallback.themeColor = new Color(0.8f, 0.3f, 1.0f);
                }
                else
                {
                    fallback.characterName = "사제 (Cleric)";
                    fallback.isPlayer = true;
                    fallback.maxHP = 110;
                    fallback.baseAttack = 12;
                    fallback.speed = 9;
                    fallback.themeColor = new Color(0.2f, 1.0f, 0.6f);
                }
                _allyDataList.Add(fallback);
            }
        }

        private void EnsureDefaultEnemyData()
        {
            if (_enemyDataList == null) _enemyDataList = new List<CharacterDataSO>();
            while (_enemyDataList.Count < 3)
            {
                int idx = _enemyDataList.Count + 1;
                var fallback = ScriptableObject.CreateInstance<CharacterDataSO>();
                if (idx == 1)
                {
                    fallback.characterName = "고블린 돌격병";
                    fallback.isPlayer = false;
                    fallback.maxHP = 65;
                    fallback.baseAttack = 11;
                    fallback.speed = 14; // 빠른 선제공격형
                    fallback.themeColor = new Color(0.9f, 0.3f, 0.2f);
                }
                else if (idx == 2)
                {
                    fallback.characterName = "오크 광전사";
                    fallback.isPlayer = false;
                    fallback.maxHP = 95;
                    fallback.baseAttack = 15;
                    fallback.speed = 8;  // 둔중한 파워형
                    fallback.themeColor = new Color(0.85f, 0.45f, 0.1f);
                }
                else
                {
                    fallback.characterName = "산성 슬라임";
                    fallback.isPlayer = false;
                    fallback.maxHP = 50;
                    fallback.baseAttack = 9;
                    fallback.speed = 10;
                    fallback.themeColor = new Color(0.3f, 0.85f, 0.35f);
                }
                _enemyDataList.Add(fallback);
            }
        }

        private void SpawnPartyVisuals()
        {
            _partyRootObject = new GameObject("[Party_Battle_Field]");

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. 아군 3인 비주얼 생성
            for (int i = 0; i < _allies.Count; i++)
            {
                var actor = _allies[i];
                var data = _allyDataList[i];
                Vector3 pos = i < _allySpawnPositions.Length ? _allySpawnPositions[i] : new Vector3(-3.5f, 1f, (i - 1) * 1.5f);

                var actorGo = CreateBillboardCharacterGameObject(actor.Name, pos, data.themeColor, litShader, true);
                actorGo.transform.parent = _partyRootObject.transform;
                _actorObjectMap[actor] = actorGo;

                CreateTurnIndicator(actor, actorGo);
            }

            // 2. 적군 3인 비주얼 생성
            for (int i = 0; i < _enemies.Count; i++)
            {
                var actor = _enemies[i];
                var data = _enemyDataList[i];
                Vector3 pos = i < _enemySpawnPositions.Length ? _enemySpawnPositions[i] : new Vector3(3.0f, 1f, (i - 1) * 1.5f);

                var actorGo = CreateBillboardCharacterGameObject(actor.Name, pos, data.themeColor, litShader, false);
                actorGo.transform.parent = _partyRootObject.transform;
                _actorObjectMap[actor] = actorGo;

                CreateTurnIndicator(actor, actorGo);
            }
        }

        private GameObject CreateBillboardCharacterGameObject(string name, Vector3 pos, Color color, Shader shader, bool isAlly)
        {
            var root = new GameObject($"Actor_{(isAlly ? "Ally" : "Enemy")}_{name}");
            root.transform.position = pos;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Visual";
            visual.transform.parent = root.transform;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(1.2f, 1.9f, 0.1f);

            var mr = visual.GetComponent<MeshRenderer>();
            var mat = new Material(shader) { color = color };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            root.AddComponent<BillboardActor25D>();
            return root;
        }

        private void CreateTurnIndicator(BattleActor actor, GameObject parentGo)
        {
            var indGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indGo.name = "ActiveTurnIndicator";
            indGo.transform.parent = parentGo.transform;
            indGo.transform.localPosition = new Vector3(0f, -0.92f, 0f);
            var col = indGo.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var indMat = new Material(unlit)
            {
                color = actor.IsPlayer ? new Color(0.2f, 0.8f, 1f, 0.8f) : new Color(1f, 0.3f, 0.2f, 0.8f)
            };
            indGo.GetComponent<MeshRenderer>().sharedMaterial = indMat;
            indGo.SetActive(false);

            _turnIndicatorMap[actor] = indGo;
        }

        /// <summary>
        /// 속도(Speed) 스탯 기반으로 현재 라운드의 턴 실행 순서를 계산합니다.
        /// 속도가 높은 캐릭터가 먼저 행동하며, 동률 시 아군 우선/ID 순으로 정렬합니다.
        /// </summary>
        public List<BattleActor> CalculateTurnOrder()
        {
            return _allies.Concat(_enemies)
                .Where(a => a.IsAlive)
                .OrderByDescending(a => a.Speed)
                .ThenByDescending(a => a.IsPlayer)
                .ThenBy(a => a.Id)
                .ToList();
        }

        /// <summary>
        /// 현재 행동 중인 액터에게 '행동 중' 시각 표시를 켭니다.
        /// </summary>
        public void SetActiveTurnActor(BattleActor activeActor)
        {
            foreach (var kvp in _turnIndicatorMap)
            {
                if (kvp.Value != null)
                {
                    bool isActive = kvp.Key == activeActor && kvp.Key.IsAlive;
                    kvp.Value.SetActive(isActive);
                }
            }
        }

        public GameObject GetActorGameObject(BattleActor actor)
        {
            if (actor != null && _actorObjectMap.TryGetValue(actor, out var go))
            {
                return go;
            }
            return null;
        }

        public Vector3 GetActorWorldPosition(BattleActor actor)
        {
            var go = GetActorGameObject(actor);
            if (go != null) return go.transform.position;
            return Vector3.zero;
        }

        private void HandleActorDeath(BattleActor deadActor)
        {
            if (_turnIndicatorMap.TryGetValue(deadActor, out var ind) && ind != null)
            {
                ind.SetActive(false);
            }

            var go = GetActorGameObject(deadActor);
            if (go != null)
            {
                // 쓰러짐 및 페이드 연출
                StartCoroutine(ActorDeathRoutine(go));
            }

            OnPartyUpdated?.Invoke();
        }

        private System.Collections.IEnumerator ActorDeathRoutine(GameObject actorGo)
        {
            float el = 0f;
            float dur = 0.5f;
            Vector3 startScale = actorGo.transform.localScale;

            while (el < dur)
            {
                el += Time.deltaTime;
                float progress = el / dur;
                // 바닥으로 쓰러지며 축소
                actorGo.transform.localScale = Vector3.Lerp(startScale, new Vector3(startScale.x, 0.1f, startScale.z), progress);
                yield return null;
            }

            actorGo.SetActive(false);
        }
    }
}
