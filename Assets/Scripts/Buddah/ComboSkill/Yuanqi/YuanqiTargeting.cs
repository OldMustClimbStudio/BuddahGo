using System.Collections.Generic;
using BuddahGo.Match;
using UnityEngine;

/// <summary>One racer as the targeting rule sees it (pure data, unit-tested).</summary>
internal readonly struct YuanqiTargetCandidate
{
    public YuanqiTargetCandidate(Vector3 position, bool isCaster, bool isServerInitialized, bool isTrainingDummy)
    {
        Position = position;
        IsCaster = isCaster;
        IsServerInitialized = isServerInitialized;
        IsTrainingDummy = isTrainingDummy;
    }

    public Vector3 Position { get; }
    public bool IsCaster { get; }
    public bool IsServerInitialized { get; }
    public bool IsTrainingDummy { get; }
}

/// <summary>
/// Server-side target scan for 缘起 pairs. Every racer carries a SkillExecutor (training dummies included, README A1),
/// so the scan walks SkillExecutors like Skill_ReverseTurn does: never the caster, only server-initialized objects,
/// horizontal distance (XZ) up to the radius inclusive. Training dummies are valid targets unless the pair opts out.
/// </summary>
public static class YuanqiTargeting
{
    public static void CollectOthersWithin(SkillExecutor caster, float radius, bool includeTrainingDummies, List<SkillExecutor> buffer)
    {
        if (buffer == null)
            return;
        buffer.Clear();
        if (caster == null)
            return;

        Vector3 center = caster.transform.position;
        SkillExecutor[] all = Object.FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            SkillExecutor target = all[i];
            if (target == null)
                continue;

            var candidate = new YuanqiTargetCandidate(
                target.transform.position,
                target == caster,
                target.IsServerInitialized,
                IsTrainingDummy(target));
            if (IsEligible(candidate, center, radius, includeTrainingDummies))
                buffer.Add(target);
        }
    }

    internal static bool IsEligible(in YuanqiTargetCandidate candidate, Vector3 center, float radius, bool includeTrainingDummies)
    {
        if (candidate.IsCaster || !candidate.IsServerInitialized)
            return false;
        if (candidate.IsTrainingDummy && !includeTrainingDummies)
            return false;
        return IsHorizontallyWithin(center, candidate.Position, radius);
    }

    // XZ only: height differences (ramps, jumps, a giant's pivot) never take a racer out of the circle.
    internal static bool IsHorizontallyWithin(Vector3 center, Vector3 point, float radius)
    {
        if (!(radius > 0f))
            return false;
        float dx = point.x - center.x;
        float dz = point.z - center.z;
        return dx * dx + dz * dz <= radius * radius;
    }

    private static bool IsTrainingDummy(SkillExecutor target)
        => target.TryGetComponent(out RacerIdentity identity) && identity.IsTrainingDummy;
}
