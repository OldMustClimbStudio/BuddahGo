using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Simulation
{
    // ASSUMPTION A5: Reconcile replay gives deterministic entry state.
    // If shadow scratch diverges from real rb AFTER a reconcile replay (not during
    // initial forward tick), the assumption is violated. Phase 3d adds the cross-peer
    // final-pos compare log; Phase 3a only catches single-peer owner-side divergence.
    // If owner-side D-LOC fires on reconcile-triggered replays, see
    // Docs/prediction-refactor-plan/16-api-spike-checklist.md A5 contingency.
    //
    // Gate notes: this step is called only from inside BuddahPredictedMotor's active
    // path (post writer-relinquish, post !MovementAllowed, post IsRooted early returns).
    // All skip-gating is the motor's responsibility. The step does not re-check those
    // conditions; it mirrors only the in-active-path force/torque/clamp math.
    //
    // All "resolved" inputs (ForwardDirection, ResolvedThrottle, ResolvedSteering) are
    // the motor's post-scaling values (after ApplyLaunchHandoffInputScaling and
    // IsSteeringSuppressed zero-out). Shadow does not reproduce those transforms -
    // it reads what the motor already produced.
    public static class BuddahLocomotionStep
    {
        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            scratch.LocomotionRan = true;

            // Pre-force planar xz clamp - mirrors BuddahPredictedMotor.ClampPlanarSpeed
            // (motor.cs:1140). Reads the pre-clamp velocity snapshot captured by motor
            // immediately before its own ClampPlanarSpeed call.
            float effMax = ctx.ComputedStats.FinalMaxSpeed
                           + (ctx.ComputedStats.IsPushGraceActive ? ctx.PushGraceExtraSpeed : 0f);

            Vector3 velocity = ctx.RbVelocityPreTick;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (planarVelocity.sqrMagnitude > effMax * effMax)
            {
                Vector3 clampedPlanar = planarVelocity.normalized * effMax;
                scratch.VelocityAfterClamp = new Vector3(clampedPlanar.x, velocity.y, clampedPlanar.z);
                scratch.ClampingApplied = true;
            }

            // Forward force - mirrors motor line 370.
            scratch.CommandedForwardForce =
                ctx.ForwardDirection * (ctx.ComputedStats.FinalForwardForce * ctx.ResolvedThrottle);

            // Torque - mirrors motor line 378-383 branch: only sets torque when
            // |resolvedSteering| > 0.001f (decay branch leaves scratch at zero).
            scratch.CommandedTurnTorque =
                Mathf.Abs(ctx.ResolvedSteering) > 0.001f
                    ? ctx.ResolvedSteering * ctx.ComputedStats.FinalTurnTorque
                    : 0f;
        }
    }
}
