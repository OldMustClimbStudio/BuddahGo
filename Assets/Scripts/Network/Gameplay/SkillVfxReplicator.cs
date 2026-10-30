using BuddahGo.VFX;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class SkillVfxReplicator : NetworkBehaviour
{
    [SerializeField] private SkillVfxDatabase vfxDatabase;

    public SkillVfxDatabase Database => vfxDatabase;

    [Header("Prewarm")]
    [SerializeField] private bool prewarmOnStart = true;
    [SerializeField] private float prewarmAliveSeconds = 0.05f;
    [SerializeField] private int revealDelayFrames = 2;

    private static readonly Vector3 DefaultLocalOffset = Vector3.back * 2f;
    private static readonly Vector3 DefaultLocalEuler = new Vector3(0f, 180f, 0f);
    private const float DefaultStopPlayingBeforeEndSeconds = 0f;

    private static readonly HashSet<int> PrewarmedPrefabIds = new();
    private bool prewarmStarted;
    // Slot effects (Docs/vfx/README.md section 4) currently playing on this player, one per vfxId.
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
    /// Server-side call: play a VFX on ALL clients with custom local transform/timing.
    /// </summary>
    public void PlayVfxAll(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler, float stopPlayingBeforeEndSeconds)
    {
        if (!IsServerInitialized) return;
        PlayVfxAllObserversRpcCustom(vfxId, durationSeconds, localOffset, localEuler, stopPlayingBeforeEndSeconds);
    }

    [ObserversRpc]
    private void PlayVfxAllObserversRpc(string vfxId, float durationSeconds)
    {
        PlayVfxInternal(vfxId, durationSeconds, DefaultLocalOffset, DefaultLocalEuler, DefaultStopPlayingBeforeEndSeconds);
    }

    [ObserversRpc]
    private void PlayVfxAllObserversRpcCustom(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler, float stopPlayingBeforeEndSeconds)
    {
        PlayVfxInternal(vfxId, durationSeconds, localOffset, localEuler, stopPlayingBeforeEndSeconds);
    }

    private void PlayVfxInternal(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler, float stopPlayingBeforeEndSeconds)
    {
        if (vfxDatabase == null)
        {
            Debug.LogWarning("[SkillVfxReplicator] vfxDatabase is null.");
            return;
        }

        if (!vfxDatabase.TryGetPrefab(vfxId, out var prefab) || prefab == null)
        {
            Debug.LogWarning($"[SkillVfxReplicator] Unknown vfxId '{vfxId}'.");
            return;
        }

        if (prefab.GetComponent<SkillVfxPhaseController>() != null)
        {
            PlaySlot(vfxId, prefab, durationSeconds);
            return;
        }

        var existing = FindExisting(vfxId);
        if (existing != null)
        {
            ConfigureAttachment(existing.gameObject, localOffset, localEuler);
            existing.Refresh(durationSeconds, stopPlayingBeforeEndSeconds);
            existing.gameObject.SetActive(true);
            PlayAllEffects(existing.gameObject);
            return;
        }

        var vfxObj = Instantiate(prefab);
        // Configure while inactive to avoid first-frame pop.
        vfxObj.SetActive(false);
        ConfigureAttachment(vfxObj, localOffset, localEuler);
        DisableNetworkComponentsForLocalVfx(vfxId, vfxObj);

        var tag = vfxObj.GetComponent<SkillVfxTag>();
        if (tag == null) tag = vfxObj.AddComponent<SkillVfxTag>();
        tag.VfxId = vfxId;

        var timer = vfxObj.GetComponent<TimedVfxInstance>();
        if (timer == null) timer = vfxObj.AddComponent<TimedVfxInstance>();
        timer.Refresh(durationSeconds, stopPlayingBeforeEndSeconds);

        SetAllRenderersEnabled(vfxObj, false);
        vfxObj.SetActive(true);
        PlayAllEffects(vfxObj);
        StartCoroutine(EnableRenderersAfterFrames(vfxObj, revealDelayFrames));
    }

    // Local only (no RPC): plays a slot effect on this player on this client, e.g. what a local view controller
    // decides per observer (一念光明's trail). Ignores ids whose prefab is not a slot.
    public void PlaySlotLocal(string vfxId, float durationSeconds)
    {
        if (vfxDatabase == null || !vfxDatabase.TryGetPrefab(vfxId, out var prefab) || prefab == null)
            return;
        if (prefab.GetComponent<SkillVfxPhaseController>() != null)
            PlaySlot(vfxId, prefab, durationSeconds);
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
    // Every client plays it locally; it never touches gameplay.
    private void PlaySlot(string vfxId, GameObject prefab, float durationSeconds)
    {
        if (_slots.TryGetValue(vfxId, out SkillVfxPhaseController previous) && previous != null)
            Destroy(previous.gameObject);

        GameObject instance = Instantiate(prefab, transform.position, transform.rotation);
        DisableNetworkComponentsForLocalVfx(vfxId, instance);
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

            var prewarmObj = Instantiate(entry.prefab);
            prewarmObj.SetActive(false);
            ConfigureAttachment(prewarmObj, DefaultLocalOffset, DefaultLocalEuler);
            DisableNetworkComponentsForLocalVfx(entry.vfxId, prewarmObj);
            SetAllRenderersEnabled(prewarmObj, false);

            prewarmObj.SetActive(true);
            PlayAllEffects(prewarmObj);

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

    private TimedVfxInstance FindExisting(string vfxId)
    {
        var tags = GetComponentsInChildren<SkillVfxTag>(true);
        foreach (var t in tags)
        {
            if (t != null && t.VfxId == vfxId)
                return t.GetComponent<TimedVfxInstance>();
        }
        return null;
    }

    private void PlayAllParticleSystems(GameObject root)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            var main = ps.main;
            if (main.stopAction != ParticleSystemStopAction.None)
                main.stopAction = ParticleSystemStopAction.None;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void PlayAllPlayableDirectors(GameObject root)
    {
        var directors = root.GetComponentsInChildren<PlayableDirector>(true);
        foreach (var director in directors)
        {
            if (director == null) continue;
            director.time = 0d;
            director.Evaluate();
            director.Play();
        }
    }

    private void PlayAllEffects(GameObject root)
    {
        PlayAllPlayableDirectors(root);
        PlayAllParticleSystems(root);
    }

    private void SetAllRenderersEnabled(GameObject root, bool enabled)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.enabled = enabled;
        }
    }

    private IEnumerator EnableRenderersAfterFrames(GameObject root, int frames)
    {
        int safeFrames = Mathf.Max(1, frames);
        for (int i = 0; i < safeFrames; i++)
            yield return null;

        if (root == null) yield break;
        SetAllRenderersEnabled(root, true);
    }

    private void DisableNetworkComponentsForLocalVfx(string vfxId, GameObject root)
    {
        var networkObjects = root.GetComponentsInChildren<NetworkObject>(true);
        var networkBehaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);

        if (networkObjects.Length == 0 && networkBehaviours.Length == 0)
            return;

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

    private void ConfigureAttachment(GameObject vfxObj, Vector3 localOffset, Vector3 localEuler)
    {
        if (vfxObj == null)
            return;

        var settings = vfxObj.GetComponent<SkillVfxAttachmentSettings>();
        if (settings == null || settings.InheritTargetRotation)
        {
            var follower = vfxObj.GetComponent<SkillVfxFollowTarget>();
            if (follower != null)
                Destroy(follower);

            vfxObj.transform.SetParent(transform, false);
            vfxObj.transform.localPosition = localOffset;
            vfxObj.transform.localRotation = Quaternion.Euler(localEuler);
            return;
        }

        vfxObj.transform.SetParent(null, true);
        var follow = vfxObj.GetComponent<SkillVfxFollowTarget>();
        if (follow == null)
            follow = vfxObj.AddComponent<SkillVfxFollowTarget>();

        follow.Initialize(transform, localOffset, localEuler, settings.InheritTargetRotation, settings.InheritTargetScale);
    }
}

public class SkillVfxTag : MonoBehaviour
{
    public string VfxId;
}
