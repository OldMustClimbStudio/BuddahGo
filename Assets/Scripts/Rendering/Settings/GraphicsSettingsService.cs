using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddahGo.Rendering.Settings
{
    // Entry point for a graphics-settings menu. Options, levels, presets and their label keys come from the design
    // tables (Graphics.xlsx); the choice is stored in PlayerPrefs on this machine and applied at startup.
    // A menu binds to it like this:
    //   enumerate  Catalog.Options (LabelKey, GroupId, Levels[].LabelKey) and Catalog.Presets; show PresetLabelKey
    //   read       Get(optionId), IsOverridden(optionId), IsAvailable(optionId, levelId)
    //   change     Set(optionId, levelId) / SetPreset(presetId) / ResetToPreset() -> Changed fires (preview is up to the UI)
    //   confirm    Apply() applies and saves; Revert() reloads the saved choice and applies it
    // Text: DesignTableOverlay.Text(labelKey, fallback). Graphics settings are client-only and never networked.
    public static class GraphicsSettingsService
    {
        static GraphicsSettingsModel s_Model;
        static GraphicsSettingsApplier s_Applier;
        static IGraphicsSettingsStore s_Store;
        static string s_StartupState;   // the choice applied at startup, which Revert falls back to when nothing is saved

        public static event Action Changed;
        public static event Action Applied;

        /// <summary>False when the tables hold no graphics settings; the game then runs on the quality level as configured.</summary>
        public static bool IsReady => s_Model != null;
        public static GraphicsSettingsCatalog Catalog => s_Model?.Catalog;
        public static GraphicsSettingsModel Model => s_Model;
        public static string PresetId => s_Model?.PresetId;
        public static string PresetLabelKey => s_Model?.PresetLabelKey;
        public static bool IsCustom => s_Model != null && s_Model.IsCustom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            Initialize(GraphicsSettingsCatalog.FromDatabase(DesignTableOverlay.Db), new PlayerPrefsGraphicsSettingsStore(), new GraphicsSettingsApplier(), apply: true);
            if (IsReady)
                GraphicsSettingsDebugPanel.Install();
        }

        /// <summary>
        /// Builds the model from <paramref name="catalog"/> and the saved choice. Without a saved choice the preset matching
        /// the current quality level is used (so a first run changes nothing), else the table's default preset.
        /// </summary>
        public static void Initialize(GraphicsSettingsCatalog catalog, IGraphicsSettingsStore store, GraphicsSettingsApplier applier, bool apply)
        {
            if (s_Model != null)
                s_Model.Changed -= OnModelChanged;
            s_Model = null;
            s_Store = store;
            s_Applier = applier;
            if (catalog == null || catalog.IsEmpty)
                return;
            foreach (var error in catalog.Errors)
                Debug.LogWarning("[GraphicsSettings] " + error);
            s_Model = new GraphicsSettingsModel(catalog);
            if (!s_Model.TryLoadJson(store?.Read()))
            {
                string current = QualitySettings.names[QualitySettings.GetQualityLevel()];
                foreach (var preset in catalog.Presets)
                    if (preset.QualityLevel == current)
                    {
                        s_Model.SetPreset(preset.Id);
                        break;
                    }
            }
            s_StartupState = s_Model.ToJson();
            s_Model.Changed += OnModelChanged;
            if (apply)
                s_Applier?.Apply(s_Model);
        }

        public static string Get(string optionId) => s_Model?.Get(optionId);
        public static bool IsOverridden(string optionId) => s_Model != null && s_Model.IsOverridden(optionId);
        public static bool Set(string optionId, string levelId) => s_Model != null && s_Model.Set(optionId, levelId);
        public static bool SetPreset(string presetId) => s_Model != null && s_Model.SetPreset(presetId);
        public static void ResetToPreset() => s_Model?.ResetToPreset();

        /// <summary>Whether a level can take effect on the current preset (SSAO needs the feature in its renderer).</summary>
        public static bool IsAvailable(string optionId, string levelId)
        {
            var option = s_Model?.Catalog.FindOption(optionId);
            var level = option?.Find(levelId);
            return level != null && GraphicsSettingsApplier.IsLevelAvailable(s_Model.Preset, optionId, level.Values);
        }

        /// <summary>Applies the current choice and saves it.</summary>
        public static void Apply()
        {
            if (s_Model == null)
                return;
            s_Applier?.Apply(s_Model);
            s_Store?.Write(s_Model.ToJson());
            Applied?.Invoke();
        }

        /// <summary>Reloads the saved choice (or the startup choice when nothing is saved) and applies it.</summary>
        public static void Revert()
        {
            if (s_Model == null)
                return;
            if (!s_Model.TryLoadJson(s_Store?.Read()))
                s_Model.TryLoadJson(s_StartupState);
            s_Applier?.Apply(s_Model);
            Applied?.Invoke();
        }

        static void OnModelChanged() => Changed?.Invoke();
    }
}
