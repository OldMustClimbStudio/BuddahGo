using UnityEngine;

namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Flags packs booleans like clearAngularVelocity / rebaseTrails / resetPushGrace (Phase 3+ steps decode).
    //
    // Phase 4b V2b Step 0: EventTick added for cross-channel API uniformity. No consumer wired yet
    // (V2b Step 1+ migrates Teleport drain). See agent-exchange/handoff/2026-05-02-phase4b-v2b-step0-design.md.
    // Phase 4b V2b Step 1: LogicalId added for cross-channel API uniformity (forward consistency
    // with ImpulseCmd's Q0 dedup mechanism). No consumer wired on Teleport yet.
    public struct TeleportCmd
    {
        public Vector3 Pos;
        public Quaternion Rot;
        public float Progress01;
        public byte Source;
        public byte Flags;
        public uint EventTick;
        public uint LogicalId;

        public TeleportCmd(Vector3 pos, Quaternion rot, float progress01, byte source, byte flags, uint eventTick, uint logicalId)
        {
            Pos = pos;
            Rot = rot;
            Progress01 = progress01;
            Source = source;
            Flags = flags;
            EventTick = eventTick;
            LogicalId = logicalId;
        }
    }
}
