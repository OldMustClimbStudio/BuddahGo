using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Phase 6 — race-start handoff data. Stop-then-countdown collapses
    // InheritDurationTicks + BlendDurationTicks into a single LockedDurationTicks
    // (the Phase 3 lock window length, default 180 ticks @ 60Hz = 3s). Snapshot*
    // fields are conventionally set to zero by the caller — Phase 6 design has
    // SnapshotVelocity = Vector3.zero (no velocity inheritance, M2 elimination).
    public struct BuddahPredictedLaunchHandoffData
    {
        public uint EventId;
        public uint StartTick;
        public Vector3 SnapshotPosition;
        public Quaternion SnapshotRotation;
        public Vector3 SnapshotVelocity;
        public Vector3 SnapshotAngularVelocity;
        public Vector3 SnapshotForward;
        public uint LockedDurationTicks;
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
            uint lockedDurationTicks,
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
            LockedDurationTicks = lockedDurationTicks;
            SuppressSteeringDurationTicks = suppressSteeringDurationTicks;
            RoomBypassDurationTicks = roomBypassDurationTicks;
            DebugSequenceId = debugSequenceId;
            EnableDebugLogs = enableDebugLogs;
        }
    }
}
