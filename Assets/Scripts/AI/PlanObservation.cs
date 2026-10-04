using UnityEngine;

namespace BuddahGo.AI
{
    // One planner decision as the driver and the evidence harness see it. Field names are the JSON
    // schema of the committed plan traces, so keep them stable.
    [System.Serializable]
    public struct PlanObservation
    {
        public MotionState start;
        public MotionParameters parameters;
        public Vector3 target, tangent;
        public int segment, selectedKey;
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
        public float observedYaw, observedYawRate, turnMultiplier, inverseYawInertia, turnTorque, turnDecay, angularDrag, maxAngularVelocity;
        public int previousKey;
    }
}
