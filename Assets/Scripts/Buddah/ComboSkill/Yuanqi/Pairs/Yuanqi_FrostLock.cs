using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// #2 禅足冰封 (acceleration + slowtrap, Instant, consumes both). The moment the two overlap, every racer other than the
/// caster within radiusMeters (horizontal) of the caster — AI and training dummies included — is frozen for
/// freezeSeconds (ApplyFreezeToOwner also interrupts a confirming cast and refunds that slot). Effect first, then both
/// skills are consumed (yuanqi-architecture §4.2 / §4.6).
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_FrostLock", menuName = "Skills/Yuanqi/Frost Lock")]
public class Yuanqi_FrostLock : YuanqiAction
{
    [Header("Frost Lock (phase A placeholders; phase B: YuanqiEffectFrostLock)")]
    [Tooltip("N: freeze seconds applied to every target.")]
    [Min(0f)] public float freezeSeconds = 1.5f;
    [Tooltip("R: horizontal radius around the caster, metres.")]
    [Min(0f)] public float radiusMeters = 12f;
    [Tooltip("Practice training dummies are frozen too.")]
    public bool affectsTrainingDummies = true;

    [Header("Presentation (PLACEHOLDER until the artist's VFX exist)")]
    [Tooltip("Reserved SkillVfxDatabase id for the iced ground. Not wired yet: the placeholder disc below is drawn instead.")]
    public string groundVfxId = "frost_lock_ground_vfx";
    [Tooltip("Reserved SkillVfxDatabase id for the ice block on each target. Not wired yet: the placeholder block below is drawn instead.")]
    public string blockVfxId = "frost_lock_block_vfx";
    [Min(0f)] public float placeholderFadeSeconds = 0.35f;
    [Min(0.001f)] public float placeholderGroundThickness = 0.08f;
    [Min(0f)] public float placeholderGroundLift = 0.03f;
    [Min(0f)] public float placeholderGroundProbeUp = 3f;
    [Min(0f)] public float placeholderGroundProbeDown = 20f;
    [Min(0f)] public float placeholderBlockPadding = 0.4f;
    public Color placeholderGroundColor = new Color(0.55f, 0.85f, 1f, 0.35f);
    public Color placeholderBlockColor = new Color(0.7f, 0.92f, 1f, 0.45f);

    [NonSerialized] private readonly List<SkillExecutor> _targets = new List<SkillExecutor>();
    [NonSerialized] private readonly RaycastHit[] _groundHits = new RaycastHit[16];

    public override void OnTriggerServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        Vector3 center = caster.transform.position;
        YuanqiTargeting.CollectOthersWithin(caster, radiusMeters, affectsTrainingDummies, _targets);

        var targetIds = new List<int>(_targets.Count);
        for (int i = 0; i < _targets.Count; i++)
        {
            SkillExecutor target = _targets[i];
            target.ApplyFreezeToOwner(freezeSeconds);
            // The registry records one "yuanqi-trigger" combat event per id listed here.
            NetworkObject targetObject = target.NetworkObject;
            if (targetObject != null)
                targetIds.Add(targetObject.ObjectId);
        }
        int frozen = _targets.Count;
        _targets.Clear();

        ctx.Presentation = new YuanqiPresentationPayload
        {
            Center = center,
            Radius = radiusMeters,
            Seconds = freezeSeconds,
            TargetObjectIds = targetIds.ToArray(),
        };

        // Effect first, then consume (if consuming throws, the freeze has already landed).
        ctx.ConsumeA("yuanqi:" + yuanqiId);
        ctx.ConsumeB("yuanqi:" + yuanqiId);

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][FrostLock][Server] '{caster.name}' froze {frozen} racer(s) within {radiusMeters:0.#} m for {freezeSeconds:0.##}s; consumed {skillA} + {skillB}.");
    }

    // PLACEHOLDER visuals: a translucent icy disc on the ground (radius R) and a translucent ice block on each target,
    // both without colliders. Replace with groundVfxId / blockVfxId once the artist's prefabs are in SkillVfxDatabase.
    public override void PlayObservers(SkillExecutor caster, YuanqiPresentationPayload payload, bool localIsCaster)
    {
        if (caster == null)
            return;

        float holdSeconds = Mathf.Max(0f, payload.Seconds);
        if (payload.Radius > 0f)
        {
            Vector3 ground = ResolveGroundPoint(payload.Center) + Vector3.up * placeholderGroundLift;
            YuanqiPlaceholderVisual.SpawnGroundDisc("FrostLock_Ground (placeholder)", ground, payload.Radius,
                placeholderGroundThickness, placeholderGroundColor, holdSeconds, placeholderFadeSeconds);
        }

        int[] ids = payload.TargetObjectIds;
        if (ids == null)
            return;
        for (int i = 0; i < ids.Length; i++)
        {
            NetworkObject target = ResolveSpawned(caster, ids[i]);
            if (target == null)
                continue;
            if (!TryGetBodyBounds(target.transform, out Bounds bounds))
                continue;
            bounds.Expand(placeholderBlockPadding * 2f);
            YuanqiPlaceholderVisual.SpawnBlock("FrostLock_Block (placeholder)", target.transform, bounds,
                placeholderBlockColor, holdSeconds, placeholderFadeSeconds);
        }

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][FrostLock][Observers] placeholder ice: radius={payload.Radius:0.#} targets={ids.Length} seconds={holdSeconds:0.##} localIsCaster={localIsCaster}");
    }

    // Nearest non-racer surface below the centre; the centre itself when nothing is hit.
    private Vector3 ResolveGroundPoint(Vector3 center)
    {
        Vector3 origin = center + Vector3.up * placeholderGroundProbeUp;
        float distance = placeholderGroundProbeUp + placeholderGroundProbeDown;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        Vector3 point = center;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHits[i];
            if (hit.collider == null || hit.collider.GetComponentInParent<SkillExecutor>() != null)
                continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }
        return point;
    }

    // World bounds of the body's solid colliders (they follow the giant scale); renderers when there are none.
    private static bool TryGetBodyBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;
            if (!found) { bounds = c.bounds; found = true; }
            else bounds.Encapsulate(c.bounds);
        }
        if (found)
            return true;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || r.GetComponent<YuanqiPlaceholderVisual>() != null)
                continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    private static NetworkObject ResolveSpawned(SkillExecutor caster, int objectId)
    {
        NetworkManager manager = caster.NetworkManager;
        if (manager == null)
            return null;
        if (manager.IsClientStarted && manager.ClientManager.Objects.Spawned.TryGetValue(objectId, out NetworkObject clientObject) && clientObject != null)
            return clientObject;
        if (manager.IsServerStarted && manager.ServerManager.Objects.Spawned.TryGetValue(objectId, out NetworkObject serverObject) && serverObject != null)
            return serverObject;
        return null;
    }
}
