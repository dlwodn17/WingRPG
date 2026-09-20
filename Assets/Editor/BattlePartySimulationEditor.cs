using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RPG25D.Core.Battle;
using RPG25D.Core.Dice;
using RPG25D.Gameplay;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 4] BattlePartySimulationEditor.cs
    /// 아군 3인 vs 적군 3인의 캐릭터 정보를 미리 설정하고, Play 모드 실시간 턴 제어 및
    /// CLI 헤드리스 고속 동기 시뮬레이션을 원클릭으로 검증할 수 있는 에디터 테스트 윈도우입니다.
    /// </summary>
    public class BattlePartySimulationEditor : EditorWindow
    {
        private int _forcedDiceRoll = 15;
        private Vector2 _scrollPos;

        [MenuItem("Tools/RPG25D/3v3 Battle Party Simulator")]
        public static void ShowWindow()
        {
            var window = GetWindow<BattlePartySimulationEditor>("3v3 Battle Simulator");
            window.minSize = new Vector2(440, 560);
            window.Show();
        }

        [MenuItem("Tools/RPG25D/Battle Simulation/Run Full Battle Simulation (Sync Batch)")]
        public static void MenuRunBatchSim()
        {
            RunSyncBatchSimulation();
        }

        [MenuItem("Tools/RPG25D/Battle Simulation/Reset & Restart 3v3 Battle")]
        public static void MenuRestartBattle()
        {
            if (Application.isPlaying)
            {
                var controller = Object.FindAnyObjectByType<BattleTurnController>();
                if (controller != null) controller.StartBattle();
            }
            else
            {
                Debug.LogWarning("[BattleSimulator] Play 모드에서만 전투를 재시작할 수 있습니다.");
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚔️ [3 vs 3] 턴제 RPG 전투 루프 시뮬레이터", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("아군 3인과 적군 3인의 스탯/속도(Speed)를 기반으로 턴 타임라인과 승패 판정(Victory/Defeat)을 실시간 및 CLI 배치 모드로 검증합니다.", MessageType.Info);

            EditorGUILayout.Space(10);

            // 1. 파티 현황 모니터링 섹션
            DrawPartyOverviewSection();

            EditorGUILayout.Space(15);

            // 2. CLI 동기 고속 시뮬레이션 섹션
            DrawBatchSimulationSection();

            EditorGUILayout.Space(15);

            // 3. Play 모드 실시간 인터랙션 섹션
            DrawPlayModeControlSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPartyOverviewSection()
        {
            EditorGUILayout.LabelField("👥 파티 구성 및 스탯 프리셋", EditorStyles.boldLabel);

            var controller = Application.isPlaying ? Object.FindAnyObjectByType<BattleTurnController>() : null;
            var party = controller != null ? controller.Party : null;

            EditorGUILayout.BeginHorizontal();

            // 아군 3인
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛡️ 아군 파티 (Allies)", EditorStyles.miniBoldLabel);
            if (party != null && party.Allies.Count > 0)
            {
                foreach (var a in party.Allies)
                {
                    string status = a.IsAlive ? $"HP: {a.CurrentHP}/{a.MaxHP}" : "<color=red>사망</color>";
                    EditorGUILayout.LabelField($"• {a.Name} (속도:{a.Speed}) | {status}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("• 용사 (Knight) [HP:130, 속도:12]");
                EditorGUILayout.LabelField("• 마법사 (Mage) [HP:90, 속도:15]");
                EditorGUILayout.LabelField("• 사제 (Cleric) [HP:110, 속도:9]");
            }
            EditorGUILayout.EndVertical();

            // 적군 3인
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("👾 적군 파티 (Enemies)", EditorStyles.miniBoldLabel);
            if (party != null && party.Enemies.Count > 0)
            {
                foreach (var e in party.Enemies)
                {
                    string status = e.IsAlive ? $"HP: {e.CurrentHP}/{e.MaxHP}" : "<color=red>사망</color>";
                    EditorGUILayout.LabelField($"• {e.Name} (속도:{e.Speed}) | {status}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("• 고블린 돌격병 [HP:65, 속도:14]");
                EditorGUILayout.LabelField("• 오크 광전사 [HP:95, 속도:8]");
                EditorGUILayout.LabelField("• 산성 슬라임 [HP:50, 속도:10]");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchSimulationSection()
        {
            EditorGUILayout.LabelField("⚡ CLI 헤드리스 고속 시뮬레이션 (동기 1프레임 완주)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("UI 렌더링 및 프레임 대기 없이 6명의 3vs3 전투를 1프레임 만에 끝까지 시뮬레이션하여 승패 결과 및 라운드 수를 콘솔에 덤프합니다.", MessageType.None);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("⚡ [동기 실행] 3 vs 3 전체 전투 시뮬레이션 시작", GUILayout.Height(36)))
            {
                RunSyncBatchSimulation();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawPlayModeControlSection()
        {
            EditorGUILayout.LabelField("🎮 Play 모드 실시간 전투 루프 제어", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("⚠️ 실시간 턴 조작 및 3D 비주얼 연출을 보려면 Play 모드를 실행해야 합니다.", MessageType.Warning);
                if (GUILayout.Button("▶️ 에디터 Play 모드 시작", GUILayout.Height(34)))
                {
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            var controller = Object.FindAnyObjectByType<BattleTurnController>();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("씬에서 BattleTurnController를 찾을 수 없습니다. Tools > RPG25D > Setup 2.5D Battle Scene을 실행하세요.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"• 현재 라운드: {controller.RoundNumber} | 단계: {controller.CurrentState}");
            string activeName = controller.ActiveActor != null ? controller.ActiveActor.Name : "없음";
            EditorGUILayout.LabelField($"• 현재 행동 중: {activeName}");
            string targetName = controller.SelectedTarget != null ? controller.SelectedTarget.Name : "없음";
            EditorGUILayout.LabelField($"• 지정된 타깃: {targetName}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 강제 눈금 조작 버튼들
            EditorGUILayout.LabelField("D20 강제 눈금 주입 공격 (플레이어 턴인 경우)", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🔴 눈금 1 (대실패)", GUILayout.Height(34)))
            {
                controller.ConfirmPlayerAction(1);
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("🟡 눈금 10 (일반)", GUILayout.Height(34)))
            {
                controller.ConfirmPlayerAction(10);
            }

            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            if (GUILayout.Button("🌟 눈금 20 (즉사)", GUILayout.Height(34)))
            {
                controller.ConfirmPlayerAction(20);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            _forcedDiceRoll = EditorGUILayout.IntSlider("임의 눈금 지정", _forcedDiceRoll, 1, 20);
            if (GUILayout.Button($"🎲 [눈금 {_forcedDiceRoll}] 강제 굴림 공격 확정", GUILayout.Height(32)))
            {
                controller.ConfirmPlayerAction(_forcedDiceRoll);
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("🔄 전투 초기화 및 재시작", GUILayout.Height(32)))
            {
                controller.StartBattle();
            }
        }

        private static void RunSyncBatchSimulation()
        {
            var controller = Object.FindAnyObjectByType<BattleTurnController>();
            if (controller == null)
            {
                var go = new GameObject("[Temp_Sim_Controller]");
                controller = go.AddComponent<BattleTurnController>();
            }

            Debug.Log("==================================================");
            Debug.Log("[BattleSimulator] ⚡ 아군 3인 vs 적군 3인 CLI 동기 시뮬레이션을 시작합니다...");
            Debug.Log("==================================================");

            var result = controller.SimulateFullBattleSync(30);

            Debug.Log("==================================================");
            Debug.Log($"[BattleSimulator] ✅ 시뮬레이션 종료! 최종 결과: {result}");
            Debug.Log("==================================================");
        }
    }
}
