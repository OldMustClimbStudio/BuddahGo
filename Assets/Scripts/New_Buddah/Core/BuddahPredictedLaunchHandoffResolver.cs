namespace NewBuddah.PredictionV2.Core
{
    // Phase 6 — pure-static 2-state resolver (Locked / Normal). Inherit/Blend
    // collapsed; ProjectForArrivalTick deleted (no projection needed when
    // SnapshotVelocity = Vector3.zero — race-start lock pose is the snapshot
    // pose itself). Same code path used by motor RefreshLaunchState wrapper
    // and BuddahHandoffStep shadow simulator → parity-by-construction.
    //
    // Zero external dependencies: no Unity TimeManager read, no bootstrap,
    // no rigidbody. Bit-identical output on identical inputs.
    public static class BuddahPredictedLaunchHandoffResolver
    {
        // Advances the handoff state machine for the current tick.
        // Caller writes the result back and emits any state-transition log.
        public static BuddahPredictedLaunchHandoffState Advance(
            BuddahPredictedLaunchHandoffState state, uint currentTick)
        {
            if (!state.IsActive)
            {
                state.CurrentState = BuddahPredictedLaunchState.Normal;
                return state;
            }

            if (state.LockedUntilTick > currentTick)
            {
                state.CurrentState = BuddahPredictedLaunchState.Locked;
            }
            else
            {
                state.CurrentState = BuddahPredictedLaunchState.Normal;
                state.IsActive = false;
            }

            return state;
        }
    }
}
