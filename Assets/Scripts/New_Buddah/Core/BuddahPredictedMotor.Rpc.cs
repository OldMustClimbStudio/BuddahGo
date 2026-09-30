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
        [ServerRpc(RequireOwnership = true)]
        private void RequestLaunchHandoffServerRpc(
            Vector3 snapshotPosition,
            Quaternion snapshotRotation,
            Vector3 snapshotVelocity,
            Vector3 snapshotAngularVelocity,
            Vector3 snapshotForward,
            float inheritDurationSeconds,
            float blendDurationSeconds,
            float bypassRoomStateSeconds,
            float suppressTurnInputSeconds,
            uint ownerTickAtRequest,
            int debugSequenceId,
            bool enableDebugLogs)
        {
            GameLog.Verbose($"[IntroHandoff][Server] Received owner handoff request seq={debugSequenceId} tick={(TimeManager != null ? TimeManager.LocalTick : 0u)} ownerTick={ownerTickAtRequest} speed={snapshotVelocity.magnitude:0.00}");
            bootstrap?.LogVerbose(
                $"[HandoffDebug] ServerRpc received tick={(TimeManager != null ? TimeManager.LocalTick : 0u)} ownerTick={ownerTickAtRequest} " +
                $"pos={snapshotPosition} speed={snapshotVelocity.magnitude:0.00} seq={debugSequenceId}");
            LaunchHandoffSnapshot snapshot = new LaunchHandoffSnapshot
            {
                Position = snapshotPosition,
                Rotation = snapshotRotation,
                Velocity = snapshotVelocity,
                AngularVelocity = snapshotAngularVelocity,
                Forward = snapshotForward
            };

            TryApplyServerAuthoritativeLaunchHandoff(
                snapshot,
                inheritDurationSeconds,
                blendDurationSeconds,
                bypassRoomStateSeconds,
                suppressTurnInputSeconds,
                false,
                debugSequenceId,
                enableDebugLogs,
                ownerTickAtRequest);
        }

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

        [TargetRpc]
        private void QueueLaunchHandoffTargetRpc(
            NetworkConnection conn,
            uint eventId,
            uint startTick,
            Vector3 snapshotPosition,
            Quaternion snapshotRotation,
            Vector3 snapshotVelocity,
            Vector3 snapshotAngularVelocity,
            Vector3 snapshotForward,
            uint inheritDurationTicks,
            uint blendDurationTicks,
            uint suppressSteeringDurationTicks,
            uint roomBypassDurationTicks,
            int debugSequenceId,
            bool enableDebugLogs)
        {
            GameLog.Verbose($"[IntroHandoff][Client] Received authoritative handoff eventId={eventId} seq={debugSequenceId} startTick={startTick} speed={snapshotVelocity.magnitude:0.00}");
            bootstrap?.LogVerbose(
                $"[HandoffDebug] TargetRpc received eventId={eventId} startTick={startTick} speed={snapshotVelocity.magnitude:0.00} seq={debugSequenceId}");
            BuddahPredictedLaunchHandoffData eventData = new(
                eventId,
                startTick,
                snapshotPosition,
                snapshotRotation,
                snapshotVelocity,
                snapshotAngularVelocity,
                snapshotForward,
                inheritDurationTicks,
                blendDurationTicks,
                suppressSteeringDurationTicks,
                roomBypassDurationTicks,
                debugSequenceId,
                enableDebugLogs);

            TryQueueLaunchHandoffEvent(eventData);
        }

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
            // Teleport consumption and its shadow use the owner's LocalTick. Translate the
            // received working copy; the server event and RPC payload stay server-stamped.
            if (!IsServerInitialized && TimeManager != null)
                eventTick = BuddahTickMath.ServerEventToLocalTick(eventTick, TimeManager.Tick, TimeManager.LocalTick);

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


    }
}
