using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Skin page of the menu environment ("一笔" design, v0.2 mockup 04), shown in the skin close-up of the garden Buddha.
    /// The skins' names sit along a thin arc to the left of the model, the chosen one in the middle of the arc with a
    /// vermilion dot; the arc turns to bring another one to the middle. On the left: the chosen skin's name in large
    /// type, its line and "按住拖动可旋转". Choosing a skin puts its material on the model (a menu preview: there is
    /// no skin system behind it yet). Mouse wheel, ↑ / ↓ or W / S, or a click on a name chooses; dragging the model turns
    /// it (MenuBuddhaTurntable); Esc / 返回 leaves the close-up. Hides the TEMP 返回 button.
    /// Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSkinPage : MonoBehaviour
    {
        [System.Serializable]
        public class Skin
        {
            public string name;
            [TextArea] public string line;
            [Tooltip("Put on every renderer of the model while chosen.")]
            public Material material;
            [Tooltip("Calligraphy of the name for the big name (white on transparent). Empty: the name is set in type.")]
            public Sprite nameArt;
            [System.NonSerialized] public TextMeshProUGUI label;
            [System.NonSerialized] public Image dot;
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private MenuSkinView skinView;
        [Tooltip("The skin model's root (its renderers take the chosen material).")]
        [SerializeField] private Transform model;
        [SerializeField] private Skin[] skins = new Skin[0];
        [SerializeField] private int selected;

        [Header("Arc (canvas pixels, top-left origin, y down)")]
        [SerializeField] private RectTransform arcItems;
        [SerializeField] private Vector2 arcCentre = new Vector2(1180f, 540f);
        [SerializeField] private float arcRadius = 600f;
        [Tooltip("Degrees between two names on the arc.")]
        [SerializeField] private float arcStep = 12f;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite dotSprite;

        [Header("Chosen skin")]
        [SerializeField] private CanvasGroup chosen;
        [SerializeField] private TextMeshProUGUI bigName;
        [Tooltip("Shows the chosen skin's name calligraphy in place of bigName when the skin has one.")]
        [SerializeField] private Image bigNameArt;
        [SerializeField] private TextMeshProUGUI line;
        [SerializeField] private TextMeshProUGUI backHint;
        [SerializeField] private GameObject tempBack;

        private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
        private static readonly Color Vermilion = new Color32(0xB8, 0x35, 0x2A, 0xFF);

        private float _offset;      // arc position (in steps) of the chosen skin, eased
        private int _shown = -1;
        private float _nameT;
        private readonly List<Renderer> _renderers = new List<Renderer>();

        public bool IsShown => skinView != null && skinView.IsInteractive;

        public void Configure(CanvasGroup g, MenuSkinView view, Transform skinModel, Skin[] list, RectTransform items, TMP_FontAsset f, Sprite dot,
                              CanvasGroup chosenGroup, TextMeshProUGUI big, TextMeshProUGUI lineText, TextMeshProUGUI back, GameObject temp)
        {
            group = g; skinView = view; model = skinModel; skins = list; arcItems = items; font = f; dotSprite = dot;
            chosen = chosenGroup; bigName = big; line = lineText; backHint = back; tempBack = temp;
        }

        public void SetNameArt(Image art) => bigNameArt = art;

        [Tooltip("The pointer's washi tag behind the names on the arc (MenuHoverTag).")]
        [SerializeField] private Sprite hoverPaper;

        public void SetHoverPaper(Sprite paper) => hoverPaper = paper;

        private void Awake()
        {
            group.alpha = 0f;
            for (int i = 0; i < skins.Length; i++) MakeItem(skins[i]);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, skins.Length - 1));
            _offset = selected;
        }

        private void Start()
        {
            if (tempBack != null)
            {
                var g = tempBack.GetComponent<CanvasGroup>();
                if (g == null) g = tempBack.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                g.blocksRaycasts = false;
            }
            Apply();
        }

        private void MakeItem(Skin skin)
        {
            var go = new GameObject("Skin_" + skin.name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(arcItems, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 40f);
            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dotGo.transform;
            drt.SetParent(rt, false);
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.sizeDelta = new Vector2(8f, 8f);
            skin.dot = dotGo.GetComponent<Image>();
            skin.dot.sprite = dotSprite;
            skin.dot.raycastTarget = false;
            var textGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(18f, 0f);
            trt.sizeDelta = new Vector2(150f, 40f);
            skin.label = textGo.GetComponent<TextMeshProUGUI>();
            skin.label.font = font;
            skin.label.text = skin.name;
            skin.label.characterSpacing = 8f;
            skin.label.color = Paper;
            skin.label.alignment = TextAlignmentOptions.MidlineLeft;
            skin.label.enableWordWrapping = false;
            skin.label.raycastTarget = false;
            // Behind the dot too, so the vermilion dot stays on top of the paper.
            if (hoverPaper != null) MenuHoverTag.Add(skin.label, hoverPaper, true);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool shown = IsShown;
            group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / 0.3f);
            if (group.alpha <= 0f) return;
            if (shown) HandleInput();
            _offset = Mathf.Lerp(_offset, selected, 1f - Mathf.Exp(-10f * dt));
            LayoutArc();
            UpdateChosen(dt);
        }

        /// <summary>Names along the arc: the chosen one at its leftmost point, the others above and below, fading out.</summary>
        private void LayoutArc()
        {
            for (int i = 0; i < skins.Length; i++)
            {
                Skin skin = skins[i];
                if (skin.label == null) continue;
                float steps = i - _offset;
                float angle = steps * arcStep * Mathf.Deg2Rad;    // + = lower on screen
                float x = arcCentre.x - arcRadius * Mathf.Cos(angle);
                float y = arcCentre.y + arcRadius * Mathf.Sin(angle);
                var rt = (RectTransform)skin.label.transform.parent;
                rt.anchoredPosition = new Vector2(x - 4f, -y);
                float near = Mathf.Clamp01(1f - Mathf.Abs(steps));          // 1 at the middle
                float far = Mathf.Clamp01(1f - (Mathf.Abs(steps) - 1f) / 2.5f);
                skin.label.fontSize = Mathf.Lerp(18f, 26f, near);
                skin.label.alpha = Mathf.Lerp(0.65f * far, 1f, near);
                skin.dot.color = Color.Lerp(new Color(Paper.r, Paper.g, Paper.b, 0.7f * far), Vermilion, near);
                ((RectTransform)skin.dot.transform).sizeDelta = Vector2.one * Mathf.Lerp(6f, 11f, near);
                ((RectTransform)skin.dot.transform).anchoredPosition = new Vector2(0f, 0f);
                rt.gameObject.SetActive(Mathf.Abs(steps) < 3.6f);
            }
        }

        private void UpdateChosen(float dt)
        {
            if (skins.Length == 0) return;
            // The big name fades out and back in on a change.
            if (_shown != selected)
            {
                _nameT = Mathf.MoveTowards(_nameT, 0f, dt / 0.12f);
                if (_nameT <= 0f)
                {
                    _shown = selected;
                    Sprite art = skins[selected].nameArt;
                    bool useArt = art != null && bigNameArt != null;
                    if (bigNameArt != null) { bigNameArt.sprite = art; bigNameArt.enabled = useArt; }
                    if (bigName != null) bigName.text = useArt ? "" : skins[selected].name;
                    if (line != null) line.text = skins[selected].line + "\n<size=85%><color=#F4EFE6AA>按住拖动可旋转</color></size>";
                }
            }
            else _nameT = Mathf.MoveTowards(_nameT, 1f, dt / 0.3f);
            if (chosen != null)
            {
                float e = 1f - (1f - _nameT) * (1f - _nameT);
                chosen.alpha = e;
                ((RectTransform)chosen.transform).anchoredPosition = new Vector2(-12f * (1f - e), 0f);
            }
        }

        private void HandleInput()
        {
            int move = 0;
            bool back = false;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move--;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move++;
                if (kb.escapeKey.wasPressedThisFrame) back = true;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) move--;
                if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) move++;
                if (pad.buttonEast.wasPressedThisFrame) back = true;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0f) move--;
                if (wheel < 0f) move++;
                Vector2 at = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    for (int i = 0; i < skins.Length; i++)
                        if (skins[i].label != null && skins[i].label.transform.parent.gameObject.activeSelf && MenuHit.Contains(skins[i].label.rectTransform, at, 10f))
                        { Choose(i); break; }
                    if (MenuHit.Contains(backHint != null ? backHint.rectTransform : null, at, 10f)) back = true;
                }
            }
            if (move != 0) Choose(Mathf.Clamp(selected + move, 0, skins.Length - 1));
            if (back && skinView != null) skinView.Leave();
        }

        private void Choose(int i)
        {
            if (i == selected) return;
            selected = i;
            Apply();
        }

        /// <summary>Puts the chosen skin's material on the model.</summary>
        private void Apply()
        {
            if (model == null || skins.Length == 0 || skins[selected].material == null) return;
            model.GetComponentsInChildren(true, _renderers);
            foreach (Renderer r in _renderers)
            {
                if (r.name == "HoverOutline") continue;
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = skins[selected].material;
                r.sharedMaterials = mats;
            }
        }
    }
}
