using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Fades a plain UI Image from its colour at one edge to transparent at the opposite edge (vertex alpha),
    /// used for the menu's edge darkening without a gradient texture.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIEdgeShade : BaseMeshEffect
    {
        public enum Edge { Left, Bottom, Top, Right }

        [SerializeField] private Edge edge = Edge.Left;

        public Edge From { get => edge; set { edge = value; graphic.SetVerticesDirty(); } }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Rect r = graphic.rectTransform.rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = edge == Edge.Left ? Mathf.InverseLerp(r.xMax, r.xMin, v.position.x)
                        : edge == Edge.Right ? Mathf.InverseLerp(r.xMin, r.xMax, v.position.x)
                        : edge == Edge.Bottom ? Mathf.InverseLerp(r.yMax, r.yMin, v.position.y)
                        : Mathf.InverseLerp(r.yMin, r.yMax, v.position.y);
                Color32 c = v.color;
                c.a = (byte)(c.a * t);
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
