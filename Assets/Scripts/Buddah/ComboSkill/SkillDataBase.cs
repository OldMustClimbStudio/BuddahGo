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
    public List<SkillAction> antiSkills = new();

    [Header("Legacy / Unsorted (Optional)")]
    [Tooltip("Backward-compatible list. Runtime will merge this list with Normal Skills and Anti Skills.")]
    public List<SkillAction> skills = new();

    private Dictionary<string, SkillAction> _byId;

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

        return _byId.TryGetValue(skillId, out skill);
    }

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

    public bool TryGetDefinition(string skillId, out SkillDefinitionRecord record)
    {
        record = null;
        return ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetDefinition(skillId, out record);
    }

    public bool TryGetBalance(string skillId, out SkillBalanceRecord record)
    {
        record = null;
        return ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetBalance(skillId, out record);
    }

    public string GetResolvedDisplayName(string skillId, SkillAction fallbackSkill = null)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository))
            return repository.GetDisplayName(skillId, fallbackSkill != null ? fallbackSkill.displayName : string.Empty);

        if (fallbackSkill != null && !string.IsNullOrWhiteSpace(fallbackSkill.displayName))
            return fallbackSkill.displayName;

        return (skillId ?? string.Empty).Trim();
    }

    public string GetResolvedDescription(string skillId)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository))
            return repository.GetDescription(skillId);

        return string.Empty;
    }

    public string GetResolvedIconKey(string skillId)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository))
            return repository.GetIconKey(skillId);

        return string.Empty;
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

    public float GetCastLockSeconds(string skillId, SkillAction fallbackSkill)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetCastLock(skillId, out float configuredValue))
        {
            return Mathf.Max(0f, configuredValue);
        }

        return fallbackSkill != null ? Mathf.Max(0f, fallbackSkill.castLockSeconds) : 0f;
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

    public string GetAntiSkillId(string skillId, SkillAction fallbackSkill)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetAntiSkillId(skillId, out string configuredValue))
        {
            return configuredValue;
        }

        return fallbackSkill != null ? (fallbackSkill.antiSkillId ?? string.Empty).Trim() : string.Empty;
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
            string iconKey = string.Empty;
            if (TryGetDefinition(skillId, out SkillDefinitionRecord definition))
            {
                description = definition.description ?? string.Empty;
                iconKey = definition.iconKey ?? string.Empty;
                isUnlocked = !string.Equals((definition.unlockState ?? string.Empty).Trim(), "locked", System.StringComparison.OrdinalIgnoreCase);
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

    public bool TryGetEffectFloat(string skillId, string effectType, string paramKey, out float value)
    {
        value = 0f;
        return ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository repository)
            && repository.TryGetFloat(skillId, effectType, paramKey, out value);
    }

    private void RebuildLookup()
    {
        _byId = new Dictionary<string, SkillAction>();
        AddListToLookup(normalSkills, "normalSkills");
        AddListToLookup(antiSkills, "antiSkills");
        AddListToLookup(skills, "skills");
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
