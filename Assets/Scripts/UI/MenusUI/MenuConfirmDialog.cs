using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The menu's one confirm dialog ("一笔" design, v0.2 mockup 10; style guide 4, page template 4): a deep gradient from
    /// the right edge, and on one centred column a title, the text held between two hairlines, and under them a row of
    /// buttons — the confirm action, an optional second action, and the one that keeps you where you were (Esc).
    /// A washi tag comes in behind the button under the pointer (or the one ← → choose): its text turns to ink on it, the
    /// tag glides from button to button with a small press as it lands, and fades away when the pointer is over none of
    /// them (as MenuHoverTag does for the skin names). The confirm action has the focus when the dialog opens. Enter (or a click) takes the focused button, marked by the
    /// Enter key in its label; Esc or a right click cancels from anywhere. While it is open, and on the frame it closes,
    /// <see cref="Blocking"/> is true: the pages check it before reading their own input, so the key that answers the
    /// dialog does not also act on the page under it. Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuConfirmDialog : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [Tooltip("Title, lines, text and buttons; slides in a little from the right.")]
        [SerializeField] private RectTransform column;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI body;
        [Tooltip("The row under the lower hairline; its buttons are laid out centred on it.")]
        [SerializeField] private RectTransform buttons;
        [Tooltip("The washi tag behind the focused button.")]
        [SerializeField] private RectTransform tag;
        [SerializeField] private TextMeshProUGUI confirmLabel;
        [SerializeField] private TextMeshProUGUI altLabel;
        [SerializeField] private TextMeshProUGUI cancelLabel;
        [Tooltip("Paper on each side of a label together: the torn edges take part of it, the key and the word stay inside.")]
        [SerializeField] private float tagPadding = 72f;
        [Tooltip("Between two buttons' slots (each slot is as wide as its tag would be).")]
        [SerializeField] private float gap = 8f;
        [SerializeField] private float slide = 24f;
        [SerializeField] private float openDuration = 0.3f;  // style guide 7: standard
        [SerializeField] private float closeDuration = 0.15f; // fast
        [Tooltip("How quickly the tag glides to the focused button.")]
        [SerializeField] private float glide = 16f;

        private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
        private static readonly Color Ink = new Color32(0x1E, 0x1A, 0x18, 0xFF);
        private const string EnterKey = "<sprite name=\"key_enter\" tint=1> ";
        private const string EscKey = "<sprite name=\"key_esc\" tint=1> ";
        private const float RestAlpha = 0.65f;

        public static MenuConfirmDialog Instance { get; private set; }

        /// <summary>Open now (the pages under it hide what would compete with it).</summary>
        public static bool IsOpen => Instance != null && Instance._open;

        /// <summary>Pages read no input while this is true: the dialog is open or closed this frame.</summary>
        public static bool Blocking => Instance != null && (Instance._open || Instance._closedFrame == Time.frameCount);

        private sealed class Choice
        {
            public TextMeshProUGUI label;
            public string word;
            public bool esc;      // the cancel: always shows its Esc key
            public Action action;
            public float focus;   // 0 … 1, how far the tag and the ink have come onto it
        }

        private readonly Choice[] _choices = new Choice[3];
        private int _count;
        private bool _open;
        private int _openedFrame = -1;
        private int _closedFrame = -1;
        private float _t;
        private Vector2 _columnBase;
        private int _focus;
        private float _tagX, _tagW, _press;
        private bool _showTag;   // the pointer is over a button, or the keys moved the focus
        private float _tagVis;
        private Image _tagImage;

        public void Configure(CanvasGroup g, RectTransform col, TextMeshProUGUI titleText, TextMeshProUGUI bodyText, RectTransform row,
                              RectTransform tagRect, TextMeshProUGUI confirmText, TextMeshProUGUI altText, TextMeshProUGUI cancelText)
        {
            group = g; column = col; title = titleText; body = bodyText; buttons = row; tag = tagRect;
            confirmLabel = confirmText; altLabel = altText; cancelLabel = cancelText;
        }

        private void Awake()
        {
            Instance = this;
            if (column != null) _columnBase = column.anchoredPosition;
            if (tag != null) _tagImage = tag.GetComponentInChildren<Image>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Opens the dialog. `confirm` is the first button and has the focus; `alt` (optional) a second one beside it;
        /// `cancel` the last, which Esc also takes (it calls `onCancel`, if any).
        /// </summary>
        public void Show(string titleText, string bodyText, string confirm, Action onConfirm, string cancel, Action onCancel = null,
                         string alt = null, Action onAlt = null)
        {
            _open = true;
            _openedFrame = Time.frameCount; // the key that opened it must not also answer it
            title.text = titleText;
            body.text = bodyText;
            _count = 0;
            _choices[_count++] = new Choice { label = confirmLabel, word = confirm, action = onConfirm };
            bool hasAlt = !string.IsNullOrEmpty(alt);
            altLabel.gameObject.SetActive(hasAlt);
            if (hasAlt) _choices[_count++] = new Choice { label = altLabel, word = alt, action = onAlt };
            _choices[_count++] = new Choice { label = cancelLabel, word = cancel, esc = true, action = onCancel };
            _focus = 0;
            _choices[0].focus = 1f;
            group.blocksRaycasts = true;
            Layout();
            RefreshLabels();
            // The tag starts on the confirm button.
            RectTransform first = _choices[0].label.rectTransform;
            _tagX = first.anchoredPosition.x;
            _tagW = first.sizeDelta.x;
            _press = 0f;
            _showTag = false;
            _tagVis = 0f;
            ApplyChoices();
        }

        /// <summary>
        /// Each button's slot is as wide as the tag on its focused label (with its key), so the row does not shift as the
        /// focus moves; the slots are centred on the column.
        /// </summary>
        private void Layout()
        {
            float total = -gap;
            var widths = new float[_count];
            for (int i = 0; i < _count; i++)
            {
                widths[i] = _choices[i].label.GetPreferredValues(Shown(_choices[i], true)).x + tagPadding;
                total += widths[i] + gap;
            }
            float x = -total * 0.5f;
            for (int i = 0; i < _count; i++)
            {
                RectTransform rt = _choices[i].label.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
                rt.sizeDelta = new Vector2(widths[i], buttons.rect.height);
                x += widths[i] + gap;
            }
        }

        private static string Shown(Choice c, bool focused) => (c.esc ? EscKey : focused ? EnterKey : "") + c.word;

        private void RefreshLabels()
        {
            for (int i = 0; i < _count; i++) _choices[i].label.text = Shown(_choices[i], i == _focus);
        }

        private void SetFocus(int index)
        {
            index = Mathf.Clamp(index, 0, _count - 1);
            if (index == _focus) return;
            _focus = index;
            _press = 1f; // the tag gives a small press as it moves onto the new button
            RefreshLabels();
        }

        private void Close(Action then)
        {
            if (!_open) return;
            _open = false;
            _closedFrame = Time.frameCount;
            group.blocksRaycasts = false;
            for (int i = 0; i < _count; i++) _choices[i].action = null;
            then?.Invoke();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t = Mathf.MoveTowards(_t, _open ? 1f : 0f, dt / (_open ? openDuration : closeDuration));
            // In: ease-out cubic; out: ease-in quad (style guide 7).
            float e = _open ? 1f - Mathf.Pow(1f - _t, 3f) : _t * _t;
            group.alpha = e;
            if (column != null) column.anchoredPosition = _columnBase + new Vector2(slide * (1f - e), 0f);
            if (!_open && _t <= 0f) return;

            if (_open && Time.frameCount != _openedFrame) HandleInput();

            for (int i = 0; i < _count; i++)
                _choices[i].focus = Mathf.MoveTowards(_choices[i].focus, i == _focus ? 1f : 0f, dt / 0.15f);
            _tagVis = Mathf.MoveTowards(_tagVis, _showTag ? 1f : 0f, dt / 0.15f);
            RectTransform target = _choices[_focus].label.rectTransform;
            // Gliding only while it shows; it comes in straight on the button it appears on.
            float k = _tagVis > 0f ? 1f - Mathf.Exp(-glide * dt) : 1f;
            _tagX = Mathf.Lerp(_tagX, target.anchoredPosition.x, k);
            _tagW = Mathf.Lerp(_tagW, target.sizeDelta.x, k);
            _press = Mathf.MoveTowards(_press, 0f, dt / 0.25f);
            ApplyChoices();
            // The tag unfolds a little as the dialog opens; as it moves it dips and comes back (a brush lifted and set down).
            float vis = _tagVis * _tagVis * (3f - 2f * _tagVis);
            float unfold = Mathf.Lerp(0.94f, 1f, e) * Mathf.Lerp(0.92f, 1f, vis);
            float dip = Mathf.Sin(_press * Mathf.PI) * 0.06f;
            tag.localScale = new Vector3(unfold * (1f + dip * 0.5f), 1f - dip, 1f);
        }

        private void ApplyChoices()
        {
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0.5f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.anchoredPosition = new Vector2(_tagX, 0f);
            tag.sizeDelta = new Vector2(_tagW, buttons.rect.height);
            float vis = _tagVis * _tagVis * (3f - 2f * _tagVis);
            if (_tagImage != null) _tagImage.color = new Color(1f, 1f, 1f, vis);
            for (int i = 0; i < _count; i++)
            {
                Choice c = _choices[i];
                float f = c.focus * c.focus * (3f - 2f * c.focus);
                // Rest: paper-white, dim; focused: full white, or ink while the tag is under it.
                Color rest = new Color(Paper.r, Paper.g, Paper.b, RestAlpha);
                c.label.color = Color.Lerp(Color.Lerp(rest, Paper, f), Ink, f * vis);
            }
        }

        private void HandleInput()
        {
            int move = 0;
            bool submit = false, cancel = false;
            int clicked = -1;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) move--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) move++;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) submit = true;
                if (kb.escapeKey.wasPressedThisFrame) cancel = true;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) move--;
                if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) move++;
                if (pad.buttonSouth.wasPressedThisFrame) submit = true;
                if (pad.buttonEast.wasPressedThisFrame) cancel = true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                bool moved = mouse.delta.ReadValue().sqrMagnitude > 0f;
                bool click = mouse.leftButton.wasPressedThisFrame;
                int over = -1;
                for (int i = 0; i < _count; i++)
                    if (Hit(_choices[i].label.rectTransform, at, 0f)) over = i;
                if (moved)
                {
                    _showTag = over >= 0;
                    if (over >= 0) SetFocus(over);
                }
                if (click && over >= 0) clicked = over;
                if (mouse.rightButton.wasPressedThisFrame) cancel = true;
            }

            if (cancel) { Close(_choices[_count - 1].action); return; }
            if (move != 0) { _showTag = true; SetFocus(_focus + move); }
            if (clicked >= 0) { SetFocus(clicked); submit = true; }
            if (submit) Close(_choices[_focus].action);
        }

        private static bool Hit(RectTransform rect, Vector2 screen, float pad)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local)) return false;
            Rect r = rect.rect;
            r.xMin -= pad; r.xMax += pad; r.yMin -= pad; r.yMax += pad;
            return r.Contains(local);
        }
    }
}
