using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    internal static class SoloUIFactory
    {
        internal static Canvas Canvas(Transform parent, string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("SoloUIEventSystem", typeof(EventSystem));
                events.transform.SetParent(parent, false);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            return canvas;
        }

        internal static RectTransform Card(Transform parent, string name, float height)
        {
            var dim = new GameObject(name + "Backdrop", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(parent, false);
            dim.layer = parent.gameObject.layer;
            var rect = (RectTransform)dim.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.06f, 0.72f);
            var card = Rect(dim.transform, name, Vector2.zero, new Vector2(680f, height));
            card.gameObject.AddComponent<Image>().color = new Color(0.94f, 0.93f, 0.88f, 0.98f);
            return card;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, string value,
            Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize = 24f)
        {
            var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.1f, 0.14f, 0.17f);
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        internal static Button Button(Transform parent, string name, string value,
            Vector2 position, Vector2 size, TMP_FontAsset font)
        {
            var rect = Rect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.84f, 0.85f, 0.81f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.84f, 0.94f, 0.95f);
            colors.selectedColor = new Color(0.75f, 0.9f, 0.92f);
            colors.pressedColor = new Color(0.62f, 0.82f, 0.84f);
            button.colors = colors;
            Label(rect, "Label", value, Vector2.zero, size - new Vector2(12f, 6f), font);
            return button;
        }

        internal static void Navigate(params Selectable[] controls)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                var previous = controls[(i + controls.Length - 1) % controls.Length];
                var next = controls[(i + 1) % controls.Length];
                controls[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = previous, selectOnLeft = previous, selectOnDown = next, selectOnRight = next };
            }
        }

        internal static void Focus(Selectable control)
        {
            if (control != null && control.IsActive() && control.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
    }
}
