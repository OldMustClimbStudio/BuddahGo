// Frozen a9c9a17 implementation: independent cost-optimization equivalence oracle.
using UnityEngine;
using NewBuddah.PredictionV2.Simulation;
namespace BuddahGo.AI {
    // Planar, collision-free prediction. Never writes to the real Rigidbody.
    public static class LegacyBuddahMotionModel
    {
        public static void Step(ref MotionState state, in MotionParameters parameters, int key, float dt)
        {
            if (parameters.Stats.IsRooted) { state.Velocity = Vector3.zero; state.YawRate = 0f; return; }
            float cap = parameters.Stats.FinalMaxSpeed + (parameters.Stats.IsPushGraceActive ? parameters.PushExtraSpeed : 0f);
            state.Velocity = Vector3.ClampMagnitude(state.Velocity, cap);
            float steering = parameters.Stats.IsSteeringSuppressed ? 0f : Mathf.Clamp(key, -1, 1)
                * parameters.TurnMultiplier * parameters.Stats.FinalSteeringSign;
            BuddahLocomotionStep.Compute(state.Forward, 1f, steering, parameters.Stats, out var force, out float torque);
            if (Mathf.Abs(steering) <= 0.001f)
                state.YawRate = Mathf.MoveTowards(state.YawRate, 0f, parameters.TurnDecay * dt);
            state.YawRate = Mathf.Clamp(state.YawRate, -parameters.MaxAngularVelocity, parameters.MaxAngularVelocity);
            state.YawRate = (state.YawRate + torque * parameters.InverseYawInertia * dt)
                * Mathf.Max(0f, 1f - parameters.AngularDrag * dt);
            state.Velocity = (state.Velocity + force / Mathf.Max(0.0001f, parameters.Mass) * dt)
                * Mathf.Max(0f, 1f - parameters.Drag * dt);
            // Coulomb sliding friction on the current flat support surface, distinct from linear drag.
            state.Velocity = Vector3.MoveTowards(state.Velocity, Vector3.zero, parameters.GroundDeceleration * dt);
            state.Position += state.Velocity * dt;
            state.Yaw += state.YawRate * dt;
        }
    }
}
