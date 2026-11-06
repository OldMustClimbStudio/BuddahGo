#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BuddahGo.Match;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using CastStage = SkillExecutor.CastStage;

namespace BuddahGo.AI
{
    // Practice (Solo, 0 AI) verification probe for the mechanics update: Docs/gameplay/README.md §7.1 A1–A6
    // "训练模式验证". It drives the real Practice flow (host, loadout, skin, intro, GO),
    // then runs controlled fixtures one by one and writes report.json / report.md / events.jsonl. Fixture tools, all
    // runtime-only and restored: reflection on ObsessionFigure.backfirePMaxPercent / backfireKeepPMaxAtOrAboveX (forced or
    // disabled backfire), SkillExecutor._nextReadyTime / _repeatSurcharge (cooldown and stack reset between scenarios),
    // harness teleports, a parking ISteeringOverride on the human (lifted only in S13, which drives with W), a virtual keyboard and mouse,
    // and direct server authority calls. It never edits gameplay state outside those, and it is never natural-race
    // evidence. Start it from an idle MainMenu in Play mode: MechanicsPracticeHarness.Begin(absoluteOutputDir); poll
    // Status / IsRunning / LastReportPath. A finished run destroys its host, so it can be repeated in the same Play
    // session (from an idle MainMenu, with a new directory). Tick-defined windows are measured in network ticks.
    public sealed class MechanicsPracticeHarness : MonoBehaviour
    {
        public static string Status { get; private set; } = "idle";
        public static bool IsRunning { get; private set; }
        public static string LastReportPath { get; private set; } = string.Empty;
        public static string LastOutputDirectory { get; private set; } = string.Empty;
        public static string Summary { get; private set; } = string.Empty;

        private const string Tag = "[MechanicsHarness]";
        private const float StagingMetresBeforeFirstDummy = 40f;
        private static readonly string[] DefaultLoadout = { "acceleration", "slowtrap", "giant" };
        private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private string _directory;
        private bool _endRaceAtEnd = true;
        private HashSet<string> _only;
        private string[] _requestedLoadout;
        private string[] _submittedLoadout;
        private readonly PracticeFlow _flow = new PracticeFlow();
        private MeasurementSettings _measurement;
        private bool _measurementApplied, _finished, _logHooked, _destroying;
        private StreamWriter _events;
        private string _startedUtc;

        // The human and its parts (host: these are the server objects).
        private RacerIdentity _human;
        private SkillExecutor _exec;
        private ObsessionFigure _obs;
        private BuddahPredictedMotor _motor;
        private BuddahHandControl _hands;
        private SkillLoadout _loadout;
        private SkillDatabase _db;
        private string _dbError;
        private MechanicsHarnessParkOverride _park;
        private float _baseMaxSpeed = 80f;
        private int _humanRacerId = int.MinValue;
        private readonly List<Dummy> _dummies = new List<Dummy>();
        private float _stagingProgress;
        private float _stagedAt;

        // Backfire fixture (ObsessionFigure serialized fields after the table overlay).
        private bool _backfireSaved;
        private float _origPmax, _origKeep;

        // Virtual input (scoped to S1 / S2).
        private Keyboard _keyboard;
        private Mouse _mouse;
        private bool _inputScope;
        private InputSettings.BackgroundBehavior _savedBackground;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _savedEditorBehavior;
#endif
        private readonly List<Action> _inputQueue = new List<Action>();
        private int _inputFlushes;

        // Recorders.
        private readonly List<CastRec> _casts = new List<CastRec>();
        private readonly List<UiRec> _uiEvents = new List<UiRec>();
        private readonly List<BackfireRec> _backfireEvents = new List<BackfireRec>();
        private float _obsPrevFrame;

        // Report.
        private readonly List<CheckRow> _checks = new List<CheckRow>();
        private readonly List<ScenarioRow> _scenarios = new List<ScenarioRow>();
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _notes = new List<string>();
        private ScenarioRow _current;
        private int _currentErrors;
        private readonly List<string> _currentErrorSamples = new List<string>();

        public static MechanicsPracticeHarness Begin(string absoluteOutputDir) => Begin(absoluteOutputDir, true, null, null);

        /// <param name="endRaceAtEnd">S12 force-ends the race (synthetic end) and checks the results screen.</param>
        /// <param name="scenarioFilter">Comma-separated scenario ids to run (e.g. "S0,S5"); null runs all.</param>
        /// <param name="loadout">Three skill ids submitted at the selection stage; null uses acceleration, slowtrap, giant.</param>
        public static MechanicsPracticeHarness Begin(string absoluteOutputDir, bool endRaceAtEnd, string scenarioFilter, string[] loadout)
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != SceneNames.MainMenu || MatchServices.Clock != null)
                throw new InvalidOperationException("Start the mechanics Practice harness from an idle MainMenu in Play mode.");
            // A finished harness destroys its host in Finish; one that is finishing this frame is ignored.
            if (IsRunning || FindObjectsByType<MechanicsPracticeHarness>(FindObjectsSortMode.None).Any(h => h != null && !h._finished))
                throw new InvalidOperationException("A mechanics Practice harness is already running.");
            if (SessionControl.Current == null)
                throw new InvalidOperationException("No SessionControl in MainMenu; cannot start Practice.");
            if (string.IsNullOrWhiteSpace(absoluteOutputDir) || !Path.IsPathRooted(absoluteOutputDir))
                throw new ArgumentException("Use an absolute evidence directory.");
            if (Directory.Exists(absoluteOutputDir) && Directory.EnumerateFileSystemEntries(absoluteOutputDir).Any())
                throw new ArgumentException("Use a new or empty evidence directory; previous runs are never overwritten.");
            Directory.CreateDirectory(absoluteOutputDir);

