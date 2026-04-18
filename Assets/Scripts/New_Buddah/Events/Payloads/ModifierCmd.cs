namespace NewBuddah.PredictionV2.Events.Payloads
{
    // Field layout per Docs/prediction-refactor-plan/03-data-contracts.md "Event Payloads" table.
    // Kind / StackPolicy are byte-encoded enums resolved inside Phase 3+ ModifierStep.
    public struct ModifierCmd
    {
        public byte Kind;
        public float Magnitude;
        public float Duration;
        public byte StackPolicy;

        public ModifierCmd(byte kind, float magnitude, float duration, byte stackPolicy)
        {
            Kind = kind;
            Magnitude = magnitude;
            Duration = duration;
            StackPolicy = stackPolicy;
        }
    }
}
