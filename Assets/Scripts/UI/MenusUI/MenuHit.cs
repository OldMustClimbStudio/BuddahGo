using UnityEngine;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The menu pages' pointer test: is a screen point inside a RectTransform, grown by some padding so small labels are
    /// easy to point at. The menu canvases are Screen Space Overlay, so no camera is passed. A missing or inactive rect is
    /// never hit.
    /// </summary>
    internal static class MenuHit
    {
        public static bool Contains(RectTransform rect, Vector2 screen, float pad) => Contains(rect, screen, new Vector2(pad, pad));

        public static bool Contains(RectTransform rect, Vector2 screen, Vector2 pad)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local)) return false;
            Rect r = rect.rect;
            r.xMin -= pad.x; r.xMax += pad.x; r.yMin -= pad.y; r.yMax += pad.y;
            return r.Contains(local);
        }
    }
}
