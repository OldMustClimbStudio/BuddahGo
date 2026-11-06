using System;
using System.Collections;
using System.Linq;
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
using Object = UnityEngine.Object;

namespace BuddahGo.Tests
{
    public class SoloSessionFlowTests
    {
        private const string MenuScene = "Assets/Scenes/MenuEnvironmentSetting.unity";
        private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        private Keyboard _keyboard;
        private Action _pendingInput;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInputBehavior;
        private bool _inputSettingsChanged;
        private string[] _savedSkills;
        private string _savedSkin;

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

        [SetUp]
        public void SaveMenuPick()
        {
            // The tests write the menu pick (PlayerPrefs); give the developer's own pick back afterwards.
            _savedSkills = LocalMatchSelection.Skills;
            _savedSkin = LocalMatchSelection.SkinId;
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
            if (_savedSkills != null) LocalMatchSelection.SetSkills(_savedSkills);
            if (_savedSkin != null) LocalMatchSelection.SetSkin(_savedSkin);
        }

        private static IEnumerator WaitUntil(Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline && !done()) yield return null;
        }

        private static bool RoomLeftMenu() => RoomStateManager.Instance != null && RoomStateManager.Instance.IsClientInitialized
            && RoomStateManager.Instance.CurrentMatchSessionPhase != MatchSessionPhase.InRoom;

        private static bool RaceSceneActive() => SceneManager.GetActiveScene().name == SceneNames.RaceMap;

        // Lambdas here must not capture locals: entering Play mode reloads the domain and the test framework rebuilds
        // the iterator without the closure object the compiler allocated at the top of the method.
        private static bool StoppedInMenu()
        {
            NetworkManager network = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.FishNetManager : null;
            return network != null && !network.ClientManager.Started && SceneManager.GetActiveScene().name == SceneNames.MainMenu;
        }

        // The persistent session objects must survive the menu → race scene change.
        private static NetworkManager LiveNetwork()
        {
            Assert.That(GameNetworkManager.Instance, Is.Not.Null, "GameNetworkManager must persist into the race");
            Assert.That(SessionControl.Current, Is.Not.Null, "SessionControl must persist into the race");
            Assert.That(RoomStateManager.Instance, Is.Not.Null, "RoomStateManager must persist into the race");
            NetworkManager network = GameNetworkManager.Instance.FishNetManager;
            Assert.That(network, Is.Not.Null, "GameNetworkManager has no FishNet NetworkManager");
            return network;
        }

        private static string[] SelectableSkills()
        {
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Skills_Assets/Skill Database.asset");
            return database.BuildSelectableSkillOptions().Where(o => o.IsUnlocked).Select(o => o.OptionId).ToArray();
        }

