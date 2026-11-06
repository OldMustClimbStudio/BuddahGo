using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Skill page of the menu environment ("一笔" design, v0.2 mockup 05), shown on the skill-select shot over the ema
    /// wall and hokora. Always there: the title at the top left, 返回 at the bottom left and a thin ensō around the ema
    /// under the pointer, and the loadout: one ensō per hokora slot holding the slotted skill's name (an empty slot is a
    /// faint ring with ＋), drawn on a world-space canvas on the front of the hokora's wooden base, so it keeps the
    /// scene's perspective as the camera turns. A click on the hokora slides in the slot-key panel on the left, over a
    /// dark gradient from the left edge: "技能键", how casting works, then one row per slot with the slotted skill's name and
    /// its keys (the keyboard bindings of InputSystem_Actions Player.SkillSlot1–3, coming in one after another); Esc, a
    /// right-click or a click elsewhere hides it.
    /// While an ema is inspected (MenuEmaInspector moves it to the left of the
    /// screen) the loadout fades out and the right side slides in with its dark gradient: the skill's calligraphy
    /// name, what it does, its backfire, cooldown and duration (from the Skill table), how to put the ema back.
    /// Two brush chevrons flank the inspected ema: clicking one (or A / D, ← / →) switches the inspection to the
    /// previous or next ema without leaving it (MenuEmaInspector.Cycle). They are the page's only raycast targets.
    /// Otherwise the page never blocks the pointer (all its other graphics ignore raycasts), so dragging, inspecting and the glow keep
    /// working under it. Esc or 返回 goes back to the main menu when nothing is inspected or dragged.
    /// Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSkillPage : MonoBehaviour
    {
        [System.Serializable]
        public class SkillArt
        {
            public string skillId;
            [Tooltip("White calligraphy of the skill's name.")]
            public Sprite name;
            [Tooltip("What the skill does, for the player.")]
            [TextArea] public string description;
            [Tooltip("What its backfire (the _anti variant) does.")]
            public string backfire;
            [Tooltip("The skill's name as text (for the hokora note).")]
            public string title;
        }

        [System.Serializable]
        public class Slot
        {
            public RectTransform rect;
            public Image ring;
            public Image art;
            public TextMeshProUGUI plus;
            [System.NonSerialized] public float pop;
            [System.NonSerialized] public float fill;
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraProtoSwitcher switcher;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private MenuSkillLoadout loadout;
        [SerializeField] private MenuHoverHighlighter highlighter;
        [SerializeField] private SkillArt[] arts = new SkillArt[0];

        [Header("Right side")]
        [Tooltip("Dark gradient behind the right side; shown while an ema is inspected.")]
        [SerializeField] private CanvasGroup rightShade;
        [Tooltip("The inspected skill.")]
        [SerializeField] private CanvasGroup panel;
        [Tooltip("World-space canvas on the hokora's base holding the slots; hidden while an ema is inspected.")]
        [SerializeField] private CanvasGroup loadoutGroup;
        [SerializeField] private CanvasGroup nameGroup;
        [SerializeField] private Image nameImage;
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private TextMeshProUGUI backfire;
        [SerializeField] private TextMeshProUGUI stats;
        [SerializeField] private Slot[] slots = new Slot[0];
        [SerializeField] private Sprite ringFilled;
        [SerializeField] private Sprite ringEmpty;

        [Header("Combo panel (after a click on the hokora)")]
        [SerializeField] private CanvasGroup keysGroup;
        [Tooltip("Dark gradient from the left edge behind the combo panel.")]
        [SerializeField] private CanvasGroup leftShade;
        [Tooltip("Per slot, in slot order: the container the key caps go in (they line up from its right edge).")]
        [SerializeField] private RectTransform[] keyRows = new RectTransform[0];
        [Tooltip("Per slot: the slotted skill's calligraphy name.")]
        [SerializeField] private Image[] rowArts = new Image[0];
        [Tooltip("Per slot: \"空\" while the slot is empty.")]
        [SerializeField] private TextMeshProUGUI[] rowEmpty = new TextMeshProUGUI[0];
        [SerializeField] private TextMeshProUGUI comboHeader;
        [SerializeField] private Sprite keycap;
        [Tooltip("Drawn key icons (asset K01), named key_<token in lower case>: key_up, key_w, …; a key without one gets the plain cap and a label.")]
        [SerializeField] private Sprite[] keyIcons = new Sprite[0];
        [SerializeField] private TMP_FontAsset keyFont;
        [SerializeField] private float keySize = 34f;
        [SerializeField] private float keyGap = 7f;
        [SerializeField] private float keyPad = 9f;
        [Tooltip("Delay between one key cap coming in and the next.")]
        [SerializeField] private float keyStagger = 0.06f;
        [SerializeField, Min(0f)] private float clickSlopPixels = 6f;

        [Header("Arrows beside the inspected ema")]
        [SerializeField] private CanvasGroup arrows;
        [SerializeField] private Button previousArrow;
        [SerializeField] private Button nextArrow;
        [Tooltip("Screen gap (canvas pixels) between the ema's edge and an arrow.")]
        [SerializeField] private float arrowGap = 56f;

        [Header("Pointer")]
        [SerializeField] private CanvasGroup hoverRing;
        [SerializeField] private TextMeshProUGUI backHint;

        [Header("Timing (seconds)")]
        [SerializeField] private float fade = 0.3f;
        [SerializeField] private float panelIn = 0.35f;
        [SerializeField] private float panelOut = 0.2f;
        [SerializeField] private float panelSlide = 30f;
        [SerializeField] private float nameOut = 0.12f;
        [SerializeField] private float nameIn = 0.28f;

        private RectTransform _canvasRect;
        private string _focus;
        private string _shownFocus;
        private float _nameT;
        private float _panelT;
        private float _loadoutT;
        private string[] _loadout = new string[0];
        private bool _wasInspecting;
        private float _ringAlpha;
        private float _ringSpin;
        private readonly Dictionary<string, SkillArt> _artOf = new Dictionary<string, SkillArt>();
        private readonly List<List<CanvasGroup>> _keys = new List<List<CanvasGroup>>();
        private bool _keysOpen;
        private float _keysT;
        private float _keysSince;
        private Vector2 _pressAt;
        private bool _pressOnHokora;
        private int _hokoraClickFrame = -1;
        private float _arrowsT;
        private float _nudgePrev, _nudgeNext;
        private readonly List<Renderer> _renderers = new List<Renderer>(); // reused by the per-frame screen placement
        private RectTransform _collapseHint; // the panel's "Esc 收起" (built by MenuTitleHomeBuilder as Hint_Collapse)

        /// <summary>On the skill-select shot with no other view over it (the menu hides its proto panel then).</summary>
        public bool IsShown =>
            switcher != null && switcher.AtSkillSelect
            && (director == null || !director.IsAway)
            && (skinView == null || !skinView.IsActive);

        public void Configure(CanvasGroup g, Camera cam, MenuCameraProtoSwitcher sw, MenuCameraDirector dir, MenuSkinView skin,
                              MenuEmaInspector insp, MenuSkillLoadout lo, MenuHoverHighlighter hover, SkillArt[] artList,
                              CanvasGroup shade, CanvasGroup panelGroup, CanvasGroup loadoutG, CanvasGroup nameG, Image nameImg, TextMeshProUGUI desc, TextMeshProUGUI backfireText,
                              TextMeshProUGUI statsText, Slot[] slotList, Sprite filled, Sprite empty,
                              CanvasGroup ring, TextMeshProUGUI back)
        {
            group = g; targetCamera = cam; switcher = sw; director = dir; skinView = skin; inspector = insp; loadout = lo;
            highlighter = hover; arts = artList; rightShade = shade; panel = panelGroup; loadoutGroup = loadoutG; nameGroup = nameG; nameImage = nameImg; description = desc;
            backfire = backfireText; stats = statsText; slots = slotList; ringFilled = filled; ringEmpty = empty;
            hoverRing = ring; backHint = back;
        }

        [Header("Hokora note (an ema in the hokora under the pointer)")]
        [Tooltip("A leader line drawn out from the ema with its name and \"right click: take it down\", like the home's object markers.")]
        [SerializeField] private CanvasGroup slotNote;
        [SerializeField] private Image noteLeader;
        [SerializeField] private CanvasGroup noteText;
        [SerializeField] private TextMeshProUGUI noteName;
        [SerializeField] private float noteIn = 0.6f;
        [SerializeField] private float noteOut = 0.25f;
        private float _noteT;
        private Transform _noteEma;
        private Vector2 _noteTextBase;

        public void SetSlotNote(CanvasGroup note, Image leader, CanvasGroup text, TextMeshProUGUI name)
        {
            slotNote = note; noteLeader = leader; noteText = text; noteName = name;
        }

        public void SetArrows(CanvasGroup group, Button previous, Button next)
        {
            arrows = group; previousArrow = previous; nextArrow = next;
        }

        public void SetComboKeys(CanvasGroup keys, CanvasGroup shade, RectTransform[] rows, Image[] arts, TextMeshProUGUI[] empty, TextMeshProUGUI header,
                                 Sprite cap, Sprite[] icons, TMP_FontAsset font)
        {
            keysGroup = keys; leftShade = shade; keyRows = rows; rowArts = arts; rowEmpty = empty; comboHeader = header; keycap = cap; keyIcons = icons; keyFont = font;
        }

        private Sprite IconFor(string token)
        {
            string name = "key_" + token.ToLowerInvariant();
            foreach (Sprite s in keyIcons)
                if (s != null && s.name == name) return s;
            return null;
        }

        private void Awake()
        {
            _canvasRect = transform as RectTransform;
            foreach (SkillArt a in arts)
                if (a != null && !string.IsNullOrEmpty(a.skillId)) _artOf[a.skillId] = a;
            group.alpha = 0f;
            // Only the arrows take the pointer (their own group turns that on while they show).
            group.blocksRaycasts = true;
            group.interactable = true;
            if (panel != null) panel.alpha = 0f;
            if (loadoutGroup != null) loadoutGroup.alpha = 0f;
            if (rightShade != null) rightShade.alpha = 0f;
            if (nameGroup != null) nameGroup.alpha = 0f;
            if (hoverRing != null) hoverRing.alpha = 0f;
            foreach (Slot s in slots) ApplySlot(s, null);
            if (keysGroup != null) keysGroup.alpha = 0f;
            if (leftShade != null) leftShade.alpha = 0f;
            if (arrows != null) { arrows.alpha = 0f; arrows.blocksRaycasts = false; }
            if (previousArrow != null) previousArrow.onClick.AddListener(() => Cycle(-1));
            if (nextArrow != null) nextArrow.onClick.AddListener(() => Cycle(1));
            BuildKeys();
            if (keysGroup != null) _collapseHint = keysGroup.transform.Find("Hint_Collapse") as RectTransform;
        }

        /// <summary>
        /// Keyboard tokens ("Q", "1") bound to each skill slot, read once from InputSystem_Actions Player.SkillSlot1–3
        /// (the same source as the race HUD's SkillSlotUI).
        /// </summary>
        private static List<string>[] SlotKeys()
        {
            var keys = new List<string>[3];
            InputSystem_Actions actions = null;
            try
            {
                actions = new InputSystem_Actions();
                InputAction[] slotActions = { actions.Player.SkillSlot1, actions.Player.SkillSlot2, actions.Player.SkillSlot3 };
                for (int i = 0; i < slotActions.Length; i++)
                {
                    keys[i] = new List<string>();
                    foreach (InputBinding binding in slotActions[i].bindings)
                    {
                        const string prefix = "<Keyboard>/";
                        string path = binding.effectivePath;
                        if (binding.isComposite || binding.isPartOfComposite || path == null || !path.StartsWith(prefix)) continue;
                        keys[i].Add(path.Substring(prefix.Length).ToUpperInvariant());
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (actions != null) Destroy(actions.asset);
            }
            return keys;
        }

        /// <summary>Key caps for each slot's keys (Q / 1, E / 2, R / 3 today).</summary>
        private void BuildKeys()
        {
            List<string>[] slotKeys = SlotKeys();
            for (int i = 0; i < keyRows.Length; i++)
            {
                var caps = new List<CanvasGroup>();
                _keys.Add(caps);
                RectTransform row = keyRows[i];
                List<string> sequence = i < slotKeys.Length && slotKeys[i] != null ? slotKeys[i] : new List<string>();
                int n = sequence.Count;
                for (int k = 0; k < n; k++)
                {
                    var go = new GameObject("Key_" + k, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(row, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(keySize, keySize);
                    rt.anchoredPosition = new Vector2(-keySize * 0.5f - (n - 1 - k) * (keySize + keyGap), 0f);
                    var img = go.GetComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0.9f);
                    img.raycastTarget = false;
                    var g = go.GetComponent<CanvasGroup>();
                    g.alpha = 0f;
                    caps.Add(g);
                    Sprite icon = IconFor(sequence[k]);
                    if (icon != null)
                    {
                        img.sprite = icon;
                        continue;
                    }
                    img.sprite = keycap;
                    img.type = Image.Type.Sliced;
                    var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    var lrt = (RectTransform)label.transform;
                    lrt.SetParent(rt, false);
                    lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                    var t = label.GetComponent<TextMeshProUGUI>();
                    t.font = keyFont;
                    t.text = KeyLabel(sequence[k]);
                    t.fontSize = keySize * 0.55f;
                    t.alignment = TextAlignmentOptions.Center;
                    t.color = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
                    t.raycastTarget = false;
                }
            }
        }

        private static string KeyLabel(string token)
        {
            switch (token)
            {
                case "Up": return "↑";
                case "Down": return "↓";
                case "Left": return "←";
                case "Right": return "→";
                default: return token;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool shown = IsShown;
            group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / fade);
            bool inspecting = inspector != null && inspector.IsInspecting;
            if (group.alpha <= 0f && _loadoutT <= 0f && !shown) { _wasInspecting = inspecting; return; }

            // A click on the hokora shows the combos one frame later, unless it opened an ema in the hokora instead.
            if (_hokoraClickFrame >= 0 && Time.frameCount > _hokoraClickFrame)
            {
                if (!inspecting && !_keysOpen) { _keysOpen = true; _keysSince = 0f; }
                _hokoraClickFrame = -1;
            }
            if (!shown || inspecting) _keysOpen = false;

            _panelT = Step(panel, _panelT, shown && inspecting, dt);
            _keysT = Step(keysGroup, _keysT, shown && _keysOpen, dt, -1f);
            if (rightShade != null) rightShade.alpha = Ease(_panelT);
            if (leftShade != null) leftShade.alpha = Ease(_keysT);
            // The corner hint gives way to the combo panel's own (same place) and to the inspect panel's usage line.
            if (backHint != null) backHint.alpha = 0.8f * (1f - Mathf.Max(Ease(_keysT), Ease(_panelT)));
            // The loadout on the hokora's base: there on the skill shot, out of the way while an ema is inspected.
            _loadoutT = Mathf.MoveTowards(_loadoutT, shown && !inspecting ? 1f : 0f, dt / (inspecting ? panelOut : panelIn));
            if (loadoutGroup != null) loadoutGroup.alpha = Ease(_loadoutT);
            UpdateArrows(shown && inspecting, dt);
            UpdateLoadout(dt);
            UpdateKeys(dt);
            UpdateFocus(dt);
            Transform hoveredEma = shown && !inspecting ? HoveredEma() : null;
            UpdateRing(hoveredEma, dt);
            UpdateNote(hoveredEma != null && loadout.IsInHokora(hoveredEma) && !_keysOpen ? hoveredEma : null, dt);
            if (shown) HandleInput(inspecting);
            _wasInspecting = inspecting;
        }

        // ---------- right panel ----------

        /// <summary>Fades and slides one side group in or out (side +1 slides in from the right, -1 from the left); returns its new progress.</summary>
        private float Step(CanvasGroup g, float t, bool open, float dt, float side = 1f)
        {
            t = Mathf.MoveTowards(t, open ? 1f : 0f, dt / (open ? panelIn : panelOut));
            if (g == null) return t;
            float e = Ease(t);
            g.alpha = e;
            ((RectTransform)g.transform).anchoredPosition = new Vector2(side * panelSlide * (1f - e), 0f);
            return t;
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);

        private void UpdateFocus(float dt)
        {
            string goal = loadout != null && inspector != null ? loadout.SkillOf(inspector.CurrentItem) : null;
            if (goal != null) _focus = goal;

            // Old name out, new name in (rising a little).
            if (_focus != _shownFocus)
            {
                _nameT = Mathf.MoveTowards(_nameT, 0f, dt / nameOut);
                if (_nameT <= 0f || _panelT <= 0f)
                {
                    _nameT = 0f;
                    _shownFocus = _focus;
                    ShowSkill(_focus);
                }
            }
            else _nameT = Mathf.MoveTowards(_nameT, 1f, dt / nameIn);
            float e = 1f - (1f - _nameT) * (1f - _nameT);
            if (nameGroup != null)
            {
                nameGroup.alpha = e;
                ((RectTransform)nameGroup.transform).anchoredPosition = new Vector2(0f, -10f * (1f - e));
            }
        }

        private void ShowSkill(string id)
        {
            _artOf.TryGetValue(id ?? "", out SkillArt art);
            if (nameImage != null)
            {
                nameImage.sprite = art != null ? art.name : null;
                nameImage.enabled = nameImage.sprite != null;
            }
            if (description != null)
                description.text = art != null && !string.IsNullOrEmpty(art.description) ? art.description : Text("skill." + id + ".desc", "");
            if (backfire != null) backfire.text = art != null && !string.IsNullOrEmpty(art.backfire) ? "<color=#B8352A>反噬</color>　" + art.backfire + BackfirePenalty() : "";
            if (stats != null) stats.text = Stats(id);
        }

        /// <summary>"冷却 5 秒　持续 3 秒" from the Skill table (empty when the row is missing).</summary>
        private static string Stats(string id)
        {
            var db = DesignTableOverlay.Db;
            if (db == null || db.skills == null || id == null) return "";
            SkillRecord row = db.skills.Find(r => r.skillId == id);
            if (row == null) return "";
            string text = "冷却 " + Seconds(row.cooldownSeconds) + " 秒";
            if (row.effectDurationSeconds > 0f) text += "　持续 " + Seconds(row.effectDurationSeconds) + " 秒";
            return text;
        }

        /// <summary>
        /// The unified backfire penalty that follows every skill's own backfire (Obsession.backfireLockSeconds: all slots
        /// locked, top speed lowered); empty when the table has no row.
        /// </summary>
        private static string BackfirePenalty()
        {
            var db = DesignTableOverlay.Db;
            ObsessionRecord row = db != null && db.obsessions != null ? db.obsessions.Find(r => r.profileId == "default") : null;
            return row != null && row.backfireLockSeconds > 0f ? "，随后 " + Seconds(row.backfireLockSeconds) + " 秒技能全锁、限速降低" : "";
        }

        private static string Seconds(float s) => Mathf.Approximately(s, Mathf.Round(s)) ? Mathf.RoundToInt(s).ToString() : s.ToString("0.#");

        // ---------- loadout ----------

        private void UpdateLoadout(float dt)
        {
            IReadOnlyList<string> now = loadout != null ? loadout.Loadout : null;
            int n = now != null ? now.Count : 0;
            if (_loadout.Length != n) _loadout = new string[n];
            for (int i = 0; i < slots.Length; i++)
            {
                string id = i < n ? now[i] : null;
                if (i < _loadout.Length && _loadout[i] != id)
                {
                    _loadout[i] = id;
                    slots[i].pop = 1f; // a small swell when a slot changes
                    ApplySlot(slots[i], id);
                }
                Slot s = slots[i];
                s.pop = Mathf.MoveTowards(s.pop, 0f, dt / 0.35f);
                s.fill = Mathf.MoveTowards(s.fill, id != null ? 1f : 0f, dt / 0.2f);
                float p = s.pop * s.pop;
                s.rect.localScale = Vector3.one * (1f + 0.12f * p);
                if (s.ring != null) s.ring.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 0.92f, s.fill));
                if (s.art != null) s.art.color = new Color(1f, 1f, 1f, s.fill);
                if (s.plus != null) s.plus.alpha = 0.4f * (1f - s.fill);
            }
        }

        private void ApplySlot(Slot s, string id)
        {
            if (s.ring != null) s.ring.sprite = id != null ? ringFilled : ringEmpty;
            if (s.art != null)
            {
                s.art.sprite = id != null && _artOf.TryGetValue(id, out SkillArt art) ? art.name : null;
                s.art.enabled = s.art.sprite != null;
            }
        }

        // ---------- arrows ----------

        private void Cycle(int direction)
        {
            if (inspector == null || !inspector.CanCycle) return;
            inspector.Cycle(direction);
            if (direction < 0) _nudgePrev = 1f; else _nudgeNext = 1f;
        }

        private void UpdateArrows(bool open, float dt)
        {
            if (arrows == null) return;
            _arrowsT = Mathf.MoveTowards(_arrowsT, open ? 1f : 0f, dt / (open ? panelIn : panelOut));
            arrows.alpha = Ease(_arrowsT);
            arrows.blocksRaycasts = open && _arrowsT > 0.5f;
            if (open)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)) Cycle(-1);
                if (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) Cycle(1);
                Gamepad pad = Gamepad.current;
                if (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame)) Cycle(-1);
                if (pad != null && (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)) Cycle(1);
            }
            _nudgePrev = Mathf.MoveTowards(_nudgePrev, 0f, dt / 0.25f);
            _nudgeNext = Mathf.MoveTowards(_nudgeNext, 0f, dt / 0.25f);
            if (_arrowsT <= 0f || inspector == null || inspector.CurrentItem == null || targetCamera == null) return;

            // Flank the ema where it is on screen (it moves in, turns, and changes when switching).
            if (!ScreenRect(inspector.CurrentItem, out Rect r)) return;
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            float breathe = 3f * Mathf.Sin(Time.unscaledTime * 2.4f);
            Place(previousArrow, new Vector2(r.xMin, r.center.y), -1f, breathe, _nudgePrev, pointer);
            Place(nextArrow, new Vector2(r.xMax, r.center.y), 1f, breathe, _nudgeNext, pointer);
        }

        /// <summary>An arrow just outside the ema's edge, breathing outwards, kicked further out right after a click; brighter and larger under the pointer.</summary>
        private void Place(Button arrow, Vector2 screenEdge, float side, float breathe, float nudge, Vector2 pointer)
        {
            if (arrow == null) return;
            var rt = (RectTransform)arrow.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenEdge, null, out Vector2 edge)) return;
            float kick = 14f * Mathf.Sin(nudge * Mathf.PI);
            rt.anchoredPosition = edge + new Vector2(side * (arrowGap + breathe + kick), 0f);
            bool hover = MenuHit.Contains(rt, pointer, 12f);
            float scale = Mathf.MoveTowards(rt.localScale.x, hover ? 1.15f : 1f, Time.unscaledDeltaTime / 0.12f);
            rt.localScale = Vector3.one * scale;
            if (arrow.targetGraphic != null)
                arrow.targetGraphic.color = new Color(1f, 1f, 1f, hover ? 1f : 0.75f);
        }

        /// <summary>Screen rectangle of an object's visible renderers (projected bounds corners).</summary>
        private bool ScreenRect(Transform item, out Rect rect)
        {
            rect = default;
            bool any = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            item.GetComponentsInChildren(_renderers);
            foreach (Renderer r in _renderers)
            {
                if (!r.enabled || r.name == "HoverOutline") continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 sp = targetCamera.WorldToScreenPoint(corner);
                    if (sp.z <= 0f) continue;
                    any = true;
                    min = Vector2.Min(min, sp); max = Vector2.Max(max, sp);
                }
            }
            if (!any) return false;
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        // ---------- combo keys ----------

        private void UpdateKeys(float dt)
        {
            if (keysGroup == null) return;
            _keysSince += dt;
            if (_keysT <= 0f) return;
            int filled = 0;
            for (int i = 0; i < keyRows.Length; i++)
            {
                string id = i < _loadout.Length ? _loadout[i] : null;
                if (id != null) filled++;
                Sprite art = id != null && _artOf.TryGetValue(id, out SkillArt a) ? a.name : null;
                if (i < rowArts.Length && rowArts[i] != null)
                {
                    rowArts[i].sprite = art;
                    rowArts[i].enabled = art != null;
                }
                if (i < rowEmpty.Length && rowEmpty[i] != null) rowEmpty[i].gameObject.SetActive(art == null);
                // Key caps come in one after another, row by row.
                List<CanvasGroup> caps = i < _keys.Count ? _keys[i] : null;
                if (caps == null) continue;
                for (int k = 0; k < caps.Count; k++)
                {
                    float t = _keysOpen ? Mathf.SmoothStep(0f, 1f, (_keysSince - 0.12f - (i * 2 + k) * keyStagger) / 0.16f) : 1f;
                    caps[k].alpha = t;
                    caps[k].transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, t);
                }
            }
            if (comboHeader != null) comboHeader.text = "小祠　已装配 " + filled + " / " + keyRows.Length;
        }

        // ---------- pointer ----------

        /// <summary>Skill ema under the pointer (glowing), not while one is dragged.</summary>
        private Transform HoveredEma()
        {
            if (highlighter == null || loadout == null || loadout.IsDragging) return null;
            Transform root = highlighter.HoveredRoot;
            return loadout.SkillOf(root) != null ? root : null;
        }

        /// <summary>
        /// The hokora note: while an ema hanging in the hokora is under the pointer, a short stroke appears beside its ring,
        /// runs out to the upper left, then the skill's name and "right click: take it down" come in at its end (the same
        /// steps as the home's object markers); leaving plays it back, faster.
        /// </summary>
        private void UpdateNote(Transform ema, float dt)
        {
            if (slotNote == null) return;
            if (ema != null && ema != _noteEma)
            {
                _noteEma = ema;
                string id = loadout.SkillOf(ema);
                SkillArt art = System.Array.Find(arts, a => a.skillId == id);
                if (noteName != null) noteName.text = art != null && !string.IsNullOrEmpty(art.title) ? art.title : "";
            }
            _noteT = Mathf.MoveTowards(_noteT, ema != null ? 1f : 0f, dt / (ema != null ? noteIn : noteOut));
            float p = _noteT;
            slotNote.alpha = p > 0f ? 1f : 0f;
            if (noteLeader != null)
            {
                float appear = Mathf.SmoothStep(0f, 1f, p / 0.18f);
                float run = Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.62f, p));
                run = 1f - (1f - run) * (1f - run) * (1f - run); // quick start, soft arrival, like a brush stroke
                noteLeader.fillAmount = Mathf.Lerp(0f, 0.06f, appear) + 0.94f * run;
                Color c = noteLeader.color;
                c.a = 0.6f * appear;
                noteLeader.color = c;
            }
            if (noteText != null)
            {
                var textRect = (RectTransform)noteText.transform;
                if (_noteTextBase == Vector2.zero) _noteTextBase = textRect.anchoredPosition;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, p));
                noteText.alpha = t;
                textRect.anchoredPosition = _noteTextBase + new Vector2(10f * (1f - t), 0f); // slides in along the line, leftwards
            }
            // Follows the ema the note was drawn for, also while it fades out.
            if (p <= 0f || _noteEma == null || targetCamera == null || _canvasRect == null) return;
            Bounds b = new Bounds(_noteEma.position, Vector3.zero);
            bool any = false;
            _noteEma.GetComponentsInChildren(_renderers);
            foreach (Renderer r in _renderers)
            {
                if (!r.enabled || r.name == "HoverOutline") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Vector3 screen = targetCamera.WorldToScreenPoint(b.center);
            if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                ((RectTransform)slotNote.transform).anchoredPosition = local;
        }

        private void UpdateRing(Transform hovered, float dt)
        {
            if (hoverRing == null) return;
            _ringAlpha = Mathf.MoveTowards(_ringAlpha, hovered != null ? 1f : 0f, dt / 0.18f);
            hoverRing.alpha = _ringAlpha;
            _ringSpin += dt * 12f;
            var rt = (RectTransform)hoverRing.transform;
            rt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - _ringAlpha) * (1f - _ringAlpha));
            rt.localEulerAngles = new Vector3(0f, 0f, -_ringSpin);
            if (hovered == null || targetCamera == null || _canvasRect == null) return;
            Bounds b = new Bounds(hovered.position, Vector3.zero);
            bool any = false;
            hovered.GetComponentsInChildren(_renderers);
            foreach (Renderer r in _renderers)
            {
                if (!r.enabled || r.name == "HoverOutline") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Vector3 screen = targetCamera.WorldToScreenPoint(b.center);
            if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                rt.anchoredPosition = local;
        }

        private void HandleInput(bool inspecting)
        {
            // Esc while inspecting hangs the ema back (MenuEmaInspector); Esc with the combos shown hides them; only Esc
            // with nothing open goes back.
            if (inspecting || _wasInspecting || (loadout != null && loadout.IsDragging)) return;
            bool cancel = false, back = false;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) cancel = true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) cancel = true;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                bool onHokora = loadout != null && loadout.PointsAtHokora(at);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _pressAt = at;
                    _pressOnHokora = onHokora;
                    // 返回 only while it shows (it gives way to the panel's 收起, which closes the panel instead).
                    if (_keysOpen) { if (MenuHit.Contains(_collapseHint, at, 10f)) cancel = true; }
                    else if (MenuHit.Contains(backHint != null ? backHint.rectTransform : null, at, 10f)) back = true;
                }
                // A click (press and release without moving) on the hokora shows the combos; anywhere else hides them.
                if (mouse.leftButton.wasReleasedThisFrame && (at - _pressAt).magnitude <= clickSlopPixels)
                {
                    if (_pressOnHokora && onHokora) _hokoraClickFrame = Time.frameCount;
                    else _keysOpen = false;
                }
                // Right-click on an ema in the hokora takes it down (MenuSkillLoadout); elsewhere it hides the combos.
                if (mouse.rightButton.wasPressedThisFrame && !onHokora) _keysOpen = false;
            }
            if (cancel)
            {
                if (_keysOpen) _keysOpen = false;
                else back = true;
            }
            if (back && switcher != null)
            {
                int menu = switcher.IndexOf("主菜单");
                if (menu >= 0) switcher.SwitchTo(menu);
            }
        }

        private static string Text(string key, string fallback) => DesignTableOverlay.Text(key, fallback);
    }
}
