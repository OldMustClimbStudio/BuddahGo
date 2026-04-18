using System;

namespace NewBuddah.PredictionV2.Events
{
    [Flags]
    public enum BuddahPredictionChannelMask : byte
    {
        None = 0,
        Impulse = 1 << 0,
        Teleport = 1 << 1,
        Modifier = 1 << 2,
        Handoff = 1 << 3,
        All = Impulse | Teleport | Modifier | Handoff,
    }
}
