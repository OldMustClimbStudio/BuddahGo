using UnityEngine;

namespace BuddahGo.AI
{
    [CreateAssetMenu(menuName = "BuddahGo/AI/Driving profile")]
    public sealed class AIDifficultyProfile : ScriptableObject
    {
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

        public void ValidateConfiguration()
        {
            float[] values = { TargetSpeed, SpeedWeight, ProgressWeight, HorizonSeconds,
                ControlSeconds, LateralWeight, LateralVelocityWeight, LateralOffset, CorneringFactor };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentException("AI profile values must be finite.");
            if (TargetSpeed < 1f || SpeedWeight < 0f || ProgressWeight < 0f || HorizonSeconds < .2f
                || ControlSeconds < .02f || ControlSeconds > HorizonSeconds || BeamWidth < 1 || BeamWidth > 64
                || LateralWeight < 0f || LateralVelocityWeight < 0f
                || CorneringFactor < 0f || CorneringFactor > 1f || ReplanTicks < 1 || ReplanTicks > 30
                || ReactionTicks < 0 || ReactionTicks > 30)
                throw new System.ArgumentException("AI profile is outside the supported parameter ranges.");
        }
    }
}
