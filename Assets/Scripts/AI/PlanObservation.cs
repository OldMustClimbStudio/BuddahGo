using UnityEngine;

namespace BuddahGo.AI
{
    // Shared by every steering planner, the driver and the evidence harness. Field names are the
    // JSON schema of the committed plan traces, so keep them stable.
    public interface ISteeringPlanner
    {
        int Plan(MotionState state, MotionParameters parameters, IRacingLine line,
            AIDifficultyProfile profile, float tickDelta, int previousKey);
        PlanObservation LastObservation { get; }
    }

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
}
