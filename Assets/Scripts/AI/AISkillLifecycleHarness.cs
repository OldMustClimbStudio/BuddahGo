#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using SteamMultiplayer.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BuddahGo.AI
{
    // Controlled cancellation/Rematch/exit checks. Synthetic completion is never natural-race evidence.
    public sealed class AISkillLifecycleHarness : MonoBehaviour
    {
        public string Status { get; private set; }
        private AISkillRaceHarness _race;
        private string _directory;
        private Keyboard _keyboard;
        private readonly List<CheckResult> _checks = new List<CheckResult>();
        private readonly List<string> _errors = new List<string>();
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static AISkillLifecycleHarness Begin(string directory)
        {
            var race = AISkillRaceHarness.Begin(directory, SoloDifficulty.Normal, false, true);
            race.AutoStopAtResults = false;
            var probe = race.gameObject.AddComponent<AISkillLifecycleHarness>();
            probe._race = race; probe._directory = directory; probe.Status = "waiting-go";
            Application.logMessageReceived += probe.Log;
            probe.StartCoroutine(probe.RunGuarded());
            return probe;
        }

        private IEnumerator RunGuarded()
        {
            var run = Run();
            while (true)
            {
                object step;
                try { if (!run.MoveNext()) break; step = run.Current; }
                catch (Exception error) { _errors.Add(error.ToString()); Finish("operator-error"); yield break; }
                yield return step;
            }
        }
        private IEnumerator WaitFor(Func<bool> ready, float seconds = 12f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < until) yield return null;
        }
        private RacerIdentity Racer(int id) => RacerDirectory.Current.All.Single(r => r.Id.Value == id);
        private static void Normal(SkillExecutor executor)
        {
            // Runtime instance only, in an explicitly labelled deterministic effect fixture.
            typeof(ObsessionFigure).GetField("backfirePMaxPercent", Private).SetValue(executor.GetComponent<ObsessionFigure>(), 0f);
        }
        private static void BeginCombo(RacerIdentity racer, int slot)
        {
            var caster = racer.GetComponent<AISkillCaster>(); var input = racer.GetComponent<ComboSkillInput>();
            var executor = racer.GetComponent<SkillExecutor>();
            input.ClearCombo(); input.TryGetSequenceForSlot(slot, out var sequence);
            caster.CastingEnabled = true;
            caster.Commitment.Begin(slot, sequence, executor.TimeManager.LocalTick,
                executor.TimeManager.LocalTick + (uint)Mathf.CeilToInt(6f / (float)executor.TimeManager.TickDelta));
        }

        private IEnumerator Run()
        {
            yield return WaitFor(() => _race.Status == "racing", 90f);
            if (_race.Status != "racing") throw new InvalidOperationException("Lifecycle fixture did not reach GO.");
            yield return new WaitForSeconds(1f);
            Status = "root-cancels-combo";
            var keeper = Racer(10000); var caster = keeper.GetComponent<AISkillCaster>(); var executor = keeper.GetComponent<SkillExecutor>();
            int before = caster.Executed, keys = caster.Keys;
            BeginCombo(keeper, 0);
            yield return WaitFor(() => caster.Keys > keys, 2f);
            executor.ApplyRootThenAccelerationToOwner(.6f, 0f, 0f, .01f);
            yield return new WaitForSeconds(.2f);
            Check("root cancels partially entered combo", !caster.Commitment.Active && !executor.HasPendingCast && caster.Executed == before);
            caster.CastingEnabled = false;
            yield return new WaitForSeconds(.8f); executor.ResetActiveSkillEffectsServer();

            Status = "respawn-cancels-confirmation";
            var hunter = Racer(10001); caster = hunter.GetComponent<AISkillCaster>(); executor = hunter.GetComponent<SkillExecutor>();
            before = caster.Executed; BeginCombo(hunter, 0);
            yield return WaitFor(() => executor.HasPendingCast, 4f);
            Check("combo reached real confirmation", executor.HasPendingCast);
            var motor = hunter.GetComponent<BuddahPredictedMotor>();
            motor.TryApplyServerAuthoritativeTeleport(hunter.transform.position + Vector3.up, hunter.transform.rotation,
                hunter.GetComponent<SplineProgressTracker>().progress01, BuddahPredictedTeleportSourceType.Manual,
                "controlled-lifecycle-reset", true, true, true, true, true, true, true);
            yield return new WaitForSeconds(1.5f);
            Check("respawn cancels pending execution and buff", !executor.HasPendingCast && !caster.Commitment.Active && caster.Executed == before
                && hunter.GetComponent<BuddahHandControl>().ProjectileBuffSecondsLeft == 0f);
            Check("cancel does not refund accepted cooldown", !executor.IsSlotReadyServer(0));
            caster.CastingEnabled = false;

            Status = "concurrent-global-confirmation";
            var trickster = Racer(10003); var opportunist = Racer(10004);
            var first = trickster.GetComponent<SkillExecutor>(); var second = opportunist.GetComponent<SkillExecutor>();
            var firstAI = trickster.GetComponent<AISkillCaster>(); var secondAI = opportunist.GetComponent<AISkillCaster>();
            firstAI.CastingEnabled = secondAI.CastingEnabled = true; Normal(first); Normal(second);
            int firstCount = firstAI.Executed, secondCount = secondAI.Executed, cancellations = secondAI.Cancelled;
            Check("first global request accepted", first.CastSlotServer(1));
            yield return null;
            Check("second global request accepted before first is visible", second.CastSlotServer(2));
            yield return new WaitForSeconds(1.5f);
            Check("visible global effect cancels second confirmation", firstAI.Executed == firstCount + 1
                && secondAI.Executed == secondCount && secondAI.Cancelled == cancellations + 1 && !second.HasPendingCast);
            firstAI.CastingEnabled = secondAI.CastingEnabled = false;
            foreach (var racer in RacerDirectory.Current.All) racer.GetComponent<SkillExecutor>().ResetActiveSkillEffectsServer();

            Status = "finish-cleans-active-effects";
            var brawler = Racer(10002); caster = brawler.GetComponent<AISkillCaster>(); executor = brawler.GetComponent<SkillExecutor>();
            brawler.GetComponent<SkillLoadout>().SetSlotsServer(new[] { "push_projectile_hands", "giant", "slowtrap" });
            caster.CastingEnabled = true; Normal(executor);
            Check("hands fixture accepted", executor.CastSlotServer(0));
            yield return new WaitForSeconds(1.2f);
            Check("buff active before synthetic finish", brawler.GetComponent<BuddahHandControl>().ProjectileBuffSecondsLeft > 0);
            brawler.GetComponent<RaceCompletionTracker>().MarkFinishedServer(1, MatchServices.Clock.Now);
            yield return new WaitForSeconds(.2f);
            brawler.GetComponent<AIRacerDriver>().TryGetOverride(out int steering, out bool drive);
            Check("finished AI stops and clears buff", !drive && steering == 0 && brawler.GetComponent<BuddahHandControl>().ProjectileBuffSecondsLeft == 0f);

            caster = keeper.GetComponent<AISkillCaster>(); executor = keeper.GetComponent<SkillExecutor>();
            caster.CastingEnabled = true; Normal(executor);
            Check("trap fixture accepted", executor.CastSlotServer(0));
            yield return new WaitForSeconds(1.2f);
            Check("zone active before synthetic finish", keeper.GetComponent<MovementSlowTrapZoneEffect>() != null);
            keeper.GetComponent<RaceCompletionTracker>().MarkFinishedServer(2, MatchServices.Clock.Now);
            yield return new WaitForSeconds(.2f);
            Check("finish clears trap zone", keeper.GetComponent<MovementSlowTrapZoneEffect>() == null);

            Status = "actual-rematch";
            var oldRacers = RacerDirectory.Current.All.ToArray();
            var oldNames = oldRacers.OrderBy(r => r.Id.Value).Select(r => r.DisplayName).ToArray();
            File.WriteAllLines(Path.Combine(_directory, "spawned-before-rematch.txt"), FishNet.InstanceFinder.ServerManager.Objects.Spawned
                .Select(pair => pair.Key + ":" + (pair.Value == null ? "DESTROYED" : pair.Value.name)));
            _race.Complete("controlled-lifecycle-boundaries", false);
            RaceFinishManager.Instance.ForceEndRaceServer();
            yield return WaitFor(() => MatchResultPresentationCoordinator.Instance != null
                && MatchResultPresentationCoordinator.Instance.CurrentStage == MatchResultPresentationStage.ResultInteractive, 30f);
            var view = FindFirstObjectByType<SoloResultsView>();
            var button = (Button)typeof(SoloResultsView).GetField("_rematch", Private).GetValue(view);
            Check("real rematch button available", button != null && button.interactable);
            button.onClick.Invoke();
            yield return WaitFor(() => PropertiesSelectionManager.Instance != null && PropertiesSelectionManager.Instance.IsClientInitialized
                && PropertiesSelectionManager.Instance.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey, 20f);
            var selection = PropertiesSelectionManager.Instance;
            File.WriteAllLines(Path.Combine(_directory, "spawned-after-rematch.txt"), FishNet.InstanceFinder.ServerManager.Objects.Spawned
                .Select(pair => pair.Key + ":" + (pair.Value == null ? "DESTROYED" : pair.Value.name)));
            Check("rematch leaves no destroyed server objects", FishNet.InstanceFinder.ServerManager.Objects.Spawned.Values.All(value => value != null));
            if (selection == null) throw new InvalidOperationException("Rematch did not reach property selection.");
            selection.SubmitSkillLoadoutSelection(new[] { "acceleration", "slowtrap", "blackcurtain" });
            yield return WaitFor(() => selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkinStageKey);
            var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
            selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId);
            yield return WaitFor(() => RacerDirectory.Current != null && RacerDirectory.Current.All.Count == 6 && oldRacers.All(r => r == null), 25f);
            if (RacerDirectory.Current == null || RacerDirectory.Current.All.Count != 6)
                throw new InvalidOperationException("Rematch failed to spawn six new racers; inspect scene-load and despawn evidence.");
            var fresh = RacerDirectory.Current.All.OrderBy(r => r.Id.Value).ToArray();
            Check("rematch replaces six racers without duplicates", fresh.Length == 6 && oldRacers.All(r => r == null));
            Check("rematch retains names", oldNames.SequenceEqual(fresh.Select(r => r.DisplayName)));
            foreach (var racer in fresh.Where(r => r.IsAI))
            {
                int index = racer.Id.AIIndex; var profile = AISkillCatalog.Current.Personalities[index];
                var loadout = racer.GetComponent<SkillLoadout>(); var ai = racer.GetComponent<AISkillCaster>();
                Check("rematch fixed personality/loadout " + profile.Id, ai.Personality.Id == profile.Id
                    && Enumerable.Range(0, 3).All(slot => loadout.GetSkillId(slot) == profile.Loadout[slot])
                    && ai.Executed == 0 && !ai.Commitment.Active && !racer.GetComponent<SkillExecutor>().HasPendingCast);
                ai.CastingEnabled = false;
            }

            Status = "active-effects-quit";
            yield return WaitFor(() => RoomStateManager.Instance != null && RoomStateManager.Instance.IsAuthoritativeGoIssued, 80f);
            yield return new WaitForSeconds(1f);
            foreach (int id in new[] { 10000, 10001, 10003 })
            {
                var racer = Racer(id); racer.GetComponent<AISkillCaster>().enabled = false;
                executor = racer.GetComponent<SkillExecutor>(); Normal(executor);
                Check("active exit cast " + id, executor.CastSlotServer(id == 10003 ? 1 : 0));
            }
            yield return new WaitForSeconds(2.2f);
            Racer(10001).GetComponent<BuddahHandControl>().InjectServerPush(true);
            Check("exit has live buff, trap and curtain", Racer(10001).GetComponent<BuddahHandControl>().ProjectileBuffSecondsLeft > 0f
                && Racer(10000).GetComponent<MovementSlowTrapZoneEffect>() != null
                && Racer(0).GetComponent<SkillPerceptionState>().VisionImpairedUntilTick > Racer(0).TimeManager.LocalTick);
            _keyboard = InputSystem.AddDevice<Keyboard>("AISkillLifecycleKeyboard");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            var quit = FindFirstObjectByType<QuitConfirmDialog>();
            Check("Escape opens real quit dialog", quit != null && quit.IsBlocked);
            var confirm = (Button)typeof(QuitConfirmDialog).GetField("_confirm", Private).GetValue(quit);
            confirm.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && !FishNet.InstanceFinder.IsServerStarted, 20f);
            Check("Home releases session and racers", !FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted
                && MatchServices.Clock == null && (RacerDirectory.Current == null || RacerDirectory.Current.All.Count == 0));
            Check("active effects leave no runtime objects", FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None).Length == 0
                && FindObjectsByType<ChargedHandProjectileRuntime>(FindObjectsSortMode.None).Length == 0
                && FindObjectsByType<HandPushProjectileRuntime>(FindObjectsSortMode.None).Length == 0
                && FindObjectsByType<MovementSlowTrapZoneEffect>(FindObjectsSortMode.None).Length == 0
                && FindObjectsByType<BlackCurtainViewController>(FindObjectsSortMode.None).Length == 0);
            Finish("completed");
        }

        private void Check(string name, bool passed)
        {
            _checks.Add(new CheckResult { Name = name, Passed = passed });
            File.WriteAllText(Path.Combine(_directory, "lifecycle-progress.json"), JsonUtility.ToJson(new Report { Status = Status, Checks = _checks.ToArray(), Errors = _errors.ToArray() }, true));
        }
        private void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) _errors.Add(message); }
        private void Finish(string status)
        {
            Status = status;
            File.WriteAllText(Path.Combine(_directory, "lifecycle.json"), JsonUtility.ToJson(new Report { Status = status,
                Passed = status == "completed" && _checks.All(c => c.Passed) && _errors.Count == 0, Checks = _checks.ToArray(), Errors = _errors.ToArray() }, true));
            Application.logMessageReceived -= Log;
            if (_keyboard != null) { InputSystem.RemoveDevice(_keyboard); _keyboard = null; }
            if (status != "completed") SessionControl.Current?.RequestStopSession();
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        }
        [Serializable] private struct CheckResult { public string Name; public bool Passed; }
        [Serializable] private sealed class Report { public string Status; public bool Passed; public CheckResult[] Checks; public string[] Errors; }
    }
}
#endif
