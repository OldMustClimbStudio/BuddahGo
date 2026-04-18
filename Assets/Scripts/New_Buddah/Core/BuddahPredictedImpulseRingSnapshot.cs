namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedImpulseRingSnapshot
    {
        public const int Capacity = 64;

        public BuddahPredictedImpulseEventData[] Entries;
        public ushort Head;
        public ushort Count;
        public uint LastConsumedId;
        public uint NextSequence;
    }
}
