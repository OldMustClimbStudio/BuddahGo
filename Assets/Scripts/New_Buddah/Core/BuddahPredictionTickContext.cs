using System.Collections.Generic;
using NewBuddah.PredictionV2.Config;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public readonly struct BuddahPredictionTickContext
    {
        public readonly Vector3 RbVelocityPreTick;
        public readonly float RbMass;
        public readonly float FixedDeltaTime;
        public readonly uint Tick;

        public readonly Vector3 ForwardDirection;
        public readonly float ResolvedThrottle;
        public readonly float ResolvedSteering;

        public readonly BuddahPredictedMotorComputedStats ComputedStats;
        public readonly float PushGraceExtraSpeed;
        public readonly float PushGraceRemaining;

        // Phase 3b — impulse snapshot (pre-consume pending list, in enqueue order).
        // Shadow walks this in REVERSE to match motor's LIFO ConsumeReady (motor.cs:1304).
        public readonly IReadOnlyList<BuddahPredictedImpulseEventData> ImpulsePendingSnapshot;

        // Phase 3b — teleport snapshot (single-slot; snapshotted before ConsumePendingTeleportEvent).
        public readonly bool HasPendingTeleportPreConsume;
        public readonly uint PendingTeleportEventId;
        public readonly uint PendingTeleportEventTick;
        public readonly Vector3 TeleportTargetPosition;
        public readonly Quaternion TeleportTargetRotation;
        public readonly bool TeleportFlag_SnapProgress;
        public readonly bool TeleportFlag_ZeroLinearVelocity;
        public readonly bool TeleportFlag_ZeroAngularVelocity;
        public readonly bool TeleportFlag_ResetModifiers;
        public readonly bool TeleportFlag_ResetImpulseQueue;
        public readonly bool TeleportFlag_ResetPushGrace;
        public readonly bool TeleportFlag_RebaseTrails;

        // Phase 3c — modifier shadow inputs. Snapshot of motor's _modifierState taken
        // immediately before the authoritative Resolve at motor.cs:341; Config is the
        // same reference motor passes into its own Resolve call.
        public readonly BuddahPredictedModifierState ShadowModifierStateSnapshot;
        public readonly BuddahPredictedMotorConfig Config;

        // Phase 3d — handoff snapshot fields. Snapshotted in the same 3b pre-consume
        // shadow block immediately before ConsumePendingLaunchHandoffEvent runs, so
        // BuddahHandoffStep can parity-mirror the consume → FromData → Advance pipeline
        // against identical inputs.
        public readonly bool ShadowPreHandoffHasPending;
        public readonly BuddahPredictedLaunchHandoffData ShadowPreHandoffEvent;
        public readonly BuddahPredictedLaunchHandoffState ShadowPreHandoffState;

        public BuddahPredictionTickContext(
            Vector3 rbVelocityPreTick,
            float rbMass,
            float fixedDeltaTime,
            uint tick,
            Vector3 forwardDirection,
            float resolvedThrottle,
            float resolvedSteering,
            BuddahPredictedMotorComputedStats computedStats,
            float pushGraceExtraSpeed,
            float pushGraceRemaining,
            IReadOnlyList<BuddahPredictedImpulseEventData> impulsePendingSnapshot,
            bool hasPendingTeleportPreConsume,
            uint pendingTeleportEventId,
            uint pendingTeleportEventTick,
            Vector3 teleportTargetPosition,
            Quaternion teleportTargetRotation,
            bool teleportFlag_SnapProgress,
            bool teleportFlag_ZeroLinearVelocity,
            bool teleportFlag_ZeroAngularVelocity,
            bool teleportFlag_ResetModifiers,
            bool teleportFlag_ResetImpulseQueue,
            bool teleportFlag_ResetPushGrace,
            bool teleportFlag_RebaseTrails,
            BuddahPredictedModifierState shadowModifierStateSnapshot,
            BuddahPredictedMotorConfig config,
            bool shadowPreHandoffHasPending,
            BuddahPredictedLaunchHandoffData shadowPreHandoffEvent,
            BuddahPredictedLaunchHandoffState shadowPreHandoffState)
        {
            RbVelocityPreTick = rbVelocityPreTick;
            RbMass = rbMass;
            FixedDeltaTime = fixedDeltaTime;
            Tick = tick;
            ForwardDirection = forwardDirection;
            ResolvedThrottle = resolvedThrottle;
            ResolvedSteering = resolvedSteering;
            ComputedStats = computedStats;
            PushGraceExtraSpeed = pushGraceExtraSpeed;
            PushGraceRemaining = pushGraceRemaining;
            ImpulsePendingSnapshot = impulsePendingSnapshot;
            HasPendingTeleportPreConsume = hasPendingTeleportPreConsume;
            PendingTeleportEventId = pendingTeleportEventId;
            PendingTeleportEventTick = pendingTeleportEventTick;
            TeleportTargetPosition = teleportTargetPosition;
            TeleportTargetRotation = teleportTargetRotation;
            TeleportFlag_SnapProgress = teleportFlag_SnapProgress;
            TeleportFlag_ZeroLinearVelocity = teleportFlag_ZeroLinearVelocity;
            TeleportFlag_ZeroAngularVelocity = teleportFlag_ZeroAngularVelocity;
            TeleportFlag_ResetModifiers = teleportFlag_ResetModifiers;
            TeleportFlag_ResetImpulseQueue = teleportFlag_ResetImpulseQueue;
            TeleportFlag_ResetPushGrace = teleportFlag_ResetPushGrace;
            TeleportFlag_RebaseTrails = teleportFlag_RebaseTrails;
            ShadowModifierStateSnapshot = shadowModifierStateSnapshot;
            Config = config;
            ShadowPreHandoffHasPending = shadowPreHandoffHasPending;
            ShadowPreHandoffEvent = shadowPreHandoffEvent;
            ShadowPreHandoffState = shadowPreHandoffState;
        }
    }
}
