using UnityEngine;

namespace BuddahGo.AI
{
    public interface ISteeringPlanner
    {
        int Plan(MotionState state, MotionParameters parameters, IRacingLine line,
            AIDifficultyProfile profile, float tickDelta, int previousKey);
    }

    // Bounded beam search over actual digital key holds. Each candidate uses the motor's force rules.
    public sealed class ForwardSimPlanner : ISteeringPlanner
    {
        private struct Candidate { public MotionState State; public float Cost, Progress, HeadingError, TangentYaw, MaxHeadingError; public int Segment; }
        private Candidate[] _beam = new Candidate[64], _next = new Candidate[64];
        internal static float AdvanceHeadingError(float error, float yawChange, float oldTangentYaw, float newTangentYaw)
            => error + yawChange - Mathf.DeltaAngle(oldTangentYaw, newTangentYaw);

        private int _nearSegment = -1;
        public PlanObservation LastObservation { get; private set; }
        [System.Serializable]
        public struct PlanObservation
        {
            public MotionState start;
            public MotionParameters parameters;
            public Vector3 target, tangent;
            public int segment, selectedKey, rejectedWinding, viableFirstKeys;
            public float progress, lateral, pace;
            public float desiredYaw, headingError; // Radians. observedYawRate is radians/second.
            public Vector3 desiredAcceleration;
            public bool brakingBranch, holdingBranch, recoveryBranch, recentCollision, backwardsRecovery;
            public float headingCoordinateOffsetDegrees, velocityHeadingErrorDegrees, targetHeadingErrorDegrees;
            public float boundedTargetHeadingDegrees, attitudeRemaining; // Remaining is radians; named headings are degrees.
            public float thrustAngle, requestedLateralAcceleration, routeHeadingErrorDegrees;
            public float velocityYaw, lateralVelocity, predictedLateral, predictionTime;
            public float curvatureAcceleration, positionCorrection, velocityCorrection, accelerationCorrection;
            public float lateralCorrection, rawLateralAcceleration, currentLateralAcceleration, provisionalTargetDegrees;
            public int side, speedMode, previousSpeedMode;
            public bool sideChanged, modeChanged, recoveryChanged, reanchored;
            public float anchorThetaDegrees, chosenThetaDegrees, anchorRolloutCost, bestRolloutCost;
            public int rolloutCandidates;
            public float selectedMaxHeadingError, rejectedWindingBrakingFraction;
            public int expandedCandidates, brakingCandidates, rejectedWindingBraking;
            public float observedYaw, observedYawRate, turnMultiplier, inverseYawInertia, turnTorque, turnDecay, angularDrag, maxAngularVelocity;
            public int previousKey;
            public float neutralCost, leftCost, rightCost, selectedYawChange, selectedYawRate;
        }
        public int Plan(MotionState state, MotionParameters parameters, IRacingLine line,
            AIDifficultyProfile profile, float tickDelta, int previousKey)
        {
            var preparedMotion = new BuddahMotionModel.PreparedMotion(parameters, tickDelta);
            if (profile.CorneringFactor > 0f && line is SplineRacingLine spline)
                spline.PreparePace(parameters.Stats.FinalForwardForce / Mathf.Max(.001f, parameters.Mass), profile.TargetSpeed, profile.CorneringFactor);
            var previousControl = preparedMotion.Control(previousKey);
            for (int i = 0; i < profile.ReactionTicks; i++) BuddahMotionModel.Step(ref state, preparedMotion, previousControl);
            var start = line.Project(state.Position, _nearSegment, 40);
            if (Mathf.Abs(start.Lateral) > 80f) start = line.Project(state.Position, -1);
            _nearSegment = start.Segment;
            int width = Mathf.Clamp(profile.BeamWidth / 3, 1, 64);
            int block = Mathf.Max(1, Mathf.RoundToInt(profile.ControlSeconds / tickDelta));
            int depth = Mathf.Clamp(Mathf.CeilToInt(profile.HorizonSeconds / (block * tickDelta)), 1, 80);
            float bestCost = float.PositiveInfinity;
            int bestKey = 0;
            var observation = new PlanObservation { start = state, parameters = parameters, target = start.Point,
                tangent = start.Tangent, segment = start.Segment, progress = start.Distance, lateral = start.Lateral, pace = start.Pace,
                observedYaw = state.Yaw, observedYawRate = state.YawRate, previousKey = previousKey,
                turnMultiplier = parameters.TurnMultiplier, inverseYawInertia = parameters.InverseYawInertia,
                turnTorque = parameters.Stats.FinalTurnTorque, turnDecay = parameters.TurnDecay,
                angularDrag = parameters.AngularDrag, maxAngularVelocity = parameters.MaxAngularVelocity };
            // Keep a separate beam for each first key: short-term cost must not prune away
            // every early turn/braking option before its later benefit enters the horizon.
            for (int first = 0; first < 3; first++)
            {
                int firstKey = first == 0 ? 0 : first == 1 ? -1 : 1;
                _beam[0] = new Candidate { State = state, Segment = start.Segment, Progress = start.Distance,
                    TangentYaw = Mathf.Atan2(start.Tangent.x, start.Tangent.z) * Mathf.Rad2Deg,
                    HeadingError = Mathf.DeltaAngle(Mathf.Atan2(start.Tangent.x, start.Tangent.z) * Mathf.Rad2Deg, state.Yaw * Mathf.Rad2Deg) };
                _beam[0].MaxHeadingError = Mathf.Abs(_beam[0].HeadingError);
                int count = 1;
                for (int d = 0; d < depth; d++)
                {
                    int nextCount = 0;
                    for (int b = 0; b < count; b++)
                    for (int choice = 0; choice < (d == 0 ? 1 : 3); choice++)
                    {
                        int key = d == 0 ? firstKey : choice == 0 ? 0 : choice == 1 ? -1 : 1;
                        Candidate candidate = _beam[b];
                        float previousYaw = candidate.State.Yaw;
                        var control = preparedMotion.Control(key);
                        for (int tick = 0; tick < block; tick++) BuddahMotionModel.Step(ref candidate.State, preparedMotion, control);
                        var projected = line.Project(candidate.State.Position, candidate.Segment);
                        float tangentYaw = Mathf.Atan2(projected.Tangent.x, projected.Tangent.z) * Mathf.Rad2Deg;
                        candidate.HeadingError = AdvanceHeadingError(candidate.HeadingError,
                            (candidate.State.Yaw - previousYaw) * Mathf.Rad2Deg, candidate.TangentYaw, tangentYaw);
                        candidate.TangentYaw = tangentYaw;
                        candidate.MaxHeadingError = Mathf.Max(candidate.MaxHeadingError, Mathf.Abs(candidate.HeadingError));
                        observation.expandedCandidates++;
                        bool braking = Vector3.Dot(candidate.State.Velocity, projected.Tangent) > projected.Pace;
                        if (braking) observation.brakingCandidates++;
                        // A full winding can have the same endpoint yaw and cheap speed/lateral costs.
                        // Stay on the initial shortest heading branch as the route itself turns.
                        // This prunes plans only; the real motor still receives ordinary digital keys.
                        if (Mathf.Abs(candidate.HeadingError) > 180f) { observation.rejectedWinding++; if (braking) observation.rejectedWindingBraking++; continue; }
                        float advance = Mathf.Repeat(projected.Distance - candidate.Progress + line.Length * 0.5f, line.Length) - line.Length * 0.5f;
                        float crossSpeed = Vector3.Dot(candidate.State.Velocity, Vector3.Cross(Vector3.up, projected.Tangent));
                        float forwardSpeed = Vector3.Dot(candidate.State.Velocity, projected.Tangent);
                        float lateral = projected.Lateral - profile.LateralOffset;
                        float targetSpeed = profile.CorneringFactor > 0f ? Mathf.Min(profile.TargetSpeed, projected.Pace) : profile.TargetSpeed;
                        candidate.Cost += block * tickDelta * (profile.LateralWeight * lateral * lateral
                            + profile.LateralVelocityWeight * crossSpeed * crossSpeed
                            + profile.SpeedWeight * (forwardSpeed - targetSpeed) * (forwardSpeed - targetSpeed))
                            - profile.ProgressWeight * advance;
                        candidate.Progress = projected.Distance; candidate.Segment = projected.Segment;
                        // Preserve distinct future yaw/velocity states instead of filling the beam
                        // with near-identical cheap prefixes. A later counter-steer needs alternatives.
                        if (profile.DiverseSearch)
                        {
                            int similar = -1;
                            for (int n = 0; n < nextCount; n++)
                                if (Mathf.Abs(Mathf.DeltaAngle(_next[n].State.Yaw * Mathf.Rad2Deg, candidate.State.Yaw * Mathf.Rad2Deg)) < 5f
                                    && Mathf.Abs(_next[n].State.YawRate - candidate.State.YawRate) < .1f
                                    && (_next[n].State.Velocity - candidate.State.Velocity).sqrMagnitude < 4f
                                    && (_next[n].State.Position - candidate.State.Position).sqrMagnitude < 4f)
                                { similar = n; break; }
                            if (similar >= 0)
                            {
                                if (_next[similar].Cost <= candidate.Cost) continue;
                                for (int n = similar; n < nextCount - 1; n++) _next[n] = _next[n + 1];
                                nextCount--;
                            }
                        }
                        int insert = nextCount;
                        while (insert > 0 && _next[insert - 1].Cost > candidate.Cost) insert--;
                        if (insert >= width) continue;
                        for (int move = Mathf.Min(nextCount, width - 1); move > insert; move--) _next[move] = _next[move - 1];
                        _next[insert] = candidate; nextCount = Mathf.Min(width, nextCount + 1);
                    }
                    var swap = _beam; _beam = _next; _next = swap; count = nextCount;
                    if (count == 0) break;
                }
                float cost = count > 0 ? _beam[0].Cost : float.MaxValue;
                if (count > 0) observation.viableFirstKeys++;
                if (first == 0) observation.neutralCost = cost;
                else if (first == 1) observation.leftCost = cost;
                else observation.rightCost = cost;
                if (count > 0 && cost < bestCost)
                {
                    bestCost = _beam[0].Cost; bestKey = firstKey;
                    observation.selectedYawChange = (_beam[0].State.Yaw - state.Yaw) * Mathf.Rad2Deg;
                    observation.selectedYawRate = _beam[0].State.YawRate;
                    observation.selectedMaxHeadingError = _beam[0].MaxHeadingError;
                }
            }
            observation.rejectedWindingBrakingFraction = observation.rejectedWinding > 0
                ? (float)observation.rejectedWindingBraking / observation.rejectedWinding : 0f;
            observation.selectedKey = bestKey; LastObservation = observation;
            // If external rotation makes every branch infeasible, release steering so the
            // existing motor turn decay can settle it; never return a stale beam action.
            return bestKey;
        }
    }
}
