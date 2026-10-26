using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Starts the mechanics Practice verification probe (Assets/Scripts/AI/Diagnostics/MechanicsPracticeHarness.cs).
// Enter Play mode on the MainMenu scene first. Evidence: Logs/mechanics-practice/<timestamp>/report.md (git-ignored).
public static class MechanicsPracticeTools
{
    private const string RunMenu = "Tools/Mechanics/Run Practice Verification";
    private const string StatusMenu = "Tools/Mechanics/Log Practice Verification Status";

    [MenuItem(RunMenu)]
    private static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[MechanicsHarness] Open the MainMenu scene and enter Play mode, then run " + RunMenu + " again.");
            return;
        }
        string directory = Path.GetFullPath(Path.Combine("Logs", "mechanics-practice", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        try
        {
            BuddahGo.AI.MechanicsPracticeHarness.Begin(directory);
        }
        catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
        {
            // Not on an idle MainMenu, a run still going, or the directory already used: say so instead of throwing.
            Debug.LogWarning("[MechanicsHarness] Not started: " + error.Message);
            return;
        }
        Debug.Log("[MechanicsHarness] Started; evidence directory " + directory);
    }

    [MenuItem(RunMenu, true)]
    private static bool CanRun() => !BuddahGo.AI.MechanicsPracticeHarness.IsRunning;

    [MenuItem(StatusMenu)]
    private static void LogStatus()
    {
        Debug.Log("[MechanicsHarness] " + BuddahGo.AI.MechanicsPracticeHarness.Status + " | " + BuddahGo.AI.MechanicsPracticeHarness.Summary
                  + " | " + BuddahGo.AI.MechanicsPracticeHarness.LastReportPath);
    }
}
