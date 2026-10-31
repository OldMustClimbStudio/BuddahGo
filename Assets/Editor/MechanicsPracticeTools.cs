using System;
using System.IO;
using BuddahGo.Match;
using UnityEditor;
using UnityEngine;

// Starts the mechanics Practice verification probe (Assets/Scripts/AI/Diagnostics/MechanicsPracticeHarness.cs).
// Enter Play mode on the MainMenu scene first. Evidence: Logs/mechanics-practice/<timestamp>/report.md (git-ignored).
public static class MechanicsPracticeTools
{
    private const string RunMenu = "Tools/Mechanics/Run Practice Verification";
    private const string StatusMenu = "Tools/Mechanics/Log Practice Verification Status";
    private const string StartPracticeMenu = "Tools/Mechanics/Start Practice (0 AI, 3 dummies)";

    // The main menu has no Practice entry (its Solo button reads Match/ModeRule Solo.aiCount = 5). For a hands-on
    // playtest this starts the same Solo host with 0 AI, which SoloMatchRules maps to the Practice row (3 training
    // dummies). Enter Play mode on MainMenu first; the loadout / skin flow then continues as usual.
    [MenuItem(StartPracticeMenu)]
    private static void StartPractice()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[MechanicsHarness] Open the MainMenu scene and enter Play mode, then run " + StartPracticeMenu + " again.");
            return;
        }
        var session = SessionControl.Current;
        if (session == null)
        {
            Debug.LogWarning("[MechanicsHarness] No SessionControl yet; wait for MainMenu to initialise.");
            return;
        }
        if (!session.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)))
        {
            Debug.LogWarning("[MechanicsHarness] Practice not started: " + session.LastError);
            return;
        }
        Debug.Log("[MechanicsHarness] Practice (0 AI) host starting; continue with the loadout / skin selection.");
    }

    [MenuItem(StartPracticeMenu, true)]
    private static bool CanStartPractice() => EditorApplication.isPlaying && !BuddahGo.AI.MechanicsPracticeHarness.IsRunning;

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
