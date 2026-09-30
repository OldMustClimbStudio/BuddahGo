using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    internal static class BuddahTickMath
    {
        // Server-stamped events must enter the owner's local simulation clock before queuing.
        // Event tick zero is valid (unlike a modifier deadline's unset sentinel).
        internal static uint ServerEventToLocalTick(uint eventTick, uint serverTick, uint localTick)
        {
            long translated = (long)localTick + (long)eventTick - serverTick;
            if (translated <= 0L)
                return 0u;
            if (translated >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)translated;
        }

        internal static uint DurationToTicks(float durationSeconds, float tickDelta)
        {
            return (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, tickDelta));
        }
    }
}
