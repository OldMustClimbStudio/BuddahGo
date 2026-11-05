using System;
using System.Collections.Generic;
using System.Linq;
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
        static string s_AppliedState;   // the choice last applied (startup, Apply or Revert), for HasPendingChanges

        public static event Action Changed;
        public static event Action Applied;

        /// <summary>False when the tables hold no graphics settings; the game then runs on the quality level as configured.</summary>
        public static bool IsReady => s_Model != null;
        public static GraphicsSettingsCatalog Catalog => s_Model?.Catalog;
        public static GraphicsSettingsModel Model => s_Model;
        public static string PresetId => s_Model?.PresetId;
        public static string PresetLabelKey => s_Model?.PresetLabelKey;
        public static bool IsCustom => s_Model != null && s_Model.IsCustom;
        /// <summary>Whether the model differs from the choice last applied (a menu enables Apply / Revert on it).</summary>
        public static bool HasPendingChanges => s_Model != null && s_Model.ToJson() != s_AppliedState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var catalog = GraphicsSettingsCatalog.FromDatabase(DesignTableOverlay.Db)
                .WithDisplayResolutions(Screen.resolutions.Select(r => (r.width, r.height)));
            Initialize(catalog, new PlayerPrefsGraphicsSettingsStore(), new GraphicsSettingsApplier(), apply: true);
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
            s_AppliedState = s_StartupState;
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

        /// <summary>
        /// Whether an option takes effect with the current choice: false when another option makes it moot
        /// (<see cref="GraphicsOptionDependencies"/>) or when the preset allows only one of its levels (SSAO without the
        /// feature). The menu and the F9 panel grey such an option out.
        /// </summary>
        public static bool IsActive(string optionId)
        {
            var option = s_Model?.Catalog.FindOption(optionId);
            if (option == null)
                return false;
            if (GraphicsOptionDependencies.InactiveBecause(s_Model.Resolve(), optionId) != null)
                return false;
            // The applier leaves the window alone in the editor and when the command line sets it.
            if ((optionId == GraphicsPipelineMapper.WindowMode || optionId == GraphicsPipelineMapper.Resolution) && !GraphicsSettingsApplier.AppliesDisplay)
                return false;
            return option.Levels.Count(l => GraphicsSettingsApplier.IsLevelAvailable(s_Model.Preset, optionId, l.Values)) > 1;
        }

        /// <summary>Whether an option is marked as taking effect only after a restart (GraphicsOption.restartRequired).</summary>
        public static bool RequiresRestart(string optionId) => s_Model?.Catalog.FindOption(optionId)?.RestartRequired ?? false;

        /// <summary>
        /// Restart-only options whose applied level differs from the one the game started with; the menu shows the
        /// restart notice while any exists. Empty after a restart, since the saved choice is then the startup choice.
        /// </summary>
        public static IReadOnlyList<string> PendingRestart => s_Model == null
            ? (IReadOnlyList<string>)Array.Empty<string>()
            : RestartDifferences(s_Model.Catalog, s_StartupState, s_AppliedState);

        /// <summary>Options marked restartRequired whose level differs between two saved states.</summary>
        public static List<string> RestartDifferences(GraphicsSettingsCatalog catalog, string fromJson, string toJson)
        {
            var result = new List<string>();
            if (catalog == null || fromJson == toJson)
                return result;
            var from = new GraphicsSettingsModel(catalog);
            var to = new GraphicsSettingsModel(catalog);
            if (!from.TryLoadJson(fromJson) || !to.TryLoadJson(toJson))
                return result;
            foreach (var option in catalog.Options)
                if (option.RestartRequired && from.Get(option.Id) != to.Get(option.Id))
                    result.Add(option.Id);
            return result;
        }

        /// <summary>Applies the current choice and saves it.</summary>
        public static void Apply()
        {
            if (s_Model == null)
                return;
            s_Applier?.Apply(s_Model);
            s_Store?.Write(s_Model.ToJson());
            s_AppliedState = s_Model.ToJson();
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
            s_AppliedState = s_Model.ToJson();
            Applied?.Invoke();
        }

        static void OnModelChanged() => Changed?.Invoke();
    }
}
