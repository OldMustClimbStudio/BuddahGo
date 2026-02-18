using FishNet.Object;
using UnityEngine;
using System.Collections;

public class SkillVfxReplicator : NetworkBehaviour
{
    [SerializeField] private SkillVfxDatabase vfxDatabase;
    private static readonly Vector3 DefaultLocalOffset = Vector3.back * 2f;
    private static readonly Vector3 DefaultLocalEuler = new Vector3(0f, 180f, 0f);
    private const float DefaultStopPlayingBeforeEndSeconds = 0f;

    /// <summary>
    /// Server-side call: play a VFX on ALL clients for durationSeconds.
    /// </summary>
    public void PlayVfxAll(string vfxId, float durationSeconds)
    {
        if (!IsServerInitialized) return;
        PlayVfxAllObserversRpc(vfxId, durationSeconds);
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

        // 如果已有同 id 的特效：刷新持续时间（不重复堆积）
        var existing = FindExisting(vfxId);
        if (existing != null)
        {
            existing.Refresh(durationSeconds, stopPlayingBeforeEndSeconds);
            existing.gameObject.SetActive(true);
            PlayAllParticleSystems(existing.gameObject);
            return;
        }

        // 本地生成并挂在施法者身上
        var vfxObj = Instantiate(prefab);
        // prefab 若在 Project 里是 inactive，实例会继承 inactive，这里强制激活
        vfxObj.SetActive(true);
        vfxObj.transform.SetParent(transform, false);
        DisableNetworkComponentsForLocalVfx(vfxId, vfxObj);
        vfxObj.SetActive(true);
        vfxObj.transform.localPosition = localOffset;
        vfxObj.transform.localRotation = Quaternion.Euler(localEuler);

        if (!vfxObj.activeInHierarchy)
            Debug.LogWarning($"[SkillVfxReplicator] Instantiated VFX '{vfxId}' is not active in hierarchy.");
        else
            Debug.Log($"[SkillVfxReplicator] Instantiated VFX '{vfxId}'.");

        StartCoroutine(LogVfxStateNextFrame(vfxId, vfxObj));

        // 标记 id + 计时
        var tag = vfxObj.GetComponent<SkillVfxTag>();
        if (tag == null) tag = vfxObj.AddComponent<SkillVfxTag>();
        tag.VfxId = vfxId;

        var timer = vfxObj.GetComponent<TimedVfxInstance>();
        if (timer == null) timer = vfxObj.AddComponent<TimedVfxInstance>();
        timer.Refresh(durationSeconds, stopPlayingBeforeEndSeconds);

        PlayAllParticleSystems(vfxObj);
    }

    private TimedVfxInstance FindExisting(string vfxId)
    {
        // 找子物体中带 SkillVfxTag 且 VfxId 相同的特效
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

    // 调试用：生成后下一帧检查特效状态，帮助诊断 prefab 设置问题
    private IEnumerator LogVfxStateNextFrame(string vfxId, GameObject vfxObj)
    {
        yield return null;

        if (vfxObj == null)
        {
            yield break;
        }

        Transform parent = vfxObj.transform.parent;
        bool parentActiveInHierarchy = parent == null || parent.gameObject.activeInHierarchy;

        Debug.Log(
            $"[SkillVfxReplicator] Next-frame state '{vfxId}': " +
            $"activeSelf={vfxObj.activeSelf}, " +
            $"activeInHierarchy={vfxObj.activeInHierarchy}, " +
            $"parent={(parent != null ? parent.name : "<none>")}, " +
            $"parentActiveInHierarchy={parentActiveInHierarchy}");
    }
}

public class SkillVfxTag : MonoBehaviour
{
    public string VfxId;
}
