using System;
using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    // Deterministic rules with caller-supplied option and duplicate policies.
    // Validation intentionally normalizes the input array, including a partial prefix on failure.
    internal static class LoadoutRules
    {
        internal static bool ValidateSubmission(string[] skillIds, int slotCount, Func<string, bool> isOptionValid, Func<bool> allowDuplicates, out string validationError)
        {
            validationError = string.Empty;
            if (skillIds == null || skillIds.Length != slotCount)
            {
                validationError = "slot array size mismatch";
                return false;
            }
            HashSet<string> seen = new HashSet<string>();
            for (int slotIndex = 0; slotIndex < skillIds.Length; slotIndex++)
            {
                string skillId = (skillIds[slotIndex] ?? string.Empty).Trim();
                skillIds[slotIndex] = skillId;
                if (string.IsNullOrWhiteSpace(skillId))
                    continue;
                if (!isOptionValid(skillId))
                {
                    validationError = $"invalid skillId '{skillId}' in slot {slotIndex}";
                    return false;
                }
                if (!allowDuplicates() && !seen.Add(skillId))
                {
                    validationError = $"duplicate skill '{skillId}' is not allowed";
                    return false;
                }
            }
            return true;
        }

        internal static bool HasCompleteSelection(string[] skillIds, Func<string, bool> isOptionValid, Func<bool> allowDuplicates)
        {
            for (int i = 0; i < skillIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(skillIds[i]) || !isOptionValid(skillIds[i]))
                    return false;
            }
            if (allowDuplicates())
                return true;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < skillIds.Length; i++)
            {
                if (!seen.Add(skillIds[i]))
                    return false;
            }
            return true;
        }

        internal static string FindNextAutoFillSkillId(IReadOnlyList<string> candidateSkillIds, HashSet<string> used, Func<bool> allowDuplicates)
        {
            for (int i = 0; i < candidateSkillIds.Count; i++)
            {
                string candidate = candidateSkillIds[i];
                if (!string.IsNullOrWhiteSpace(candidate) && (allowDuplicates() || !used.Contains(candidate)))
                    return candidate;
            }
            return string.Empty;
        }

        internal static void AppendOptionCandidates(List<string> candidates, HashSet<string> seen, IReadOnlyList<SelectablePropertyOption> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                string optionId = options[i].OptionId;
                if (!string.IsNullOrWhiteSpace(optionId) && seen.Add(optionId))
                    candidates.Add(optionId);
            }
        }

        internal static void AppendFallbackCandidates(List<string> candidates, HashSet<string> seen, IReadOnlyList<string> fallbackSkillIds)
        {
            for (int i = 0; i < fallbackSkillIds.Count; i++)
            {
                string skillId = (fallbackSkillIds[i] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(skillId) && seen.Add(skillId))
                    candidates.Add(skillId);
            }
        }
    }
}
