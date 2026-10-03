// Frozen revision five for exact seam regression capture. Test assembly only.
using UnityEngine;

namespace BuddahGo.AI
{
    // Closed-loop guidance. Only returns digital input; the authoritative motor applies all forces.
    public sealed class ThrustVectorRevisionFiveFixture : ISteeringPlanner
    {
        private int _nearSegment = -1;
        private bool _braking;
        private int _turnSign;
        private bool _headingInitialized;
        private float _carRouteHeading, _lastYaw, _lastTangentYaw;

        internal static float BranchHeadingError(ref float carErrorDegrees, float yawChangeDegrees,
            float tangentChangeDegrees, float targetErrorDegrees)
        {
            carErrorDegrees += yawChangeDegrees - tangentChangeDegrees;
            if (Mathf.Abs(carErrorDegrees) > 180f) carErrorDegrees = Mathf.DeltaAngle(0f, carErrorDegrees);
            return (Mathf.DeltaAngle(0f, targetErrorDegrees) - carErrorDegrees) * Mathf.Deg2Rad;
        }
        public ForwardSimPlanner.PlanObservation LastObservation { get; private set; }

        internal static int AttitudeKey(float phi, float omega, float beta, float deadband,
            float hysteresis, int previousPhysicalKey)
        {
            float remaining = phi - omega * Mathf.Abs(omega) / (2f * Mathf.Max(.001f, beta));
            float threshold = deadband + (previousPhysicalKey == 0 ? hysteresis : 0f);
            return Mathf.Abs(remaining) <= threshold ? 0 : remaining > 0f ? 1 : -1;
        }

        internal static bool IsHolding(float speed, float pace, float margin, float lateral)
            => Mathf.Abs(speed - pace) <= margin && Mathf.Abs(lateral) >= 18f;

        internal static float GuidanceAngle(float lateral, float acceleration, float speed, float pace,
            float cap, float margin, ref bool braking, ref int turnSign)
        {
            if ((speed >= cap - .1f && pace >= cap - margin) || speed < pace - margin) braking = false;
            else if (speed > pace + margin) braking = true;
            // Schmitt sign: retain direction near zero, but follow a sustained opposite
            // lateral demand. Locking an entire braking interval can steer away from the route.
            if (turnSign == 0 || lateral * turnSign <= -3f)
                turnSign = lateral < 0f ? -1 : 1;
            float angle = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(lateral) / Mathf.Max(.001f, acceleration)));
            if (IsHolding(speed, pace, margin, lateral)) angle = Mathf.PI * .5f;
            else if (braking) angle = Mathf.Clamp(Mathf.PI - angle, 120f * Mathf.Deg2Rad, 178f * Mathf.Deg2Rad);
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
            float pace = profile.CorneringFactor > 0f ? Mathf.Min(profile.TargetSpeed,
                spline != null ? spline.MinimumPace(projection.Distance, lookahead) : projection.Pace) : profile.TargetSpeed;
            float cap = parameters.Stats.FinalMaxSpeed + (parameters.Stats.IsPushGraceActive ? parameters.PushExtraSpeed : 0f);
            float curvature = spline != null ? spline.CurvatureAtDistance(projection.Distance) : 0f;
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
            float yawDegrees = state.Yaw * Mathf.Rad2Deg;
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _carRouteHeading = Mathf.DeltaAngle(tangentYaw, yawDegrees);
                _lastYaw = yawDegrees; _lastTangentYaw = tangentYaw;
            }
            // Rigidbody Euler wraps at 360; at the normal tick rate each physical yaw step
            // is far below 180. Only this observed step and the tangent step are unwrapped.
            float phi = BranchHeadingError(ref _carRouteHeading, Mathf.DeltaAngle(_lastYaw, yawDegrees),
                Mathf.DeltaAngle(_lastTangentYaw, tangentYaw), desiredYaw * Mathf.Rad2Deg - tangentYaw);
            _lastYaw = yawDegrees; _lastTangentYaw = tangentYaw;
            int sign = parameters.Stats.FinalSteeringSign < 0f ? -1 : 1;
            int key = parameters.Stats.IsRooted || parameters.Stats.IsSteeringSuppressed || parameters.TurnMultiplier == 0f ? 0
                : sign * AttitudeKey(phi, state.YawRate, parameters.TurnDecay,
                    profile.AttitudeDeadbandDegrees * Mathf.Deg2Rad, profile.AttitudeHysteresisDegrees * Mathf.Deg2Rad, previousKey * sign);
            LastObservation = new ForwardSimPlanner.PlanObservation {
                start = state, parameters = parameters, target = projection.Point, tangent = tangent,
                segment = projection.Segment, progress = projection.Distance, lateral = projection.Lateral, pace = pace,
                selectedKey = key, desiredYaw = desiredYaw, desiredAcceleration = desired, headingError = phi,
                brakingBranch = _braking && !IsHolding(speed, pace, profile.SpeedMargin, lateral),
                holdingBranch = IsHolding(speed, pace, profile.SpeedMargin, lateral),
                thrustAngle = theta, requestedLateralAcceleration = lateral,
                routeHeadingErrorDegrees = _carRouteHeading,
                observedYaw = state.Yaw, observedYawRate = state.YawRate, previousKey = previousKey,
                turnMultiplier = parameters.TurnMultiplier, inverseYawInertia = parameters.InverseYawInertia,
                turnTorque = parameters.Stats.FinalTurnTorque, turnDecay = parameters.TurnDecay,
                angularDrag = parameters.AngularDrag, maxAngularVelocity = parameters.MaxAngularVelocity };
            return key;
        }
    }
}
