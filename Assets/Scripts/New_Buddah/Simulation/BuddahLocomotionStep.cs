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
    //
    // Phase 4a Z2 (2026-04-19): `Compute` exposes the commanded-force/turn-torque
    // math as a pure-static overload that both motor's real path and shadow `Run`
    // delegate to. Motor applies the returned values via AddForce / AddTorque at
    // BuddahPredictedMotor.cs:441-463; shadow `Run` additionally writes scratch for
    // the parity-by-construction compare. Decay branch at motor.cs:459-463 is
    // retained inline (Z2 hybrid scope; Phase 8 Entry 3 covers the deferred
    // decay-branch migration contingent on Phase 3a scope extension).
    public static class BuddahLocomotionStep
    {
        // Phase 4a Z2: commanded-value math lifted out of motor's inline locomotion
        // block so motor and shadow call the SAME body. Bit-identical output under
        // identical inputs in single-threaded C# / Mono / IL2CPP. Motor uses the out
        // params directly; shadow `Run` wraps this call and also captures into scratch.
        // Pure — zero Unity API calls beyond Vector3 math + Mathf.Abs.
        public static void Compute(
            Vector3 forwardDirection,
            float resolvedThrottle,
            float resolvedSteering,
            BuddahPredictedMotorComputedStats computedStats,
            out Vector3 commandedForwardForce,
            out float commandedTurnTorque)
        {
            commandedForwardForce = forwardDirection * (computedStats.FinalForwardForce * resolvedThrottle);
            commandedTurnTorque = Mathf.Abs(resolvedSteering) > 0.001f
                ? resolvedSteering * computedStats.FinalTurnTorque
                : 0f;
        }

        public static void Run(
            in BuddahPredictionTickContext ctx,
            in BuddahPredictedInputData input,
            ref BuddahPredictionShadowScratch scratch)
        {
            _ = input;
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

            // Phase 4a Z2: delegate commanded math to Compute so motor's real path
            // and this shadow invocation run through identical code. Decay-branch
            // output is NOT produced here (decay stays motor-inline).
            Compute(
                ctx.ForwardDirection,
                ctx.ResolvedThrottle,
                ctx.ResolvedSteering,
                ctx.ComputedStats,
                out Vector3 fwd,
                out float turn);
            scratch.CommandedForwardForce = fwd;
            scratch.CommandedTurnTorque = turn;
        }
    }
}
