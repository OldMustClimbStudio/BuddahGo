using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    internal static class BuddahTickMath
    {
        internal static uint DurationToTicks(float durationSeconds, float tickDelta)
        {
            return (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, tickDelta));
        }
    }
}
