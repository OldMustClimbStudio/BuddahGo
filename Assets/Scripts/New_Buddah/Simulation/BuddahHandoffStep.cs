using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Simulation
{
    // Phase 6 handoff shadow step — observation only.
    //
    // Phase 6 collapsed the queue-based handoff to a SyncVar-driven Locked state.
    // Pending-event consume + ProjectForArrivalTick retired with the RPC chain
    // (Section 11.1 option a deletion). Shadow now mirrors only the post-Advance
    // state of the motor's _handoffState — no consume-side parity to verify.
    //
    // Gate: unconditional (parity-by-call-site-invariant). Motor invokes this
    // step every tick after the post-consume RefreshLaunchState; skip-gating is
    // the motor's responsibility.
    public static class BuddahHandoffStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;

            BuddahPredictedLaunchHandoffState shadowState = BuddahPredictedLaunchHandoffResolver.Advance(
                ctx.ShadowPreHandoffState, ctx.Tick);
            scratch.ShadowHandoffState = shadowState;
        }
    }
}
