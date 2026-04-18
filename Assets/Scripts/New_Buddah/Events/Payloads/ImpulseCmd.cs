using UnityEngine;

namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Non-readonly struct with public fields so FishNet auto-codegen can serialize it over TargetRpc.
    public struct ImpulseCmd
    {
        public Vector3 LinearImpulse;
        public float TurnImpulse;
        public byte SourceType;
        public int SourceObjectId;

        public ImpulseCmd(Vector3 linearImpulse, float turnImpulse, byte sourceType, int sourceObjectId)
        {
            LinearImpulse = linearImpulse;
            TurnImpulse = turnImpulse;
            SourceType = sourceType;
            SourceObjectId = sourceObjectId;
        }
    }
}
