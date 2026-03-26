using System;
using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    [Serializable]
    public struct PropertySelectionEntry : IEquatable<PropertySelectionEntry>
    {
        public string PropertyKey;
        public string SelectedOptionId;

        public bool Equals(PropertySelectionEntry other)
        {
            return PropertyKey == other.PropertyKey
                && SelectedOptionId == other.SelectedOptionId;
        }

        public override bool Equals(object obj)
        {
            return obj is PropertySelectionEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PropertyKey, SelectedOptionId);
        }
    }

    [Serializable]
    public struct PlayerPropertySelection : IEquatable<PlayerPropertySelection>
    {
        public int PlayerId;
        public string SteamId;
        public string PlayerName;
        public List<PropertySelectionEntry> Selections;

        public bool TryGetSelectedOptionId(string propertyKey, out string selectedOptionId)
        {
            if (Selections != null)
            {
                for (int i = 0; i < Selections.Count; i++)
                {
                    if (Selections[i].PropertyKey == propertyKey)
                    {
                        selectedOptionId = Selections[i].SelectedOptionId;
                        return true;
                    }
                }
            }

            selectedOptionId = string.Empty;
            return false;
        }

        public Dictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            if (Selections == null)
                return result;

            for (int i = 0; i < Selections.Count; i++)
                result[Selections[i].PropertyKey] = Selections[i].SelectedOptionId;

            return result;
        }

        public bool Equals(PlayerPropertySelection other)
        {
            return PlayerId == other.PlayerId
                && SteamId == other.SteamId
                && PlayerName == other.PlayerName;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerPropertySelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, SteamId, PlayerName);
        }
    }
}
