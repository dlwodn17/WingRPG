using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RPG25D.Data;
using RPG25D.Gameplay;
using RPG25D.Gameplay.Exploration;

namespace RPG25D.Tests
{
    /// <summary>
    /// [요구 산출물 2] EnemyPersistenceTests.cs
    /// 영속성 데이터 관리자(GameManagerData)의 적 처치 기록, 판정 및 씬 로드 시 적 심볼 비활성화 상태 동기화를 검증하는 단위 테스트
    /// </summary>
    [TestFixture]
    public class EnemyPersistenceTests
    {
        private GameManagerData _gameManagerData;

        [SetUp]
        public void SetUp()
        {
            _gameManagerData = ScriptableObject.CreateInstance<GameManagerData>();
            _gameManagerData.ResetProgress();
            GameManagerData.Instance = _gameManagerData;
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerData != null)
            {
                Object.DestroyImmediate(_gameManagerData);
            }
        }

        #region 1. GameManagerData 영속성 데이터 모델 검증 (세부 개발 명세 2)

        [Test]
        public void GameManagerData_RecordDefeatedEnemy_StoresIDAndReturnsTrue()
        {
            const string testEnemyID = "Enemy_Goblin_PERSIST_01";

            Assert.IsFalse(_gameManagerData.IsEnemyDefeated(testEnemyID), "처치 등록 전에는 처치 상태가 아니어야 합니다.");

            _gameManagerData.RecordDefeatedEnemy(testEnemyID);

            Assert.IsTrue(_gameManagerData.IsEnemyDefeated(testEnemyID), "RecordDefeatedEnemy 호출 후에는 IsEnemyDefeated가 true여야 합니다.");
            Assert.IsTrue(_gameManagerData.DefeatedEnemyIDs.Contains(testEnemyID), "DefeatedEnemyIDs 목록에 해당 ID가 포함되어야 합니다.");
        }

        [Test]
        public void GameManagerData_RecordDefeatedEnemy_PreventsDuplicateRegistrations()
        {
            const string testEnemyID = "Enemy_Orc_PERSIST_02";

            _gameManagerData.RecordDefeatedEnemy(testEnemyID);
            _gameManagerData.RecordDefeatedEnemy(testEnemyID);

            int count = _gameManagerData.DefeatedEnemyIDs.FindAll(id => id == testEnemyID).Count;
            Assert.AreEqual(1, count, "동일한 적 ID를 여러 번 처치 등록해도 중복 추가되지 않아야 합니다.");
        }

        [Test]
        public void GameManagerData_ResetProgress_ClearsAllDefeatedDataAndEngagedID()
        {
            _gameManagerData.CurrentEngagedEnemyID = "Enemy_Boss_01";
            _gameManagerData.RecordDefeatedEnemy("Enemy_Boss_01");
            _gameManagerData.RecordDefeatedEnemy("Enemy_Minion_02");

            Assert.AreEqual(2, _gameManagerData.DefeatedEnemyIDs.Count);
            Assert.AreEqual("Enemy_Boss_01", _gameManagerData.CurrentEngagedEnemyID);

            _gameManagerData.ResetProgress();

            Assert.AreEqual(0, _gameManagerData.DefeatedEnemyIDs.Count, "ResetProgress 호출 시 처치 목록이 모두 비워져야 합니다.");
            Assert.IsEmpty(_gameManagerData.CurrentEngagedEnemyID, "CurrentEngagedEnemyID가 초기화되어야 합니다.");
            Assert.IsFalse(_gameManagerData.IsEnemyDefeated("Enemy_Boss_01"));
        }

        #endregion

        #region 2. EnemySymbol 고유 식별자 및 인카운터 연동 검증 (세부 개발 명세 1)

