namespace NewBuddah.PredictionV2.Core
{
    public enum BuddahPredictedModifierCommandType
    {
        None = 0,
        Acceleration = 1,
        RootThenAcceleration = 2,
        InvertTurn = 3,
        Scale = 4,
        PushGrace = 5,
        SteeringSuppression = 6,
        RoomBypass = 7,
        // Mechanics update (PredictionProtocol v2). Explicit numbers: they cross the wire as TargetRpc ints.
        Freeze = 8,
        EndModifier = 9,
        Burst = 10,
        MaxSpeedMultiplier = 11
    }

    public struct BuddahPredictedModifierCommandData
    {
        public BuddahPredictedModifierCommandType Type;
        public float ValueA;
        public float ValueB;
        public float ValueC;
        public float ValueD;
        public string Source;

        public static BuddahPredictedModifierCommandData CreateAcceleration(float extraForwardForce, float extraMaxSpeed, float durationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.Acceleration,
                ValueA = extraForwardForce,
                ValueB = extraMaxSpeed,
                ValueC = durationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateRootThenAcceleration(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.RootThenAcceleration,
                ValueA = rootDurationSeconds,
                ValueB = extraForwardForce,
                ValueC = extraMaxSpeed,
                ValueD = accelDurationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateInvertTurn(float durationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.InvertTurn,
                ValueA = durationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateScale(float scaleMultiplier, float durationSeconds, float massMultiplier, float forwardForceMultiplier, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.Scale,
                ValueA = scaleMultiplier,
                ValueB = durationSeconds,
                ValueC = massMultiplier,
                ValueD = forwardForceMultiplier,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreatePushGrace(float durationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.PushGrace,
                ValueA = durationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateSteeringSuppression(float durationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.SteeringSuppression,
                ValueA = durationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateRoomBypass(float durationSeconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.RoomBypass,
                ValueA = durationSeconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateFreeze(float seconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.Freeze,
                ValueA = seconds,
                Source = source
            };
        }

        // Ends an active modifier early (deadline = now). It never starts or extends anything.
        public static BuddahPredictedModifierCommandData CreateEndModifier(BuddahPredictedModifierCommandType target, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.EndModifier,
                ValueA = (int)target,
                Source = source
            };
        }

        internal static BuddahPredictedModifierCommandType ReadEndModifierTarget(BuddahPredictedModifierCommandData command)
        {
            return (BuddahPredictedModifierCommandType)UnityEngine.Mathf.RoundToInt(command.ValueA);
        }

        public static BuddahPredictedModifierCommandData CreateBurst(float extraMaxSpeed, float seconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.Burst,
                ValueA = extraMaxSpeed,
                ValueB = seconds,
                Source = source
            };
        }

        public static BuddahPredictedModifierCommandData CreateMaxSpeedMultiplier(float multiplier, float seconds, string source)
        {
            return new BuddahPredictedModifierCommandData
            {
                Type = BuddahPredictedModifierCommandType.MaxSpeedMultiplier,
                ValueA = multiplier,
                ValueB = seconds,
                Source = source
            };
        }
    }
}
