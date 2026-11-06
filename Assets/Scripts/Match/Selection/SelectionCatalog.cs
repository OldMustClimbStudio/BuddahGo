using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddahGo.Match
{
    /// <summary>
    /// The skins and maps a player can pick in the menu. The menu and the race read the same asset, so a skin id
    /// picked on the skin page resolves to the same material on the racer. Materials cannot live in the design
    /// tables, hence a ScriptableObject (Resources/<see cref="ResourcePath"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "BuddahGo/Match/Selection Catalog", fileName = "SelectionCatalog")]
    public sealed class SelectionCatalog : ScriptableObject
    {
        public const string ResourcePath = "Match/SelectionCatalog";

        [Serializable]
        public sealed class Skin
        {
            [Tooltip("Stable id sent over the network and saved in PlayerPrefs.")]
            public string id;
            public string displayName;
            [TextArea] public string line;
            [Tooltip("Put on every renderer of the Buddha model. Empty: the model keeps its own material.")]
            public Material material;
            [Tooltip("Calligraphy of the name (white on transparent) for the skin page. Empty: the name is typeset.")]
            public Sprite nameArt;
        }

        [Serializable]
        public sealed class Map
        {
            [Tooltip("Stable id sent over the network and saved in PlayerPrefs.")]
            public string id;
            public string displayName;
            [Tooltip("Scene loaded for the race; must be in Build Settings.")]
            public string sceneName;
        }

        [SerializeField] private Skin[] skins = Array.Empty<Skin>();
        [SerializeField] private Map[] maps = Array.Empty<Map>();

        private static SelectionCatalog _current;

        /// <summary>The catalog in Resources; null when the asset is missing.</summary>
        public static SelectionCatalog Current => _current != null ? _current : _current = Resources.Load<SelectionCatalog>(ResourcePath);

        public IReadOnlyList<Skin> Skins => skins;
        public IReadOnlyList<Map> Maps => maps;

        public string DefaultSkinId => skins.Length > 0 ? skins[0].id : string.Empty;
        public string DefaultMapId => maps.Length > 0 ? maps[0].id : string.Empty;

        public bool TryGetSkin(string id, out Skin skin) => TryFind(skins, s => s.id, id, out skin);
        public bool TryGetMap(string id, out Map map) => TryFind(maps, m => m.id, id, out map);

        public int IndexOfSkin(string id)
        {
            for (int i = 0; i < skins.Length; i++)
                if (skins[i] != null && string.Equals(skins[i].id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>A known skin id, or the default skin for an empty or unknown one.</summary>
        public string NormalizeSkinId(string id) => TryGetSkin(id, out _) ? id : DefaultSkinId;

        /// <summary>A known map id, or the default map for an empty or unknown one.</summary>
        public string NormalizeMapId(string id) => TryGetMap(id, out _) ? id : DefaultMapId;

        /// <summary>The race scene of a map, or <paramref name="fallbackScene"/> when the map is unknown.</summary>
        public string SceneOf(string mapId, string fallbackScene)
            => TryGetMap(mapId, out Map map) && !string.IsNullOrWhiteSpace(map.sceneName) ? map.sceneName : fallbackScene;

        private static bool TryFind<T>(T[] list, Func<T, string> idOf, string id, out T found) where T : class
        {
            found = null;
            if (string.IsNullOrWhiteSpace(id) || list == null) return false;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && string.Equals(idOf(list[i]), id, StringComparison.Ordinal))
                {
                    found = list[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>Editor and test hook: fills the lists in code.</summary>
        public void Configure(Skin[] skinList, Map[] mapList)
        {
            skins = skinList ?? Array.Empty<Skin>();
            maps = mapList ?? Array.Empty<Map>();
        }
    }
}
