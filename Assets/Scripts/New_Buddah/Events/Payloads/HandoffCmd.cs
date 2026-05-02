using UnityEngine;

namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // "LaunchHandoffSnapshot" is inlined as discrete fields so FishNet auto-codegen can serialize without
    // introducing a new nested type. Flags packs booleans like clearAngularVelocity / rebaseTrails.
    //
    // Phase 4b V2b Step 0: EventTick added for cross-channel API uniformity. No consumer wired yet
    // (V2b Step 1+ migrates Handoff drain). See agent-exchange/handoff/2026-05-02-phase4b-v2b-step0-design.md.
    public struct HandoffCmd
    {
        public Vector3 SnapshotPosition;
        public Quaternion SnapshotRotation;
        public Vector3 SnapshotVelocity;
        public Vector3 SnapshotAngularVelocity;
        public Vector3 SnapshotForward;
        public float Inherit;
        public float Blend;
        public float Bypass;
        public float SuppressTurn;
        public byte Flags;
        public uint EventTick;
    }
}
