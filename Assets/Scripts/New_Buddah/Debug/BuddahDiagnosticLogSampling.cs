namespace NewBuddah.PredictionV2.Debugging
{
    internal static class BuddahDiagnosticLogSampling
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool VerboseCompiledIn = true;
#else
        public const bool VerboseCompiledIn = false;
#endif

        public static bool TryBeginSample(bool hasConsumer, float now, float interval, ref float lastSample)
        {
            if (!VerboseCompiledIn || !hasConsumer || now - lastSample < System.Math.Max(0.1f, interval))
                return false;

            // Advance even if the resulting text is unchanged: deduplication must not cause
            // formatting to resume every frame after the first quiet interval.
            lastSample = now;
            return true;
        }
    }
}
