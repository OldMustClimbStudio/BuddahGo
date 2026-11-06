namespace NewBuddah.PredictionV2.Core
{
    // Every public field is on the reconcile wire (FishNet codegen). Every *UntilTick is a
    // timestamp translated by BuddahTickMath.ModifiersToLocal; durations and magnitudes are not.
    public struct BuddahPredictedModifierState
    {
        public uint RootUntilTick;
        public uint AccelUntilTick;
        public float AccelExtraForwardForce;
        public float AccelExtraMaxSpeed;

        public uint PostRootAccelUntilTick;
        public float PostRootAccelExtraForwardForce;
        public float PostRootAccelExtraMaxSpeed;

        public uint ScaleUntilTick;
        public float ScaleMultiplier;
        public float ScaleMassMultiplier;
        public float ScaleForwardForceMultiplier;

        public uint InvertTurnUntilTick;
        public uint PushGraceUntilTick;
        public uint SuppressSteeringUntilTick;
        public uint RoomBypassUntilTick;

        // Freeze (frost lock): a root that also stops turning and locks actions.
        public uint FreezeUntilTick;

        // Burst (charge): extra max speed decaying with remaining01² towards BurstUntilTick.
        // BurstDurationTicks is the decay length (a duration, never translated between clocks).
        public uint BurstUntilTick;
        public uint BurstDurationTicks;
        public float BurstExtraMaxSpeed;

        // Backfire max-speed penalty. Persists through skill resets and respawn teleports.
        public uint MaxSpeedPenaltyUntilTick;
        public float MaxSpeedPenaltyMultiplier;

        public bool HasAnyActive(uint tick)
        {
            return RootUntilTick > tick
                   || AccelUntilTick > tick
                   || PostRootAccelUntilTick > tick
                   || ScaleUntilTick > tick
                   || InvertTurnUntilTick > tick
                   || PushGraceUntilTick > tick
                   || SuppressSteeringUntilTick > tick
                   || RoomBypassUntilTick > tick
                   || FreezeUntilTick > tick
                   || BurstUntilTick > tick
                   || MaxSpeedPenaltyUntilTick > tick;
        }
    }
}
