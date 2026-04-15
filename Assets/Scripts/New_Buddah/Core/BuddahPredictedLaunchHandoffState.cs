using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedLaunchHandoffState
    {
        public bool IsActive;
        public uint EventId;
        public uint EventTick;
        public uint StartTick;
        public uint InheritEndTick;
        public uint BlendEndTick;
        public uint SuppressSteeringUntilTick;
        public uint RoomBypassUntilTick;
        public BuddahPredictedLaunchState CurrentState;
        public float BlendAlpha;
        public Vector3 SnapshotPosition;
        public Quaternion SnapshotRotation;
        public Vector3 SnapshotVelocity;
        public Vector3 SnapshotAngularVelocity;
        public Vector3 SnapshotForward;

        public static BuddahPredictedLaunchHandoffState FromData(BuddahPredictedLaunchHandoffData data)
        {
            uint inheritEndTick = data.StartTick + data.InheritDurationTicks;
            uint blendEndTick = inheritEndTick + data.BlendDurationTicks;
            BuddahPredictedLaunchState state = BuddahPredictedLaunchState.Normal;

            if (data.InheritDurationTicks > 0u)
                state = BuddahPredictedLaunchState.Inherit;
            else if (data.BlendDurationTicks > 0u)
                state = BuddahPredictedLaunchState.Blend;

            return new BuddahPredictedLaunchHandoffState
            {
                IsActive = state != BuddahPredictedLaunchState.Normal,
                EventId = data.EventId,
                EventTick = data.StartTick,
                StartTick = data.StartTick,
                InheritEndTick = inheritEndTick,
                BlendEndTick = blendEndTick,
                SuppressSteeringUntilTick = data.StartTick + data.SuppressSteeringDurationTicks,
                RoomBypassUntilTick = data.StartTick + data.RoomBypassDurationTicks,
                CurrentState = state,
                BlendAlpha = state == BuddahPredictedLaunchState.Blend ? 0f : (state == BuddahPredictedLaunchState.Normal ? 1f : 0f),
                SnapshotPosition = data.SnapshotPosition,
                SnapshotRotation = data.SnapshotRotation,
                SnapshotVelocity = data.SnapshotVelocity,
                SnapshotAngularVelocity = data.SnapshotAngularVelocity,
                SnapshotForward = data.SnapshotForward
            };
        }
    }
}
