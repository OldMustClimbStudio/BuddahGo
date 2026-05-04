namespace NewBuddah.PredictionV2.Core
{
    // Phase 6 — race-start handoff redesign (stop-then-countdown).
    // Inherit / Blend collapsed. Locked = Phase 3 freeze (rb.isKinematic=true,
    // input gated, MovementAllowed=false). Normal = Phase 4 unlock.
    public enum BuddahPredictedLaunchState : byte
    {
        Normal = 0,
        Locked = 1
    }
}
