namespace NewBuddah.PredictionV2.Core
{
    // On the reconcile wire (BuddahPredictedReconcileData.ComputedStats): new fields need a protocol bump.
    [System.Serializable]
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
        // A freeze also sets IsRooted and IsSteeringSuppressed; IsFrozen adds the action lock.
        public bool IsFrozen;
        // Burst deadline active and the body is not rooted or frozen (drives burst body impacts).
        public bool IsBurstActive;
        public bool IsMaxSpeedPenaltyActive;
    }
}
