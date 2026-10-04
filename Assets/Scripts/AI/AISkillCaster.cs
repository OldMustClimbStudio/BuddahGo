using System;
using BuddahGo.Match;
using FishNet.Managing.Timing;
using NewBuddah.PredictionV2.Core;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using Unity.Profiling;
using UnityEngine;

namespace BuddahGo.AI
{
    [DisallowMultipleComponent]
    public sealed class AISkillCaster : MonoBehaviour, ISkillCastContinuation
    {
        public AISkillPersonality Personality { get; private set; }
        public AISkillDifficulty Difficulty { get; private set; }
        public AISkillCommitment Commitment { get; } = new AISkillCommitment();
        public bool CastingEnabled { get; set; } = true;
        public int Requests { get; private set; }
        public int Accepted { get; private set; }
        public int Executed { get; private set; }
        public int Backlashes { get; private set; }
        public int Cancelled { get; private set; }
        public int Keys { get; private set; }
        public int BuffShots { get; private set; }
        public static readonly ProfilerMarker Marker = new ProfilerMarker("AI.Skill");
        // Nested: key injection and server pushes (coroutines, hitbox/projectile spawns) as opposed to decisions.
        public static readonly ProfilerMarker InputMarker = new ProfilerMarker("AI.Skill.Input");
        public static long WorkTicksThisFrame => _workFrame == Time.frameCount ? _workTicksThisFrame : 0;
        // Portion of WorkTicksThisFrame spent inside AI.Skill.Input (key injection, server pushes, spawns).
        public static long InputTicksThisFrame => _workFrame == Time.frameCount ? _inputTicksThisFrame : 0;
        private static long _workTicksThisFrame, _inputTicksThisFrame;
        private static int _workFrame = -1;

        [Serializable]
        public struct Observation
        {
            public uint Tick;
            public int RacerId, Slot, TargetId;
            public string Personality, Event, Reason, Skill;
            public SoloDifficulty Difficulty;
            public AISkillSituation Situation;
            public float Opportunity, ValidSeconds, Obsession, BacklashProbability, Utility;
            public bool TargetImpaired, Backlash;
        }
        public event Action<Observation> Observed;

        private SkillExecutor _executor;
        private ComboSkillInput _combo;
        private BuddahHandControl _hands;
        private BuddahPredictedMotor _motor;
        private RaceCompletionTracker _completion;
        private ObsessionFigure _obsession;
        private SkillPerceptionState _perception;
        private TimeManager _time;
        private AISkillWorld _world;
        private AISkillTuning _tuning;
        private readonly ComboSkillInput.Token[][] _sequences = new ComboSkillInput.Token[3][];
        private readonly AISkillKind[] _kinds = new AISkillKind[3];
        private System.Random _decisionRandom, _keyRandom;
        private int _racerId, _index, _targetId = -1, _shotsThisBuff;
        private uint _nextDecision, _lastDecision, _nextShot, _situationSince;
        private AISkillSituation _situation, _pendingSituation;
        private bool _subscribed, _hadBuff, _finishedCleaned, _hasSituation;
        private float _reactionSeconds;
        private long _inputTicksThisTick;

        public void Configure(AISkillWorld world, AISkillPersonality personality, AISkillDifficulty difficulty,
            AISkillTuning tuning, int racerIndex, int seed)
        {
            _world = world; Personality = personality; Difficulty = difficulty; _tuning = tuning;
            _index = racerIndex; _racerId = RacerId.ForAI(racerIndex).Value;
            _executor = GetComponent<SkillExecutor>(); _combo = GetComponent<ComboSkillInput>();
            _hands = GetComponent<BuddahHandControl>(); _motor = GetComponent<BuddahPredictedMotor>();
            _completion = GetComponent<RaceCompletionTracker>(); _obsession = GetComponent<ObsessionFigure>();
            _perception = GetComponent<SkillPerceptionState>();
            if (_executor == null || _combo == null || _hands == null || _motor == null || _obsession == null || _perception == null)
                throw new InvalidOperationException("AI racer is missing a required skill/input/perception component.");
            for (int slot = 0; slot < 3; slot++)
            {
                if (!_combo.TryGetSequenceForSlot(slot, out _sequences[slot])) throw new InvalidOperationException("AI skill slot has no combo binding.");
                _kinds[slot] = AISkillCatalog.Kind(personality.Loadout[slot]);
            }
            _decisionRandom = new System.Random(unchecked(seed ^ 0x4A17D31));
            _keyRandom = new System.Random(unchecked(seed ^ 0x615F321));
            _reactionSeconds = SampleReaction();
            _time = _motor.TimeManager;
            GetComponent<AIRacerDriver>().ConfigureSkillPerception(difficulty, _perception);
            enabled = true;
            Subscribe();
        }

