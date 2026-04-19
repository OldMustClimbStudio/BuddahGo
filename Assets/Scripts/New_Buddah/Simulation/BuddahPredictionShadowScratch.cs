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
        public uint ShadowLastConsumedModifierId;
        public uint ShadowLastConsumedHandoffId;

        // Phase 3b — impulse step.
        public bool ImpulseRan;

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
    }
}
