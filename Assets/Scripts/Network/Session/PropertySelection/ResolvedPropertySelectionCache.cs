using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    public static class ResolvedPropertySelectionCache
    {
        public static string MatchSceneName { get; private set; } = string.Empty;
        public static IReadOnlyDictionary<string, string> ResolvedSelections => _resolvedSelections;
        public static IReadOnlyDictionary<int, string[]> PlayerSkillLoadouts => _playerSkillLoadouts;

        private static readonly Dictionary<string, string> _resolvedSelections = new Dictionary<string, string>();
        private static readonly Dictionary<int, string[]> _playerSkillLoadouts = new Dictionary<int, string[]>();

        public static void Clear()
        {
            MatchSceneName = string.Empty;
            _resolvedSelections.Clear();
            _playerSkillLoadouts.Clear();
        }

        public static void SetMatchScene(string matchSceneName)
        {
            MatchSceneName = matchSceneName ?? string.Empty;
        }

        public static void SetSelection(string propertyKey, string optionId)
        {
            if (string.IsNullOrWhiteSpace(propertyKey))
                return;

            _resolvedSelections[propertyKey] = optionId ?? string.Empty;
        }

        public static bool TryGetSelection(string propertyKey, out string optionId)
        {
            return _resolvedSelections.TryGetValue(propertyKey, out optionId);
        }

        public static void SetPlayerSkillLoadout(int playerId, IReadOnlyList<string> skillIds)
        {
            if (playerId < 0)
                return;

            string[] slots = new string[SkillLoadout.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = (skillIds != null && i < skillIds.Count ? skillIds[i] : string.Empty) ?? string.Empty;
            }

            _playerSkillLoadouts[playerId] = slots;
        }

        public static bool TryGetPlayerSkillLoadout(int playerId, out string[] skillIds)
        {
            if (_playerSkillLoadouts.TryGetValue(playerId, out string[] cached))
            {
                skillIds = (string[])cached.Clone();
                return true;
            }

            skillIds = null;
            return false;
        }
    }
}
