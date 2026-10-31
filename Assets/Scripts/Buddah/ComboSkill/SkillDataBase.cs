using System.Collections.Generic;
using UnityEngine;
using SteamMultiplayer.Network;

[CreateAssetMenu(menuName = "Skills/Databases/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [Header("Config Bridge")]
    [SerializeField] private ProjectConfigDatabase _projectConfigDatabase;
    [SerializeField] private string _activeRuleSetId = ProjectConfigConstants.DefaultRuleSetId;
    [SerializeField] private string _activeModeTag = ProjectConfigConstants.DefaultModeTag;
    [SerializeField] private string _activeMapTag = string.Empty;

    [Header("Normal Skills")]
    public List<SkillAction> normalSkills = new();

    [Header("Anti Skills")]
    [Tooltip("The per-skill _anti variants. A backfire runs the requested skill's variant (Skill.antiSkillId).")]
    public List<SkillAction> antiSkills = new();

    [Header("Legacy / Unsorted (Optional)")]
    [Tooltip("Backward-compatible list. Runtime will merge this list with Normal Skills and Anti Skills.")]
    public List<SkillAction> skills = new();

    [Header("缘起 (Skill Pairs)")]
    [Tooltip("Every 缘起 pair asset. YuanqiCatalog picks the enabled pairs fully inside a loadout; observers resolve presentations by yuanqiId.")]
    public List<YuanqiAction> yuanqis = new();

    private Dictionary<string, SkillAction> _byId;
    private Dictionary<string, YuanqiAction> _yuanqiById;

    private void OnEnable()
    {
        ProjectConfigRuntime.EnsureInitialized(_projectConfigDatabase);
        RebuildLookup();
    }

    private void OnValidate()
    {
        ProjectConfigRuntime.EnsureInitialized(_projectConfigDatabase);
        RebuildLookup();
    }

    public bool TryGet(string skillId, out SkillAction skill)
    {
        if (_byId == null)
            RebuildLookup();
        EnsureDesignTablesApplied();

        return _byId.TryGetValue(skillId, out skill);
    }

    /// <summary>All 缘起 pair assets (table values applied in play, like the skills). Filter with YuanqiCatalog.ForLoadout.</summary>
    public IReadOnlyList<YuanqiAction> GetYuanqis()
    {
        if (_byId == null)
            RebuildLookup();
        EnsureDesignTablesApplied();
        return yuanqis;
    }

    public bool TryGetYuanqi(string yuanqiId, out YuanqiAction action)
    {
        action = null;
        if (_byId == null || _yuanqiById == null)
            RebuildLookup();
        EnsureDesignTablesApplied();
        string id = (yuanqiId ?? string.Empty).Trim();
        return id.Length > 0 && _yuanqiById.TryGetValue(id, out action) && action != null;
    }

    /// <summary>Skill/Yuanqi row of the pair (identity, text keys, closing rules); false without tables or row.</summary>
    public bool TryGetYuanqiRecord(string yuanqiId, out YuanqiRecord record)
    {
        string id = (yuanqiId ?? string.Empty).Trim();
        ProjectConfigDatabase db = DesignTableOverlay.Db;
        record = id.Length > 0 && db != null ? DesignTableOverlay.Single(db.yuanqis, r => (r.yuanqiId ?? string.Empty).Trim() == id) : null;
        return record != null;
    }

    /// <summary>
    /// Display text of a 缘起 (HUD toast, loadout screen): UI/Text via the row's displayNameKey (yuanqi.&lt;id&gt;.name
    /// when there is no row), else the SO displayName, else the id.
    /// </summary>
    public string GetYuanqiDisplayName(string yuanqiId)
    {
        string id = (yuanqiId ?? string.Empty).Trim();
        if (id.Length == 0)
            return string.Empty;
        string fallback = TryGetYuanqi(id, out YuanqiAction action) && !string.IsNullOrWhiteSpace(action.displayName) ? action.displayName : id;
        string key = TryGetYuanqiRecord(id, out YuanqiRecord record) && !string.IsNullOrWhiteSpace(record.displayNameKey)
            ? record.displayNameKey.Trim()
            : YuanqiTextKeyPrefix + id + ".name";
        return DesignTableOverlay.Text(key, fallback);
    }

    /// <summary>Description text of a 缘起 (loadout screen): UI/Text via the row's descriptionKey (yuanqi.&lt;id&gt;.desc without a row), else <paramref name="fallback"/>.</summary>
    public string GetYuanqiDescription(string yuanqiId, string fallback = "")
    {
        string id = (yuanqiId ?? string.Empty).Trim();
        if (id.Length == 0)
            return fallback ?? string.Empty;
        string key = TryGetYuanqiRecord(id, out YuanqiRecord record) && !string.IsNullOrWhiteSpace(record.descriptionKey)
            ? record.descriptionKey.Trim()
            : YuanqiTextKeyPrefix + id + ".desc";
        return DesignTableOverlay.Text(key, fallback ?? string.Empty);
    }

    private const string YuanqiTextKeyPrefix = "yuanqi.";

    public List<SkillAction> GetSelectableNormalSkills()
    {
        if (_byId == null)
            RebuildLookup();

        if (ProjectConfigRuntime.TryGetSelectionRuleRepository(out SelectionRuleRepository selectionRules))
        {
            List<string> configuredSkillIds = selectionRules.GetSkillIdsForPool(GetActiveRuleSetId(), GetActiveModeTag(), GetActiveMapTag(), SkillSelectionPoolType.Selectable);
            if (configuredSkillIds.Count > 0)
            {
                List<SkillAction> configuredResults = new List<SkillAction>();
                for (int i = 0; i < configuredSkillIds.Count; i++)
                {
                    if (TryGet(configuredSkillIds[i], out SkillAction configuredSkill) && configuredSkill != null)
                        configuredResults.Add(configuredSkill);
                }

                if (configuredResults.Count > 0)
                    return configuredResults;
            }
        }

        List<SkillAction> results = new List<SkillAction>();
        HashSet<string> antiSkillIds = BuildAntiSkillIdSet();
        HashSet<string> seen = new HashSet<string>();

        AddSelectableSkillsFromList(normalSkills, antiSkillIds, seen, results, "normalSkills");
        AddSelectableSkillsFromList(skills, antiSkillIds, seen, results, "skills");
        return results;
    }

    public string GetActiveRuleSetId()
    {
        if (!string.IsNullOrWhiteSpace(_activeRuleSetId))
            return _activeRuleSetId.Trim();

        return _projectConfigDatabase != null ? _projectConfigDatabase.DefaultRuleSetId : ProjectConfigConstants.DefaultRuleSetId;
    }

    public string GetActiveModeTag()
    {
        if (!string.IsNullOrWhiteSpace(_activeModeTag))
            return _activeModeTag.Trim();

        return _projectConfigDatabase != null ? _projectConfigDatabase.DefaultModeTag : ProjectConfigConstants.DefaultModeTag;
    }

    public string GetActiveMapTag()
    {
        return (_activeMapTag ?? string.Empty).Trim();
    }

    public bool TryGetSkillRecord(string skillId, out SkillRecord record)
    {
        record = null;
        return ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGet(skillId, out record);
    }

    public string GetResolvedDisplayName(string skillId, SkillAction fallbackSkill = null)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository))
            return repository.GetDisplayName(skillId, fallbackSkill != null ? fallbackSkill.displayName : string.Empty);

        if (fallbackSkill != null && !string.IsNullOrWhiteSpace(fallbackSkill.displayName))
            return fallbackSkill.displayName;

        return (skillId ?? string.Empty).Trim();
    }

    public float GetCooldownSeconds(string skillId, SkillAction fallbackSkill)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetCooldown(skillId, out float configuredValue))
        {
            return Mathf.Max(0f, configuredValue);
        }

        return fallbackSkill != null ? Mathf.Max(0f, fallbackSkill.cooldownSeconds) : 0f;
    }

    public string GetAntiSkillId(string skillId, SkillAction fallbackSkill)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetAntiSkillId(skillId, out string configuredValue))
        {
            return configuredValue;
        }

        return fallbackSkill != null ? (fallbackSkill.antiSkillId ?? string.Empty).Trim() : string.Empty;
    }

    public float GetObsessionGain(string skillId, SkillAction fallbackSkill)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetObsessionGain(skillId, out float configuredValue))
        {
            return Mathf.Max(0f, configuredValue);
        }

        return fallbackSkill != null ? Mathf.Max(0f, fallbackSkill.ObsessionGain) : 0f;
    }

    public bool GetAllowDuplicateSkillSelections(bool fallbackValue = false)
    {
        if (ProjectConfigRuntime.TryGetSelectionRuleRepository(out SelectionRuleRepository repository)
            && repository.TryGetAllowDuplicateForPool(GetActiveRuleSetId(), GetActiveModeTag(), GetActiveMapTag(), SkillSelectionPoolType.Selectable, out bool configuredValue))
        {
            return configuredValue;
        }

        return fallbackValue;
    }

    public List<SelectablePropertyOption> BuildSelectableSkillOptions()
    {
        List<SelectablePropertyOption> results = new List<SelectablePropertyOption>();
        List<SkillAction> selectableSkills = GetSelectableNormalSkills();
        selectableSkills.Sort(CompareSelectableSkills);

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < selectableSkills.Count; i++)
        {
            SkillAction skill = selectableSkills[i];
            if (!TryGetValidSkillId(skill, out string skillId) || !seen.Add(skillId))
                continue;

            bool isUnlocked = true;
            string description = string.Empty;
            string iconKey = string.Empty; // icons are resolved by skillId (SkillUiSpriteLibrary / UI.SkillIcon); the table has no icon key column
            if (TryGetSkillRecord(skillId, out SkillRecord record))
            {
                description = DesignTableOverlay.Text(record.descriptionKey, string.Empty);
                isUnlocked = !string.Equals((record.unlockState ?? string.Empty).Trim(), "locked", System.StringComparison.OrdinalIgnoreCase);
            }

            results.Add(SelectablePropertyOption.Create(
                PropertiesSelectionManager.SkillLoadoutStageKey,
                skillId,
                GetResolvedDisplayName(skillId, skill),
                description,
                iconKey,
                isUnlocked));
        }

        return results;
    }

    public List<string> GetFallbackSkillIds()
    {
        if (ProjectConfigRuntime.TryGetSelectionRuleRepository(out SelectionRuleRepository repository))
        {
            List<string> configuredSkillIds = repository.GetSkillIdsForPool(GetActiveRuleSetId(), GetActiveModeTag(), GetActiveMapTag(), SkillSelectionPoolType.Fallback);
            if (configuredSkillIds.Count > 0)
                return configuredSkillIds;
        }

        return new List<string>();
    }

    public bool TryGetDefaultLoadout(out string[] skillIds, string ruleSetId = null, string modeTag = null)
    {
        skillIds = null;
        if (!ProjectConfigRuntime.TryGetSelectionRuleRepository(out SelectionRuleRepository repository))
            return false;

        if (!repository.TryGetDefaultLoadout(
                string.IsNullOrWhiteSpace(ruleSetId) ? GetActiveRuleSetId() : ruleSetId,
                string.IsNullOrWhiteSpace(modeTag) ? GetActiveModeTag() : modeTag,
                out DefaultLoadoutRecord record))
        {
            return false;
        }

        skillIds = record.ToSlots();
        return true;
    }

    private void RebuildLookup()
    {
        _byId = new Dictionary<string, SkillAction>();
        AddListToLookup(normalSkills, "normalSkills");
        AddListToLookup(antiSkills, "antiSkills");
        AddListToLookup(skills, "skills");
        RebuildYuanqiLookup();
        _tablesApplied = false;
        EnsureDesignTablesApplied();
    }

    // Entering Play Mode in the editor reloads the domain and calls OnEnable while Application.isPlaying is still
    // false, so applying only from RebuildLookup skipped the tables for the whole session (effect durations kept
    // their SO values). Every lookup now checks once per play session instead.
    private bool _tablesApplied;

    /// <summary>Play-state probe; tests replace it to reproduce the "lookup built before Play" sequence.</summary>
    public static System.Func<bool> IsPlaying = () => Application.isPlaying;

    private void EnsureDesignTablesApplied()
    {
        if (_tablesApplied || !IsPlaying() || _byId == null)
            return;
        _tablesApplied = true;
#if UNITY_EDITOR
        SnapshotSkillAssetsForEditor();
#endif
        ApplyDesignTablesToSkills();
    }

