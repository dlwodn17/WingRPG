using UnityEditor;
using UnityEngine;
using RPG25D.Core.Dice;
using RPG25D.Gameplay;
using RPG25D.Visual;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 4] D20FeedbackPreviewEditor.cs
    /// 눈금 1(대실패), 10(일반 적중), 20(절대 성공/즉사) 케이스의 비주얼 피드백 시퀀스를
    /// 에디터 상에서 버튼 하나로 즉각 미리보기(Preview)할 수 있는 에디터 테스트 툴입니다.
    /// </summary>
    public class D20FeedbackPreviewEditor : EditorWindow
    {
        private int _customDiceValue = 15;

        [MenuItem("Tools/RPG25D/D20 Visual Feedback Previewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<D20FeedbackPreviewEditor>("D20 Feedback Previewer");
            window.minSize = new Vector2(380, 420);
            window.Show();
        }

        [MenuItem("Tools/RPG25D/Feedback Preview/Preview Roll 1 (Critical Fail)")]
        public static void MenuPreviewRoll1() => TriggerPreview(1);

        [MenuItem("Tools/RPG25D/Feedback Preview/Preview Roll 10 (Normal Hit)")]
        public static void MenuPreviewRoll10() => TriggerPreview(10);

        [MenuItem("Tools/RPG25D/Feedback Preview/Preview Roll 20 (Instant Kill)")]
        public static void MenuPreviewRoll20() => TriggerPreview(20);

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎲 D20 도파민 비주얼 피드백 테스트 툴", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play 모드 실행 중 원하는 눈금 버튼을 클릭하면 3D 물리 주사위 굴림, 사운드, 카메라 셰이크, 즉사 슬로우모션 연출을 즉각 시뮬레이션합니다.", MessageType.Info);

            EditorGUILayout.Space(15);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("⚠️ 시뮬레이션을 보려면 먼저 Play 모드를 실행해야 합니다.", MessageType.Warning);
                if (GUILayout.Button("▶️ 에디터 Play 모드 시작", GUILayout.Height(35)))
                {
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            // 1. 눈금 1 (대실패)
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🔴 [눈금 1] 대실패 (Crit Fail / Shatter / SFX)", GUILayout.Height(45)))
            {
                TriggerPreview(1);
            }

            EditorGUILayout.Space(8);

            // 2. 눈금 10 (일반 적중)
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("🟡 [눈금 10] 일반 적중 (Hit / Scaled Damage / SFX)", GUILayout.Height(45)))
            {
                TriggerPreview(10);
            }

            EditorGUILayout.Space(8);

            // 3. 눈금 20 (절대 성공 / 즉사)
            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            if (GUILayout.Button("🌟 [눈금 20] 절대 성공 / 즉사 (Instant Kill / Slow-Mo / Fanfare)", GUILayout.Height(50)))
            {
                TriggerPreview(20);
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("임의 눈금 테스트 (1 ~ 20)", EditorStyles.boldLabel);
            _customDiceValue = EditorGUILayout.IntSlider("D20 눈금 지정", _customDiceValue, 1, 20);

            if (GUILayout.Button($"🎲 [눈금 {_customDiceValue}] 커스텀 연출 실행", GUILayout.Height(35)))
            {
                TriggerPreview(_customDiceValue);
            }
        }

        private static void TriggerPreview(int diceValue)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[D20Preview] Play 모드에서만 비주얼 피드백 시퀀스를 실행할 수 있습니다. Play 모드를 먼저 켜주세요.");
                return;
            }

            var turnController = Object.FindAnyObjectByType<BattleTurnController>();
            if (turnController != null && turnController.CurrentState == BattlePhase.PlayerInput)
            {
                Debug.Log($"[D20Preview] 플레이어 턴에서 강제 눈금 [{diceValue}]으로 스킬 시전을 트리거합니다.");
                turnController.ConfirmPlayerAction(diceValue);
                return;
            }

            var feedbackMgr = BattleFeedbackManager.Instance;
            if (feedbackMgr != null)
            {
                Debug.Log($"[D20Preview] 피드백 매니저를 통해 강제 눈금 [{diceValue}] 연출을 독립 실행합니다.");
                var roll = D20DiceRoller.Evaluate(diceValue);
                feedbackMgr.StartCoroutine(feedbackMgr.PlayDopamineFeedbackRoutine(roll));
            }
            else
            {
                Debug.LogError("[D20Preview] BattleFeedbackManager 인스턴스를 씬에서 찾을 수 없습니다.");
            }
        }
    }
}
