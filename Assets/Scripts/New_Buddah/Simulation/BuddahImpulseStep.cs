using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Simulation
{
    // Phase 3b impulse shadow step — observation only.
    //
    // Consumes from the pre-consume pending-list snapshot carried on TickContext.
    // Motor's BuddahPredictedImpulseEventQueue.ConsumeReady walks _pending in REVERSE
    // (i = Count-1 down to 0). See BuddahPredictedMotor.cs:1304. Shadow MUST match
    // that order so the consumed set (and ShadowLastConsumedImpulseId = max consumed
    // EventId) agrees with motor.
    //
    // Scope: cursor + ran-flag compare only. No velocity/torque delta integration;
    // PhysX mass math is deferred. The event struct carries the final world-space
    // Impulse + TurnTorqueImpulse already baked upstream, so there are no
    // intermediate transformations to mirror here.
    //
    // Gate: unconditional (parity-by-call-site-invariant, same as 3a Locomotion).
    // Motor invokes this step every tick inside RunInputs; skip-gating is the
    // motor's responsibility.
    public static class BuddahImpulseStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;

            var snapshot = ctx.ImpulsePendingSnapshot;
            if (snapshot == null || snapshot.Count == 0)
                return;

            uint maxConsumedId = 0u;
            bool anyConsumed = false;

            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                BuddahPredictedImpulseEventData ev = snapshot[i];
                if (ev.EventTick > ctx.Tick)
                    continue;

                anyConsumed = true;
                if (ev.EventId > maxConsumedId)
                    maxConsumedId = ev.EventId;
            }

            if (anyConsumed)
            {
                scratch.ImpulseRan = true;
                scratch.ShadowLastConsumedImpulseId = maxConsumedId;
            }
        }
    }
}
