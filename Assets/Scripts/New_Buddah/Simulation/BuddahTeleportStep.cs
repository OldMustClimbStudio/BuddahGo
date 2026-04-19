using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Simulation
{
    // Phase 3b teleport shadow step — observation only.
    //
    // Consumes from the single-slot teleport snapshot on TickContext (captured
    // before motor's ConsumePendingTeleportEvent at BuddahPredictedMotor.cs:1343).
    // Motor applies 7 Flags bits + unconditional position/rotation write. Shadow
    // mirrors cursor, ran-flag, target pose, and all 7 flag reads. Physical effects
    // of 3 flags are mirrored via scratch (ZeroLinearVelocity / ZeroAngularVelocity /
    // ResetImpulseQueue); non-physics flags (SnapProgress / RebaseTrails) only
    // compare their read bits; ResetModifiers / ResetPushGrace are deferred to 3c.
    //
    // Gate: unconditional (parity-by-call-site-invariant, same as 3a Locomotion).
    public static class BuddahTeleportStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;

            if (!ctx.HasPendingTeleportPreConsume)
                return;

            if (ctx.PendingTeleportEventTick > ctx.Tick)
                return;

            scratch.TeleportRan = true;
            scratch.ShadowLastConsumedTeleportId = ctx.PendingTeleportEventId;
            scratch.PostTeleportPosition = ctx.TeleportTargetPosition;
            scratch.PostTeleportRotation = ctx.TeleportTargetRotation;

            scratch.TeleportFlag_SnapProgress = ctx.TeleportFlag_SnapProgress;
            scratch.TeleportFlag_ZeroLinearVelocity = ctx.TeleportFlag_ZeroLinearVelocity;
            scratch.TeleportFlag_ZeroAngularVelocity = ctx.TeleportFlag_ZeroAngularVelocity;
            scratch.TeleportFlag_ResetModifiers = ctx.TeleportFlag_ResetModifiers;
            scratch.TeleportFlag_ResetImpulseQueue = ctx.TeleportFlag_ResetImpulseQueue;
            scratch.TeleportFlag_ResetPushGrace = ctx.TeleportFlag_ResetPushGrace;
            scratch.TeleportFlag_RebaseTrails = ctx.TeleportFlag_RebaseTrails;
        }
    }
}
