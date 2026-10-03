using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Development Windows x64 build for thrust-vector acceptance laps (AITuningHarness compiles under
// DEVELOPMENT_BUILD). Output stays outside the repository, next to the other private evidence roots.
public static class ThrustVectorAcceptanceBuild
{
    public static void Build()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.worktree/single-player-mode/Assets")) throw new Exception("Wrong project");
        string output = Environment.GetEnvironmentVariable("THRUST_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output)) output = "C:/Users/dwh88/Documents/Codex/2026-10-02/claude-thrust/build";
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/PropertySelection.unity", "Assets/Scenes/RaceMap.unity" },
            locationPathName = output + "/BuddahGoThrust.exe", target = BuildTarget.StandaloneWindows64,
            options = Environment.GetEnvironmentVariable("THRUST_BUILD_RELEASE") == "1" ? BuildOptions.None : BuildOptions.Development,
            extraScriptingDefines = Array.Empty<string>() });
        File.WriteAllText(output + "/receipt.json", JsonUtility.ToJson(new Receipt { result = report.summary.result.ToString(),
            errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds }, true));
        File.WriteAllLines(output + "/warnings.txt", report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Warning || m.type == LogType.Error).Select(m => m.type + ": " + m.content));
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    [Serializable] class Receipt { public string result; public int errors, warnings; public double seconds; }
}
