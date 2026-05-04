using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using System;
using System.Text;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Integration;
using NewBuddah.PredictionV2.Simulation;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
using System.Collections.Generic;
#endif
#if BUDDAH_PREDICTION_PERF_PROBE
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
using UnityEngine;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;

namespace NewBuddah.PredictionV2.Core
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class BuddahPredictedMotor : TickNetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictedMotorConfig config;
        [SerializeField] private Rigidbody rb;

        private readonly BuddahPredictionOwnerInputBridge _ownerInputBridge = new();
        private readonly BuddahPredictionMovementGateBridge _movementGateBridge = new();
        private PredictionRigidbody _predictionRigidbody;
        private BuddahPredictedModifierState _modifierState;
        private BuddahPredictedMotorComputedStats _computedStats;
        private uint _nextTeleportEventId = 1u;
        private bool _impulseConsumedThisTick;
        private bool _hasPendingTeleportEvent;
        private BuddahPredictedTeleportEventData _pendingTeleportEvent;
        private uint _lastConsumedTeleportEventId;
        private BuddahPredictedLaunchHandoffState _handoffState;
        private bool _introControlActive;
        private bool _externalKinematicControlActive;
        private bool _lastLoggedIntroWriterSuppressed;
        private string _lastLoggedIntroWriterReason;
        private bool _lastLoggedIntroReconcileSkipped;
        private bool _lastLoggedIntroReconcileControlled;
        private bool _lastLoggedIntroReconcileHostOwner;
        private bool _lastLoggedIntroReconcilePending;
        private SplineProgressTracker _splineProgressTracker;
        private SkillExecutor _skillExecutor;
        private float _baseMass = 1f;

#if BUDDAH_PREDICTION_PERF_PROBE
        // V13 perf probe — Stopwatch-backed per-frame accumulator consumed by
        // BuddahPredictionPerfProbe. Replaces Phase 4 ProfilerMarker path (which
        // required active Profiler recording to sample custom markers; Entry 7 M2
        // switches to System.Diagnostics.Stopwatch so standalone Player.log runs
        // produce valid rep-*-ms). Compiles out when the define is undefined ->
        // release builds carry zero timing overhead. Reviewer G1 bind.
        //
        // Main-thread invariant: every RunInputs invocation (forward tick +
        // FishNet reconcile replays) originates from TimeManager.TickUpdate on
        // the main thread; the probe reads + zeros this field from its own
        // Update (also main thread). Safe without atomics.
        internal static long s_runInputsTicksThisFrame;

        internal readonly ref struct PerfProbeScope
        {
            private readonly long _startTicks;
            private PerfProbeScope(long startTicks) { _startTicks = startTicks; }
            public static PerfProbeScope Auto() => new PerfProbeScope(Stopwatch.GetTimestamp());
            public void Dispose() => s_runInputsTicksThisFrame += Stopwatch.GetTimestamp() - _startTicks;
        }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
        private BuddahPredictionShadowScratch _realScratch;
        private BuddahPredictionShadowScratch _shadowScratch;
        private int _dLocConsecutive;
        private int _shadowActiveCompares;
        private int _shadowSkipCompares;
        // Phase 4b V5 Q3-B — reconcile-callback count. Cumulative-since-spawn; emitted in
        // [D-LOC HEARTBEAT] as rec-cb. Used as LatencySim engagement sanity check: under
        // simulated latency, FishNet fires reconcile more often → growth rate proportional.
        // 0 callbacks across a session under LatencySim = simulator not engaged.
        private uint _reconcileCallbackCount;
        private Vector3 _shadowPreClampVelocity;
        private bool _shadowPreTeleportHasPending;
        private BuddahPredictedTeleportEventData _shadowPreTeleportEvent;
        // Per-window divergence counters (reset at heartbeat).
        // V2b Step 1 Q4 + V4: impulse axis fully retired; only loc/tel/mod/hof divergence tracked.
        private int _dLocLocomotionDivCount;
        private int _dLocTeleportDivCount;
        // Cumulative consume counters (never reset — prove shadow steps actually consumed events).
        private uint _shadowTeleportConsumedCount;
        // Phase 3c — modifier shadow state. Snapshot of _modifierState captured at the
        // motor.cs:341 authoritative Resolve; fed into BuddahModifierStep for independent
        // Resolve+compare. Cumulative counter obeys L13 (compared>0 gate); per-window
        // divergence counter resets at heartbeat.
        private BuddahPredictedModifierState _shadowModifierStateSnapshot;
        private uint _shadowModifierConsumedCount;
        private int _dLocModifierDivCount;
        // Phase 6 — handoff shadow collapsed to single state field (Phase 3d
        // pre-consume pending-event slot retired with the RPC chain). Cumulative
        // counter obeys L13 (compared>0 gate); per-window divergence counter
        // resets at heartbeat.
        private BuddahPredictedLaunchHandoffState _shadowPreHandoffState;
        private uint _shadowHandoffConsumedCount;
        private int _dLocHandoffDivCount;