#if UNITY_EDITOR
    // The overlay writes into the skill assets themselves; in the editor those are the project assets, so restore
    // their serialized values when Play Mode ends (otherwise the inspector and the drift validator would show the
    // table values, and any later save of the asset would bake them in). The 缘起 assets are included: their
    // Skill/Yuanqi and YuanqiEffect<Pair> rows are overlaid the same way.
    private readonly Dictionary<ScriptableObject, string> _editorSnapshots = new Dictionary<ScriptableObject, string>();

    private void SnapshotSkillAssetsForEditor()
    {
        if (_editorSnapshots.Count > 0)
            return;
        foreach (SkillAction skill in _byId.Values)
            if (skill != null && !_editorSnapshots.ContainsKey(skill))
                _editorSnapshots[skill] = JsonUtility.ToJson(skill);
        if (yuanqis != null)
            foreach (YuanqiAction yuanqi in yuanqis)
                if (yuanqi != null && !_editorSnapshots.ContainsKey(yuanqi))
                    _editorSnapshots[yuanqi] = JsonUtility.ToJson(yuanqi);
        if (_editorSnapshots.Count > 0)
            UnityEditor.EditorApplication.playModeStateChanged += RestoreSkillAssetsOnExit;
    }

    private void RestoreSkillAssetsOnExit(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode && change != UnityEditor.PlayModeStateChange.EnteredEditMode)
            return;
        UnityEditor.EditorApplication.playModeStateChanged -= RestoreSkillAssetsOnExit;
        foreach (KeyValuePair<ScriptableObject, string> kv in _editorSnapshots)
            if (kv.Key != null)
                JsonUtility.FromJsonOverwrite(kv.Value, kv.Key);
        _editorSnapshots.Clear();
        _tablesApplied = false;
    }
