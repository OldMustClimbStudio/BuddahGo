using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

namespace BuddahGo.AI
{
    // One instance per spawned race, shared by every caster. No static scene state or per-AI scans.
    public sealed class AISkillWorld
    {
        private const int HistorySize = 48;
        private readonly AISkillSnapshot[] _history = new AISkillSnapshot[HistorySize];
        private readonly Entry[] _entries = new Entry[6];
        private readonly AISkillTuning _tuning;
        private readonly AISkillDecisionSchedule _schedule = new AISkillDecisionSchedule();
        private int _count, _head = -1, _written;
        private uint _lastTick, _captureTick;
        private bool _hasTick;
        private SplineRacingLine _line;
        private AISkillTrackTable _track;
        private readonly float[] _lastCasts = new float[6];

        private sealed class Entry
        {
            public RacerIdentity Identity;
            public Rigidbody Body;
            public BuddahPredictedMotor Motor;
            public SplineProgressTracker Progress;
            public LapProgress Lap;
            public RaceCompletionTracker Completion;
            public SkillPerceptionState Perception;
            public float PreviousSpeed;
        }

        public AISkillWorld(AISkillTuning tuning)
        {
            _tuning = tuning;
            for (int i = 0; i < HistorySize; i++) _history[i] = new AISkillSnapshot();
            for (int i = 0; i < _lastCasts.Length; i++) _lastCasts[i] = float.NegativeInfinity;
        }

        public void Advance(uint tick, float delta)
        {
            if (_hasTick && tick == _lastTick) return;
            _hasTick = true; _lastTick = tick;
            if (RoomStateManager.Instance == null || !RoomStateManager.Instance.IsAuthoritativeGoIssued) return;
            if (_line == null)
            {
                var track = TrackSplineRef.Instance;
                if (track == null || track.TrackLength <= 0f) return;
                _line = SplineRacingLine.Capture(track);
                _track = new AISkillTrackTable(_line, _tuning.StraightCurvature, _tuning.CornerCurvature);
            }
            if (_written > 0 && tick - _captureTick < Mathf.CeilToInt(_tuning.SnapshotSeconds / delta)) return;
            RefreshRoster();
            _captureTick = tick;
            _schedule.SnapshotCaptured(Time.frameCount);
            _head = (_head + 1) % HistorySize; _written = Mathf.Min(_written + 1, HistorySize);
            var frame = _history[_head]; frame.Tick = tick; frame.TickDelta = delta; frame.Count = _count;
            frame.ReverseSeconds = 0f; frame.CurtainSeconds = 0f;
            for (int i = 0; i < _count; i++)
            {
                var e = _entries[i];
                if (e.Identity == null || e.Motor == null || e.Body == null || e.Progress == null || e.Lap == null)
                { frame.Racers[i] = default; continue; }
                var stats = e.Motor.CurrentComputedStats;
                var modifiers = e.Motor.CurrentSkillModifiers;
                float speed = new Vector2(e.Body.velocity.x, e.Body.velocity.z).magnitude;
                var route = _line.SampleDistance(e.Progress.distanceOnTrack);
                bool accelerating = modifiers.AccelUntilTick > tick && modifiers.AccelExtraForwardForce > 0f
                    || modifiers.PostRootAccelUntilTick > tick && modifiers.RootUntilTick <= tick && modifiers.PostRootAccelExtraForwardForce > 0f;
                bool slowed = modifiers.AccelUntilTick > tick && (modifiers.AccelExtraForwardForce < 0f || modifiers.AccelExtraMaxSpeed < 0f);
                float impaired = Mathf.Max(Remaining(modifiers.RootUntilTick, tick, delta), Remaining(modifiers.InvertTurnUntilTick, tick, delta));
                if (slowed) impaired = Mathf.Max(impaired, Remaining(modifiers.AccelUntilTick, tick, delta));
                if (stats.ScaleMultiplier < .99f) impaired = Mathf.Max(impaired, Remaining(modifiers.ScaleUntilTick, tick, delta));
                // A speed drop is only a short observed window; never infer another racer's intent.
                if (e.PreviousSpeed > 5f && speed < e.PreviousSpeed * (1f - _tuning.SpeedDropFraction)) impaired = Mathf.Max(impaired, .5f);
                var racer = new AISkillRacerSnapshot
                {
                    RacerId = e.Identity.Id.Value, Position = e.Body.position, Forward = e.Body.rotation * Vector3.forward,
                    Velocity = e.Body.velocity, Progress = e.Lap.TotalProgress01 * _line.Length,
                    TrackDistance = e.Progress.distanceOnTrack, Speed = speed, AlongSpeed = Vector3.Dot(e.Body.velocity, route.Tangent),
                    Lateral = Vector3.Dot(e.Body.position - route.Point, Vector3.Cross(Vector3.up, route.Tangent)),
                    Available = e.Motor.IsServerInitialized && (e.Completion == null || !e.Completion.IsFinished)
                        && !e.Motor.IsLaunchHandoffActive && !e.Motor.IsAuthoritativeLaunchHandoffPending
                        && ResultAreaInteractionGate.ShouldProcessRaceProgress(e.Identity.gameObject),
                    Rooted = stats.IsRooted, Inverted = stats.IsInvertTurnActive, Suppressed = stats.IsSteeringSuppressed,
                    PushProtected = stats.IsPushGraceActive, Scale = stats.ScaleMultiplier, Accelerating = accelerating, Slowed = slowed,
                    PreviousSpeed = e.PreviousSpeed, ImpairedSecondsLeft = impaired,
                    InvertSecondsLeft = Remaining(modifiers.InvertTurnUntilTick, tick, delta),
                    VisionImpaired = e.Perception != null && e.Perception.VisionImpairedUntilTick > tick,
                    StraightFraction = _track.Fraction(e.Progress.distanceOnTrack, Mathf.Max(1f, speed * 10f), false),
                    StraightSeconds = _track.StraightDistance(e.Progress.distanceOnTrack) / Mathf.Max(5f, speed),
                    CornerFraction = _track.Fraction(e.Progress.distanceOnTrack, Mathf.Max(1f, speed * 2f), true),
                    CornerSeconds = 2f + 2f * _track.Fraction(e.Progress.distanceOnTrack + speed * 2f, Mathf.Max(1f, speed * 2f), true),
                    LastCastSeconds = _lastCasts[i]
                };
                e.PreviousSpeed = speed;
                frame.Racers[i] = racer;
                frame.ReverseSeconds = Mathf.Max(frame.ReverseSeconds, racer.InvertSecondsLeft);
                if (e.Perception != null) frame.CurtainSeconds = Mathf.Max(frame.CurtainSeconds, Remaining(e.Perception.CurtainUntilTick, tick, delta));
            }
        }

