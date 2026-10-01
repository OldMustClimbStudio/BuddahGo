using System.Collections;
using BuddahGo.Match;
using FishNet.Managing;
using NUnit.Framework;
using SteamMultiplayer.Network;
using SteamMultiplayer.UI;
using SteamMultiplayer.Network.Results;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BuddahGo.Tests
{
    public class SoloSessionFlowTests
    {
        private Keyboard _keyboard;
        private System.Action _pendingInput;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInputBehavior;
        private bool _inputSettingsChanged;

        private void QueueKeyboardState(KeyboardState state)
        {
            // EditMode iterators also run on editor updates. Queue on the game's input
            // update so an editor-only input pass cannot consume the simulated press.
            _pendingInput = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                InputSystem.onBeforeUpdate -= _pendingInput;
                _pendingInput = null;
                InputSystem.QueueStateEvent(_keyboard, state);
            };
            InputSystem.onBeforeUpdate += _pendingInput;
        }

        [TearDown]
        public void RemoveTestKeyboard()
        {
            if (_pendingInput != null) InputSystem.onBeforeUpdate -= _pendingInput;
            _pendingInput = null;
            if (_inputSettingsChanged)
            {
                InputSystem.settings.backgroundBehavior = _backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = _editorInputBehavior;
                _inputSettingsChanged = false;
            }
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
        }

        [UnityTest]
        public IEnumerator StartupRollback_AfterSelectionLoad_ReturnsHomeAndPreservesError()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)), Is.True);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < deadline && (PropertiesSelectionManager.Instance == null ||
                !PropertiesSelectionManager.Instance.IsClientInitialized || !PropertiesSelectionManager.Instance.IsStageCountdownActive))
                yield return null;
            Assert.That(PropertiesSelectionManager.Instance, Is.Not.Null);
            var network = Object.FindFirstObjectByType<NetworkManager>();
            // Inject a startup failure at the private rollback boundary after scene load.
            // This tests recovery, not the cause or duration of a real authentication timeout.
            var rollback = typeof(SessionLauncher).GetMethod("Rollback",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(rollback, Is.Not.Null);
            Assert.That(rollback.Invoke(SessionControl.Current, new object[] { "Injected startup failure" }), Is.EqualTo(false));
            Assert.That(network.ClientManager.Started, Is.True, "Failure cleanup is also deferred out of its calling stack.");
            deadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < deadline && (network.ClientManager.Started ||
                SceneManager.GetActiveScene().name != SceneNames.MainMenu)) yield return null;
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            Assert.That(SessionControl.Current.LastError, Is.EqualTo("Injected startup failure"));
            Assert.That(LocalInputBlock.IsBlocked, Is.False);
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && (GameObject.Find("OnlineAvailability") == null ||
                GameObject.Find("OnlineAvailability").GetComponent<TMPro.TMP_Text>().text != "Injected startup failure"))
                yield return null;
            Assert.That(GameObject.Find("OnlineAvailability"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineAvailability").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("Injected startup failure"), "Home displays the failure before reopening setup.");
            Object.FindFirstObjectByType<MainMenuUI>().ShowSoloSetup();
            yield return null;
            Assert.That(GameObject.Find("StartPractice").GetComponent<Button>().interactable, Is.True);
            var setup = Object.FindFirstObjectByType<SoloSetupPanel>();
            Assert.That(System.Array.Exists(setup.GetComponentsInChildren<TMPro.TMP_Text>(),
                label => label.text == "Injected startup failure"), Is.True, "Reopened setup keeps the startup error visible.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Practice_SelectsExitsReopensAndRematches_WithSyntheticFinish()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(SessionControl.Current, Is.Not.Null);
            ScreenCapture.CaptureScreenshot("Temp/solo-home.png");
            yield return null;
            Object.FindFirstObjectByType<MainMenuUI>().ShowSoloSetup();
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/solo-setup.png");
            yield return null;
            GameObject.Find("StartPractice").GetComponent<Button>().onClick.Invoke();
            Assert.That(MatchRules.Current.AutoStartRoom, Is.True, SessionControl.Current.LastError);
            float waitDeadline1 = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < waitDeadline1 && ((PropertiesSelectionManager.Instance == null || !PropertiesSelectionManager.Instance.IsClientInitialized || PropertiesSelectionManager.Instance.Participants.Count == 0 || !PropertiesSelectionManager.Instance.IsStageCountdownActive)))
                yield return null;
            var selection = PropertiesSelectionManager.Instance;
            Assert.That(selection, Is.Not.Null, "Auto-start must reach the selection scene.");
            Assert.That(selection.Participants.Count, Is.EqualTo(1));
            Assert.That(selection.StageOrder.Contains(PropertiesSelectionManager.MapStageKey), Is.False);
            Assert.That(selection.StageCountdownSecondsRemaining, Is.Zero);
            Assert.That(selection.IsStageCountdownActive, Is.True, "Selection submissions remain enabled without a timer.");
            if (!SessionControl.Current.IsOnlineAvailable)
                Assert.That(selection.Participants[0].PlayerName, Is.EqualTo("玩家"));
            float selectionWaitUntil = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < selectionWaitUntil) yield return null;
            Assert.That(selection.CurrentStagePropertyKey, Is.EqualTo(PropertiesSelectionManager.SkillLoadoutStageKey));

            var network = Object.FindFirstObjectByType<NetworkManager>();
            // Batch runs have no focused Game View. Scope the input routing override
            // to this test and restore it in teardown; production settings stay unchanged.
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _inputSettingsChanged = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            QueueKeyboardState(new KeyboardState(Key.Escape));
            float quitDeadline = Time.realtimeSinceStartup + 3f;
            while (!LocalInputBlock.IsBlocked && Time.realtimeSinceStartup < quitDeadline) yield return null;
            Assert.That(LocalInputBlock.IsBlocked, Is.True, $"Esc must open the quit modal during selection. Dialogs={Object.FindObjectsByType<QuitConfirmDialog>(FindObjectsSortMode.None).Length}; keyboard={Keyboard.current == _keyboard}; pressed={_keyboard.escapeKey.isPressed}; enabled={_keyboard.enabled}; frame={Time.frameCount}");
            QueueKeyboardState(new KeyboardState());
            yield return null;
            GameObject.Find("ConfirmQuit").GetComponent<Button>().onClick.Invoke();
            Assert.That(network.ClientManager.Started, Is.True, "Stop must be deferred out of the calling RPC/UI stack.");
            float waitDeadline2 = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < waitDeadline2 && ((network.ClientManager.Started || SceneManager.GetActiveScene().name != SceneNames.MainMenu)))
                yield return null;
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            Assert.That(MatchRules.Current.RequiresReady, Is.True);

            Assert.That(SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)), Is.True,
                SessionControl.Current.LastError);
            float waitDeadline3 = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < waitDeadline3 && ((PropertiesSelectionManager.Instance == null || !PropertiesSelectionManager.Instance.IsClientInitialized || PropertiesSelectionManager.Instance.Participants.Count == 0 || !PropertiesSelectionManager.Instance.IsStageCountdownActive)))
                yield return null;
            selection = PropertiesSelectionManager.Instance;
            Assert.That(selection, Is.Not.Null);
            var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey);
            Assert.That(options.Count, Is.GreaterThanOrEqualTo(3));
            selection.SubmitSkillLoadoutSelection(new[] { options[0].OptionId, options[1].OptionId, options[2].OptionId });
            float waitDeadline4 = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < waitDeadline4 && (selection.CurrentStagePropertyKey != PropertiesSelectionManager.SkinStageKey)) yield return null;
            Assert.That(selection.CurrentStagePropertyKey, Is.EqualTo(PropertiesSelectionManager.SkinStageKey));
            options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
            Assert.That(options.Count, Is.GreaterThan(0));
            selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId);
            float raceStartDeadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < raceStartDeadline && (RoomStateManager.Instance == null || !RoomStateManager.Instance.IsAuthoritativeGoIssued)) yield return null;
            Assert.That(RoomStateManager.Instance.IsAuthoritativeGoIssued, Is.True, "Selection and intro must reach authoritative GO.");
            float movementDeadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < movementDeadline && !RoomStateManager.Instance.IsGameplayMovementUnlocked) yield return null;
            Assert.That(RoomStateManager.Instance.IsGameplayMovementUnlocked, Is.True);
            QueueKeyboardState(new KeyboardState(Key.Escape));
            quitDeadline = Time.realtimeSinceStartup + 3f;
            while (!LocalInputBlock.IsBlocked && Time.realtimeSinceStartup < quitDeadline) yield return null;
            Assert.That(LocalInputBlock.IsBlocked, Is.True, "Esc must block steering during racing.");
            QueueKeyboardState(new KeyboardState());
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/solo-quit.png");
            yield return null;
            GameObject.Find("CancelQuit").GetComponent<Button>().onClick.Invoke();
            Assert.That(LocalInputBlock.IsBlocked, Is.False);
            Assert.That(MatchServices.Clock, Is.Not.Null);
            Assert.That(MatchServices.Timing, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            // Same component, two rounds: stale synchronized Lap Times must not survive Begin.
            // This exercises timing state only, and is not a physical lap/completion acceptance run.
            var timing = Object.FindFirstObjectByType<RaceTimingSync>();
            var racer = RacerId.FromClient(network.ClientManager.Connection.ClientId);
            double start = MatchServices.Clock.Now;
            timing.Begin(start);
            timing.ObserveCompletedLaps(racer, 1, start + 1d);
            timing.Finish(racer, 2, start + 2d);
            Assert.That(timing.Records.Count, Is.EqualTo(2));
            timing.Begin(start + 3d);
            Assert.That(timing.Records.Count, Is.Zero);
            timing.Finish(racer, 1, start + 4d);
            Assert.That(timing.Records.Count, Is.EqualTo(1));
            Assert.That(timing.Records[0].TotalSeconds, Is.EqualTo(1d));

            // Controlled registration verifies result ordering and rematch, not physical lap validation.
            var finish = RaceFinishManager.Instance;
            var unowned = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab")
                .GetComponentInChildren<RaceCompletionTracker>(true);
            Assert.That(unowned.OwnerId, Is.EqualTo(-1));
            Assert.That(finish.TryRegisterFinish(unowned), Is.False);
            var live = Object.FindFirstObjectByType<PlayerProgressReporter>().GetComponent<RaceCompletionTracker>();
            start = MatchServices.Clock.Now - finish.LapsToFinish;
            timing.Begin(start);
            for (int lap = 1; lap < finish.LapsToFinish; lap++) timing.ObserveCompletedLaps(racer, lap, start + lap);
            Assert.That(finish.TryRegisterFinish(live), Is.True);
            Assert.That(live.FinishOrder, Is.EqualTo(1), "Rejected identity must not consume first place.");
            Assert.That(finish.IsRaceForceEnded, Is.False, "Final leaderboard report must precede immediate Practice end.");
            LeaderboardManager.Instance.ReportSplineProgress(live.OwnerId, 0f, 1f, finish.LapsToFinish + 1,
                1f, 100f, true, live.FinishOrder, live.FinishServerTime);
            finish.EvaluateRaceEndServer();
            Assert.That(finish.IsRaceForceEnded, Is.True);
            Assert.That(LeaderboardManager.Instance.BuildFinalResultsSnapshot()[0].IsFinished, Is.True);
            float resultDeadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < resultDeadline && MatchResultPresentationCoordinator.Instance.CurrentStage != MatchResultPresentationStage.ResultInteractive)
                yield return null;
            Assert.That(MatchResultPresentationCoordinator.Instance.CurrentStage, Is.EqualTo(MatchResultPresentationStage.ResultInteractive));
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/solo-results.png");
            yield return null;
            Assert.That(GameObject.Find("Rematch").GetComponent<Button>().interactable, Is.True);
            GameObject.Find("Rematch").GetComponent<Button>().onClick.Invoke();
            float rematchDeadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < rematchDeadline && (PropertiesSelectionManager.Instance == null || !PropertiesSelectionManager.Instance.IsClientInitialized))
                yield return null;
            Assert.That(PropertiesSelectionManager.Instance, Is.Not.Null);
            Assert.That(network.ServerManager.Started && network.ClientManager.Started, Is.True);
            SessionControl.Current.RequestStopSession();
            float waitDeadline5 = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < waitDeadline5 && ((network.ClientManager.Started || SceneManager.GetActiveScene().name != SceneNames.MainMenu))) yield return null;
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(MatchServices.Clock, Is.Null);
            Assert.That(MatchServices.Timing, Is.Null);
            RemoveTestKeyboard();
            yield return new ExitPlayMode();
        }
    }
}
