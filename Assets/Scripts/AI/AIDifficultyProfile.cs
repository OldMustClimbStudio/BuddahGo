using UnityEngine;

namespace BuddahGo.AI
{
    [CreateAssetMenu(menuName = "BuddahGo/AI/Driving profile")]
    public sealed class AIDifficultyProfile : ScriptableObject
    {
        [Header("Controller")]
        public bool UseThrustVector;
        [Min(.05f)] public float LookaheadSeconds = 1f;
        [Min(0f)] public float SpeedMargin = 2f;
        [Min(0f)] public float LateralGain = .1f;
        [Min(0f)] public float LateralDamping = 1f;
        [Min(0f)] public float AttitudeDeadbandDegrees = 1.5f;
        [Min(0f)] public float AttitudeHysteresisDegrees = .5f;
        [Header("Thrust vector specification")]
        [Min(0f)] public float PredictionGain = .15f;
        [Range(0f, 1f)] public float ThrustPaceFactor = .90f;
        [Min(.01f)] public float PlanningBrakeAcceleration = 10f;
        [Header("Thrust vector rollout selection")]
        [Min(0f)] public float RolloutSeconds = 2f;
        [Min(0f)] public float RolloutHoldSeconds = .6f;
        [Range(1, 6)] public int ThrustReplanTicks = 2;
        [Range(1, 4)] public int RolloutSubsteps = 2;
        [Header("Wall corridor (frictionless walls are the vehicle's brakes)")]
        public bool UseWallCorridor;
        [Min(0f)] public float WallMargin = 1.5f;
        [Min(0f)] public float RolloutWallWeight = .2f;
        [Min(0f)] public float RolloutCenterWeight = .02f;
        // What a rollout does after the held candidate: 0 = anchor law (line follower), 1 = keep the
        // candidate angle, 2 = full thrust along the velocity (0 degrees). With walls the anchor law's
        // centreline braking flattens the differences between candidates, so 1 or 2 is expected there.
        [Range(0, 2)] public int RolloutTail;
        [Header("Randomness (per-racer personality and in-race variation)")]
        public int NoiseSeed;                                  // 0 = derive from the instance
        [Min(0f)] public float SpeedNoise;                     // +/- m/s drawn once per racer (applied by AIDifficultyProfiles.Resolve)
        [Min(0f)] public float LateralOffsetNoise;             // +/- m drawn once per racer (applied by AIDifficultyProfiles.Resolve)
        [Min(0f)] public float AngleNoiseDegrees;              // slow Gaussian wobble on the chosen thrust angle
        [Range(1, 120)] public int WobbleTicks = 30;           // resample period of the wobble
        [Range(0f, 1f)] public float MistakeProbability;       // per selection: take the second-best candidate
        [Range(0, 30)] public int ReactionJitterTicks;         // extra random reaction delay
        [Range(1, 30)] public int RolloutSampleTicks = 6;
        [Min(0f)] public float RolloutLateralWeight = 1f;
        [Min(0f)] public float RolloutLateralVelocityWeight = .7f;
        [Min(0f)] public float RolloutOverspeedWeight = 1f;
        [Min(0f)] public float RolloutUnderspeedWeight = 1f;
        [Min(0f)] public float RolloutProgressWeight = 1f;
        [Min(1f)] public float RolloutTerminalWeight = 1f;
        [Min(0f)] public float SwitchPenaltyPerDegree = .3f;
        [Range(90f, 178f)] public float MaxThrustAngleDegrees = 170f;
        public int EffectiveReplanTicks => UseThrustVector ? 1 : Mathf.Max(1, ReplanTicks);
        [Header("Pace (planning preferences, never motor limits)")]
        [Min(1f)] public float TargetSpeed = 80f;
        [Min(0f)] public float SpeedWeight = 1f;
        [Min(0f)] public float ProgressWeight = 5f;
        [Header("Tracking precision")]
        [Min(0.2f)] public float HorizonSeconds = 3f;
        [Min(0.02f)] public float ControlSeconds = 0.15f;
        [Range(1, 64)] public int BeamWidth = 48;
        [Min(0f)] public float LateralWeight = 1f;
        [Min(0f)] public float LateralVelocityWeight = 0.7f;
        public float LateralOffset;
        [Range(0f, 1f)] public float CorneringFactor = 0.8f;
        public bool DiverseSearch = true;
        [Header("Reaction (ticks)")]
        [Range(1, 30)] public int ReplanTicks = 3;
        [Range(0, 30)] public int ReactionTicks;
        // Normal is the selected V5 configuration. Easy/Hard calibration remains outside A2.
        [ContextMenu("Restore Normal (V5)")]
        public void RestoreNormal()
        {
            TargetSpeed = 80f; SpeedWeight = 1f; ProgressWeight = 5f;
            HorizonSeconds = 3f; ControlSeconds = .15f; BeamWidth = 48;
            LateralWeight = 1f; LateralVelocityWeight = .7f; LateralOffset = 0f;
            CorneringFactor = .8f; DiverseSearch = true;
            ReplanTicks = 3; ReactionTicks = 0;
        }

        // Every float must be finite and inside its range; ints have no NaN so only the range applies.
        public void ValidateConfiguration()
        {
            Range(TargetSpeed, 1f); Range(SpeedWeight, 0f); Range(ProgressWeight, 0f); Range(HorizonSeconds, .2f);
            Range(ControlSeconds, .02f, HorizonSeconds); Range(BeamWidth, 1, 64);
            Range(LateralWeight, 0f); Range(LateralVelocityWeight, 0f); Range(LateralOffset); Range(CorneringFactor, 0f, 1f);
            Range(ReplanTicks, 1, 30); Range(ReactionTicks, 0, 30); Range(ReactionJitterTicks, 0);
            Range(LookaheadSeconds, .05f); Range(SpeedMargin, 0f); Range(LateralGain, 0f); Range(LateralDamping, 0f);
            Range(AttitudeDeadbandDegrees, 0f); Range(AttitudeHysteresisDegrees, 0f); Range(PredictionGain, 0f);
            Range(ThrustPaceFactor, 0f, 1f); Range(PlanningBrakeAcceleration, float.Epsilon);
            Range(RolloutSeconds, 0f); Range(RolloutHoldSeconds, 0f); Range(RolloutSampleTicks, 1);
            Range(SwitchPenaltyPerDegree, 0f); Range(MaxThrustAngleDegrees, 90f, 178f);
            Range(SpeedNoise, 0f); Range(LateralOffsetNoise, 0f); Range(AngleNoiseDegrees, 0f); Range(WobbleTicks, 1);
            Range(MistakeProbability, 0f, 1f); Range(WallMargin, 0f);
            Range(RolloutWallWeight, 0f); Range(RolloutCenterWeight, 0f); Range(RolloutLateralWeight, 0f);
            Range(RolloutLateralVelocityWeight, 0f); Range(RolloutOverspeedWeight, 0f); Range(RolloutUnderspeedWeight, 0f);
            Range(RolloutProgressWeight, 0f); Range(RolloutTerminalWeight, 1f);
        }

        private static void Range(float value, float min = float.NegativeInfinity, float max = float.PositiveInfinity)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new System.ArgumentException("AI profile is outside the supported parameter ranges.");
        }

        private static void Range(int value, int min, int max = int.MaxValue)
        {
            if (value < min || value > max)
                throw new System.ArgumentException("AI profile is outside the supported parameter ranges.");
        }
    }
}
