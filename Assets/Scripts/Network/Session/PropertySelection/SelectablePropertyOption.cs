using System;

namespace SteamMultiplayer.Network
{
    [Serializable]
    public struct SelectablePropertyOption : IEquatable<SelectablePropertyOption>
    {
        public string PropertyKey;
        public string OptionId;
        public string DisplayName;
        public string Description;
        public string PreviewIconKey;
        public bool IsUnlocked;

        public bool Equals(SelectablePropertyOption other)
        {
            return PropertyKey == other.PropertyKey
                && OptionId == other.OptionId
                && DisplayName == other.DisplayName
                && Description == other.Description
                && PreviewIconKey == other.PreviewIconKey
                && IsUnlocked == other.IsUnlocked;
        }

        public override bool Equals(object obj)
        {
            return obj is SelectablePropertyOption other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PropertyKey, OptionId, DisplayName, Description, PreviewIconKey, IsUnlocked);
        }

        public static SelectablePropertyOption Create(
            string propertyKey,
            string optionId,
            string displayName,
            string description = "",
            string previewIconKey = "",
            bool isUnlocked = true)
        {
            return new SelectablePropertyOption
            {
                PropertyKey = propertyKey ?? string.Empty,
                OptionId = optionId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Description = description ?? string.Empty,
                PreviewIconKey = previewIconKey ?? string.Empty,
                IsUnlocked = isUnlocked
            };
        }
    }
}
