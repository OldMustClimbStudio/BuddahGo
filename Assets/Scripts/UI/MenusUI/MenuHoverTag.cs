using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The pointer's washi tag on a clickable text (the skin names on the arc; not the small key hints, where it reads badly): while the pointer is over
    /// the text a torn paper tag (asset A12) fades in behind it, just wrapping it, and the text turns to ink; when the
    /// pointer leaves, the tag fades away and the text is paper-white again. Same look as the confirm dialog's buttons.
    /// The tag is made on Awake as the text's sibling, right behind it (or behind everything in the text's parent), and
    /// follows the text's drawn bounds every frame, so it fits texts whose size or place animates. Shows only while the
    /// text itself is visible, and not while the confirm dialog is up. Added by the page that makes the text (MenuHoverTag.Add).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuHoverTag : MonoBehaviour
    {
        [SerializeField] private Sprite paper;
        [Tooltip("Paper around the text, both sides together, for a 22 px text (scaled with the font size).")]
        [SerializeField] private Vector2 padding = new Vector2(72f, 21f);
        [Tooltip("Extra pointer area around the text (canvas pixels).")]
        [SerializeField] private float hitPad = 10f;
        [Tooltip("Put the tag behind everything in the text's parent (e.g. behind a marker dot) instead of just behind the text.")]
        [SerializeField] private bool behindParent;

        private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
        private static readonly Color Ink = new Color32(0x1E, 0x1A, 0x18, 0xFF);

        private TextMeshProUGUI _text;
        private RectTransform _tag;
        private Image _image;
        private float _h;

        /// <summary>Gives `text` a hover tag (once).</summary>
        public static MenuHoverTag Add(TextMeshProUGUI text, Sprite paperSprite, bool behindParentContents = false)
        {
            if (text == null) return null;
            var tag = text.GetComponent<MenuHoverTag>();
            if (tag == null)
            {
                bool wasActive = text.gameObject.activeSelf;
                text.gameObject.SetActive(false); // set the fields before Awake runs
                tag = text.gameObject.AddComponent<MenuHoverTag>();
                tag.paper = paperSprite;
                tag.behindParent = behindParentContents;
                text.gameObject.SetActive(wasActive);
            }
            return tag;
        }

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            var go = new GameObject(name + "_HoverTag", typeof(RectTransform), typeof(Image));
            _tag = (RectTransform)go.transform;
            _tag.SetParent(transform.parent, false);
            _tag.SetSiblingIndex(behindParent ? 0 : transform.GetSiblingIndex()); // drawn before (under) the text
            _tag.anchorMin = _tag.anchorMax = _tag.pivot = new Vector2(0.5f, 0.5f);
            _image = go.GetComponent<Image>();
            _image.sprite = paper;
            _image.type = Image.Type.Sliced;
            _image.raycastTarget = false;
            _image.color = new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
        }

        private void OnDisable()
        {
            _h = 0f;
            if (_tag != null) _tag.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_tag != null) Destroy(_tag.gameObject);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool visible = _text.alpha * _text.canvasRenderer.GetInheritedAlpha() > 0.3f;
            bool hover = visible && !MenuConfirmDialog.Blocking && Mouse.current != null && Hit(Mouse.current.position.ReadValue());
            _h = Mathf.MoveTowards(_h, hover ? 1f : 0f, dt / 0.15f);
            float e = _h * _h * (3f - 2f * _h);
            Color c = Color.Lerp(Paper, Ink, e);
            c.a = _text.color.a;
            _text.color = c;
            bool show = _h > 0f;
            if (_tag.gameObject.activeSelf != show) _tag.gameObject.SetActive(show);
            if (!show) return;

            // Fit the drawn text, centred on it; unfold a little as it comes in.
            Bounds b = _text.textBounds;
            float s = _text.fontSize / 22f;
            Vector3 centre = _text.rectTransform.TransformPoint(b.center);
            _tag.position = centre;
            _tag.sizeDelta = new Vector2(b.size.x + padding.x * s, b.size.y + padding.y * s);
            _tag.localScale = new Vector3(Mathf.Lerp(0.92f, 1f, e), 1f, 1f);
            _image.color = new Color(1f, 1f, 1f, e);
        }

        private bool Hit(Vector2 screen)
        {
            RectTransform rect = _text.rectTransform;
            if (!rect.gameObject.activeInHierarchy) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local)) return false;
            Rect r = rect.rect;
            r.xMin -= hitPad; r.xMax += hitPad; r.yMin -= hitPad; r.yMax += hitPad;
            return r.Contains(local);
        }
    }
}
