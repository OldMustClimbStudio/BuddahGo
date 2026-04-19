using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Simulation
{
    // Phase 3c modifier shadow step — observation only.
    //
    // Runs BuddahPredictedModifierResolver.Resolve on the pre-resolve snapshot of
    // _modifierState carried on TickContext. Motor runs its own Resolve at
    // BuddahPredictedMotor.cs:341 and writes _computedStats; the shadow runs the
    // same Resolve on the snapshot (struct-by-value, so the snapshot is independent
    // of motor's later mutations). Compare happens in Shadow_CompareAndReport.
    //
    // Resolver is pure-static with zero side effects and no floating-point
    // non-determinism (see Phase 3c audit §0). Running Resolve twice on identical
    // inputs produces bit-identical output under C# / Mono / IL2CPP single-thread.
    //
    // Gate: unconditional (parity-by-call-site-invariant, same as 3a Locomotion and
    // 3b Impulse/Teleport). Motor invokes this step every tick inside RunInputs
    // immediately after its authoritative post-consume Resolve; skip-gating is the
    // motor's responsibility.
    public static class BuddahModifierStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;

            scratch.ShadowComputedStats = BuddahPredictedModifierResolver.Resolve(
                ctx.ShadowModifierStateSnapshot, ctx.Config, ctx.Tick);
            scratch.ModifierRan = true;
        }
    }
}
