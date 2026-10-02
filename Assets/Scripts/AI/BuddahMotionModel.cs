using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Simulation;
using UnityEngine;

namespace BuddahGo.AI
{
    public struct MotionState
    {
        public Vector3 Position, Velocity;
        public float Yaw, YawRate;
        public Vector3 Forward => new Vector3(Mathf.Sin(Yaw), 0f, Mathf.Cos(Yaw));
        public static MotionState Read(Rigidbody body) => new MotionState {
            Position = body.position, Velocity = new Vector3(body.velocity.x, 0f, body.velocity.z),
            Yaw = body.rotation.eulerAngles.y * Mathf.Deg2Rad, YawRate = body.angularVelocity.y };
    }

    public struct MotionParameters
    {
        public BuddahPredictedMotorComputedStats Stats;
        public float Mass, InverseYawInertia, Drag, AngularDrag, MaxAngularVelocity;
        public float TurnDecay, TurnMultiplier, PushExtraSpeed, GroundDeceleration;

        public static float ReadInverseYawInertia(Rigidbody body)
        {
            Vector3 axis = Quaternion.Inverse(body.rotation * body.inertiaTensorRotation) * Vector3.up;
            Vector3 inertia = body.inertiaTensor;
            return axis.x * axis.x / Mathf.Max(inertia.x, 0.0001f)
                 + axis.y * axis.y / Mathf.Max(inertia.y, 0.0001f)
                 + axis.z * axis.z / Mathf.Max(inertia.z, 0.0001f);
        }
    }

    // Planar, collision-free prediction. Never writes to the real Rigidbody.
    public static class BuddahMotionModel
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
