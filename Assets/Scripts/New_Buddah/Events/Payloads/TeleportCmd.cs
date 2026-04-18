using UnityEngine;

namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Flags packs booleans like clearAngularVelocity / rebaseTrails / resetPushGrace (Phase 3+ steps decode).
    public struct TeleportCmd
    {
        public Vector3 Pos;
        public Quaternion Rot;
        public float Progress01;
        public byte Source;
        public byte Flags;

        public TeleportCmd(Vector3 pos, Quaternion rot, float progress01, byte source, byte flags)
        {
            Pos = pos;
            Rot = rot;
            Progress01 = progress01;
            Source = source;
            Flags = flags;
        }
    }
}
