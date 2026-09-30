using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using System;
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
    public partial class BuddahPredictedMotor
    {
        private void LogPredictionIntroWriterState(uint currentTick, bool relinquished, string reason, bool introControlActive, bool externalControlActive, bool authoritativePending)
        {
            if (_lastLoggedIntroWriterSuppressed == relinquished && string.Equals(_lastLoggedIntroWriterReason, reason, StringComparison.Ordinal))
                return;

            _lastLoggedIntroWriterSuppressed = relinquished;
            _lastLoggedIntroWriterReason = reason;
            bool isHostOwner = IsOwner && IsServerInitialized;
            GameLog.Verbose(
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
            GameLog.Verbose(
                $"[PredictionIntro][Reconcile] tick={tick} skipped={skipped} reason={reason} owner={IsOwner} hostOwner={isHostOwner} " +
                $"introControlled={introControlled} intro={_introControlActive} external={_externalKinematicControlActive} " +
                $"pending={authoritativePending} posDelta={positionDelta:0.000} velDelta={velocityDelta:0.000}");
        }

        private BuddahPredictionLogSnapshot _consoleLogSnapshot;

        // Called only when the console consumer samples. Capture methods never format strings.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void RefreshConsoleDebugSummaries()
        {
            if (bootstrap == null)
                return;
            bootstrap.DebugState.replicateSummary = _consoleLogSnapshot.BuildReplicateSummary();
            bootstrap.DebugState.reconcileSummary = _consoleLogSnapshot.BuildReconcileSummary();
        }

        private void UpdateReplicateDebug(BuddahPredictedInputData data, ReplicateState state, string status, string writerReason = null)
        {
            if (bootstrap == null)
                return;

            Vector3 planarVelocity = rb.velocity;
            planarVelocity.y = 0f;

            bootstrap.DebugState.lastReplicateTick = data.GetTick();
            bootstrap.DebugState.planarSpeed = planarVelocity.magnitude;
            _consoleLogSnapshot.CaptureReplicate(data.GetTick(), data.Steering, data.Throttle, status, writerReason);

            if (bootstrap.DebugSettings.dumpReplicate)
                bootstrap.LogVerbose(
                    $"replicate tick={data.GetTick()} state={state} steer={data.Steering:0.00} " +
                    $"throttle={data.Throttle:0.00} allowed={data.MovementAllowed}");
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

        private void LogModifier(string message)
        {
            bootstrap?.LogVerbose(message);
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

        private void UpdateQueuedHandoffDebug(BuddahPredictedLaunchHandoffData eventData)
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.lastHandoffEventId = eventData.EventId;
            bootstrap.DebugState.lastHandoffEventTick = eventData.StartTick;
            bootstrap.DebugState.handoffStartTick = eventData.StartTick;
            bootstrap.DebugState.handoffInheritEndTick = eventData.StartTick + eventData.InheritDurationTicks;
            bootstrap.DebugState.handoffBlendEndTick = eventData.StartTick + eventData.InheritDurationTicks + eventData.BlendDurationTicks;
            bootstrap.DebugState.handoffSnapshotPosition = eventData.SnapshotPosition;
            bootstrap.DebugState.handoffSnapshotYaw = eventData.SnapshotRotation.eulerAngles.y;
            bootstrap.DebugState.handoffSnapshotSpeed = new Vector3(eventData.SnapshotVelocity.x, 0f, eventData.SnapshotVelocity.z).magnitude;
            bootstrap.DebugState.handoffSnapshotAngularSpeed = eventData.SnapshotAngularVelocity.magnitude;
        }

        private void UpdateConsumedHandoffDebug(uint currentTick, BuddahPredictedLaunchHandoffData eventData)
        {
            if (bootstrap == null)
                return;

            bootstrap.DebugState.lastHandoffEventId = eventData.EventId;
            bootstrap.DebugState.lastHandoffEventTick = currentTick;
            bootstrap.DebugState.handoffStartTick = _handoffState.StartTick;
            bootstrap.DebugState.handoffInheritEndTick = _handoffState.InheritEndTick;
            bootstrap.DebugState.handoffBlendEndTick = _handoffState.BlendEndTick;
            bootstrap.DebugState.handoffSnapshotPosition = eventData.SnapshotPosition;
            bootstrap.DebugState.handoffSnapshotYaw = eventData.SnapshotRotation.eulerAngles.y;
            bootstrap.DebugState.handoffSnapshotSpeed = new Vector3(eventData.SnapshotVelocity.x, 0f, eventData.SnapshotVelocity.z).magnitude;
            bootstrap.DebugState.handoffSnapshotAngularSpeed = eventData.SnapshotAngularVelocity.magnitude;
            bootstrap.DebugState.handoffActive = _handoffState.IsActive;
            bootstrap.DebugState.launchState = _handoffState.CurrentState.ToString();
            bootstrap.DebugState.handoffBlendAlpha = _handoffState.BlendAlpha;
            bootstrap.DebugState.handoffSuppressSteeringActive = _handoffState.SuppressSteeringUntilTick > currentTick;
            bootstrap.DebugState.handoffRoomBypassActive = _handoffState.RoomBypassUntilTick > currentTick;
            bootstrap.DebugState.introControlActive = _introControlActive;
            bootstrap.DebugState.externalKinematicControlActive = _externalKinematicControlActive;
        }

        private void UpdateHandoffDebug(uint currentTick)
        {
            if (bootstrap == null)
                return;

            RefreshLaunchState(currentTick);
            bootstrap.DebugState.handoffActive = _handoffState.IsActive;
            bootstrap.DebugState.launchState = _handoffState.CurrentState.ToString();
            bootstrap.DebugState.handoffBlendAlpha = _handoffState.BlendAlpha;
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
            var impulseChannelDbg = bootstrap.CommandBus != null ? bootstrap.CommandBus.ImpulseChannel : null;
            bootstrap.DebugState.pendingImpulseCount = impulseChannelDbg != null ? impulseChannelDbg.Count : 0;
            UpdateHandoffDebug(tick);
        }

    }
}
