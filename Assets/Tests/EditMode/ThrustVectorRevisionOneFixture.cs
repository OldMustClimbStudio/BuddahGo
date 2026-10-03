// Frozen first revision, used only to reproduce the recorded t=2s failure fixture.
using UnityEngine;

namespace BuddahGo.AI
{
    // Closed-loop guidance. Only returns digital input; the authoritative motor applies all forces.
    internal sealed class ThrustVectorRevisionOneFixture : ISteeringPlanner
    {
        private int _nearSegment = -1;
        private bool _braking;
        private int _turnSign;
        public ForwardSimPlanner.PlanObservation LastObservation { get; private set; }

        internal static int AttitudeKey(float phi, float omega, float beta, float deadband,
            float hysteresis, int previousPhysicalKey)
        {
            float remaining = phi - omega * Mathf.Abs(omega) / (2f * Mathf.Max(.001f, beta));
            float threshold = deadband + (previousPhysicalKey == 0 ? hysteresis : 0f);
            return Mathf.Abs(remaining) <= threshold ? 0 : remaining > 0f ? 1 : -1;
        }

        internal static float GuidanceAngle(float lateral, float acceleration, float speed, float pace,
            float cap, float margin, ref bool braking, ref int turnSign)
        {
            bool wasBraking = braking;
            if (speed >= cap - .1f || speed < pace - margin) braking = false;
            else if (speed > pace + margin) braking = true;
            // Latch direction through a braking maneuver: exchanging +150/-150 each tick
            // would reintroduce the shortest-angle ambiguity. In the accelerating branch,
            // lateral demand passes through zero before reversing, with a small sign deadband.
            if (turnSign == 0 || (!braking || !wasBraking) && Mathf.Abs(lateral) > .25f)
                turnSign = lateral < 0f ? -1 : 1;
            float angle = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(lateral) / Mathf.Max(.001f, acceleration)));
            if (braking) angle = Mathf.Min(150f * Mathf.Deg2Rad, Mathf.PI - angle);
            return turnSign * angle;
        }

        public int Plan(MotionState state, MotionParameters parameters, IRacingLine line,
            AIDifficultyProfile profile, float tickDelta, int previousKey)
        {
            float acceleration = Mathf.Max(0f, parameters.Stats.FinalForwardForce / Mathf.Max(.001f, parameters.Mass));
            var spline = line as SplineRacingLine;
            if (spline != null) spline.PreparePace(acceleration, profile.TargetSpeed, profile.CorneringFactor);
            var projection = spline != null ? spline.ProjectSmall(state.Position, _nearSegment)
                : line.Project(state.Position, _nearSegment, 3);
            if ((state.Position - projection.Point).sqrMagnitude > 6400f) projection = line.Project(state.Position, -1);
            _nearSegment = projection.Segment;
            Vector3 tangent = projection.Tangent, routeNormal = Vector3.Cross(Vector3.up, tangent);
            float speed = state.Velocity.magnitude;
            Vector3 velocityDirection = speed > 1f ? state.Velocity / speed : tangent;
            float lookahead = Mathf.Max(2f, speed * profile.LookaheadSeconds);
            var ahead = spline != null ? spline.SampleDistance(projection.Distance + lookahead) : projection;
            float pace = profile.CorneringFactor > 0f ? Mathf.Min(profile.TargetSpeed, ahead.Pace) : profile.TargetSpeed;
            float cap = parameters.Stats.FinalMaxSpeed + (parameters.Stats.IsPushGraceActive ? parameters.PushExtraSpeed : 0f);
            float curvature = spline != null ? spline.CurvatureAtDistance(projection.Distance + lookahead) : 0f;
            float lateralError = projection.Lateral - profile.LateralOffset;
            // Error and its rate are relative to the route. The resulting signed acceleration
            // is commanded normal to velocity; dot(velocity, velocityNormal) would always be zero.
            float lateralSpeed = Vector3.Dot(state.Velocity, routeNormal);
            float lateral = Mathf.Clamp(speed * speed * curvature - profile.LateralGain * lateralError
                - profile.LateralDamping * lateralSpeed, -acceleration, acceleration);
            float theta = GuidanceAngle(lateral, acceleration, speed, pace, cap,
                profile.SpeedMargin, ref _braking, ref _turnSign);
            float desiredYaw = Mathf.Atan2(velocityDirection.x, velocityDirection.z) + theta;
            Vector3 desired = new Vector3(Mathf.Sin(desiredYaw), 0f, Mathf.Cos(desiredYaw)) * acceleration;
            float phi = Mathf.DeltaAngle(state.Yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            int sign = parameters.Stats.FinalSteeringSign < 0f ? -1 : 1;
            int key = parameters.Stats.IsRooted || parameters.Stats.IsSteeringSuppressed || parameters.TurnMultiplier == 0f ? 0
                : sign * AttitudeKey(phi, state.YawRate, parameters.TurnDecay,
                    profile.AttitudeDeadbandDegrees * Mathf.Deg2Rad, profile.AttitudeHysteresisDegrees * Mathf.Deg2Rad, previousKey * sign);
            LastObservation = new ForwardSimPlanner.PlanObservation {
                start = state, parameters = parameters, target = projection.Point, tangent = tangent,
                segment = projection.Segment, progress = projection.Distance, lateral = projection.Lateral, pace = pace,
                selectedKey = key, desiredYaw = desiredYaw, desiredAcceleration = desired, headingError = phi,
                brakingBranch = _braking, thrustAngle = theta, requestedLateralAcceleration = lateral,
                observedYaw = state.Yaw, observedYawRate = state.YawRate, previousKey = previousKey,
                turnMultiplier = parameters.TurnMultiplier, inverseYawInertia = parameters.InverseYawInertia,
                turnTorque = parameters.Stats.FinalTurnTorque, turnDecay = parameters.TurnDecay,
                angularDrag = parameters.AngularDrag, maxAngularVelocity = parameters.MaxAngularVelocity };
            return key;
        }
    }
}
