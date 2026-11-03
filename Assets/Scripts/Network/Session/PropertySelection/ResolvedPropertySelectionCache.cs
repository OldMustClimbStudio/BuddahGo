using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    public static class ResolvedPropertySelectionCache
    {
        private static readonly Dictionary<int, string[]> _playerSkillLoadouts = new Dictionary<int, string[]>();

        public static void Clear()
        {
            _playerSkillLoadouts.Clear();
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
