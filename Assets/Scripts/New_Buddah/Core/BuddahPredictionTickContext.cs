using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public readonly struct BuddahPredictionTickContext
    {
        public readonly Vector3 RbVelocityPreTick;
        public readonly float RbMass;
        public readonly float FixedDeltaTime;
        public readonly uint Tick;

        public readonly Vector3 ForwardDirection;
        public readonly float ResolvedThrottle;
        public readonly float ResolvedSteering;

        public readonly BuddahPredictedMotorComputedStats ComputedStats;
        public readonly float PushGraceExtraSpeed;
        public readonly float PushGraceRemaining;

        public BuddahPredictionTickContext(
            Vector3 rbVelocityPreTick,
            float rbMass,
            float fixedDeltaTime,
            uint tick,
            Vector3 forwardDirection,
            float resolvedThrottle,
            float resolvedSteering,
            BuddahPredictedMotorComputedStats computedStats,
            float pushGraceExtraSpeed,
            float pushGraceRemaining)
        {
            RbVelocityPreTick = rbVelocityPreTick;
            RbMass = rbMass;
            FixedDeltaTime = fixedDeltaTime;
            Tick = tick;
            ForwardDirection = forwardDirection;
            ResolvedThrottle = resolvedThrottle;
            ResolvedSteering = resolvedSteering;
            ComputedStats = computedStats;
            PushGraceExtraSpeed = pushGraceExtraSpeed;
            PushGraceRemaining = pushGraceRemaining;
        }
    }
}