        public bool CanContinueCast(int slotIndex)
        {
            if (!isActiveAndEnabled || Personality == null) return true;
            // Rooting does not cancel a Human Player's queued cast, so it does not cancel an AI's either (Q2).
            if (!CastingEnabled || _motor.IsLaunchHandoffActive
                || _motor.IsAuthoritativeLaunchHandoffPending || _completion != null && _completion.IsFinished) return false;
            if (slotIndex < 0 || slotIndex >= _kinds.Length) return false;
            // Two independent AIs can commit before either effect is visible. Recheck only public,
            // already-active global effects at confirmation; never inspect either pending draw.
            return !_world.IsGlobalEffectVisible(_kinds[slotIndex], _time.LocalTick);
        }

        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (_subscribed || _time == null || !RacerAuthority.IsServerAI(_motor)) return;
            _subscribed = true;
            _time.OnTick += OnTick;
            _executor.ServerCastChanged += OnCast;
            _executor.ActiveEffectsReset += OnEffectsReset;
        }
        private void OnDisable()
        {
            if (!_subscribed) return;
            Cancel("disabled");
            if (_time != null) _time.OnTick -= OnTick;
            if (_executor != null) { _executor.ServerCastChanged -= OnCast; _executor.ActiveEffectsReset -= OnEffectsReset; }
            _subscribed = false;
        }

