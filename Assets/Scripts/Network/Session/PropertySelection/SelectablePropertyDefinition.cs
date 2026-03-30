using System;
using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    public enum PropertySelectionMode
    {
        Single = 0,
        Multi = 1,
        Vote = 2,
        HostOnly = 3
    }

    [Serializable]
    public struct SelectablePropertyDefinition : IEquatable<SelectablePropertyDefinition>
    {
        public string PropertyKey;
        public string DisplayName;
        public PropertySelectionMode SelectionMode;
        public List<SelectablePropertyOption> AvailableOptions;

        public bool Equals(SelectablePropertyDefinition other)
        {
            return PropertyKey == other.PropertyKey
                && DisplayName == other.DisplayName
                && SelectionMode == other.SelectionMode;
        }

        public override bool Equals(object obj)
        {
            return obj is SelectablePropertyDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PropertyKey, DisplayName, SelectionMode);
        }

        public IEnumerable<SelectablePropertyOption> GetSafeOptions()
        {
            return AvailableOptions ?? new List<SelectablePropertyOption>();
        }

        public static SelectablePropertyDefinition Create(
            string propertyKey,
            string displayName,
            PropertySelectionMode selectionMode,
            params SelectablePropertyOption[] options)
        {
            return new SelectablePropertyDefinition
            {
                PropertyKey = propertyKey ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                SelectionMode = selectionMode,
                AvailableOptions = options != null
                    ? new List<SelectablePropertyOption>(options)
                    : new List<SelectablePropertyOption>()
            };
        }
    }
}
