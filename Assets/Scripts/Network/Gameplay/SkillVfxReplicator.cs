using BuddahGo.VFX;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays the skill VFX slots (Docs/vfx/README.md section 4) on this player. Every effect in SkillVfxDatabase is a
// slot prefab with a SkillVfxPhaseController; the old attach-and-time path (TimedVfxInstance,
// SkillVfxAttachmentSettings) was retired with the first-pass effects (Docs/vfx/retired.md).
public class SkillVfxReplicator : NetworkBehaviour
{
    [SerializeField] private SkillVfxDatabase vfxDatabase;

    public SkillVfxDatabase Database => vfxDatabase;

    [Header("Prewarm")]
    [SerializeField] private bool prewarmOnStart = true;
    [SerializeField] private float prewarmAliveSeconds = 0.05f;

    private static readonly HashSet<int> PrewarmedPrefabIds = new();
    private bool prewarmStarted;
    // Slot effects currently playing on this player, one per vfxId.
    private readonly Dictionary<string, SkillVfxPhaseController> _slots = new();
    // Slot effects outlast their window by this much so the last particles and trails fade out.
    private const float SlotTailSeconds = 1.5f;

    public override void OnStartClient()
    {
        base.OnStartClient();
        TryStartPrewarm("OnStartClient");
    }

    private void Start()
    {
        // Fallback for non-network/local test scenes.
        TryStartPrewarm("Start");
    }

    private void TryStartPrewarm(string source)
    {
        if (!prewarmOnStart || prewarmStarted)
            return;

        prewarmStarted = true;
        GameLog.Verbose($"[SkillVfxReplicator] Prewarm started from {source}.", this);
        StartCoroutine(PrewarmAllVfx());
    }

    /// <summary>
    /// Server-side call: play a slot effect on this player on ALL clients for the gameplay length.
    /// </summary>
    public void PlayVfxAll(string vfxId, float durationSeconds)
    {
        if (!IsServerInitialized) return;
        PlayVfxAllObserversRpc(vfxId, durationSeconds);
    }

    [ObserversRpc]
    private void PlayVfxAllObserversRpc(string vfxId, float durationSeconds)
    {
        PlaySlot(vfxId, durationSeconds, true);
    }

    // Local only (no RPC): plays a slot effect on this player on this client, e.g. what a local view controller
    // decides per observer (一念光明's trail). Silently ignores ids whose prefab is missing or not a slot.
    public void PlaySlotLocal(string vfxId, float durationSeconds)
    {
        PlaySlot(vfxId, durationSeconds, false);
    }

    // Ends a local slot effect early: its layers stop and it fades out over its dissipation.
    public void StopSlotLocal(string vfxId)
    {
        if (_slots.TryGetValue(vfxId, out SkillVfxPhaseController slot) && slot != null)
            slot.BeginDissipation();
        _slots.Remove(vfxId);
    }

    // A slot prefab places its own layers on this player (caster anchors), so it is not parented or offset here:
    // the root stands at the player, the controller gets this player as the caster and runs the release window.
    // Every client plays it locally; it never touches gameplay. warn: log why nothing played (the RPC path).
    private void PlaySlot(string vfxId, float durationSeconds, bool warn)
    {
        GameObject prefab = null;
        string problem = vfxDatabase == null ? "vfxDatabase is null."
            : !vfxDatabase.TryGetPrefab(vfxId, out prefab) || prefab == null ? $"Unknown vfxId '{vfxId}'."
            : prefab.GetComponent<SkillVfxPhaseController>() == null ? $"'{vfxId}' is not a slot prefab (no SkillVfxPhaseController); nothing played."
            : null;
        if (problem != null)
        {
            if (warn)
                Debug.LogWarning("[SkillVfxReplicator] " + problem);
            return;
        }

        if (_slots.TryGetValue(vfxId, out SkillVfxPhaseController previous) && previous != null)
            Destroy(previous.gameObject);

        GameObject instance = Instantiate(prefab, transform.position, transform.rotation);
        DisableNetworkComponentsForLocalVfx(instance);
        var controller = instance.GetComponent<SkillVfxPhaseController>();
        var executor = GetComponent<SkillExecutor>();
        controller.Configure(new SkillVfxPhaseController.Context
        {
            caster = transform,
            isLocalCaster = IsOwner,
            playLocalFeel = executor != null ? executor.PlayFeelLocal : (System.Action<string>)null,
        });
        controller.BeginPhase(SkillVfxPhase.Imp, Mathf.Max(0f, durationSeconds));
        controller.DestroyWhenFinished(SlotTailSeconds);
        _slots[vfxId] = controller;
    }

    // Instantiates every slot prefab once (renderers off, controller left unconfigured so it stays inert) to load
    // its materials and particle systems before the first cast. RaceMap's VfxStartupWarmup also plays the table's
    // warmup rows; this covers local and test scenes without it.
    private IEnumerator PrewarmAllVfx()
    {
        if (vfxDatabase == null || vfxDatabase.entries == null)
        {
            Debug.LogWarning("[SkillVfxReplicator] Prewarm skipped: vfxDatabase or entries is null.", this);
            yield break;
        }

        int warmedCount = 0;

        foreach (var entry in vfxDatabase.entries)
        {
            if (entry == null || entry.prefab == null)
                continue;

            int prefabId = entry.prefab.GetInstanceID();
            if (!PrewarmedPrefabIds.Add(prefabId))
                continue;

            var prewarmObj = Instantiate(entry.prefab, transform.position, transform.rotation);
            DisableNetworkComponentsForLocalVfx(prewarmObj);
            foreach (var r in prewarmObj.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null)
                    r.enabled = false;
            }

            if (prewarmAliveSeconds > 0f)
                yield return new WaitForSeconds(prewarmAliveSeconds);
            else
                yield return null;

            if (prewarmObj != null)
                Destroy(prewarmObj);

            warmedCount++;
            yield return null;
        }

        GameLog.Verbose($"[SkillVfxReplicator] Prewarm finished. warmed={warmedCount}", this);
    }

    private static void DisableNetworkComponentsForLocalVfx(GameObject root)
    {
        var networkObjects = root.GetComponentsInChildren<NetworkObject>(true);
        var networkBehaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);

        foreach (var networkBehaviour in networkBehaviours)
        {
            if (networkBehaviour == null) continue;
            networkBehaviour.enabled = false;
        }

        foreach (var networkObject in networkObjects)
        {
            if (networkObject == null) continue;
            networkObject.enabled = false;
        }
    }
}
