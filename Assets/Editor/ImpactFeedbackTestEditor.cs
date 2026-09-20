using UnityEditor;
using UnityEngine;
using RPG25D.Data;
using RPG25D.Visual;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 2] ImpactFeedbackTestEditor.cs
    /// D20 눈금 1(대실패), 15(강타), 20(즉사/절대성공) 케이스의 타격감(히트스톱 & 2.5D 카메라 셰이크)을
    /// 에디터 상에서 즉각 시뮬레이션 및 테스트할 수 있는 에디터 윈도우입니다.
    /// </summary>
    public class ImpactFeedbackTestEditor : EditorWindow
    {
        private int _selectedDiceValue = 15;

        [MenuItem("Tools/RPG25D/Impact Feedback Tester (Hit-Stop & Shake)")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImpactFeedbackTestEditor>("Impact Tester");
            window.minSize = new Vector2(400, 480);
            window.Show();
        }

        // 상단 메뉴바 단축 실행 항목들
        [MenuItem("Tools/RPG25D/Impact Test/Test Roll 1 (Crit Fail: 0.2s Hit-Stop + Heavy Shake)")]
        public static void MenuTestRoll1() => ExecuteTest(1);

        [MenuItem("Tools/RPG25D/Impact Test/Test Roll 15 (Strong Hit: 0.12s Hit-Stop + Sharp Shake)")]
        public static void MenuTestRoll15() => ExecuteTest(15);

        [MenuItem("Tools/RPG25D/Impact Test/Test Roll 20 (Instant Kill: 0.25s Hit-Stop + Tearing Shake)")]
        public static void MenuTestRoll20() => ExecuteTest(20);

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("💥 D20 타격감(히트스톱 & 2.5D 카메라 셰이크) 테스트 툴", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play 모드 실행 중 각 눈금 버튼을 클릭하면 실시간 히트 스톱(Time.timeScale 동결)과 2.5D 최적화 카메라 셰이크가 동시 발동되어 타격감을 즉각 검증할 수 있습니다.", MessageType.Info);

            EditorGUILayout.Space(12);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("⚠️ 타격감 시뮬레이션(Time.timeScale 및 카메라 이동)을 보려면 Play 모드여야 합니다.", MessageType.Warning);
                if (GUILayout.Button("▶️ 에디터 Play 모드 시작", GUILayout.Height(36)))
                {
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            // 1. 눈금 1 (대실패)
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("🔴 [눈금 1] 대실패 타격감 테스트\n(0.20s 긴 동결 + 둔탁한 저주파 14Hz 셰이크 + 시네마틱 레터박스)", GUILayout.Height(50)))
            {
                ExecuteTest(1);
            }

            EditorGUILayout.Space(6);

            // 2. 눈금 15 (강한 타격)
            GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
            if (GUILayout.Button("🟠 [눈금 15] 회심의 강타 타격감 테스트\n(0.12s 뚜렷한 히트스톱 + 34Hz 날카로운 2.5D 카메라 흔들림)", GUILayout.Height(50)))
            {
                ExecuteTest(15);
            }

            EditorGUILayout.Space(6);

            // 3. 눈금 20 (절대 성공 / 즉사)
            GUI.backgroundColor = new Color(1f, 0.88f, 0.2f);
            if (GUILayout.Button("🌟 [눈금 20] 절대 성공 / 즉사 타격감 테스트\n(0.25s 강력 히트스톱 + 45Hz 화면 찢김 최대 셰이크 + 시네마틱 레터박스)", GUILayout.Height(55)))
            {
                ExecuteTest(20);
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(18);
            EditorGUILayout.LabelField("임의 눈금 슬라이더 테스트 (1 ~ 20)", EditorStyles.boldLabel);
            _selectedDiceValue = EditorGUILayout.IntSlider("D20 눈금 지정", _selectedDiceValue, 1, 20);

            // 선택된 눈금의 예상 파라미터 미리보기 박스
            DrawProfilePreviewBox(_selectedDiceValue);

            EditorGUILayout.Space(6);
            if (GUILayout.Button($"⚡ [눈금 {_selectedDiceValue}] 커스텀 타격감 즉시 발동", GUILayout.Height(38)))
            {
                ExecuteTest(_selectedDiceValue);
            }
        }

        private void DrawProfilePreviewBox(int d20)
        {
            var mgr = ImpactFeedbackManager.Instance;
            ImpactProfile profile = mgr != null && mgr.Config != null
                ? mgr.Config.GetProfileForRoll(d20)
                : ImpactProfile.Default;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"📊 [눈금 {d20}] 프로필 상세", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"• 히트 스톱 지속: {profile.hitStopDuration:F2}초 (TimeScale: {profile.hitStopTimeScale})");
            EditorGUILayout.LabelField($"• 셰이크 진폭(Amp): {profile.shakeAmplitude:F2} | 주파수(Freq): {profile.shakeFrequency:F0}Hz");
            EditorGUILayout.LabelField($"• 셰이크 지속: {profile.shakeDuration:F2}초 | 레터박스: {(profile.useLetterbox ? "활성화(ON)" : "비활성화(OFF)")}");
            EditorGUILayout.LabelField($"• 2.5D 축 가중치: X={profile.axisMultiplier.x:F1}, Y={profile.axisMultiplier.y:F1}, Z={profile.axisMultiplier.z:F1}");
            EditorGUILayout.EndVertical();
        }

        private static void ExecuteTest(int diceValue)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ImpactFeedbackTest] Play 모드에서만 실행할 수 있습니다. Play 모드를 켜주세요.");
                return;
            }

            var manager = ImpactFeedbackManager.Instance;
            if (manager == null)
            {
                manager = Object.FindAnyObjectByType<ImpactFeedbackManager>();
            }

            if (manager != null)
            {
                manager.TriggerImpact(diceValue);
                Debug.Log($"[ImpactFeedbackTest] ✅ D20 눈금 [{diceValue}] 타격감(히트스톱 & 2.5D 카메라 셰이크)을 성공적으로 트리거했습니다.");
            }
            else
            {
                // 인스턴스가 씬에 없으면 즉석 생성하여 테스트 지원
                var go = new GameObject("[ImpactFeedbackManager_Temp]");
                manager = go.AddComponent<ImpactFeedbackManager>();
                manager.TriggerImpact(diceValue);
                Debug.Log($"[ImpactFeedbackTest] 임시 ImpactFeedbackManager를 생성하여 눈금 [{diceValue}] 타격감을 트리거했습니다.");
            }
        }
    }
}
