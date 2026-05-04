using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Phase 6 — race-start handoff runtime state. Inherit/Blend collapsed:
    // BlendAlpha / InheritEndTick / BlendEndTick removed. LockedUntilTick is
    // the single tick boundary: while currentTick < LockedUntilTick the motor
    // is in Locked state (Phase 3 freeze); past it, Normal (Phase 4 unlock).
    public struct BuddahPredictedLaunchHandoffState
    {
        public bool IsActive;
        public uint EventId;
        public uint EventTick;
        public uint StartTick;
        public uint LockedUntilTick;
        public uint SuppressSteeringUntilTick;
        public uint RoomBypassUntilTick;
        public BuddahPredictedLaunchState CurrentState;
        public Vector3 SnapshotPosition;
        public Quaternion SnapshotRotation;
        public Vector3 SnapshotVelocity;
        public Vector3 SnapshotAngularVelocity;
        public Vector3 SnapshotForward;

        public static BuddahPredictedLaunchHandoffState FromData(BuddahPredictedLaunchHandoffData data)
        {
            uint lockedUntilTick = data.StartTick + data.LockedDurationTicks;
            BuddahPredictedLaunchState state = data.LockedDurationTicks > 0u
                ? BuddahPredictedLaunchState.Locked
                : BuddahPredictedLaunchState.Normal;

            return new BuddahPredictedLaunchHandoffState
            {
                IsActive = state == BuddahPredictedLaunchState.Locked,
                EventId = data.EventId,
                EventTick = data.StartTick,
                StartTick = data.StartTick,
                LockedUntilTick = lockedUntilTick,
                SuppressSteeringUntilTick = data.StartTick + data.SuppressSteeringDurationTicks,
                RoomBypassUntilTick = data.StartTick + data.RoomBypassDurationTicks,
                CurrentState = state,
                SnapshotPosition = data.SnapshotPosition,
                SnapshotRotation = data.SnapshotRotation,
                SnapshotVelocity = data.SnapshotVelocity,
                SnapshotAngularVelocity = data.SnapshotAngularVelocity,
                SnapshotForward = data.SnapshotForward
            };
        }
    }
}
