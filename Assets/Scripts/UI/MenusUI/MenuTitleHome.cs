using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Title screen and home menu of the menu environment ("一笔" design).
    /// Opening: the screen brightens from black while the camera travels from the torii to the main-menu shot.
    /// The vertical calligraphy title sits in the middle with "按任意键开始" under it and fades out during the
    /// move; any key or click finishes the move quickly. Then the home menu fades in: items on the baseline,
    /// the ink runs along it to the selected item and ends in a vermilion dot; the selected item lifts and
    /// brightens. Items press existing buttons (技能选择 / Start / 皮肤, kept in the invisible TEMP_MenuCanvas/MenuActions
    /// holder since the proto panel was removed), so the camera moves and views
    /// behave exactly as before. The whole UI shows only on the main-menu shot.
    /// Runs after MenuCameraProtoSwitcher.Start (which places the camera on the start shot), then takes the
    /// camera over for the opening. Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class MenuTitleHome : MonoBehaviour
    {
        [System.Serializable]
        public class Item
        {
            public string label;
            [TextArea] public string description;
            public TextMeshProUGUI text;
            [Tooltip("Existing button this item presses (empty: no action yet).")]
            public Button target;
            public bool quits;
            [Tooltip("Opens the settings page.")]
            public bool opensSettings;
        }

        /// <summary>
        /// Label on a leader line beside an object that can be clicked on the main menu, shown while that object
        /// glows under the pointer. It draws in steps: a short stroke appears, the stroke runs out to full length,
        /// then the text comes in at its end. Leaving plays it back the other way, faster.
        /// </summary>
        [System.Serializable]
        public class Marker
        {
            public string label;
            public CanvasGroup group;
            [Tooltip("Hover highlighter target root; the marker shows while that target glows.")]
            public Transform hoverRoot;
            [Tooltip("The marker sits on this point (anchorLocal in the anchor's space).")]
            public Transform anchor;
            public Vector3 anchorLocal;
            [Tooltip("Horizontally filled line: the fill draws it out from its start.")]
            public Image leader;
            public CanvasGroup text;
            [System.NonSerialized] public float progress;
            [System.NonSerialized] public Vector2 textBase;
        }

        [Header("Groups")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private CanvasGroup homeGroup;
        [SerializeField] private TextMeshProUGUI pressAnyKey;

        [Header("Opening camera move (torii → main-menu shot)")]
        [Tooltip("Start pose first, then the points the camera passes through. It ends on the switcher's start shot.")]
        [SerializeField] private Transform[] introPath = new Transform[0];
        [SerializeField] private float introStartFov = 55f;
        [Tooltip("Full-screen black that fades out at the start. It also blocks clicks on the scene during the move.")]
        [SerializeField] private Image blackout;
        [SerializeField] private MenuCameraMouseLook mouseLook;

        [Header("Home")]
        [SerializeField] private Item[] items = new Item[0];
        [SerializeField] private int selected = 1;
        [SerializeField] private RectTransform ink;
        [SerializeField] private RectTransform dot;
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private float inkStartX = 60f;
        [SerializeField] private float itemBaseY = 84f;
        [SerializeField] private float liftY = 10f;
        [SerializeField] private float normalSize = 22f; // T3 (Docs/ui/style-guide.md)
        [SerializeField] private float selectedSize = 22f; // the selected item brightens and lifts but keeps its size (style guide: one rhythm)
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.65f;

        [Header("Object markers (label + leader line, shown while the pointer is over the object)")]
        [SerializeField] private Marker[] markers = new Marker[0];
        [SerializeField] private MenuHoverHighlighter highlighter;
        [Tooltip("Stroke appears, runs out, text comes in.")]
        [SerializeField] private float markerIn = 0.7f;
        [Tooltip("The same steps backwards when the pointer leaves.")]
        [SerializeField] private float markerOut = 0.3f;
        [Tooltip("How far the text slides in along the line's end (canvas pixels).")]
        [SerializeField] private float markerTextSlide = 10f;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuSettingsPage settingsPage;
        [Tooltip("The game's calligraphy title at the top left; hidden while the settings page is open.")]
        [SerializeField] private CanvasGroup logo;
        [Tooltip("The bottom menu (baseline, ink, items, description); hidden while the settings page is open.")]
        [SerializeField] private CanvasGroup menuBar;
        [Tooltip("Skill page on the skill-select shot; the proto panel stays hidden while it is up.")]
        [SerializeField] private MenuSkillPage skillPage;
        [Tooltip("Online pages at the hall door and in the lobby; the proto panel stays hidden while they are up.")]
        [SerializeField] private MenuDojoPage dojoPage;
        [Tooltip("Skin page in the skin close-up; the proto panel stays hidden while it is up.")]
        [SerializeField] private MenuSkinPage skinPage;

        [Header("Menu environment")]
        [SerializeField] private MenuCameraProtoSwitcher switcher;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField] private MenuEmaInspector inspector;
        [Tooltip("Hidden (not disabled, so its buttons still work) while this UI is up.")]
        [SerializeField] private GameObject protoPanel;

        [Header("Timing (seconds)")]
        [Tooltip("The title comes in on black for this long before the screen brightens and the camera moves.")]
        [SerializeField] private float titleLead = 2.2f;
        [SerializeField] private float introDuration = 10f;
        [Tooltip("Black → full brightness, from the start of the move.")]
        [SerializeField] private float brightenDuration = 4f;
        [SerializeField] private float titleFadeIn = 1.5f;
        [Tooltip("Share of the move at which the title starts and finishes fading out.")]
        [SerializeField] private Vector2 titleFadeOut = new Vector2(0.45f, 0.85f);
        [Tooltip("From the start of the title fading in.")]
        [SerializeField] private float pressAnyKeyDelay = 1.5f;
        [Tooltip("Share of the move from which the home menu fades in.")]
        [SerializeField, Range(0f, 1f)] private float homeFadeIn = 0.85f;
        [Tooltip("Length of the quick finish after a key press or click.")]
        [SerializeField] private float skipDuration = 0.8f;
        [Tooltip("Smooth frames in a row (under 1/30 s) needed before the opening starts.")]
        [SerializeField] private int settleFrames = 10;
        [Tooltip("Longest wait on black for the frame time to settle.")]
        [SerializeField] private float maxSettleWait = 3f;
        [SerializeField] private float inkSpeed = 9f;

        private const float MaxStep = 0.1f; // longest time one frame may advance the opening

        private bool _home;
        private bool _transition;
        private bool _skip;
        private float _titleAlpha;
        private float _promptAlpha;
        private float _inkWidth;
        private float[] _lift;
        private CanvasGroup _protoGroup;
        private RectTransform _canvasRect;

        public void Configure(CanvasGroup rootGroup, CanvasGroup title, CanvasGroup home, TextMeshProUGUI anyKey, Image black,
                              Transform[] path, Item[] list, RectTransform inkRect, RectTransform dotRect, TextMeshProUGUI desc,
                              Marker[] markerList, Camera cam, MenuCameraProtoSwitcher sw, MenuCameraDirector dir,
                              MenuSkinView skin, MenuEmaInspector insp, MenuCameraMouseLook look, MenuHoverHighlighter hover,
                              GameObject proto)
        {
            root = rootGroup; titleGroup = title; homeGroup = home; pressAnyKey = anyKey; blackout = black; introPath = path;
            items = list; ink = inkRect; dot = dotRect; description = desc; markers = markerList;
            targetCamera = cam; switcher = sw; director = dir; skinView = skin; inspector = insp; mouseLook = look; highlighter = hover; protoPanel = proto;
        }

        /// <summary>The opening is over and the home menu is up.</summary>
        public bool IsHome => _home;

        /// <summary>Finishes the opening quickly (back from a match into the room).</summary>
        public void SkipIntro()
        {
            if (!_home) _skip = true;
        }

        public void SetSkillPage(MenuSkillPage page) => skillPage = page;

        public void SetDojoPage(MenuDojoPage page) => dojoPage = page;

        public void SetSkinPage(MenuSkinPage page) => skinPage = page;

        public void SetSettingsPage(MenuSettingsPage page, CanvasGroup logoGroup, CanvasGroup bar)
        {
            settingsPage = page;
            logo = logoGroup;
            menuBar = bar;
        }

        private void Start()
        {
            _lift = new float[items.Length];
            _canvasRect = transform as RectTransform;
            if (protoPanel != null)
            {
                // Not `??`: a missing component is a Unity fake null, which `??` treats as an object.
                _protoGroup = protoPanel.GetComponent<CanvasGroup>();
                if (_protoGroup == null) _protoGroup = protoPanel.AddComponent<CanvasGroup>();
            }
            homeGroup.alpha = 0f;
            homeGroup.blocksRaycasts = false;
            foreach (Marker m in markers)
            {
                if (m.text != null) m.textBase = ((RectTransform)m.text.transform).anchoredPosition;
                ApplyMarker(m);
            }
            for (int i = 0; i < items.Length; i++) ApplyItem(i, 1f);
            _inkWidth = 0f;
            if (description != null) description.text = items.Length > selected ? items[selected].description : "";
            StartCoroutine(Intro());
        }

        /// <summary>On the main-menu shot with nothing else going on.</summary>
        private bool MenuIdle =>
            (switcher == null || switcher.AtStartShot)
            && (director == null || !director.IsAway)
            && (skinView == null || !skinView.IsActive)
            && (inspector == null || !inspector.IsInspecting);

        private void Update()
        {
            bool idle = MenuIdle;
            float dt = Time.unscaledDeltaTime;
            root.alpha = Mathf.MoveTowards(root.alpha, idle ? 1f : 0f, dt / 0.3f);
            root.blocksRaycasts = idle;
            if (_protoGroup != null)
            {
                bool hide = idle || (skillPage != null && skillPage.IsShown) || (dojoPage != null && dojoPage.IsShown)
                            || (skinPage != null && skinPage.IsShown);
                _protoGroup.alpha = hide ? 0f : 1f;
                _protoGroup.blocksRaycasts = !hide;
            }

            if (!_home)
            {
                if (_transition && !_skip && AnyPressed()) _skip = true;
                float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f));
                if (pressAnyKey != null) pressAnyKey.alpha = _promptAlpha * pulse;
                return;
            }

            bool inSettings = settingsPage != null && settingsPage.IsOpen;
            if (idle && !_transition && !inSettings && !MenuConfirmDialog.Blocking) HandleInput();
            if (!idle && inSettings) settingsPage.Close();
            // The logo and the bottom menu give way to the settings page and to the confirm dialog (one focus).
            bool covered = inSettings || MenuConfirmDialog.IsOpen;
            if (logo != null) logo.alpha = Mathf.MoveTowards(logo.alpha, covered ? 0f : 1f, dt / 0.25f);
            if (menuBar != null) menuBar.alpha = Mathf.MoveTowards(menuBar.alpha, covered ? 0f : 1f, dt / 0.25f);
            for (int i = 0; i < items.Length; i++)
            {
                _lift[i] = Mathf.MoveTowards(_lift[i], i == selected ? 1f : 0f, dt / 0.18f);
                ApplyItem(i, 1f);
            }
            UpdateInk(dt);
            UpdateMarkers(idle, dt);
        }

        /// <summary>The opening: black → scene while the camera travels the path to the main-menu shot.</summary>
        private IEnumerator Intro()
        {
            _transition = true;
            Transform endPose = switcher != null ? switcher.StartPose : null;
            Camera cam = targetCamera;
            bool move = cam != null && endPose != null && introPath != null && introPath.Length > 0 && introPath[0] != null;
            float endFov = switcher != null ? switcher.StartFieldOfView : (cam != null ? cam.fieldOfView : 60f);
            Path path = null;
            Quaternion r0 = Quaternion.identity, r1 = Quaternion.identity;
            if (move)
            {
                var points = new List<Vector3>();
                foreach (Transform t in introPath) if (t != null) points.Add(t.position);
                points.Add(endPose.position);
                path = new Path(points);
                r0 = introPath[0].rotation;
                r1 = endPose.rotation;
                // The mouse look puts the camera back on its base pose when disabled, so turn it off first.
                if (mouseLook != null) mouseLook.enabled = false;
                PlaceCamera(path, r0, r1, endFov, 0f);
            }
            SetBlack(1f, true);
            SetTitle(0f, 0f);

            // The first frames after loading hitch; hold on black until the frame time settles so the
            // move is not over before it can be seen.
            int smooth = 0;
            for (float wait = 0f; wait < maxSettleWait && smooth < settleFrames && !_skip; wait += Time.unscaledDeltaTime)
            {
                yield return null;
                smooth = Time.unscaledDeltaTime < 1f / 30f ? smooth + 1 : 0;
            }

            // The title first, on black.
            for (float lead = 0f; lead < titleLead && !_skip; )
            {
                yield return null;
                lead += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                SetTitle(Mathf.SmoothStep(0f, 1f, lead / titleFadeIn), Mathf.SmoothStep(0f, 1f, (lead - pressAnyKeyDelay) / 0.8f));
            }

            // Then the screen brightens while the camera travels.
            float time = 0f;
            while (time < introDuration && !_skip)
            {
                yield return null;
                // Capped step: a slow frame slows the move down instead of skipping part of it.
                time += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                float k = Mathf.Clamp01(time / introDuration);
                if (move) PlaceCamera(path, r0, r1, endFov, Ease(k));
                SetBlack(1f - Mathf.SmoothStep(0f, 1f, time / brightenDuration), true);
                float since = titleLead + time;
                float fadeIn = Mathf.SmoothStep(0f, 1f, since / titleFadeIn);
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(titleFadeOut.x, titleFadeOut.y, k));
                float prompt = Mathf.SmoothStep(0f, 1f, (since - pressAnyKeyDelay) / 0.8f);
                SetTitle(fadeIn * fadeOut, prompt * fadeOut);
                homeGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(homeFadeIn, 1f, k));
            }

            if (_skip)
            {
                // Quick finish from wherever the move is.
                Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
                Quaternion r = cam != null ? cam.transform.rotation : Quaternion.identity;
                float fov = cam != null ? cam.fieldOfView : endFov;
                float black = blackout != null ? blackout.color.a : 0f;
                float title = _titleAlpha, prompt = _promptAlpha, home = homeGroup.alpha;
                for (float t = 0f; t < skipDuration; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep))
                {
                    float e = Mathf.SmoothStep(0f, 1f, t / skipDuration);
                    if (move)
                    {
                        cam.transform.SetPositionAndRotation(Vector3.Lerp(p, endPose.position, e), Quaternion.Slerp(r, endPose.rotation, e));
                        cam.fieldOfView = Mathf.Lerp(fov, endFov, e);
                    }
                    SetBlack(Mathf.Lerp(black, 0f, e), true);
                    float fade = 1f - Mathf.Clamp01(e * 2f);
                    SetTitle(title * fade, prompt * fade);
                    homeGroup.alpha = Mathf.Lerp(home, 1f, e);
                    yield return null;
                }
            }

            if (move && switcher != null) switcher.ApplyStartShot(); // exact pose, mouse look back on
            else if (mouseLook != null) mouseLook.enabled = true;
            SetBlack(0f, false);
            SetTitle(0f, 0f);
            titleGroup.gameObject.SetActive(false);
            homeGroup.alpha = 1f;
            homeGroup.blocksRaycasts = true;
            _home = true;
            _transition = false;
        }

        private void PlaceCamera(Path path, Quaternion r0, Quaternion r1, float endFov, float u)
        {
            targetCamera.transform.SetPositionAndRotation(path.At(u), Quaternion.Slerp(r0, r1, u));
            targetCamera.fieldOfView = Mathf.Lerp(introStartFov, endFov, u);
        }

        /// <summary>Slow start and slow arrival.</summary>
        private static float Ease(float k) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * k);

        private void SetBlack(float alpha, bool blockClicks)
        {
            if (blackout == null) return;
            Color c = blackout.color;
            c.a = alpha;
            blackout.color = c;
            blackout.raycastTarget = blockClicks;
        }

        private void SetTitle(float title, float prompt)
        {
            _titleAlpha = title;
            _promptAlpha = prompt;
            titleGroup.alpha = title;
        }

        /// <summary>Smooth curve through points (Catmull-Rom), walked at an even speed.</summary>
        private sealed class Path
        {
            private readonly List<Vector3> _p;
            private readonly float[] _length;
            private const int Samples = 256;

            public Path(List<Vector3> points)
            {
                _p = points;
                _length = new float[Samples + 1];
                Vector3 prev = Raw(0f);
                for (int i = 1; i <= Samples; i++)
                {
                    Vector3 next = Raw(i / (float)Samples);
                    _length[i] = _length[i - 1] + Vector3.Distance(prev, next);
                    prev = next;
                }
            }

            public Vector3 At(float u)
            {
                float goal = Mathf.Clamp01(u) * _length[Samples];
                int i = 1;
                while (i < Samples && _length[i] < goal) i++;
                float span = _length[i] - _length[i - 1];
                float f = span > 0f ? (goal - _length[i - 1]) / span : 0f;
                return Raw((i - 1 + f) / Samples);
            }

            private Vector3 Raw(float t)
            {
                int segments = _p.Count - 1;
                if (segments <= 0) return _p[0];
                float x = Mathf.Clamp01(t) * segments;
                int s = Mathf.Min((int)x, segments - 1);
                float f = x - s;
                Vector3 a = _p[Mathf.Max(s - 1, 0)], b = _p[s], c = _p[s + 1], d = _p[Mathf.Min(s + 2, segments)];
                return 0.5f * (2f * b + (c - a) * f + (2f * a - 5f * b + 4f * c - d) * f * f + (3f * b - a - 3f * c + d) * f * f * f);
            }
        }

        private static bool AnyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Gamepad.current != null)
                foreach (var c in Gamepad.current.allControls)
                    if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
            return false;
        }

        private void HandleInput()
        {
            int move = 0;
            bool submit = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) move--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) move++;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) submit = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) move--;
                if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) move++;
                if (pad.buttonSouth.wasPressedThisFrame) submit = true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                for (int i = 0; i < items.Length; i++)
                {
                    if (!MenuHit.Contains(items[i].text.rectTransform, at, new Vector2(16f, 14f))) continue;
                    if (mouse.delta.ReadValue().sqrMagnitude > 0f) Select(i);
                    if (mouse.leftButton.wasPressedThisFrame) { Select(i); submit = true; }
                }
            }
            if (move != 0) Select((selected + move + items.Length) % items.Length);
            if (submit) Submit(items[selected]);
        }

        private void Select(int index)
        {
            if (index == selected) return;
            selected = index;
            if (description != null) description.text = items[index].description;
        }

        private void Submit(Item item)
        {
            if (item.opensSettings)
            {
                if (settingsPage != null) settingsPage.Open();
                return;
            }
            if (item.quits)
            {
                if (MenuConfirmDialog.Instance != null)
                    MenuConfirmDialog.Instance.Show("离开道场", "今日修行到此为止？", "离开", Application.Quit, "留下");
                else Application.Quit();
                return;
            }
            if (item.target != null && item.target.IsActive() && item.target.interactable) item.target.onClick.Invoke();
        }

        private void ApplyItem(int i, float _)
        {
            Item item = items[i];
            if (item.text == null) return;
            float l = _lift == null ? (i == selected ? 1f : 0f) : _lift[i];
            float e = l * l * (3f - 2f * l);
            item.text.fontSize = Mathf.Lerp(normalSize, selectedSize, e);
            item.text.alpha = Mathf.Lerp(dimAlpha, 1f, e);
            var rt = item.text.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, itemBaseY + liftY * e);
        }

        private void UpdateInk(float dt)
        {
            if (ink == null || items.Length == 0) return;
            RectTransform target = items[selected].text.rectTransform;
            float end = target.anchoredPosition.x + items[selected].text.preferredWidth * 0.5f + 20f;
            float goal = end - inkStartX;
            _inkWidth = Mathf.Lerp(_inkWidth, goal, 1f - Mathf.Exp(-inkSpeed * dt));
            ink.sizeDelta = new Vector2(_inkWidth, ink.sizeDelta.y);
            if (dot != null) dot.anchoredPosition = new Vector2(inkStartX + _inkWidth + 6f, dot.anchoredPosition.y);
        }

        private void UpdateMarkers(bool idle, float dt)
        {
            Transform hovered = idle && !_transition && highlighter != null ? highlighter.HoveredRoot : null;
            foreach (Marker m in markers)
            {
                if (m.group == null) continue;
                bool show = hovered != null && hovered == m.hoverRoot;
                m.progress = Mathf.MoveTowards(m.progress, show ? 1f : 0f, dt / (show ? markerIn : markerOut));
                ApplyMarker(m);
                if (m.progress <= 0f || m.anchor == null || targetCamera == null || _canvasRect == null) continue;
                Vector3 screen = targetCamera.WorldToScreenPoint(m.anchor.TransformPoint(m.anchorLocal));
                Vector2 local;
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local))
                    ((RectTransform)m.group.transform).anchoredPosition = local;
            }
        }

        /// <summary>The marker's steps at its progress: stroke appears (0–0.18), runs out (0.12–0.62), text comes in (0.55–1).</summary>
        private void ApplyMarker(Marker m)
        {
            if (m.group == null) return;
            float p = m.progress;
            m.group.alpha = p > 0f ? 1f : 0f;
            if (m.leader != null)
            {
                float appear = Smooth(p / 0.18f);
                float run = Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.62f, p));
                run = 1f - (1f - run) * (1f - run) * (1f - run); // quick start, soft arrival, like a brush stroke
                m.leader.fillAmount = Mathf.Lerp(0f, 0.06f, appear) + 0.94f * run;
                Color c = m.leader.color;
                c.a = 0.6f * appear;
                m.leader.color = c;
            }
            if (m.text != null)
            {
                float t = Smooth(Mathf.InverseLerp(0.55f, 1f, p));
                m.text.alpha = t;
                ((RectTransform)m.text.transform).anchoredPosition = m.textBase + new Vector2(-markerTextSlide * (1f - t), 0f);
            }
        }

        private static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, x);
    }
}
