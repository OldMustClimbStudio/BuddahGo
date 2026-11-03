using System;

namespace NewBuddah.PredictionV2.Events
{
    [Flags]
    public enum BuddahPredictionChannelMask : byte
    {
        None = 0,
        Impulse = 1 << 0,
        All = Impulse,
    }
}