            var host = new GameObject("Mechanics Practice Harness");
            DontDestroyOnLoad(host);
            var h = host.AddComponent<MechanicsPracticeHarness>();
            h._directory = absoluteOutputDir;
            h._endRaceAtEnd = endRaceAtEnd;
            h._requestedLoadout = loadout != null && loadout.Length >= SkillLoadout.SlotCount ? loadout.Take(SkillLoadout.SlotCount).ToArray() : DefaultLoadout;
            if (!string.IsNullOrWhiteSpace(scenarioFilter))
                h._only = new HashSet<string>(scenarioFilter.Split(',').Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0));
            h._startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            h._events = new StreamWriter(Path.Combine(absoluteOutputDir, "events.jsonl")) { AutoFlush = true };
            h._measurement = MeasurementSettings.Apply();
            h._measurementApplied = true;
            Application.logMessageReceived += h.OnLog;
            h._logHooked = true;

            Status = "starting";
            IsRunning = true;
            Summary = string.Empty;
            LastOutputDirectory = absoluteOutputDir;
            LastReportPath = Path.Combine(absoluteOutputDir, "report.json");
            h._notes.Add("CONTROLLED FIXTURE: runtime-only reflection on ObsessionFigure backfire fields and SkillExecutor cooldown/surcharge state (restored), harness teleports, a parking steering override on the human, a virtual keyboard/mouse and direct server authority calls. Not natural race evidence.");
            h.Log("begin; evidence " + absoluteOutputDir);
            h.StartCoroutine(h.Outer());
            return h;
        }

        // ---------------------------------------------------------------- run structure

        private IEnumerator Outer()
        {
            var result = new GuardResult();
            yield return Guard(Main(), 3600f, result);
            if (result.Failed)
            {
                _errors.Add("harness: " + result.Reason);
                Finish("operator-error");
            }
        }

        private IEnumerator Main()
        {
            var setup = new GuardResult();
            yield return Guard(Setup(), 300f, setup);
            if (setup.Failed)
            {
                _errors.Add("setup: " + setup.Reason);
                Finish("setup-failed");
                yield break;
            }

            foreach (Scenario scenario in BuildScenarios())
            {
                if (_only != null && !_only.Contains(scenario.Id))
                    continue;
                yield return RunScenario(scenario);
            }
            Finish("completed");
        }

        private IEnumerable<Scenario> BuildScenarios()
        {
            string[] noPairCast = { "acceleration", "blackcurtain", "reverseturn" };
            yield return new Scenario("S0", "spawn: three parked training dummies, identity, 1P intro", 30f, S0Spawn, null, true);
            yield return new Scenario("S1", "skill keys Q / 2 / R through the real input path; W and ↑ neither cast nor push", 40f, S1SkillKeys, _submittedLoadout);
            yield return new Scenario("S2", "mouse aim (rate cap, ground point) and mouse push buttons", 40f, S2MouseAim, _submittedLoadout);
            yield return new Scenario("S3", "cast serialization: one pending cast, two effects at once", 25f, S3Serialization, new[] { "slowtrap", "giant", "blackcurtain" });
            yield return new Scenario("S4", "same-skill surcharge x1.15 compounding, reset after the window", 70f, S4Surcharge, noPairCast);
            yield return new Scenario("S5", "backfire: the skill fails and its _anti variant runs; locks all slots, freezes obsession, x0.85 speed, survives respawn, x0.8 at the end", 50f, S5Backfire, noPairCast);
            yield return new Scenario("S6", "freeze interrupt: cancel + refund; a rolled backfire survives and still runs its _anti variant", 40f, S6FreezeInterrupt, noPairCast);
            yield return new Scenario("S7", "dummies: slow + readout, invert, impulse and 3 s reset", 80f, S7Dummies, new[] { "slowtrap", "reverseturn", "blackcurtain" });
            yield return new Scenario("S11", "HUD smoke: key labels, cooldown text, backfire probability text", 30f, S11Hud, _submittedLoadout);
            // S13 runs before S12, which ends the race.
            yield return new Scenario("S13", "drive: no W = no push, W held pushes, release coasts", 30f, S13Drive, null);
            yield return new Scenario("S12", "rankings / results list only the human", 80f, S12Results, null);
        }

        private IEnumerator RunScenario(Scenario scenario)
        {
            _current = new ScenarioRow { Id = scenario.Id, Title = scenario.Title, Result = "RUNNING" };
            _scenarios.Add(_current);
            _currentErrors = 0;
            _currentErrorSamples.Clear();
            float started = Time.realtimeSinceStartup;
            Status = scenario.Id + " " + scenario.Title;
            Log("scenario " + scenario.Id + " begin: " + scenario.Title);
            Event("scenario-begin", scenario.Id);

            if (!SessionAlive())
            {
                _current.Result = "SKIP";
                _current.Note = "Practice session is no longer running";
                SafeWriteReport("running");
                yield break;
            }

            // A failed reset leaves the human locked, frozen, scaled or away from the staging spot, or the dummies away
            // from home: the body would then fail for the wrong reason, so it is skipped and the reset reason reported.
            string resetFailure = null;
            if (!scenario.NoReset)
            {
                var reset = new GuardResult();
                yield return Guard(ResetFixture(scenario.Loadout), 60f, reset);
                if (reset.Failed)
                {
                    resetFailure = reset.Reason;
                    Fail("fixture reset", "clean fixture before the scenario", reset.Reason);
                }
            }

            if (resetFailure == null)
            {
                var body = new GuardResult();
                yield return Guard(scenario.Body(), scenario.Timeout, body);
                if (body.Failed)
                    Fail("scenario completed", "no exception, no timeout", body.Reason);
            }
            else
            {
                Log("scenario " + scenario.Id + " body skipped: fixture reset failed");
            }

            var cleanup = new GuardResult();
            yield return Guard(ScenarioCleanup(), 10f, cleanup);

            // Bookkeeping must never stop the remaining scenarios.
            try
            {
                if (cleanup.Failed)
                    Warn("scenario cleanup", "clean", cleanup.Reason);
                if (_currentErrors > 0)
                    Warn("console errors during the scenario", "0", _currentErrors + ": " + string.Join(" || ", _currentErrorSamples));

                int fails = _checks.Count(c => c.Scenario == _current.Id && c.Result == "FAIL");
                int passes = _checks.Count(c => c.Scenario == _current.Id && c.Result == "PASS");
                _current.Pass = passes;
                _current.Fail = fails;
                _current.Seconds = Time.realtimeSinceStartup - started;
                _current.Result = fails > 0 ? "FAIL" : "PASS";
                if (resetFailure != null)
                    _current.Note = "body skipped: fixture reset failed (" + resetFailure + ")";
                Log("scenario " + scenario.Id + " " + _current.Result + " (" + passes + " pass, " + fails + " fail, " + F(_current.Seconds, "0.0") + " s)");
                Event("scenario-end", scenario.Id + " " + _current.Result);
            }
            catch (Exception error)
            {
                _errors.Add("scenario " + scenario.Id + " bookkeeping: " + error.GetType().Name + ": " + error.Message);
            }
            SafeWriteReport("running");
        }

        private IEnumerator ScenarioCleanup()
        {
            EndInputScope();
            SetBaselineBackfire();
            yield break;
        }

        // Runs nested IEnumerators manually so an exception or a timeout anywhere inside is caught here and the abandoned
        // iterators are disposed (their finally blocks run). Only null / YieldInstructions reach Unity.
        private IEnumerator Guard(IEnumerator body, float timeoutSeconds, GuardResult result)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (stack.Count > 0)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    result.Failed = true;
                    result.Reason = "timeout after " + F(timeoutSeconds, "0") + " s";
                    DisposeAll(stack);
                    yield break;
                }

                IEnumerator top = stack.Peek();
                object current;
                try
                {
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        (top as IDisposable)?.Dispose();
                        continue;
                    }
                    current = top.Current;
                }
                catch (Exception error)
                {
                    result.Failed = true;
                    result.Reason = error.GetType().Name + ": " + error.Message + FirstFrame(error);
                    DisposeAll(stack);
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }
        }

        private void DisposeAll(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                IEnumerator item = stack.Pop();
                try { (item as IDisposable)?.Dispose(); }
                catch (Exception error) { _errors.Add("dispose: " + error.Message); }
            }
        }

        private static string FirstFrame(Exception error)
        {
            string trace = error.StackTrace;
            if (string.IsNullOrEmpty(trace))
                return string.Empty;
            string line = trace.Split('\n').FirstOrDefault(l => l.Contains("MechanicsPracticeHarness")) ?? trace.Split('\n')[0];
            return " @ " + line.Trim();
        }

        // ---------------------------------------------------------------- setup

        private IEnumerator Setup()
        {
            Status = "selection";
            float deadline = Time.realtimeSinceStartup + 150f;
            while (!_flow.Advance(_requestedLoadout))
            {
                if (_human == null)
                    TryAttachHuman();
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Practice selection flow did not finish (stage " + _flow.Stage + ").");
                yield return null;
            }
            _submittedLoadout = _flow.Submitted;
            if (!string.IsNullOrEmpty(_flow.Note))
                _notes.Add(_flow.Note);
            Event("loadout-submitted", string.Join(",", _submittedLoadout));

            Status = "waiting-go";
            deadline = Time.realtimeSinceStartup + 120f;
            while (RoomStateManager.Instance == null || !RoomStateManager.Instance.IsAuthoritativeGoIssued)
            {
                if (_human == null)
                    TryAttachHuman();
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Practice did not reach authoritative GO.");
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 20f;
            while (!RoomStateManager.Instance.IsGameplayMovementUnlocked)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Gameplay movement did not unlock after GO.");
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 10f;
            while (!TryAttachHuman())
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new InvalidOperationException("No owned human racer found after GO.");
                yield return null;
            }
            // FishNet changes the frame cap on host startup; re-establish the measurement cap after it (as A3 does).
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            _notes.Add("MatchRules.SpawnTrainingDummies=" + MatchRules.Current.SpawnTrainingDummies + " (Practice only)");
            yield return Until(() => !_motor.IsLaunchHandoffActive && !_motor.IsAuthoritativeLaunchHandoffPending, 10f);
            // A teleport clears the handoff state but not intro / external kinematic control, so never stage over it.
            if (_motor.IsLaunchHandoffActive || _motor.IsAuthoritativeLaunchHandoffPending)
                throw new InvalidOperationException("Launch handoff still active 10 s after GO (" + HandoffState() + ").");

            CollectDummies();
            float length = TrackSplineRef.Instance != null ? TrackSplineRef.Instance.TrackLength : 0f;
            if (length <= 0f)
                throw new InvalidOperationException("RaceMap needs an initialized TrackSplineRef.");
            _stagingProgress = _dummies.Count > 0
                ? Mathf.Repeat(_dummies[0].HomeProgress - StagingMetresBeforeFirstDummy / length, 1f)
                : ProjectProgress01(_human.transform.position);
            PlaceHuman(_stagingProgress, 0f, true, "mechanics-harness staging");
            _stagedAt = Time.time;
            Event("staged", "progress01=" + F(_stagingProgress, "0.0000") + " dummies=" + _dummies.Count);
            Status = "racing";
            yield return Wait(1f);
        }

        private bool TryAttachHuman()
        {
            if (_human != null)
                return true;
            RacerIdentity human = null;
            foreach (RacerIdentity racer in FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None))
            {
                if (racer != null && !racer.IsTrainingDummy && racer.IsAssigned && !racer.IsAI && racer.IsOwner)
                {
                    human = racer;
                    break;
                }
            }
            if (human == null)
                return false;

            var executor = human.GetComponent<SkillExecutor>();
            var obsession = human.GetComponent<ObsessionFigure>();
            var motor = human.GetComponent<BuddahPredictedMotor>();
            var hands = human.GetComponent<BuddahHandControl>();
            var loadout = human.GetComponent<SkillLoadout>();
            if (executor == null || obsession == null || motor == null || hands == null || loadout == null || !executor.IsServerInitialized)
                return false;

            _human = human;
            _exec = executor;
            _obs = obsession;
            _motor = motor;
            _hands = hands;
            _loadout = loadout;
            _humanRacerId = human.Id.Value;
            var config = human.GetComponent<BuddahPredictedMotorConfig>();
            if (config != null)
                _baseMaxSpeed = config.MaxSpeed;
            // A renamed field fails only the scenarios that need the database, naming the field (ResolveBaseGain etc.).
            try { _db = GetOrNull<SkillDatabase>(_exec, "database"); }
            catch (MissingFieldException error)
            {
                _db = null;
                _dbError = "reflection: " + error.Message;
                _errors.Add(_dbError);
            }

            _exec.ServerCastChanged += OnCast;
            _exec.LocalSkillUiTriggered += OnSkillUi;
            _obs.BackfireStateChanged += OnBackfireState;

            _origPmax = GetF(_obs, "backfirePMaxPercent");
            _origKeep = GetF(_obs, "backfireKeepPMaxAtOrAboveX");
            _backfireSaved = true;
            SetBaselineBackfire();

            // Keeps the human still (virtual W / A / D included); the motor honours ISteeringOverride for every racer with local control.
            _park = _human.gameObject.AddComponent<MechanicsHarnessParkOverride>();
            _motor.RefreshSteeringProviders();
            Event("human", human.name + " id=" + _humanRacerId + " baseMaxSpeed=" + F(_baseMaxSpeed));
            return true;
        }

        private void CollectDummies()
        {
            _dummies.Clear();
            var identities = FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None)
                .Where(r => r != null && r.IsTrainingDummy)
                .OrderBy(r => r.name, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < identities.Length; i++)
            {
                RacerIdentity identity = identities[i];
                var dummy = new Dummy
                {
                    Index = i,
                    Name = identity.name,
                    Identity = identity,
                    Behaviour = identity.GetComponent<TrainingDummy>(),
                    Motor = identity.GetComponent<BuddahPredictedMotor>(),
                    Executor = identity.GetComponent<SkillExecutor>(),
                    Body = identity.GetComponent<Rigidbody>(),
                    Bootstrap = identity.GetComponent<BuddahPredictionBootstrap>(),
                    Net = identity.NetworkObject,
                };
                dummy.Home = identity.transform.position;
                dummy.HomeRotation = identity.transform.rotation;
                dummy.HomeProgress = ProjectProgress01(dummy.Home);
                if (dummy.Behaviour != null)
                {
                    try
                    {
                        dummy.Home = Get<Vector3>(dummy.Behaviour, "_homePosition");
                        dummy.HomeRotation = Get<Quaternion>(dummy.Behaviour, "_homeRotation");
                        dummy.HomeProgress = GetF(dummy.Behaviour, "_homeProgress01");
                    }
                    catch (Exception error)
                    {
                        _notes.Add("dummy home via reflection failed (" + error.Message + "); using the spawn position");
                    }
                }
                TrackPose(dummy.HomeProgress, out Vector3 point, out Vector3 tangent);
                dummy.Tangent = tangent;
                dummy.Lateral = Vector3.Dot(Flat(dummy.Home - point), RightOf(tangent));
                _dummies.Add(dummy);
                Event("dummy", dummy.Name + " home=" + F(dummy.Home) + " progress01=" + F(dummy.HomeProgress, "0.00000") + " lateral=" + F(dummy.Lateral));
            }
        }

        // ---------------------------------------------------------------- fixture reset

        // Every wait is followed by an assertion: a timed-out wait throws, naming what is still active, so the scenario
        // reports "fixture reset" with the real reason instead of running on a dirty fixture.
        private IEnumerator ResetFixture(string[] loadout)
        {
            EndInputScope();
            yield return Until(() => !_exec.HasPendingCast, 3f);
            if (_exec.HasPendingCast)
                throw new InvalidOperationException("a cast is still pending 3 s into the reset");
            if (_obs.IsBackfireActive || _motor.CurrentComputedStats.IsMaxSpeedPenaltyActive)
            {
                float penaltyTimeout = _obs.BackfireLockSeconds + 3f;
                yield return UntilClock(() => !_obs.IsBackfireActive && !_motor.CurrentComputedStats.IsMaxSpeedPenaltyActive, penaltyTimeout);
                if (_obs.IsBackfireActive || _motor.CurrentComputedStats.IsMaxSpeedPenaltyActive)
                    throw new InvalidOperationException("backfire penalty still active " + F(penaltyTimeout, "0.#") + " s into the reset (lock "
                        + F(_obs.BackfireSecondsLeft) + " s left, speed penalty " + _motor.CurrentComputedStats.IsMaxSpeedPenaltyActive + " "
                        + F(_motor.MaxSpeedPenaltySecondsLeft) + " s left)");
            }
            float freezeTimeout = Mathf.Max(3f, _motor.FreezeSecondsLeft + 1f);
            yield return UntilClock(() => !_motor.IsFrozen, freezeTimeout);
            if (_motor.IsFrozen)
                throw new InvalidOperationException("the human is still frozen " + F(freezeTimeout, "0.#") + " s into the reset (" + F(_motor.FreezeSecondsLeft) + " s left)");

            SetBaselineBackfire();
            if (loadout != null)
                _loadout.SetSlotsServer(loadout);
            _hands.ResetSkillInputAndEffects();
            var zone = _human.GetComponent<MovementSlowTrapZoneEffect>();
            if (zone != null)
            {
                zone.enabled = false; // OnDisable removes the trigger and debug children
                Destroy(zone);
            }
            PlaceHuman(_stagingProgress, 0f, true, "mechanics-harness reset");
            _exec.ResetActiveSkillEffectsServer();
            ClearCooldowns();
            ClearSurcharge();
            _obs.MultiplyServer(0f);
            foreach (Dummy dummy in _dummies)
            {
                if (dummy.Executor != null)
                    dummy.Executor.ResetActiveSkillEffectsServer();
            }
            yield return Wait(0.3f);
            yield return WaitDummiesHome(15f);
            yield return Wait(0.7f);
            if (HumanScale() > 1.05f)
            {
                yield return Until(() => HumanScale() <= 1.05f, 2f);
                if (HumanScale() > 1.05f)
                    throw new InvalidOperationException("body scale still x" + F(HumanScale()) + " 2 s after the reset");
            }
            Event("reset", "loadout=" + string.Join(",", _loadout.CopySlotSkillIds()));
        }

        private float HumanScale()
        {
            var scale = _human != null ? _human.GetComponent<PlayerScaleEffect>() : null;
            return scale != null ? scale.CurrentScaleMultiplier : 1f;
        }

        private string HandoffState()
            => "handoff=" + _motor.IsLaunchHandoffActive + " intro=" + _motor.IsPredictionIntroControlActive
               + " external=" + _motor.IsPredictionExternalKinematicControlActive + " pending=" + _motor.IsAuthoritativeLaunchHandoffPending;

        // Throws when a dummy is still away from home (or moving / frozen) after the forced reset.
        private IEnumerator WaitDummiesHome(float timeout)
        {
            yield return Until(() => _dummies.All(IsDummyHome), timeout);
            var late = _dummies.Where(d => !IsDummyHome(d)).ToList();
            if (late.Count == 0)
                yield break;
            foreach (Dummy dummy in late)
            {
                try { Call(dummy.Behaviour, "ResetToHome", new[] { typeof(float) }, Time.time); }
                catch (Exception error) { _errors.Add("force dummy home: " + error.Message); }
            }
            Info("fixture: dummies forced home", string.Join(", ", late.Select(DummyState)), "not home " + F(timeout, "0") + " s after the reset");
            yield return Wait(1.5f);
            var still = _dummies.Where(d => !IsDummyHome(d)).ToList();
            if (still.Count > 0)
                throw new InvalidOperationException("dummies not home after a forced reset: " + string.Join(", ", still.Select(DummyState)));
        }

        private static bool IsDummyHome(Dummy dummy)
            => dummy.Identity == null || (dummy.HomeDistance < 1f && dummy.PlanarSpeed < 0.5f && (dummy.Motor == null || !dummy.Motor.IsFrozen));

        private static string DummyState(Dummy dummy)
            => dummy.Name + " " + F(dummy.HomeDistance) + " m from home, " + F(dummy.PlanarSpeed) + " m/s"
               + (dummy.Motor != null && dummy.Motor.IsFrozen ? ", frozen" : string.Empty);

        // ---------------------------------------------------------------- S0 spawn

        private IEnumerator S0Spawn()
        {
            yield return Until(() => Time.time - _stagedAt >= 3f, 6f);
            var dummies = FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None).Where(r => r != null && r.IsTrainingDummy).ToArray();
            Check("training dummy count", dummies.Length == 3, "3", dummies.Length.ToString(CultureInfo.InvariantCulture));

            foreach (Dummy d in _dummies)
            {
                bool assigned = d.Identity.IsAssigned;
                int owner = d.Identity.OwnerId;
                bool serverDummy = RacerAuthority.IsServerDummy(d.Identity);
                bool ai = RacerAuthority.IsServerAI(d.Identity);
                bool progress = RacerAuthority.IsProgressAuthority(d.Identity);
                Check(d.Name + " identity", !assigned && owner == -1 && serverDummy && !ai && !progress,
                    "unassigned id, ownerless, server dummy, not AI, no progress authority",
                    "assigned=" + assigned + " owner=" + owner + " serverDummy=" + serverDummy + " ai=" + ai + " progressAuthority=" + progress);
            }

            var directory = RacerDirectory.Current != null ? RacerDirectory.Current.All : null;
            int directoryCount = directory != null ? directory.Count : -1;
            bool directoryHasDummy = directory != null && directory.Any(r => r != null && r.IsTrainingDummy);
            bool directoryHasHuman = directory != null && directory.Contains(_human);
            Check("RacerDirectory lists only the human", directoryCount == 1 && directoryHasHuman && !directoryHasDummy,
                "1 racer (the human), no dummy", "count=" + directoryCount + " human=" + directoryHasHuman + " dummy=" + directoryHasDummy);

            var board = LeaderboardManager.Instance;
            int rankings = board != null ? board.Rankings.Count : -1;
            string rankingIds = board != null ? string.Join(",", board.Rankings.Select(e => e.RacerId.ToString(CultureInfo.InvariantCulture))) : "no LeaderboardManager";
            Check("Leaderboard ranks only the human", rankings == 1 && board.Rankings[0].RacerId == _humanRacerId,
                "1 ranking, racer id " + _humanRacerId, "count=" + rankings + " ids=[" + rankingIds + "]");

            float length = TrackSplineRef.Instance.TrackLength;
            float expectedSpacing = ReadSpawnManagerFloat("_trainingDummySpacingMeters", 15f);
            for (int i = 0; i + 1 < _dummies.Count; i++)
            {
                float spacing = Mathf.Repeat(_dummies[i + 1].HomeProgress - _dummies[i].HomeProgress, 1f) * length;
                Check("spacing " + _dummies[i].Name + " -> " + _dummies[i + 1].Name, Mathf.Abs(spacing - expectedSpacing) <= 1f,
                    F(expectedSpacing) + " m ±1 along the track", F(spacing) + " m");
            }
            if (_dummies.Count == 3)
            {
                float l0 = _dummies[0].Lateral, l1 = _dummies[1].Lateral, l2 = _dummies[2].Lateral;
                Check("lateral left / centre / right", l0 < -1f && Mathf.Abs(l1) < 1.5f && l2 > 1f,
                    "left < -1 m, |centre| < 1.5 m, right > 1 m", F(l0) + " / " + F(l1) + " / " + F(l2) + " m");
            }
            Info("dummy start distance from staging", _dummies.Count > 0 ? F(StagingMetresBeforeFirstDummy) + " m before " + _dummies[0].Name : "no dummies");

            foreach (Dummy d in _dummies)
            {
                Check(d.Name + " parked at home", d.PlanarSpeed < 0.3f && d.HomeDistance < 1f,
                    "planar speed < 0.3 m/s, < 1 m from home", "speed=" + F(d.PlanarSpeed) + " m/s, home distance=" + F(d.HomeDistance) + " m");
            }

            var intro = FindFirstObjectByType<IntroSequenceManager>();
            int prepared = intro != null ? Get<int>(intro, "_lastPreparedPlayerCount") : -99;
            bool dummyAssigned = _dummies.Any(d => d.Identity.TryGetComponent(out RaceBodyIntroStateController body) && body.HasAssignment);
            Check("intro sized for 1 racer (1P), no dummy slot", prepared == 1 && !dummyAssigned, "1 prepared racer, dummies unassigned",
                "prepared=" + prepared + " dummyAssigned=" + dummyAssigned);

            string[] slots = _loadout.CopySlotSkillIds();
            Check("human loadout as submitted", slots.SequenceEqual(_submittedLoadout), string.Join(",", _submittedLoadout), string.Join(",", slots));

            Info("human base max speed", F(_baseMaxSpeed) + " m/s");
        }

        // ---------------------------------------------------------------- S1 skill keys

        private IEnumerator S1SkillKeys()
        {
            BeginInputScope();
            EnsureKeyboard();
            yield return Wait(0.3f);

            yield return TapSlotKey(Key.Q, 0, "Q");
            yield return TapSlotKey(Key.Digit2, 1, "2");
            yield return TapSlotKey(Key.R, 2, "R");

            // The former slot-0 combo (W ↑ W, 0.08 s apart). ComboSkillInput is deleted (A7) and the W / ↑ push bindings
            // are deleted (2026-10-30, W is the drive key): the taps must neither cast nor push. The park override keeps
            // W from moving the human here; S13 checks the drive.
            ClearCooldowns();
            float leftBefore = GetF(_hands, "_nextPushServerTimeLeft");
            float rightBefore = GetF(_hands, "_nextPushServerTimeRight");
            int from = _casts.Count;
            foreach (Key key in new[] { Key.W, Key.UpArrow, Key.W })
            {
                yield return Tap(key);
                yield return Wait(0.08f);
            }
            yield return Wait(1.5f);
            Check("W ↑ W (the former slot-0 combo) does not cast", !_casts.Skip(from).Any(c => c.Stage == CastStage.Requested),
                "no cast request", CastsSince(from, "none"));
            float leftAfter = GetF(_hands, "_nextPushServerTimeLeft");
            float rightAfter = GetF(_hands, "_nextPushServerTimeRight");
            Check("W and ↑ push neither hand (push bindings deleted)", Mathf.Approximately(leftAfter, leftBefore) && Mathf.Approximately(rightAfter, rightBefore),
                "both server push cooldowns untouched", "left " + F(leftBefore) + " -> " + F(leftAfter) + ", right " + F(rightBefore) + " -> " + F(rightAfter));
            EndInputScope();
        }

        private IEnumerator TapSlotKey(Key key, int slot, string label)
        {
            ClearCooldowns();
            int from = _casts.Count;
            yield return Tap(key);
            yield return Until(() => HasCast(from, CastStage.Accepted, slot) || HasCast(from, CastStage.Rejected, slot), 2f);
            Check(label + " requests slot " + slot, HasCast(from, CastStage.Requested, slot), "Requested slot " + slot, CastsSince(from, "no event"));
            Check(label + " accepted for slot " + slot, HasCast(from, CastStage.Accepted, slot), "Accepted slot " + slot, CastsSince(from, "no event"));
            yield return Until(() => HasCast(from, CastStage.Executed, slot), 2.5f);
            Info(label + " execution", HasCast(from, CastStage.Executed, slot) ? "Executed " + _loadout.GetSkillId(slot) : "not executed");
        }

        // ---------------------------------------------------------------- S13 drive (W)

        // W held = throttle 1, released = 0 and the racer coasts (no brake, user decision 2026-10-30). The park override
        // is lifted for this scenario only; the staging spot is 40 m before the first dummy, and 0.6 s of push plus 1.4 s
        // of coasting stays well short of it.
        private IEnumerator S13Drive()
        {
            var body = _human.GetComponent<Rigidbody>();
            var bootstrap = _human.GetComponent<BuddahPredictionBootstrap>();
            if (body == null || bootstrap == null)
            {
                Fail("human rigidbody and prediction bootstrap", "present", body == null ? "no rigidbody" : "no bootstrap");
                yield break;
            }
            BeginInputScope();
            EnsureKeyboard();
            _park.enabled = false;
            try
            {
                yield return Wait(0.6f);
                float idleSpeed = Flat(body.velocity).magnitude;
                Check("no W: no push", bootstrap.DebugState.throttleInput == 0f && idleSpeed < 0.5f,
                    "throttle 0, planar speed < 0.5 m/s", "throttle " + F(bootstrap.DebugState.throttleInput) + ", " + F(idleSpeed) + " m/s");

                _keyboard.MakeCurrent();
                yield return QueueInput(() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W)));
                float pushStart = Time.time;
                bool sawFull = false;
                while (Time.time - pushStart < 0.6f)
                {
                    sawFull |= bootstrap.DebugState.throttleInput == 1f;
                    yield return null;
                }
                float pushSeconds = Time.time - pushStart;
                float pushedSpeed = Flat(body.velocity).magnitude;
                float force = _motor.CurrentComputedStats.FinalForwardForce;
                float ideal = force / Mathf.Max(0.001f, body.mass) * pushSeconds;
                Check("W held: throttle 1", sawFull && bootstrap.DebugState.throttleInput == 1f, "throttle 1", "throttle " + F(bootstrap.DebugState.throttleInput));
                Check("W held: the human speeds up", pushedSpeed >= 0.4f * ideal, ">= 40% of force / mass x time = " + F(0.4f * ideal) + " m/s",
                    F(pushedSpeed) + " m/s after " + F(pushSeconds) + " s (force " + F(force) + ", mass " + F(body.mass) + ")");

                yield return QueueInput(() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState()));
                yield return Wait(0.2f);
                float coastStart = Time.time;
                float coastStartSpeed = Flat(body.velocity).magnitude;
                Check("W released: throttle 0", bootstrap.DebugState.throttleInput == 0f, "throttle 0", "throttle " + F(bootstrap.DebugState.throttleInput));
                float peak = coastStartSpeed;
                while (Time.time - coastStart < 1.2f)
                {
                    peak = Mathf.Max(peak, Flat(body.velocity).magnitude);
                    yield return null;
                }
                float coastSeconds = Time.time - coastStart;
                float coastEndSpeed = Flat(body.velocity).magnitude;
                Check("W released: no more push (the speed does not rise)", peak <= coastStartSpeed + 0.3f && coastEndSpeed <= coastStartSpeed + 0.05f,
                    "peak <= start + 0.3 m/s", "start " + F(coastStartSpeed) + ", peak " + F(peak) + ", end " + F(coastEndSpeed) + " m/s");
                Info("coast deceleration", F((coastStartSpeed - coastEndSpeed) / Mathf.Max(0.001f, coastSeconds), "0.00") + " m/s2 over " + F(coastSeconds, "0.00") + " s",
                    "no brake; ground friction only (estimate before the change: about 2 m/s2)");
            }
            finally
            {
                if (_park != null)
                    _park.enabled = true;
                EndInputScope();
            }
        }

        // ---------------------------------------------------------------- S2 mouse aim

        private IEnumerator S2MouseAim()
        {
            BeginInputScope();
            EnsureMouse();
            yield return Wait(0.6f);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Fail("main camera", "Camera.main present", "null");
                yield break;
            }
            if (!PickAimTarget(camera, 1f, out Vector3 rightWorld, out Vector2 rightScreen, out float rightYaw)
                || !PickAimTarget(camera, -1f, out Vector3 leftWorld, out Vector2 leftScreen, out float leftYaw))
            {
                Fail("aim targets on screen", "two ground points left/right of the human inside the view", "none found");
                yield break;
            }
            Info("aim targets", "right " + F(rightYaw) + "° at " + F(rightScreen) + ", left " + F(leftYaw) + "° at " + F(leftScreen));
            float cap = _hands.HandRotationSpeed;

            var toRight = new AimRun { TargetYaw = rightYaw };
            yield return DriveCursor(rightScreen, 1.6f, toRight);
            float yaw = OwnerYaw();
            Check("hand yaw reaches the right cursor target", Mathf.Abs(Mathf.DeltaAngle(yaw, rightYaw)) <= 3f, F(rightYaw) + "° ±3", F(yaw) + "°");
            bool hasPoint = _hands.TryGetAimGroundPoint(out Vector3 groundPoint);
            Check("TryGetAimGroundPoint = cursor ground point", hasPoint && Flat(groundPoint - rightWorld).magnitude <= 0.5f,
                F(rightWorld) + " ±0.5 m", hasPoint ? F(groundPoint) : "false");
            Check("server hand yaw follows the owner", Mathf.Abs(Mathf.DeltaAngle(_hands.ServerHandYaw, yaw)) <= 1f, F(yaw) + "° ±1", F(_hands.ServerHandYaw) + "°");

            var toLeft = new AimRun { TargetYaw = leftYaw };
            yield return DriveCursor(leftScreen, 1.6f, toLeft);
            yaw = OwnerYaw();
            Check("hand yaw reaches the left cursor target", Mathf.Abs(Mathf.DeltaAngle(yaw, leftYaw)) <= 3f, F(leftYaw) + "° ±3", F(yaw) + "°");
            float maxRate = Mathf.Max(toRight.MaxRate, toLeft.MaxRate);
            Check("hand turn rate capped at rotationSpeedDegPerSec", maxRate <= cap * 1.01f && maxRate > 0f, "<= " + F(cap) + " °/s",
                "max " + F(maxRate) + " °/s over " + (toRight.Frames + toLeft.Frames) + " frames");
            Info("swing right -> left", toLeft.SettleSeconds > 0f ? F(toLeft.Gap) + "° to 90% in " + F(toLeft.SettleSeconds) + " s = " + F(0.9f * toLeft.Gap / toLeft.SettleSeconds) + " °/s average" : "did not settle",
                "smoothing " + F(GetF(_hands, "mouseAimSmoothingSeconds")) + " s, then the rate cap");
            Info("Mouse.current is the virtual mouse", (Mouse.current == _mouse).ToString());

            float leftBefore = GetF(_hands, "_nextPushServerTimeLeft");
            float rightBefore = GetF(_hands, "_nextPushServerTimeRight");
            yield return Click(MouseButton.Left, leftScreen);
            yield return Until(() => GetF(_hands, "_nextPushServerTimeLeft") > leftBefore, 1f);
            float leftAfter = GetF(_hands, "_nextPushServerTimeLeft");
            float rightAfterLeft = GetF(_hands, "_nextPushServerTimeRight");
            Check("mouse left button pushes the left hand", leftAfter > leftBefore && Mathf.Approximately(rightAfterLeft, rightBefore),
                "left server push cooldown armed, right untouched", "left " + F(leftBefore) + " -> " + F(leftAfter) + ", right " + F(rightBefore) + " -> " + F(rightAfterLeft));

            yield return Wait(0.2f);
            leftBefore = GetF(_hands, "_nextPushServerTimeLeft");
            rightBefore = GetF(_hands, "_nextPushServerTimeRight");
            yield return Click(MouseButton.Right, leftScreen);
            yield return Until(() => GetF(_hands, "_nextPushServerTimeRight") > rightBefore, 1f);
            float rightAfter = GetF(_hands, "_nextPushServerTimeRight");
            float leftAfterRight = GetF(_hands, "_nextPushServerTimeLeft");
            Check("mouse right button pushes the right hand", rightAfter > rightBefore && Mathf.Approximately(leftAfterRight, leftBefore),
                "right server push cooldown armed, left untouched", "right " + F(rightBefore) + " -> " + F(rightAfter) + ", left " + F(leftBefore) + " -> " + F(leftAfterRight));
            EndInputScope();
        }

        private bool PickAimTarget(Camera camera, float sign, out Vector3 world, out Vector2 screen, out float yawDeg)
        {
            Vector3 forward = Flat(_human.transform.forward);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 origin = _human.transform.position;
            foreach (float angle in new[] { 55f, 40f, 25f })
            {
                foreach (float distance in new[] { 14f, 10f, 20f })
                {
                    Vector3 direction = Quaternion.AngleAxis(sign * angle, Vector3.up) * forward;
                    Vector3 candidate = origin + direction * distance; // on the aim plane y = body y
                    Vector3 point = camera.WorldToScreenPoint(candidate);
                    if (point.z <= camera.nearClipPlane || point.x < 20f || point.x > camera.pixelWidth - 20f
                        || point.y < 20f || point.y > camera.pixelHeight - 20f)
                        continue;
                    world = candidate;
                    screen = new Vector2(point.x, point.y);
                    yawDeg = Vector3.SignedAngle(forward, direction, Vector3.up);
                    return true;
                }
            }
            world = Vector3.zero;
            screen = Vector2.zero;
            yawDeg = 0f;
            return false;
        }

        private IEnumerator DriveCursor(Vector2 screen, float seconds, AimRun run)
        {
            float start = Time.time;
            float previous = OwnerYaw();
            run.Gap = Mathf.Abs(Mathf.DeltaAngle(previous, run.TargetYaw));
            while (Time.time - start < seconds)
            {
                _mouse.MakeCurrent();
                _inputQueue.Add(() => InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }));
                yield return null;
                float yaw = OwnerYaw();
                if (Time.deltaTime > 0f)
                {
                    run.MaxRate = Mathf.Max(run.MaxRate, Mathf.Abs(yaw - previous) / Time.deltaTime);
                    run.Frames++;
                }
                previous = yaw;
                if (run.SettleSeconds <= 0f && run.Gap > 1f && Mathf.Abs(Mathf.DeltaAngle(yaw, run.TargetYaw)) <= 0.1f * run.Gap)
                    run.SettleSeconds = Time.time - start;
            }
        }

        private IEnumerator Click(MouseButton button, Vector2 screen)
        {
            _mouse.MakeCurrent();
            yield return QueueInput(() => InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(button, true)));
            yield return Wait(0.06f);
            yield return QueueInput(() => InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }));
        }

        private float OwnerYaw() => GetF(_hands, "handYawOffsetDeg");

        // ---------------------------------------------------------------- S3 cast serialization

        private IEnumerator S3Serialization()
        {
            string a = _loadout.GetSkillId(0), b = _loadout.GetSkillId(1);
            int from = _casts.Count;
            bool first = _exec.CastSlotServer(0);
            bool pending = _exec.HasPendingCast;
            bool second = _exec.CastSlotServer(1);
            Check("first request accepted", first && HasCast(from, CastStage.Accepted, 0), "true + Accepted slot 0", first + "; " + CastsSince(from, "none"));
            Check("second request rejected while one is pending", pending && !second && HasCast(from, CastStage.Rejected, 1) && !HasCast(from, CastStage.Accepted, 1),
                "false + Rejected slot 1 (CastStage.Rejected)", "pending=" + pending + " returned=" + second + "; " + CastsSince(from, "none"));

            yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
            bool executed0 = HasCast(from, CastStage.Executed, 0);
            int from2 = _casts.Count;
            bool third = _exec.CastSlotServer(1);
            Check("slot 1 accepted after slot 0 executed", executed0 && third && HasCast(from2, CastStage.Accepted, 1),
                "slot 0 Executed, then slot 1 Accepted", "executed0=" + executed0 + " returned=" + third + "; " + CastsSince(from2, "none"));
            yield return Until(() => HasCast(from2, CastStage.Executed, 1), 2.5f);

            bool activeA = SkillEffectRunning(a), activeB = SkillEffectRunning(b);
            Check("both skills in effect at once", activeA && activeB, a + " and " + b + " effects running", a + "=" + activeA + " " + b + "=" + activeB);
            int exec0 = _casts.Skip(from).Count(c => c.Stage == CastStage.Executed && c.Slot == 0);
            int exec1 = _casts.Skip(from).Count(c => c.Stage == CastStage.Executed && c.Slot == 1);
            Check("no double execution", exec0 == 1 && exec1 == 1, "one Executed per slot", "slot0=" + exec0 + " slot1=" + exec1);
            var scale = _human.GetComponent<PlayerScaleEffect>();
            Info("physical effects", "slow zone=" + (_human.GetComponent<MovementSlowTrapZoneEffect>() != null) + " scale=" + (scale != null ? F(scale.CurrentScaleMultiplier) : "n/a"));
        }

        // ---------------------------------------------------------------- S4 surcharge

        private IEnumerator S4Surcharge()
        {
            string skill = _loadout.GetSkillId(0);
            float baseGain = ResolveBaseGain(skill);
            float multiplier = _obs.RepeatGainMultiplier;
            float window = _obs.RepeatWindowSeconds;
            float[] expected = { 1f, multiplier, multiplier * multiplier, 1f };
            string[] labels = { "first cast (no stack)", "recast within the window", "second recast within the window", "recast after the window" };
            Info("surcharge parameters", "base gain " + F(baseGain) + ", x" + F(multiplier) + " per stack, window " + F(window) + " s");

            for (int i = 0; i < expected.Length; i++)
            {
                float lateBy = 0f;
                if (i > 0)
                {
                    yield return Until(() => _exec.IsSlotReadyServer(0), 15f);
                    if (!_exec.IsSlotReadyServer(0))
                    {
                        Fail("cast " + (i + 1) + " slot ready", "slot 0 ready after its cooldown", "not ready after 15 s");
                        yield break;
                    }
                    float readyAt = Time.time;
                    if (i == 3)
                        yield return Wait(window + 0.75f);
                    lateBy = Time.time - readyAt;
                }

                _obs.MultiplyServer(0f);
                int from = _casts.Count;
                int uiFrom = _uiEvents.Count;
                bool accepted = _exec.CastSlotServer(0);
                yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
                if (!accepted || !TryCast(from, CastStage.Executed, 0, out CastRec rec))
                {
                    Fail("cast " + (i + 1) + " executed", "accepted and executed", "accepted=" + accepted + "; " + CastsSince(from, "none"));
                    yield break;
                }
                float gain = rec.ObsAt - DrainAdjusted(rec);
                float ratio = baseGain > 0f ? gain / baseGain : float.NaN;
                yield return Until(() => _uiEvents.Skip(uiFrom).Any(u => u.Slot == 0), 0.5f);
                UiRec ui = _uiEvents.Skip(uiFrom).FirstOrDefault(u => u.Slot == 0);
                string stacks = _uiEvents.Skip(uiFrom).Any(u => u.Slot == 0) ? ui.Stacks.ToString(CultureInfo.InvariantCulture) : "n/a";
                Check("cast " + (i + 1) + ": " + labels[i], Mathf.Abs(ratio - expected[i]) <= 0.01f,
                    "gain x" + F(expected[i], "0.####") + " of " + F(baseGain),
                    "x" + F(ratio, "0.####") + " (Δ " + F(gain) + ", HUD stacks " + stacks + ", requested " + F(lateBy) + " s after ready)");
            }
        }

        // ---------------------------------------------------------------- S5 backfire

        private IEnumerator S5Backfire()
        {
            string skill = _loadout.GetSkillId(0);
            float cooldown = ResolveCooldown(skill);
            float keepRatio = GetF(_obs, "backfireEndKeepRatio");
            float lockSeconds = _obs.BackfireLockSeconds;
            float expectedCap = _baseMaxSpeed * _obs.BackfireMaxSpeedMultiplier;
            _obs.MultiplyServer(0f);
            _obs.AddServer(500f);
            yield return null;

            int from = _casts.Count, uiFrom = _uiEvents.Count, backfireFrom = _backfireEvents.Count;
            bool accepted;
            ForceBackfireNext();
            try { accepted = _exec.CastSlotServer(0); }
            finally { SetBaselineBackfire(); }
            Check("forced-backfire cast accepted", accepted && HasCast(from, CastStage.Accepted, 0), "Accepted slot 0", CastsSince(from, "none"));

            yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
            if (!TryCast(from, CastStage.Executed, 0, out CastRec exec))
            {
                Fail("backfire resolves", "Executed after the confirm window", CastsSince(from, "none"));
                yield break;
            }
            TryCast(from, CastStage.Accepted, 0, out CastRec acceptedRec);
            Check("ends as Executed with IsBacklash", exec.Backlash, "IsBacklash = true", "IsBacklash = " + exec.Backlash + " after " + F(exec.T - acceptedRec.T) + " s confirm");
            float gain = exec.ObsAt - DrainAdjusted(exec);
            Check("no obsession gain", Mathf.Abs(gain) < 1f, "Δ ≈ 0 (a normal cast adds " + F(ResolveBaseGain(skill)) + ")", "Δ " + F(gain));
            bool accel = AccelActive(_motor);
            Check("no skill effect", !accel, "no acceleration modifier", "accel=" + accel);
            // User rule 2026-10-31: the backfire also runs the skill's own _anti variant (acceleration_anti: a self slow on
            // the shared acceleration channel, negative extras).
            bool variant = VariantSlowActive(_motor);
            Check("the skill's _anti variant runs", skill != "acceleration" || variant, "acceleration_anti: self slow (negative acceleration extras)",
                "slow=" + variant + " extraMaxSpeed=" + F(_motor.CurrentSkillModifiers.AccelExtraMaxSpeed) + " extraForce=" + F(_motor.CurrentSkillModifiers.AccelExtraForwardForce));
            Check("backfire penalty active", _obs.IsBackfireActive && Mathf.Abs(_obs.BackfireSecondsLeft - lockSeconds) <= 0.25f,
                "IsBackfireActive, " + F(lockSeconds) + " s left", "active=" + _obs.IsBackfireActive + " left=" + F(_obs.BackfireSecondsLeft) + " s");
            float cooldownLeft = CooldownLeft(0);
            Check("slot cooldown spent", Mathf.Abs(cooldownLeft - cooldown) <= 0.3f, F(cooldown) + " s ±0.3 left", F(cooldownLeft) + " s");

            bool anyReady = Enumerable.Range(0, SkillLoadout.SlotCount).Any(i => _exec.IsSlotReadyServer(i));
            int from2 = _casts.Count;
            bool r0 = _exec.CastSlotServer(0), r1 = _exec.CastSlotServer(1), r2 = _exec.CastSlotServer(2);
            int rejected = _casts.Skip(from2).Count(c => c.Stage == CastStage.Rejected);
            Check("all three slots locked", !anyReady && !r0 && !r1 && !r2 && rejected == 3,
                "IsSlotReadyServer false x3, CastSlotServer Rejected x3", "anyReady=" + anyReady + " returned=" + r0 + "," + r1 + "," + r2 + " rejected=" + rejected);

            float frozenValue = _obs.Current;
            yield return Wait(0.4f);
            var stats = _motor.CurrentComputedStats;
            // The variant's slow (if still running) lowers the cap before the x0.85 penalty multiplies it.
            float variantExtra = VariantSlowActive(_motor) ? _motor.CurrentSkillModifiers.AccelExtraMaxSpeed : 0f;
            float expectedNow = (_baseMaxSpeed + variantExtra) * _obs.BackfireMaxSpeedMultiplier;
            Check("max speed penalty", stats.IsMaxSpeedPenaltyActive && Mathf.Abs(stats.FinalMaxSpeed - expectedNow) <= expectedNow * 0.01f,
                F(expectedNow) + " m/s (" + F(_obs.BackfireMaxSpeedMultiplier) + " x (" + F(_baseMaxSpeed) + " " + F(variantExtra) + " variant slow))",
                "active=" + stats.IsMaxSpeedPenaltyActive + " FinalMaxSpeed=" + F(stats.FinalMaxSpeed));
            Info("HUD while locked", SlotStatusTexts());
            yield return Wait(2.6f);
            float after = _obs.Current;
            Check("obsession frozen (no natural decay)", Mathf.Abs(after - frozenValue) <= 0.01f, F(frozenValue) + " held", F(after) + " after 3 s");

            PlaceHuman(ProjectProgress01(_human.transform.position), LateralOf(_human.transform.position), true,
                "mechanics-harness respawn mid-penalty", BuddahPredictedTeleportSourceType.DropRespawn);
            _exec.ResetActiveSkillEffectsServer();
            yield return Wait(0.7f);
            stats = _motor.CurrentComputedStats;
            anyReady = Enumerable.Range(0, SkillLoadout.SlotCount).Any(i => _exec.IsSlotReadyServer(i));
            bool persists = _obs.IsBackfireActive && stats.IsMaxSpeedPenaltyActive && Mathf.Abs(stats.FinalMaxSpeed - expectedCap) <= expectedCap * 0.01f
                            && !anyReady && Mathf.Abs(_obs.Current - frozenValue) <= 0.01f;
            Check("penalty persists through a respawn teleport + skill reset", persists, "lock, freeze and x0.85 continue",
                "active=" + _obs.IsBackfireActive + " speedPenalty=" + stats.IsMaxSpeedPenaltyActive + " FinalMaxSpeed=" + F(stats.FinalMaxSpeed)
                + " anyReady=" + anyReady + " obsession=" + F(_obs.Current));

            yield return UntilClock(() => _backfireEvents.Skip(backfireFrom).Any(e => !e.Active), lockSeconds + 3f);
            BackfireRec end = _backfireEvents.Skip(backfireFrom).FirstOrDefault(e => !e.Active);
            bool ended = _backfireEvents.Skip(backfireFrom).Any(e => !e.Active);
            // The penalty end is a server tick; frame hitches stretch Time.time but not the tick count (tick dropping).
            float penaltySeconds = ended ? TickSpan(exec.Tick, end.Tick) : float.NaN;
            Check("penalty lasts backfireLockSeconds", ended && Mathf.Abs(penaltySeconds - lockSeconds) <= 0.3f, F(lockSeconds) + " s ±0.3 in network ticks",
                ended ? F(penaltySeconds) + " s (" + (end.Tick - exec.Tick) + " ticks; Time.time " + F(end.T - exec.T) + " s)" : "no end within " + F(lockSeconds + 3f) + " s");
            float expectedEnd = frozenValue * keepRatio;
            Check("obsession x" + F(keepRatio) + " at the end", ended && Mathf.Abs(end.Obs - expectedEnd) <= Mathf.Max(0.5f, expectedEnd * 0.005f),
                F(expectedEnd) + " (" + F(frozenValue) + " x " + F(keepRatio) + ")", ended ? F(end.Obs) : "n/a");
            yield return Until(() => !_motor.CurrentComputedStats.IsMaxSpeedPenaltyActive, 1.5f);
            stats = _motor.CurrentComputedStats;
            Check("speed penalty ended", !stats.IsMaxSpeedPenaltyActive && Mathf.Abs(stats.FinalMaxSpeed - _baseMaxSpeed) <= _baseMaxSpeed * 0.01f,
                "inactive, " + F(_baseMaxSpeed) + " m/s", "active=" + stats.IsMaxSpeedPenaltyActive + " FinalMaxSpeed=" + F(stats.FinalMaxSpeed));
            bool raisedTrue = _backfireEvents.Skip(backfireFrom).Any(e => e.Active);
            Check("BackfireStateChanged raised start and end", raisedTrue && ended, "true, then false", "start=" + raisedTrue + " end=" + ended);
            Info("owner HUD backfire event", _uiEvents.Skip(uiFrom).Any(u => u.Stacks < 0) ? "SurchargeStacks -1 received" : "no -1 event");
        }

        // ---------------------------------------------------------------- S6 freeze interrupt

        private IEnumerator S6FreezeInterrupt()
        {
            const float freezeSeconds = 1.5f;
            int from = _casts.Count;
            bool accepted = _exec.CastSlotServer(0);
            yield return Wait(0.3f);
            bool pendingBefore = _exec.HasPendingCast;
            float cooldownBefore = CooldownLeft(0);
            _exec.ApplyFreezeToOwner(freezeSeconds);
            float cooldownAfter = CooldownLeft(0);
            bool cancelled = HasCast(from, CastStage.Cancelled, 0);
            Check("freeze during the confirm cancels the cast", accepted && pendingBefore && cancelled && !_exec.HasPendingCast,
                "Cancelled slot 0, nothing pending", "accepted=" + accepted + " pendingBefore=" + pendingBefore + " pendingAfter=" + _exec.HasPendingCast + "; " + CastsSince(from, "none"));
            Check("freeze refunds the slot cooldown", cooldownBefore > 1f && cooldownAfter <= 0.01f, "cooldown left > 1 s before, 0 after",
                F(cooldownBefore) + " s -> " + F(cooldownAfter) + " s");
            yield return Until(() => _motor.IsFrozen, 0.5f);
            Check("caster frozen", _motor.IsFrozen, "IsFrozen, ≈" + F(freezeSeconds) + " s", "frozen=" + _motor.IsFrozen + " left=" + F(_motor.FreezeSecondsLeft) + " s");
            yield return Wait(0.2f); // let SkillSlotUI.Update sample the frozen state
            string frozenHud = SlotStatusTexts();
            Check("HUD shows the freeze on the slots", frozenHud.Contains("冻结") || frozenHud.Contains("FROZEN"), "slots show 冻结 / FROZEN (font fallback)", frozenHud);
            yield return Wait(1.1f);
            Check("cancelled cast never executes", !HasCast(from, CastStage.Executed, 0), "no Executed", CastsSince(from, "none"));
            yield return UntilClock(() => !_motor.IsFrozen, freezeSeconds + 1f);
            yield return Wait(0.1f);
            Check("slot ready after the freeze (refunded)", _exec.IsSlotReadyServer(0), "IsSlotReadyServer(0)", _exec.IsSlotReadyServer(0) + ", cooldown left " + F(CooldownLeft(0)) + " s");

            ClearCooldowns();
            from = _casts.Count;
            ForceBackfireNext();
            try { accepted = _exec.CastSlotServer(0); }
            finally { SetBaselineBackfire(); }
            yield return Wait(0.3f);
            cooldownBefore = CooldownLeft(0);
            _exec.ApplyFreezeToOwner(freezeSeconds);
            cooldownAfter = CooldownLeft(0);
            Check("freeze keeps a rolled backfire pending", accepted && !HasCast(from, CastStage.Cancelled, 0) && _exec.HasPendingCast,
                "no Cancelled, still pending", "accepted=" + accepted + " pending=" + _exec.HasPendingCast + "; " + CastsSince(from, "none"));
            yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
            bool executed = TryCast(from, CastStage.Executed, 0, out CastRec rec);
            Check("still ends in backfire", executed && rec.Backlash && _obs.IsBackfireActive, "Executed + IsBacklash, penalty active",
                executed ? "IsBacklash=" + rec.Backlash + " penalty=" + _obs.IsBackfireActive : CastsSince(from, "none"));
            bool variantAfterFreeze = VariantSlowActive(_motor);
            Check("the _anti variant still runs after the freeze", _loadout.GetSkillId(0) != "acceleration" || variantAfterFreeze,
                "acceleration_anti self slow active", "slow=" + variantAfterFreeze + " frozen=" + _motor.IsFrozen);
            Check("backfire keeps the spent cooldown", Mathf.Abs(cooldownAfter - cooldownBefore) <= 0.05f && cooldownBefore > 3f,
                "unchanged by the freeze (> 3 s left)", F(cooldownBefore) + " s -> " + F(cooldownAfter) + " s, now " + F(CooldownLeft(0)) + " s");
        }

        // ---------------------------------------------------------------- S7 dummies

        private IEnumerator S7Dummies()
        {
            if (_dummies.Count < 3)
            {
                Fail("three dummies", "3", _dummies.Count.ToString(CultureInfo.InvariantCulture));
                yield break;
            }
            float length = TrackSplineRef.Instance.TrackLength;
            Dummy target = _dummies[1];
            PlaceHuman(target.HomeProgress - 9.5f / length, target.Lateral, true, "mechanics-harness slow fixture");
            yield return Wait(1.2f);

            int from = _casts.Count;
            _exec.CastSlotServer(0); // slowtrap
            yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
            yield return Until(() => IsSlowed(target), 2.5f);
            var mods = target.Motor.CurrentSkillModifiers;
            Check(target.Name + " slowed by the zone", IsSlowed(target), "AccelExtraMaxSpeed < 0 and running",
                "extraMaxSpeed=" + F(mods.AccelExtraMaxSpeed) + " left=" + F(TicksToSeconds(mods.AccelUntilTick)) + " s");
            // Only the overhead TMP text counts: the text rebuilt from state uses the same builder and proves nothing
            // beyond the IsSlowed row above, so it is reported in the note only.
            yield return Until(() => OverheadReadoutShows(target, "SLOW"), 1.5f);
            string readout = ReadoutText(target, out bool fromTmp, out string readoutState);
            Check(target.Name + " readout shows SLOW", fromTmp && readout.Contains("SLOW"), "overhead readout (TMP) mentions SLOW",
                fromTmp ? "TMP: " + OneLine(readout) : "overhead readout not visible (" + readoutState + ")",
                fromTmp ? null : "computed from state, not the overhead text: " + OneLine(readout));

            from = _casts.Count;
            _exec.CastSlotServer(1); // reverseturn
            yield return Until(() => HasCast(from, CastStage.Executed, 1), 2.5f);
            yield return Until(() => _dummies.All(IsInverted), 1.5f);
            Check("reverse turn reaches every dummy", _dummies.All(IsInverted), "InvertTurnUntilTick set on 3 dummies",
                string.Join(", ", _dummies.Select(d => d.Name + "=" + F(TicksToSeconds(d.Motor.CurrentSkillModifiers.InvertTurnUntilTick)) + " s")));
            readout = ReadoutText(target, out fromTmp);
            Info(target.Name + " readout after reverse turn", (fromTmp ? "TMP: " : "computed: ") + OneLine(readout));

            var zone = _human.GetComponent<MovementSlowTrapZoneEffect>();
            if (zone != null)
            {
                zone.enabled = false;
                Destroy(zone);
            }
            PlaceHuman(_stagingProgress, 0f, true, "mechanics-harness S7 clear");
            foreach (Dummy dummy in _dummies)
                dummy.Executor.ResetActiveSkillEffectsServer();
            yield return WaitDummiesHome(15f);
            Dummy pushed = _dummies[2];
            yield return Until(() => !DummyEffectsActive(pushed), 3f);
            yield return Wait(0.5f);

            float impulseMagnitude = 0.4f * GetF(_hands, "pushImpulseStrength");
            uint impulseIdBefore = pushed.Bootstrap != null ? pushed.Bootstrap.DebugState.lastImpulseEventId : 0u;
            float resetDelay = GetF(pushed.Behaviour, "resetDelaySeconds");
            bool routed = BuddahPredictionRouter.RouteImpulse(pushed.Net, pushed.Tangent * impulseMagnitude, 0f,
                BuddahPredictedImpulseSourceType.Projectile, _human.NetworkObject);
            float pushedAt = Time.time, lastDisturbed = pushedAt, resetAt = -1f, maxDisplacement = 0f;
            string impulseReadout = null;
            while (Time.time - pushedAt < 25f)
            {
                yield return null;
                float displacement = pushed.HomeDistance;
                maxDisplacement = Mathf.Max(maxDisplacement, displacement);
                if (maxDisplacement > 1f && displacement < 0.5f)
                {
                    resetAt = Time.time;
                    break;
                }
                // TrainingDummy rule: impulses and active skill effects (push grace included) disturb; its own coasting does not.
                if (DummyEffectsActive(pushed))
                    lastDisturbed = Time.time;
                if (impulseReadout == null)
                {
                    string text = ReadoutText(pushed, out bool tmp);
                    if (text.Contains("IMPULSE"))
                        impulseReadout = (tmp ? "TMP: " : "computed: ") + OneLine(text);
                }
            }
            bool consumed = pushed.Bootstrap != null && pushed.Bootstrap.DebugState.lastImpulseEventId != impulseIdBefore;
            Check("impulse moves " + pushed.Name, routed && maxDisplacement > 1f, "routed, > 1 m from home",
                "routed=" + routed + " consumed=" + consumed + " max displacement=" + F(maxDisplacement) + " m (impulse " + F(impulseMagnitude) + ")");
            float delay = resetAt - lastDisturbed;
            Check(pushed.Name + " returns home after the last disturbance", resetAt > 0f && delay >= resetDelay - 0.3f && delay <= resetDelay + 0.6f,
                F(resetDelay) + " s (-0.3 / +0.6)", resetAt > 0f ? F(delay) + " s after the last disturbance (" + F(resetAt - pushedAt) + " s after the push)" : "no reset within 25 s");
            Info(pushed.Name + " readout impulse line", impulseReadout ?? "not seen");
        }

        // ---------------------------------------------------------------- S11 HUD

        private IEnumerator S11Hud()
        {
            yield return Wait(0.6f);
            var slots = new Dictionary<int, SkillSlotUI>();
            foreach (SkillSlotUI ui in FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None))
            {
                int index = Get<int>(ui, "_slotIndex");
                if (index >= 0 && !slots.ContainsKey(index))
                    slots[index] = ui;
            }
            string[] keys = { "Q", "E", "R" };
            string[] digits = { "1", "2", "3" };
            for (int i = 0; i < SkillLoadout.SlotCount; i++)
            {
                string label = slots.TryGetValue(i, out SkillSlotUI ui) ? LabelText(ui, "_keyLabel") : null;
                Check("slot " + i + " key label", label != null && label.Contains(keys[i]), "contains " + keys[i] + " (and " + digits[i] + ")", label ?? "no SkillSlotUI / label");
            }

            if (slots.TryGetValue(0, out SkillSlotUI slot0))
            {
                int from = _casts.Count;
                _exec.CastSlotServer(0);
                yield return Until(() => HasCast(from, CastStage.Executed, 0), 2.5f);
                yield return Wait(0.5f);
                string status = LabelText(slot0, "_statusLabel");
                float cooldown = CooldownLeft(0);
                var match = Regex.Match(status ?? string.Empty, @"(\d+(?:\.\d+)?)s");
                float shown = match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : float.NaN;
                Check("cooldown text while cooling", match.Success && Mathf.Abs(shown - cooldown) <= 0.4f, F(cooldown) + " s ±0.4", status ?? "null");
            }

            var obsessionUi = FindFirstObjectByType<ObsessionUI>();
            if (obsessionUi == null)
            {
                Fail("ObsessionUI present", "ObsessionUI in the HUD", "none");
                yield break;
            }
            RestoreOriginalBackfire();
            try
            {
                _obs.MultiplyServer(0f);
                _obs.AddServer(500f);
                yield return Wait(0.5f);
                string probability = LabelText(obsessionUi, "_probabilityLabel");
                string status = LabelText(obsessionUi, "_statusLabel");
                float actual = _obs.CurrentBackfireProbabilityPercent;
                var match = Regex.Match(probability ?? string.Empty, @"(\d+(?:\.\d+)?)%");
                float shown = match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : float.NaN;
                Check("backfire probability text", match.Success && Mathf.Abs(shown - actual) <= 1.5f, F(actual, "0.0") + "% ±1.5 (table curve at obsession " + F(_obs.Current, "0") + ")",
                    probability ?? "null");
                Info("obsession status text", status ?? "null");
            }
            finally
            {
                SetBaselineBackfire();
            }
        }

        // ---------------------------------------------------------------- S12 results

        private IEnumerator S12Results()
        {
            var board = LeaderboardManager.Instance;
            int count = board != null ? board.Rankings.Count : -1;
            Check("rankings list only the human", count == 1 && board.Rankings[0].RacerId == _humanRacerId, "1 entry, racer " + _humanRacerId,
                board != null ? "count=" + count + " ids=" + string.Join(",", board.Rankings.Select(e => e.RacerId)) : "no LeaderboardManager");
            var snapshot = board != null ? board.BuildFinalResultsSnapshot() : null;
            Check("results snapshot lists only the human", snapshot != null && snapshot.Count == 1 && snapshot[0].RacerId == _humanRacerId,
                "1 entry, racer " + _humanRacerId, snapshot != null ? "count=" + snapshot.Count + " ids=" + string.Join(",", snapshot.Select(e => e.RacerId)) : "n/a");

            if (!_endRaceAtEnd)
            {
                Info("synthetic race end", "skipped (endRaceAtEnd = false)");
                yield break;
            }
            // The fixture has to be gone before the race scene unloads.
            RestoreOriginalBackfire();
            RemovePark();
            var finish = RaceFinishManager.Instance;
            if (finish == null)
            {
                Fail("RaceFinishManager present", "RaceFinishManager", "none");
                yield break;
            }
            Info("synthetic race end", "RaceFinishManager.ForceEndRaceServer (not a natural finish)");
            finish.ForceEndRaceServer();
            float deadline = Time.realtimeSinceStartup + 50f;
            while (Time.realtimeSinceStartup < deadline && (MatchResultPresentationCoordinator.Instance == null
                   || MatchResultPresentationCoordinator.Instance.CurrentStage != MatchResultPresentationStage.ResultInteractive))
                yield return null;
            var coordinator = MatchResultPresentationCoordinator.Instance;
            bool interactive = coordinator != null && coordinator.CurrentStage == MatchResultPresentationStage.ResultInteractive;
            Check("results screen reached", interactive, "ResultInteractive", coordinator != null ? coordinator.CurrentStage.ToString() : "no coordinator");
            var finals = coordinator != null ? coordinator.FinalResults : null;
            Check("results contain only the human", finals != null && finals.Count == 1 && finals[0].RacerId == _humanRacerId, "1 entry, racer " + _humanRacerId,
                finals != null ? "count=" + finals.Count + " ids=" + string.Join(",", finals.Select(e => e.RacerId)) : "n/a");
        }

        // ---------------------------------------------------------------- input scope

        private void BeginInputScope()
        {
            if (_inputScope)
                return;
            _inputScope = true;
            _savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            // Without a focused Game View, editor input would not reach the game; scoped and restored like SoloSessionFlowTests.
            _savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.onBeforeUpdate += OnBeforeInputUpdate;
        }

        private void EnsureKeyboard()
        {
            if (_keyboard == null)
                _keyboard = InputSystem.AddDevice<Keyboard>("MechanicsHarnessKeyboard");
            _keyboard.MakeCurrent();
        }

        private void EnsureMouse()
        {
            if (_mouse == null)
                _mouse = InputSystem.AddDevice<Mouse>("MechanicsHarnessMouse");
            _mouse.MakeCurrent();
        }

        private void EndInputScope()
        {
            if (!_inputScope)
                return;
            _inputScope = false;
            InputSystem.onBeforeUpdate -= OnBeforeInputUpdate;
            _inputQueue.Clear();
            try
            {
                if (_keyboard != null && _keyboard.added)
                    InputSystem.RemoveDevice(_keyboard);
                if (_mouse != null && _mouse.added)
                    InputSystem.RemoveDevice(_mouse);
            }
            catch (Exception error)
            {
                _errors.Add("remove virtual devices: " + error.Message);
            }
            _keyboard = null;
            _mouse = null;
            InputSystem.settings.backgroundBehavior = _savedBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorBehavior;
#endif
        }

        // Queued on the game's input update only, so an editor-only input pass cannot consume the simulated state.
        private void OnBeforeInputUpdate()
        {
            if (_inputQueue.Count == 0)
                return;
            InputUpdateType wanted = InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInFixedUpdate
                ? InputUpdateType.Fixed : InputUpdateType.Dynamic;
            if (InputState.currentUpdateType != wanted)
                return;
            Action[] pending = _inputQueue.ToArray();
            _inputQueue.Clear();
            foreach (Action action in pending)
            {
                try { action(); }
                catch (Exception error) { _errors.Add("virtual input: " + error.Message); }
            }
            _inputFlushes++;
        }

        private IEnumerator QueueInput(Action action)
        {
            int flushes = _inputFlushes;
            _inputQueue.Add(action);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (_inputFlushes == flushes && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (_inputFlushes == flushes)
                throw new TimeoutException("Virtual input was not flushed on the game input update.");
            yield return null;
        }

        private IEnumerator Tap(Key key)
        {
            _keyboard.MakeCurrent();
            yield return QueueInput(() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key)));
            yield return Wait(0.06f);
            yield return QueueInput(() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState()));
        }

        // ---------------------------------------------------------------- fixture helpers

        private void PlaceHuman(float progress01, float lateral, bool resetModifiers, string reason,
            BuddahPredictedTeleportSourceType source = BuddahPredictedTeleportSourceType.Manual)
        {
            progress01 = Mathf.Repeat(progress01, 1f);
            if (!TrackPose(progress01, out Vector3 point, out Vector3 tangent))
                throw new InvalidOperationException("No track pose at progress " + F(progress01, "0.0000"));
            Vector3 position = point + RightOf(tangent) * lateral + Vector3.up * 2f;
            bool queued = _motor.TryApplyServerAuthoritativeTeleport(position, Quaternion.LookRotation(tangent, Vector3.up), progress01,
                source, reason, true, true, true, resetModifiers, true, true, true);
            if (!queued)
                throw new InvalidOperationException("Teleport refused: " + reason);
            Event("teleport", reason + " progress01=" + F(progress01, "0.00000") + " lateral=" + F(lateral) + " reset=" + resetModifiers);
        }

        private void SetBaselineBackfire()
        {
            if (!_backfireSaved || _obs == null)
                return;
            Set(_obs, "backfirePMaxPercent", 0f);
            Set(_obs, "backfireKeepPMaxAtOrAboveX", _origKeep);
        }

        private void ForceBackfireNext()
        {
            Set(_obs, "backfirePMaxPercent", 100f);
            Set(_obs, "backfireKeepPMaxAtOrAboveX", 0f);
        }

        private void RestoreOriginalBackfire()
        {
            if (!_backfireSaved || _obs == null)
                return;
            Set(_obs, "backfirePMaxPercent", _origPmax);
            Set(_obs, "backfireKeepPMaxAtOrAboveX", _origKeep);
        }

        private void ClearCooldowns()
        {
            float[] ready = Get<float[]>(_exec, "_nextReadyTime");
            for (int i = 0; i < ready.Length; i++)
                ready[i] = 0f;
        }

        private void ClearSurcharge() => Get<SkillRepeatSurcharge>(_exec, "_repeatSurcharge").Clear();

        private float CooldownLeft(int slot) => Get<float[]>(_exec, "_nextReadyTime")[slot] - Time.time;

        private void RemovePark()
        {
            if (_park == null)
                return;
            _park.enabled = false;
            Destroy(_park);
            _park = null;
        }

        private float ResolveBaseGain(string skillId)
        {
            SkillAction action = RequireSkill(skillId);
            return _db.GetObsessionGain(skillId, action);
        }

        private float ResolveCooldown(string skillId)
        {
            SkillAction action = RequireSkill(skillId);
            return _db.GetCooldownSeconds(skillId, action);
        }

        private SkillAction RequireSkill(string skillId)
        {
            if (_db == null)
                throw new InvalidOperationException("No SkillDatabase (" + (_dbError ?? "SkillExecutor.database is null") + ")");
            if (!_db.TryGet(skillId, out SkillAction action) || action == null)
                throw new InvalidOperationException("No SkillDatabase entry for " + skillId);
            return action;
        }

        // Throws (naming the skill or the type and field) instead of returning a fallback a check could accept.
        private float ReadSkillFloat(string skillId, string field) => GetF(RequireSkill(skillId), field);

        private float ReadSpawnManagerFloat(string field, float fallback)
        {
            var spawner = FindFirstObjectByType<SteamMultiplayer.Network.Match.MatchSpawnManager>();
            try
            {
                if (spawner != null)
                    return GetF(spawner, field);
                _notes.Add("MatchSpawnManager not found; " + field + " assumed " + F(fallback));
            }
            catch (Exception error)
            {
                _notes.Add("MatchSpawnManager." + field + " unreadable (" + error.Message + "); assumed " + F(fallback));
            }
            return fallback;
        }

        private uint NowTick() => _exec != null && _exec.TimeManager != null ? _exec.TimeManager.LocalTick : 0u;

        private double TickDelta => _exec != null && _exec.TimeManager != null && _exec.TimeManager.TickDelta > 0d ? _exec.TimeManager.TickDelta : 1d / 60d;

        // Seconds between two ticks (negative when to < from). Tick-defined windows (backfire penalty, burst, freeze)
        // are measured this way: a frame hitch advances Time.time by the frame time but adds at most
        // _maximumFrameTicks ticks (tick dropping), so Time.time differences overstate them.
        private float TickSpan(uint from, uint to) => (float)(((long)to - (long)from) * TickDelta);

        private uint SecondsToTicks(float seconds) => (uint)Mathf.CeilToInt((float)(Mathf.Max(0f, seconds) / TickDelta));

        private float TicksToSeconds(uint untilTick)
        {
            uint now = NowTick();
            return untilTick > now && _exec.TimeManager != null ? (float)((untilTick - now) * _exec.TimeManager.TickDelta) : 0f;
        }

        // acceleration_anti (backfire variant): the shared acceleration channel holds negative extras.
        private bool VariantSlowActive(BuddahPredictedMotor motor)
        {
            var mods = motor.CurrentSkillModifiers;
            return mods.AccelUntilTick > NowTick() && mods.AccelExtraMaxSpeed < 0f;
        }

        private bool AccelActive(BuddahPredictedMotor motor)
        {
            var mods = motor.CurrentSkillModifiers;
            return mods.AccelUntilTick > NowTick() && mods.AccelExtraMaxSpeed > 0f;
        }

        // S3: the running effect of a skill, read from the effect itself (the slow-trap zone, the motor's modifiers).
        private bool SkillEffectRunning(string skillId)
        {
            var mods = _motor.CurrentSkillModifiers;
            switch (skillId)
            {
                case "slowtrap": return _human.GetComponent<MovementSlowTrapZoneEffect>() != null;
                case "giant": return mods.ScaleUntilTick > NowTick() && mods.ScaleMultiplier > 1f;
                case "acceleration": return AccelActive(_motor);
                default: return false;
            }
        }

        private bool IsSlowed(Dummy dummy)
        {
            var mods = dummy.Motor.CurrentSkillModifiers;
            return mods.AccelUntilTick > NowTick() && mods.AccelExtraMaxSpeed < 0f;
        }

        private bool IsInverted(Dummy dummy) => dummy.Motor.CurrentSkillModifiers.InvertTurnUntilTick > NowTick();

        private static bool DummyEffectsActive(Dummy dummy)
            => dummy.Behaviour != null && dummy.Behaviour.TryCaptureEffects(out TrainingDummyEffects effects) && effects.AnyActive;

        private static string ReadoutText(Dummy dummy, out bool fromTmp) => ReadoutText(dummy, out fromTmp, out _);

        private static bool OverheadReadoutShows(Dummy dummy, string token)
        {
            string text = ReadoutText(dummy, out bool fromTmp);
            return fromTmp && text.Contains(token);
        }

        // The overhead TMP text when it is shown (it refreshes only while visible), else the same text built from the
        // state (diagnostic only). state says why the TMP text was not used. A renamed _text field throws.
        private static string ReadoutText(Dummy dummy, out bool fromTmp, out string state)
        {
            fromTmp = false;
            var readout = dummy.Identity.GetComponent<TrainingDummyReadout>();
            TMP_Text text = readout != null ? GetOrNull<TextMeshPro>(readout, "_text") : null;
            if (text != null && text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text))
            {
                fromTmp = true;
                state = "visible";
                return text.text;
            }
            state = readout == null ? "no TrainingDummyReadout"
                : !readout.isActiveAndEnabled ? "TrainingDummyReadout disabled"
                : text == null ? "readout text never created (not client-initialized, no camera, or behind the camera)"
                : !text.gameObject.activeInHierarchy ? "readout text hidden"
                : "readout text empty";
            if (dummy.Behaviour == null)
                return string.Empty;
            dummy.Behaviour.TryCaptureEffects(out TrainingDummyEffects effects);
            return TrainingDummyReadoutText.Build(dummy.Behaviour.DisplayName, effects, dummy.Behaviour.VisibleImpulseMagnitude, dummy.Behaviour.SecondsUntilReset);
        }

        private string SlotStatusTexts()
        {
            var parts = new List<string>();
            foreach (SkillSlotUI ui in FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None))
            {
                try { parts.Add("slot " + Get<int>(ui, "_slotIndex") + " '" + LabelText(ui, "_statusLabel") + "'"); }
                catch (Exception error) { parts.Add(error.Message); }
            }
            return parts.Count > 0 ? string.Join(", ", parts.OrderBy(p => p, StringComparer.Ordinal)) : "no SkillSlotUI";
        }

        private static string LabelText(Component owner, string field)
        {
            TMP_Text label = GetOrNull<TMP_Text>(owner, field);
            return label != null ? label.text : null;
        }


        private float DrainAdjusted(CastRec rec)
            => rec.ObsPrev > 0f ? Mathf.Max(0f, rec.ObsPrev - _obs.DrainPerSecond * rec.Dt) : rec.ObsPrev;

        private bool SessionAlive()
            => _human != null && _exec != null && _motor != null && MatchServices.Clock != null && FishNet.InstanceFinder.IsServerStarted;

        // ---------------------------------------------------------------- track helpers

        private static bool TrackPose(float progress01, out Vector3 point, out Vector3 tangent)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            TrackSplineRef track = TrackSplineRef.Instance;
            if (track == null || !track.TryEvaluateWorldPoseAtProgress01(Mathf.Repeat(progress01, 1f), out point, out tangent))
                return false;
            tangent = Flat(tangent);
            tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            return true;
        }

        private static float ProjectProgress01(Vector3 world)
        {
            TrackSplineRef track = TrackSplineRef.Instance;
            if (track == null || track.container == null || track.TrackLength <= 1e-3f)
                return 0f;
            Vector3 local = track.container.transform.InverseTransformPoint(world);
            SplineUtility.GetNearestPoint(track.container.Spline, (Unity.Mathematics.float3)local, out _, out float t);
            return track.DistanceAtT(Mathf.Repeat(t, 1f)) / track.TrackLength;
        }

        private static float LateralOf(Vector3 world)
        {
            TrackPose(ProjectProgress01(world), out Vector3 point, out Vector3 tangent);
            return Vector3.Dot(Flat(world - point), RightOf(tangent));
        }

        private static Vector3 RightOf(Vector3 tangent) => Vector3.Cross(Vector3.up, tangent).normalized;

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        // ---------------------------------------------------------------- recorders

        private void OnCast(SkillExecutor.CastEvent cast)
        {
            _casts.Add(new CastRec
            {
                T = Time.time,
                Tick = NowTick(),
                Stage = cast.Stage,
                Slot = cast.Slot,
                Skill = cast.SkillId,
                Backlash = cast.IsBacklash,
                ObsAt = _obs != null ? _obs.Current : 0f,
                ObsPrev = _obsPrevFrame,
                Dt = Time.deltaTime,
            });
            Event("cast", cast.Stage + " slot=" + cast.Slot + " skill=" + cast.SkillId + " backlash=" + cast.IsBacklash);
        }

        private void OnSkillUi(SkillExecutor.LocalSkillUiEvent ui)
        {
            _uiEvents.Add(new UiRec { T = Time.time, Slot = ui.SlotIndex, Stacks = ui.SurchargeStacks, Cooldown = ui.CooldownSeconds });
            Event("skill-ui", "slot=" + ui.SlotIndex + " stacks=" + ui.SurchargeStacks + " cooldown=" + F(ui.CooldownSeconds));
        }

        private void OnBackfireState(bool active)
        {
            _backfireEvents.Add(new BackfireRec { T = Time.time, Tick = NowTick(), Active = active, Obs = _obs != null ? _obs.Current : 0f });
            Event("backfire-state", active + " obsession=" + (_obs != null ? F(_obs.Current) : "n/a"));
        }

        private void LateUpdate()
        {
            if (_obs != null)
                _obsPrevFrame = _obs.Current;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            if (message != null && message.StartsWith(Tag, StringComparison.Ordinal))
                return;
            string line = type + ": " + Trim(message, 300);
            if (_errors.Count < 200)
                _errors.Add(line);
            _currentErrors++;
            if (_currentErrorSamples.Count < 3)
                _currentErrorSamples.Add(Trim(message, 160));
        }

        // ---------------------------------------------------------------- waits

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        private static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float until = Time.time + timeoutSeconds;
            while (!condition() && Time.time < until)
                yield return null;
        }

        // For tick-defined state (backfire, freeze): times out only after both Time.time and the network
        // tick advanced by timeoutSeconds, so frame hitches cannot end the wait before the ticks got there.
        private IEnumerator UntilClock(Func<bool> condition, float timeoutSeconds)
        {
            float until = Time.time + timeoutSeconds;
            uint untilTick = NowTick() + SecondsToTicks(timeoutSeconds);
            while (!condition() && (Time.time < until || NowTick() < untilTick))
                yield return null;
        }

        private bool HasCast(int from, CastStage stage, int slot) => TryCast(from, stage, slot, out _);

        private bool TryCast(int from, CastStage stage, int slot, out CastRec rec)
        {
            for (int i = Mathf.Max(0, from); i < _casts.Count; i++)
            {
                if (_casts[i].Stage == stage && _casts[i].Slot == slot)
                {
                    rec = _casts[i];
                    return true;
                }
            }
            rec = default;
            return false;
        }

        private string CastsSince(int from, string none)
        {
            var events = _casts.Skip(from).Select(c => c.Stage + "(" + c.Slot + (c.Backlash ? ",backlash" : string.Empty) + ")").ToList();
            return events.Count > 0 ? string.Join(" ", events) : none;
        }


        // ---------------------------------------------------------------- report

        private bool Check(string check, bool pass, string expected, string measured, string note = null)
        {
            AddRow(check, pass ? "PASS" : "FAIL", expected, measured, note);
            return pass;
        }

        private void Info(string check, string measured, string note = null) => AddRow(check, "INFO", string.Empty, measured, note);

        private void Warn(string check, string expected, string measured) => AddRow(check, "WARN", expected, measured, null);

        private void Fail(string check, string expected, string measured) => AddRow(check, "FAIL", expected, measured, null);

        private void AddRow(string check, string result, string expected, string measured, string note)
        {
            var row = new CheckRow
            {
                Scenario = _current != null ? _current.Id : "setup",
                Check = check,
                Expected = expected ?? string.Empty,
                Measured = measured ?? string.Empty,
                Result = result,
                Note = note ?? string.Empty,
                T = Time.time,
            };
            _checks.Add(row);
            Log(row.Scenario + " | " + result + " | " + check + " | expected: " + row.Expected + " | measured: " + row.Measured + (row.Note.Length > 0 ? " | " + row.Note : string.Empty));
        }

        private void Finish(string status)
        {
            if (_finished)
                return;
            _finished = true;
            try
            {
                EndInputScope();
                RemovePark();
                RestoreOriginalBackfire();
            }
            catch (Exception error)
            {
                _errors.Add("cleanup: " + error.Message);
            }
            Unsubscribe();
            if (_measurementApplied)
            {
                _measurement.Restore();
                _measurementApplied = false;
            }
            if (_logHooked)
            {
                Application.logMessageReceived -= OnLog;
                _logHooked = false;
            }

            int ran = _scenarios.Count(s => s.Result == "PASS" || s.Result == "FAIL");
            int passed = _scenarios.Count(s => s.Result == "PASS");
            int failedChecks = _checks.Count(c => c.Result == "FAIL");
            Summary = status + ": " + passed + "/" + ran + " scenarios PASS, " + failedChecks + " failed check(s)";
            try
            {
                WriteReport(status);
                WriteMarkdown(status);
            }
            catch (Exception error)
            {
                Debug.LogWarning(Tag + " report write failed: " + error.Message);
            }
            Log("finished: " + Summary + "; " + LastReportPath);
            try { _events?.Dispose(); }
            catch (Exception error) { Debug.LogWarning(Tag + " events.jsonl close failed: " + error.Message); }
            _events = null;
            Status = status;
            IsRunning = false;
            // The statics keep the result; the host goes so a later Begin in the same Play session is accepted.
            if (!_destroying)
                Destroy(gameObject);
        }

        private void Unsubscribe()
        {
            if (_exec != null)
            {
                _exec.ServerCastChanged -= OnCast;
                _exec.LocalSkillUiTriggered -= OnSkillUi;
            }
            if (_obs != null)
                _obs.BackfireStateChanged -= OnBackfireState;
        }

        private void OnDestroy()
        {
            _destroying = true;
            if (!_finished)
                Finish("aborted");
        }

        // Per-scenario report write: an IO error (e.g. a reader holding report.json) is logged and the run continues.
        private void SafeWriteReport(string status)
        {
            try { WriteReport(status); }
            catch (Exception error)
            {
                string line = "report write: " + error.GetType().Name + ": " + error.Message;
                if (_errors.Count < 200)
                    _errors.Add(line);
                Debug.LogWarning(Tag + " " + line);
            }
        }

        // Write to <file>.tmp, then swap it in, so a reader never sees a partial file.
        private static void WriteAtomically(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        private void WriteReport(string status)
        {
            var report = new Report
            {
                Status = status,
                Passed = status == "completed" && _scenarios.Count > 0 && _scenarios.All(s => s.Result == "PASS" || s.Result == "SKIP")
                         && _checks.All(c => c.Result != "FAIL"),
                StartedUtc = _startedUtc,
                WrittenUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                RequestedLoadout = _requestedLoadout ?? Array.Empty<string>(),
                SubmittedLoadout = _submittedLoadout ?? Array.Empty<string>(),
                EndRaceAtEnd = _endRaceAtEnd,
                Summary = Summary,
                Scenarios = _scenarios.ToArray(),
                Checks = _checks.ToArray(),
                Errors = _errors.ToArray(),
                Notes = _notes.ToArray(),
            };
            WriteAtomically(Path.Combine(_directory, "report.json"), JsonUtility.ToJson(report, true));
        }

        private void WriteMarkdown(string status)
        {
            var md = new StringBuilder();
            md.AppendLine("# Mechanics Practice verification");
            md.AppendLine();
            md.AppendLine("- Status: " + status + " — " + Summary);
            md.AppendLine("- Started (UTC): " + _startedUtc + "; written: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            md.AppendLine("- Loadout submitted: " + string.Join(", ", _submittedLoadout ?? Array.Empty<string>()));
            foreach (string note in _notes)
                md.AppendLine("- " + Cell(note));
            md.AppendLine();
            md.AppendLine("## Scenarios");
            md.AppendLine();
            md.AppendLine("| Scenario | Title | Result | Pass | Fail | Seconds | Note |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (ScenarioRow s in _scenarios)
                md.AppendLine("| " + s.Id + " | " + Cell(s.Title) + " | " + s.Result + " | " + s.Pass + " | " + s.Fail + " | " + F(s.Seconds, "0.0") + " | " + Cell(s.Note) + " |");
            md.AppendLine();
            md.AppendLine("## Checks");
            md.AppendLine();
            md.AppendLine("| Scenario | Check | Expected | Measured | Result | Note |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (CheckRow c in _checks)
                md.AppendLine("| " + c.Scenario + " | " + Cell(c.Check) + " | " + Cell(c.Expected) + " | " + Cell(c.Measured) + " | " + c.Result + " | " + Cell(c.Note) + " |");
            if (_errors.Count > 0)
            {
                md.AppendLine();
                md.AppendLine("## Console errors and harness errors");
                md.AppendLine();
                foreach (string error in _errors)
                    md.AppendLine("- " + Cell(error));
            }
            WriteAtomically(Path.Combine(_directory, "report.md"), md.ToString());
        }

        private void Event(string kind, string detail)
        {
            if (_events == null)
                return;
            // Also called from game events (ServerCastChanged etc.): an IO error must not reach the game's invoker.
            try
            {
                _events.WriteLine(JsonUtility.ToJson(new EventRow
                {
                    T = Time.time,
                    Scenario = _current != null ? _current.Id : "setup",
                    Kind = kind,
                    Detail = detail,
                    Tick = NowTick(),
                }));
            }
            catch (Exception error)
            {
                if (_errors.Count < 200)
                    _errors.Add("events.jsonl: " + error.Message);
            }
        }

        private void Log(string message) => GameLog.Verbose(Tag + " " + message);

        // ---------------------------------------------------------------- reflection and formatting

        private static FieldInfo FieldOf(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.Name, name);
        }

        private static T Get<T>(object target, string name) => (T)FieldOf(target.GetType(), name).GetValue(target);

        private static float GetF(object target, string name) => Get<float>(target, name);

        private static void Set(object target, string name, object value) => FieldOf(target.GetType(), name).SetValue(target, value);

        // Null only for a null target or a null (unassigned) value; a renamed field throws MissingFieldException
        // naming the type and field, so it fails the scenario instead of reading as "absent".
        private static T GetOrNull<T>(object target, string name) where T : class
            => target == null ? null : FieldOf(target.GetType(), name).GetValue(target) as T;

        private static object Call(object target, string method, Type[] parameterTypes, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(method, AnyInstance, null, parameterTypes, null);
            if (info == null)
                throw new MissingMethodException(target.GetType().Name, method);
            try { return info.Invoke(target, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        private static string F(float value, string format = "0.###")
            => float.IsNaN(value) ? "NaN" : float.IsInfinity(value) ? "inf" : value.ToString(format, CultureInfo.InvariantCulture);

        private static string F(Vector3 value) => "(" + F(value.x, "0.##") + ", " + F(value.y, "0.##") + ", " + F(value.z, "0.##") + ")";

        private static string F(Vector2 value) => "(" + F(value.x, "0") + ", " + F(value.y, "0") + ")";

        private static string Trim(string value, int max)
            => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value.Substring(0, max) + "…";

        private static string OneLine(string value) => (value ?? string.Empty).Replace("\r", string.Empty).Replace('\n', '/');

        private static string Cell(string value) => OneLine(value).Replace("|", "\\|");

        // ---------------------------------------------------------------- types

        private sealed class Scenario
        {
            public readonly string Id, Title;
            public readonly float Timeout;
            public readonly Func<IEnumerator> Body;
            public readonly string[] Loadout;
            public readonly bool NoReset;

            public Scenario(string id, string title, float timeout, Func<IEnumerator> body, string[] loadout, bool noReset = false)
            {
                Id = id; Title = title; Timeout = timeout; Body = body; Loadout = loadout; NoReset = noReset;
            }
        }

        private sealed class GuardResult
        {
            public bool Failed;
            public string Reason;
        }

        private sealed class Dummy
        {
            public int Index;
            public string Name;
            public RacerIdentity Identity;
            public TrainingDummy Behaviour;
            public BuddahPredictedMotor Motor;
            public SkillExecutor Executor;
            public Rigidbody Body;
            public BuddahPredictionBootstrap Bootstrap;
            public NetworkObject Net;
            public Vector3 Home;
            public Quaternion HomeRotation;
            public float HomeProgress;
            public float Lateral;
            public Vector3 Tangent;

            public float PlanarSpeed => Body != null ? Flat(Body.velocity).magnitude : 0f;
            public float HomeDistance => Identity != null ? Flat(Identity.transform.position - Home).magnitude : 0f;
        }

        private sealed class AimRun
        {
            public float TargetYaw, Gap, MaxRate, SettleSeconds;
            public int Frames;
        }

        // T (Time.time) for Time.time mechanics (cooldowns, surcharge window, confirm delay, dummy reset, impact arm
        // window); Tick for tick-defined ones (backfire penalty, freeze).
        private struct CastRec
        {
            public float T, ObsAt, ObsPrev, Dt;
            public uint Tick;
            public CastStage Stage;
            public int Slot;
            public string Skill;
            public bool Backlash;
        }

        private struct UiRec
        {
            public float T, Cooldown;
            public int Slot, Stacks;
        }

        private struct BackfireRec
        {
            public float T, Obs;
            public uint Tick;
            public bool Active;
        }

        [Serializable] private sealed class CheckRow { public string Scenario, Check, Expected, Measured, Result, Note; public float T; }
        [Serializable] private sealed class ScenarioRow { public string Id, Title, Result, Note = string.Empty; public int Pass, Fail; public float Seconds; }
        [Serializable] private struct EventRow { public float T; public uint Tick; public string Scenario, Kind, Detail; }

        [Serializable]
        private sealed class Report
        {
            public string Status;
            public bool Passed;
            public string StartedUtc, WrittenUtc, Summary;
            public string[] RequestedLoadout, SubmittedLoadout;
            public bool EndRaceAtEnd;
            public ScenarioRow[] Scenarios;
            public CheckRow[] Checks;
            public string[] Errors, Notes;
        }

        // Practice: start a Solo host with 0 AI, submit the requested loadout (falls back to the first three offered skills
        // when one is not offered), then the first skin. Stage 3 = every selection submitted.
        private sealed class PracticeFlow
        {
            public int Stage { get; private set; }
            public string[] Submitted { get; private set; } = Array.Empty<string>();
            public string Note { get; private set; }

            public bool Advance(string[] loadout)
            {
                if (Stage == 0 && SessionControl.Current != null)
                {
                    if (!SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)))
                        throw new InvalidOperationException("StartSoloHost(0 AI) failed: " + SessionControl.Current.LastError);
                    Stage = 1;
                }
                var selection = PropertiesSelectionManager.Instance;
                if (Stage == 1 && selection != null && selection.IsClientInitialized && selection.IsStageCountdownActive
                    && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey)
                {
                    var offered = selection.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey).Select(o => o.OptionId).ToList();
                    string[] ids = loadout;
                    if (ids == null || ids.Length < SkillLoadout.SlotCount || ids.Any(id => !offered.Contains(id)) || ids.Distinct().Count() != ids.Length)
                    {
                        Note = "requested loadout [" + string.Join(",", loadout ?? Array.Empty<string>()) + "] not offered (offered: "
                               + string.Join(",", offered) + "); submitted the first three offered skills";
                        ids = offered.Take(SkillLoadout.SlotCount).ToArray();
                    }
                    selection.SubmitSkillLoadoutSelection(ids);
                    Submitted = ids;
                    Stage = 2;
                }
                if (Stage == 2 && selection != null && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkinStageKey)
                {
                    var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
                    if (options.Count > 0)
                    {
                        selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId);
                        Stage = 3;
                    }
                }
                return Stage >= 3;
            }
        }
    }
}
#endif
