using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Durable test receipts survive the Test Runner's domain reload and an MCP reconnect.
[InitializeOnLoad]
public static class AISkillAcceptanceTools
{
    private const string OutputKey = "BuddahGo.AISkills.TestOutput";
    private static readonly TestRunnerApi Runner;
    static AISkillAcceptanceTools()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        Runner.RegisterCallbacks(new Receipt());
    }
    public static void RunTests(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathRooted(outputPath)) throw new ArgumentException("Absolute test receipt path required.");
        if (File.Exists(outputPath)) throw new InvalidOperationException("Preserve previous test receipts.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        SessionState.SetString(OutputKey, outputPath);
        Runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "AISkillSceneTests", "AISkillDecisionTests", "AISkillCommitmentTests", "AIDifficultyProfilesTests", "AIDrivingTests",
                "SplineRacingLineTests", "ProjectileBurstPlannerTests", "RacerIdTests", "RaceTimingTests", "SixRacerFoundationTests", "SixRacerResultsTests",
                "SoloMatchSettingsTests", "SoloPresentationTimelineTests", "IntroHandoffContinuityTests", "IntroHandoffTimingTests", "PlayerCameraIntroExitTests",
                "SoloSessionFlowTests", "SoloTransportTests", "RaceEndPolicyTests" } }));
    }
    private sealed class Receipt : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string path = SessionState.GetString(OutputKey, string.Empty);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, result.ToXml().OuterXml);
            SessionState.EraseString(OutputKey);
        }
    }
}
