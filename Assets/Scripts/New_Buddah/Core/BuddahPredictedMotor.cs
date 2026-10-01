using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using System;
using System.Text;
using NewBuddah.PredictionV2.Debugging;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Integration;
using NewBuddah.PredictionV2.Simulation;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
using System.Collections.Generic;
#endif
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_PERF_PROBE
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
    public partial class BuddahPredictedMotor : TickNetworkBehaviour
    {
        // Small buffer ahead of the owner's last known tick so the TargetRpc has time to arrive
        // before the owner reaches StartTick. Keeps Consume firing the instant the event queues.
        private const uint HandoffOwnerTickTravelBufferTicks = 2u;

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
        private uint _nextLaunchHandoffEventId = 1u;
        private bool _impulseConsumedThisTick;
        private bool _hasPendingTeleportEvent;
        private bool _hasPendingLaunchHandoffEvent;
        private BuddahPredictedTeleportEventData _pendingTeleportEvent;
        private BuddahPredictedLaunchHandoffData _pendingLaunchHandoffEvent;
        private uint _lastConsumedTeleportEventId;
        private uint _lastConsumedLaunchHandoffEventId;
        private BuddahPredictedLaunchHandoffState _handoffState;
        private bool _introControlActive;
        private bool _externalKinematicControlActive;
        private bool _awaitingAuthoritativeLaunchHandoff;
        private uint _localPreHandoffBypassUntilTick;
        private bool _lastLoggedIntroWriterSuppressed;
        private string _lastLoggedIntroWriterReason;
        private bool _lastLoggedIntroReconcileSkipped;
        private bool _lastLoggedIntroReconcileControlled;
        private bool _lastLoggedIntroReconcileHostOwner;
        private bool _lastLoggedIntroReconcilePending;
        private SplineProgressTracker _splineProgressTracker;
        private SkillExecutor _skillExecutor;
        private float _baseMass = 1f;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_PERF_PROBE
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

        // L7 latch tracker. Flipped to true on the first RunInputs tick where
        // ShouldRunPrediction passes — guarantees BuddahMovementModeSwitcher's
        // double-ApplyMode + its 4 [CommandBus]:ClearAll lines have all landed
        // before the adapter's MarkReady() fires (lessons-log L7 rule b).
        private bool _combatAdapterInitialized;

        public bool IsLaunchHandoffActive => _handoffState.IsActive || _externalKinematicControlActive || _introControlActive;
        public float CurrentScaleMultiplier => _computedStats.ScaleMultiplier > 0f ? _computedStats.ScaleMultiplier : 1f;
        public bool IsPredictionIntroControlActive => _introControlActive;
        public bool IsPredictionExternalKinematicControlActive => _externalKinematicControlActive;
        public bool IsAuthoritativeLaunchHandoffPending => _awaitingAuthoritativeLaunchHandoff || _hasPendingLaunchHandoffEvent;
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

            LogSpectatorStepProbe();

            if (!IsServerInitialized)
                return;

            CreateReconcile();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ValidatePredictionInvariants();
        }

        // Prediction replay only integrates physics when FishNet owns the physics step. These are
        // configuration invariants, not runtime state; a violation means reconcile replays are no-ops.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidatePredictionInvariants()
        {
            if (TimeManager == null || rb == null || bootstrap == null || !bootstrap.IsPredictionModeActive())
                return;

            if (TimeManager.PhysicsMode != PhysicsMode.TimeManager)
                Debug.LogError($"[BuddahPredictionV2] invariant: TimeManager.PhysicsMode={TimeManager.PhysicsMode}; client-side prediction requires PhysicsMode.TimeManager (NetworkManager > TimeManager).", this);
            if (rb.interpolation != RigidbodyInterpolation.None)
                Debug.LogError($"[BuddahPredictionV2] invariant: Rigidbody.interpolation={rb.interpolation}; predicted rigidbody must use None.", this);
            if (!Mathf.Approximately(Time.fixedDeltaTime, (float)TimeManager.TickDelta))
                Debug.LogError($"[BuddahPredictionV2] invariant: Time.fixedDeltaTime={Time.fixedDeltaTime} != TickDelta={TimeManager.TickDelta}.", this);
            if (NetworkObject != null && NetworkObject.GetGraphicalObject() == null)
                Debug.LogError("[BuddahPredictionV2] invariant: NetworkObject has no graphical object; tick smoothing is not configured.", this);
        }

        public override void CreateReconcile()
        {
            if (!ShouldRunPrediction() || rb == null || _predictionRigidbody == null)
                return;

            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;
            bool movementAllowed = _movementGateBridge.IsMovementAllowed(gameObject)
                                   || _computedStats.IsRoomBypassActive
                                   || IsLocalPreHandoffBypassActive(currentTick);

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
            bool preHandoffBypassActive = IsLocalPreHandoffBypassActive(currentTick);
            bool movementAllowed = _movementGateBridge.IsMovementAllowed(gameObject)
                                   || _computedStats.IsRoomBypassActive
                                   || preHandoffBypassActive;
            float steering = movementAllowed ? (_ownerInputBridge.ReadSteering() * config.TurnInputMultiplier) : 0f;
            float throttle = movementAllowed ? 1f : 0f;
            bool ownerInputLive = IsOwner && movementAllowed;

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
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_PERF_PROBE
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
            _shadowPreHandoffHasPending = _hasPendingLaunchHandoffEvent;
            _shadowPreHandoffEvent = _pendingLaunchHandoffEvent;
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
            ConsumePendingLaunchHandoffEvent(currentTick);
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
                UpdateReplicateDebug(data, state, "writer-relinquished", writerReason);
                LogPredictionIntroWriterState(currentTick, true, writerReason, introControlActive, externalControlActive, authoritativePending);
                return;
            }

            LogPredictionIntroWriterState(currentTick, false, "prediction-active", introControlActive, externalControlActive, authoritativePending);

            if (!data.MovementAllowed)
            {
                CompleteStoppedPredictionStep(data, state, "blocked");
                return;
            }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _shadowPreClampVelocity = rb != null ? rb.velocity : Vector3.zero;
