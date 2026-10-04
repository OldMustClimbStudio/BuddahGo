#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuddahGo.AI
{
    // A3: five product AI, no human takeover, no synthetic finish, unchanged race-end policy.
    // This observer exists only in Editor/Development builds and must be explicitly started.
    public sealed class AISkillRaceHarness : MonoBehaviour
    {
        public string Status { get; private set; } = "starting";
        public string OutputDirectory { get; private set; }
        public bool SkillsEnabled { get; private set; }
        public SoloDifficulty Difficulty { get; private set; }
        public bool ControlledEffects { get; private set; }
        public bool CaptureDetails { get; private set; }
        public bool AutoStopAtResults { get; set; } = true;
        public double DiagnosticStopSeconds { get; set; }
        private const double PerfWarmupSeconds = 30d, PerfWindowSeconds = 60d;
        private readonly List<RacerRecord> _racers = new List<RacerRecord>();
        private readonly List<double> _skillFrameMs = new List<double>();
        private readonly List<double> _frameMs = new List<double>();
        private StreamWriter _trajectory, _skills, _events, _combat;
        private AITestObstacleScope _obstacles;
        private int _stage, _errors, _sample, _oldFps, _oldVsync;
        private bool _done, _restored, _oldBackground, _go;
        private double _began, _goAt, _sampleAt, _heartbeatAt;
        private uint _lastSampleTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "--ai-skill-race-output");
            if (flag < 0 || flag + 1 >= args.Length) return;
            int difficulty = Array.IndexOf(args, "--ai-difficulty");
            int seed = Array.IndexOf(args, "--ai-skill-seed");
            if (seed >= 0 && seed + 1 < args.Length && int.TryParse(args[seed + 1], out int value)) AIDifficultyProfiles.DiagnosticMatchSeed = value;
            var tier = SoloDifficulty.Normal;
            if (difficulty >= 0 && difficulty + 1 < args.Length) Enum.TryParse(args[difficulty + 1], true, out tier);
            var harness = Begin(args[flag + 1], tier, Array.IndexOf(args, "--ai-skills-off") < 0, false, Array.IndexOf(args, "--ai-skill-quiet") < 0);
            int stop = Array.IndexOf(args, "--ai-skill-window-seconds");
            if (stop >= 0 && stop + 1 < args.Length && double.TryParse(args[stop + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double seconds)) harness.DiagnosticStopSeconds = Math.Max(91d, seconds);
        }

        public static AISkillRaceHarness Begin(string directory, SoloDifficulty difficulty = SoloDifficulty.Normal, bool skills = true, bool controlledEffects = false, bool captureDetails = true)
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != "MainMenu" || MatchServices.Clock != null)
                throw new InvalidOperationException("Start A3 from an idle MainMenu in Play mode.");
            if (FindFirstObjectByType<AISkillRaceHarness>() != null) throw new InvalidOperationException("An A3 observer already exists.");
            if (!Path.IsPathRooted(directory) || Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new ArgumentException("Use a new absolute evidence directory; previous runs are never overwritten.");
            Directory.CreateDirectory(directory);
            var host = new GameObject("A3 Skill Race Observer"); DontDestroyOnLoad(host);
            var h = host.AddComponent<AISkillRaceHarness>();
            h.OutputDirectory = directory; h.Difficulty = difficulty; h.SkillsEnabled = skills;
            h.ControlledEffects = controlledEffects;
            h.CaptureDetails = captureDetails;
            h._trajectory = new StreamWriter(Path.Combine(directory, "trajectory.jsonl"));
            h._skills = new StreamWriter(Path.Combine(directory, "skills.jsonl"));
            h._combat = new StreamWriter(Path.Combine(directory, "combat.jsonl"));
            h._events = new StreamWriter(Path.Combine(directory, "events.jsonl")) { AutoFlush = true };
            h._began = Time.realtimeSinceStartupAsDouble;
            h._oldBackground = Application.runInBackground; h._oldFps = Application.targetFrameRate; h._oldVsync = QualitySettings.vSyncCount;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Application.logMessageReceived += h.Log;
            if (captureDetails) SkillCombatEvents.Observed += h.Combat;
            File.WriteAllText(Path.Combine(directory, "skill-config.json"), JsonUtility.ToJson(AISkillCatalog.Current, true));
            h.Event("run", controlledEffects ? "CONTROLLED EFFECT FIXTURE: deterministic backlash, fixture teleports and direct authority cast calls; NOT natural race or independent AI decision evidence"
                : "five independent AI; human no injected steering or skills; shared obsession/cooldowns; natural finish or DNF; samples 10Hz; profiler includes observer cost");
            return h;
        }

        private void Update()
        {
            if (_done) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble - _began > 900d) { Complete("timeout"); return; }
                if (_stage == 0 && SessionControl.Current != null)
                {
                    if (!SessionControl.Current.StartSoloHost(new SoloMatchSettings(5, Difficulty))) throw new InvalidOperationException(SessionControl.Current.LastError);
                    _stage = 1;
                }
                var selection = PropertiesSelectionManager.Instance;
                if (_stage == 1 && selection != null && selection.IsClientInitialized && selection.IsStageCountdownActive
                    && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey)
                {
                    var ids = selection.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey).Take(3).Select(o => o.OptionId).ToArray();
                    selection.SubmitSkillLoadoutSelection(ids); _stage = 2; Event("human-loadout", string.Join(",", ids));
                }
                if (_stage == 2 && selection != null && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkinStageKey)
                {
                    var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
                    if (options.Count > 0) { selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId); _stage = 3; }
                }
                if (_stage == 3 && RacerDirectory.Current != null && RacerDirectory.Current.All.Count == 6) Attach();
                if (_stage < 4) return;
                var room = RoomStateManager.Instance;
                if (!_go && room != null && room.IsAuthoritativeGoIssued && MatchServices.Clock != null)
                { _go = true; _goAt = MatchServices.Clock.Now; Status = "racing"; Event("go", "Authority clock; human input untouched/no injected actions."); }
                if (!_go || MatchServices.Clock == null) return;
                double now = MatchServices.Clock.Now;
                if (DiagnosticStopSeconds > 0 && now - _goAt >= DiagnosticStopSeconds)
                { Complete("diagnostic-window-complete-not-natural-finish"); return; }
                if (now >= _sampleAt) { _sampleAt = now + .1d; Sample(now); }
                var presentation = MatchResultPresentationCoordinator.Instance;
                if (presentation != null && presentation.CurrentStage == MatchResultPresentationStage.ResultInteractive)
                { if (AutoStopAtResults) Complete("natural-race-results"); return; }
                if (now > _heartbeatAt)
                {
                    _heartbeatAt = now + 20;
                    Event("heartbeat", string.Join(";", _racers.Select(r => r.Id + ":lap=" + r.Lap.CurrentLap + ":s=" + r.Progress.distanceOnTrack.ToString("F1") + ":casts=" + (r.Caster != null ? r.Caster.Executed : 0))));
                    Flush();
                }
            }
            catch (Exception e) { Event("operator-error", e.ToString()); Complete("operator-error"); }
        }

        private void LateUpdate()
        {
            if (_done || !_go || MatchServices.Clock == null) return;
            double elapsed = MatchServices.Clock.Now - _goAt;
            if (elapsed < PerfWarmupSeconds || elapsed >= PerfWarmupSeconds + PerfWindowSeconds) return;
            _skillFrameMs.Add(AISkillCaster.WorkTicksThisFrame * 1000d / System.Diagnostics.Stopwatch.Frequency);
            _frameMs.Add(Time.unscaledDeltaTime * 1000d);
        }

        private void Attach()
        {
            if (TrackSplineRef.Instance == null || TrackSplineRef.Instance.TrackLength <= 0f)
                throw new InvalidOperationException("RaceMap needs an active, initialized TrackSplineRef before observing AI.");
            _obstacles ??= new AITestObstacleScope(Event);
            if (!_obstacles.TryDisable()) return;
            // FishNet changes the cap on host startup; establish the declared measurement cap after that transition.
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Event("measurement-settings", "targetFPS=60;vSync=0;resolution=" + Screen.width + "x" + Screen.height + ";decisionObserver=" + CaptureDetails);
            foreach (var identity in RacerDirectory.Current.All.OrderBy(r => r.Id.Value))
            {
                var record = new RacerRecord { Id = identity.Id.Value, Name = identity.DisplayName,
                    Identity = identity, Body = identity.GetComponent<Rigidbody>(), Motor = identity.GetComponent<BuddahPredictedMotor>(),
                    Driver = identity.GetComponent<AIRacerDriver>(), Caster = identity.GetComponent<AISkillCaster>(),
                    Lap = identity.GetComponent<LapProgress>(), Progress = identity.GetComponent<SplineProgressTracker>(),
                    Obsession = identity.GetComponent<ObsessionFigure>(), Perception = identity.GetComponent<SkillPerceptionState>() };
                if (identity.IsAI)
                {
                    record.Caster.CastingEnabled = SkillsEnabled; record.Personality = record.Caster.Personality.Id;
                    if (CaptureDetails) record.Caster.Observed += Skill;
                    File.WriteAllText(Path.Combine(OutputDirectory, "driving-" + record.Id + ".json"), JsonUtility.ToJson(record.Driver.Profile, true));
                }
                else
                {
                    if (record.Driver.enabled) throw new InvalidOperationException("Human takeover would invalidate an A3 natural race.");
                    record.Combo = identity.GetComponent<ComboSkillInput>(); record.Hands = identity.GetComponent<BuddahHandControl>();
                    record.ComboEnabled = record.Combo.enabled; record.HandsEnabled = record.Hands.enabled;
                    if (!ControlledEffects) { record.Combo.enabled = false; record.Hands.enabled = false; }
                }
                _racers.Add(record);
            }
            _stage = 4; Status = "intro";
        }

        private void Sample(double now)
        {
            uint tick = _racers[0].Motor.TimeManager.LocalTick;
            uint gap = _sample == 0 ? 0 : tick - _lastSampleTick;
            foreach (var r in _racers)
            {
                if (r.Body == null) continue;
                var stats = r.Motor.CurrentComputedStats;
                bool racing = !r.NaturalFinish && ResultAreaInteractionGate.ShouldProcessRaceProgress(r.Identity.gameObject);
                bool discontinuity = racing && r.HasSample && Vector3.Distance(r.LastPosition, r.Body.position) > r.Body.velocity.magnitude * (float)(now - r.LastClock) + 10f;
                float advance = Mathf.Repeat(r.Progress.distanceOnTrack - r.LastDistance + TrackSplineRef.Instance.TrackLength * .5f, TrackSplineRef.Instance.TrackLength) - TrackSplineRef.Instance.TrackLength * .5f;
                r.Unwrapped += advance;
                if (!r.HasSample || r.Unwrapped > r.BestDistance + 2f) { r.BestDistance = r.Unwrapped; r.LastAdvanceClock = now; }
                bool stalled = racing ? now - r.LastAdvanceClock > 5d : r.Stalled;
                if (discontinuity) r.Discontinuities++;
                if (stalled && !r.Stalled) r.Stalls++;
                r.Stalled = stalled; r.LastPosition = r.Body.position; r.LastDistance = r.Progress.distanceOnTrack;
                r.LastClock = now; r.HasSample = true;
                if (MatchServices.Timing.TryGetResult(RacerId.FromValue(r.Id), out var timing))
                {
                    if (timing.LapSeconds.Length != r.Laps.Length) Event("legal-lap", r.Id + ":" + string.Join(",", timing.LapSeconds));
                    r.Laps = timing.LapSeconds;
                    if (timing.Finished && !r.NaturalFinish) { r.NaturalFinish = true; r.FinishSeconds = timing.TotalSeconds; Event("natural-finish", r.Id + ":" + timing.TotalSeconds); }
                }
                if (!r.NaturalFinish && ResultAreaInteractionGate.ShouldProcessRaceProgress(r.Identity.gameObject))
                { r.FinalLap = r.Lap.CurrentLap; r.FinalCheckpoint = r.Lap.NextCheckpointIndex; r.FinalDistance = r.Progress.distanceOnTrack; }
                _trajectory.WriteLine(JsonUtility.ToJson(new SampleRow { Sample = _sample, RacerId = r.Id, Tick = tick, GapTicks = gap,
                    Clock = now, SinceGo = now - _goAt, Position = r.Body.position, Velocity = r.Body.velocity,
                    Progress = r.Progress.progress01, Lap = r.Lap.CurrentLap, Checkpoint = r.Lap.NextCheckpointIndex,
                    Steering = r.Driver.Steering, PerceivedSign = r.Driver.PerceivedSteeringSign, Stats = stats,
                    Obsession = r.Obsession.Current, BacklashProbability = r.Obsession.CurrentBackfireProbabilityPercent,
                    VisionUntil = r.Perception.VisionImpairedUntilTick, PlanMs = r.Driver.LastPlanMilliseconds,
                    Discontinuity = discontinuity, Stalled = stalled, NaturalFinish = r.NaturalFinish }));
            }
            _sample++; _lastSampleTick = tick;
        }

        public void Complete(string reason, bool stopSession = true)
        {
            if (_done) return;
            _done = true; Status = reason;
            Event("end", reason);
            var result = new Summary { Reason = reason, Difficulty = Difficulty.ToString(), SkillsEnabled = SkillsEnabled, ControlledEffects = ControlledEffects,
                CaptureDetails = CaptureDetails, MeasuredTargetFps = Application.targetFrameRate, MeasuredVsync = QualitySettings.vSyncCount,
                RuntimeErrors = _errors, SamplesPerRacer = _sample, AI = _racers.Where(r => r.Identity != null && r.Identity.IsAI).Select(Result).ToArray(),
                Human = _racers.Where(r => r.Identity != null && !r.Identity.IsAI).Select(Result).FirstOrDefault(),
                SkillFrameMedianMs = Percentile(_skillFrameMs, .5), SkillFrameP95Ms = Percentile(_skillFrameMs, .95),
                SkillFrameP99Ms = Percentile(_skillFrameMs, .99), SkillFrameMaxMs = _skillFrameMs.Count > 0 ? _skillFrameMs.Max() : -1,
                SkillFrameMeanMs = _skillFrameMs.Count > 0 ? _skillFrameMs.Average() : -1,
                PerfFrames = _skillFrameMs.Count, PerfWarmupSeconds = PerfWarmupSeconds, PerfWindowSeconds = PerfWindowSeconds,
                FrameMedianMs = Percentile(_frameMs, .5), FrameP95Ms = Percentile(_frameMs, .95) };
            string json = JsonUtility.ToJson(result, true);
            // JsonUtility cannot write nullable doubles. DNF has no race completion time, not -1 or the cutoff time.
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"FinishSeconds\":\\s*-1(?:\\.0+)?(?=[,\\s])", "\"FinishSeconds\": null");
            File.WriteAllText(Path.Combine(OutputDirectory, "summary.json"), json);
            using (var performance = new StreamWriter(Path.Combine(OutputDirectory, "skill-perf.csv")))
            {
                performance.WriteLine("frame,ai_skill_ms,frame_ms");
                for (int i = 0; i < _skillFrameMs.Count; i++) performance.WriteLine(i + ","
                    + _skillFrameMs[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ","
                    + _frameMs[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            Restore();
            if (stopSession) SessionControl.Current?.RequestStopSession();
        }
        private RacerResult Result(RacerRecord r) => new RacerResult { RacerId = r.Id, Name = r.Name, Personality = r.Personality,
            NaturalFinish = r.NaturalFinish, FinishSeconds = r.NaturalFinish ? r.FinishSeconds : -1, Laps = r.Laps,
            DNFReason = r.NaturalFinish ? "" : Status != "natural-race-results" ? Status : r.Stalled ? "stalled-at-cutoff" : "race-countdown-cutoff",
            FinalLap = r.FinalLap, FinalCheckpoint = r.FinalCheckpoint, FinalDistance = r.FinalDistance,
            Stalls = r.Stalls, Discontinuities = r.Discontinuities, Requests = r.Caster != null ? r.Caster.Requests : 0,
            Accepted = r.Caster != null ? r.Caster.Accepted : 0, Executed = r.Caster != null ? r.Caster.Executed : 0,
            Backlashes = r.Caster != null ? r.Caster.Backlashes : 0, Cancelled = r.Caster != null ? r.Caster.Cancelled : 0,
            ComboFailures = r.Caster != null ? r.Caster.Commitment.FailedCombos : 0, Keys = r.Caster != null ? r.Caster.Keys : 0,
            BuffShots = r.Caster != null ? r.Caster.BuffShots : 0 };
        private static double Percentile(List<double> values, double fraction)
        { if (values.Count == 0) return -1; var sorted = values.OrderBy(v => v).ToArray(); return sorted[(int)Math.Round((sorted.Length - 1) * fraction)]; }
        private void Skill(AISkillCaster.Observation observation) => _skills?.WriteLine(JsonUtility.ToJson(observation));
        private void Combat(SkillCombatEvents.Entry entry) => _combat?.WriteLine(JsonUtility.ToJson(entry));
        private void Event(string kind, string detail) => _events?.WriteLine(JsonUtility.ToJson(new EventRow { Kind = kind, Detail = detail, Clock = MatchServices.Clock?.Now ?? -1d }));
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) { _errors++; Event("runtime-error", message); }
            else if (message.Contains("respawn routed") || message.Contains("Executing fall respawn")) Event("recovery", message);
        }
        private void Flush() { _trajectory?.Flush(); _skills?.Flush(); _combat?.Flush(); }
        private void Restore()
        {
            if (_restored) return; _restored = true;
            Application.logMessageReceived -= Log; SkillCombatEvents.Observed -= Combat;
            foreach (var r in _racers)
            {
                if (r.Caster != null) r.Caster.Observed -= Skill;
                if (r.Combo != null) r.Combo.enabled = r.ComboEnabled;
                if (r.Hands != null) r.Hands.enabled = r.HandsEnabled;
            }
            _obstacles?.Dispose();
            Application.runInBackground = _oldBackground; Application.targetFrameRate = _oldFps; QualitySettings.vSyncCount = _oldVsync;
            Flush(); _trajectory?.Dispose(); _skills?.Dispose(); _combat?.Dispose(); _events?.Dispose();
            _trajectory = _skills = _combat = _events = null;
        }
        private void OnDisable() { if (!_done && OutputDirectory != null) Complete("operator-disabled"); }
        private void OnDestroy() => Restore();

        private sealed class RacerRecord
        {
            public int Id, FinalLap, FinalCheckpoint, Stalls, Discontinuities; public string Name, Personality;
            public RacerIdentity Identity; public Rigidbody Body; public BuddahPredictedMotor Motor; public AIRacerDriver Driver;
            public AISkillCaster Caster; public LapProgress Lap; public SplineProgressTracker Progress; public ObsessionFigure Obsession;
            public SkillPerceptionState Perception; public ComboSkillInput Combo; public BuddahHandControl Hands;
            public bool ComboEnabled, HandsEnabled, NaturalFinish, HasSample, Stalled;
            public Vector3 LastPosition; public float LastDistance, Unwrapped, BestDistance, FinalDistance;
            public double LastClock, LastAdvanceClock, FinishSeconds; public double[] Laps = Array.Empty<double>();
        }
        [Serializable] private sealed class RacerResult
        {
            public int RacerId, FinalLap, FinalCheckpoint, Stalls, Discontinuities, Requests, Accepted, Executed, Backlashes, Cancelled, ComboFailures, Keys, BuffShots;
            public string Name, Personality, DNFReason; public bool NaturalFinish; public double FinishSeconds; public double[] Laps; public float FinalDistance;
        }
        [Serializable] private sealed class Summary
        {
            public string Reason, Difficulty; public bool SkillsEnabled, ControlledEffects, CaptureDetails;
            public int RuntimeErrors, SamplesPerRacer, MeasuredTargetFps, MeasuredVsync;
            public RacerResult[] AI; public RacerResult Human; public double SkillFrameMedianMs, SkillFrameP95Ms, FrameMedianMs, FrameP95Ms;
            public double SkillFrameMeanMs, SkillFrameP99Ms, SkillFrameMaxMs, PerfWarmupSeconds, PerfWindowSeconds; public int PerfFrames;
        }
        [Serializable] private struct EventRow { public string Kind, Detail; public double Clock; }
        [Serializable] private struct SampleRow
        {
            public int Sample, RacerId, Lap, Checkpoint, Steering; public uint Tick, GapTicks, VisionUntil;
            public double Clock, SinceGo, PlanMs; public float Progress, PerceivedSign, Obsession, BacklashProbability;
            public Vector3 Position, Velocity; public BuddahPredictedMotorComputedStats Stats; public bool Discontinuity, Stalled, NaturalFinish;
        }
    }
}
#endif
