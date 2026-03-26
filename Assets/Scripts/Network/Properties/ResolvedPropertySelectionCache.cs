using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    public static class ResolvedPropertySelectionCache
    {
        public static string MatchSceneName { get; private set; } = string.Empty;
        public static IReadOnlyDictionary<string, string> ResolvedSelections => _resolvedSelections;

        private static readonly Dictionary<string, string> _resolvedSelections = new Dictionary<string, string>();

        public static void Clear()
        {
            MatchSceneName = string.Empty;
            _resolvedSelections.Clear();
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
    }
}
