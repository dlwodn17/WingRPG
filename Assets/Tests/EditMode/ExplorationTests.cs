using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Core.Exploration;
using RPG25D.Data;
using RPG25D.Data.Events;
using RPG25D.Gameplay;
using RPG25D.Gameplay.Exploration;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 4] ExplorationTests.cs
    /// 2.5D 탐색 이동 모델, 그리드 변환, 심볼 인카운터 감지 및 이벤트 SO 영속성을 헤드리스 환경에서 검증하는 단위 테스트
    /// </summary>
    [TestFixture]
    public class ExplorationTests
    {
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManager = ScriptableObject.CreateInstance<GameManager>();
            _gameManager.ResetGameData();
            GameManager.Instance = _gameManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManager != null)
            {
                Object.DestroyImmediate(_gameManager);
            }
        }

        #region 1. 2.5D 탐색 이동 도메인 모델 검증

        [Test]
        public void MovementModel_NormalizeDiagonalInput_KeepsUnitMagnitude()
        {
            var model = new ExplorationMovementModel(Vector3.zero, speed: 5.0f);
            Vector2 diagonalInput = new Vector2(1.0f, 1.0f);

            model.UpdateMovement(diagonalInput, deltaTime: 1.0f);

            // 대각선 이동 시 방향 벡터의 크기는 1.0이어야 함
            Assert.AreEqual(1.0f, model.MoveDirection.magnitude, 0.001f);
            Assert.IsTrue(model.IsMoving);
            Assert.AreEqual(5.0f, model.Position.magnitude, 0.01f);
        }

        [Test]
        public void MovementModel_UpdatesPositionBasedOnSpeedAndDeltaTime()
        {
            var startPos = new Vector3(2f, 1f, 3f);
            var model = new ExplorationMovementModel(startPos, speed: 4.0f);

            // 전방(+Z) 이동 0.5초 수행 -> 2.0m 전진 기대
            model.UpdateMovement(new Vector2(0f, 1f), deltaTime: 0.5f);

            Assert.AreEqual(2.0f, model.Position.x, 0.001f);
            Assert.AreEqual(1.0f, model.Position.y, 0.001f);
            Assert.AreEqual(5.0f, model.Position.z, 0.001f);
        }

        [Test]
        public void MovementModel_ClampsPositionWithinMapBounds()
        {
            var model = new ExplorationMovementModel(Vector3.zero, speed: 10.0f);
            model.MapBounds = new Bounds(Vector3.zero, new Vector3(10f, 5f, 10f)); // min: -5, max: +5

            // 오른쪽으로 10m 이동 시도 -> +5에서 클램프되어야 함
            model.UpdateMovement(new Vector2(1f, 0f), deltaTime: 1.0f);

            Assert.AreEqual(5.0f, model.Position.x, 0.001f);
        }

        [TestCase(0f, 0f, 0, 0)]
        [TestCase(1.4f, 2.8f, 1, 2)]
        [TestCase(-3.1f, -4.6f, -2, -3)]
        public void MovementModel_WorldToGridAndGridToWorld_ConvertsAccurately(float wx, float wz, int gx, int gz)
        {
            var model = new ExplorationMovementModel(Vector3.zero);
            float tileSize = 1.5f;

            var gridPos = model.WorldToGrid(new Vector3(wx, 0f, wz), tileSize);
            Assert.AreEqual(gx, gridPos.x);
            Assert.AreEqual(gz, gridPos.y);

            var reconstructedWorld = model.GridToWorld(gridPos, tileSize);
            Assert.AreEqual(gx * tileSize, reconstructedWorld.x, 0.001f);
            Assert.AreEqual(gz * tileSize, reconstructedWorld.z, 0.001f);
        }

        [Test]
        public void MovementModel_FacingDirection_FlipsOnHorizontalMove()
        {
            var model = new ExplorationMovementModel(Vector3.zero);
            Assert.IsTrue(model.IsFacingRight);

            // 왼쪽 이동 -> IsFacingRight false
            model.UpdateMovement(new Vector2(-1f, 0f), 0.1f);
            Assert.IsFalse(model.IsFacingRight);

            // 오른쪽 이동 -> IsFacingRight true
            model.UpdateMovement(new Vector2(1f, 0f), 0.1f);
            Assert.IsTrue(model.IsFacingRight);
        }

        #endregion

        #region 2. 심볼 인카운터 감지 및 판정 검증

        [Test]
        public void EncounterDetection_Triggered_WhenWithinRadius()
        {
            var detector = new EncounterDetectionModel();
            Vector3 playerPos = new Vector3(2.0f, 1.0f, 3.0f);
            Vector3 enemyPos = new Vector3(2.5f, 1.0f, 3.5f); // 거리 sqrt(0.5) ~ 0.707m

            bool inRange = detector.CheckEncounter(playerPos, enemyPos, triggerDistance: 1.2f);
            Assert.IsTrue(inRange);
        }

        [Test]
        public void EncounterDetection_NotTriggered_WhenOutsideRadius()
        {
            var detector = new EncounterDetectionModel();
            Vector3 playerPos = new Vector3(0f, 1f, 0f);
            Vector3 enemyPos = new Vector3(3f, 1f, 4f); // 거리 5.0m

            bool inRange = detector.CheckEncounter(playerPos, enemyPos, triggerDistance: 1.5f);
            Assert.IsFalse(inRange);
        }

        [Test]
        public void EncounterDetection_IgnoreDefeatedEncounters()
        {
            var detector = new EncounterDetectionModel();
            var defeatedIds = new HashSet<int> { 1, 3 };

            Assert.IsFalse(detector.IsEncounterActive(1, defeatedIds), "처치된 인카운터 1은 비활성이어야 합니다.");
            Assert.IsTrue(detector.IsEncounterActive(2, defeatedIds), "처치되지 않은 인카운터 2는 활성이어야 합니다.");
            Assert.IsFalse(detector.IsEncounterActive(3, defeatedIds), "처치된 인카운터 3은 비활성이어야 합니다.");
        }

        [Test]
        public void EncounterDetection_DetectsNearestEncounterAmongMultipleSymbols()
        {
            var detector = new EncounterDetectionModel();
            Vector3 playerPos = new Vector3(0f, 1f, 0f);

            var symbols = new List<EncounterSymbolData>
            {
                new EncounterSymbolData(1, "고블린 A", new Vector3(5f, 1f, 0f), radius: 1.5f), // 범위 밖
                new EncounterSymbolData(2, "오크 B", new Vector3(1.2f, 1f, 0f), radius: 1.5f),  // 거리 1.2m (범위 안)
                new EncounterSymbolData(3, "슬라임 C", new Vector3(0.8f, 1f, 0f), radius: 1.5f) // 거리 0.8m (최단 거리)
            };

            int? detectedId = detector.DetectNearestEncounter(playerPos, symbols, defeatedIds: null);
            Assert.AreEqual(3, detectedId, "가장 가까운 거리의 슬라임 C(ID 3)가 검출되어야 합니다.");

            // 슬라임 C가 처치된 경우 -> 그 다음 가까운 오크 B(ID 2)가 검출되어야 함
            var defeated = new HashSet<int> { 3 };
            int? nextDetected = detector.DetectNearestEncounter(playerPos, symbols, defeated);
            Assert.AreEqual(2, nextDetected, "슬라임 C 처치 후에는 오크 B(ID 2)가 검출되어야 합니다.");
        }

        #endregion

        #region 3. 탐색 이벤트 ScriptableObject 실행 및 보상 검증

        [Test]
        public void TreasureChestEvent_ExecutesAndGrantsRewards()
        {
            var chestSO = ScriptableObject.CreateInstance<TreasureChestEventSO>();
            chestSO.Initialize("CHEST_TEST_01", "시험의 보물상자", "성스러운 비약", gold: 300);

            var context = new ExplorationContext(Vector3.zero, initialGold: 50);

            Assert.IsTrue(chestSO.CanExecute(context));
            var result = chestSO.Execute(context);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("CHEST_TEST_01", result.EventId);
            Assert.AreEqual("성스러운 비약", result.AwardedItem);
            Assert.AreEqual(300, result.AwardedGold);

            // 컨텍스트에 아이템과 골드가 반영되었는지 검증
            Assert.Contains("성스러운 비약", context.Inventory);
            Assert.AreEqual(350, context.Gold);

            // GameManager 영속 데이터에도 반영되었는지 검증
            Assert.IsTrue(_gameManager.IsEventTriggered("CHEST_TEST_01"));
            Assert.IsTrue(_gameManager.InventoryItems.Contains("성스러운 비약"));
            Assert.AreEqual(100 + 300, _gameManager.CurrentGold);

            Object.DestroyImmediate(chestSO);
        }

        [Test]
        public void TreasureChestEvent_CannotExecuteTwice()
        {
            var chestSO = ScriptableObject.CreateInstance<TreasureChestEventSO>();
            chestSO.Initialize("CHEST_ONCE", "일회성 상자", "철 검", gold: 100);

            var context = new ExplorationContext(Vector3.zero);

            // 1회차: 성공
            var res1 = chestSO.Execute(context);
            Assert.IsTrue(res1.Success);

            // 2회차: 중복 실행 거부
            Assert.IsFalse(chestSO.CanExecute(context));
            var res2 = chestSO.Execute(context);
            Assert.IsFalse(res2.Success);

            Object.DestroyImmediate(chestSO);
        }

        [Test]
        public void NPCInteractionEvent_ExecutesMultipleTimes()
        {
            var npcSO = ScriptableObject.CreateInstance<NPCInteractionEventSO>();
            npcSO.Initialize("NPC_ELDER", "장로", new[] { "용사여, 마을을 구해다오." });

            var context = new ExplorationContext(Vector3.zero);

            var res1 = npcSO.Execute(context);
            Assert.IsTrue(res1.Success);

            // NPC 대화는 중복 실행 가능
            Assert.IsTrue(npcSO.CanExecute(context));
            var res2 = npcSO.Execute(context);
            Assert.IsTrue(res2.Success);

            Object.DestroyImmediate(npcSO);
        }

        #endregion

        #region 4. GameManager 데이터 영속성 검증

        [Test]
        public void GameManager_SavesAndRestoresPlayerPosition()
        {
            Vector3 testPos = new Vector3(-4.5f, 1.2f, 18.0f);

            Assert.IsFalse(_gameManager.HasSavedPosition);
            _gameManager.SavePlayerPosition(testPos);

            Assert.IsTrue(_gameManager.HasSavedPosition);
            Assert.AreEqual(testPos, _gameManager.SavedPlayerPosition);
        }

        [Test]
        public void GameManager_TracksDefeatedEncounters()
        {
            Assert.IsFalse(_gameManager.IsEncounterDefeated(7));

            _gameManager.MarkEncounterDefeated(7);
            Assert.IsTrue(_gameManager.IsEncounterDefeated(7));
            Assert.IsTrue(_gameManager.DefeatedEncounterIds.Contains(7));
        }

        [Test]
        public void GameManager_BattleVictory_MarksLastEncounterAsDefeated()
        {
            _gameManager.EnterBattle(10);
            Assert.AreEqual(10, _gameManager.LastEncounterId);
            Assert.IsTrue(_gameManager.IsInBattle);

            _gameManager.OnBattleVictory();
            Assert.IsFalse(_gameManager.IsInBattle);
            Assert.IsTrue(_gameManager.IsEncounterDefeated(10));
        }

        [Test]
        public void GameManager_DefeatedEnemyIDs_TracksStringAndIntSynchronously()
        {
            Assert.IsFalse(_gameManager.IsEncounterDefeated("Symbol_Goblin_01"));
            Assert.IsFalse(_gameManager.IsEncounterDefeated(1));

            _gameManager.MarkEncounterDefeated("Symbol_Goblin_01");

            Assert.IsTrue(_gameManager.IsEncounterDefeated("Symbol_Goblin_01"));
            Assert.IsTrue(_gameManager.DefeatedEnemyIDs.Contains("Symbol_Goblin_01"));
            Assert.IsTrue(_gameManager.IsEncounterDefeated(1));
            Assert.IsTrue(_gameManager.DefeatedEncounterIds.Contains(1));
        }

        [Test]
        public void BattleTurnController_RecordBattleVictory_RegistersDefeatedEnemyID()
        {
            var go = new GameObject("TestBattleController");
            var turnCtrl = go.AddComponent<BattleTurnController>();
            turnCtrl.TriggeringEnemyID = "Symbol_Orc_02";

            Assert.IsFalse(_gameManager.DefeatedEnemyIDs.Contains("Symbol_Orc_02"));

            turnCtrl.RecordBattleVictory();

            Assert.IsTrue(_gameManager.DefeatedEnemyIDs.Contains("Symbol_Orc_02"));
            Assert.IsTrue(_gameManager.IsEncounterDefeated("Symbol_Orc_02"));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EnemySymbolActor_CheckDefeatedState_DeactivatesGameObjectWhenDefeated()
        {
            var go = new GameObject("Symbol_Slime_03");
            var actor = go.AddComponent<EnemySymbolActor>();
            actor.Initialize(3, "산성 슬라임", 1.3f, "Symbol_Slime_03");

            Assert.IsTrue(go.activeSelf);

            _gameManager.MarkEncounterDefeated("Symbol_Slime_03");
            actor.CheckDefeatedState();

            Assert.IsFalse(go.activeSelf);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EncounterManager_RefreshDefeatedSymbols_DeactivatesMatchingActors()
        {
            var managerGo = new GameObject("TestEncounterManager");
            var manager = managerGo.AddComponent<EncounterManager>();

            var sym1Go = new GameObject("Symbol_Goblin_01");
            var sym1 = sym1Go.AddComponent<EnemySymbolActor>();
            sym1.Initialize(1, "고블린 정찰병", 1.4f, "Symbol_Goblin_01");

            var sym2Go = new GameObject("Symbol_Orc_02");
            var sym2 = sym2Go.AddComponent<EnemySymbolActor>();
            sym2.Initialize(2, "오크 돌격병", 1.5f, "Symbol_Orc_02");

            manager.CollectSymbolsInScene();
            Assert.AreEqual(2, manager.Symbols.Count);

            // 1번 심볼만 처치
            _gameManager.MarkEncounterDefeated("Symbol_Goblin_01");
            manager.RefreshDefeatedSymbols();

            Assert.IsFalse(sym1Go.activeSelf);
            Assert.IsTrue(sym2Go.activeSelf);

            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(sym1Go);
            Object.DestroyImmediate(sym2Go);
        }

        #endregion
    }
}
