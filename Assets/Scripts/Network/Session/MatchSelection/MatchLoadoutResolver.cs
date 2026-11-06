using System.Collections.Generic;
using UnityEngine;

namespace SteamMultiplayer.Network
{
    /// <summary>
    /// Server-side checks for a skill loadout picked in the menu: which skill ids are selectable (unlocked skills of
    /// the <see cref="SkillDatabase"/>), whether duplicates are allowed, and how empty or invalid slots are filled
    /// before the race. Same rules the selection scene used (<see cref="LoadoutRules"/>).
    /// </summary>
    internal sealed class MatchLoadoutResolver
    {
        // Last resort when neither the skill table nor the database lists fallbacks (PropertiesSelectionManager's old default).
        private static readonly string[] LegacyFallbackSkillIds = { "acceleration", "push_projectile_hands", "blackcurtain" };

        private readonly HashSet<string> _selectable = new HashSet<string>();
        private readonly List<string> _candidates = new List<string>();
        private readonly bool _allowDuplicates;

        internal MatchLoadoutResolver(SkillDatabase database)
        {
            var seen = new HashSet<string>();
            if (database != null)
            {
                _allowDuplicates = database.GetAllowDuplicateSkillSelections(false);
                List<SelectablePropertyOption> options = database.BuildSelectableSkillOptions();
                for (int i = 0; i < options.Count; i++)
                    if (options[i].IsUnlocked && !string.IsNullOrWhiteSpace(options[i].OptionId)) _selectable.Add(options[i].OptionId);
                LoadoutRules.AppendOptionCandidates(_candidates, seen, options);
                LoadoutRules.AppendFallbackCandidates(_candidates, seen, database.GetFallbackSkillIds());
            }
            else
            {
                Debug.LogWarning("[MatchLoadoutResolver] No SkillDatabase assigned; every submitted loadout is filled from the fallback list.");
            }
            LoadoutRules.AppendFallbackCandidates(_candidates, seen, LegacyFallbackSkillIds);
        }

        internal bool IsSelectable(string skillId) => !string.IsNullOrWhiteSpace(skillId) && _selectable.Contains(skillId);

        /// <summary>
        /// Trims a submitted loadout to <see cref="SkillLoadout.SlotCount"/> slots and checks it. Empty slots are allowed
        /// (filled later by <see cref="Complete"/>); an unknown, locked or duplicated skill rejects the whole loadout.
        /// </summary>
        internal bool TryNormalize(IReadOnlyList<string> skillIds, out string[] normalized, out string error)
        {
            normalized = new string[SkillLoadout.SlotCount];
            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = skillIds != null && i < skillIds.Count ? skillIds[i] ?? string.Empty : string.Empty;
            return LoadoutRules.ValidateSubmission(normalized, SkillLoadout.SlotCount, IsSelectable, () => _allowDuplicates, out error);
        }

        /// <summary>A full loadout: valid picks stay in their slot, empty or invalid slots take the next fallback skill.</summary>
        internal string[] Complete(IReadOnlyList<string> skillIds)
        {
            var result = new string[SkillLoadout.SlotCount];
            var used = new HashSet<string>();
            for (int i = 0; i < result.Length; i++)
            {
                string id = (skillIds != null && i < skillIds.Count ? skillIds[i] : null)?.Trim() ?? string.Empty;
                if (!IsSelectable(id) || (!_allowDuplicates && used.Contains(id))) id = string.Empty;
                result[i] = id;
                if (id.Length > 0) used.Add(id);
            }
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].Length > 0) continue;
                result[i] = LoadoutRules.FindNextAutoFillSkillId(_candidates, used, () => _allowDuplicates);
                if (result[i].Length > 0) used.Add(result[i]);
            }
            return result;
        }
    }
}