        public bool TryObserve(uint tick, float reactionSeconds, out AISkillSnapshot frame)
        {
            frame = null;
            for (int age = 0; age < _written; age++)
            {
                var candidate = _history[(_head - age + HistorySize) % HistorySize];
                if ((tick - candidate.Tick) * candidate.TickDelta + .0001f < reactionSeconds) continue;
                frame = candidate; return true;
            }
            return false;
        }

        public bool IsDecisionPhase(uint tick, int racerIndex)
            => tick != _captureTick && _schedule.TryClaim(tick, racerIndex, Time.frameCount);

        public bool IsGlobalEffectVisible(AISkillKind kind, uint tick)
        {
            if (kind != AISkillKind.Reverse && kind != AISkillKind.Curtain) return false;
            // Rare confirmation check, not a decision scan. Cached racer references, no scene query.
            for (int i = 0; i < _count; i++)
            {
                var entry = _entries[i];
                if (entry.Identity == null) continue;
                if (kind == AISkillKind.Curtain && entry.Perception != null && entry.Perception.CurtainUntilTick > tick) return true;
                if (kind == AISkillKind.Reverse && entry.Motor != null && entry.Motor.CurrentSkillModifiers.InvertTurnUntilTick > tick) return true;
            }
            return false;
        }

        public void RecordCast(int racerId, uint tick, float delta)
        {
            for (int i = 0; i < _count; i++)
                if (_entries[i].Identity != null && _entries[i].Identity.Id.Value == racerId) _lastCasts[i] = tick * delta;
        }

        private void RefreshRoster()
        {
            var directory = RacerDirectory.Current;
            if (directory == null || directory.All.Count == _count) return;
            _count = 0;
            foreach (var racer in directory.All)
            {
                if (racer == null || !racer.IsAssigned || _count >= _entries.Length) continue;
                _entries[_count++] = new Entry { Identity = racer, Body = racer.GetComponent<Rigidbody>(),
                    Motor = racer.GetComponent<BuddahPredictedMotor>(), Progress = racer.GetComponent<SplineProgressTracker>(),
                    Lap = racer.GetComponent<LapProgress>(), Completion = racer.GetComponent<RaceCompletionTracker>(),
                    Perception = racer.GetComponent<SkillPerceptionState>() };
            }
        }

        private static float Remaining(uint until, uint tick, float delta) => until > tick ? (until - tick) * delta : 0f;
    }

    // Prefix sums make speed-dependent straight/corner windows O(1), including lap wrapping.
    internal sealed class AISkillTrackTable
    {
        private readonly float _length, _step;
        private readonly int _count;
        private readonly float[] _straight, _corner, _straightRun;
        public AISkillTrackTable(SplineRacingLine line, float straightThreshold, float cornerThreshold)
        {
            _length = line.Length; _count = Mathf.Max(16, Mathf.CeilToInt(_length / 4f)); _step = _length / _count;
            _straight = new float[_count + 1]; _corner = new float[_count + 1]; _straightRun = new float[_count];
            for (int i = 0; i < _count; i++)
            {
                float curvature = Mathf.Abs(line.CurvatureAtDistance(i * _step));
                _straight[i + 1] = _straight[i] + (curvature <= straightThreshold ? 1f : 0f);
                _corner[i + 1] = _corner[i] + (curvature >= cornerThreshold ? 1f : 0f);
            }
            int run = 0;
            for (int i = _count * 2 - 1; i >= 0; i--)
            {
                int index = i % _count;
                run = _straight[index + 1] > _straight[index] ? Mathf.Min(_count, run + 1) : 0;
                if (i < _count) _straightRun[i] = run * _step;
            }
        }
        public float StraightDistance(float distance)
        {
            float index = Mathf.Repeat(distance, _length) / _step;
            int cell = Mathf.Min((int)index, _count - 1);
            return Mathf.Max(0f, _straightRun[cell] - (index - cell) * _step);
        }
        public float Fraction(float distance, float span, bool corner)
        {
            var prefix = corner ? _corner : _straight;
            float start = Mathf.Repeat(distance, _length) / _step;
            float cells = Mathf.Clamp(span / _step, .001f, _count);
            return Mathf.Clamp01((Integral(prefix, start + cells) - Integral(prefix, start)) / cells);
        }
        private float Integral(float[] prefix, float index)
        {
            float cycles = Mathf.Floor(index / _count); float local = index - cycles * _count;
            int cell = Mathf.Min((int)local, _count - 1);
            return cycles * prefix[_count] + Mathf.Lerp(prefix[cell], prefix[cell + 1], local - cell);
        }
    }
}
