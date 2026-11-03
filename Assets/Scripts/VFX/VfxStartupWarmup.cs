using System.Collections;
using System.Collections.Generic;
using BuddahGo.VFX;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.VFX;

[DefaultExecutionOrder(-10000)]
public class VfxStartupWarmup : MonoBehaviour
{
    [Header("Run Control")]
    [SerializeField] private bool runOncePerApp = true;
    [SerializeField] private bool dontDestroyOnLoad = false;
    [SerializeField] private bool runOnAwake = true;

    [Header("Shader Warmup")]
    [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;

    [Header("Prefab Warmup")]
    [SerializeField] private GameObject[] vfxPrefabs;
    [Tooltip("Also warms every SkillVfxDatabase prefab whose VFX table row (Skill.xlsx / VfxRegistry) has warmup = true.")]
    [SerializeField] private SkillVfxDatabase skillVfxDatabase;
    [SerializeField] private Transform warmupAnchor;
    [SerializeField] private float keepAliveSeconds = 0.1f;

    private static bool s_hasRun;

    private void Awake()
    {
        if (!runOnAwake)
            return;

        StartWarmup();
    }

    public void StartWarmup()
    {
        if (runOncePerApp && s_hasRun)
            return;

        s_hasRun = true;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        StartCoroutine(WarmupRoutine());
    }

    private IEnumerator WarmupRoutine()
    {
        WarmupShaders();
        yield return null;
        yield return WarmupPrefabs();
    }

    private void WarmupShaders()
    {
        if (shaderVariantCollections == null)
            return;

        int warmed = 0;
        foreach (var svc in shaderVariantCollections)
        {
            if (svc == null)
                continue;

            svc.WarmUp();
            warmed++;
        }

        Debug.Log($"[VfxStartupWarmup] Shader warmup done. collections={warmed}", this);
    }

    private List<GameObject> CollectPrefabs()
    {
        var prefabs = new List<GameObject>();
        if (vfxPrefabs != null)
            prefabs.AddRange(vfxPrefabs);

        if (skillVfxDatabase != null && skillVfxDatabase.entries != null)
        {
            foreach (SkillVfxDatabase.Entry entry in skillVfxDatabase.entries)
            {
                if (entry == null || entry.prefab == null || prefabs.Contains(entry.prefab))
                    continue;
                if (SkillVfxTable.TryGetRecord(entry.vfxId, out VfxRegistryRecord row) && row.warmup)
                    prefabs.Add(entry.prefab);
            }
        }
        return prefabs;
    }

    private IEnumerator WarmupPrefabs()
    {
        List<GameObject> prefabs = CollectPrefabs();
        if (prefabs.Count == 0)
            yield break;

        int warmed = 0;
        Vector3 pos = warmupAnchor != null ? warmupAnchor.position : transform.position;
        Quaternion rot = warmupAnchor != null ? warmupAnchor.rotation : transform.rotation;

        foreach (var prefab in prefabs)
        {
            if (prefab == null)
                continue;

            var obj = Instantiate(prefab, pos, rot);
            if (obj == null)
                continue;

            obj.SetActive(true);
            PlayAll(obj);
            warmed++;

            if (keepAliveSeconds > 0f)
                yield return new WaitForSeconds(keepAliveSeconds);
            else
                yield return null;

            if (obj != null)
                Destroy(obj);

            yield return null;
        }

        Debug.Log($"[VfxStartupWarmup] Prefab warmup done. prefabs={warmed}", this);
    }

    private static void PlayAll(GameObject root)
    {
        var directors = root.GetComponentsInChildren<PlayableDirector>(true);
        foreach (var d in directors)
        {
            if (d == null) continue;
            d.time = 0d;
            d.Evaluate();
            d.Play();
        }

        var vfxList = root.GetComponentsInChildren<VisualEffect>(true);
        foreach (var vfx in vfxList)
        {
            if (vfx == null) continue;
            vfx.Reinit();
            vfx.Play();
        }

        var particles = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }
}
