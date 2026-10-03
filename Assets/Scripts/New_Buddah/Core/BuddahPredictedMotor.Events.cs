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
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_PERF_PROBE
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
using UnityEngine;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;

namespace NewBuddah.PredictionV2.Core
{
    public partial class BuddahPredictedMotor
    {
        public void SetPredictionIntroControlActive(bool active)
        {
            if (active && !_introControlActive) PresentationRevision++;
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
            if (active != _externalKinematicControlActive) PresentationRevision++;
            _externalKinematicControlActive = active;

            if (active)
            {
                _handoffState = default;
                _awaitingAuthoritativeLaunchHandoff = false;
                _localPreHandoffBypassUntilTick = 0u;
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
            if (Owner.IsValid) QueueTeleportEventTargetRpc(
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

        public bool RequestAuthoritativeLaunchHandoffFromOwner(
            LaunchHandoffSnapshot snapshot,
            float inheritDurationSeconds,
            float blendDurationSeconds,
            float bypassRoomStateSeconds,
            float suppressTurnInputSeconds,
            bool clearAngularVelocity,
            int debugSequenceId,
            bool enableDebugLogs)
        {
            bootstrap?.LogVerbose(
                $"[HandoffDebug] owner request start isServer={IsServerInitialized} isOwner={IsOwner} " +
                $"awaiting={_awaitingAuthoritativeLaunchHandoff} pos={snapshot.Position} speed={snapshot.Velocity.magnitude:0.00} seq={debugSequenceId}");
            if (IsServerInitialized)
            {
                bootstrap?.LogVerbose("[HandoffDebug] owner request resolved locally on server without releasing intro/external control early.");
                return TryApplyServerAuthoritativeLaunchHandoff(
                    snapshot,
                    inheritDurationSeconds,
                    blendDurationSeconds,
                    bypassRoomStateSeconds,
                    suppressTurnInputSeconds,
                    clearAngularVelocity,
                    debugSequenceId,
                    enableDebugLogs);
            }

            uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            _awaitingAuthoritativeLaunchHandoff = true;
            _localPreHandoffBypassUntilTick = Math.Max(
                _localPreHandoffBypassUntilTick,
                SecondsToTick(Mathf.Max(bypassRoomStateSeconds, 0.5f), currentTick));
            GameLog.Verbose($"[IntroHandoff][Prediction] Owner requested authoritative handoff seq={debugSequenceId} tick={currentTick} bypassUntil={_localPreHandoffBypassUntilTick}");
            bootstrap?.LogVerbose(
                $"[HandoffDebug] owner request sending ServerRpc tick={currentTick} bypassUntil={_localPreHandoffBypassUntilTick} seq={debugSequenceId}");
            RequestLaunchHandoffServerRpc(
                snapshot.Position,
                snapshot.Rotation,
                snapshot.Velocity,
                clearAngularVelocity ? Vector3.zero : snapshot.AngularVelocity,
                snapshot.Forward,
                inheritDurationSeconds,
                blendDurationSeconds,
                bypassRoomStateSeconds,
                suppressTurnInputSeconds,
                currentTick,
                debugSequenceId,
                enableDebugLogs);
            return true;
        }

        public bool TryApplyServerAuthoritativeLaunchHandoff(
            LaunchHandoffSnapshot snapshot,
            float inheritDurationSeconds,
            float blendDurationSeconds,
            float bypassRoomStateSeconds,
            float suppressTurnInputSeconds,
            bool clearAngularVelocity,
            int debugSequenceId,
            bool enableDebugLogs,
            uint ownerTickAtRequest = 0u)
        {
            if (!IsServerInitialized || TimeManager == null)
                return false;

            // Server queues with its own LocalTick so ConsumePendingLaunchHandoffEvent fires with
            // staleTicks=0 and does not project the snapshot forward. The RPC to the remote owner
            // uses an owner-anchored tick instead, because server.LocalTick can be hundreds of ticks
            // ahead of client.LocalTick (the host's tick counter keeps running from before the
            // client joined), and a single shared StartTick would either freeze the owner or make
            // the server over-project its Buddah hundreds of meters ahead of the snapshot.
            uint serverStartTick = TimeManager.LocalTick;
            uint clientStartTick = (ownerTickAtRequest > 0u)
                ? ownerTickAtRequest + HandoffOwnerTickTravelBufferTicks
                : serverStartTick;

            BuddahPredictedLaunchHandoffData eventData = new(
                _nextLaunchHandoffEventId++,
                serverStartTick,
                snapshot.Position,
                snapshot.Rotation,
                snapshot.Velocity,
                clearAngularVelocity ? Vector3.zero : snapshot.AngularVelocity,
                snapshot.Forward,
                SecondsToDurationTicks(inheritDurationSeconds),
                SecondsToDurationTicks(blendDurationSeconds),
                SecondsToDurationTicks(suppressTurnInputSeconds),
                SecondsToDurationTicks(bypassRoomStateSeconds),
                debugSequenceId,
                enableDebugLogs);

            bool queuedOnServer = TryQueueLaunchHandoffEvent(eventData);
            if (Owner.IsValid) QueueLaunchHandoffTargetRpc(
                Owner,
                eventData.EventId,
                clientStartTick,
                eventData.SnapshotPosition,
                eventData.SnapshotRotation,
                eventData.SnapshotVelocity,
                eventData.SnapshotAngularVelocity,
                eventData.SnapshotForward,
                eventData.InheritDurationTicks,
                eventData.BlendDurationTicks,
                eventData.SuppressSteeringDurationTicks,
                eventData.RoomBypassDurationTicks,
                eventData.DebugSequenceId,
                eventData.EnableDebugLogs);

            GameLog.Verbose($"[IntroHandoff][Server] Created authoritative handoff eventId={eventData.EventId} seq={debugSequenceId} serverStartTick={eventData.StartTick} clientStartTick={clientStartTick} ownerTickAtRequest={ownerTickAtRequest} queuedServer={queuedOnServer}");
            bootstrap?.LogVerbose(
                $"[HandoffDebug] server authoritative create owner={(Owner != null ? Owner.ClientId : -1)} " +
                $"eventId={eventData.EventId} tick={eventData.StartTick} queuedServer={queuedOnServer} seq={debugSequenceId}");
            bootstrap?.LogVerbose(
                $"handoff authoritative create id={eventData.EventId} tick={eventData.StartTick} seq={debugSequenceId} queuedServer={queuedOnServer} " +
                $"inherit={eventData.InheritDurationTicks} blend={eventData.BlendDurationTicks} pos={snapshot.Position} speed={snapshot.Velocity.magnitude:0.00}");
            return queuedOnServer;
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

        public bool TryQueueLaunchHandoffEvent(BuddahPredictedLaunchHandoffData eventData)
        {
            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
                return false;

            if ((_hasPendingLaunchHandoffEvent && _pendingLaunchHandoffEvent.EventId == eventData.EventId) || _lastConsumedLaunchHandoffEventId == eventData.EventId)
            {
                bootstrap.LogVerbose($"handoff duplicate ignored id={eventData.EventId} tick={eventData.StartTick}");
                return false;
            }

            _pendingLaunchHandoffEvent = eventData;
            _hasPendingLaunchHandoffEvent = true;
            UpdateQueuedHandoffDebug(eventData);
            bootstrap.LogVerbose(
                $"[HandoffDebug] event queued locally eventId={eventData.EventId} startTick={eventData.StartTick} " +
                $"awaiting={_awaitingAuthoritativeLaunchHandoff} speed={eventData.SnapshotVelocity.magnitude:0.00}");
            bootstrap.LogVerbose(
                $"handoff enqueued id={eventData.EventId} tick={eventData.StartTick} inherit={eventData.InheritDurationTicks} " +
                $"blend={eventData.BlendDurationTicks} suppress={eventData.SuppressSteeringDurationTicks} bypass={eventData.RoomBypassDurationTicks}");
            return true;
        }

        public bool TryApplyModifierCommand(BuddahPredictedModifierCommandData command)
        {
            if (TimeManager == null || config == null)
                return false;

            uint currentTick = TimeManager.LocalTick;
            uint ToTicks(float durationSeconds)
            {
                if (durationSeconds <= 0f)
                    return currentTick;

                uint durationTicks = BuddahTickMath.DurationToTicks(durationSeconds, (float)TimeManager.TickDelta);
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

            // Impulse channels retain canonical server ticks on both peers. LocalTick is
            // unsynchronized on a pure client and can delay old hits until long after impact.
            var prediction = PredictionManager;
            uint eventClockTick = BuddahTickMath.ImpulseEventClock(IsServerInitialized,
                prediction != null && prediction.IsReconciling, currentTick,
                TimeManager != null ? TimeManager.Tick : currentTick,
                prediction != null ? prediction.ServerReplayTick : currentTick);
            channel.ConsumeReady(eventClockTick, ConsumeImpulseAuthoritativeEntry);

            if (bootstrap != null)
            {
                bootstrap.DebugState.pendingImpulseCount = channel.Count;
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
            PresentationRevision++;
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
                _hasPendingLaunchHandoffEvent = false;
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

        // Solo GO is queued and consumed before its first actual Unity physics step.
        // RunInputs still owns input, modifiers, skills and subsequent motor ticks.
        internal bool ConsumeSoloLaunchBeforePhysics()
        {
            if (!Time.inFixedTimeStep || !ShouldRunPrediction() || !BuddahGo.Match.RacerAuthority.HasLocalControl(NetworkObject) || !IsServerInitialized
                || !BuddahGo.Match.MatchRules.Current.IsSolo
                || !_hasPendingLaunchHandoffEvent || TimeManager == null)
                return false;
            uint tick = TimeManager.LocalTick;
            uint before = _lastConsumedLaunchHandoffEventId;
            ConsumePendingLaunchHandoffEvent(tick);
            if (_lastConsumedLaunchHandoffEventId == before)
                return false;
            // Consume resets the body with Sleep/Wake. The normal motor tick restores
            // inherited velocity later in RunInputs; this pre-physics path must restore
            // that same consumed snapshot now, before the first Unity integration.
            SetPredictionVelocitiesSafely(_handoffState.SnapshotVelocity, _handoffState.SnapshotAngularVelocity);
            _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, tick);
            // The handoff cannot bypass a live movement restriction before the next motor tick.
            if (_computedStats.IsRooted || !(_movementGateBridge.IsMovementAllowed(gameObject)
                || _computedStats.IsRoomBypassActive))
            {
                SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
            }
            return !_hasPendingLaunchHandoffEvent;
        }

        private void ConsumePendingLaunchHandoffEvent(uint currentTick)
        {
            if (!_hasPendingLaunchHandoffEvent || rb == null)
                return;

            if (_pendingLaunchHandoffEvent.StartTick > currentTick)
                return;

            BuddahPredictedLaunchHandoffData eventData = _pendingLaunchHandoffEvent;
            _hasPendingLaunchHandoffEvent = false;
            _awaitingAuthoritativeLaunchHandoff = false;
            _localPreHandoffBypassUntilTick = currentTick;
            uint preAdjustStartTick = eventData.StartTick;
            float tickDeltaSeconds = TimeManager != null ? (float)TimeManager.TickDelta : 0f;
            eventData = BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick(eventData, currentTick, tickDeltaSeconds);
            if (bootstrap != null && eventData.StartTick != preAdjustStartTick)
            {
                uint staleTicks = currentTick - preAdjustStartTick;
                bootstrap.LogVerbose(
                    $"[HandoffDebug] stale handoff adjusted eventId={eventData.EventId} staleTicks={staleTicks} " +
                    $"oldStart={preAdjustStartTick} newStart={currentTick} projectedPos={eventData.SnapshotPosition}");
            }
            bootstrap?.LogVerbose(
                $"[HandoffDebug] consuming queued handoff eventId={eventData.EventId} currentTick={currentTick} startTick={eventData.StartTick} " +
                $"speed={eventData.SnapshotVelocity.magnitude:0.00}");
            _lastConsumedLaunchHandoffEventId = eventData.EventId;
            _handoffState = BuddahPredictedLaunchHandoffState.FromData(eventData);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch.HandoffRan = true;
            _realScratch.ShadowLastConsumedHandoffId = eventData.EventId;
#endif

            Vector3 preVelocity = rb.velocity;
            if (bootstrap != null)
                bootstrap.DebugState.preHandoffSpeed = new Vector3(preVelocity.x, 0f, preVelocity.z).magnitude;

            _introControlActive = false;
            _externalKinematicControlActive = false;
            rb.isKinematic = false;
            _predictionRigidbody.ClearPendingForces();
            rb.position = eventData.SnapshotPosition;
            rb.rotation = eventData.SnapshotRotation;
            rb.velocity = eventData.SnapshotVelocity;
            rb.angularVelocity = eventData.SnapshotAngularVelocity;
            rb.Sleep();
            rb.WakeUp();
            InitializePredictionRigidbody();
            _splineProgressTracker?.SnapToWorldPosition(eventData.SnapshotPosition);

            if (eventData.SuppressSteeringDurationTicks > 0u)
                _modifierState.SuppressSteeringUntilTick = Math.Max(_modifierState.SuppressSteeringUntilTick, _handoffState.SuppressSteeringUntilTick);
            if (eventData.RoomBypassDurationTicks > 0u)
                _modifierState.RoomBypassUntilTick = Math.Max(_modifierState.RoomBypassUntilTick, _handoffState.RoomBypassUntilTick);

            RefreshLaunchState(currentTick);
            UpdateConsumedHandoffDebug(currentTick, eventData);
            GameLog.Verbose($"[IntroHandoff][Prediction] Handoff consumed eventId={eventData.EventId} tick={currentTick} seq={eventData.DebugSequenceId} launchState={_handoffState.CurrentState}");
            if (IsOwner && eventData.DebugSequenceId >= 0)
                RoomStateManager.Instance?.ReportLocalGameplayLive(eventData.DebugSequenceId);
            bootstrap?.LogVerbose(
                $"handoff entered state={_handoffState.CurrentState} id={eventData.EventId} tick={currentTick} " +
                $"speed={eventData.SnapshotVelocity.magnitude:0.00} suppressUntil={_handoffState.SuppressSteeringUntilTick} bypassUntil={_handoffState.RoomBypassUntilTick}");
        }

        // Phase 3d — thin wrapper over BuddahPredictedLaunchHandoffResolver.Advance.
        // Motor owns the verbose-log side effect (resolver stays pure). The in-place
        // previousState capture is preserved by reading _handoffState.CurrentState
        // BEFORE the assignment below. Behavior-neutral vs the prior in-place impl.
        private void RefreshLaunchState(uint currentTick)
        {
            BuddahPredictedLaunchHandoffState advanced = BuddahPredictedLaunchHandoffResolver.Advance(_handoffState, currentTick);
            if (bootstrap != null && _handoffState.IsActive && _handoffState.CurrentState != advanced.CurrentState)
                bootstrap.LogVerbose($"handoff state transition {_handoffState.CurrentState} -> {advanced.CurrentState} tick={currentTick} id={_handoffState.EventId}");
            _handoffState = advanced;
        }

        private void ApplyLaunchHandoffInputScaling(uint currentTick, ref float throttle, ref float steering)
        {
            RefreshLaunchState(currentTick);
            if (!_handoffState.IsActive && _handoffState.CurrentState == BuddahPredictedLaunchState.Normal)
                return;

            switch (_handoffState.CurrentState)
            {
                case BuddahPredictedLaunchState.Inherit:
                    throttle = 0f;
                    steering = 0f;
                    break;
                case BuddahPredictedLaunchState.Blend:
                    throttle *= _handoffState.BlendAlpha;
                    steering *= _handoffState.BlendAlpha;
                    break;
            }
        }

        private void ApplyLaunchInheritedVelocity(uint currentTick)
        {
            if (rb == null || _predictionRigidbody == null)
                return;

            RefreshLaunchState(currentTick);
            if (_handoffState.CurrentState == BuddahPredictedLaunchState.Normal)
                return;

            Vector3 currentVelocity = rb.velocity;
            Vector3 inheritedPlanar = new Vector3(_handoffState.SnapshotVelocity.x, 0f, _handoffState.SnapshotVelocity.z);
            Vector3 currentPlanar = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float blend01 = _handoffState.CurrentState == BuddahPredictedLaunchState.Inherit ? 0f : _handoffState.BlendAlpha;
            Vector3 planar = Vector3.Lerp(inheritedPlanar, currentPlanar, Mathf.Clamp01(blend01));
            SetPredictionVelocitiesSafely(new Vector3(planar.x, currentVelocity.y, planar.z), Vector3.zero);

            if (bootstrap != null)
                bootstrap.DebugState.postHandoffSpeed = planar.magnitude;
        }

        private void ApplyPushGraceFromImpulse(uint currentTick, BuddahPredictedImpulseEventData eventData)
        {
            uint untilTick = SecondsToTick(config.PushGraceSeconds, currentTick);
            _modifierState.PushGraceUntilTick = Math.Max(_modifierState.PushGraceUntilTick, untilTick);
            bootstrap?.LogVerbose(
                $"pushGrace applied eventId={eventData.EventId} source={eventData.SourceType} until={_modifierState.PushGraceUntilTick}");
        }

        private uint SecondsToTick(float durationSeconds, uint currentTick)
        {
            if (TimeManager == null || durationSeconds <= 0f)
                return currentTick;

            uint durationTicks = BuddahTickMath.DurationToTicks(durationSeconds, (float)TimeManager.TickDelta);
            return currentTick + durationTicks;
        }

        private uint SecondsToDurationTicks(float durationSeconds)
        {
            if (TimeManager == null || durationSeconds <= 0f)
                return 0u;

            return BuddahTickMath.DurationToTicks(durationSeconds, (float)TimeManager.TickDelta);
        }

        private bool IsLocalPreHandoffBypassActive(uint currentTick)
        {
            return _awaitingAuthoritativeLaunchHandoff && _localPreHandoffBypassUntilTick > currentTick;
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


    }
}
