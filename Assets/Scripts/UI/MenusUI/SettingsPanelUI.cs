using System.Collections.Generic;
using System.Linq;
using BuddahGo.Rendering.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The main menu's settings panel. Today it holds one section, graphics, bound to
    /// <see cref="GraphicsSettingsService"/> the way Docs/rendering/graphics-settings.md describes: a preset row, one
    /// row per option (group headers between groups) with "&lt;" / "&gt;" stepping through the levels the current
    /// preset allows, and Apply / Revert / Reset-to-preset buttons. Stepping only changes the model (the label shows
    /// a "*" while the option differs from the preset); Apply applies and saves, Revert reloads the saved choice, Back
    /// drops unapplied changes. Rows are built from the catalog at runtime, so a new table row or display size shows
    /// up without a scene change; labels come from UI/Text through the catalog's label keys (a generated resolution
    /// level has none and shows its id). Future sections (audio, controls) go next to the graphics section.
    /// Options that do not take effect with the current choice (upscaler at 100 %, sharpening without TAA, the frame
    /// cap with VSync on, SSAO without the feature, window and resolution in the editor) are dimmed and cannot be
    /// stepped; options marked restartRequired carry a "需要重启" tag, and after Apply a changed one shows the restart
    /// notice until the next start (GraphicsSettingsService.IsActive / PendingRestart, shared with the F9 panel).
    /// The scene objects are created by Assets/Editor/UI/SettingsPanelBuilder.cs.
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        static readonly string[] s_Groups = { "display", "quality", "effects", "performance" };

        [Header("Graphics section")]
        [SerializeField] private TMP_Text presetLabel;
        [SerializeField] private Transform presetButtonsParent;
        [SerializeField] private Button presetButtonTemplate;
        [SerializeField] private Transform rowsParent;
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private GameObject groupHeaderTemplate;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Actions")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button revertButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;
        [SerializeField] private MainMenuUI mainMenu;

        sealed class Row
        {
            public string OptionId;
            public TMP_Text Value;
            public Button Previous, Next;
            public CanvasGroup Group;
        }

        // Rows of options that do not take effect (GraphicsSettingsService.IsActive) are dimmed to this alpha.
        const float InactiveAlpha = 0.4f;

        readonly List<Row> _rows = new List<Row>();
        readonly List<(string presetId, Button button, TMP_Text label)> _presetButtons = new List<(string, Button, TMP_Text)>();
        bool _built;

        void Awake()
        {
            if (applyButton != null) applyButton.onClick.AddListener(OnApply);
            if (revertButton != null) revertButton.onClick.AddListener(OnRevert);
            if (resetButton != null) resetButton.onClick.AddListener(OnReset);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (groupHeaderTemplate != null) groupHeaderTemplate.SetActive(false);
            if (presetButtonTemplate != null) presetButtonTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GraphicsSettingsService.Changed += Refresh;
            GraphicsSettingsService.Applied += Refresh;
            if (!_built)
                Build();
            Refresh();
        }

        void OnDisable()
        {
            GraphicsSettingsService.Changed -= Refresh;
            GraphicsSettingsService.Applied -= Refresh;
        }

        /// <summary>Creates the preset buttons and option rows from the catalog; nothing when the tables hold no settings.</summary>
        void Build()
        {
            var catalog = GraphicsSettingsService.Catalog;
            if (catalog == null)
                return;
            _built = true;
            if (presetButtonTemplate != null && presetButtonsParent != null)
            {
                foreach (var preset in catalog.Presets)
                {
                    var button = Instantiate(presetButtonTemplate, presetButtonsParent);
                    button.gameObject.SetActive(true);
                    button.name = "Preset_" + preset.Id;
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                        label.text = Text(preset.LabelKey, preset.Id);
                    string id = preset.Id;
                    button.onClick.AddListener(() => GraphicsSettingsService.SetPreset(id));
                    _presetButtons.Add((preset.Id, button, label));
                }
            }
            if (rowTemplate == null || rowsParent == null)
                return;
            string lastGroup = null;
            foreach (var option in catalog.Options.OrderBy(o => System.Array.IndexOf(s_Groups, o.GroupId)).ThenBy(o => o.SortOrder))
            {
                if (option.GroupId != lastGroup && groupHeaderTemplate != null)
                {
                    var header = Instantiate(groupHeaderTemplate, rowsParent);
                    header.SetActive(true);
                    header.name = "Group_" + option.GroupId;
                    var headerText = header.GetComponentInChildren<TMP_Text>(true);
                    if (headerText != null)
                        headerText.text = Text("settings.graphics.group." + option.GroupId, option.GroupId);
                    lastGroup = option.GroupId;
                }
                var row = Instantiate(rowTemplate, rowsParent);
                row.SetActive(true);
                row.name = "Option_" + option.Id;
                var texts = row.GetComponentsInChildren<TMP_Text>(true);
                var buttons = row.GetComponentsInChildren<Button>(true);
                var entry = new Row
                {
                    OptionId = option.Id,
                    Value = texts.FirstOrDefault(t => t.name == "Value"),
                    Previous = buttons.FirstOrDefault(b => b.name == "Previous"),
                    Next = buttons.FirstOrDefault(b => b.name == "Next"),
                };
                var label = texts.FirstOrDefault(t => t.name == "Label");
                if (label != null)
                    label.text = Text(option.LabelKey, option.Id) + (option.RestartRequired ? RestartTag() : string.Empty);
                entry.Group = row.TryGetComponent(out CanvasGroup group) ? group : row.AddComponent<CanvasGroup>();
                string optionId = option.Id;
                if (entry.Previous != null) entry.Previous.onClick.AddListener(() => Step(optionId, -1));
                if (entry.Next != null) entry.Next.onClick.AddListener(() => Step(optionId, 1));
                _rows.Add(entry);
            }
        }

        /// <summary>Moves an option to the previous or next level the current preset allows, wrapping around.</summary>
        void Step(string optionId, int direction)
        {
            var option = GraphicsSettingsService.Catalog?.FindOption(optionId);
            if (option == null)
                return;
            var levels = option.Levels.Where(l => GraphicsSettingsService.IsAvailable(optionId, l.Id)).ToList();
            if (levels.Count == 0)
                return;
            int index = Mathf.Max(0, levels.FindIndex(l => l.Id == GraphicsSettingsService.Get(optionId)));
            int next = (index + direction + levels.Count) % levels.Count;
            GraphicsSettingsService.Set(optionId, levels[next].Id);
        }

        void Refresh()
        {
            if (!GraphicsSettingsService.IsReady)
            {
                if (statusLabel != null)
                    statusLabel.text = Text("settings.graphics.status.unavailable", "配表里没有画面设置");
                return;
            }
            if (presetLabel != null)
                presetLabel.text = Text("settings.graphics.preset", "预设") + "：" + Text(GraphicsSettingsService.PresetLabelKey, GraphicsSettingsService.PresetId);
            foreach (var (presetId, button, label) in _presetButtons)
            {
                bool current = !GraphicsSettingsService.IsCustom && GraphicsSettingsService.PresetId == presetId;
                button.interactable = !current;
            }
            foreach (var row in _rows)
            {
                var option = GraphicsSettingsService.Catalog.FindOption(row.OptionId);
                var level = option?.Find(GraphicsSettingsService.Get(row.OptionId));
                bool steppable = GraphicsSettingsService.IsActive(row.OptionId);
                if (row.Group != null)
                    row.Group.alpha = steppable ? 1f : InactiveAlpha;
                if (row.Value != null)
                    row.Value.text = (level != null ? Text(level.LabelKey, level.Id) : "-") + (GraphicsSettingsService.IsOverridden(row.OptionId) ? " *" : string.Empty);
                if (row.Previous != null) row.Previous.interactable = steppable;
                if (row.Next != null) row.Next.interactable = steppable;
            }
            bool pending = GraphicsSettingsService.HasPendingChanges;
            if (applyButton != null) applyButton.interactable = pending;
            if (revertButton != null) revertButton.interactable = pending;
            if (statusLabel != null)
            {
                string status = pending
                    ? Text("settings.graphics.status.pending", "有未应用的改动")
                    : Text("settings.graphics.status.applied", "* 与预设不同。改动在点击 应用 后生效并保存在本机。");
                // After Apply, a change to a restart-only option waits for the next start (the usual banner).
                if (GraphicsSettingsService.PendingRestart.Count > 0)
                    status = Text("settings.graphics.status.restart", "标有“需要重启”的设置将在重启游戏后生效") + "\n" + status;
                statusLabel.text = status;
            }
        }

        void OnApply() => GraphicsSettingsService.Apply();
        void OnRevert() => GraphicsSettingsService.Revert();

        void OnReset()
        {
            GraphicsSettingsService.ResetToPreset();
        }

        /// <summary>Back drops unapplied changes, then returns to the previous menu panel.</summary>
        void OnBack()
        {
            if (GraphicsSettingsService.HasPendingChanges)
                GraphicsSettingsService.Revert();
            if (mainMenu != null)
                mainMenu.GoBack();
        }

        static string RestartTag() => "  <size=75%><color=#D08A3A>" + Text("settings.graphics.tag.restart", "需要重启") + "</color></size>";

        static string Text(string key, string fallback) => string.IsNullOrEmpty(key) ? fallback : DesignTableOverlay.Text(key, fallback);
    }
}
