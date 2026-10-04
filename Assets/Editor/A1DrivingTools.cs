using System;
using System.IO;
using BuddahGo.AI;
using UnityEditor;
using UnityEngine;

// Editor entry points for the A1 (one lap) and A2 (three laps) driving harness. Builds use
// ThrustVectorAcceptanceBuild; test receipts use AISkillAcceptanceTools.
public static class A1DrivingTools
{
    [MenuItem("Tools/AI/A1 Run From Playing MainMenu")]
    public static void Run()
    {
        string directory = Path.GetFullPath(Path.Combine("Logs", "ai-a1", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        AITuningHarness.Begin(directory);
        Debug.Log("[AI A1] Evidence directory: " + directory);
    }

    [MenuItem("Tools/AI/A2 Run From Playing MainMenu")]
    public static void RunA2()
    {
        string directory = Path.GetFullPath(Path.Combine("Logs", "ai-a2", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        AITuningHarness.Begin(directory, null, 3);
    }

    [MenuItem("Tools/AI/A1 Run From Playing MainMenu", true)]
    private static bool CanRun() => Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";
}
