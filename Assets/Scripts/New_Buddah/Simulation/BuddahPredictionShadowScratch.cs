using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Simulation
{
    public struct BuddahPredictionShadowScratch
    {
        public Vector3 CommandedForwardForce;
        public float CommandedTurnTorque;
        public bool LocomotionRan;

        public Vector3 VelocityAfterClamp;
        public bool ClampingApplied;

        public uint ShadowLastConsumedImpulseId;
        public uint ShadowLastConsumedTeleportId;
        // DEAD: Phase 0 design assumption superseded by Phase 3c (modifier is
        // state-driven, not event-driven — no per-event cursor). Phase 8 cleanup removes.
        public uint ShadowLastConsumedModifierId;
        public uint ShadowLastConsumedHandoffId;

        // Phase 3b — impulse step.
        // Phase 4b V2b Step 1: post authority-flip, _realScratch is NEW-driven (channel
        // ConsumePendingImpulseEvents_Authoritative writes here) and _legacyShadowScratch is
        // OLD-driven (queue ConsumePendingImpulseEvents_LegacyShadow writes here, observation
        // only — no rb writes). Compared at tick-end for the [D-IMP LEG HEARTBEAT] gate.
        // Note: _shadowScratch.ImpulseRan / ShadowLastConsumedImpulseId / ImpulseDrainCount were
        // removed from production write sites in Step 1 (Q4 amendment — early-shadow impulse step
        // killed); fields kept on struct for the other axes (Locomotion / Teleport / Modifier /
        // Handoff still use _shadowScratch).
        public bool ImpulseRan;
        public int ImpulseDrainCount;

        // Phase 3b — teleport step.
        public bool TeleportRan;
        public Vector3 PostTeleportPosition;
        public Quaternion PostTeleportRotation;
        public bool TeleportFlag_SnapProgress;
        public bool TeleportFlag_ZeroLinearVelocity;
        public bool TeleportFlag_ZeroAngularVelocity;
        public bool TeleportFlag_ResetModifiers;
        public bool TeleportFlag_ResetImpulseQueue;
        public bool TeleportFlag_ResetPushGrace;
        public bool TeleportFlag_RebaseTrails;

        // Phase 3c — modifier step. ShadowComputedStats is the output of running
        // BuddahPredictedModifierResolver.Resolve on the snapshotted _modifierState;
        // motor compares against its own _computedStats at same tick.
        public bool ModifierRan;
        public BuddahPredictedMotorComputedStats ShadowComputedStats;

        // Phase 3d — handoff step. HandoffRan marks whether the shadow step
        // consumed the pending handoff slot on this tick (gate parity with motor's
        // ConsumePendingLaunchHandoffEvent). ShadowHandoffState is the post-advance
        // state produced by BuddahHandoffStep — compared against motor's _handoffState
        // after motor.cs post-consume RefreshLaunchState. ShadowLastConsumedHandoffId
        // (declared above alongside the Phase 0 cursors) is now live in 3d — its
        // Phase 0 per-event cursor assumption was wrong for modifier (DP6 tag retained
        // above), but correct for handoff since handoff is genuinely event-queued.
        public bool HandoffRan;
        public BuddahPredictedLaunchHandoffState ShadowHandoffState;
    }
}
