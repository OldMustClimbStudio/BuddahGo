using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuddahGo.Rendering.Settings
{
    /// <summary>Serialisable graphics choice: a preset plus the options the player changed away from it.</summary>
    [Serializable]
    public sealed class GraphicsSettingsState
    {
        [Serializable]
        public struct Override
        {
            public string optionId;
            public string levelId;
        }

        public int version = 1;
        public string presetId;
        public List<Override> overrides = new List<Override>();
    }

    // Client-local graphics choice. The player picks a preset; changing an option away from the preset's default
    // stores an override and the UI shows "Custom" (CustomLabelKey). Setting an option back to the preset default,
    // picking a preset or ResetToPreset removes overrides. Never networked.
    public sealed class GraphicsSettingsModel
    {
        readonly GraphicsSettingsCatalog m_Catalog;
        readonly Dictionary<string, string> m_Overrides = new Dictionary<string, string>(StringComparer.Ordinal);

        public GraphicsSettingsModel(GraphicsSettingsCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            PresetId = catalog.DefaultPreset?.Id;
        }

        public event Action Changed;

        public GraphicsSettingsCatalog Catalog => m_Catalog;
        public string PresetId { get; private set; }
        public GraphicsSettingsCatalog.Preset Preset => m_Catalog.FindPreset(PresetId);
        public bool IsCustom => m_Overrides.Count > 0;
        /// <summary>Label key for the preset selector: the preset's own, or "Custom" while any option is overridden.</summary>
        public string PresetLabelKey => IsCustom ? GraphicsSettingsCatalog.CustomLabelKey : Preset?.LabelKey;

        /// <summary>Current level of an option (override or preset default); null for an unknown option.</summary>
        public string Get(string optionId)
        {
            if (m_Overrides.TryGetValue(optionId, out var level))
                return level;
            return Preset?.DefaultLevel(optionId);
        }

        public bool IsOverridden(string optionId) => m_Overrides.ContainsKey(optionId);

        /// <summary>Chooses a level. Returns false for unknown ids. Choosing the preset default removes the override.</summary>
        public bool Set(string optionId, string levelId)
        {
            var option = m_Catalog.FindOption(optionId);
            if (option == null || option.Find(levelId) == null)
                return false;
            string presetDefault = Preset?.DefaultLevel(optionId);
            bool changed;
            if (levelId == presetDefault)
                changed = m_Overrides.Remove(optionId);
            else
            {
                changed = !m_Overrides.TryGetValue(optionId, out var previous) || previous != levelId;
                m_Overrides[optionId] = levelId;
            }
            if (changed)
                Changed?.Invoke();
            return true;
        }

        /// <summary>Switches to a preset and drops every override. Returns false for an unknown preset.</summary>
        public bool SetPreset(string presetId)
        {
            if (m_Catalog.FindPreset(presetId) == null)
                return false;
            bool changed = PresetId != presetId || m_Overrides.Count > 0;
            PresetId = presetId;
            m_Overrides.Clear();
            if (changed)
                Changed?.Invoke();
            return true;
        }

        public void ResetToPreset()
        {
            if (m_Overrides.Count == 0)
                return;
            m_Overrides.Clear();
            Changed?.Invoke();
        }

        /// <summary>Resolved level row per option id, for the applier.</summary>
        public Dictionary<string, GraphicsOptionLevelRecord> Resolve()
        {
            var result = new Dictionary<string, GraphicsOptionLevelRecord>(StringComparer.Ordinal);
            foreach (var option in m_Catalog.Options)
            {
                var level = option.Find(Get(option.Id));
                if (level != null)
                    result[option.Id] = level.Values;
            }
            return result;
        }

        public GraphicsSettingsState ToState()
        {
            return new GraphicsSettingsState
            {
                presetId = PresetId,
                overrides = m_Overrides.OrderBy(kv => kv.Key)
                    .Select(kv => new GraphicsSettingsState.Override { optionId = kv.Key, levelId = kv.Value }).ToList(),
            };
        }

        /// <summary>Restores a saved state. Unknown presets fall back to the default preset; stale options or levels are dropped.</summary>
        public void Load(GraphicsSettingsState state)
        {
            m_Overrides.Clear();
            PresetId = state != null && m_Catalog.FindPreset(state.presetId) != null ? state.presetId : m_Catalog.DefaultPreset?.Id;
            if (state?.overrides != null)
            {
                foreach (var entry in state.overrides)
                {
                    var option = m_Catalog.FindOption(entry.optionId);
                    if (option == null || option.Find(entry.levelId) == null || entry.levelId == Preset?.DefaultLevel(entry.optionId))
                        continue;
                    m_Overrides[entry.optionId] = entry.levelId;
                }
            }
            Changed?.Invoke();
        }

        public string ToJson() => JsonUtility.ToJson(ToState());

        public bool TryLoadJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;
            try
            {
                var state = JsonUtility.FromJson<GraphicsSettingsState>(json);
                if (state == null)
                    return false;
                Load(state);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public interface IGraphicsSettingsStore
    {
        string Read();
        void Write(string json);
        void Clear();
    }

    /// <summary>PlayerPrefs on this machine only; graphics settings are never synchronised or networked.</summary>
    public sealed class PlayerPrefsGraphicsSettingsStore : IGraphicsSettingsStore
    {
        public const string DefaultKey = "BuddahGo.GraphicsSettings.v1";
        readonly string m_Key;

        public PlayerPrefsGraphicsSettingsStore(string key = DefaultKey) => m_Key = key;

        public string Read() => PlayerPrefs.GetString(m_Key, string.Empty);

        public void Write(string json)
        {
            PlayerPrefs.SetString(m_Key, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(m_Key);
            PlayerPrefs.Save();
        }
    }
}
