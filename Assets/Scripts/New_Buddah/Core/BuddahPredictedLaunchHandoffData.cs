using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedLaunchHandoffData
    {
        public uint EventId;
        public uint StartTick;
        public Vector3 SnapshotPosition;
        public Quaternion SnapshotRotation;
        public Vector3 SnapshotVelocity;
        public Vector3 SnapshotAngularVelocity;
        public Vector3 SnapshotForward;
        public uint InheritDurationTicks;
        public uint BlendDurationTicks;
        public uint SuppressSteeringDurationTicks;
        public uint RoomBypassDurationTicks;
        public int DebugSequenceId;
        public bool EnableDebugLogs;

        public BuddahPredictedLaunchHandoffData(
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
            EventId = eventId;
            StartTick = startTick;
            SnapshotPosition = snapshotPosition;
            SnapshotRotation = snapshotRotation;
            SnapshotVelocity = snapshotVelocity;
            SnapshotAngularVelocity = snapshotAngularVelocity;
            SnapshotForward = snapshotForward;
            InheritDurationTicks = inheritDurationTicks;
            BlendDurationTicks = blendDurationTicks;
            SuppressSteeringDurationTicks = suppressSteeringDurationTicks;
            RoomBypassDurationTicks = roomBypassDurationTicks;
            DebugSequenceId = debugSequenceId;
            EnableDebugLogs = enableDebugLogs;
        }
    }
}
