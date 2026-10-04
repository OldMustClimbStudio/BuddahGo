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
        // Valid for one Plan call only: modifiers, support friction and tick delta are resampled next plan.
        internal readonly struct PreparedMotion
        {
            internal readonly MotionParameters Parameters;
            internal readonly float Delta, Cap, ForwardForce, TurnDecay, AngularDamping, LinearDamping, MassDenominator, GroundDeceleration;
            private readonly float _left, _neutral, _right, _leftTorque, _neutralTorque, _rightTorque;
            internal PreparedMotion(in MotionParameters parameters, float dt)
            {
                Parameters = parameters; Delta = dt;
                Cap = parameters.Stats.FinalMaxSpeed + (parameters.Stats.IsPushGraceActive ? parameters.PushExtraSpeed : 0f);
                ForwardForce = parameters.Stats.FinalForwardForce * 1f;
                TurnDecay = parameters.TurnDecay * dt;
                AngularDamping = Mathf.Max(0f, 1f - parameters.AngularDrag * dt);
                LinearDamping = Mathf.Max(0f, 1f - parameters.Drag * dt);
                MassDenominator = Mathf.Max(.0001f, parameters.Mass);
                GroundDeceleration = parameters.GroundDeceleration * dt;
                _left = parameters.Stats.IsSteeringSuppressed ? 0f : -1 * parameters.TurnMultiplier * parameters.Stats.FinalSteeringSign;
                _neutral = parameters.Stats.IsSteeringSuppressed ? 0f : 0 * parameters.TurnMultiplier * parameters.Stats.FinalSteeringSign;
                _right = parameters.Stats.IsSteeringSuppressed ? 0f : 1 * parameters.TurnMultiplier * parameters.Stats.FinalSteeringSign;
                BuddahLocomotionStep.Compute(Vector3.forward, 1f, _left, parameters.Stats, out _, out _leftTorque);
                BuddahLocomotionStep.Compute(Vector3.forward, 1f, _neutral, parameters.Stats, out _, out _neutralTorque);
                BuddahLocomotionStep.Compute(Vector3.forward, 1f, _right, parameters.Stats, out _, out _rightTorque);
            }
            internal PreparedControl Control(int key)
            {
                int index = Mathf.Clamp(key, -1, 1) + 1;
                return new PreparedControl(Steering(index), Torque(index));
            }
            private float Steering(int index) => index == 0 ? _left : index == 1 ? _neutral : _right;
            private float Torque(int index) => index == 0 ? _leftTorque : index == 1 ? _neutralTorque : _rightTorque;
        }
        internal readonly struct PreparedControl
        {
            internal readonly float Torque;
            internal readonly bool Decay;
            internal PreparedControl(float steering, float torque)
            { Torque = torque; Decay = Mathf.Abs(steering) <= .001f; }
        }
        public static void Step(ref MotionState state, in MotionParameters parameters, int key, float dt)
        {
            var prepared = new PreparedMotion(parameters, dt);
            Step(ref state, prepared, prepared.Control(key));
        }
        internal static void Step(ref MotionState state, in PreparedMotion prepared, in PreparedControl control)
        {
            ref readonly MotionParameters parameters = ref prepared.Parameters;
            float dt = prepared.Delta;
            if (parameters.Stats.IsRooted) { state.Velocity = Vector3.zero; state.YawRate = 0f; return; }
            float cap = prepared.Cap;
            state.Velocity = Vector3.ClampMagnitude(state.Velocity, cap);
            float torque = control.Torque;
            Vector3 force = state.Forward * prepared.ForwardForce;
            if (control.Decay)
                state.YawRate = Mathf.MoveTowards(state.YawRate, 0f, prepared.TurnDecay);
            state.YawRate = Mathf.Clamp(state.YawRate, -parameters.MaxAngularVelocity, parameters.MaxAngularVelocity);
            state.YawRate = (state.YawRate + torque * parameters.InverseYawInertia * dt)
                * prepared.AngularDamping;
            state.Velocity = (state.Velocity + force / prepared.MassDenominator * dt)
                * prepared.LinearDamping;
            // Coulomb sliding friction on the current flat support surface, distinct from linear drag.
            state.Velocity = Vector3.MoveTowards(state.Velocity, Vector3.zero, prepared.GroundDeceleration);
            state.Position += state.Velocity * dt;
            state.Yaw += state.YawRate * dt;
        }
    }
}
