using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillVfxDatabase", menuName = "Skills/VFX Database")]
public class SkillVfxDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string vfxId;
        public GameObject prefab;
    }

    public List<Entry> entries = new();

    private Dictionary<string, GameObject> _map;

    private void OnEnable()
    {
        _map = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.vfxId) || e.prefab == null)
                continue;

            if (_map.ContainsKey(e.vfxId))
            {
                Debug.LogWarning($"[SkillVfxDatabase] Duplicate vfxId '{e.vfxId}'.");
                continue;
            }

            _map.Add(e.vfxId, e.prefab);
        }
    }

    public bool TryGetPrefab(string vfxId, out GameObject prefab)
    {
        if (_map == null) OnEnable();
        return _map.TryGetValue(vfxId, out prefab);
    }
}
