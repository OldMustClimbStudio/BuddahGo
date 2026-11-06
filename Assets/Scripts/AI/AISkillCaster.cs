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
        // Counters, observations and frame accounting live apart from the decisions that produce them.
        public AISkillTelemetry Telemetry { get; } = new AISkillTelemetry();
        public bool CastingEnabled { get; set; } = true;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("AI.Skill");
        // Nested: cast requests and server pushes (coroutines, hitbox/projectile spawns) as opposed to decisions.
        private static readonly ProfilerMarker InputMarker = new ProfilerMarker("AI.Skill.Input");

        private SkillExecutor _executor;
        private BuddahHandControl _hands;
        private BuddahPredictedMotor _motor;
        private AIRacerDriver _driver;
        private RaceCompletionTracker _completion;
        private ObsessionFigure _obsession;
        private SkillPerceptionState _perception;
        private TimeManager _time;
        private AISkillWorld _world;
        private AISkillTuning _tuning;
        private readonly AISkillKind[] _kinds = new AISkillKind[3];
        private System.Random _decisionRandom;
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
            _executor = GetComponent<SkillExecutor>();
            _hands = GetComponent<BuddahHandControl>(); _motor = GetComponent<BuddahPredictedMotor>();
            _completion = GetComponent<RaceCompletionTracker>(); _obsession = GetComponent<ObsessionFigure>();
            _perception = GetComponent<SkillPerceptionState>();
            if (_executor == null || _hands == null || _motor == null || _obsession == null || _perception == null)
                throw new InvalidOperationException("AI racer is missing a required skill/input/perception component.");
            for (int slot = 0; slot < 3; slot++) _kinds[slot] = AISkillCatalog.Kind(personality.Loadout[slot]);
            _decisionRandom = new System.Random(unchecked(seed ^ 0x4A17D31));
            _reactionSeconds = SampleReaction();
            _time = _motor.TimeManager;
            Telemetry.Bind(_racerId, personality, difficulty, _obsession, _time);
            _driver = GetComponent<AIRacerDriver>();
            _driver.ConfigureSkillPerception(difficulty, _perception);
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
            AISkillTelemetry.AccumulateFrame(System.Diagnostics.Stopwatch.GetTimestamp() - start, _inputTicksThisTick);
            _inputTicksThisTick = 0;
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
            // Rooting does not stop a Human Player's accepted cast from executing, so it neither cancels the
            // commitment nor the pending cast here; it only blocks new decisions below.
            bool rooted = _motor.CurrentComputedStats.IsRooted;
            Commitment.ReleaseResolved();
            if (Commitment.Active)
            {
                // Requested: the executor resolves it (Accepted → Executed / Cancelled, or Rejected). A Human Player
                // cannot withdraw an accepted cast either, and a cancel would not refund the cooldown.
                // Liveness: Accepted and ClearPendingCast → Executed / Cancelled are synchronous with respect to
                // OnTick, so Waiting without a pending cast means the terminal event was lost (an exception in the
                // executor's coroutine); release the commitment instead of idling for the rest of the race.
                if (Commitment.Waiting) { if (!_executor.HasPendingCast) Cancel("lost-pending"); return; }
                if (tick >= Commitment.Deadline) { Cancel("opportunity-expired"); return; }
                // The tick after the decision, like a Human Player pressing the slot key: the same CastSlotServer
                // path as the owner RPC (serial casts, same-skill surcharge and the unified backfire all live there).
                if (Commitment.ShouldRequest(tick)) Request(Commitment.Slot);
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

        private void Request(int slot)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool accepted;
            using (InputMarker.Auto()) accepted = _executor.CastSlotServer(slot);
            _inputTicksThisTick += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            // A refusal already arrived as the Rejected event (cancelled and counted there); this is only the
            // fallback when no event reached us, and it is silent when the commitment is already gone.
            if (!accepted) Cancel("rejected");
        }

        private void Decide(AISkillSnapshot frame, int self, uint tick, float delta)
        {
            float elapsed = _lastDecision == 0 ? Difficulty.DecisionSeconds : Mathf.Min(_tuning.MaxElapsedSeconds, (tick - _lastDecision) * delta);
            _lastDecision = tick; _nextDecision = tick + AISkillCommitment.SecondsToTicks(Difficulty.DecisionSeconds, delta);
            UpdateSituation(AISkillDecision.Situation(frame, self, _tuning), tick, delta);
            float age = (tick - frame.Tick) * delta;
            float probability = _obsession.GetBackfireProbabilityPercent(_obsession.Current) * .01f;
            // No combo keys and no key retries: the request lands next tick, then the confirm delay.
            float confirm = _executor.ConfirmationSeconds;
            float required = confirm + delta * 2f;
            float best = float.NegativeInfinity; int chosen = -1;
            AISkillOpportunity opportunity = default;
            for (int slot = 0; slot < 3; slot++)
            {
                if (!_executor.IsSlotReadyServer(slot)) { Emit("candidate", "cooldown-or-lock", slot); continue; }
                var candidate = AISkillDecision.Evaluate(_kinds[slot], frame, self, _tuning, Personality.Target, _hands.ServerHandYaw, _hands.HandRotationSpeed);
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
            Commitment.Begin(chosen, tick, deadline);
            Emit("decision", "committed", chosen, opportunity, best);
        }

        private bool Aim(AISkillSnapshot frame, int self, uint tick, float delta)
        {
            float remaining = _hands.ProjectileBuffSecondsLeft;
            if (remaining <= 0f) { _hadBuff = false; return false; }
            if (!_hadBuff) { _hadBuff = true; _shotsThisBuff = 0; _nextShot = tick; }
            // Hold decisions only while actively aiming with shots left. Pushes during the buff fire buff shots exactly
            // as a Human Player's mouse buttons would; the hands slot itself is on cooldown, so no double buff.
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
            // A direct left-hand push (no combo key); the whole injection (coroutines, projectile spawns) is AI.Skill.Input.
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool pushed;
            using (InputMarker.Auto()) pushed = _hands.InjectServerPush(true);
            _inputTicksThisTick += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            if (pushed)
            {
                _shotsThisBuff++; Telemetry.BuffShots++;
                _nextShot = tick + AISkillCommitment.SecondsToTicks(_hands.ServerPushCooldown, delta);
                Emit("aim-push", "accepted", -1, opportunity);
            }
            return true;
        }

        private void OnCast(SkillExecutor.CastEvent cast)
        {
            switch (cast.Stage)
            {
                case SkillExecutor.CastStage.Requested: Telemetry.Requests++; break;
                case SkillExecutor.CastStage.Accepted:
                    Telemetry.Accepted++; Commitment.Accepted();
                    break;
                case SkillExecutor.CastStage.Executed:
                    Telemetry.Executed++; if (cast.IsBacklash) Telemetry.Backlashes++;
                    Commitment.Resolve(); _world.RecordCast(_racerId, _time.LocalTick, (float)_time.TickDelta); break;
                case SkillExecutor.CastStage.Cancelled: Telemetry.Cancelled++; Commitment.Cancel(); break;
                // Serial casts: name the gate that refused the request (pending / backfire lock / frozen / cooldown).
                case SkillExecutor.CastStage.Rejected:
                    // None = the gate was open, so the AI-only entry check refused it (a same-kind global effect is visible).
                    SkillCastBlock block = _executor.ResolveCastBlockServer(cast.Slot);
                    Cancel("rejected-" + (block == SkillCastBlock.None ? "global-visible" : block.ToString().ToLowerInvariant())); break;
            }
            Emit("cast", cast.Stage.ToString(), cast.Slot, backlash: cast.IsBacklash);
        }
        private void OnEffectsReset()
        {
            Cancel("effects-reset"); _hadBuff = false; _shotsThisBuff = 0;
            _driver?.ResetSkillPerception();
        }
        // An accepted cast is kept when the opportunity merely expired (a Human Player cannot withdraw one either, and a
        // cancel would not refund the cooldown) and when a request was refused (the pending cast is then not ours).
        // "lost-pending" never has a pending cast (that is its condition), so it only releases the commitment.
        private static bool KeepsAcceptedCast(string reason) => reason == "opportunity-expired" || reason.StartsWith("rejected", StringComparison.Ordinal);
        private void Cancel(string reason)
        {
            bool committed = Commitment.Active; int slot = Commitment.Slot;
            if (reason == "opportunity-expired" && Commitment.Waiting) return;
            bool withdraw = _executor.HasPendingCast && !KeepsAcceptedCast(reason);
            if (!committed && !withdraw) return;
            if (withdraw)
            {
                // The executor keeps a confirming backfire (Human Player rule): it still ends in Executed + IsBacklash.
                // Once the commitment is gone there is nothing left to cancel, so repeated calls stay silent.
                // A withdrawn cast arrives as the Cancelled event, which counts and clears the commitment.
                if (!_executor.CancelPendingCastServer() && !committed) return;
            }
            else Telemetry.Cancelled++;
            Commitment.Cancel();
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
            => Telemetry.Emit(kind, reason, slot, _situation, _targetId, opportunity, utility, backlash);
    }
}
