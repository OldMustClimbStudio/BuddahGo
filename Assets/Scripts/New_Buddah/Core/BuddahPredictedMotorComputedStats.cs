namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedMotorComputedStats
    {
        public float FinalForwardForce;
        public float FinalMaxSpeed;
        public float FinalTurnTorque;
        public float FinalSteeringSign;
        public bool IsRooted;
        public bool IsInvertTurnActive;
        public bool IsPushGraceActive;
        public bool IsSteeringSuppressed;
        public bool IsRoomBypassActive;
        public float ScaleMultiplier;
        public float ScaleMassMultiplier;
        public float ScaleForwardForceMultiplier;
    }
}