        [Test]
        public void EnemySymbol_GenerateUniqueID_ProducesNonEmptyAndDistinctIDs()
        {
            var go1 = new GameObject("Symbol_MonsterA");
            var symbol1 = go1.AddComponent<EnemySymbol>();

            var go2 = new GameObject("Symbol_MonsterB");
            var symbol2 = go2.AddComponent<EnemySymbol>();

            Assert.IsNotEmpty(symbol1.EnemyID, "자동 생성된 ID는 비어있지 않아야 합니다.");
            Assert.IsNotEmpty(symbol2.EnemyID, "자동 생성된 ID는 비어있지 않아야 합니다.");
            Assert.AreNotEqual(symbol1.EnemyID, symbol2.EnemyID, "서로 다른 심볼의 고유 ID는 고유해야 합니다.");

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void EnemySymbol_EngageBattle_RegistersCurrentEngagedEnemyID()
        {
            var go = new GameObject("TestEnemySymbol");
            var symbol = go.AddComponent<EnemySymbol>();
            symbol.Initialize("Enemy_Unique_Battle_Target");

            symbol.EngageBattle();

            Assert.AreEqual("Enemy_Unique_Battle_Target", _gameManagerData.CurrentEngagedEnemyID,
                "EngageBattle 호출 시 GameManagerData의 CurrentEngagedEnemyID로 등록되어야 합니다.");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region 3. 씬 로드 시 적 심볼 비활성화 상태 동기화 검증 (세부 개발 명세 4)

        [Test]
        public void EnemySymbol_CheckDefeatedState_DeactivatesGameObjectWhenDefeated()
        {
            var go = new GameObject("Symbol_DefeatCheck");
            var symbol = go.AddComponent<EnemySymbol>();
            symbol.Initialize("Enemy_To_Be_Defeated");

            Assert.IsTrue(go.activeSelf, "초기 상태는 활성화되어 있어야 합니다.");
            Assert.IsFalse(symbol.CheckDefeatedState(), "미처치 상태에서는 false를 반환해야 합니다.");

            // 처치 기록
            _gameManagerData.RecordDefeatedEnemy("Enemy_To_Be_Defeated");

            bool isDefeated = symbol.CheckDefeatedState();
            Assert.IsTrue(isDefeated, "처치 등록 후 CheckDefeatedState는 true를 반환해야 합니다.");
            Assert.IsFalse(go.activeSelf, "처치 등록 후 심볼 GameObject는 비활성화(SetActive(false))되어야 합니다.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EncounterManager_RefreshDefeatedSymbols_SynchronizesSceneState()
        {
            var managerGo = new GameObject("EncounterManager_Holder");
            var encManager = managerGo.AddComponent<EncounterManager>();

            var enemy1Go = new GameObject("Enemy_Goblin");
            var enemy1 = enemy1Go.AddComponent<EnemySymbol>();
            enemy1.Initialize("ENEMY_GOB_001");

            var enemy2Go = new GameObject("Enemy_Orc");
            var enemy2 = enemy2Go.AddComponent<EnemySymbol>();
            enemy2.Initialize("ENEMY_ORC_002");

            encManager.CollectSymbolsInScene();
            Assert.AreEqual(2, encManager.EnemySymbols.Count);

            // Goblin만 처치
            _gameManagerData.RecordDefeatedEnemy("ENEMY_GOB_001");

            encManager.RefreshDefeatedSymbols();

            Assert.IsFalse(enemy1Go.activeSelf, "처치된 몬스터(Goblin)는 비활성화되어야 합니다.");
            Assert.IsTrue(enemy2Go.activeSelf, "처치되지 않은 몬스터(Orc)는 활성화 상태를 유지해야 합니다.");

            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(enemy1Go);
            Object.DestroyImmediate(enemy2Go);
        }

        #endregion

        #region 4. 전투 승리 시 처치 확정 및 복귀 연동 검증 (세부 개발 명세 3)

        [Test]
        public void BattleTurnController_BattleVictory_RecordsEngagedEnemyAndClearsOnReturn()
        {
            const string engagedID = "Enemy_Boss_Chamber";
            _gameManagerData.CurrentEngagedEnemyID = engagedID;

            var battleGo = new GameObject("BattleController_Holder");
            var turnCtrl = battleGo.AddComponent<BattleTurnController>();

            // 전투 승리 시 처치 확정 연동
            turnCtrl.RecordBattleVictory();

            Assert.IsTrue(_gameManagerData.IsEnemyDefeated(engagedID), "전투 승리 시 CurrentEngagedEnemyID가 처치 목록에 등록되어야 합니다.");
            Assert.IsTrue(_gameManagerData.DefeatedEnemyIDs.Contains(engagedID));

            // 탐색 씬 복귀 처리 -> CurrentEngagedEnemyID 클리어 검증
            turnCtrl.ReturnToExploration("Level01_Exploration");

            Assert.IsEmpty(_gameManagerData.CurrentEngagedEnemyID, "탐색 씬 복귀 전 CurrentEngagedEnemyID는 클리어되어야 합니다.");

            Object.DestroyImmediate(battleGo);
        }

        #endregion
    }
}
