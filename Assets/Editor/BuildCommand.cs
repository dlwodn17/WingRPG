using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPG25D.Editor
{
    /// <summary>
    /// [요구 산출물 4] BuildCommand.cs
    /// 명령줄(CLI)에서 헤드리스 배치 모드(-batchmode -nographics)로 실행 가능한 빌드 스크립트입니다.
    /// 실행 예:
    /// Unity.exe -quit -batchmode -nographics -projectPath "C:\path\to\project" -executeMethod RPG25D.Editor.BuildCommand.PerformBuild
    /// </summary>
    public static class BuildCommand
    {
        private const string DefaultBuildDirectory = "Builds/StandaloneWindows64";
        private const string ExecutableName = "RPG25D_Game.exe";

        [MenuItem("Tools/Build/Perform Standalone Build")]
        public static void PerformBuildMenu()
        {
            PerformBuildInternal(false);
        }

        public static void PerformBuild()
        {
            PerformBuildInternal(true);
        }

        private static void PerformBuildInternal(bool isBatchMode)
        {
            Debug.Log("==================================================");
            Debug.Log("[BuildCommand] CLI 자동 빌드 프로세스를 시작합니다.");
            Debug.Log($"[BuildCommand] 실행 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Debug.Log("==================================================");

            // 1. 빌드 대상 씬 수집
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Build Settings에 등록된 씬이 없으면 기본 SampleScene 또는 현재 씬 지정
                string fallbackScene = "Assets/Scenes/SampleScene.unity";
                if (File.Exists(fallbackScene))
                {
                    scenes = new[] { fallbackScene };
                    Debug.Log($"[BuildCommand] Build Settings에 활성화된 씬이 없어 기본 씬을 사용합니다: {fallbackScene}");
                }
                else
                {
                    Debug.LogError("[BuildCommand] 빌드할 씬을 찾을 수 없습니다!");
                    ExitWithCode(1, isBatchMode);
                    return;
                }
            }

            // 2. 출력 경로 설정 (CLI 파라미터 확인 가능)
            string buildDir = GetCommandLineArg("-buildPath") ?? Path.Combine(Directory.GetCurrentDirectory(), DefaultBuildDirectory);
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

            string fullExecutablePath = Path.Combine(buildDir, ExecutableName);

            // 3. BuildPlayerOptions 구성
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildCommand] 빌드 타깃: {buildPlayerOptions.target}");
            Debug.Log($"[BuildCommand] 출력 파일: {fullExecutablePath}");
            Debug.Log($"[BuildCommand] 포함 씬 수: {scenes.Length}");

            // 4. 빌드 실행
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            Debug.Log("==================================================");
            Debug.Log($"[BuildCommand] 빌드 결과: {summary.result}");
            Debug.Log($"[BuildCommand] 소요 시간: {summary.totalTime.TotalSeconds:F2}초");
            Debug.Log($"[BuildCommand] 파일 크기: {summary.totalSize / (1024 * 1024):F2} MB");
            Debug.Log($"[BuildCommand] 에러 수: {summary.totalErrors}, 경고 수: {summary.totalWarnings}");
            Debug.Log("==================================================");

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[BuildCommand] ✅ 빌드가 성공적으로 완료되었습니다!");
                ExitWithCode(0, isBatchMode);
            }
            else
            {
                Debug.LogError($"[BuildCommand] ❌ 빌드 실패! (원인: {summary.result})");
                ExitWithCode(1, isBatchMode);
            }
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void ExitWithCode(int code, bool isBatchMode)
        {
            if (isBatchMode || Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}

/// <summary>
/// CLI 커맨드라인에서 네임스페이스 생략 시(-executeMethod BuildCommand.PerformBuild) 호출 호환성을 위한 프록시 클래스
/// </summary>
public static class BuildCommand
{
    public static void PerformBuild()
    {
        RPG25D.Editor.BuildCommand.PerformBuild();
    }
}
