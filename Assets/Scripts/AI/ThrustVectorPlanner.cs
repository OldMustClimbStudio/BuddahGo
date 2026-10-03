using UnityEngine;

namespace BuddahGo.AI
{
    // Digital steering only. The authoritative predicted motor applies all physical forces.
    //
    // Structure (Docs/single-player/thrust-vector-controller-spec.md):
    //   guidance  -> an analytic anchor thrust angle relative to the velocity direction
    //   rollouts  -> a small set of candidate thrust angles, each driven closed-loop by the
    //                attitude layer through the motion model for a short horizon and scored
    //                with the V5 lateral/speed/progress weights; the sweep of the thrust vector
    //                while the heading turns is therefore paid for explicitly
    //   attitude  -> time-optimal switching curve on the continuous route-relative heading branch
    public sealed class ThrustVectorPlanner : ISteeringPlanner
    {
        internal enum SpeedMode { Accel, Hold, Brake }
        internal struct Guidance
        {
            public SpeedMode Mode;
            public int Side;
            public float Theta, Target;
        }
        private static readonly float[] FixedCandidates = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 170f, -170f };
        private static readonly float[] AnchorOffsets = { 5f, -5f, 10f, -10f };
        // Diagnostic only (EditMode traces): per-candidate thetas/costs are allocated when set.
        internal static bool CaptureCandidates;
        private int _nearSegment = -1, _side;
        private SpeedMode _mode;
        private bool _headingInitialized, _wasRecovering, _hasChosen;
        private float _carHeading, _coordinateOffset, _lastYaw, _lastTangentYaw, _collisionSeconds, _chosenTheta;
        private float[] _candidateThetas, _candidateCosts;
        private int _selectionPhase = -1, _planCounter, _wobbleCounter;
        private float _wobble, _secondTheta;
        private System.Random _random;
        internal float LastWobbleDegrees => _wobble;
        internal bool LastSelectionWasMistake { get; private set; }
        private System.Random Random(AIDifficultyProfile profile)
            => _random ??= new System.Random(profile.NoiseSeed != 0 ? profile.NoiseSeed : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this));
        private static float Gaussian(System.Random random)
        {
            double u = 1.0 - random.NextDouble(), v = random.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * v));
        }
        internal float[] LastCandidateThetas => _candidateThetas;
        internal float[] LastCandidateCosts => _candidateCosts;
        public ForwardSimPlanner.PlanObservation LastObservation { get; private set; }
        internal void NotifyCollision() => _collisionSeconds = .5f;

        internal static int AttitudeKey(float phi, float omega, float beta, float deadband,
            float hysteresis, int previousPhysicalKey)
        {
            float remaining = phi - omega * Mathf.Abs(omega) / (2f * Mathf.Max(.001f, beta));
            float threshold = deadband + (previousPhysicalKey == 0 ? hysteresis : 0f);
            return Mathf.Abs(remaining) < threshold ? 0 : remaining > 0f ? 1 : -1;
        }

        // Analytic anchor: lateral demand fixes |theta| (a*sin theta), the speed band picks the
        // accelerating, holding or braking solution. Constant full thrust cannot hold speed except at 90 degrees.
        internal static Guidance Guide(float lateral, float acceleration, float speed, float pace,
            float margin, float velocityErrorDegrees, SpeedMode previousMode, int previousSide, bool recovering)
        {
            int side = previousSide;
            if (side == 0 || Mathf.Abs(lateral) >= 3f) side = lateral < 0f ? -1 : 1;
            SpeedMode mode = previousMode;
            if (recovering || (speed >= 79f && pace >= 78f) || speed < pace - margin) mode = SpeedMode.Accel;
            else if (speed > pace + margin) mode = SpeedMode.Brake;
            else if (Mathf.Abs(lateral) >= 18f) mode = SpeedMode.Hold;
            float theta = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(lateral) / Mathf.Max(.001f, acceleration)));
            if (mode == SpeedMode.Hold) theta = Mathf.PI * .5f;
            else if (mode == SpeedMode.Brake) theta = Mathf.Clamp(Mathf.PI - theta, 120f * Mathf.Deg2Rad, 178f * Mathf.Deg2Rad);
            if (recovering) theta = 0f;
            return new Guidance { Mode = mode, Side = side, Theta = theta,
                Target = recovering ? 0f : Mathf.Clamp(velocityErrorDegrees + side * theta * Mathf.Rad2Deg, -178f, 178f) };
        }

        internal static float PredictionTime(float provisionalTargetDegrees, float carDegrees, float omega)
            => Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Abs(provisionalTargetDegrees - carDegrees) * Mathf.Deg2Rad / 4.82f)
                + Mathf.Abs(omega) / 4.82f, .3f, 1f);

        internal static float HeadingError(ref float carDegrees, ref float offsetDegrees, float targetDegrees,
            bool recentCollision, out bool reanchored, out float targetCoordinate)
        {
            float logicalCar = carDegrees + offsetDegrees;
            reanchored = Mathf.Abs(logicalCar) > 200f || (recentCollision && Mathf.Abs(logicalCar) > 180f);
            if (reanchored) { carDegrees = Mathf.DeltaAngle(0f, logicalCar); offsetDegrees = 0f; }
            targetCoordinate = targetDegrees - offsetDegrees;
            if (Mathf.Abs(carDegrees) > 360f || Mathf.Abs(targetCoordinate) > 360f)
            {
                float shift = 360f * Mathf.Round(carDegrees / 360f);
                carDegrees -= shift; targetCoordinate -= shift; offsetDegrees += shift;
            }
            return (targetCoordinate - carDegrees) * Mathf.Deg2Rad;
        }

        // Closed-loop rollout of one candidate thrust angle (degrees, signed, relative to the velocity
        // direction). Phase 1 holds the candidate for holdTicks; phase 2 follows the analytic anchor law
        // re-evaluated at every sample, so a candidate is judged by what the first action buys and not by
        // what holding it for the whole horizon would cost. The attitude layer drives the model exactly as
        // the real driver would; the route projection, the continuous route-relative heading and the cost
        // are sampled every sampleTicks.
        internal static float Rollout(float thetaDegrees, MotionState state, in BuddahMotionModel.PreparedMotion prepared,
            IRacingLine line, AIDifficultyProfile profile, float dt, int horizonTicks, int holdTicks, int sampleTicks, int segment,
            float carDegrees, float tangentYawDegrees, float distance, int previousPhysicalKey, int steeringSign,
            float beta, float deadband, float hysteresis, float maxAngle, float acceleration, SpeedMode mode, int side)
        {
            float cost = 0f, length = line.Length;
            var spline = line as SplineRacingLine;
            float velocityYaw = Mathf.Atan2(state.Velocity.x, state.Velocity.z) * Mathf.Rad2Deg;
            float velocityError = Mathf.DeltaAngle(tangentYawDegrees, velocityYaw);
            float target = Mathf.Clamp(velocityError + thetaDegrees, -maxAngle, maxAngle);
            int key = previousPhysicalKey;
            // horizonTicks and sampleTicks are expressed in rollout steps by the caller (coarse substeps).
            for (int t = 0; t < horizonTicks; t++)
            {
                key = AttitudeKey((target - carDegrees) * Mathf.Deg2Rad, state.YawRate, beta, deadband, hysteresis, key);
                float yawBefore = state.Yaw;
                BuddahMotionModel.Step(ref state, prepared, prepared.Control(key * steeringSign));
                carDegrees += (state.Yaw - yawBefore) * Mathf.Rad2Deg; // model yaw is continuous
                if ((t + 1) % sampleTicks != 0) continue;
                var projection = line.Project(state.Position, segment, 6);
                segment = projection.Segment;
                float tangentYaw = (spline != null ? spline.TangentYaw(segment) : Mathf.Atan2(projection.Tangent.x, projection.Tangent.z)) * Mathf.Rad2Deg;
                carDegrees -= Mathf.DeltaAngle(tangentYawDegrees, tangentYaw); tangentYawDegrees = tangentYaw;
                float speedSquared = state.Velocity.sqrMagnitude, speed = Mathf.Sqrt(speedSquared);
                if (speedSquared > 25f)
                {
                    velocityYaw = Mathf.Atan2(state.Velocity.x, state.Velocity.z) * Mathf.Rad2Deg;
                    velocityError = Mathf.DeltaAngle(tangentYaw, velocityYaw);
                }
                float lateral = projection.Lateral - profile.LateralOffset;
                // Cross(up, tangent) = (tangent.z, 0, -tangent.x)
                float crossSpeed = state.Velocity.x * projection.Tangent.z - state.Velocity.z * projection.Tangent.x;
                float wallLoss = 0f, lateralCost;
                bool corridor = profile.UseWallCorridor && spline != null && spline.HasWalls;
                if (corridor)
                {
                    // Frictionless wall: beyond the wall (minus the car margin) the outward normal velocity is
                    // removed and the position is pulled back to the wall; the tangential speed is kept. This is
                    // the physics the player relies on instead of a brake, so the rollout must see it.
                    float limit = Mathf.Max(1f, (projection.Lateral > 0f ? spline.RightWall(segment) : spline.LeftWall(segment)) - profile.WallMargin);
                    if (Mathf.Abs(projection.Lateral) > limit)
                    {
                        float sign = projection.Lateral > 0f ? 1f : -1f;
                        if (crossSpeed * sign > 0f)
                        {
                            wallLoss = Mathf.Abs(crossSpeed);
                            state.Velocity -= new Vector3(projection.Tangent.z, 0f, -projection.Tangent.x) * crossSpeed;
                            crossSpeed = 0f;
                        }
                        float excess = Mathf.Abs(projection.Lateral) - limit;
                        state.Position -= new Vector3(projection.Tangent.z, 0f, -projection.Tangent.x) * (excess * sign);
                        lateral = sign * limit - profile.LateralOffset;
                    }
                    lateralCost = profile.RolloutCenterWeight * lateral * lateral + profile.RolloutWallWeight * wallLoss * wallLoss;
                }
                else lateralCost = profile.RolloutLateralWeight * lateral * lateral;
                float forwardSpeed = state.Velocity.x * projection.Tangent.x + state.Velocity.z * projection.Tangent.z;
                float pace = Mathf.Min(profile.TargetSpeed, projection.Pace);
                float advance = Mathf.Repeat(projection.Distance - distance + length * .5f, length) - length * .5f;
                distance = projection.Distance;
                float over = Mathf.Max(0f, forwardSpeed - pace), under = Mathf.Max(0f, pace - forwardSpeed);
                float weight = sampleTicks * dt * (t + 1 >= horizonTicks - sampleTicks + 1 ? profile.RolloutTerminalWeight : 1f);
                cost += weight * (lateralCost
                    + profile.RolloutLateralVelocityWeight * crossSpeed * crossSpeed
                    + profile.RolloutOverspeedWeight * over * over + profile.RolloutUnderspeedWeight * under * under)
                    - profile.RolloutProgressWeight * advance;
                if (t + 1 < holdTicks || profile.RolloutTail == 1) { target = Mathf.Clamp(velocityError + thetaDegrees, -maxAngle, maxAngle); continue; }
                if (profile.RolloutTail == 2) { target = Mathf.Clamp(velocityError, -maxAngle, maxAngle); continue; }
                // Phase 2: the anchor law from the rolled-out state (current-position curvature, windowed pace).
                float curvature = spline != null ? spline.CurvatureAtDistance(projection.Distance) : 0f;
                // Precomputed fixed-window minimum pace (SplineRacingLine.WindowPace) keeps the anchor law's
                // anticipation without a window scan per rollout sample.
                float windowPace = Mathf.Min(profile.TargetSpeed, spline != null ? spline.WindowPace(projection.Segment) : projection.Pace);
                float demand = Mathf.Clamp(speed * speed * curvature - profile.PredictionGain * lateral
                    - profile.LateralDamping * crossSpeed, -acceleration, acceleration);
                bool recovering = speed < 5f || Mathf.Abs(velocityError) > 90f;
                var guidance = Guide(demand, acceleration, speed, windowPace, profile.SpeedMargin, velocityError, mode, side, recovering);
                mode = guidance.Mode; side = guidance.Side;
                target = recovering ? 0f : Mathf.Clamp(velocityError + side * guidance.Theta * Mathf.Rad2Deg, -maxAngle, maxAngle);
            }
            return cost;
        }

        public int Plan(MotionState state, MotionParameters parameters, IRacingLine line,
            AIDifficultyProfile profile, float tickDelta, int previousKey)
        {
            float acceleration = Mathf.Max(0f, parameters.Stats.FinalForwardForce / Mathf.Max(.001f, parameters.Mass));
            var spline = line as SplineRacingLine;
            if (spline != null) spline.PreparePace(acceleration, profile.TargetSpeed, profile.ThrustPaceFactor, profile.PlanningBrakeAcceleration);
            var projection = line.Project(state.Position, _nearSegment, 8);
            if (Mathf.Abs(projection.Lateral) > 15f)
                projection = spline != null ? spline.ProjectContinuous(state.Position, _nearSegment, 40)
                    : line.Project(state.Position, _nearSegment, 40);
            _nearSegment = projection.Segment;
            Vector3 tangent = projection.Tangent, routeNormal = Vector3.Cross(Vector3.up, tangent);
            float speed = state.Velocity.magnitude;
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            float yaw = state.Yaw * Mathf.Rad2Deg;
            float velocityYaw = speed > 0f ? Mathf.Atan2(state.Velocity.x, state.Velocity.z) * Mathf.Rad2Deg : tangentYaw;
            float velocityError = Mathf.DeltaAngle(tangentYaw, velocityYaw);
            bool referenceRecovery = speed < 5f || Mathf.Abs(velocityError) > 90f;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _carHeading = Mathf.DeltaAngle(tangentYaw, yaw);
                _lastYaw = yaw; _lastTangentYaw = tangentYaw;
            }
            _carHeading += Mathf.DeltaAngle(_lastYaw, yaw) - Mathf.DeltaAngle(_lastTangentYaw, tangentYaw);
            bool recentCollision = _collisionSeconds > 0f;
            // Apply external reanchoring before estimating a turn on the continuous branch.
            HeadingError(ref _carHeading, ref _coordinateOffset, 0f, recentCollision, out bool reanchored, out _);
            float carLogical = _carHeading + _coordinateOffset;
            _lastYaw = yaw; _lastTangentYaw = tangentYaw;
            _collisionSeconds = Mathf.Max(0f, _collisionSeconds - Mathf.Max(0f, tickDelta));

            // Sign contract: Project's positive e is right of the route, n=Cross(up,tangent),
            // and e_dot=Dot(v,n). Positive a_lat is commanded to the right of velocity.
            // Positive theta adds positive yaw (+Z toward +X). Motor torque follows
            // key*FinalSteeringSign, so multiplying the physical key by the same sign
            // reverses digital input without reversing the requested physical turn.
            float error = projection.Lateral - profile.LateralOffset;
            float lateralVelocity = Vector3.Dot(state.Velocity, routeNormal);
            float curvature = spline != null ? spline.CurvatureAtDistance(projection.Distance) : 0f;
            float curvatureAcceleration = speed * speed * curvature;
            float lookahead = speed * profile.LookaheadSeconds;
            float pace = Mathf.Min(profile.TargetSpeed, spline != null
                ? spline.MinimumPace(projection.Distance, lookahead) : projection.Pace);
            float provisionalLateral = Mathf.Clamp(curvatureAcceleration - profile.PredictionGain * error, -acceleration, acceleration);
            var provisional = Guide(provisionalLateral, acceleration, speed, pace, profile.SpeedMargin,
                velocityError, _mode, _side, referenceRecovery);
            float predictionTime = PredictionTime(provisional.Target, carLogical, state.YawRate);
            float currentTheta = Mathf.DeltaAngle(referenceRecovery ? tangentYaw : velocityYaw, yaw) * Mathf.Deg2Rad;
            float currentLateralAcceleration = acceleration * Mathf.Sin(currentTheta);
            float predictedError = error + lateralVelocity * predictionTime
                + .5f * currentLateralAcceleration * predictionTime * predictionTime;
            float positionCorrection = -profile.PredictionGain * error;
            float velocityCorrection = -profile.PredictionGain * lateralVelocity * predictionTime;
            float accelerationCorrection = -profile.PredictionGain * .5f * currentLateralAcceleration * predictionTime * predictionTime;
            float correction = -profile.PredictionGain * predictedError - profile.LateralDamping * lateralVelocity;
            float rawLateral = curvatureAcceleration + correction;
            float lateral = Mathf.Clamp(rawLateral, -acceleration, acceleration);
            var guidance = Guide(lateral, acceleration, speed, pace, profile.SpeedMargin,
                velocityError, _mode, _side, referenceRecovery);
            float anchorTheta = guidance.Side * guidance.Theta * Mathf.Rad2Deg;

            int sign = parameters.Stats.FinalSteeringSign < 0f ? -1 : 1;
            float beta = parameters.TurnDecay, deadband = profile.AttitudeDeadbandDegrees * Mathf.Deg2Rad,
                hysteresis = profile.AttitudeHysteresisDegrees * Mathf.Deg2Rad, maxAngle = profile.MaxThrustAngleDegrees;
            float chosenTheta = anchorTheta, bestCost = float.PositiveInfinity, anchorCost = float.PositiveInfinity, secondCost = float.PositiveInfinity;
            int candidateCount = 0;
            LastSelectionWasMistake = false;
            // Rollout selection runs every ThrustReplanTicks (staggered across instances so five AI do not
            // select on the same tick); the attitude layer below still runs every tick on the retained angle.
            int period = Mathf.Max(1, profile.ThrustReplanTicks);
            if (_selectionPhase < 0) _selectionPhase = (int)(((uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)) % (uint)period);
            bool select = !_hasChosen || ((_planCounter + _selectionPhase) % period) == 0;
            _planCounter++;
            if (!referenceRecovery && profile.RolloutSeconds > 0f && select)
            {
                // Rollouts integrate with coarse substeps (RolloutSubsteps ticks per model step): the model is
                // Euler either way and the candidates are only ranked, so a 33 ms step halves the dominant cost.
                int substeps = Mathf.Max(1, profile.RolloutSubsteps);
                float rolloutDelta = tickDelta * substeps;
                var prepared = new BuddahMotionModel.PreparedMotion(parameters, rolloutDelta);
                int horizon = Mathf.Max(1, Mathf.RoundToInt(profile.RolloutSeconds / Mathf.Max(.0001f, rolloutDelta)));
                int sampleTicks = Mathf.Clamp(Mathf.Max(1, profile.RolloutSampleTicks / substeps), 1, horizon);
                int hold = Mathf.Clamp(Mathf.RoundToInt(profile.RolloutHoldSeconds / Mathf.Max(.0001f, rolloutDelta)), sampleTicks, horizon);
                float reference = _hasChosen ? _chosenTheta : anchorTheta;
                int memory = _hasChosen ? 2 : 1, extra = memory + AnchorOffsets.Length;
                _candidateThetas = null; _candidateCosts = null;
                float[] thetas = CaptureCandidates ? new float[FixedCandidates.Length + extra] : null, costs = thetas != null ? new float[thetas.Length] : null;
                for (int c = 0; c < FixedCandidates.Length + extra; c++)
                {
                    float theta = c == 0 ? anchorTheta : c == 1 && _hasChosen ? _chosenTheta
                        : c < extra ? anchorTheta + AnchorOffsets[c - memory] : FixedCandidates[c - extra];
                    theta = Mathf.Clamp(theta, -maxAngle, maxAngle);
                    float cost = Rollout(theta, state, prepared, line, profile, rolloutDelta, horizon, hold, sampleTicks,
                        projection.Segment, carLogical, tangentYaw, projection.Distance, previousKey * sign, sign,
                        beta, deadband, hysteresis, maxAngle, acceleration, _mode, _side)
                        + profile.SwitchPenaltyPerDegree * Mathf.Abs(theta - reference);
                    if (thetas != null) { thetas[c] = theta; costs[c] = cost; }
                    candidateCount++;
                    if (c == 0) anchorCost = cost;
                    if (cost < bestCost) { secondCost = bestCost; _secondTheta = chosenTheta; bestCost = cost; chosenTheta = theta; }
                    else if (cost < secondCost) { secondCost = cost; _secondTheta = theta; }
                }
                _candidateThetas = thetas; _candidateCosts = costs;
                // Difficulty randomness: an occasional second-best pick is a human-like misjudgement that
                // lasts one selection period, never an unsafe or impossible command.
                if (profile.MistakeProbability > 0f && !float.IsPositiveInfinity(secondCost) && Random(profile).NextDouble() < profile.MistakeProbability)
                { chosenTheta = _secondTheta; LastSelectionWasMistake = true; }
            }
            // Slow Gaussian wobble on the thrust angle (resampled every WobbleTicks): line-holding gets
            // visibly less precise without per-tick jitter.
            if (profile.AngleNoiseDegrees > 0f && !referenceRecovery)
            {
                if (_wobbleCounter++ % Mathf.Max(1, profile.WobbleTicks) == 0) _wobble = Mathf.Clamp(Gaussian(Random(profile)) * profile.AngleNoiseDegrees, -3f * profile.AngleNoiseDegrees, 3f * profile.AngleNoiseDegrees);
                chosenTheta = Mathf.Clamp(chosenTheta + _wobble, -maxAngle, maxAngle);
            }
            if (!referenceRecovery && !select) { chosenTheta = _chosenTheta; bestCost = float.NaN; anchorCost = float.NaN; }
            if (!referenceRecovery)
            {
                guidance.Target = Mathf.Clamp(velocityError + chosenTheta, -maxAngle, maxAngle);
                guidance.Theta = Mathf.Abs(chosenTheta) * Mathf.Deg2Rad;
                guidance.Side = chosenTheta < 0f ? -1 : chosenTheta > 0f ? 1 : guidance.Side;
                _chosenTheta = chosenTheta; _hasChosen = true;
            }
            else { _hasChosen = false; chosenTheta = 0f; }
            float phi = HeadingError(ref _carHeading, ref _coordinateOffset, guidance.Target,
                false, out _, out float targetCoordinate);
            float desiredYaw = (tangentYaw + guidance.Target) * Mathf.Deg2Rad;
            float remaining = phi - state.YawRate * Mathf.Abs(state.YawRate) / (2f * Mathf.Max(.001f, parameters.TurnDecay));
            int key = parameters.Stats.IsRooted || parameters.Stats.IsSteeringSuppressed || parameters.TurnMultiplier == 0f ? 0
                : sign * AttitudeKey(phi, state.YawRate, parameters.TurnDecay, deadband, hysteresis, previousKey * sign);
            bool recovering = referenceRecovery || reanchored;
            LastObservation = new ForwardSimPlanner.PlanObservation {
                start = state, parameters = parameters, target = projection.Point, tangent = tangent,
                segment = projection.Segment, progress = projection.Distance, lateral = projection.Lateral, pace = pace,
                selectedKey = key, desiredYaw = desiredYaw,
                desiredAcceleration = new Vector3(Mathf.Sin(desiredYaw), 0f, Mathf.Cos(desiredYaw)) * acceleration,
                headingError = phi, thrustAngle = guidance.Side * guidance.Theta, requestedLateralAcceleration = lateral,
                brakingBranch = !referenceRecovery && Mathf.Abs(chosenTheta) > 90f,
                holdingBranch = !referenceRecovery && Mathf.Abs(chosenTheta) == 90f,
                routeHeadingErrorDegrees = _carHeading, headingCoordinateOffsetDegrees = _coordinateOffset,
                velocityHeadingErrorDegrees = velocityError, targetHeadingErrorDegrees = targetCoordinate,
                boundedTargetHeadingDegrees = guidance.Target, recoveryBranch = recovering,
                recentCollision = recentCollision, backwardsRecovery = referenceRecovery, attitudeRemaining = remaining,
                velocityYaw = velocityYaw * Mathf.Deg2Rad, lateralVelocity = lateralVelocity,
                predictedLateral = predictedError, predictionTime = predictionTime,
                curvatureAcceleration = curvatureAcceleration, positionCorrection = positionCorrection,
                velocityCorrection = velocityCorrection, accelerationCorrection = accelerationCorrection,
                lateralCorrection = correction, rawLateralAcceleration = rawLateral,
                currentLateralAcceleration = currentLateralAcceleration, provisionalTargetDegrees = provisional.Target,
                side = guidance.Side, speedMode = (int)guidance.Mode, previousSpeedMode = (int)_mode,
                sideChanged = _side != 0 && guidance.Side != _side, modeChanged = guidance.Mode != _mode,
                recoveryChanged = recovering != _wasRecovering, reanchored = reanchored,
                anchorThetaDegrees = anchorTheta, chosenThetaDegrees = chosenTheta,
                anchorRolloutCost = anchorCost, bestRolloutCost = bestCost, rolloutCandidates = candidateCount,
                observedYaw = state.Yaw, observedYawRate = state.YawRate, previousKey = previousKey,
                turnMultiplier = parameters.TurnMultiplier, inverseYawInertia = parameters.InverseYawInertia,
                turnTorque = parameters.Stats.FinalTurnTorque, turnDecay = parameters.TurnDecay,
                angularDrag = parameters.AngularDrag, maxAngularVelocity = parameters.MaxAngularVelocity };
            _side = guidance.Side; _mode = guidance.Mode; _wasRecovering = recovering;
            return key;
        }
    }
}