        [UnityTest]
        public IEnumerator StartupRollback_AfterRaceLoad_ReturnsToMenuWithTheError()
        {
            EditorSceneManager.OpenScene(MenuScene);
            yield return new EnterPlayMode();
            yield return null;
            float projectMaximumDeltaTime = Time.maximumDeltaTime;
            Assert.That(SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Hard)), Is.True);
            // Solo physics runs on FixedUpdate; its slow-frame catch-up must not exceed the TimeManager's tick budget.
            Assert.That(Time.maximumDeltaTime, Is.EqualTo(Time.fixedDeltaTime * SessionLauncher.SoloMaxPhysicsStepsPerFrame).Within(1e-6f));
            yield return WaitUntil(RaceSceneActive, 45f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.RaceMap), "Solo goes straight from the menu into the race.");
            var network = LiveNetwork();
            // Inject a startup failure at the private rollback boundary after the menu unloaded.
            // This tests recovery, not the cause or duration of a real authentication timeout.
            var rollback = typeof(SessionLauncher).GetMethod("Rollback", Private);
            Assert.That(rollback, Is.Not.Null);
            Assert.That(rollback.Invoke(SessionControl.Current, new object[] { "Injected startup failure" }), Is.EqualTo(false));
            Assert.That(network.ClientManager.Started, Is.True, "Failure cleanup is also deferred out of its calling stack.");
            yield return WaitUntil(StoppedInMenu, 45f);
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            Assert.That(SessionControl.Current.LastError, Is.EqualTo("Injected startup failure"));
            Assert.That(Time.maximumDeltaTime, Is.EqualTo(projectMaximumDeltaTime), "Leaving Solo restores the project's catch-up limit.");
            Assert.That(LocalInputBlock.IsBlocked, Is.False);
            yield return null;
            var dojo = Object.FindFirstObjectByType<MenuDojoPage>();
            Assert.That(dojo, Is.Not.Null);
            Assert.That(typeof(MenuDojoPage).GetField("_soloError", Private).GetValue(dojo), Is.EqualTo("Injected startup failure"),
                "The Dojo page takes the failed start back and shows its error.");
            Assert.That(SessionControl.Current.TakeFailedSoloSettings(), Is.Null, "The page consumed the failed request.");
            Assert.That(MatchServices.Clock, Is.Null);
            Assert.That(MatchServices.Timing, Is.Null);
            Assert.That(Object.FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None).Length, Is.Zero);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DojoPractice_StartsZeroAIPracticeStraightIntoTheRace()
        {
            // The Dojo page's 练习 entry: Solo host, 0 AI, Practice rules, no selection scene in between.
            EditorSceneManager.OpenScene(MenuScene);
            yield return new EnterPlayMode();
            yield return null;
            var dojo = Object.FindFirstObjectByType<MenuDojoPage>();
            Assert.That(dojo, Is.Not.Null);
            typeof(MenuDojoPage).GetMethod("StartSolo", Private).Invoke(dojo, new object[] { true });
            Assert.That(MatchRules.Current.SpawnTrainingDummies, Is.True, SessionControl.Current.LastError);
            Assert.That(MatchRules.Current.EndOnHumanFinish, Is.True);
            Assert.That(typeof(MenuDojoPage).GetField("_soloRequested", Private).GetValue(dojo), Is.True, "One request per press.");
            yield return WaitUntil(RoomLeftMenu, 45f);
            Assert.That(RoomStateManager.Instance.Players.Count, Is.EqualTo(1), "Practice has one player and no AI.");
            yield return WaitUntil(RaceSceneActive, 45f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.RaceMap));
            var network = LiveNetwork();
            SessionControl.Current.RequestStopSession();
            yield return WaitUntil(StoppedInMenu, 45f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Practice_RacesWithTheMenuPick_ExitsReopensAndRematches()
        {
            EditorSceneManager.OpenScene(MenuScene);
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(SessionControl.Current, Is.Not.Null);
            ScreenCapture.CaptureScreenshot("Temp/solo-menu.png");
            yield return null;
            Assert.That(SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)), Is.True, SessionControl.Current.LastError);
            Assert.That(MatchRules.Current.AutoStartRoom, Is.True);
            yield return WaitUntil(RaceSceneActive, 45f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.RaceMap), "Auto-start goes straight into the race.");
            Assert.That(RoomStateManager.Instance.Players.Count, Is.EqualTo(1));
            if (!SessionControl.Current.IsOnlineAvailable)
                Assert.That(RoomStateManager.Instance.Players[0].PlayerName, Is.EqualTo("玩家"));

            var network = LiveNetwork();
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
            Assert.That(LocalInputBlock.IsBlocked, Is.True, $"Esc must open the quit modal before GO. Dialogs={Object.FindObjectsByType<QuitConfirmDialog>(FindObjectsSortMode.None).Length}; keyboard={Keyboard.current == _keyboard}; pressed={_keyboard.escapeKey.isPressed}; enabled={_keyboard.enabled}; frame={Time.frameCount}");
            QueueKeyboardState(new KeyboardState());
            yield return null;
            GameObject.Find("ConfirmQuit").GetComponent<Button>().onClick.Invoke();
            Assert.That(network.ClientManager.Started, Is.True, "Stop must be deferred out of the calling RPC/UI stack.");
            yield return WaitUntil(StoppedInMenu, 45f);
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            Assert.That(MatchRules.Current.RequiresReady, Is.True);

            // The menu pick (skill page, skin page) is what the racer gets.
            string[] offered = SelectableSkills();
            Assert.That(offered.Length, Is.GreaterThanOrEqualTo(3));
            string[] pick = { offered[2], offered[0], offered[1] };
            LocalMatchSelection.SetSkills(pick);
            LocalMatchSelection.SetSkin("zhehong");
            Assert.That(SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)), Is.True,
                SessionControl.Current.LastError);
            float raceStartDeadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < raceStartDeadline && (RoomStateManager.Instance == null || !RoomStateManager.Instance.IsAuthoritativeGoIssued)) yield return null;
            Assert.That(RoomStateManager.Instance.IsAuthoritativeGoIssued, Is.True, "Start and intro must reach authoritative GO.");
            float movementDeadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < movementDeadline && !RoomStateManager.Instance.IsGameplayMovementUnlocked) yield return null;
            Assert.That(RoomStateManager.Instance.IsGameplayMovementUnlocked, Is.True);

            var reporters = Object.FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None);
            var ownedReporters = Array.FindAll(reporters, reporter => reporter.IsOwner);
            Assert.That(ownedReporters.Length, Is.EqualTo(1));
            var human = ownedReporters[0];
            var loadout = human.GetComponent<SkillLoadout>();
            Assert.That(Enumerable.Range(0, SkillLoadout.SlotCount).Select(loadout.GetSkillId).ToArray(), Is.EqualTo(pick), "The racer's slots are the menu pick, in order.");
            var skin = human.GetComponent<RacerSkinApplier>();
            float skinDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < skinDeadline && skin.AppliedSkinId != "zhehong") yield return null;
            Assert.That(skin.AppliedSkinId, Is.EqualTo("zhehong"), "The racer wears the menu's skin.");

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
            // Practice (0 AI) adds three training dummies from the same prefab; they carry reporters but are never racers.
            var dummies = Array.FindAll(Object.FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None), identity => identity.IsTrainingDummy);
            Assert.That(dummies.Length, Is.EqualTo(3), "Practice spawns three training dummies.");
            Assert.That(reporters.Length, Is.EqualTo(ownedReporters.Length + dummies.Length));
            foreach (var dummy in dummies)
            {
                Assert.That(dummy.IsAssigned, Is.False, "A training dummy keeps the unassigned racer id.");
                Assert.That(dummy.OwnerId, Is.EqualTo(-1));
                Assert.That(RacerAuthority.IsServerDummy(dummy) && RacerAuthority.IsServerControlled(dummy), Is.True);
                Assert.That(RacerAuthority.IsServerAI(dummy), Is.False);
                Assert.That(RacerAuthority.HasLocalControl(dummy.NetworkObject), Is.True, "The server parks the dummy through the motor override.");
                Assert.That(RacerAuthority.IsProgressAuthority(dummy), Is.False, "No lap, progress or wrong-way logic runs for a dummy.");
                Assert.That(dummy.GetComponent<Rigidbody>().isKinematic, Is.False, "The host must be able to push a dummy.");
                Assert.That(dummy.GetComponent<RaceBodyIntroStateController>().HasAssignment, Is.False, "The intro neither flies nor launches dummies.");
                Assert.That(dummy.GetComponent<TrainingDummy>(), Is.Not.Null);
                Assert.That(dummy.GetComponent<RacerSkinApplier>().AppliedSkinId, Is.Null, "Dummies keep the prefab's material.");
            }
            Assert.That(RacerDirectory.Current.All.Count, Is.EqualTo(1), "Dummies are not in the racer directory.");
            Assert.That(LeaderboardManager.Instance.Rankings.Count, Is.EqualTo(1), "Dummies are not ranked.");

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
            var live = human.GetComponent<RaceCompletionTracker>();
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
            Assert.That(LeaderboardManager.Instance.BuildFinalResultsSnapshot().Count, Is.EqualTo(1), "Results never list training dummies.");
            yield return WaitUntil(() => MatchResultPresentationCoordinator.Instance.CurrentStage == MatchResultPresentationStage.ResultInteractive, 45f);
            Assert.That(MatchResultPresentationCoordinator.Instance.CurrentStage, Is.EqualTo(MatchResultPresentationStage.ResultInteractive));
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/solo-results.png");
            yield return null;
            Assert.That(GameObject.Find("Rematch").GetComponent<Button>().interactable, Is.True);
            GameObject.Find("Rematch").GetComponent<Button>().onClick.Invoke();
            // A rematch passes through the menu scene (FishNet does not reload a loaded scene) and races again.
            float rematchDeadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < rematchDeadline && !(human == null && RaceSceneActive() && RoomStateManager.Instance != null
                && RoomStateManager.Instance.CurrentMatchSessionPhase == MatchSessionPhase.InMatch)) yield return null;
            Assert.That(human == null, Is.True, "The old racer is gone.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.RaceMap));
            Assert.That(RoomStateManager.Instance.CurrentMatchSessionPhase, Is.EqualTo(MatchSessionPhase.InMatch));
            Assert.That(network.ServerManager.Started && network.ClientManager.Started, Is.True);
            yield return WaitUntil(() => Array.Exists(Object.FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None), r => r.IsOwner), 30f);
            var again = Array.Find(Object.FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None), r => r.IsOwner);
            Assert.That(again, Is.Not.Null, "The rematch spawns the player again.");
            Assert.That(Enumerable.Range(0, SkillLoadout.SlotCount).Select(again.GetComponent<SkillLoadout>().GetSkillId).ToArray(), Is.EqualTo(pick),
                "The rematch keeps the menu pick.");
            SessionControl.Current.RequestStopSession();
            yield return WaitUntil(StoppedInMenu, 45f);
            Assert.That(network.ServerManager.Started || network.ClientManager.Started, Is.False);
            Assert.That(MatchServices.Clock, Is.Null);
            Assert.That(MatchServices.Timing, Is.Null);
            RemoveTestKeyboard();
            yield return new ExitPlayMode();
        }
    }
}
