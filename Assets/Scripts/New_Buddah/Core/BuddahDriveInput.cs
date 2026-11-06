namespace NewBuddah.PredictionV2.Core
{
    internal static class BuddahDriveInput
    {
        // The one push switch every racer writes into the replicate data: the owner's W (1 held, 0 released), or the
        // drive flag of an ISteeringOverride (AI, training dummy, harness park), which replaces the owner's input.
        // A closed movement gate (countdown, results) zeroes it; releasing only removes the force, the racer coasts.
        internal static float ResolveThrottle(bool movementAllowed, float ownerThrottle, bool hasOverride, bool overrideDrive)
        {
            if (!movementAllowed)
                return 0f;
            if (hasOverride)
                return overrideDrive ? 1f : 0f;
            return ownerThrottle > 0.5f ? 1f : 0f;
        }
    }
}
