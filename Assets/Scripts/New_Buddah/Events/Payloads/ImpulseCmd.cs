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
    //
    // Phase 4b V2b Step 1: LogicalId added (Q0 hybrid dedup belt-and-braces). Adapter stamps via
    // its per-instance _nextLogicalId monotonic counter at cmd construction. Channel TryEnqueue
    // checks recent-IDs against LogicalId, drops duplicates with [Channel]:DupReject warning
    // (not FATAL — single-emit invariant remains the convention; dedup catches future-fault
    // double-fire). +4 bytes wire format change vs Step 0.
    public struct ImpulseCmd
    {
        public Vector3 LinearImpulse;
        public float TurnImpulse;
        public byte SourceType;
        public int SourceObjectId;
        public uint EventTick;
        public uint LogicalId;

        public ImpulseCmd(Vector3 linearImpulse, float turnImpulse, byte sourceType, int sourceObjectId, uint eventTick, uint logicalId)
        {
            LinearImpulse = linearImpulse;
            TurnImpulse = turnImpulse;
            SourceType = sourceType;
            SourceObjectId = sourceObjectId;
            EventTick = eventTick;
            LogicalId = logicalId;
        }
    }
}
