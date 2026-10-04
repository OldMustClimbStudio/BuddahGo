using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Development Windows x64 build for thrust-vector acceptance laps (AITuningHarness compiles under
// DEVELOPMENT_BUILD). Output defaults to the git-ignored Logs/ folder; THRUST_BUILD_OUTPUT overrides it.
public static class ThrustVectorAcceptanceBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("THRUST_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output)) output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/thrust-vector-build"));
        string executable = Environment.GetEnvironmentVariable("THRUST_BUILD_NAME");
        if (string.IsNullOrWhiteSpace(executable)) executable = "BuddahGoThrust.exe";
        if (Path.GetFileName(executable) != executable || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("THRUST_BUILD_NAME must be a Windows executable filename.");
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/PropertySelection.unity", "Assets/Scenes/RaceMap.unity" },
            locationPathName = Path.Combine(output, executable), target = BuildTarget.StandaloneWindows64,
            options = Environment.GetEnvironmentVariable("THRUST_BUILD_RELEASE") == "1" ? BuildOptions.None : BuildOptions.Development,
            extraScriptingDefines = Array.Empty<string>() });
        File.WriteAllText(output + "/receipt.json", JsonUtility.ToJson(new Receipt { result = report.summary.result.ToString(),
            errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds }, true));
        File.WriteAllLines(output + "/warnings.txt", report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Warning || m.type == LogType.Error).Select(m => m.type + ": " + m.content));
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    [Serializable] class Receipt { public string result; public int errors, warnings; public double seconds; }
}
