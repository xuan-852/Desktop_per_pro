using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class BuildScript
{
    public static void BuildDesktopPet()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        // 统一输出到 D:\Unity\projects\Desktop_per_pro\Build\
        string buildDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "Build"));
        string buildPath = Path.Combine(buildDir, "DesktopPet.exe");

        if (!Directory.Exists(buildDir))
            Directory.CreateDirectory(buildDir);

        // 查找所有场景
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(
            EditorBuildSettings.scenes);

        if (scenes == null || scenes.Length == 0)
        {
            // 回退：手动找场景
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            if (guids.Length > 0)
            {
                scenes = new string[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                    scenes[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }
        }

        if (scenes == null || scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] 找不到任何场景!");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[BuildScript] 场景: {string.Join(", ", scenes)}");
        Debug.Log($"[BuildScript] 输出路径: {buildPath}");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            targetGroup = BuildTargetGroup.Standalone,
            target = BuildTarget.StandaloneWindows,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[BuildScript] 构建完成: {summary.result}, 耗时 {summary.totalTime.TotalSeconds:F1}s");

        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
        else
            EditorApplication.Exit(0);
    }
}
