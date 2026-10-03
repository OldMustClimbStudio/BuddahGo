// Frozen pre-spec revision six; historical replay only.
using UnityEngine;

namespace BuddahGo.AI
{
    // Closed-loop guidance. Only returns digital input; the authoritative motor applies all forces.
    public sealed class ThrustVectorRevisionSixFixture : ISteeringPlanner
    {
        private int _nearSegment = -1;
        private bool _braking;
        private int _turnSign;
        private bool _headingInitialized;
        private float _lastYaw, _lastTangentYaw, _collisionSeconds;
        private HeadingBranchState _heading;

        // Offset records a shared coordinate translation, not a physical heading reset.
        internal struct HeadingBranchState { public float Car, Offset; }
        internal void NotifyCollision() => _collisionSeconds = .5f;

        internal static float BranchHeadingError(ref HeadingBranchState state, float yawChangeDegrees,
            float tangentChangeDegrees, float velocityErrorDegrees, float thetaDegrees, bool recentCollision,
            out float targetDegrees, out bool recovering)
        {
            state.Car += yawChangeDegrees - tangentChangeDegrees;
            float logicalCar = state.Car + state.Offset;
            bool backwards = Mathf.Abs(velocityErrorDegrees) > 90f;
            float target = backwards ? 0f : Mathf.Clamp(velocityErrorDegrees + thetaDegrees, -178f, 178f);
            recovering = backwards || Mathf.Abs(logicalCar) > 200f
                || (recentCollision && Mathf.Abs(logicalCar) > 180f);
            if (recovering)
            {
                // Recovery alone can choose the shortest arc. Normal side changes retain
                // the tangent-facing branch, even when phi is close to +/-360 degrees.
                state.Car = target - Mathf.DeltaAngle(logicalCar, target);
                state.Offset = 0f;
            }
            targetDegrees = target - state.Offset;
            if (Mathf.Abs(state.Car) > 180f)
            {
                float shift = 360f * Mathf.Floor((state.Car + 180f) / 360f);
                state.Car -= shift; targetDegrees -= shift; state.Offset += shift;
            }
            return (targetDegrees - state.Car) * Mathf.Deg2Rad;
        }

        // Existing recorded fixtures supply an already route-relative target.
        internal static float BranchHeadingError(ref float carErrorDegrees, float yawChangeDegrees,
            float tangentChangeDegrees, float targetErrorDegrees)
        {
            var state = new HeadingBranchState { Car = carErrorDegrees };
            float phi = BranchHeadingError(ref state, yawChangeDegrees, tangentChangeDegrees,
                0f, targetErrorDegrees, false, out _, out _);
            carErrorDegrees = state.Car + state.Offset;
            return phi;
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
            // Sign contract (Project -> guidance -> yaw -> motor): Project defines positive
            // lateral to the route's right, n = Cross(up, tangent); e_dot = Dot(v, n).
            // Positive a_lat means rightward demand, built from route-normal errors but
            // commanded along Cross(up, velocityDirection). Positive theta adds positive
            // yaw (forward +Z turns toward +X); a car at x=+10 on a +Z straight needs
            // negative a_lat/theta. Motor yaw torque follows key * FinalSteeringSign,
            // so the planner multiplies the desired physical key by that same sign.
            // Inverting FinalSteeringSign reverses the digital key, not the physical turn.
            float lateralSpeed = Vector3.Dot(state.Velocity, routeNormal);
            float lateral = Mathf.Clamp(speed * speed * curvature - profile.LateralGain * lateralError
                - profile.LateralDamping * lateralSpeed, -acceleration, acceleration);
            float theta = GuidanceAngle(lateral, acceleration, speed, pace, cap,
                profile.SpeedMargin, ref _braking, ref _turnSign);
            float yawDegrees = state.Yaw * Mathf.Rad2Deg;
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _heading.Car = Mathf.DeltaAngle(tangentYaw, yawDegrees);
                _lastYaw = yawDegrees; _lastTangentYaw = tangentYaw;
            }
            // Rigidbody Euler wraps at 360; at the normal tick rate each physical yaw step
            // is far below 180. Only this observed step and the tangent step are unwrapped.
            float velocityError = Mathf.DeltaAngle(tangentYaw,
                Mathf.Atan2(velocityDirection.x, velocityDirection.z) * Mathf.Rad2Deg);
            bool recentCollision = _collisionSeconds > 0f;
            float phi = BranchHeadingError(ref _heading, Mathf.DeltaAngle(_lastYaw, yawDegrees),
                Mathf.DeltaAngle(_lastTangentYaw, tangentYaw), velocityError, theta * Mathf.Rad2Deg,
                recentCollision, out float targetError, out bool recovering);
            _collisionSeconds = Mathf.Max(0f, _collisionSeconds - Mathf.Max(0f, tickDelta));
            float boundedTarget = targetError + _heading.Offset;
            float desiredYaw = (tangentYaw + boundedTarget) * Mathf.Deg2Rad;
            Vector3 desired = new Vector3(Mathf.Sin(desiredYaw), 0f, Mathf.Cos(desiredYaw)) * acceleration;
            _lastYaw = yawDegrees; _lastTangentYaw = tangentYaw;
            int sign = parameters.Stats.FinalSteeringSign < 0f ? -1 : 1;
            int key = parameters.Stats.IsRooted || parameters.Stats.IsSteeringSuppressed || parameters.TurnMultiplier == 0f ? 0
                : sign * AttitudeKey(phi, state.YawRate, parameters.TurnDecay,
                    profile.AttitudeDeadbandDegrees * Mathf.Deg2Rad, profile.AttitudeHysteresisDegrees * Mathf.Deg2Rad, previousKey * sign);
            LastObservation = new ForwardSimPlanner.PlanObservation {
                start = state, parameters = parameters, target = projection.Point, tangent = tangent,
                segment = projection.Segment, progress = projection.Distance, lateral = projection.Lateral, pace = pace,
                selectedKey = key, desiredYaw = desiredYaw, desiredAcceleration = desired, headingError = phi,
                brakingBranch = !recovering && _braking && !IsHolding(speed, pace, profile.SpeedMargin, lateral),
                holdingBranch = !recovering && IsHolding(speed, pace, profile.SpeedMargin, lateral),
                thrustAngle = theta, requestedLateralAcceleration = lateral,
                routeHeadingErrorDegrees = _heading.Car, headingCoordinateOffsetDegrees = _heading.Offset,
                velocityHeadingErrorDegrees = velocityError, targetHeadingErrorDegrees = targetError,
                boundedTargetHeadingDegrees = boundedTarget, recoveryBranch = recovering,
                recentCollision = recentCollision, backwardsRecovery = Mathf.Abs(velocityError) > 90f,
                attitudeRemaining = phi - state.YawRate * Mathf.Abs(state.YawRate) / (2f * Mathf.Max(.001f, parameters.TurnDecay)),
                observedYaw = state.Yaw, observedYawRate = state.YawRate, previousKey = previousKey,
                turnMultiplier = parameters.TurnMultiplier, inverseYawInertia = parameters.InverseYawInertia,
                turnTorque = parameters.Stats.FinalTurnTorque, turnDecay = parameters.TurnDecay,
                angularDrag = parameters.AngularDrag, maxAngularVelocity = parameters.MaxAngularVelocity };
            return key;
        }
    }
}