#endif

        // L7 latch tracker. Flipped to true on the first RunInputs tick where
        // ShouldRunPrediction passes — guarantees BuddahMovementModeSwitcher's
        // double-ApplyMode + its 4 [CommandBus]:ClearAll lines have all landed
        // before the adapter's MarkReady() fires (lessons-log L7 rule b).
        private bool _combatAdapterInitialized;

        public bool IsLaunchHandoffActive => _handoffState.IsActive || _externalKinematicControlActive || _introControlActive;
        public float CurrentScaleMultiplier => _computedStats.ScaleMultiplier > 0f ? _computedStats.ScaleMultiplier : 1f;
        public bool IsPredictionIntroControlActive => _introControlActive;
        public bool IsPredictionExternalKinematicControlActive => _externalKinematicControlActive;
        // Phase 6 — owner-initiated pending state retired (Section 11.1 option a).
        // No transient pending window in new design; Locked is the only "active" state.
        public bool IsAuthoritativeLaunchHandoffPending => false;
        public bool IsPredictionLaunchHandoffConsumedOrActive => _handoffState.IsActive;

        private void Awake()
        {
            ResolveReferences();
            InitializePredictionRigidbody();
            enabled = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _ownerInputBridge.Initialize();
            RefreshInputBridge();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _ownerInputBridge.Dispose();
        }

        private void OnEnable()
        {
            ResolveReferences();
            InitializePredictionRigidbody();
            RefreshInputBridge();
        }

        private void OnDisable()
        {
            _ownerInputBridge.SetEnabled(false);
            if (rb != null)
                rb.mass = _baseMass;
            if (bootstrap != null)
                bootstrap.RefreshDebugBanner("predicted motor disabled");
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (config == null)
                config = GetComponent<BuddahPredictedMotorConfig>();
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (rb != null)
                _baseMass = Mathf.Max(0.0001f, rb.mass);
            if (_splineProgressTracker == null)
                _splineProgressTracker = GetComponent<SplineProgressTracker>();
            if (_skillExecutor == null)
                _skillExecutor = GetComponent<SkillExecutor>();

            bootstrap?.ResolveReferences();
        }

        private void InitializePredictionRigidbody()
        {
            if (rb == null)
                return;

            _predictionRigidbody ??= new PredictionRigidbody();
            _predictionRigidbody.Initialize(rb);
        }

        private void Update()
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.isOwner = IsOwner;
            bootstrap.DebugState.rigidbodyIsKinematic = rb != null && rb.isKinematic;
            RefreshInputBridge();
            bootstrap.DebugState.inputBridgeEnabled = _ownerInputBridge.IsEnabled;
            bootstrap.DebugState.predictionBlockReason = GetPredictionBlockReason();
            var impulseChannel = bootstrap.CommandBus != null ? bootstrap.CommandBus.ImpulseChannel : null;
            bootstrap.DebugState.pendingImpulseCount = impulseChannel != null ? impulseChannel.Count : 0;
            bootstrap.DebugState.pendingImpulseSummary = impulseChannel != null ? impulseChannel.BuildPendingSummary() : "none";
            bootstrap.DebugState.introControlActive = _introControlActive;
            bootstrap.DebugState.externalKinematicControlActive = _externalKinematicControlActive;
        }

        protected override void TimeManager_OnTick()
        {
            if (!ShouldRunPrediction())
                return;

            RunInputs(BuildReplicateData());
        }

        protected override void TimeManager_OnPostTick()
        {
            if (!ShouldRunPrediction())
                return;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            // Phase 4b V2b Step 0 — drain relocated back to RunInputs (alongside OLD's
            // ConsumePendingImpulseEvents) now that the channel is tick-stamped + replay-safe via
            // ConsumeReady. PostTick now only does compare+reset, matching the V2a-pre-fix structure.
            // L17 phase-skew is eliminated at the call-site level: OLD and NEW drains run at the
            // same lifecycle phase against the same currentTick.
            if (IsOwner || IsServerInitialized)
                Shadow_CompareAndReport();
            _realScratch = default;
            _shadowScratch = default;
#endif

            if (!IsServerInitialized)
                return;

            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            if (!ShouldRunPrediction() || rb == null || _predictionRigidbody == null)
                return;

            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;
            bool isLocked = _handoffState.IsActive && _handoffState.CurrentState == BuddahPredictedLaunchState.Locked;
            bool movementAllowed = !isLocked
                                   && (_movementGateBridge.IsMovementAllowed(gameObject)
                                       || _computedStats.IsRoomBypassActive);

            BuddahPredictedReconcileData data = new(
                _predictionRigidbody,
                _modifierState,
                _computedStats,
                _handoffState,
                _introControlActive,
                _externalKinematicControlActive,
                movementAllowed,
                planarVelocity.magnitude,
                transform.forward);

            data.PendingTeleport = default;
            data.HasPendingTeleport = false;
            data.LastConsumedTeleportId = 0u;
            data.PendingHandoff = default;
            data.HasPendingHandoff = false;
            data.LastConsumedHandoffId = 0u;
            data.AwaitingAuthoritativeLaunchHandoff = false;
            data.LocalPreHandoffBypassUntilTick = 0u;

            ReconcileState(data);
        }

        private bool ShouldRunPrediction()
        {
            return isActiveAndEnabled
                   && bootstrap != null
                   && bootstrap.IsPredictionModeActive()
                   && config != null
                   && rb != null;
        }

        private string GetPredictionBlockReason()
        {
            if (!isActiveAndEnabled)
                return "component-disabled";
            if (bootstrap == null)
                return "missing-bootstrap";
            if (!bootstrap.IsPredictionModeActive())
                return "mode-not-prediction";
            if (config == null)
                return "missing-config";
            if (rb == null)
                return "missing-rigidbody";

            return "running";
        }

        private void RefreshInputBridge()
        {
            bool enableOwnerInput = isActiveAndEnabled
                                    && bootstrap != null
                                    && bootstrap.IsPredictionModeActive()
                                    && IsOwner;
            _ownerInputBridge.SetEnabled(enableOwnerInput);
        }

        private BuddahPredictedInputData BuildReplicateData()
        {
            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            // Phase 6 — Locked state forces movementAllowed=false, which drives the
            // motor.cs:466 MovementAllowed=false early-return (zero AddForce, zero
            // velocity write — G8b replay determinism). Auto-forward (throttle=1f)
            // resumes automatically when LockedUntilTick passes.
            bool isLocked = _handoffState.IsActive && _handoffState.CurrentState == BuddahPredictedLaunchState.Locked;
            bool movementAllowed = !isLocked
                                   && (_movementGateBridge.IsMovementAllowed(gameObject)
                                       || _computedStats.IsRoomBypassActive);
            float steering = movementAllowed ? (_ownerInputBridge.ReadSteering() * config.TurnInputMultiplier) : 0f;
            float throttle = movementAllowed ? 1f : 0f;
            bool ownerInputLive = IsOwner && movementAllowed;
#if UNITY_EDITOR
            // G8b — During Phase 3 Lock, motor must NEVER reach the post-MovementAllowed
            // gameplay path (which produces AddForce calls). isLocked → movementAllowed=false
            // → motor.cs:466 early-return. If this contract is broken, fail loud.
            if (isLocked && movementAllowed)
                Debug.LogError($"[D-LOC G8b] T={currentTick} Locked-state but MovementAllowed=true — Phase 3 Lock gate failed");
#endif

            if (bootstrap != null)
            {
                bootstrap.DebugState.currentTick = currentTick;
                bootstrap.DebugState.movementAllowed = movementAllowed;
                bootstrap.DebugState.gateBlocked = !movementAllowed && !_computedStats.IsRoomBypassActive;
                bootstrap.DebugState.ownerInputLive = ownerInputLive;
                bootstrap.DebugState.steeringInput = steering;
                SyncModifierDebugState(currentTick);
            }

            BuddahPredictedInputData replicate = new BuddahPredictedInputData(steering, throttle, movementAllowed, ownerInputLive);
            replicate.LastConsumedImpulseId = 0u;
            replicate.LastConsumedTeleportId = 0u;
            replicate.LastConsumedModifierId = 0u;
            replicate.LastConsumedHandoffId = 0u;
            replicate.OwnerControlMask = 0;
            return replicate;
        }

        [Replicate]
        private void RunInputs(BuddahPredictedInputData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
#if BUDDAH_PREDICTION_PERF_PROBE
            using var markerScope = PerfProbeScope.Auto();
#endif
            if (!ShouldRunPrediction() || _predictionRigidbody == null)
                return;

            // Phase 4b — L7 late-bind for the CombatAdapter. ShouldRunPrediction()
            // returning true here proves we're past BuddahMovementModeSwitcher's
            // double-ApplyMode (Awake + OnEnable both fire ApplyMode → 4
            // [CommandBus]:ClearAll lines per spawn). MarkReady() runs exactly
            // once per motor instance; subsequent enqueues from CombatAdapter
            // will not be wiped by a second ClearAll. Per L7 rule (b).
            if (!_combatAdapterInitialized && bootstrap != null && bootstrap.CombatAdapter != null)
            {
                bootstrap.CombatAdapter.MarkReady();
                _combatAdapterInitialized = true;
            }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch = default;
            _shadowScratch = default;
#endif

            InitializePredictionRigidbody();
            uint currentTick = TimeManager != null ? TimeManager.LocalTick : data.GetTick();

            _ = data.LastConsumedImpulseId;
            _ = data.LastConsumedTeleportId;
            _ = data.LastConsumedModifierId;
            _ = data.LastConsumedHandoffId;
            _ = data.OwnerControlMask;

            RefreshLaunchState(currentTick);
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            ApplyResolvedMassMultiplier();
            _impulseConsumedThisTick = false;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _shadowPreTeleportHasPending = _hasPendingTeleportEvent;
            _shadowPreTeleportEvent = _pendingTeleportEvent;
            _shadowPreHandoffState = _handoffState;
            {
                BuddahPredictionTickContext earlyTickCtx = BuildTickContext(in data, Vector3.forward, 0f, 0f);
                BuddahTeleportStep.Run(in earlyTickCtx, in data, ref _shadowScratch);
                if (_shadowScratch.TeleportRan)
                    _shadowTeleportConsumedCount++;
                // V2b Step 1 Q4 / V4: BuddahImpulseStep early-shadow call retired (L19);
                // impulse axis no longer participates in D-LOC compare.
            }
#endif
            ConsumePendingTeleportEvent(currentTick);
            // Phase 6 — ConsumePendingLaunchHandoffEvent retired with the queue
            // infrastructure (Section 11.1 option a). Locked state is set externally
            // via EnterRaceStartLock + cleared by RefreshLaunchState's tick-tail.
            // Phase 4b V2b Step 1 / V4 — sole impulse drain path. NEW (CommandBus.ImpulseChannel
            // ConsumeReady) is rb-writing authority. OLD queue + LEG shadow compare retired in V4.
            ConsumePendingImpulseEvents_Authoritative(currentTick);
            RefreshLaunchState(currentTick);
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            // Phase 3c — snapshot _modifierState at the authoritative post-consume moment
            // (struct by value, independent of subsequent motor writes), then run the shadow
            // Resolve on it. Real-side ShadowComputedStats mirrors the motor's just-written
            // _computedStats so Shadow_CompareAndReport has a stable snapshot to compare
            // against (sidesteps any mid-tick reconcile-replay _computedStats overwrite).
            _shadowModifierStateSnapshot = _modifierState;
            _realScratch.ShadowComputedStats = _computedStats;
            _realScratch.ModifierRan = true;
            // Real-side handoff mirror: _handoffState after motor.cs RefreshLaunchState at
            // line above is the post-consume authoritative state. HandoffRan and the cursor
            // are set inside ConsumePendingLaunchHandoffEvent when consume actually fires.
            _realScratch.ShadowHandoffState = _handoffState;
            {
                BuddahPredictionTickContext modifierTickCtx = BuildTickContext(in data, Vector3.forward, 0f, 0f);
                BuddahModifierStep.Run(in modifierTickCtx, in data, ref _shadowScratch);
                if (_shadowScratch.ModifierRan)
                    _shadowModifierConsumedCount++;
                BuddahHandoffStep.Run(in modifierTickCtx, in data, ref _shadowScratch);
                if (_shadowScratch.HandoffRan)
                    _shadowHandoffConsumedCount++;
            }
#endif
            ApplyResolvedMassMultiplier();

            bool introControlActive = _introControlActive;
            bool externalControlActive = _externalKinematicControlActive;
            bool authoritativePending = IsPredictionAuthoritativeHandoffPending();
            if (ShouldRelinquishPredictionWriterDuringIntroOrPending(introControlActive, externalControlActive, authoritativePending, out string writerReason))
            {
                if (authoritativePending && !introControlActive && !externalControlActive)
                    SimulateZeroVelocityPredictionStep();
                else
                    _predictionRigidbody.ClearPendingForces();

                FinalizeImpulseDebugAfterSimulate();
                UpdateHandoffDebug(currentTick);
                UpdateReplicateDebug(data, state, $"writer-relinquished:{writerReason}");
                LogPredictionIntroWriterState(currentTick, true, writerReason, introControlActive, externalControlActive, authoritativePending);
                return;
            }

            LogPredictionIntroWriterState(currentTick, false, "prediction-active", introControlActive, externalControlActive, authoritativePending);

            if (!data.MovementAllowed)
            {
                _predictionRigidbody.ClearPendingForces();
                SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
                _predictionRigidbody.Simulate();
                FinalizeImpulseDebugAfterSimulate();
                UpdateReplicateDebug(data, state, "blocked");
                return;
            }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _shadowPreClampVelocity = rb != null ? rb.velocity : Vector3.zero;
#endif
            ClampPlanarSpeed(_computedStats.FinalMaxSpeed + (_computedStats.IsPushGraceActive ? config.PushExtraMaxSpeed : 0f));

            if (_computedStats.IsRooted)
            {
                _predictionRigidbody.ClearPendingForces();
                SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
                _predictionRigidbody.Simulate();
                FinalizeImpulseDebugAfterSimulate();
                UpdateReplicateDebug(data, state, "rooted");
                return;
            }

            Vector3 forwardDirection = transform.forward;
            forwardDirection.y = 0f;
            if (forwardDirection.sqrMagnitude < 0.0001f)
                forwardDirection = Vector3.forward;
            forwardDirection.Normalize();

            float resolvedThrottle = data.Throttle;
            float resolvedSteering = data.Steering * _computedStats.FinalSteeringSign;
            // Phase 6 — ApplyLaunchHandoffInputScaling (Inherit/Blend throttle*=BlendAlpha)
            // and ApplyLaunchInheritedVelocity (post-handoff velocity blend) retired.
            // Locked state gates input via MovementAllowed=false at line 466 early-return;
            // post-unlock path is untouched (auto-forward = throttle 1f intact).

            // Phase 4a Z2 (2026-04-19): apply IsSteeringSuppressed BEFORE Compute so
            // commanded turn torque is derived from the zeroed-steering value. Forward
            // force is unaffected by steering so the ordering swap is behavior-neutral.
            if (_computedStats.IsSteeringSuppressed)
                resolvedSteering = 0f;

            // Phase 4a Z2: migrate CommandedForwardForce + CommandedTurnTorque
            // computation to BuddahLocomotionStep.Compute so motor real path and
            // shadow step share the exact same body (parity-by-construction).
            // Decay branch below is RETAINED inline per Z2 hybrid scope —
            // phase-8-cleanup-queue.md Entry 3 tracks the deferred decay-branch
            // migration contingent on Phase 3a shadow scope extension.
            BuddahLocomotionStep.Compute(
                forwardDirection,
                resolvedThrottle,
                resolvedSteering,
                _computedStats,
                out Vector3 forwardForce,
                out float turnTorque);

            _predictionRigidbody.AddForce(forwardForce, ForceMode.Force);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch.CommandedForwardForce = forwardForce;
            _realScratch.LocomotionRan = true;
#endif

            if (Mathf.Abs(resolvedSteering) > 0.001f)
            {
                _predictionRigidbody.AddTorque(Vector3.up * turnTorque, ForceMode.Force);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
                _realScratch.CommandedTurnTorque = turnTorque;
#endif
            }
            else if (config.TurnDecayPerSecond > 0f)
            {
                Vector3 angularVelocity = rb.angularVelocity;
                angularVelocity.y = Mathf.MoveTowards(angularVelocity.y, 0f, config.TurnDecayPerSecond * (float)TimeManager.TickDelta);
                _predictionRigidbody.AngularVelocity(angularVelocity);
            }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            {
                BuddahPredictionTickContext tickCtx = BuildTickContext(in data, forwardDirection, resolvedThrottle, resolvedSteering);
                BuddahLocomotionStep.Run(in tickCtx, in data, ref _shadowScratch);
            }
#endif

            _predictionRigidbody.Simulate();
            FinalizeImpulseDebugAfterSimulate();
            UpdateHandoffDebug(currentTick);
            UpdateReplicateDebug(data, state, "active");
        }

        [Reconcile]
        private void ReconcileState(BuddahPredictedReconcileData data, Channel channel = Channel.Unreliable)
        {
            _reconcileCallbackCount++;
            if (_predictionRigidbody == null || data.RigidbodyState == null)
                return;

            Vector3 preReconcilePosition = rb != null ? rb.position : Vector3.zero;
            Vector3 preReconcileVelocity = rb != null ? rb.velocity : Vector3.zero;
            bool introControlled = data.IntroControlActive || data.ExternalKinematicControlActive;
            bool authoritativePending = IsPredictionAuthoritativeHandoffPending();
            bool skipOwnerIntroReconcile = ShouldSkipTransformReconcileDuringIntroOrPendingHandoff(data, authoritativePending, out string reconcileReason);
            if (!skipOwnerIntroReconcile)
                _predictionRigidbody.Reconcile(data.RigidbodyState);
            Vector3 postReconcilePosition = rb != null ? rb.position : Vector3.zero;
            Vector3 postReconcileVelocity = rb != null ? rb.velocity : Vector3.zero;
            LogPredictionIntroReconcileState(
                data.GetTick(),
                skipOwnerIntroReconcile,
                reconcileReason,
                introControlled,
                authoritativePending,
                Vector3.Distance(preReconcilePosition, postReconcilePosition),
                Vector3.Distance(preReconcileVelocity, postReconcileVelocity));

            if (bootstrap == null)
                return;

            _modifierState = data.ModifierState;
            _computedStats = data.ComputedStats;
            _handoffState = data.HandoffState;
            _introControlActive = data.IntroControlActive;
            _externalKinematicControlActive = data.ExternalKinematicControlActive;

            _ = data.PendingTeleport;
            _ = data.HasPendingTeleport;
            _ = data.LastConsumedTeleportId;
            _ = data.PendingHandoff;
            _ = data.HasPendingHandoff;
            _ = data.LastConsumedHandoffId;
            _ = data.AwaitingAuthoritativeLaunchHandoff;
            _ = data.LocalPreHandoffBypassUntilTick;

            if (rb != null && (IsOwner || IsServerInitialized))
                rb.isKinematic = _externalKinematicControlActive;
            bootstrap.DebugState.lastReconcileTick = data.GetTick();
            bootstrap.DebugState.planarSpeed = data.PlanarSpeed;
            bootstrap.DebugState.movementAllowed = data.MovementAllowed;
            bootstrap.DebugState.lastReconcilePositionDelta = Vector3.Distance(preReconcilePosition, postReconcilePosition);
            bootstrap.DebugState.lastReconcileVelocityDelta = Vector3.Distance(preReconcileVelocity, postReconcileVelocity);
            Vector3 prePlanarVelocity = preReconcileVelocity;
            prePlanarVelocity.y = 0f;
            Vector3 postPlanarVelocity = postReconcileVelocity;
            postPlanarVelocity.y = 0f;
            bootstrap.DebugState.lastReconcileVelocityPlanarDelta = Vector3.Distance(prePlanarVelocity, postPlanarVelocity);
            bootstrap.DebugState.lastReconcileVelocityVerticalDelta = Mathf.Abs(preReconcileVelocity.y - postReconcileVelocity.y);
            bootstrap.DebugState.reconcileHadCorrection =
                bootstrap.DebugState.lastReconcilePositionDelta > 0.001f ||
                bootstrap.DebugState.lastReconcileVelocityDelta > 0.001f;
            bootstrap.DebugState.reconcileSummary =
                $"tick={data.GetTick()} speed={data.PlanarSpeed:0.00} posDelta={bootstrap.DebugState.lastReconcilePositionDelta:0.000} velDelta={bootstrap.DebugState.lastReconcileVelocityDelta:0.000} " +
                $"planarVelDelta={bootstrap.DebugState.lastReconcileVelocityPlanarDelta:0.000} verticalVelDelta={bootstrap.DebugState.lastReconcileVelocityVerticalDelta:0.000}";
            SyncModifierDebugState(data.GetTick());
            UpdateHandoffDebug(data.GetTick());

            if (bootstrap.DebugSettings.dumpReconcile)
                bootstrap.LogVerbose($"reconcile tick={data.GetTick()} allowed={data.MovementAllowed} speed={data.PlanarSpeed:0.00}");

#if BUDDAH_SERGATE_DEBUG
            // Enable via ProjectSettings -> Scripting Define Symbols: BUDDAH_SERGATE_DEBUG.
            // Used in Phase 2/3 to observe reconcile-data mirroring from motor private state.
            Debug.Log($"[SerGate] T={data.GetTick()} isOwner={IsOwner} " +
                      $"preHoTick={data.LocalPreHandoffBypassUntilTick} " +
                      $"awaitHo={data.AwaitingAuthoritativeLaunchHandoff} " +
                      $"pendingTp={data.HasPendingTeleport} tpPos={data.PendingTeleport.TargetPosition}");
#endif
        }

        private bool ShouldRelinquishPredictionWriterDuringIntroOrPending(bool introControlActive, bool externalControlActive, bool authoritativePending, out string reason)
        {
            if (externalControlActive)
            {
                reason = "external-control";
                return true;
            }

            if (introControlActive)
            {
                reason = "intro-control";
                return true;
            }

            if (authoritativePending)
            {
                reason = "authoritative-handoff-pending";
                return true;
            }

            reason = "prediction-active";
            return false;
        }

        private void SimulateZeroVelocityPredictionStep()
        {
            _predictionRigidbody.ClearPendingForces();
            SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
            _predictionRigidbody.Simulate();
        }

        private void SetPredictionVelocitiesSafely(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (rb != null && rb.isKinematic)
                return;

            _predictionRigidbody.Velocity(linearVelocity);
            _predictionRigidbody.AngularVelocity(angularVelocity);
        }

        private bool ShouldSkipTransformReconcileDuringIntroOrPendingHandoff(BuddahPredictedReconcileData data, bool authoritativePending, out string reason)
        {
            if (!IsOwner)
            {
                reason = "not-owner";
                return false;
            }

            if (data.ExternalKinematicControlActive)
            {
                reason = "external-control";
                return true;
            }

            if (data.IntroControlActive)
            {
                reason = "intro-control";
                return true;
            }

            if (authoritativePending)
            {
                reason = "authoritative-handoff-pending";
                return true;
            }

            reason = "prediction-active";
            return false;
        }

        private bool IsPredictionAuthoritativeHandoffPending()
        {
            return IsAuthoritativeLaunchHandoffPending;
        }

        private void LogPredictionIntroWriterState(uint currentTick, bool relinquished, string reason, bool introControlActive, bool externalControlActive, bool authoritativePending)
        {
            if (_lastLoggedIntroWriterSuppressed == relinquished && string.Equals(_lastLoggedIntroWriterReason, reason, StringComparison.Ordinal))
                return;

            _lastLoggedIntroWriterSuppressed = relinquished;
            _lastLoggedIntroWriterReason = reason;
            bool isHostOwner = IsOwner && IsServerInitialized;
            Debug.Log(
                $"[PredictionIntro][Writer] tick={currentTick} relinquished={relinquished} why={reason} " +
                $"owner={IsOwner} hostOwner={isHostOwner} intro={introControlActive} external={externalControlActive} pending={authoritativePending}");
        }

        private void LogPredictionIntroReconcileState(uint tick, bool skipped, string reason, bool introControlled, bool authoritativePending, float positionDelta, float velocityDelta)
        {
            bool isHostOwner = IsOwner && IsServerInitialized;
            if (_lastLoggedIntroReconcileSkipped == skipped
                && _lastLoggedIntroReconcileControlled == introControlled
                && _lastLoggedIntroReconcileHostOwner == isHostOwner
                && _lastLoggedIntroReconcilePending == authoritativePending)
            {
                return;
            }

            _lastLoggedIntroReconcileSkipped = skipped;
            _lastLoggedIntroReconcileControlled = introControlled;
            _lastLoggedIntroReconcileHostOwner = isHostOwner;
            _lastLoggedIntroReconcilePending = authoritativePending;
            Debug.Log(
                $"[PredictionIntro][Reconcile] tick={tick} skipped={skipped} reason={reason} owner={IsOwner} hostOwner={isHostOwner} " +
                $"introControlled={introControlled} intro={_introControlActive} external={_externalKinematicControlActive} " +
                $"pending={authoritativePending} posDelta={positionDelta:0.000} velDelta={velocityDelta:0.000}");
        }

        private void UpdateReplicateDebug(BuddahPredictedInputData data, ReplicateState state, string status)
        {
            if (bootstrap == null)
                return;

            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;

            bootstrap.DebugState.lastReplicateTick = data.GetTick();
            bootstrap.DebugState.planarSpeed = planarVelocity.magnitude;
            bootstrap.DebugState.replicateSummary =
                $"tick={data.GetTick()} steer={data.Steering:0.00} throttle={data.Throttle:0.00} {status}";

            if (bootstrap.DebugSettings.dumpReplicate)
                bootstrap.LogVerbose(
                    $"replicate tick={data.GetTick()} state={state} steer={data.Steering:0.00} " +
                    $"throttle={data.Throttle:0.00} allowed={data.MovementAllowed}");
        }

        public void SetPredictionIntroControlActive(bool active)
        {
            _introControlActive = active;
            if (!active)
                RefreshLaunchState(TimeManager != null ? TimeManager.LocalTick : 0u);

            if (bootstrap != null)
            {
                bootstrap.DebugState.introControlActive = _introControlActive;
                bootstrap.LogVerbose($"intro control active={active}");
            }
        }

        public void SetPredictionExternalKinematicControlActive(bool active)
        {
            _externalKinematicControlActive = active;

            if (active)
            {
                _handoffState = default;
            }

            if (rb != null && (IsOwner || IsServerInitialized))
            {
                if (active)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = active;
                InitializePredictionRigidbody();
            }

            if (bootstrap != null)
            {
                bootstrap.DebugState.externalKinematicControlActive = _externalKinematicControlActive;
                bootstrap.LogVerbose($"external kinematic control active={active}");
            }
        }

        public bool TryApplyServerAuthoritativeTeleport(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            string reason,
            bool snapProgress,
            bool zeroLinearVelocity,
            bool zeroAngularVelocity,
            bool resetModifiers,
            bool resetImpulseQueue,
            bool resetPushGrace,
            bool rebaseTrails)
        {
            if (!IsServerInitialized || TimeManager == null)
                return false;

            BuddahPredictedTeleportEventData eventData = new(
                _nextTeleportEventId++,
                TimeManager.LocalTick,
                targetPosition,
                targetRotation,
                Mathf.Clamp01(targetProgress01),
                sourceType,
                snapProgress,
                zeroLinearVelocity,
                zeroAngularVelocity,
                resetModifiers,
                resetImpulseQueue,
                resetPushGrace,
                rebaseTrails);

            bool queuedOnServer = TryQueueTeleportEvent(eventData);
            QueueTeleportEventTargetRpc(
                Owner,
                eventData.EventId,
                eventData.EventTick,
                eventData.TargetPosition,
                eventData.TargetRotation,
                eventData.TargetProgress01,
                eventData.SourceType,
                eventData.SnapProgress,
                eventData.ZeroLinearVelocity,
                eventData.ZeroAngularVelocity,
                eventData.ResetModifiers,
                eventData.ResetImpulseQueue,
                eventData.ResetPushGrace,
                eventData.RebaseTrails);

            bootstrap?.LogVerbose(
                $"teleport authoritative create id={eventData.EventId} tick={eventData.EventTick} source={sourceType} reason={reason} " +
                $"pos={targetPosition} yaw={targetRotation.eulerAngles.y:0.0} progress={eventData.TargetProgress01:0.000} queuedServer={queuedOnServer}");
            return queuedOnServer;
        }

        // Phase 6 — Race-start lock entry point. Replaces the owner→server→owner
        // RPC chain (RequestAuthoritativeLaunchHandoffFromOwner / TryApplyServer
        // AuthoritativeLaunchHandoff / RequestLaunchHandoffServerRpc / QueueLaunch
        // HandoffTargetRpc / TryQueueLaunchHandoffEvent), which was deleted whole
        // per Section 11.1 option (a). Called by RoomStateManager._raceStartTick
        // SyncVar OnChange handler (Area 3) on each client; caller passes the
        // server-authoritative raceStartTick so all peers compute the same lock
        // window. snapshotPose is the spline endpoint pose (rb already snapped
        // there by spline driver per Phase 1 endpoint logic).
        public void EnterRaceStartLock(uint raceStartTick, Vector3 snapshotPosition, Quaternion snapshotRotation)
        {
            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            uint lockedDurationTicks = raceStartTick > currentTick
                ? raceStartTick - currentTick
                : 0u;
            _handoffState = new BuddahPredictedLaunchHandoffState
            {
                IsActive = lockedDurationTicks > 0u,
                EventTick = currentTick,
                StartTick = currentTick,
                LockedUntilTick = raceStartTick,
                CurrentState = lockedDurationTicks > 0u
                    ? BuddahPredictedLaunchState.Locked
                    : BuddahPredictedLaunchState.Normal,
                SnapshotPosition = snapshotPosition,
                SnapshotRotation = snapshotRotation,
                SnapshotVelocity = Vector3.zero,
                SnapshotAngularVelocity = Vector3.zero,
                SnapshotForward = snapshotRotation * Vector3.forward
            };
            Debug.Log($"[IntroHandoff][Phase6] EnterRaceStartLock currentTick={currentTick} raceStartTick={raceStartTick} lockedDurationTicks={lockedDurationTicks} pos={snapshotPosition}");
        }

        public bool RequestAuthoritativeTeleportFromOwner(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            string reason,
            bool snapProgress,
            bool zeroLinearVelocity,
            bool zeroAngularVelocity,
            bool resetModifiers,
            bool resetImpulseQueue,
            bool resetPushGrace,
            bool rebaseTrails)
        {
            if (IsServerInitialized)
            {
                return TryApplyServerAuthoritativeTeleport(
                    targetPosition,
                    targetRotation,
                    targetProgress01,
                    sourceType,
                    reason,
                    snapProgress,
                    zeroLinearVelocity,
                    zeroAngularVelocity,
                    resetModifiers,
                    resetImpulseQueue,
                    resetPushGrace,
                    rebaseTrails);
            }

            RequestTeleportServerRpc(
                targetPosition,
                targetRotation,
                targetProgress01,
                sourceType,
                reason,
                snapProgress,
                zeroLinearVelocity,
                zeroAngularVelocity,
                resetModifiers,
                resetImpulseQueue,
                resetPushGrace,
            rebaseTrails);
            return true;
        }

        // Phase 6 — RequestLaunchHandoffServerRpc retired (Section 11.1 option a).
        // Server-driven race-start broadcast now flows through RoomStateManager
        // SyncVars (_raceStartTick + _gameplayMovementUnlocked).

        [ServerRpc(RequireOwnership = true)]
        private void RequestTeleportServerRpc(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            string reason,
            bool snapProgress,
            bool zeroLinearVelocity,
            bool zeroAngularVelocity,
            bool resetModifiers,
            bool resetImpulseQueue,
            bool resetPushGrace,
            bool rebaseTrails)
        {
            TryApplyServerAuthoritativeTeleport(
                targetPosition,
                targetRotation,
                targetProgress01,
                sourceType,
                reason,
                snapProgress,
                zeroLinearVelocity,
                zeroAngularVelocity,
                resetModifiers,
                resetImpulseQueue,
                resetPushGrace,
                rebaseTrails);
        }

        // Phase 6 — QueueLaunchHandoffTargetRpc retired (Section 11.1 option a).

        [TargetRpc]
        private void QueueTeleportEventTargetRpc(
            NetworkConnection conn,
            uint eventId,
            uint eventTick,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            bool snapProgress,
            bool zeroLinearVelocity,
            bool zeroAngularVelocity,
            bool resetModifiers,
            bool resetImpulseQueue,
            bool resetPushGrace,
            bool rebaseTrails)
        {
            BuddahPredictedTeleportEventData eventData = new(
                eventId,
                eventTick,
                targetPosition,
                targetRotation,
                targetProgress01,
                sourceType,
                snapProgress,
                zeroLinearVelocity,
                zeroAngularVelocity,
                resetModifiers,
                resetImpulseQueue,
                resetPushGrace,
                rebaseTrails);

            TryQueueTeleportEvent(eventData);
        }

        public bool TryQueueTeleportEvent(BuddahPredictedTeleportEventData eventData)
        {
            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
                return false;

            if ((_hasPendingTeleportEvent && _pendingTeleportEvent.EventId == eventData.EventId) || _lastConsumedTeleportEventId == eventData.EventId)
            {
                bootstrap.LogVerbose($"teleport duplicate ignored id={eventData.EventId} source={eventData.SourceType} tick={eventData.EventTick}");
                return false;
            }

            _pendingTeleportEvent = eventData;
            _hasPendingTeleportEvent = true;
            UpdateTeleportDebugQueued(eventData);
            bootstrap.LogVerbose(
                $"teleport enqueued id={eventData.EventId} tick={eventData.EventTick} source={eventData.SourceType} " +
                $"pos={eventData.TargetPosition} yaw={eventData.TargetRotation.eulerAngles.y:0.0} progress={eventData.TargetProgress01:0.000}");
            return true;
        }

        // Phase 6 — TryQueueLaunchHandoffEvent retired (Section 11.1 option a).

        private void ClampPlanarSpeed(float maxSpeed)
        {
            Vector3 velocity = rb.velocity;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (planarVelocity.sqrMagnitude <= maxSpeed * maxSpeed)
                return;

            Vector3 clampedPlanar = planarVelocity.normalized * maxSpeed;
            Vector3 clampedVel = new Vector3(clampedPlanar.x, velocity.y, clampedPlanar.z);
            _predictionRigidbody.Velocity(clampedVel);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch.VelocityAfterClamp = clampedVel;
            _realScratch.ClampingApplied = true;
#endif
        }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
        private BuddahPredictionTickContext BuildTickContext(
            in BuddahPredictedInputData data,
            Vector3 forwardDirection,
            float resolvedThrottle,
            float resolvedSteering)
        {
            uint tick = TimeManager != null ? TimeManager.LocalTick : data.GetTick();
            float dt = TimeManager != null ? (float)TimeManager.TickDelta : Time.fixedDeltaTime;
            float pushExtra = config != null ? config.PushExtraMaxSpeed : 0f;
            float pushGraceRemaining = _modifierState.PushGraceUntilTick > tick
                ? (_modifierState.PushGraceUntilTick - tick) * dt
                : 0f;

            return new BuddahPredictionTickContext(
                rbVelocityPreTick: _shadowPreClampVelocity,
                rbMass: rb != null ? rb.mass : 1f,
                fixedDeltaTime: dt,
                tick: tick,
                forwardDirection: forwardDirection,
                resolvedThrottle: resolvedThrottle,
                resolvedSteering: resolvedSteering,
                computedStats: _computedStats,
                pushGraceExtraSpeed: pushExtra,
                pushGraceRemaining: pushGraceRemaining,
                hasPendingTeleportPreConsume: _shadowPreTeleportHasPending,
                pendingTeleportEventId: _shadowPreTeleportEvent.EventId,
                pendingTeleportEventTick: _shadowPreTeleportEvent.EventTick,
                teleportTargetPosition: _shadowPreTeleportEvent.TargetPosition,
                teleportTargetRotation: _shadowPreTeleportEvent.TargetRotation,
                teleportFlag_SnapProgress: _shadowPreTeleportEvent.SnapProgress,
                teleportFlag_ZeroLinearVelocity: _shadowPreTeleportEvent.ZeroLinearVelocity,
                teleportFlag_ZeroAngularVelocity: _shadowPreTeleportEvent.ZeroAngularVelocity,
                teleportFlag_ResetModifiers: _shadowPreTeleportEvent.ResetModifiers,
                teleportFlag_ResetImpulseQueue: _shadowPreTeleportEvent.ResetImpulseQueue,
                teleportFlag_ResetPushGrace: _shadowPreTeleportEvent.ResetPushGrace,
                teleportFlag_RebaseTrails: _shadowPreTeleportEvent.RebaseTrails,
                shadowModifierStateSnapshot: _shadowModifierStateSnapshot,
                config: config,
                shadowPreHandoffState: _shadowPreHandoffState);
        }

        private void Shadow_CompareAndReport()
        {
            bool anyRan = _realScratch.LocomotionRan || _shadowScratch.LocomotionRan
                          // Phase 4b V2b Step 1 — _shadowScratch.ImpulseRan dropped (Q4 amendment:
                          // early-shadow impulse step removed). _realScratch.ImpulseRan is now
                          // NEW-driven (channel drain authority).
                          || _realScratch.ImpulseRan
                          || _realScratch.TeleportRan || _shadowScratch.TeleportRan
                          || _realScratch.ModifierRan || _shadowScratch.ModifierRan
                          || _realScratch.HandoffRan || _shadowScratch.HandoffRan;
            if (!anyRan)
            {
                _dLocConsecutive = 0;
                _shadowSkipCompares++;
                if ((_shadowSkipCompares % 300) == 1)
                {
                    uint tickIdle = TimeManager != null ? TimeManager.LocalTick : 0u;
                    // V2b Step 1 Q4 / V4: impulse axis dropped from D-LOC HEARTBEAT (LEG axis retired).
                    Debug.Log($"[D-LOC HEARTBEAT] T={tickIdle} active-ticks={_shadowActiveCompares} skip-ticks={_shadowSkipCompares}\n  loc-div={_dLocLocomotionDivCount} tel-div={_dLocTeleportDivCount} mod-div={_dLocModifierDivCount} hof-div={_dLocHandoffDivCount}\n  tel-compared={_shadowTeleportConsumedCount} mod-compared={_shadowModifierConsumedCount} hof-compared={_shadowHandoffConsumedCount} rec-cb={_reconcileCallbackCount} (both sides idle)");
                    _dLocLocomotionDivCount = 0;
                    _dLocTeleportDivCount = 0;
                    _dLocModifierDivCount = 0;
                    _dLocHandoffDivCount = 0;
                }
                return;
            }

            _shadowActiveCompares++;
            if ((_shadowActiveCompares % 120) == 1)
            {
                uint tickHb = TimeManager != null ? TimeManager.LocalTick : 0u;
                Debug.Log($"[D-LOC HEARTBEAT] T={tickHb} active-ticks={_shadowActiveCompares} skip-ticks={_shadowSkipCompares}\n  loc-div={_dLocLocomotionDivCount} tel-div={_dLocTeleportDivCount} mod-div={_dLocModifierDivCount} hof-div={_dLocHandoffDivCount}\n  tel-compared={_shadowTeleportConsumedCount} mod-compared={_shadowModifierConsumedCount} hof-compared={_shadowHandoffConsumedCount} rec-cb={_reconcileCallbackCount}");
                _dLocLocomotionDivCount = 0;
                _dLocTeleportDivCount = 0;
                _dLocModifierDivCount = 0;
                _dLocHandoffDivCount = 0;
            }

            bool locDiverged = false;
            // Phase 4b V2b Step 1 — impDiverged removed (Q4 amendment: D-LOC impulse compare killed).
            bool telDiverged = false;
            bool modDiverged = false;
            bool hofDiverged = false;
            uint tick = TimeManager != null ? TimeManager.LocalTick : 0u;

            // Locomotion compare (3a).
            if (_realScratch.LocomotionRan != _shadowScratch.LocomotionRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} loc gate mismatch: realRan={_realScratch.LocomotionRan} shadowRan={_shadowScratch.LocomotionRan}");
                locDiverged = true;
            }
            else if (_realScratch.LocomotionRan)
            {
                if (_realScratch.ClampingApplied != _shadowScratch.ClampingApplied)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} clamp-gate mismatch: realClamp={_realScratch.ClampingApplied} shadowClamp={_shadowScratch.ClampingApplied}");
                    locDiverged = true;
                }
                else
                {
                    float fwdDelta = (_realScratch.CommandedForwardForce - _shadowScratch.CommandedForwardForce).magnitude;
                    float trnDelta = Mathf.Abs(_realScratch.CommandedTurnTorque - _shadowScratch.CommandedTurnTorque);
                    float velDelta = _realScratch.ClampingApplied
                        ? (_realScratch.VelocityAfterClamp - _shadowScratch.VelocityAfterClamp).magnitude
                        : 0f;

                    if (fwdDelta > 1e-4f || trnDelta > 1e-4f || velDelta > 1e-4f)
                    {
                        Debug.LogWarning($"[D-LOC] T={tick} fwd={fwdDelta:F6} trn={trnDelta:F6} vel={velDelta:F6} clamp={_realScratch.ClampingApplied}");
                        locDiverged = true;
                    }
                }
            }

            // V2b Step 1 Q4 + L19 + V4: D-LOC impulse compare retired. NEW (CommandBus.ImpulseChannel)
            // is sole impulse drain authority post-V4; no shadow comparison needed.

            // Teleport compare (3b) — ran flag + cursor + target pose + 7 flag reads.
            if (_realScratch.TeleportRan != _shadowScratch.TeleportRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} teleport-ran gate mismatch: real={_realScratch.TeleportRan} shadow={_shadowScratch.TeleportRan}");
                telDiverged = true;
            }
            else if (_realScratch.TeleportRan)
            {
                if (_realScratch.ShadowLastConsumedTeleportId != _shadowScratch.ShadowLastConsumedTeleportId)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-consume cursor mismatch: realId={_realScratch.ShadowLastConsumedTeleportId} shadowId={_shadowScratch.ShadowLastConsumedTeleportId}");
                    telDiverged = true;
                }

                float posDelta = (_realScratch.PostTeleportPosition - _shadowScratch.PostTeleportPosition).magnitude;
                if (posDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-position delta={posDelta:F6}");
                    telDiverged = true;
                }

                float rotDelta = Quaternion.Angle(_realScratch.PostTeleportRotation, _shadowScratch.PostTeleportRotation);
                if (rotDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-rotation angular-delta-deg={rotDelta:F6}");
                    telDiverged = true;
                }

                if (_realScratch.TeleportFlag_SnapProgress != _shadowScratch.TeleportFlag_SnapProgress)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=SnapProgress real={_realScratch.TeleportFlag_SnapProgress} shadow={_shadowScratch.TeleportFlag_SnapProgress}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ZeroLinearVelocity != _shadowScratch.TeleportFlag_ZeroLinearVelocity)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ZeroLinearVelocity real={_realScratch.TeleportFlag_ZeroLinearVelocity} shadow={_shadowScratch.TeleportFlag_ZeroLinearVelocity}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ZeroAngularVelocity != _shadowScratch.TeleportFlag_ZeroAngularVelocity)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ZeroAngularVelocity real={_realScratch.TeleportFlag_ZeroAngularVelocity} shadow={_shadowScratch.TeleportFlag_ZeroAngularVelocity}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetModifiers != _shadowScratch.TeleportFlag_ResetModifiers)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetModifiers real={_realScratch.TeleportFlag_ResetModifiers} shadow={_shadowScratch.TeleportFlag_ResetModifiers}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetImpulseQueue != _shadowScratch.TeleportFlag_ResetImpulseQueue)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetImpulseQueue real={_realScratch.TeleportFlag_ResetImpulseQueue} shadow={_shadowScratch.TeleportFlag_ResetImpulseQueue}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetPushGrace != _shadowScratch.TeleportFlag_ResetPushGrace)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetPushGrace real={_realScratch.TeleportFlag_ResetPushGrace} shadow={_shadowScratch.TeleportFlag_ResetPushGrace}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_RebaseTrails != _shadowScratch.TeleportFlag_RebaseTrails)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=RebaseTrails real={_realScratch.TeleportFlag_RebaseTrails} shadow={_shadowScratch.TeleportFlag_RebaseTrails}");
                    telDiverged = true;
                }
            }

            // Modifier compare (3c) — 12 ComputedStats fields (4 floats + 5 bools + 3 floats).
            // Real-side ModifierRan is set at the motor.cs:341 hook; shadow-side is set by
            // BuddahModifierStep.Run. Resolver is pure-static, so any delta > 1e-4 on floats
            // or any bool mismatch points to a divergent _modifierState input — NOT
            // resolver floating-point drift (C# single-thread Resolve is bit-deterministic).
            if (_realScratch.ModifierRan != _shadowScratch.ModifierRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} mod-ran gate mismatch: real={_realScratch.ModifierRan} shadow={_shadowScratch.ModifierRan}");
                modDiverged = true;
            }
            else if (_realScratch.ModifierRan)
            {
                BuddahPredictedMotorComputedStats realStats = _realScratch.ShadowComputedStats;
                BuddahPredictedMotorComputedStats shadowStats = _shadowScratch.ShadowComputedStats;

                float fwdDelta = Mathf.Abs(realStats.FinalForwardForce - shadowStats.FinalForwardForce);
                if (fwdDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalForwardForce real={realStats.FinalForwardForce:F6} shadow={shadowStats.FinalForwardForce:F6} delta={fwdDelta:F6}");
                    modDiverged = true;
                }

                float msDelta = Mathf.Abs(realStats.FinalMaxSpeed - shadowStats.FinalMaxSpeed);
                if (msDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalMaxSpeed real={realStats.FinalMaxSpeed:F6} shadow={shadowStats.FinalMaxSpeed:F6} delta={msDelta:F6}");
                    modDiverged = true;
                }

                float ttDelta = Mathf.Abs(realStats.FinalTurnTorque - shadowStats.FinalTurnTorque);
                if (ttDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalTurnTorque real={realStats.FinalTurnTorque:F6} shadow={shadowStats.FinalTurnTorque:F6} delta={ttDelta:F6}");
                    modDiverged = true;
                }

                float ssDelta = Mathf.Abs(realStats.FinalSteeringSign - shadowStats.FinalSteeringSign);
                if (ssDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalSteeringSign real={realStats.FinalSteeringSign:F6} shadow={shadowStats.FinalSteeringSign:F6} delta={ssDelta:F6}");
                    modDiverged = true;
                }

                float smDelta = Mathf.Abs(realStats.ScaleMultiplier - shadowStats.ScaleMultiplier);
                if (smDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleMultiplier real={realStats.ScaleMultiplier:F6} shadow={shadowStats.ScaleMultiplier:F6} delta={smDelta:F6}");
                    modDiverged = true;
                }

                float smmDelta = Mathf.Abs(realStats.ScaleMassMultiplier - shadowStats.ScaleMassMultiplier);
                if (smmDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleMassMultiplier real={realStats.ScaleMassMultiplier:F6} shadow={shadowStats.ScaleMassMultiplier:F6} delta={smmDelta:F6}");
                    modDiverged = true;
                }

                float sffDelta = Mathf.Abs(realStats.ScaleForwardForceMultiplier - shadowStats.ScaleForwardForceMultiplier);
                if (sffDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleForwardForceMultiplier real={realStats.ScaleForwardForceMultiplier:F6} shadow={shadowStats.ScaleForwardForceMultiplier:F6} delta={sffDelta:F6}");
                    modDiverged = true;
                }

                if (realStats.IsRooted != shadowStats.IsRooted)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsRooted real={realStats.IsRooted} shadow={shadowStats.IsRooted}");
                    modDiverged = true;
                }

                if (realStats.IsInvertTurnActive != shadowStats.IsInvertTurnActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsInvertTurnActive real={realStats.IsInvertTurnActive} shadow={shadowStats.IsInvertTurnActive}");
                    modDiverged = true;
                }

                if (realStats.IsPushGraceActive != shadowStats.IsPushGraceActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsPushGraceActive real={realStats.IsPushGraceActive} shadow={shadowStats.IsPushGraceActive}");
                    modDiverged = true;
                }

                if (realStats.IsSteeringSuppressed != shadowStats.IsSteeringSuppressed)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsSteeringSuppressed real={realStats.IsSteeringSuppressed} shadow={shadowStats.IsSteeringSuppressed}");
                    modDiverged = true;
                }

                if (realStats.IsRoomBypassActive != shadowStats.IsRoomBypassActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsRoomBypassActive real={realStats.IsRoomBypassActive} shadow={shadowStats.IsRoomBypassActive}");
                    modDiverged = true;
                }
            }

            // Handoff compare (3d) — 15 LaunchHandoffState fields. Real side mirrors
            // motor's _handoffState after the post-consume RefreshLaunchState; shadow
            // side re-runs Advance / ProjectForArrivalTick / FromData on snapshotted
            // pre-consume state via BuddahHandoffStep. Both paths call
            // BuddahPredictedLaunchHandoffResolver, so output is bit-identical by
            // construction — any delta > 1e-4 or flag mismatch points to divergent
            // input state (snapshot timing drift, serializer precision, etc.), NOT
            // resolver noise.
            if (_realScratch.HandoffRan != _shadowScratch.HandoffRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} hof-ran gate mismatch: real={_realScratch.HandoffRan} shadow={_shadowScratch.HandoffRan}");
                hofDiverged = true;
            }
            else if (_realScratch.HandoffRan
                     && _realScratch.ShadowLastConsumedHandoffId != _shadowScratch.ShadowLastConsumedHandoffId)
            {
                Debug.LogWarning($"[D-LOC] T={tick} hof-consume cursor mismatch: realId={_realScratch.ShadowLastConsumedHandoffId} shadowId={_shadowScratch.ShadowLastConsumedHandoffId}");
                hofDiverged = true;
            }

            {
                BuddahPredictedLaunchHandoffState realHof = _realScratch.ShadowHandoffState;
                BuddahPredictedLaunchHandoffState shadowHof = _shadowScratch.ShadowHandoffState;

                if (realHof.IsActive != shadowHof.IsActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-flag mismatch: flag=IsActive real={realHof.IsActive} shadow={shadowHof.IsActive}");
                    hofDiverged = true;
                }
                if (realHof.CurrentState != shadowHof.CurrentState)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-flag mismatch: flag=CurrentState real={realHof.CurrentState} shadow={shadowHof.CurrentState}");
                    hofDiverged = true;
                }
                if (realHof.EventId != shadowHof.EventId)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=EventId real={realHof.EventId} shadow={shadowHof.EventId}");
                    hofDiverged = true;
                }
                if (realHof.EventTick != shadowHof.EventTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=EventTick real={realHof.EventTick} shadow={shadowHof.EventTick}");
                    hofDiverged = true;
                }
                if (realHof.StartTick != shadowHof.StartTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=StartTick real={realHof.StartTick} shadow={shadowHof.StartTick}");
                    hofDiverged = true;
                }
                if (realHof.LockedUntilTick != shadowHof.LockedUntilTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=LockedUntilTick real={realHof.LockedUntilTick} shadow={shadowHof.LockedUntilTick}");
                    hofDiverged = true;
                }
                if (realHof.SuppressSteeringUntilTick != shadowHof.SuppressSteeringUntilTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=SuppressSteeringUntilTick real={realHof.SuppressSteeringUntilTick} shadow={shadowHof.SuppressSteeringUntilTick}");
                    hofDiverged = true;
                }
                if (realHof.RoomBypassUntilTick != shadowHof.RoomBypassUntilTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=RoomBypassUntilTick real={realHof.RoomBypassUntilTick} shadow={shadowHof.RoomBypassUntilTick}");
                    hofDiverged = true;
                }

                // Phase 6 — BlendAlpha field-delta compare retired with the
                // Inherit/Blend state machine (per L19: dead compares deleted,
                // not gated, so they cannot resurface as silent dead code).

                float posDelta = (realHof.SnapshotPosition - shadowHof.SnapshotPosition).magnitude;
                if (posDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotPosition magnitude={posDelta:F6}");
                    hofDiverged = true;
                }

                // Normalize default(Quaternion)=(0,0,0,0) to Quaternion.identity before Angle compare.
                // Quaternion.Angle(default, default) returns 180° (dot=0, 2*acos(0)=180°), producing
                // a false-positive divergence when both sides hold uninitialized quaternion (typical
                // pre-consume steady state with IsActive=false). Both default quaternions ARE equal;
                // only the compare tool misreports. Normalizing preserves divergence detection for
                // any non-zero rotation. See Docs/lessons-log.md L15.
                Quaternion realRot = realHof.SnapshotRotation;
                Quaternion shadowRot = shadowHof.SnapshotRotation;
                if (realRot.x == 0f && realRot.y == 0f && realRot.z == 0f && realRot.w == 0f)
                    realRot = Quaternion.identity;
                if (shadowRot.x == 0f && shadowRot.y == 0f && shadowRot.z == 0f && shadowRot.w == 0f)
                    shadowRot = Quaternion.identity;
                float rotDelta = Quaternion.Angle(realRot, shadowRot);
                if (rotDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotRotation angle-deg={rotDelta:F6}");
                    hofDiverged = true;
                }

                float velDelta = (realHof.SnapshotVelocity - shadowHof.SnapshotVelocity).magnitude;
                if (velDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotVelocity magnitude={velDelta:F6}");
                    hofDiverged = true;
                }

                float angDelta = (realHof.SnapshotAngularVelocity - shadowHof.SnapshotAngularVelocity).magnitude;
                if (angDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotAngularVelocity magnitude={angDelta:F6}");
                    hofDiverged = true;
                }

                float fwdDelta = (realHof.SnapshotForward - shadowHof.SnapshotForward).magnitude;
                if (fwdDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotForward magnitude={fwdDelta:F6}");
                    hofDiverged = true;
                }
            }

            if (locDiverged) _dLocLocomotionDivCount++;
            if (telDiverged) _dLocTeleportDivCount++;
            if (modDiverged) _dLocModifierDivCount++;
            if (hofDiverged) _dLocHandoffDivCount++;

            bool anyDiverged = locDiverged || telDiverged || modDiverged || hofDiverged;
            if (anyDiverged)
                _dLocConsecutive++;
            else
                _dLocConsecutive = 0;

            if (_dLocConsecutive >= 60)
            {
                // Phase-agnostic FATAL: lists the diverged categories present this
                // window instead of naming a specific phase. Easier to maintain across
                // future shadow phases without per-phase text churn.
                string categories;
                {
                    var sb = new StringBuilder();
                    if (locDiverged) sb.Append(sb.Length > 0 ? ",loc" : "loc");
                    // V2b Step 1 — imp axis dropped from D-LOC composite FATAL string (Q4 amendment).
                    if (telDiverged) sb.Append(sb.Length > 0 ? ",tel" : "tel");
                    if (modDiverged) sb.Append(sb.Length > 0 ? ",mod" : "mod");
                    if (hofDiverged) sb.Append(sb.Length > 0 ? ",hof" : "hof");
                    categories = sb.ToString();
                }
                Debug.LogError($"[D-LOC FATAL] T={tick} shadow formula divergence: {{{categories}}}");
                _dLocConsecutive = 0;
            }
        }
