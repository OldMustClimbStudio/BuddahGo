using UnityEngine;

namespace BuddahGo.AI
{
    [CreateAssetMenu(menuName = "BuddahGo/AI/Driving profile")]
    public sealed class AIDifficultyProfile : ScriptableObject
    {
        [Header("Pace (planning preferences, never motor limits)")]
        [Min(1f)] public float TargetSpeed = 65f;
        [Min(0f)] public float SpeedWeight = 0.15f;
        [Min(0f)] public float ProgressWeight = 3f;
        [Header("Tracking precision")]
        [Min(0.2f)] public float HorizonSeconds = 3f;
        [Min(0.02f)] public float ControlSeconds = 0.15f;
        [Range(1, 64)] public int BeamWidth = 24;
        [Min(0f)] public float LateralWeight = 1f;
        [Min(0f)] public float LateralVelocityWeight = 0.7f;
        public float LateralOffset;
        [Header("Reaction (ticks)")]
        [Range(1, 30)] public int ReplanTicks = 3;
        [Range(0, 30)] public int ReactionTicks;
        // A1 has one experimental Normal profile. Easy/Hard mappings need A2/A3 evidence.
    }
}
