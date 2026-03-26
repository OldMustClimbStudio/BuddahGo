using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class SkillVfxReplicator : NetworkBehaviour
{
    [SerializeField] private SkillVfxDatabase vfxDatabase;

    [Header("Prewarm")]
    [SerializeField] private bool prewarmOnStart = true;
    [SerializeField] private float prewarmAliveSeconds = 0.05f;
    [SerializeField] private int revealDelayFrames = 2;

    private static readonly Vector3 DefaultLocalOffset = Vector3.back * 2f;
    private static readonly Vector3 DefaultLocalEuler = new Vector3(0f, 180f, 0f);
    private const float DefaultStopPlayingBeforeEndSeconds = 0f;

    private static readonly HashSet<int> PrewarmedPrefabIds = new();
    private bool prewarmStarted;

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

    public void ForcePrewarmNow()
    {
        TryStartPrewarm("ForcePrewarmNow");
    }

    private void TryStartPrewarm(string source)
    {
        if (!prewarmOnStart || prewarmStarted)
            return;

        prewarmStarted = true;
        Debug.Log($"[SkillVfxReplicator] Prewarm started from {source}.", this);
        StartCoroutine(PrewarmAllVfx());
    }

    /// <summary>
    /// Server-side call: play a VFX on ALL clients for durationSeconds.
    /// </summary>
    public void PlayVfxAll(string vfxId, float durationSeconds)
    {
        if (!IsServerInitialized) return;
        PlayVfxAllObserversRpc(vfxId, durationSeconds);
    }

    /// <summary>
    /// Server-side call: play a VFX on ALL clients with custom local transform/timing.
    /// </summary>
    public void PlayVfxAll(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler, float stopPlayingBeforeEndSeconds)
    {
        if (!IsServerInitialized) return;
        PlayVfxAllObserversRpcCustom(vfxId, durationSeconds, localOffset, localEuler, stopPlayingBeforeEndSeconds);
    }

    /// <summary>
    /// Client/local call: play a VFX locally for durationSeconds.
    /// </summary>
    public void PlayVfxLocal(string vfxId, float durationSeconds)
    {
        PlayVfxInternal(vfxId, durationSeconds, DefaultLocalOffset, DefaultLocalEuler, DefaultStopPlayingBeforeEndSeconds);
    }

    /// <summary>
    /// Client/local call: play a VFX locally with skill-defined local transform.
    /// </summary>
    public void PlayVfxLocal(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler)
    {
        PlayVfxInternal(vfxId, durationSeconds, localOffset, localEuler, DefaultStopPlayingBeforeEndSeconds);
    }

    /// <summary>
    /// Client/local call: play a VFX locally with skill-defined local transform and particle stop timing.
    /// </summary>
    public void PlayVfxLocal(string vfxId, float durationSeconds, Vector3 localOffset, Vector3 localEuler, float stopPlayingBeforeEndSeconds)
    {
        PlayVfxInternal(vfxId, durationSeconds, localOffset, localEuler, stopPlayingBeforeEndSeconds);
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

        var existing = FindExisting(vfxId);
        if (existing != null)
        {
            existing.Refresh(durationSeconds, stopPlayingBeforeEndSeconds);
            existing.gameObject.SetActive(true);
            PlayAllEffects(existing.gameObject);
            return;
        }

        var vfxObj = Instantiate(prefab);
        // Configure while inactive to avoid first-frame pop.
        vfxObj.SetActive(false);
        vfxObj.transform.SetParent(transform, false);
        vfxObj.transform.localPosition = localOffset;
        vfxObj.transform.localRotation = Quaternion.Euler(localEuler);
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
            prewarmObj.transform.SetParent(transform, false);
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

        Debug.Log($"[SkillVfxReplicator] Prewarm finished. warmed={warmedCount}", this);
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
}

public class SkillVfxTag : MonoBehaviour
{
    public string VfxId;
}
