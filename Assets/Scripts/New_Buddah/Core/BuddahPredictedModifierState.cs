namespace NewBuddah.PredictionV2.Core
{
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

        public bool HasAnyActive(uint tick)
        {
            return RootUntilTick > tick
                   || AccelUntilTick > tick
                   || PostRootAccelUntilTick > tick
                   || ScaleUntilTick > tick
                   || InvertTurnUntilTick > tick
                   || PushGraceUntilTick > tick
                   || SuppressSteeringUntilTick > tick
                   || RoomBypassUntilTick > tick;
        }
    }
}