#endif
            ClampPlanarSpeed(_computedStats.FinalMaxSpeed + (_computedStats.IsPushGraceActive ? config.PushExtraMaxSpeed : 0f));

            if (_computedStats.IsRooted)
            {
                CompleteStoppedPredictionStep(data, state, "rooted");
                return;
            }

            Vector3 forwardDirection = transform.forward;
            forwardDirection.y = 0f;
            if (forwardDirection.sqrMagnitude < 0.0001f)
                forwardDirection = Vector3.forward;
            forwardDirection.Normalize();

            float resolvedThrottle = data.Throttle;
            float resolvedSteering = data.Steering * _computedStats.FinalSteeringSign;
            ApplyLaunchHandoffInputScaling(currentTick, ref resolvedThrottle, ref resolvedSteering);
            ApplyLaunchInheritedVelocity(currentTick);

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
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _reconcileCallbackCount++;
#endif
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
            _handoffState = data.HandoffState;
            uint stateTick = data.GetTick();
            // Use the tick pair belonging to this authoritative snapshot. The live estimated
            // server clock can jump during timing updates; it must not move snapshot deadlines.
            // Only working copies are translated; wire/history states retain server timestamps.
            stateTick = BuddahTickMath.ReconcileToLocal(ref _modifierState, ref _handoffState, stateTick,
                IsServerInitialized, IsOwner, PredictionManager.ServerStateTick, PredictionManager.ClientStateTick);
            _computedStats = data.ComputedStats;
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
            LogReconcileDeltaProbe(data, skipOwnerIntroReconcile, reconcileReason,
                bootstrap.DebugState.lastReconcilePositionDelta, bootstrap.DebugState.lastReconcileVelocityDelta);
            Vector3 prePlanarVelocity = preReconcileVelocity;
            prePlanarVelocity.y = 0f;
            Vector3 postPlanarVelocity = postReconcileVelocity;
            postPlanarVelocity.y = 0f;
            bootstrap.DebugState.lastReconcileVelocityPlanarDelta = Vector3.Distance(prePlanarVelocity, postPlanarVelocity);
            bootstrap.DebugState.lastReconcileVelocityVerticalDelta = Mathf.Abs(preReconcileVelocity.y - postReconcileVelocity.y);
            bootstrap.DebugState.reconcileHadCorrection =
                bootstrap.DebugState.lastReconcilePositionDelta > 0.001f ||
                bootstrap.DebugState.lastReconcileVelocityDelta > 0.001f;
            _consoleLogSnapshot.CaptureReconcile(data.GetTick(), data.PlanarSpeed,
                bootstrap.DebugState.lastReconcilePositionDelta, bootstrap.DebugState.lastReconcileVelocityDelta,
                bootstrap.DebugState.lastReconcileVelocityPlanarDelta, bootstrap.DebugState.lastReconcileVelocityVerticalDelta);
            SyncModifierDebugState(stateTick);
            UpdateHandoffDebug(stateTick);

            if (bootstrap.DebugSettings.dumpReconcile)
                bootstrap.LogVerbose($"reconcile tick={data.GetTick()} allowed={data.MovementAllowed} speed={data.PlanarSpeed:0.00}");

#if BUDDAH_SERGATE_DEBUG
            // Enable via ProjectSettings -> Scripting Define Symbols: BUDDAH_SERGATE_DEBUG.
            // Used in Phase 2/3 to observe reconcile-data mirroring from motor private state.
            GameLog.Verbose($"[SerGate] T={data.GetTick()} isOwner={IsOwner} " +
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

        private void CompleteStoppedPredictionStep(BuddahPredictedInputData data, ReplicateState state, string status)
        {
            _predictionRigidbody.ClearPendingForces();
            SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
            _predictionRigidbody.Simulate();
            FinalizeImpulseDebugAfterSimulate();
            UpdateReplicateDebug(data, state, status);
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


    }
}
