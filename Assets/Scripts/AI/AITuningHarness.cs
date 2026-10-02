#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuddahGo.AI
{
    // Explicit opt-in observer/operator. Steering always comes from AIRacerDriver on the prefab.
    // A1 stops observing after one authoritative lap; it never finishes a three-lap Match synthetically.
    public sealed class AITuningHarness : MonoBehaviour
    {
        public string OutputDirectory { get; private set; }
        public string Status { get; private set; } = "created";
        public AIDifficultyProfile Profile;
        private StreamWriter _samples, _events;
        private AIRacerDriver _driver;
        private BuddahPredictedMotor _motor;
        private Rigidbody _body;
        private LapProgress _lap;
        private SplineProgressTracker _progress;
        private GameObject[] _boxes;
        private bool[] _boxStates;
        private ComboSkillInput _skills;
        private bool _skillWasEnabled, _started, _go, _stalled, _wrongWay;
        [SerializeField] private bool _finished, _restored;
        private int _stage, _sampleId, _collisions, _teleports, _lastCheckpoint, _lastLap, _runtimeErrors;
        private uint _lastSampleTick;
        private double _createdAt, _goObserved, _lastClock, _lastProgressTime, _nextHeartbeat;
        private float _bestDistance, _lastDistance, _unwrapped;
        private Vector3 _lastPosition;
        private bool _oldBackground;
        private int _oldTargetFps, _oldVsync;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--ai-a1-output");
            int profile = Array.IndexOf(args, "--ai-a1-profile");
            if (flag >= 0 && flag + 1 < args.Length)
                Begin(args[flag + 1], profile >= 0 && profile + 1 < args.Length ? args[profile + 1] : null);
        }

        public static AITuningHarness Begin(string directory, string profileJsonPath = null)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("A1 requires Play mode.");
            if (FindFirstObjectByType<AITuningHarness>() != null) throw new InvalidOperationException("A1 already running.");
            if (SceneManager.GetActiveScene().name != "MainMenu" || MatchServices.Clock != null)
                throw new InvalidOperationException("Start A1 from an idle MainMenu.");
            if (!Path.IsPathRooted(directory)) throw new ArgumentException("Use an absolute private evidence directory.");
            string profileJson = profileJsonPath != null ? File.ReadAllText(profileJsonPath) : null;
            Directory.CreateDirectory(directory);
            var host = new GameObject("A1 Tuning Harness"); DontDestroyOnLoad(host);
            var harness = host.AddComponent<AITuningHarness>();
            harness.OutputDirectory = directory;
            harness._samples = new StreamWriter(Path.Combine(directory, "trajectory.jsonl"), false);
            harness._events = new StreamWriter(Path.Combine(directory, "events.jsonl"), false) { AutoFlush = true };
            harness._oldBackground = Application.runInBackground; harness._oldTargetFps = Application.targetFrameRate;
            harness._oldVsync = QualitySettings.vSyncCount;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            harness._createdAt = Time.realtimeSinceStartupAsDouble;
            harness.Profile = ScriptableObject.CreateInstance<AIDifficultyProfile>();
            if (profileJson != null) JsonUtility.FromJsonOverwrite(profileJson, harness.Profile);
            harness.Event("run", "A1 host-owner takeover; plannedLaps=1; productLaps unchanged; no skills; deterministic planner (no RNG); samples every >=0.1s on server ticks; collision=OnCollisionEnter; stall=less than 2m forward progress for 5s; discontinuity=distance exceeds speed*dt+10m; no custom recovery/teleport; model tolerance at 60 ticks: 0.5m position, 3deg yaw, 0.5m/s velocity");
            Application.logMessageReceived += harness.OnLog;
            harness.Status = "starting";
            return harness;
        }

        private void Update()
        {
            if (_finished) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble - _createdAt > 420) { Complete(false, "run-timeout"); return; }
                var selection = PropertiesSelectionManager.Instance;
                if (!_started && SessionControl.Current != null)
                {
                    if (!SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, SoloDifficulty.Normal)))
                        throw new InvalidOperationException(SessionControl.Current.LastError);
                    _started = true; _stage = 1; Status = "selection";
                }
                if (_stage == 1 && selection != null && selection.IsClientInitialized && selection.IsStageCountdownActive
                    && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey)
                {
                    // The product requires a legal loadout; it is recorded, and skill input is disabled before GO.
                    var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey);
                    var ids = options.Take(3).Select(option => option.OptionId).ToArray();
                    Event("loadout-not-cast", string.Join(",", ids)); selection.SubmitSkillLoadoutSelection(ids); _stage = 2;
                }
                if (_stage == 2 && selection != null && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkinStageKey)
                {
                    var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
                    if (options.Count > 0) { selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId); _stage = 3; }
                }
                if (_stage >= 3 && _driver == null) Attach();
                if (_driver == null) return;
                var room = RoomStateManager.Instance;
                if (!_go && room != null && room.IsAuthoritativeGoIssued && MatchServices.Clock != null)
                {
                    _go = true; _goObserved = MatchServices.Clock.Now; _lastProgressTime = _goObserved;
                    _lastDistance = _progress.distanceOnTrack; _lastPosition = _body.position;
                    Status = "driving"; Event("go-observed", "Official lap time is read from RaceTiming, not this observation.");
                }
                if (!_go || MatchServices.Clock == null) return;
                Sample();
                if (_lap.CurrentLap != _lastLap || _lap.NextCheckpointIndex != _lastCheckpoint)
                { _lastLap = _lap.CurrentLap; _lastCheckpoint = _lap.NextCheckpointIndex; Event("checkpoint", "lap=" + _lastLap + "; next=" + _lastCheckpoint); }
                if (MatchServices.Timing != null && MatchServices.Timing.TryGetResult(RacerId.FromClient(_motor.OwnerId), out var result)
                    && result.LapSeconds.Length >= 1)
                { Complete(true, "authoritative-natural-lap", result.LapSeconds[0]); return; }
                double now = MatchServices.Clock.Now;
                if (now - _goObserved > 240) { Complete(false, "lap-timeout"); return; }
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
            if (racers.Length != 1) throw new InvalidOperationException("A1 requires exactly one racer.");
            _driver = _motor.GetComponent<AIRacerDriver>();
            if (_driver == null) throw new InvalidOperationException("Prefab has no AIRacerDriver.");
            _body = _motor.GetComponent<Rigidbody>(); _lap = _motor.GetComponent<LapProgress>();
            _progress = _motor.GetComponent<SplineProgressTracker>();
            _skills = _motor.GetComponent<ComboSkillInput>();
            if (_skills != null) { _skillWasEnabled = _skills.enabled; _skills.enabled = false; }
            string[] names = { "DebugboxCanPush", "DebugboxCanPush (1)", "DebugboxCanPush (2)", "DebugboxCanPush (3)", "DebugboxTriggerPush" };
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _boxes = names.Select(name => all.Single(t => t.name == name && t.parent != null && t.parent.name == "DebugBox").gameObject).ToArray();
            _boxStates = _boxes.Select(box => box.activeSelf).ToArray();
            for (int i = 0; i < _boxes.Length; i++) { Event("test-box-disabled", "RaceMap/DebugBox/" + _boxes[i].name + "; original=" + _boxStates[i]); _boxes[i].SetActive(false); }
            _driver.Profile = Profile; _driver.enabled = true; _driver.Contact += Collision;
            _driver.ModelCompared += ModelCompared;
            Event("configuration", JsonUtility.ToJson(Profile));
            Event("body", $"mass={_body.mass}; inertia={_body.inertiaTensor}; inertiaRotation={_body.inertiaTensorRotation}; angularDrag={_body.angularDrag}; maxAngular={_body.maxAngularVelocity}; tickDelta={_motor.TimeManager.TickDelta}; owner={_motor.OwnerId}");
            File.WriteAllText(Path.Combine(OutputDirectory, "configuration.json"), JsonUtility.ToJson(Profile, true));
        }

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
                discontinuity = discontinuity, stalled = stalled };
            _samples.WriteLine(JsonUtility.ToJson(row)); _samples.Flush();
            if (_sampleId == 300) ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, "driving.png"));
            _lastPosition = _body.position; _lastClock = now; _lastSampleTick = tick;
            if (_driver.Line != null && !File.Exists(Path.Combine(OutputDirectory, "racing-line.json")))
                File.WriteAllText(Path.Combine(OutputDirectory, "racing-line.json"), JsonUtility.ToJson(new LineRow { points = _driver.Line.Points }));
        }

        private void Collision(Collision collision)
        {
            // Ground contact is not counted as a wall impact. Keep obstacle contact identity and impulse.
            bool side = collision.contacts.Any(contact => Mathf.Abs(contact.normal.y) < 0.7f);
            if (!side) return;
            _collisions++; Event("collision", collision.gameObject.name + "; impulse=" + collision.impulse);
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
        public void Complete(bool success, string reason, double lapSeconds = -1)
        {
            if (_finished) return;
            _finished = true; Status = reason;
            Event("end", reason);
            if (success) ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, "lap-complete.png"));
            File.WriteAllText(Path.Combine(OutputDirectory, "summary.json"), JsonUtility.ToJson(new Summary {
                success = success, reason = reason, plannedLaps = 1, completedLaps = success ? 1 : 0,
                lapSeconds = lapSeconds, samples = _sampleId, collisions = _collisions,
                discontinuities = _teleports, runtimeErrors = _runtimeErrors, productMatchFinished = false }, true));
            Restore();
            Debug.Log($"[AI A1] END success={success} reason={reason} lapSeconds={lapSeconds:F3} output={OutputDirectory}");
            // End the observation through the ordinary session stop, without synthetic Finish or physics writes.
            SessionControl.Current?.RequestStopSession();
        }
        private void Restore()
        {
            if (_restored) return;
            _restored = true;
            Application.logMessageReceived -= OnLog;
            if (_driver != null) { _driver.Contact -= Collision; _driver.ModelCompared -= ModelCompared; _driver.enabled = false; }
            if (_skills != null) _skills.enabled = _skillWasEnabled;
            if (_boxes != null) for (int i = 0; i < _boxes.Length; i++) if (_boxes[i] != null)
            { _boxes[i].SetActive(_boxStates[i]); Event("test-box-restored", "RaceMap/DebugBox/" + _boxes[i].name + "; active=" + _boxes[i].activeSelf); }
            Application.runInBackground = _oldBackground; Application.targetFrameRate = _oldTargetFps; QualitySettings.vSyncCount = _oldVsync;
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
        [Serializable] private class SampleRow { public int sample, lap, nextCheckpoint, steering; public uint tick, gapTicks; public double clock, elapsed, planMs; public Vector3 position, velocity; public float yaw, yawRate, progress, lateral; public bool discontinuity, stalled; }
        [Serializable] private class EventRow { public string kind, detail; public double clock; public Vector3 position; }
        [Serializable] private class LineRow { public Vector3[] points; }
        [Serializable] private class Summary { public bool success, productMatchFinished; public string reason; public int plannedLaps, completedLaps, samples, collisions, discontinuities, runtimeErrors; public double lapSeconds; }
    }
}
#endif
