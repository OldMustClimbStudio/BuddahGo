using FishNet.Object.Prediction;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedReconcileData : IReconcileData
    {
        private uint _tick;

        public PredictionRigidbody RigidbodyState;
        public BuddahPredictedModifierState ModifierState;
        public BuddahPredictedMotorComputedStats ComputedStats;
        public BuddahPredictedLaunchHandoffState HandoffState;
        public bool IntroControlActive;
        public bool ExternalKinematicControlActive;
        public bool MovementAllowed;
        public float PlanarSpeed;
        public Vector3 ServerForward;

        public BuddahPredictedTeleportEventData PendingTeleport;
        public bool HasPendingTeleport;
        public uint LastConsumedTeleportId;

        public BuddahPredictedLaunchHandoffData PendingHandoff;
        public bool HasPendingHandoff;
        public uint LastConsumedHandoffId;

        public bool AwaitingAuthoritativeLaunchHandoff;
        public uint LocalPreHandoffBypassUntilTick;

        public BuddahPredictedImpulseRingSnapshot ImpulseQueueState;

        public BuddahPredictedReconcileData(
            PredictionRigidbody rigidbodyState,
            BuddahPredictedModifierState modifierState,
            BuddahPredictedMotorComputedStats computedStats,
            BuddahPredictedLaunchHandoffState handoffState,
            bool introControlActive,
            bool externalKinematicControlActive,
            bool movementAllowed,
            float planarSpeed,
            Vector3 serverForward) : this()
        {
            RigidbodyState = rigidbodyState;
            ModifierState = modifierState;
            ComputedStats = computedStats;
            HandoffState = handoffState;
            IntroControlActive = introControlActive;
            ExternalKinematicControlActive = externalKinematicControlActive;
            MovementAllowed = movementAllowed;
            PlanarSpeed = planarSpeed;
            ServerForward = serverForward;
        }

        public uint GetTick()
        {
            return _tick;
        }

        public void SetTick(uint value)
        {
            _tick = value;
        }

        public void Dispose()
        {
        }
    }
}
