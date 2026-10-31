using UnityEngine;

// A per-call value snapshot, never serialized or sent over the network.
internal struct ProjectileBurstParameters
{
    internal int projectileCount;
    internal float projectileSpacing;
    internal float buildUpSeconds;
    internal float projectileSpeed;
    internal float projectileLifetimeSeconds;
    internal float projectileImpulseStrength;
    internal Vector3 projectileColliderSize;
    internal bool reverseDirection;
    internal bool allowSelfHit;
    internal bool ignoreSolidWorld;
    internal float hitTurnTorqueImpulse;
    internal float additionalForwardSpawnOffset;
    internal float additionalHeightSpawnOffset;
    internal bool spawnServerHitboxes;
    internal string replicatedSkillId;
    internal AnimationCurve speedCurve;   // flight shape (ChargedProjectileFlight); null = linear fall
}

internal static class ProjectileBurstPlanner
{
    internal static Vector3 GetSpawnPosition(Vector3 burstAnchor, Vector3 sideDir, int index,
        int projectileCount, float spacing, float scaleMultiplier)
    {
        float offsetIndex = index - ((projectileCount - 1) * 0.5f);
        return burstAnchor + (sideDir * (offsetIndex * spacing * scaleMultiplier));
    }

    internal static Vector3 SanitizeColliderSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.05f, Mathf.Abs(size.x)),
            Mathf.Max(0.05f, Mathf.Abs(size.y)),
            Mathf.Max(0.05f, Mathf.Abs(size.z)));
    }
}