        private void OnTick()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            using (Marker.Auto()) Tick();
            if (_workFrame != Time.frameCount) { _workFrame = Time.frameCount; _workTicksThisFrame = 0; _inputTicksThisFrame = 0; }
            _workTicksThisFrame += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            _inputTicksThisFrame += _inputTicksThisTick; _inputTicksThisTick = 0;
        }

        private void Tick()
        {
            if (!RacerAuthority.IsServerAI(_motor) || _time == null) return;
            uint tick = _time.LocalTick; float delta = (float)_time.TickDelta;
            _world.Advance(tick, delta);
            bool finished = _completion != null && _completion.IsFinished;
            bool racing = ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject);
            if (finished || !racing)
            {
                Cancel(finished ? "finished" : "outside-race");
                if (!_finishedCleaned && (finished || RoomStateManager.Instance != null && RoomStateManager.Instance.IsResultPhaseActive))
                { _finishedCleaned = true; _executor.ResetActiveSkillEffectsServer(); }
                return;
            }
            if (!CastingEnabled || _motor.IsLaunchHandoffActive || _motor.IsAuthoritativeLaunchHandoffPending)
            { Cancel(!CastingEnabled ? "casting-disabled" : "input-blocked"); _hands.CancelPendingServerInput(); return; }
            // Rooting does not stop a Human Player from finishing a combo or from having an accepted cast execute,
            // so it neither cancels the commitment nor the pending cast here; it only blocks new decisions below.
            bool rooted = _motor.CurrentComputedStats.IsRooted;
            Commitment.ReleaseResolved();
            if (Commitment.Active)
            {
                if (tick >= Commitment.Deadline) { Cancel("opportunity-expired"); return; }
                bool wasActive = Commitment.Active;
                if (Commitment.NextKey(tick, delta, _tuning.KeyMinSeconds, _tuning.KeyMaxSeconds, _combo.StepWindowSeconds,
                    Difficulty.KeyMistakeProbability, _keyRandom, out var key))
                {
                    Keys++;
                    Emit("key", key == ComboSkillInput.Token.W ? "W" : "Up", Commitment.Slot);
                    // Independent recognizer and hand listeners match the player's input semantics:
                    // a hand on cooldown does not suppress that key's combo token.
                    long inputStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    using (InputMarker.Auto())
                    {
                        _hands.InjectServerPush(key == ComboSkillInput.Token.W);
                        _combo.InjectServerKey(key, tick * (double)delta);
                    }
                    _inputTicksThisTick += System.Diagnostics.Stopwatch.GetTimestamp() - inputStart;
                    if (Commitment.Waiting && !_executor.HasPendingCast) Cancel("combo-not-accepted");
                }
                else if (wasActive && !Commitment.Active) { Cancelled++; _combo.ClearCombo(); Emit("cancel", "retry-exhausted"); }
                return;
            }
            float reaction = _reactionSeconds + (_perception.VisionImpairedUntilTick > tick ? Difficulty.ImpairedReactionTicks * delta : 0f);
            if (!_world.TryObserve(tick, reaction, out var frame)) return;
            int self = frame.IndexOf(_racerId);
            if (self < 0 || !frame.Racers[self].Available) return;
            if (Aim(frame, self, tick, delta)) return;
            if (rooted || tick < _nextDecision || !_world.IsDecisionPhase(tick, _index)) return;
            Decide(frame, self, tick, delta);
        }

        private void Decide(AISkillSnapshot frame, int self, uint tick, float delta)
        {
            float elapsed = _lastDecision == 0 ? Difficulty.DecisionSeconds : Mathf.Min(1f, (tick - _lastDecision) * delta);
            _lastDecision = tick; _nextDecision = tick + AISkillCommitment.SecondsToTicks(Difficulty.DecisionSeconds, delta);
            UpdateSituation(AISkillDecision.Situation(frame, self, _tuning), tick, delta);
            float age = (tick - frame.Tick) * delta;
            float probability = _obsession.GetBackfireProbabilityPercent(_obsession.Current) * .01f;
            float best = float.NegativeInfinity; int chosen = -1;
            AISkillOpportunity opportunity = default;
            for (int slot = 0; slot < 3; slot++)
            {
                if (!_executor.IsSlotReadyServer(slot)) { Emit("candidate", "cooldown-or-lock", slot); continue; }
                var candidate = AISkillDecision.Evaluate(_kinds[slot], frame, self, _tuning, Personality.Target, _hands.ServerHandYaw, _hands.HandRotationSpeed);
                float comboSeconds = (_sequences[slot].Length - 1) * _tuning.KeyMaxSeconds;
                // Expected retry cost: one expired input window plus a restarted combo, weighted by the per-key mistake rate.
                float retrySeconds = Difficulty.KeyMistakeProbability * (_combo.StepWindowSeconds + delta + comboSeconds);
                float required = comboSeconds + retrySeconds + _executor.ConfirmationSeconds + delta * 2f;
                float noise = 1f + ((float)_decisionRandom.NextDouble() * 2f - 1f) * Difficulty.DecisionNoise;
                float utility = AISkillDecision.Utility(_kinds[slot], candidate, _situation, Personality, Difficulty, _tuning, probability, required, age, noise);
                Emit("candidate", candidate.Score <= 0f ? "no-opportunity" : candidate.ValidSeconds - age < required ? "validity" : float.IsNegativeInfinity(utility) ? "risk-stop" : "scored", slot, candidate, utility);
                if (utility > best) { best = utility; chosen = slot; opportunity = candidate; }
            }
            _reactionSeconds = SampleReaction();
            if (chosen < 0) return;
            if (_decisionRandom.NextDouble() >= AISkillDecision.CastProbability(best, Personality.Patience, elapsed, Personality.TempoSeconds))
            { Emit("decision", "sample-wait", chosen, opportunity, best); return; }
            _targetId = opportunity.TargetId;
            uint deadline = tick + AISkillCommitment.SecondsToTicks(opportunity.ValidSeconds - age, delta);
            _combo.ClearCombo();
            Commitment.Begin(chosen, _sequences[chosen], tick, deadline);
            Emit("decision", "committed", chosen, opportunity, best);
        }

        private bool Aim(AISkillSnapshot frame, int self, uint tick, float delta)
        {
            float remaining = _hands.ProjectileBuffSecondsLeft;
            if (remaining <= 0f) { _hadBuff = false; return false; }
            if (!_hadBuff) { _hadBuff = true; _shotsThisBuff = 0; _nextShot = tick; }
            // Hold decisions only while actively aiming with shots left. Combo keys pressed during the buff fire
            // buff shots exactly as a Human Player's would; the hands slot itself is on cooldown, so no double buff.
            if (_shotsThisBuff >= Personality.HandsShots) return false;
            var opportunity = AISkillDecision.Evaluate(AISkillKind.Hands, frame, self, _tuning, Personality.Target, _hands.ServerHandYaw, _hands.HandRotationSpeed);
            int target = frame.IndexOf(opportunity.TargetId);
            // No reachable target right now: stop turning and let ordinary decisions resume.
            if (opportunity.Score <= 0f || target < 0) { _hands.InjectServerRotation(0, delta); return false; }
            _targetId = opportunity.TargetId;
            Vector3 direction = frame.Racers[target].Position - frame.Racers[self].Position; direction.y = 0f;
            float yaw = Vector3.SignedAngle(frame.Racers[self].Forward, direction, Vector3.up);
            float error = Mathf.DeltaAngle(_hands.ServerHandYaw, yaw);
            _hands.InjectServerRotation(Mathf.Abs(error) <= Difficulty.AimToleranceDegrees ? 0 : error > 0f ? 1 : -1, delta);
            if (Mathf.Abs(error) > Difficulty.AimToleranceDegrees || tick < _nextShot || !_hands.CanPushServer(true)) return true;
            bool pushed;
            long inputStart = System.Diagnostics.Stopwatch.GetTimestamp();
            using (InputMarker.Auto())
            {
                pushed = _hands.InjectServerPush(true);
                if (pushed) _combo.InjectServerKey(ComboSkillInput.Token.W, tick * (double)delta);
            }
            _inputTicksThisTick += System.Diagnostics.Stopwatch.GetTimestamp() - inputStart;
            if (pushed)
            {
                _shotsThisBuff++; BuffShots++; Keys++;
                _nextShot = tick + AISkillCommitment.SecondsToTicks(Mathf.Max(_combo.StepWindowSeconds + delta, _hands.ServerPushCooldown), delta);
                Emit("aim-push", "accepted", -1, opportunity);
            }
            return true;
        }

        private void OnCast(SkillExecutor.CastEvent cast)
        {
            switch (cast.Stage)
            {
                case SkillExecutor.CastStage.Requested: Requests++; break;
                case SkillExecutor.CastStage.Accepted: Accepted++; Commitment.Accepted(); break;
                case SkillExecutor.CastStage.Executed:
                    Executed++; if (cast.IsBacklash) Backlashes++;
                    Commitment.Resolve(); _world.RecordCast(_racerId, _time.LocalTick, (float)_time.TickDelta); break;
                case SkillExecutor.CastStage.Cancelled: Cancelled++; Commitment.Cancel(); break;
                case SkillExecutor.CastStage.Rejected: Cancel("rejected"); break;
            }
            Emit("cast", cast.Stage.ToString(), cast.Slot, backlash: cast.IsBacklash);
        }
        private void OnEffectsReset()
        {
            Cancel("effects-reset"); _hadBuff = false; _shotsThisBuff = 0;
            GetComponent<AIRacerDriver>()?.ResetSkillPerception();
        }
        private void Cancel(string reason)
        {
            if (!Commitment.Active && !_executor.HasPendingCast) return;
            int slot = Commitment.Slot;
            if (_executor.HasPendingCast) _executor.CancelPendingCastServer(); else Cancelled++;
            Commitment.Cancel(); _combo.ClearCombo();
            Emit("cancel", reason, slot);
        }
        private float SampleReaction() => Difficulty.ReactionMinSeconds + (float)_decisionRandom.NextDouble() * (Difficulty.ReactionMaxSeconds - Difficulty.ReactionMinSeconds);
        private void UpdateSituation(AISkillSituation candidate, uint tick, float delta)
        {
            if (!_hasSituation) { _hasSituation = true; _situation = _pendingSituation = candidate; _situationSince = tick; return; }
            if (candidate != _pendingSituation) { _pendingSituation = candidate; _situationSince = tick; }
            if (tick - _situationSince >= AISkillCommitment.SecondsToTicks(_tuning.SituationHysteresisSeconds, delta)) _situation = candidate;
        }
        private void Emit(string kind, string reason, int slot = -1, AISkillOpportunity opportunity = default, float utility = 0f, bool backlash = false)
        {
            if (Observed == null && !NetDebug.EnableVerboseLog) return;
            var observation = new Observation { Tick = _time.LocalTick, RacerId = _racerId, Personality = Personality.Id,
                Difficulty = Difficulty.Difficulty, Event = kind, Reason = reason, Slot = slot, TargetId = opportunity.Score > 0f ? opportunity.TargetId : _targetId,
                Skill = slot >= 0 ? Personality.Loadout[slot] : string.Empty, Situation = _situation,
                Opportunity = opportunity.Score, ValidSeconds = opportunity.ValidSeconds, TargetImpaired = opportunity.ImpairedTarget,
                Obsession = _obsession.Current, BacklashProbability = _obsession.CurrentBackfireProbabilityPercent * .01f,
                Utility = float.IsNegativeInfinity(utility) ? -999f : utility, Backlash = backlash };
            Observed?.Invoke(observation);
            if (NetDebug.EnableVerboseLog) GameLog.Verbose("[AI.Skill] " + JsonUtility.ToJson(observation));
        }
    }
}
