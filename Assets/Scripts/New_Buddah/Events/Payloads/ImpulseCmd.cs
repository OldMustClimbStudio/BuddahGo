using UnityEngine;

namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Non-readonly struct with public fields so FishNet auto-codegen can serialize it over TargetRpc.
    //
    // Phase 4b V2b Step 0: EventTick added (server-side canonical clock per design Q&A archive
    // 2026-05-02-phase4b-v2b-step0-design.md). Server stamps via TimeManager.LocalTick at adapter
    // construction; client uses cmd-supplied tick verbatim for ConsumeReady gate. Resolves L17
    // phase-skew at the protocol level — both OLD and NEW paths now compare against the same
    // canonical clock for the same logical event.
    public struct ImpulseCmd
    {
        public Vector3 LinearImpulse;
        public float TurnImpulse;
        public byte SourceType;
        public int SourceObjectId;
        public uint EventTick;

        public ImpulseCmd(Vector3 linearImpulse, float turnImpulse, byte sourceType, int sourceObjectId, uint eventTick)
        {
            LinearImpulse = linearImpulse;
            TurnImpulse = turnImpulse;
            SourceType = sourceType;
            SourceObjectId = sourceObjectId;
            EventTick = eventTick;
        }
    }
}
