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
        RoomBypass = 7
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
    }
}