#endif

        public bool TryApplyModifierCommand(BuddahPredictedModifierCommandData command)
        {
            if (TimeManager == null || config == null)
                return false;

            uint currentTick = TimeManager.LocalTick;
            uint ToTicks(float durationSeconds)
            {
                if (durationSeconds <= 0f)
                    return currentTick;

                uint durationTicks = (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, (float)TimeManager.TickDelta));
                return currentTick + durationTicks;
            }

            switch (command.Type)
            {
                case BuddahPredictedModifierCommandType.Acceleration:
                    _modifierState.AccelUntilTick = Math.Max(_modifierState.AccelUntilTick, ToTicks(command.ValueC));
                    _modifierState.AccelExtraForwardForce = command.ValueA;
                    _modifierState.AccelExtraMaxSpeed = command.ValueB;
                    LogModifier($"modifier accel enter source={command.Source} ff={command.ValueA:0.00} ms={command.ValueB:0.00} until={_modifierState.AccelUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.RootThenAcceleration:
                    _modifierState.RootUntilTick = Math.Max(_modifierState.RootUntilTick, ToTicks(command.ValueA));
                    _modifierState.PostRootAccelUntilTick = Math.Max(_modifierState.PostRootAccelUntilTick, ToTicks(command.ValueA + command.ValueD));
                    _modifierState.PostRootAccelExtraForwardForce = command.ValueB;
                    _modifierState.PostRootAccelExtraMaxSpeed = command.ValueC;
                    LogModifier($"modifier rootThenAccel enter source={command.Source} rootUntil={_modifierState.RootUntilTick} postUntil={_modifierState.PostRootAccelUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.InvertTurn:
                    _modifierState.InvertTurnUntilTick = Math.Max(_modifierState.InvertTurnUntilTick, ToTicks(command.ValueA));
                    LogModifier($"modifier invert enter source={command.Source} until={_modifierState.InvertTurnUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.Scale:
                    _modifierState.ScaleUntilTick = Math.Max(_modifierState.ScaleUntilTick, ToTicks(command.ValueB));
                    _modifierState.ScaleMultiplier = Mathf.Max(0.1f, command.ValueA);
                    _modifierState.ScaleMassMultiplier = Mathf.Max(0.1f, command.ValueC);
                    _modifierState.ScaleForwardForceMultiplier = Mathf.Max(0.1f, command.ValueD);
                    LogModifier($"modifier scale enter source={command.Source} scale={_modifierState.ScaleMultiplier:0.00} massMul={_modifierState.ScaleMassMultiplier:0.00} ffMul={_modifierState.ScaleForwardForceMultiplier:0.00} until={_modifierState.ScaleUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.PushGrace:
                    _modifierState.PushGraceUntilTick = Math.Max(_modifierState.PushGraceUntilTick, ToTicks(command.ValueA));
                    LogModifier($"modifier pushGrace enter source={command.Source} until={_modifierState.PushGraceUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.SteeringSuppression:
                    _modifierState.SuppressSteeringUntilTick = Math.Max(_modifierState.SuppressSteeringUntilTick, ToTicks(command.ValueA));
                    LogModifier($"modifier steeringSuppress enter source={command.Source} until={_modifierState.SuppressSteeringUntilTick}");
                    break;
                case BuddahPredictedModifierCommandType.RoomBypass:
                    _modifierState.RoomBypassUntilTick = Math.Max(_modifierState.RoomBypassUntilTick, ToTicks(command.ValueA));
                    LogModifier($"modifier roomBypass enter source={command.Source} until={_modifierState.RoomBypassUntilTick}");
                    break;
                default:
                    return false;
            }

            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            SyncModifierDebugState(currentTick);
            return true;
        }

        // Phase 4b V2b Step 1 — NEW = rb-writing authority. Drains
        // bootstrap.CommandBus.ImpulseChannel (tick-stamped, ConsumeReady-gated) and applies the
        // 4 gameplay actions per entry: rb.WakeUp + _predictionRigidbody.AddForce/AddTorque +
        // ApplyPushGraceFromImpulse + _impulseConsumedThisTick = true. PredictionRigidbody
        // integrity (Methodology Rule 7): all force/torque writes go through _predictionRigidbody,
        // NEVER direct rb. Replay-safe by construction (channel removes consumed entries; reconcile
        // replay sees empty pending → no re-apply; reconcile state captures impulse-applied rb).
        // _realScratch counter writes preserved as the sole remaining D-LOC impulse-axis signal
        // (post-V4: LEG axis retired; impulse correctness verified by visual smoke + spawn-window
        // L7 probe + counter sanity rather than dual-drain compare).
        private void ConsumePendingImpulseEvents_Authoritative(uint currentTick)
        {
            if (_predictionRigidbody == null || rb == null)
                return;
            if (bootstrap == null || bootstrap.CommandBus == null)
                return;

            var channel = bootstrap.CommandBus.ImpulseChannel;
            if (channel == null)
                return;

            channel.ConsumeReady(currentTick, ConsumeImpulseAuthoritativeEntry);

            if (bootstrap != null)
            {
                bootstrap.DebugState.pendingImpulseCount = channel.Count;
                bootstrap.DebugState.pendingImpulseSummary = channel.BuildPendingSummary();
            }
        }

        // Helper for ConsumePendingImpulseEvents_Authoritative's ConsumeReady callback.
        // Method form (vs lambda) to avoid `in` parameter capture quirks and keep the
        // hot path allocation-free across reconcile replays.
        private bool ConsumeImpulseAuthoritativeEntry(in NewBuddah.PredictionV2.Events.BuddahPredictionEventChannel<NewBuddah.PredictionV2.Events.Payloads.ImpulseCmd>.Entry entry)
        {
            NewBuddah.PredictionV2.Events.Payloads.ImpulseCmd cmd = entry.Payload;

            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;

            bootstrap.DebugState.preImpulseSpeed = planarVelocity.magnitude;
            bootstrap.DebugState.lastImpulseEventId = entry.Id;
            bootstrap.DebugState.lastImpulseEventTick = entry.EventTick;
            bootstrap.DebugState.lastImpulseSourceType = ((BuddahPredictedImpulseSourceType)cmd.SourceType).ToString();
            bootstrap.DebugState.lastImpulseVector = cmd.LinearImpulse;
            bootstrap.DebugState.lastImpulseTurnTorque = cmd.TurnImpulse;
            bootstrap.DebugState.lastImpulseConsumed = true;
            _impulseConsumedThisTick = true;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch.ImpulseRan = true;
            _realScratch.ImpulseDrainCount++;
            if (entry.Id > _realScratch.ShadowLastConsumedImpulseId)
                _realScratch.ShadowLastConsumedImpulseId = entry.Id;
#endif

            rb.WakeUp();
            if (cmd.LinearImpulse.sqrMagnitude > 0f)
                _predictionRigidbody.AddForce(cmd.LinearImpulse, ForceMode.Impulse);
            if (Mathf.Abs(cmd.TurnImpulse) > 0.001f)
                _predictionRigidbody.AddTorque(Vector3.up * cmd.TurnImpulse, ForceMode.Impulse);

            // Synthesize ALL fields per Q1 risk note. ApplyPushGraceFromImpulse currently reads
            // EventId + SourceType for log only, but populate every field to forward-proof against
            // future helper signature growth without re-touching this synthesis site.
            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            BuddahPredictedImpulseEventData syntheticEventData = new BuddahPredictedImpulseEventData(
                eventId: entry.Id,
                eventTick: entry.EventTick,
                impulse: cmd.LinearImpulse,
                turnTorqueImpulse: cmd.TurnImpulse,
                sourceType: (BuddahPredictedImpulseSourceType)cmd.SourceType,
                sourceObjectId: cmd.SourceObjectId);
            ApplyPushGraceFromImpulse(currentTick, syntheticEventData);

            bootstrap.LogVerbose(
                $"impulse consumed (NEW authoritative) entryId={entry.Id} logicalId={entry.LogicalId} source={(BuddahPredictedImpulseSourceType)cmd.SourceType} tick={currentTick} " +
                $"impulse={cmd.LinearImpulse} torque={cmd.TurnImpulse:0.00}");
            return true;
        }

        private void ConsumePendingTeleportEvent(uint currentTick)
        {
            if (!_hasPendingTeleportEvent || rb == null)
                return;

            if (_pendingTeleportEvent.EventTick > currentTick)
                return;

            BuddahPredictedTeleportEventData eventData = _pendingTeleportEvent;
            _hasPendingTeleportEvent = false;
            _lastConsumedTeleportEventId = eventData.EventId;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch.TeleportRan = true;
            _realScratch.ShadowLastConsumedTeleportId = eventData.EventId;
            _realScratch.PostTeleportPosition = eventData.TargetPosition;
            _realScratch.PostTeleportRotation = eventData.TargetRotation;
            _realScratch.TeleportFlag_SnapProgress = eventData.SnapProgress;
            _realScratch.TeleportFlag_ZeroLinearVelocity = eventData.ZeroLinearVelocity;
            _realScratch.TeleportFlag_ZeroAngularVelocity = eventData.ZeroAngularVelocity;
            _realScratch.TeleportFlag_ResetModifiers = eventData.ResetModifiers;
            _realScratch.TeleportFlag_ResetImpulseQueue = eventData.ResetImpulseQueue;
            _realScratch.TeleportFlag_ResetPushGrace = eventData.ResetPushGrace;
            _realScratch.TeleportFlag_RebaseTrails = eventData.RebaseTrails;
#endif

            Vector3 prePosition = rb.position;
            Vector3 preVelocity = rb.velocity;
            float preAngularSpeed = rb.angularVelocity.magnitude;

            if (bootstrap != null)
            {
                bootstrap.DebugState.preTeleportPosition = prePosition;
                bootstrap.DebugState.preTeleportSpeed = new Vector3(preVelocity.x, 0f, preVelocity.z).magnitude;
                bootstrap.DebugState.preTeleportAngularSpeed = preAngularSpeed;
            }

            if (eventData.ResetModifiers)
            {
                _modifierState = default;
                _handoffState = default;
                if (bootstrap != null)
                    bootstrap.DebugState.modifiersClearedByTeleport = true;
            }

            if (eventData.ResetImpulseQueue)
            {
                bootstrap?.CommandBus?.ImpulseChannel?.Clear();
                if (bootstrap != null)
                    bootstrap.DebugState.impulseQueueClearedByTeleport = true;
            }

            if (eventData.ResetPushGrace)
            {
                _modifierState.PushGraceUntilTick = 0u;
                if (bootstrap != null)
                    bootstrap.DebugState.pushGraceClearedByTeleport = true;
            }

            _predictionRigidbody.ClearPendingForces();
            if (eventData.ZeroLinearVelocity)
            {
                rb.velocity = Vector3.zero;
                _predictionRigidbody.Velocity(Vector3.zero);
            }

            if (eventData.ZeroAngularVelocity)
            {
                rb.angularVelocity = Vector3.zero;
                _predictionRigidbody.AngularVelocity(Vector3.zero);
            }

            if (eventData.RebaseTrails)
                NotifyTeleportTrailRebases();

            rb.position = eventData.TargetPosition;
            rb.rotation = eventData.TargetRotation;
            rb.Sleep();
            rb.WakeUp();
            InitializePredictionRigidbody();

            if (eventData.SnapProgress && _splineProgressTracker != null)
            {
                _splineProgressTracker.SnapToTrackProgress(eventData.TargetProgress01);
                if (bootstrap != null)
                    bootstrap.DebugState.progressSnappedByTeleport = true;
            }

            if (_skillExecutor != null && IsOwner)
                _skillExecutor.ResetActiveSkillEffectsForOwner();

            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            SyncModifierDebugState(currentTick);
            UpdateTeleportDebugConsumed(eventData, rb.position, rb.velocity, rb.angularVelocity);
            bootstrap?.LogVerbose(
                $"teleport consumed id={eventData.EventId} source={eventData.SourceType} tick={currentTick} pos={rb.position} " +
                $"yaw={rb.rotation.eulerAngles.y:0.0} progress={eventData.TargetProgress01:0.000}");
        }

        // Phase 6 — ConsumePendingLaunchHandoffEvent retired with the queue
        // (Section 11.1 option a). Locked state is set externally via
        // EnterRaceStartLock; cleared by RefreshLaunchState's tick-tail when
        // currentTick passes LockedUntilTick.
        //
        // ApplyLaunchHandoffInputScaling (Inherit/Blend throttle*=BlendAlpha)
        // and ApplyLaunchInheritedVelocity (post-handoff velocity blend) also
        // retired — Locked state gates input via MovementAllowed=false at
        // motor.cs:466 early-return; post-unlock = auto-forward (throttle=1f).

        // Thin wrapper over BuddahPredictedLaunchHandoffResolver.Advance.
        // Motor owns the verbose-log side effect (resolver stays pure).
        private void RefreshLaunchState(uint currentTick)
        {
            BuddahPredictedLaunchHandoffState advanced = BuddahPredictedLaunchHandoffResolver.Advance(_handoffState, currentTick);
            if (bootstrap != null && _handoffState.IsActive && _handoffState.CurrentState != advanced.CurrentState)
                bootstrap.LogVerbose($"handoff state transition {_handoffState.CurrentState} -> {advanced.CurrentState} tick={currentTick} id={_handoffState.EventId}");
            _handoffState = advanced;
        }

        private void ApplyPushGraceFromImpulse(uint currentTick, BuddahPredictedImpulseEventData eventData)
        {
            uint untilTick = SecondsToTick(config.PushGraceSeconds, currentTick);
            _modifierState.PushGraceUntilTick = Math.Max(_modifierState.PushGraceUntilTick, untilTick);
            bootstrap?.LogVerbose(
                $"pushGrace applied eventId={eventData.EventId} source={eventData.SourceType} until={_modifierState.PushGraceUntilTick}");
        }

        private void FinalizeImpulseDebugAfterSimulate()
        {
            if (bootstrap == null || rb == null || !_impulseConsumedThisTick)
                return;

            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;
            bootstrap.DebugState.postImpulseSpeed = planarVelocity.magnitude;
            _impulseConsumedThisTick = false;
        }

        private uint SecondsToTick(float durationSeconds, uint currentTick)
        {
            if (TimeManager == null || durationSeconds <= 0f)
                return currentTick;

            uint durationTicks = (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, (float)TimeManager.TickDelta));
            return currentTick + durationTicks;
        }

        private uint SecondsToDurationTicks(float durationSeconds)
        {
            if (TimeManager == null || durationSeconds <= 0f)
                return 0u;

            return (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, (float)TimeManager.TickDelta));
        }

        // Phase 6 — IsLocalPreHandoffBypassActive retired (Section 11.1 option a).

        private void LogModifier(string message)
        {
            bootstrap?.LogVerbose(message);
        }

        private void ApplyResolvedMassMultiplier()
        {
            if (rb == null)
                return;

            float baseMass = Mathf.Max(0.0001f, _baseMass);
            float multiplier = Mathf.Max(0.1f, _computedStats.ScaleMassMultiplier);
            rb.mass = baseMass * multiplier;
        }

        private void NotifyTeleportTrailRebases()
        {
            PlayerBlackCurtainTrail[] blackCurtainTrails = GetComponentsInChildren<PlayerBlackCurtainTrail>(true);
            for (int i = 0; i < blackCurtainTrails.Length; i++)
            {
                if (blackCurtainTrails[i] != null)
                    blackCurtainTrails[i].NotifyTeleportRebase();
            }

            PlayerAccelerationTrail[] accelerationTrails = GetComponentsInChildren<PlayerAccelerationTrail>(true);
            for (int i = 0; i < accelerationTrails.Length; i++)
            {
                if (accelerationTrails[i] != null)
                    accelerationTrails[i].NotifyTeleportRebase();
            }

            if (bootstrap != null)
                bootstrap.DebugState.trailRebasedByTeleport = true;
        }

        private void UpdateTeleportDebugQueued(BuddahPredictedTeleportEventData eventData)
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.lastTeleportEventId = eventData.EventId;
            bootstrap.DebugState.lastTeleportEventTick = eventData.EventTick;
            bootstrap.DebugState.lastTeleportSourceType = eventData.SourceType.ToString();
            bootstrap.DebugState.lastTeleportTargetPosition = eventData.TargetPosition;
            bootstrap.DebugState.lastTeleportTargetYaw = eventData.TargetRotation.eulerAngles.y;
            bootstrap.DebugState.lastTeleportTargetProgress01 = eventData.TargetProgress01;
            bootstrap.DebugState.lastTeleportConsumed = false;
            bootstrap.DebugState.modifiersClearedByTeleport = false;
            bootstrap.DebugState.impulseQueueClearedByTeleport = false;
            bootstrap.DebugState.pushGraceClearedByTeleport = false;
            bootstrap.DebugState.progressSnappedByTeleport = false;
            bootstrap.DebugState.trailRebasedByTeleport = false;
        }

        private void UpdateTeleportDebugConsumed(BuddahPredictedTeleportEventData eventData, Vector3 postPosition, Vector3 postVelocity, Vector3 postAngularVelocity)
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.lastTeleportEventId = eventData.EventId;
            bootstrap.DebugState.lastTeleportEventTick = eventData.EventTick;
            bootstrap.DebugState.lastTeleportSourceType = eventData.SourceType.ToString();
            bootstrap.DebugState.lastTeleportTargetPosition = eventData.TargetPosition;
            bootstrap.DebugState.lastTeleportTargetYaw = eventData.TargetRotation.eulerAngles.y;
            bootstrap.DebugState.lastTeleportTargetProgress01 = eventData.TargetProgress01;
            bootstrap.DebugState.lastTeleportConsumed = true;
            bootstrap.DebugState.postTeleportPosition = postPosition;
            bootstrap.DebugState.postTeleportSpeed = new Vector3(postVelocity.x, 0f, postVelocity.z).magnitude;
            bootstrap.DebugState.postTeleportAngularSpeed = postAngularVelocity.magnitude;
        }

        // Phase 6 — UpdateQueuedHandoffDebug retired (queue retired with RPC chain).
        // UpdateConsumedHandoffDebug also retired (no consume callback in new design).

        private void UpdateHandoffDebug(uint currentTick)
        {
            if (bootstrap == null)
                return;

            RefreshLaunchState(currentTick);
            bootstrap.DebugState.handoffActive = _handoffState.IsActive;
            bootstrap.DebugState.launchState = _handoffState.CurrentState.ToString();
            bootstrap.DebugState.handoffStartTick = _handoffState.StartTick;
            bootstrap.DebugState.handoffLockedUntilTick = _handoffState.LockedUntilTick;
            bootstrap.DebugState.handoffSuppressSteeringActive = _handoffState.SuppressSteeringUntilTick > currentTick;
            bootstrap.DebugState.handoffRoomBypassActive = _handoffState.RoomBypassUntilTick > currentTick;
            bootstrap.DebugState.introControlActive = _introControlActive;
            bootstrap.DebugState.externalKinematicControlActive = _externalKinematicControlActive;
        }

        private void SyncModifierDebugState(uint tick)
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.rooted = _computedStats.IsRooted;
            bootstrap.DebugState.invertTurnActive = _computedStats.IsInvertTurnActive;
            bootstrap.DebugState.pushGraceActive = _computedStats.IsPushGraceActive;
            bootstrap.DebugState.suppressSteeringActive = _computedStats.IsSteeringSuppressed;
            bootstrap.DebugState.roomBypassActive = _computedStats.IsRoomBypassActive;
            bootstrap.DebugState.scaleMultiplier = _computedStats.ScaleMultiplier;
            bootstrap.DebugState.finalForwardForce = _computedStats.FinalForwardForce;
            bootstrap.DebugState.finalMaxSpeed = _computedStats.FinalMaxSpeed;
            bootstrap.DebugState.finalTurnTorque = _computedStats.FinalTurnTorque;
            bootstrap.DebugState.finalSteeringSign = _computedStats.FinalSteeringSign;
            bootstrap.DebugState.rootUntilTick = _modifierState.RootUntilTick;
            bootstrap.DebugState.accelUntilTick = _modifierState.AccelUntilTick;
            bootstrap.DebugState.postRootAccelUntilTick = _modifierState.PostRootAccelUntilTick;
            bootstrap.DebugState.scaleUntilTick = _modifierState.ScaleUntilTick;
            bootstrap.DebugState.invertTurnUntilTick = _modifierState.InvertTurnUntilTick;
            bootstrap.DebugState.pushGraceUntilTick = _modifierState.PushGraceUntilTick;
            bootstrap.DebugState.suppressSteeringUntilTick = _modifierState.SuppressSteeringUntilTick;
            bootstrap.DebugState.roomBypassUntilTick = _modifierState.RoomBypassUntilTick;
            bootstrap.DebugState.activeModifiers = BuildModifierSummary(tick);
            var impulseChannelDbg = bootstrap.CommandBus != null ? bootstrap.CommandBus.ImpulseChannel : null;
            bootstrap.DebugState.pendingImpulseCount = impulseChannelDbg != null ? impulseChannelDbg.Count : 0;
            bootstrap.DebugState.pendingImpulseSummary = impulseChannelDbg != null ? impulseChannelDbg.BuildPendingSummary() : "none";
            UpdateHandoffDebug(tick);
        }

        private string BuildModifierSummary(uint tick)
        {
            StringBuilder sb = new StringBuilder();

            void Append(string label, bool active)
            {
                if (!active)
                    return;

                if (sb.Length > 0)
                    sb.Append(", ");

                sb.Append(label);
            }

            Append("root", _modifierState.RootUntilTick > tick);
            Append("accel", _modifierState.AccelUntilTick > tick);
            Append("postRootAccel", _modifierState.PostRootAccelUntilTick > tick);
            Append("scale", _modifierState.ScaleUntilTick > tick);
            Append("invert", _modifierState.InvertTurnUntilTick > tick);
            Append("pushGrace", _modifierState.PushGraceUntilTick > tick);
            Append("suppress", _modifierState.SuppressSteeringUntilTick > tick);
            Append("roomBypass", _modifierState.RoomBypassUntilTick > tick);
            Append("handoff", _handoffState.IsActive);

            return sb.Length > 0 ? sb.ToString() : "none";
        }
    }
}