#endif

    /// <summary>Apply the design tables to the skills of this database regardless of play mode (tests call it on copies of the assets).</summary>
    public void ApplyDesignTablesNow()
    {
        _byId = new Dictionary<string, SkillAction>();
        AddListToLookup(normalSkills, "normalSkills");
        AddListToLookup(antiSkills, "antiSkills");
        AddListToLookup(skills, "skills");
        RebuildYuanqiLookup();
        ApplyDesignTablesToSkills();
    }

    /// <summary>SO field that receives Skill.effectDurationSeconds for a skill class (null = the skill has no primary duration).</summary>
    public static string EffectDurationField(SkillAction skill)
    {
        switch (skill)
        {
            case Skill_Acceleration _: case Skill_Acceleration_Anti _: case Skill_Giant _: case Skill_Giant_Anti _: return "durationSeconds";
            case Skill_SlowTrap _: return "zoneDurationSeconds";
            case Skill_SlowTrap_Anti _: return "rootDurationSeconds";
            case Skill_BlackCurtain _: case Skill_BlackCurtain_Anti _: return "holdDurationSeconds";
            case Skill_PushProjectileHands _: return "buffDurationSeconds";
            case Skill_ReverseTurn _: case Skill_ReverseTurn_Anti _: return "invertDurationSeconds";
            default: return null;
        }
    }

    /// <summary>Skill/Push "charged" row → Skill_PushProjectileHands fields ("recordField=soField").</summary>
    public static readonly string[] PushChargedMap =
    {
        "windupSeconds=buildUpSeconds", "pushCooldownSeconds=chargedPushCooldownSeconds", "delayedPushActionSeconds", "lifetimeSeconds=projectileLifetimeSeconds",
        "impulseStrength=projectileImpulseStrength", "speed=projectileSpeed", "forwardOffset=projectileForwardOffset", "heightOffset=projectileHeightOffset", "colliderSize=projectileColliderSize"
    };

    /// <summary>Skill/Push "charged_anti" row → Skill_PushProjectileHands_Anti fields.</summary>
    public static readonly string[] PushChargedAntiMap =
    {
        "windupSeconds=buildUpSeconds", "lifetimeSeconds=projectileLifetimeSeconds", "impulseStrength=projectileImpulseStrength", "speed=projectileSpeed",
        "forwardOffset=additionalForwardSpawnOffset", "heightOffset=additionalHeightSpawnOffset", "projectileCount", "projectileSpacing", "colliderSize=projectileColliderSize", "hitTurnTorqueImpulse"
    };

    /// <summary>Skill/Yuanqi row → YuanqiAction fields. Explicit: the row also carries text keys and pairNumber, which are not SO fields.</summary>
    public static readonly string[] YuanqiRowMap =
    {
        "skillA", "skillB", "consumeA", "consumeB", "mode", "retriggerPolicy", "isEnabled"
    };

    /// <summary>Yuanqi_* class whose same-name fields a YuanqiEffect&lt;Pair&gt; record overlays (null = not a 缘起 family record).</summary>
    public static System.Type YuanqiFamilyActionType(System.Type familyRecord)
    {
        if (familyRecord == typeof(YuanqiEffectFrostLockRecord)) return typeof(Yuanqi_FrostLock);
        if (familyRecord == typeof(YuanqiEffectGiantChargeRecord)) return typeof(Yuanqi_GiantCharge);
        return null;
    }

    // Design tables own the effect numbers; the SO fields are only the editor-time fallback.
    // Applied in play mode only so edit-mode validation can still compare SO values against the tables.
    //   Skill.effectDurationSeconds → the primary duration field of each skill class (EffectDurationField)
    //   SkillEffect<Family> rows    → same-name fields of the normal / anti class (blank cells have no field on that class)
    //   Push charged / charged_anti → explicit mapping onto the two projectile skills (basic row belongs to BuddahHandControl)
    //   Yuanqi rows                 → explicit mapping onto the Yuanqi_* SO with the same yuanqiId (YuanqiRowMap)
    //   YuanqiEffect<Pair> rows     → same-name fields of that pair's Yuanqi_* class (YuanqiFamilyActionType)
    // Rows without an asset here are skipped silently.
    private void ApplyDesignTablesToSkills()
    {
        ProjectConfigDatabase db = DesignTableOverlay.Db;
        if (db == null)
            return;

        if (db.skills != null)
        {
            foreach (SkillRecord row in db.skills)
            {
                if (row == null || row.effectDurationSeconds <= 0f || !TryGetAction(row.skillId, out SkillAction skill))
                    continue;
                string field = EffectDurationField(skill);
                if (field != null)
                    DesignTableOverlay.ApplyMapped(skill, row, "effectDurationSeconds=" + field);
            }
        }

        ApplyFamily(db.skillEffectAccelerations, r => r.skillId);
        ApplyFamily(db.skillEffectSlowTraps, r => r.skillId);
        ApplyFamily(db.skillEffectBlackCurtains, r => r.skillId);
        ApplyFamily(db.skillEffectGiants, r => r.skillId);

        if (TryGetAction("push_projectile_hands", out SkillAction charged))
            DesignTableOverlay.ApplyMapped(charged, DesignTableOverlay.Single(db.pushes, r => r.pushId == "charged"), PushChargedMap);
        if (TryGetAction("push_projectile_hands_anti", out SkillAction chargedAnti))
            DesignTableOverlay.ApplyMapped(chargedAnti, DesignTableOverlay.Single(db.pushes, r => r.pushId == "charged_anti"), PushChargedAntiMap);

        if (db.yuanqis != null)
        {
            foreach (YuanqiRecord row in db.yuanqis)
            {
                if (row != null && TryGetYuanqiAsset(row.yuanqiId, out YuanqiAction yuanqi))
                    DesignTableOverlay.ApplyMapped(yuanqi, row, YuanqiRowMap);
            }
        }

        ApplyYuanqiFamily(db.yuanqiEffectFrostLocks, r => r.yuanqiId);
        ApplyYuanqiFamily(db.yuanqiEffectGiantCharges, r => r.yuanqiId);
    }

    private void ApplyYuanqiFamily<T>(List<T> rows, System.Func<T, string> yuanqiIdOf) where T : class
    {
        if (rows == null)
            return;
        System.Type expected = YuanqiFamilyActionType(typeof(T));
        foreach (T row in rows)
        {
            if (row == null || !TryGetYuanqiAsset(yuanqiIdOf(row), out YuanqiAction yuanqi))
                continue;
            if (expected == null || !expected.IsInstanceOfType(yuanqi))
            {
                Debug.LogWarning($"[SkillDatabase] {typeof(T).Name} row '{yuanqiIdOf(row)}' targets {yuanqi.GetType().Name}, expected {(expected != null ? expected.Name : "a known 缘起 family")}; row skipped.");
                continue;
            }
            DesignTableOverlay.Apply(yuanqi, row);
        }
    }

    private bool TryGetYuanqiAsset(string yuanqiId, out YuanqiAction yuanqi)
    {
        yuanqi = null;
        string id = (yuanqiId ?? string.Empty).Trim();
        return id.Length > 0 && _yuanqiById != null && _yuanqiById.TryGetValue(id, out yuanqi) && yuanqi != null;
    }

    private void ApplyFamily<T>(List<T> rows, System.Func<T, string> skillIdOf) where T : class
    {
        if (rows == null)
            return;
        foreach (T row in rows)
        {
            if (row != null && TryGetAction(skillIdOf(row), out SkillAction skill))
                DesignTableOverlay.Apply(skill, row);
        }
    }

    private bool TryGetAction(string skillId, out SkillAction skill)
    {
        skill = null;
        return !string.IsNullOrEmpty(skillId) && _byId != null && _byId.TryGetValue(skillId, out skill) && skill != null;
    }

    private void AddListToLookup(List<SkillAction> list, string listName)
    {
        if (list == null)
            return;

        foreach (SkillAction skill in list)
        {
            if (!TryGetValidSkillId(skill, out string skillId))
                continue;

            if (_byId.ContainsKey(skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate skillId '{skillId}' in database (list={listName}).");
                continue;
            }

            _byId.Add(skillId, skill);
        }
    }

    private void RebuildYuanqiLookup()
    {
        _yuanqiById = new Dictionary<string, YuanqiAction>();
        if (yuanqis == null)
            return;

        foreach (YuanqiAction yuanqi in yuanqis)
        {
            string id = yuanqi != null ? (yuanqi.yuanqiId ?? string.Empty).Trim() : string.Empty;
            if (id.Length == 0)
                continue;

            if (_yuanqiById.ContainsKey(id))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate yuanqiId '{id}' in database (list=yuanqis).");
                continue;
            }

            _yuanqiById.Add(id, yuanqi);
        }
    }

    private void AddSelectableSkillsFromList(
        List<SkillAction> list,
        HashSet<string> antiSkillIds,
        HashSet<string> seen,
        List<SkillAction> results,
        string listName)
    {
        if (list == null)
            return;

        foreach (SkillAction skill in list)
        {
            if (!TryGetValidSkillId(skill, out string skillId))
                continue;

            if (antiSkillIds.Contains(skillId))
                continue;

            if (!seen.Add(skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate selectable skillId '{skillId}' skipped (list={listName}).");
                continue;
            }

            results.Add(skill);
        }
    }

    private HashSet<string> BuildAntiSkillIdSet()
    {
        HashSet<string> antiSkillIds = new HashSet<string>();
        if (antiSkills == null)
            return antiSkillIds;

        foreach (SkillAction skill in antiSkills)
        {
            if (TryGetValidSkillId(skill, out string skillId))
                antiSkillIds.Add(skillId);
        }

        return antiSkillIds;
    }

    private static bool TryGetValidSkillId(SkillAction skill, out string skillId)
    {
        skillId = skill != null ? (skill.skillId ?? string.Empty).Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(skillId);
    }

    private int CompareSelectableSkills(SkillAction x, SkillAction y)
    {
        string xId = x != null ? x.skillId : string.Empty;
        string yId = y != null ? y.skillId : string.Empty;
        int sortCompare = 0;
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository))
            sortCompare = repository.GetSortOrder(xId).CompareTo(repository.GetSortOrder(yId));

        if (sortCompare != 0)
            return sortCompare;

        return string.Compare(GetResolvedDisplayName(xId, x), GetResolvedDisplayName(yId, y), System.StringComparison.OrdinalIgnoreCase);
    }
}
