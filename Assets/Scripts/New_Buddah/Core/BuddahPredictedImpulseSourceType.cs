namespace NewBuddah.PredictionV2.Core
{
    public enum BuddahPredictedImpulseSourceType : byte
    {
        Unknown = 0,
        MeleePush = 1,
        Projectile = 2,
        ChargedProjectile = 3,
        DebugBox = 4,
        // 缘起 #4 giant charge: a bursting giant's body hits another racer (BuddahBodyImpact). Log/evidence label only.
        BodyImpact = 5,
    }
}
