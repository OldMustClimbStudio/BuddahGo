#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
    // Explicit opt-in observer/operator. Steering always comes from AIRacerDriver on the prefab.
    // A1 observes one lap; A2 observes the unchanged three-lap Match through ResultInteractive.
    public sealed class AITuningHarness : MonoBehaviour
    {
        public static bool EnableSkillCasting;
        public string OutputDirectory { get; private set; }
        public string Status { get; private set; } = "created";
        public AIDifficultyProfile Profile;
#if UNITY_EDITOR
        public bool PauseAtFirstLapForDemo;
#endif
        private StreamWriter _samples, _events, _plans;
        private Transform _observedVisual;
        private int _plannedLaps = 1;
        private double[] _lapSeconds = Array.Empty<double>();
        private bool _productFinished;
        private double _finishObserved;
        private string _lastResultStage;
        private AIRacerDriver _driver;
        private BuddahPredictedMotor _motor;
        private Rigidbody _body;
        private LapProgress _lap;
        private SplineProgressTracker _progress;
        private AITestObstacleScope _obstacles;
        private ComboSkillInput _skills;
        private readonly SoloHarnessFlow _flow = new SoloHarnessFlow();
        private MeasurementSettings _measurement;
        private bool _skillWasEnabled, _go, _stalled, _wrongWay;
        [SerializeField] private bool _finished, _restored;
        private int _sampleId, _collisions, _teleports, _lastCheckpoint, _lastLap, _runtimeErrors, _aiCount;
        // Frame-time window GO+2..11 s (the 2026-10-02 performance comparison window), written to perf.csv.
        private readonly System.Collections.Generic.List<float> _perfFrameMs = new System.Collections.Generic.List<float>();
        private uint _perfFirstTick, _perfLastTick; private int _perfFrames;
        // Race mode: one profile per racer (index 0 = this harness racer, 1.. = server AI ordered by RacerId),
        // with per-racer contacts and speed so that controller variants can be ranked in one match.
        private string[] _racerProfilePaths;
        // When set, server AI keep the profiles the spawner resolved from the Solo difficulty (product path).
        public static bool ProductProfiles;
        public static SoloDifficulty Difficulty = SoloDifficulty.Normal;
        private readonly System.Collections.Generic.Dictionary<int, RacerStats> _racerStats = new System.Collections.Generic.Dictionary<int, RacerStats>();
        [Serializable] private class RacerStats { public int racerId; public string name, profile; public int collisions, samples; public double speedSum, maxSpeed; public bool finished; public double totalSeconds; public double[] laps; public Rigidbody body; }
        private uint _lastSampleTick;
        private double _createdAt, _goObserved, _lastClock, _lastProgressTime, _nextHeartbeat;
        private float _bestDistance, _lastDistance, _unwrapped;
        private Vector3 _lastPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            EnableSkillCasting = HarnessArgs.Has(args, "--ai-skills-on");
            bool a2 = HarnessArgs.Has(args, "--ai-a2-output");
            string output = HarnessArgs.Value(args, a2 ? "--ai-a2-output" : "--ai-a1-output");
            string profile = HarnessArgs.Value(args, a2 ? "--ai-a2-profile" : "--ai-a1-profile");
            string[] profiles = HarnessArgs.Value(args, "--ai-profiles")?.Split(';');
            ProductProfiles = HarnessArgs.Has(args, "--ai-product-profiles");
            string difficulty = HarnessArgs.Value(args, "--ai-difficulty");
            if (difficulty != null && Enum.TryParse(difficulty, true, out SoloDifficulty parsedDifficulty)) Difficulty = parsedDifficulty;
            if (output != null)
                Begin(output, profile ?? profiles?[0], a2 ? 3 : 1,
                    int.TryParse(HarnessArgs.Value(args, "--ai-count"), out int parsed) ? parsed : 0, profiles);
        }

        public static AITuningHarness Begin(string directory, string profileJsonPath = null, int plannedLaps = 1, int aiCount = 0, string[] racerProfilePaths = null)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("A1 requires Play mode.");
            if (FindFirstObjectByType<AITuningHarness>() != null) throw new InvalidOperationException("A1 already running.");
            if (SceneManager.GetActiveScene().name != "MainMenu" || MatchServices.Clock != null)
                throw new InvalidOperationException("Start A1 from an idle MainMenu.");
            if (!Path.IsPathRooted(directory)) throw new ArgumentException("Use an absolute private evidence directory.");
            if (plannedLaps != 1 && plannedLaps != 3) throw new ArgumentOutOfRangeException(nameof(plannedLaps));
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new InvalidOperationException("Evidence directory must be empty; never overwrite a previous run.");
            string profileJson = profileJsonPath != null ? File.ReadAllText(profileJsonPath) : null;
            Directory.CreateDirectory(directory);
            var host = new GameObject("A1 Tuning Harness"); DontDestroyOnLoad(host);
            var harness = host.AddComponent<AITuningHarness>();
            harness.OutputDirectory = directory; harness._plannedLaps = plannedLaps; harness._aiCount = Mathf.Clamp(aiCount, 0, 5);
            harness._racerProfilePaths = racerProfilePaths;
            harness._plans = new StreamWriter(Path.Combine(directory, "plans.jsonl"), false);
            harness._samples = new StreamWriter(Path.Combine(directory, "trajectory.jsonl"), false);
            harness._events = new StreamWriter(Path.Combine(directory, "events.jsonl"), false) { AutoFlush = true };
            harness._measurement = MeasurementSettings.Apply();
            harness._createdAt = Time.realtimeSinceStartupAsDouble;
            harness.Profile = ScriptableObject.CreateInstance<AIDifficultyProfile>();
            if (profileJson != null) JsonUtility.FromJsonOverwrite(profileJson, harness.Profile);
            harness.Profile.ValidateConfiguration();
            harness.Event("run", $"AI host-owner takeover; plannedLaps={plannedLaps}; productLaps unchanged; no skills; deterministic planner (no RNG); samples every >=0.1s on server ticks; collision=OnCollisionEnter; stall=less than 2m forward progress for 5s; discontinuity=distance exceeds speed*dt+10m; no custom recovery/teleport; model tolerance at 60 ticks: 0.5m position, 3deg yaw, 0.5m/s velocity");
            Application.logMessageReceived += harness.OnLog;
            harness.Status = "starting";
            return harness;
        }

        private void Update()
        {
            if (_finished) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble - _createdAt > (_plannedLaps == 3 ? 900 : 420)) { Complete(false, "run-timeout"); return; }
                // The product requires a legal loadout; it is recorded, and skill input is disabled before GO.
                bool idle = _flow.Stage == 0;
                bool selected = _flow.Advance(_aiCount, Difficulty, ids => Event("loadout-not-cast", string.Join(",", ids)));
                if (idle && _flow.Stage > 0) Status = "selection";
                if (selected && _driver == null) Attach();
                if (_driver == null) return;
                var room = RoomStateManager.Instance;
                if (!_go && room != null && room.IsAuthoritativeGoIssued && MatchServices.Clock != null)
                {
                    _go = true; _goObserved = MatchServices.Clock.Now; _lastProgressTime = _goObserved;
                    _lastDistance = _progress.distanceOnTrack; _lastPosition = _body.position;
                    Status = "driving"; Event("go-observed", "Official lap time is read from RaceTiming, not this observation.");
                    ShareProfileWithServerAi();
                }
                if (!_go || MatchServices.Clock == null) return;
                double sinceGo = MatchServices.Clock.Now - _goObserved;
                if (sinceGo >= 2d && sinceGo <= 11d)
                {
                    if (_perfFrames == 0) _perfFirstTick = _motor.TimeManager.LocalTick;
                    _perfLastTick = _motor.TimeManager.LocalTick; _perfFrames++;
                    _perfFrameMs.Add(Time.unscaledDeltaTime * 1000f);
                }
                if (!_productFinished) { Sample(); if (_racerStats.Count > 0) SampleRacers(); }
                var presentation = MatchResultPresentationCoordinator.Instance;
                string stage = presentation != null ? presentation.CurrentStage.ToString() : "missing";
                if (stage != _lastResultStage) { _lastResultStage = stage; Event("result-stage", stage); }
                if (_lap.CurrentLap != _lastLap || _lap.NextCheckpointIndex != _lastCheckpoint)
                { _lastLap = _lap.CurrentLap; _lastCheckpoint = _lap.NextCheckpointIndex; Event("checkpoint", "lap=" + _lastLap + "; next=" + _lastCheckpoint); }
                if (MatchServices.Timing != null && MatchServices.Timing.TryGetResult(RacerId.FromClient(_motor.OwnerId), out var result))
                {
                    if (result.LapSeconds.Length > _lapSeconds.Length)
                    {
                        _lapSeconds = result.LapSeconds;
                        Event("authoritative-lap", JsonUtility.ToJson(new LapEvent { completedLaps = _lapSeconds.Length,
                            lapSeconds = _lapSeconds[_lapSeconds.Length - 1], totalSeconds = result.TotalSeconds }));
                    }
                    if (_plannedLaps == 1 && _lapSeconds.Length >= 1)
                    {
#if UNITY_EDITOR
                        if (PauseAtFirstLapForDemo)
                        {
                            _finished = true; Status = "demo-one-lap-paused";
                            Event("demo-one-lap-paused", "Natural lap complete; editor paused for inspection, not product Finish.");
                            _samples.Flush();
                            UnityEditor.EditorApplication.isPaused = true;
                            return;
                        }
#endif
                        Complete(true, "authoritative-natural-lap"); return;
                    }
                    if (result.Finished && !_productFinished)
                    {
                        _productFinished = true; _finishObserved = MatchServices.Clock.Now;
                        Event("authoritative-finish", "total=" + result.TotalSeconds.ToString("R") + "; laps=" + _lapSeconds.Length);
                    }
                }
                double now = MatchServices.Clock.Now;
                if (_productFinished)
                {
                    if (presentation != null && presentation.CurrentStage == MatchResultPresentationStage.ResultInteractive
                        && ResultDecisionManager.Instance != null && ResultDecisionManager.Instance.IsDecisionActive)
                    {
                        bool stopped = _driver.TryGetOverride(out int endSteering, out bool drive) && !drive && endSteering == 0;
                        Event("finish-input", "drive=" + drive + "; steering=" + endSteering);
                        Complete(_lapSeconds.Length == 3 && stopped && _runtimeErrors == 0 && _teleports == 0,
                            stopped ? "authoritative-three-lap-results" : "finish-input-failed"); return;
                    }
                    if (now - _finishObserved > 45) { Complete(false, "results-timeout"); return; }
                }
                else if (now - _goObserved > 240 * _plannedLaps) { Complete(false, "lap-timeout"); return; }
                if (now >= _nextHeartbeat)
                {
                    _nextHeartbeat = now + 10;
                    Debug.Log($"[AI A1] elapsed={now-_goObserved:F1}s lap={_lap.CurrentLap} progress={_progress.progress01:F3} speed={_body.velocity.magnitude:F1} steer={_driver.Steering} plan={_driver.LastPlanMilliseconds:F2}ms");
                }
            }
            catch (Exception exception) { Event("operator-error", exception.ToString()); Complete(false, "operator-error"); }
        }

        private void Attach()
        {
            var racers = FindObjectsByType<BuddahPredictedMotor>(FindObjectsSortMode.None);
            _motor = racers.FirstOrDefault(item => item.IsOwner && item.IsServerInitialized);
            if (_motor == null || TrackSplineRef.Instance == null) return;
            _obstacles ??= new AITestObstacleScope(Event);
            _obstacles.Disable();
            if (_aiCount == 0 && racers.Length != 1) throw new InvalidOperationException("A1 requires exactly one racer.");
            _driver = _motor.GetComponent<AIRacerDriver>();
            if (_driver == null) throw new InvalidOperationException("Prefab has no AIRacerDriver.");
            _body = _motor.GetComponent<Rigidbody>(); _lap = _motor.GetComponent<LapProgress>();
            _progress = _motor.GetComponent<SplineProgressTracker>();
            _skills = _motor.GetComponent<ComboSkillInput>();
            if (_skills != null) { _skillWasEnabled = _skills.enabled; _skills.enabled = false; }
            _driver.Profile = Profile; _driver.enabled = true; _driver.Contact += Collision;
            _driver.ModelCompared += ModelCompared;
            _driver.PlanObserved += PlanObserved;
            var bridge = _motor.GetComponent<NewBuddah.PredictionV2.Visual.BuddahPredictionVisualRootBridge>();
            if (bridge != null) _observedVisual = (Transform)typeof(NewBuddah.PredictionV2.Visual.BuddahPredictionVisualRootBridge)
                .GetField("visualRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(bridge);
            ShareProfileWithServerAi();
            Event("configuration", JsonUtility.ToJson(Profile));
            Event("body", $"mass={_body.mass}; inertia={_body.inertiaTensor}; inertiaRotation={_body.inertiaTensorRotation}; angularDrag={_body.angularDrag}; maxAngular={_body.maxAngularVelocity}; tickDelta={_motor.TimeManager.TickDelta}; owner={_motor.OwnerId}");
            File.WriteAllText(Path.Combine(OutputDirectory, "configuration.json"), JsonUtility.ToJson(Profile, true));
        }

        // Server AI opponents (ownerless) use the same loaded profile, so a five-AI window measures the
        // selected controller for every racer. Planner state stays per driver; the profile is read-only.
        private void ShareProfileWithServerAi()
        {
            if (_aiCount == 0 || Profile == null) return;
            var drivers = FindObjectsByType<AIRacerDriver>(FindObjectsSortMode.None).Where(d => d != _driver)
                .OrderBy(d => { var id = d.GetComponent<RacerIdentity>(); return id != null ? id.Id.Value : int.MaxValue; }).ToArray();
            for (int i = 0; i < drivers.Length; i++)
            {
                var driver = drivers[i];
                var caster = driver.GetComponent<AISkillCaster>();
                if (caster != null) caster.CastingEnabled = EnableSkillCasting;
                AIDifficultyProfile target = Profile; string path = "(shared)";
                if (ProductProfiles) { Track(driver, driver.Profile != null ? driver.Profile.name : "(product)"); continue; }
                if (_racerProfilePaths != null && _racerProfilePaths.Length > 1)
                {
                    path = _racerProfilePaths[1 + i % (_racerProfilePaths.Length - 1)];
                    target = LoadProfile(path);
                }
                if (driver.Profile != target)
                {
                    driver.Profile = target;
                    if (caster != null && caster.Difficulty != null)
                        driver.ConfigureSkillPerception(caster.Difficulty, driver.GetComponent<SkillPerceptionState>());
                    Event("ai-profile-assigned", driver.name + " <- " + Path.GetFileName(path));
                }
                Track(driver, path);
            }
            if (_driver != null) Track(_driver, _racerProfilePaths != null ? _racerProfilePaths[0] : "(harness)");
        }
        private readonly System.Collections.Generic.Dictionary<string, AIDifficultyProfile> _loadedProfiles = new System.Collections.Generic.Dictionary<string, AIDifficultyProfile>();
        private AIDifficultyProfile LoadProfile(string path)
        {
            if (_loadedProfiles.TryGetValue(path, out var cached)) return cached;
            var profile = ScriptableObject.CreateInstance<AIDifficultyProfile>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), profile); profile.ValidateConfiguration();
            _loadedProfiles[path] = profile; return profile;
        }
        private void Track(AIRacerDriver driver, string profilePath)
        {
            var identity = driver.GetComponent<RacerIdentity>(); int id = identity != null ? identity.Id.Value : driver.GetInstanceID();
            if (_racerStats.ContainsKey(id)) return;
            var stats = new RacerStats { racerId = id, name = identity != null ? identity.DisplayName : driver.name, profile = Path.GetFileName(profilePath), body = driver.GetComponent<Rigidbody>() };
            _racerStats[id] = stats;
            if (driver != _driver) driver.Contact += collision => { if (collision.contacts.Any(c => Mathf.Abs(c.normal.y) < 0.7f)) stats.collisions++; };
        }
        private void SampleRacers()
        {
            foreach (var stats in _racerStats.Values)
            {
                if (stats.body == null) continue;
                double speed = stats.body.velocity.magnitude; stats.speedSum += speed; stats.samples++; stats.maxSpeed = Math.Max(stats.maxSpeed, speed);
            }
        }
        private void WriteRaceResults()
        {
            if (_racerStats.Count == 0) return;
            var directory = RacerDirectory.Current;
            foreach (var stats in _racerStats.Values)
            {
                if (stats.racerId == (_motor != null ? RacerId.FromClient(_motor.OwnerId).Value : int.MinValue)) stats.collisions = _collisions;
                if (MatchServices.Timing != null && MatchServices.Timing.TryGetResult(RacerId.FromValue(stats.racerId), out var result))
                { stats.finished = result.Finished; stats.totalSeconds = result.TotalSeconds; stats.laps = result.LapSeconds; }
                stats.body = null;
            }
            var ordered = _racerStats.Values.OrderByDescending(s => s.laps != null ? s.laps.Length : 0).ThenBy(s => s.laps != null && s.laps.Length > 0 ? s.totalSeconds : double.MaxValue).ToArray();
            File.WriteAllText(Path.Combine(OutputDirectory, "race-results.json"), JsonUtility.ToJson(new RaceResults { racers = ordered }, true));
        }
        [Serializable] private class RaceResults { public RacerStats[] racers; }
        private void Sample()
        {
            uint tick = _motor.TimeManager.Tick;
            uint interval = (uint)Mathf.Max(1, Mathf.RoundToInt(0.1f / (float)_motor.TimeManager.TickDelta));
            if (_sampleId > 0 && tick - _lastSampleTick < interval) return;
            double now = MatchServices.Clock.Now;
            if (_sampleId > 0 && now < _lastClock) Event("clock-regression", now.ToString("R"));
            float delta = Mathf.Repeat(_progress.distanceOnTrack - _lastDistance + TrackSplineRef.Instance.TrackLength * 0.5f,
                TrackSplineRef.Instance.TrackLength) - TrackSplineRef.Instance.TrackLength * 0.5f;
            _unwrapped += delta; _lastDistance = _progress.distanceOnTrack;
            if (_unwrapped > _bestDistance + 2f) { _bestDistance = _unwrapped; _lastProgressTime = now; }
            bool stalled = now - _lastProgressTime >= 5d;
            if (stalled != _stalled) { _stalled = stalled; Event(stalled ? "stalled" : "progress-recovered", "diagnostic-only; no reset"); }
            bool wrong = _progress.forwardDot < -0.1f;
            if (wrong != _wrongWay) { _wrongWay = wrong; Event(wrong ? "wrong-way" : "forward-again", "velocity vs spline tangent"); }
            bool discontinuity = _sampleId > 0 && Vector3.Distance(_body.position, _lastPosition) > _body.velocity.magnitude * Mathf.Max(0f, (float)(now - _lastClock)) + 10f;
            if (discontinuity) { _teleports++; Event("position-discontinuity", "Break trajectory; inspect respawn log."); }
            var projection = _driver.Line != null ? _driver.Line.Project(_body.position, -1) : default;
            var row = new SampleRow { sample = _sampleId++, tick = tick, clock = now, elapsed = now - _goObserved,
                position = _body.position, velocity = _body.velocity, yaw = _body.rotation.eulerAngles.y,
                yawRate = _body.angularVelocity.y, lap = _lap.CurrentLap, nextCheckpoint = _lap.NextCheckpointIndex,
                progress = _progress.progress01, steering = _driver.Steering, lateral = projection.Lateral,
                planMs = _driver.LastPlanMilliseconds, gapTicks = _sampleId > 1 ? tick - _lastSampleTick : 0,
                visualYaw = _observedVisual != null ? _observedVisual.eulerAngles.y : float.NaN,
                cameraYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : float.NaN,
                discontinuity = discontinuity, stalled = stalled };
            _samples.WriteLine(JsonUtility.ToJson(row)); _samples.Flush();
            if (_sampleId == 300) CaptureCamera("driving-camera.png");
            _lastPosition = _body.position; _lastClock = now; _lastSampleTick = tick;
            if (_driver.Line != null && !File.Exists(Path.Combine(OutputDirectory, "racing-line.json")))
                File.WriteAllText(Path.Combine(OutputDirectory, "racing-line.json"), JsonUtility.ToJson(new LineRow { points = _driver.Line.Points }));
        }

        private void CaptureCamera(string filename)
        {
            var camera = Camera.main;
            if (camera == null) { Event("camera-unavailable", filename); return; }
            var target = new RenderTexture(960, 540, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(OutputDirectory, filename), texture.EncodeToPNG());
                Event("camera-capture", filename + "; camera=" + camera.name + "; rendered from current scene state");
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Destroy(texture); target.Release(); Destroy(target);
            }
        }

        private void Collision(Collision collision)
        {
            // Ground contact is not counted as a wall impact. Keep obstacle contact identity and impulse.
            bool side = collision.contacts.Any(contact => Mathf.Abs(contact.normal.y) < 0.7f);
            if (!side) return;
            _collisions++; Event("collision", collision.gameObject.name + "; impulse=" + collision.impulse);
        }
        private void PlanObserved(uint tick, PlanObservation plan)
        {
            _plans?.WriteLine(JsonUtility.ToJson(new PlanRow { tick = tick, clock = MatchServices.Clock.Now, plan = plan }));
        }
        private void ModelCompared(AIRacerDriver.MotionComparison comparison) => Event("model-60-ticks", JsonUtility.ToJson(comparison));
        private void OnLog(string condition, string stack, LogType type)
        {
            if (_finished) return;
            if (type == LogType.Error || type == LogType.Exception) { _runtimeErrors++; Event("runtime-error", condition); }
            else if (condition.Contains("respawn routed") || condition.Contains("Executing fall respawn")) Event("existing-respawn", condition);
        }
        private void Event(string kind, string detail)
        {
            _events?.WriteLine(JsonUtility.ToJson(new EventRow { kind = kind, detail = detail,
                clock = MatchServices.Clock != null ? MatchServices.Clock.Now : -1,
                position = _body != null ? _body.position : Vector3.zero }));
        }
        public void Complete(bool success, string reason)
        {
            if (_finished) return;
            _finished = true; Status = reason;
            Event("end", reason);
            if (success) CaptureCamera("complete-camera.png");
            try { WriteRaceResults(); } catch (Exception exception) { Event("race-results-error", exception.Message); }
            File.WriteAllText(Path.Combine(OutputDirectory, "summary.json"), JsonUtility.ToJson(new Summary {
                success = success, reason = reason, plannedLaps = _plannedLaps, completedLaps = _lapSeconds.Length,
                lapSeconds = _lapSeconds.Length > 0 ? _lapSeconds[0] : -1, laps = _lapSeconds,
                totalSeconds = _lapSeconds.Sum(), averageLapSeconds = _lapSeconds.Length > 0 ? _lapSeconds.Average() : -1, samples = _sampleId, collisions = _collisions,
                discontinuities = _teleports, runtimeErrors = _runtimeErrors, productMatchFinished = _productFinished,
                aiCount = _aiCount, perfFrames = _perfFrames, perfTicks = _perfFrames > 0 ? (int)(_perfLastTick - _perfFirstTick) : 0,
                perfMedianMs = Percentile(.5f), perfP95Ms = Percentile(.95f) }, true));
            if (_perfFrameMs.Count > 0) File.WriteAllLines(Path.Combine(OutputDirectory, "perf.csv"),
                new[] { "frame_ms" }.Concat(_perfFrameMs.Select(ms => ms.ToString("R"))));
            Restore();
            Debug.Log($"[AI A1] END success={success} reason={reason} completedLaps={_lapSeconds.Length} totalSeconds={_lapSeconds.Sum():F3} output={OutputDirectory}");
            // End the observation through the ordinary session stop, without synthetic Finish or physics writes.
            SessionControl.Current?.RequestStopSession();
        }
        private float Percentile(float q)
        {
            if (_perfFrameMs.Count == 0) return -1f;
            var sorted = _perfFrameMs.OrderBy(ms => ms).ToArray();
            return sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Length - 1)), 0, sorted.Length - 1)];
        }
        private void Restore()
        {
            if (_restored) return;
            _restored = true;
            Application.logMessageReceived -= OnLog;
            if (_driver != null) { _driver.PlanObserved -= PlanObserved; _driver.Contact -= Collision; _driver.ModelCompared -= ModelCompared; _driver.enabled = false; }
            if (_skills != null) _skills.enabled = _skillWasEnabled;
            _obstacles?.Dispose();
            _measurement.Restore();
            _plans?.Dispose(); _plans = null;
            _samples?.Dispose(); _samples = null; _events?.Dispose(); _events = null;
        }
        private void OnDestroy()
        {
            if (!_finished && OutputDirectory != null) { _finished = true; Event("interrupted", "harness destroyed"); }
            Restore(); if (Profile != null) Destroy(Profile);
        }
        private void OnDisable()
        {
            if (!_finished && OutputDirectory != null) Complete(false, "operator-disabled-or-reloaded");
        }
        [Serializable] private class SampleRow { public int sample, lap, nextCheckpoint, steering; public uint tick, gapTicks; public double clock, elapsed, planMs; public Vector3 position, velocity; public float yaw, yawRate, progress, lateral, visualYaw, cameraYaw; public bool discontinuity, stalled; }
        [Serializable] private class PlanRow { public uint tick; public double clock; public PlanObservation plan; }
        [Serializable] private class EventRow { public string kind, detail; public double clock; public Vector3 position; }
        [Serializable] private class LineRow { public Vector3[] points; }
        [Serializable] private class LapEvent { public int completedLaps; public double lapSeconds, totalSeconds; }
        [Serializable] private class Summary { public bool success, productMatchFinished; public string reason; public int plannedLaps, completedLaps, samples, collisions, discontinuities, runtimeErrors, aiCount, perfFrames, perfTicks; public double lapSeconds, totalSeconds, averageLapSeconds; public float perfMedianMs, perfP95Ms; public double[] laps; }
    }
}
#endif
