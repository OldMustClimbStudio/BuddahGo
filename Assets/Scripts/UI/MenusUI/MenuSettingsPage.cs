using System.Collections.Generic;
using System.Linq;
using BuddahGo.Audio;
using BuddahGo.Rendering.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Settings page of the menu environment ("一笔" design, v0.2 mockup 08), opened from 设置 on the home menu, which
    /// stays on the baseline below it. Tabs 画面 / 音频 / 操作. 操作 lists the keys (race, then menu) as rows to read, not to change. 音频 shows <see cref="AudioSettingsService"/>:
    /// 总音量 / 音乐 / 音效 as a value and a thin bar (step, or click / drag on the bar), and 切到后台时静音 on / off; audio
    /// changes take effect and are saved at once, R puts the defaults back. The graphics rows come from
    /// <see cref="GraphicsSettingsService"/> at runtime, the same model as the old SettingsPanelUI: a preset row, then
    /// one row per option. The selected row brightens (same size) with a vermilion dot before it and ‹ › around its value;
    /// stepping changes the model only ("＊" while an option differs from the preset), Enter applies and saves, R puts
    /// the preset back, Esc closes (with unapplied changes, after the confirm dialog: 应用 / 放弃 / 继续调整). Options that do not take effect now are dimmed and
    /// cannot be stepped; restart-only ones carry a tag. Keyboard / gamepad: up-down choose, left-right step, Q / E tab.
    /// Mouse: hover chooses, click steps (‹ back, anything else forward), wheel scrolls, the hints at the foot click.
    /// Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSettingsPage : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [Tooltip("Slides in from the left as the page opens.")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI[] tabs = new TextMeshProUGUI[0];
        [SerializeField] private RectTransform tabInk;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform dot;
        [SerializeField] private TextMeshProUGUI placeholder;
        [SerializeField] private TextMeshProUGUI note;
        [SerializeField] private TextMeshProUGUI applyHint;
        [SerializeField] private TextMeshProUGUI resetHint;
        [SerializeField] private TextMeshProUGUI backHint;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite hairline;

        [Header("Rows")]
        [SerializeField] private float rowHeight = 52f;
        [SerializeField] private float rowWidth = 480f;
        [SerializeField] private float labelX = 0f; // flush with the title, tabs and hairlines; the dot hangs in the margin
        [SerializeField] private float valueRight = 480f; // the column's right edge, where the hairlines end
        [SerializeField] private float normalSize = 22f; // T3 (Docs/ui/style-guide.md)
        [SerializeField] private float selectedSize = 22f; // the selected row brightens but keeps its size, so the rows keep one rhythm
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.65f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.3f;

        [Header("Timing (seconds)")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.2f;
        [Tooltip("Delay between one row coming in and the next.")]
        [SerializeField] private float rowStagger = 0.035f;
        [SerializeField] private float slide = 28f;

        private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
        private const string PresetRow = "__preset";
        private const string AudioPrefix = "audio.";
        private const int GraphicsTab = 0, AudioTab = 1;
        private const int ControlsTab = 2;
        private const string ControlsPrefix = "controls.";
        private const float BarX = 190f, BarWidth = 180f;

        private sealed class Row
        {
            public string id;
            public RectTransform rect;
            public TextMeshProUGUI label, value, previous, next;
            public RectTransform bar;      // volume rows only: the track, the click / drag area
            public Image track, fill;
            public float lift;
            public float fillWidth;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Row> _audioRows = new List<Row>();
        private RectTransform _audioContent;
        private readonly List<Row> _controlRows = new List<Row>();
        private RectTransform _controlsContent;

        private List<Row> Rows => _tab == AudioTab ? _audioRows : _tab == ControlsTab ? _controlRows : _rows;
        private RectTransform Container => _tab == AudioTab ? _audioContent : _tab == ControlsTab ? _controlsContent : content;
        private bool HasRows => _tab == AudioTab ? _audioContent != null
                              : _tab == ControlsTab ? _controlsContent != null
                              : _tab == GraphicsTab && GraphicsSettingsService.IsReady;
        private bool _open;
        private bool _built;
        private float _openT;      // 0 closed … 1 open, in seconds of openDuration
        private float _sinceOpen;  // seconds since Open, for the rows coming in one after another
        private int _tab;
        private int _selected;
        private float _scroll;
        private float _inkX, _inkW;
        private int _openedFrame = -1;
        private bool _dirty = true;

        public bool IsOpen => _open;

        public void Configure(CanvasGroup g, RectTransform p, TextMeshProUGUI[] tabTexts, RectTransform ink, RectTransform view,
                              RectTransform rows, RectTransform dotRect, TextMeshProUGUI empty, TextMeshProUGUI noteText,
                              TextMeshProUGUI apply, TextMeshProUGUI reset, TextMeshProUGUI back, TMP_FontAsset f, Sprite line)
        {
            group = g; panel = p; tabs = tabTexts; tabInk = ink; viewport = view; content = rows; dot = dotRect; placeholder = empty;
            note = noteText; applyHint = apply; resetHint = reset; backHint = back; font = f; hairline = line;
        }

        private void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void OnEnable()
        {
            GraphicsSettingsService.Changed += MarkDirty;
            GraphicsSettingsService.Applied += MarkDirty;
            AudioSettingsService.Changed += MarkDirty;
        }

        private void OnDisable()
        {
            GraphicsSettingsService.Changed -= MarkDirty;
            GraphicsSettingsService.Applied -= MarkDirty;
            AudioSettingsService.Changed -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        public void Open()
        {
            if (_open) return;
            if (!_built) Build();
            _open = true;
            _openedFrame = Time.frameCount; // the key that opened the page must not also act on it
            _openT = 0f;
            _sinceOpen = 0f;
            _dirty = true;
            group.blocksRaycasts = true;
            SelectTab(0, true);
        }

        /// <summary>
        /// The player's 返回: with unapplied graphics changes the confirm dialog asks first (应用 applies them and closes,
        /// 放弃 drops them and closes, Esc stays on the page); otherwise closes.
        /// </summary>
        public void Back()
        {
            if (!_open) return;
            if (GraphicsSettingsService.HasPendingChanges && MenuConfirmDialog.Instance != null)
            {
                MenuConfirmDialog.Instance.Show("未应用的改动", "返回将放弃这些改动。", "应用",
                    () => { GraphicsSettingsService.Apply(); Close(); }, "继续调整", null, "放弃", Close);
                return;
            }
            Close();
        }

        /// <summary>Drops unapplied changes and closes.</summary>
        public void Close()
        {
            if (!_open) return;
            if (GraphicsSettingsService.HasPendingChanges) GraphicsSettingsService.Revert();
            _open = false;
            group.blocksRaycasts = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _sinceOpen += dt;
            _openT = Mathf.MoveTowards(_openT, _open ? 1f : 0f, dt / (_open ? openDuration : closeDuration));
            float e = 1f - (1f - _openT) * (1f - _openT);
            group.alpha = e;
            if (panel != null) panel.anchoredPosition = new Vector2(-slide * (1f - e), 0f);
            if (_openT <= 0f && !_open) return;

            if (_open && Time.frameCount != _openedFrame && !MenuConfirmDialog.Blocking) HandleInput();
            if (_dirty) { _dirty = false; RefreshTexts(); }
            Animate(dt);
        }

        // ---------- build ----------

        private void Build()
        {
            _built = true;
            BuildAudio();
            BuildControls();
            var catalog = GraphicsSettingsService.Catalog;
            if (catalog == null) return;
            _rows.Add(MakeRow(content, PresetRow, Text("settings.graphics.preset", "预设")));
            Separator(rowHeight - 8f);
            foreach (var option in catalog.Options.OrderBy(o => System.Array.IndexOf(new[] { "display", "quality", "effects", "performance" }, o.GroupId)).ThenBy(o => o.SortOrder))
            {
                string label = Text(option.LabelKey, option.Id);
                if (option.RestartRequired)
                    label += "  <size=72%><color=#B8352A>" + Text("settings.graphics.tag.restart", "需要重启") + "</color></size>";
                _rows.Add(MakeRow(content, option.Id, label));
            }
            for (int i = 0; i < _rows.Count; i++) _rows[i].rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
            content.sizeDelta = new Vector2(rowWidth, _rows.Count * rowHeight);
        }

        /// <summary>音频 rows, in a container next to the graphics one (only the current tab's container shows).</summary>
        private void BuildAudio()
        {
            var go = new GameObject("AudioContent", typeof(RectTransform));
            _audioContent = (RectTransform)go.transform;
            _audioContent.SetParent(viewport, false);
            _audioContent.anchorMin = _audioContent.anchorMax = _audioContent.pivot = new Vector2(0f, 1f);
            _audioContent.anchoredPosition = Vector2.zero;
            foreach (var (id, label) in new[]
                     {
                         (AudioSettingsService.Master, "总音量"), (AudioSettingsService.Music, "音乐"),
                         (AudioSettingsService.Sfx, "音效"), (AudioSettingsService.Background, "切到后台时静音"),
                     })
            {
                Row row = MakeRow(_audioContent, AudioPrefix + id, label);
                if (!AudioSettingsService.IsToggle(id))
                {
                    row.track = Bar(row.rect, "Track", 6f);
                    row.fill = Bar(row.rect, "Fill", 10f);
                    // Click / drag area: the bar's length, most of the row's height (the line itself is thin).
                    var hit = new GameObject("BarHit", typeof(RectTransform));
                    row.bar = (RectTransform)hit.transform;
                    row.bar.SetParent(row.rect, false);
                    row.bar.anchorMin = row.bar.anchorMax = new Vector2(0f, 0.5f);
                    row.bar.pivot = new Vector2(0f, 0.5f);
                    row.bar.anchoredPosition = new Vector2(BarX, 0f);
                    row.bar.sizeDelta = new Vector2(BarWidth, rowHeight * 0.7f);
                }
                _audioRows.Add(row);
            }
            for (int i = 0; i < _audioRows.Count; i++) _audioRows[i].rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
            _audioContent.sizeDelta = new Vector2(rowWidth, _audioRows.Count * rowHeight);
        }

        private static string Key(string key) => "<sprite name=\"key_" + key + "\" tint=1>";

        /// <summary>
        /// 操作 rows: the race's keys (InputSystem_Actions → Player, Docs/gameplay/README.md §3 键位), a hairline,
        /// then the menu's keys (style guide 8). Read only: chosen like the other rows, never stepped.
        /// </summary>
        private void BuildControls()
        {
            var go = new GameObject("ControlsContent", typeof(RectTransform));
            _controlsContent = (RectTransform)go.transform;
            _controlsContent.SetParent(viewport, false);
            _controlsContent.anchorMin = _controlsContent.anchorMax = _controlsContent.pivot = new Vector2(0f, 1f);
            _controlsContent.anchoredPosition = Vector2.zero;
            var race = new[]
            {
                ("forward", "前进", Key("w")),
                ("steer", "转向", Key("a") + " " + Key("d")),
                ("aim", "瞄准佛手", "鼠标"),
                ("push", "推掌", Key("mouse_left") + " " + Key("mouse_right")),
                ("skill", "施展技能", Key("q") + Key("e") + Key("r") + " / 1 2 3"),
            };
            var menu = new[]
            {
                ("focus", "移动焦点", Key("up") + Key("down") + Key("left") + Key("right")),
                ("confirm", "确认", Key("enter") + " / " + Key("space")),
                ("back", "返回", Key("esc") + " / " + Key("mouse_right")),
                ("tab", "切换页签", Key("q") + " " + Key("e")),
                ("reset", "恢复默认", Key("r")),
            };
            float y = 0f;
            foreach (var (id, label, keys) in race)
            {
                Row row = MakeRow(_controlsContent, ControlsPrefix + id, label);
                row.value.text = keys;
                row.rect.anchoredPosition = new Vector2(0f, -y);
                _controlRows.Add(row);
                y += rowHeight;
            }
            Separator(y - 8f, _controlsContent);
            foreach (var (id, label, keys) in menu)
            {
                Row row = MakeRow(_controlsContent, ControlsPrefix + id, label);
                row.value.text = keys;
                row.rect.anchoredPosition = new Vector2(0f, -y);
                _controlRows.Add(row);
                y += rowHeight;
            }
            _controlsContent.sizeDelta = new Vector2(rowWidth, y);
        }

        private Image Bar(RectTransform row, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(BarX, 0f);
            rt.sizeDelta = new Vector2(BarWidth, height);
            var img = go.GetComponent<Image>();
            img.sprite = hairline;
            img.color = Paper;
            img.raycastTarget = false;
            return img;
        }

        private Row MakeRow(RectTransform parent, string id, string label)
        {
            var go = new GameObject("Row_" + id, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(rowWidth, rowHeight);
            var row = new Row
            {
                id = id, rect = rt,
                label = Label(rt, "Label", label, TextAlignmentOptions.MidlineLeft, 0.08f),
                value = Label(rt, "Value", "", TextAlignmentOptions.MidlineRight, 0.08f),
                previous = Label(rt, "Previous", "‹", TextAlignmentOptions.Center, 0f),
                next = Label(rt, "Next", "›", TextAlignmentOptions.Center, 0f),
            };
            row.label.rectTransform.anchoredPosition = new Vector2(labelX, 0f);
            row.label.rectTransform.pivot = new Vector2(0f, 0.5f);
            row.value.rectTransform.pivot = new Vector2(1f, 0.5f);
            row.previous.rectTransform.sizeDelta = row.next.rectTransform.sizeDelta = new Vector2(30f, rowHeight);
            row.next.rectTransform.anchoredPosition = new Vector2(valueRight - 12f, 0f);
            return row;
        }

        private TextMeshProUGUI Label(RectTransform parent, string name, string text, TextAlignmentOptions align, float spacingEm)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, rowHeight);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = normalSize;
            t.characterSpacing = spacingEm * 100f;
            t.color = Paper;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private void Separator(float y, RectTransform parent = null)
        {
            var go = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : content, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(labelX, -y);
            rt.sizeDelta = new Vector2(valueRight - labelX, 8f);
            var img = go.GetComponent<Image>();
            img.sprite = hairline;
            img.color = new Color(1f, 1f, 1f, 0.18f);
            img.raycastTarget = false;
        }

        // ---------- input ----------

        private void HandleInput()
        {
            int move = 0, step = 0, tab = 0;
            bool apply = false, reset = false, back = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move--;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move++;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step++;
                if (kb.qKey.wasPressedThisFrame) tab--;
                if (kb.eKey.wasPressedThisFrame) tab++;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) apply = true;
                if (kb.rKey.wasPressedThisFrame) reset = true;
                if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) back = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) move--;
                if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) move++;
                if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) step--;
                if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) step++;
                if (pad.leftShoulder.wasPressedThisFrame) tab--;
                if (pad.rightShoulder.wasPressedThisFrame) tab++;
                if (pad.buttonSouth.wasPressedThisFrame) apply = true;
                if (pad.buttonNorth.wasPressedThisFrame) reset = true;
                if (pad.buttonEast.wasPressedThisFrame) back = true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                bool moved = mouse.delta.ReadValue().sqrMagnitude > 0f;
                bool click = mouse.leftButton.wasPressedThisFrame;
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0f) move--;
                if (wheel < 0f) move++;
                if (click || moved)
                    for (int i = 0; i < tabs.Length; i++)
                        if (click && MenuHit.Contains(tabs[i].rectTransform, at, 14f)) SelectTab(i, false);
                if (HasRows && MenuHit.Contains(viewport, at, 0f))
                {
                    List<Row> rows = Rows;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (!MenuHit.Contains(rows[i].rect, at, 0f)) continue;
                        if (moved) _selected = i;
                        // Volume bar: click or drag sets the volume where the pointer is.
                        if (rows[i].bar != null && mouse.leftButton.isPressed && MenuHit.Contains(rows[i].bar, at, 6f))
                        {
                            _selected = i;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rows[i].bar, at, null, out Vector2 local))
                                AudioSettingsService.Set(rows[i].id.Substring(AudioPrefix.Length),
                                    Mathf.RoundToInt(Mathf.Clamp01(local.x / BarWidth) * 100f));
                            continue;
                        }
                        if (click)
                        {
                            _selected = i;
                            step = MenuHit.Contains(rows[i].previous.rectTransform, at, 6f) ? -1 : 1;
                        }
                    }
                }
                if (click && MenuHit.Contains(applyHint.rectTransform, at, 8f)) apply = true;
                if (click && MenuHit.Contains(resetHint.rectTransform, at, 8f)) reset = true;
                if (click && MenuHit.Contains(backHint.rectTransform, at, 8f)) back = true;
                if (mouse.rightButton.wasPressedThisFrame) back = true;
            }

            if (back) { Back(); return; }
            if (tab != 0) SelectTab((_tab + tab + tabs.Length) % tabs.Length, false);
            if (!HasRows || Rows.Count == 0) return;
            if (move != 0) _selected = Mathf.Clamp(_selected + move, 0, Rows.Count - 1);
            if (step != 0) Step(Rows[_selected].id, step);
            if (_tab == AudioTab)
            {
                if (reset) AudioSettingsService.ResetToDefaults();
                return;
            }
            if (_tab == ControlsTab) return; // read only
            if (apply && GraphicsSettingsService.HasPendingChanges) GraphicsSettingsService.Apply();
            if (reset) GraphicsSettingsService.ResetToPreset();
        }

        private void SelectTab(int index, bool reset)
        {
            if (index != _tab) reset = true; // each tab has its own rows
            _tab = index;
            if (reset) { _selected = 0; _scroll = 0f; }
            _dirty = true;
        }

        /// <summary>Previous or next level the current preset allows (wrapping); on the preset row, the previous or next preset.</summary>
        private static void Step(string id, int direction)
        {
            if (id.StartsWith(ControlsPrefix)) return; // the keys are shown, not changed
            if (id.StartsWith(AudioPrefix))
            {
                AudioSettingsService.Step(id.Substring(AudioPrefix.Length), direction);
                return;
            }
            var catalog = GraphicsSettingsService.Catalog;
            if (catalog == null) return;
            if (id == PresetRow)
            {
                var presets = catalog.Presets.OrderBy(p => p.SortOrder).ToList();
                int at = presets.FindIndex(p => p.Id == GraphicsSettingsService.PresetId);
                if (presets.Count > 0) GraphicsSettingsService.SetPreset(presets[(Mathf.Max(at, 0) + direction + presets.Count) % presets.Count].Id);
                return;
            }
            if (!GraphicsSettingsService.IsActive(id)) return;
            var option = catalog.FindOption(id);
            var levels = option?.Levels.Where(l => GraphicsSettingsService.IsAvailable(id, l.Id)).ToList();
            if (levels == null || levels.Count == 0) return;
            int index = Mathf.Max(0, levels.FindIndex(l => l.Id == GraphicsSettingsService.Get(id)));
            GraphicsSettingsService.Set(id, levels[(index + direction + levels.Count) % levels.Count].Id);
        }

        // ---------- show ----------

        private bool Steppable(Row row) => row.id == PresetRow || row.id.StartsWith(AudioPrefix) || GraphicsSettingsService.IsActive(row.id);

        private void RefreshTexts()
        {
            bool ready = GraphicsSettingsService.IsReady;
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(!HasRows);
                placeholder.text = _tab != GraphicsTab ? "尚未开放" : Text("settings.graphics.status.unavailable", "配表里没有画面设置");
            }
            content.gameObject.SetActive(_tab == GraphicsTab && ready);
            if (_audioContent != null) _audioContent.gameObject.SetActive(_tab == AudioTab);
            if (_controlsContent != null) _controlsContent.gameObject.SetActive(_tab == ControlsTab);
            if (dot != null) dot.gameObject.SetActive(HasRows);
            if (resetHint != null)
            {
                resetHint.text = "<sprite name=\"key_r\" tint=1> " + (_tab == GraphicsTab ? "恢复预设" : "恢复默认");
                resetHint.alpha = _tab == ControlsTab ? 0.35f : 0.8f; // nothing to reset on 操作
            }

            foreach (Row row in _audioRows)
            {
                string id = row.id.Substring(AudioPrefix.Length);
                row.value.text = AudioSettingsService.IsToggle(id)
                    ? (AudioSettingsService.MuteInBackground ? "开" : "关")
                    : AudioSettingsService.Get(id).ToString();
            }
            if (_tab == ControlsTab)
            {
                if (note != null) note.text = "施展技能：比赛中按下一槽的键（Q / E / R 或 1 / 2 / 3）";
                if (applyHint != null) applyHint.alpha = 0.3f;
                return;
            }
            if (_tab == AudioTab)
            {
                if (note != null) note.text = "改动立即生效并保存在本机";
                if (applyHint != null) applyHint.alpha = 0.3f;
                return;
            }
            if (!ready) return;

            foreach (Row row in _rows)
            {
                if (row.id == PresetRow)
                {
                    row.value.text = Text(GraphicsSettingsService.PresetLabelKey, GraphicsSettingsService.PresetId);
                    continue;
                }
                var level = GraphicsSettingsService.Catalog.FindOption(row.id)?.Find(GraphicsSettingsService.Get(row.id));
                row.value.text = (level != null ? Text(level.LabelKey, level.Id) : "-") + (GraphicsSettingsService.IsOverridden(row.id) ? "＊" : "");
            }

            bool pending = GraphicsSettingsService.HasPendingChanges;
            string status = pending ? "有未应用的改动　按 <sprite name=\"key_enter\" tint=1> 应用" : "＊ 与预设不同　应用后生效并保存在本机";
            if (GraphicsSettingsService.PendingRestart.Count > 0)
                status = Text("settings.graphics.status.restart", "标有“需要重启”的设置将在重启游戏后生效") + "\n" + status;
            if (note != null) note.text = status;
            if (applyHint != null) applyHint.alpha = pending ? 1f : 0.35f;
        }

        private void Animate(float dt)
        {
            // Tabs and the ink under the current one.
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].alpha = Mathf.MoveTowards(tabs[i].alpha, i == _tab ? 1f : dimAlpha, dt / 0.15f);
            if (tabInk != null && tabs.Length > _tab)
            {
                RectTransform t = tabs[_tab].rectTransform;
                float goalW = tabs[_tab].preferredWidth + 8f;
                float goalX = t.anchoredPosition.x - 4f;
                float k = 1f - Mathf.Exp(-14f * dt);
                if (_inkW <= 0f) { _inkX = goalX; _inkW = 0f; }
                _inkX = Mathf.Lerp(_inkX, goalX, k);
                _inkW = Mathf.Lerp(_inkW, goalW, k);
                tabInk.anchoredPosition = new Vector2(_inkX, tabInk.anchoredPosition.y);
                tabInk.sizeDelta = new Vector2(_inkW, tabInk.sizeDelta.y);
            }
            if (!HasRows || Rows.Count == 0) return;
            List<Row> rows = Rows;

            // Keep the selected row in view.
            float view = viewport.rect.height;
            float top = _selected * rowHeight;
            float goal = _scroll;
            if (top < goal + rowHeight * 0.5f) goal = Mathf.Max(0f, top - rowHeight * 0.5f);
            if (top + rowHeight > goal + view - rowHeight * 0.5f) goal = top + rowHeight * 1.5f - view;
            goal = Mathf.Clamp(goal, 0f, Mathf.Max(0f, rows.Count * rowHeight - view));
            _scroll = Mathf.Lerp(_scroll, goal, 1f - Mathf.Exp(-12f * dt));
            Container.anchoredPosition = new Vector2(Container.anchoredPosition.x, _scroll);

            float shown = _sinceOpen;
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                row.lift = Mathf.MoveTowards(row.lift, i == _selected ? 1f : 0f, dt / 0.16f);
                float e = row.lift * row.lift * (3f - 2f * row.lift);
                // Rows come in one after another from the top as the page opens.
                float reveal = Mathf.SmoothStep(0f, 1f, (shown - i * rowStagger) / 0.18f);
                if (!_open) reveal = 1f;
                bool steppable = Steppable(row);
                bool readable = steppable || row.id.StartsWith(ControlsPrefix); // 操作 rows read normally, without the arrows
                float a = (readable ? Mathf.Lerp(dimAlpha, 1f, e) : inactiveAlpha) * reveal;
                float size = Mathf.Lerp(normalSize, selectedSize, e);
                row.label.fontSize = row.value.fontSize = size;
                row.label.alpha = row.value.alpha = a;
                row.label.rectTransform.anchoredPosition = new Vector2(labelX + 6f * (1f - reveal), 0f);
                float right = valueRight - 34f * e;
                row.value.rectTransform.anchoredPosition = new Vector2(right, 0f);
                float arrows = steppable ? e * reveal : 0f;
                row.previous.alpha = row.next.alpha = arrows;
                row.previous.fontSize = row.next.fontSize = size;
                row.previous.rectTransform.anchoredPosition = new Vector2(right - row.value.preferredWidth - 22f, 0f);
                if (row.fill != null)
                {
                    // Volume bar: a faint track, the ink filled up to the volume.
                    float goalW = BarWidth * AudioSettingsService.Get(row.id.Substring(AudioPrefix.Length)) / 100f;
                    row.fillWidth = Mathf.Lerp(row.fillWidth, goalW, 1f - Mathf.Exp(-16f * dt));
                    row.fill.rectTransform.sizeDelta = new Vector2(row.fillWidth, row.fill.rectTransform.sizeDelta.y);
                    row.fill.color = new Color(Paper.r, Paper.g, Paper.b, a * 0.9f);
                    row.track.color = new Color(Paper.r, Paper.g, Paper.b, 0.22f * reveal);
                }
            }
            if (dot != null)
            {
                float y = viewport.anchoredPosition.y - (_selected * rowHeight + rowHeight * 0.5f) + _scroll;
                Vector2 p = dot.anchoredPosition;
                dot.anchoredPosition = new Vector2(p.x, Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-18f * dt)));
            }
        }

        private static string Text(string key, string fallback) => string.IsNullOrEmpty(key) ? fallback : DesignTableOverlay.Text(key, fallback);
    }
}
