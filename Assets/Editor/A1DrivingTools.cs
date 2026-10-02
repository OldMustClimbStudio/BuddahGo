using System;
using System.IO;
using System.Linq;
using BuddahGo.AI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class A1DrivingTools
{
    private static readonly TestRunnerApi Tests;
    private const string RecordingKey = "BuddahGo.A1.TestOutput";
    static A1DrivingTools()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        Tests = ScriptableObject.CreateInstance<TestRunnerApi>();
        Tests.RegisterCallbacks(new TestRecorder());
    }
    [MenuItem("Tools/AI/Run A1 Targeted Checks")]
    public static void RunChecks()
    {
        string directory = Path.GetFullPath(Path.Combine("Logs", "ai-a1", "tests"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "cases.jsonl"), "");
        SessionState.SetString(RecordingKey, directory);
        Tests.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            assemblyNames = new[] { "BuddahGo.Tests" },
            groupNames = new[] { "^AIDrivingTests", "^BuddahGo.Tests.AcceptedLapTimingTests", "^BuddahGo.Tests.MatchClockTests", "^BuddahGo.Tests.SoloSessionFlowTests" } }));
    }
    private sealed class TestRecorder : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            string directory = SessionState.GetString(RecordingKey, "");
            if (directory.Length == 0 || result.HasChildren) return;
            File.AppendAllText(Path.Combine(directory, "cases.jsonl"), JsonUtility.ToJson(new TestCase {
                name = result.FullName, state = result.ResultState, message = result.Message, output = result.Output }) + "\n");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            string directory = SessionState.GetString(RecordingKey, "");
            if (directory.Length == 0) return;
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonUtility.ToJson(new TestSummary {
                state = result.ResultState, passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount }, true));
            SessionState.EraseString(RecordingKey);
        }
    }
    [Serializable] private class TestCase { public string name, state, message, output; }
    [Serializable] private class TestSummary { public string state; public int passed, failed, skipped; }
    [MenuItem("Tools/AI/A1 Run From Playing MainMenu")]
    public static void Run()
    {
        string directory = Path.GetFullPath(Path.Combine("Logs", "ai-a1", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        AITuningHarness.Begin(directory);
        Debug.Log("[AI A1] Evidence directory: " + directory);
    }

    [MenuItem("Tools/AI/A1 Run From Playing MainMenu", true)]
    private static bool CanRun() => Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";

    [MenuItem("Tools/AI/Build A1 Development Player")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before building A1.");
        string directory = Path.GetFullPath(Path.Combine("Logs", "ai-a1", "development"));
        Directory.CreateDirectory(directory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/PropertySelection.unity", "Assets/Scenes/RaceMap.unity" },
            locationPathName = Path.Combine(directory, "BuddahGoA1.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(directory, "build-result.txt"),
            $"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nDuration={report.summary.totalTime}\n" +
            "Launch with --ai-a1-output <absolute private directory>. No input injection; prefab AIRacerDriver controls steering.\n");
        File.WriteAllLines(Path.Combine(directory, "build-messages.txt"), report.steps.SelectMany(step => step.messages)
            .Where(message => message.type == LogType.Warning || message.type == LogType.Error).Select(message => message.type + ": " + message.content));
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("A1 build failed: " + report.summary.result);
    }
}
