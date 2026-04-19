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
    }
}
