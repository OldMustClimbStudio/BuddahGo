using UnityEngine;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The menu's mouse cursor: a hollow white circle (drawn here, with a faint dark rim so it reads on the bright sky),
    /// its centre on the pointer. Set while this component is enabled; the system arrow comes back when it is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCursor : MonoBehaviour
    {
        [SerializeField, Min(8)] private int size = 32;
        [Tooltip("Ring radius in texture pixels (to the middle of the line).")]
        [SerializeField, Min(2f)] private float radius = 10f;
        [SerializeField, Min(0.5f)] private float thickness = 1.8f;
        [SerializeField, Range(0f, 1f)] private float rimAlpha = 0.3f;
        [Tooltip("Hand-drawn cursor (imported as a Cursor texture). When set it is used instead of the drawn ring.")]
        [SerializeField] private Texture2D art;

        private Texture2D _texture;

        private void OnEnable()
        {
            if (art != null)
            {
                Cursor.SetCursor(art, new Vector2(art.width * 0.5f, art.height * 0.5f), CursorMode.Auto);
                return;
            }
            if (_texture == null) _texture = Draw();
            Cursor.SetCursor(_texture, new Vector2(size * 0.5f, size * 0.5f), CursorMode.Auto);
        }

        private void OnDisable() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private Texture2D Draw()
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MenuCursor", filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = size * 0.5f;
            float half = thickness * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - radius);
                // White line with a soft one-pixel edge, then a faint dark rim around it.
                float white = Mathf.Clamp01(half + 0.5f - d);
                float rim = Mathf.Clamp01(half + 1.6f - d) * rimAlpha;
                float a = white + rim * (1f - white);
                float v = a > 0f ? white / a : 0f;
                px[y * size + x] = new Color(v, v, v, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
