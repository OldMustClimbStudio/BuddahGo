namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Kind / StackPolicy are byte-encoded enums resolved inside Phase 3+ ModifierStep.
    //
    // Phase 4b V2b Step 0: EventTick added for cross-channel API uniformity. No consumer wired yet
    // (V2b Step 1+ migrates Modifier drain). See agent-exchange/handoff/2026-05-02-phase4b-v2b-step0-design.md.
    public struct ModifierCmd
    {
        public byte Kind;
        public float Magnitude;
        public float Duration;
        public byte StackPolicy;
        public uint EventTick;

        public ModifierCmd(byte kind, float magnitude, float duration, byte stackPolicy, uint eventTick)
        {
            Kind = kind;
            Magnitude = magnitude;
            Duration = duration;
            StackPolicy = stackPolicy;
            EventTick = eventTick;
        }
    }
}
