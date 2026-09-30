using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Simulation
{
    // Phase 3d handoff shadow step — observation only.
    //
    // Mirrors BuddahPredictedMotor's RunInputs handoff sequence on snapshotted
    // inputs carried via TickContext:
    //   (1) Advance pre-consume state — mirrors motor.cs:326 RefreshLaunchState.
    //   (2) If pending handoff slot is ripe, project for arrival + FromData +
    //       Advance — mirrors motor.cs:345 ConsumePendingLaunchHandoffEvent +
    //       motor.cs:347 post-consume RefreshLaunchState.
    // Both motor and shadow call into BuddahPredictedLaunchHandoffResolver for
    // the pure math, so the shadow's output is bit-identical to motor's by
    // construction (no re-implementation drift — see Phase 3d closeout).
    //
    // Gate: unconditional (parity-by-call-site-invariant, consistent with
    // 3a / 3b / 3c). Motor invokes this step every tick inside RunInputs after
    // the post-consume RefreshLaunchState; skip-gating is the motor's
    // responsibility.
    public static class BuddahHandoffStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;

            // Teleport is consumed first by the motor. ResetModifiers clears both
            // the current handoff and its pending slot before handoff consumption.
            bool clearedByTeleport = scratch.TeleportRan && scratch.TeleportFlag_ResetModifiers;
            BuddahPredictedLaunchHandoffState preState = clearedByTeleport
                ? default
                : ctx.ShadowPreHandoffState;
            BuddahPredictedLaunchHandoffState shadowState = BuddahPredictedLaunchHandoffResolver.Advance(
                preState, ctx.Tick);

            if (!clearedByTeleport && ctx.ShadowPreHandoffHasPending
                && ctx.ShadowPreHandoffEvent.StartTick <= ctx.Tick)
            {
                BuddahPredictedLaunchHandoffData adjusted = BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick(
                    ctx.ShadowPreHandoffEvent, ctx.Tick, ctx.FixedDeltaTime);
                shadowState = BuddahPredictedLaunchHandoffState.FromData(adjusted);
                shadowState = BuddahPredictedLaunchHandoffResolver.Advance(shadowState, ctx.Tick);
                scratch.HandoffRan = true;
                scratch.ShadowLastConsumedHandoffId = adjusted.EventId;
            }

            scratch.ShadowHandoffState = shadowState;
        }
    }
}
