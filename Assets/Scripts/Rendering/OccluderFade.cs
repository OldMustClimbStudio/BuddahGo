using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.Rendering
{
    // Fades tall scenery that stands between the gameplay camera and the player it follows (RaceMap: the torii, the
    // gate and the watari span over the track, the two 塔庭 torii).
    // Client-side visuals only: each frame the segment from the main camera to the active virtual camera's Follow target
    // is tested against every occluder's renderer bounds (a little inflated). A blocking occluder swaps its renderers to
    // transparent copies of their materials and fades to `fadedAlpha`; once clear it fades back and gets its original
    // opaque materials again, so shadows and batching are unchanged while nothing is in the way.
    [DisallowMultipleComponent]
    public sealed class OccluderFade : MonoBehaviour
    {
        [Tooltip("Objects that may hide the player; all their child renderers fade together.")]
        public List<GameObject> occluders = new List<GameObject>();
        [Range(0f, 1f)] public float fadedAlpha = 0.25f;
        [Tooltip("Seconds for a full fade in either direction.")] public float fadeTime = 0.25f;
        [Tooltip("Bounds are grown by this many metres before the sight-line test.")] public float padding = 1.5f;
        [Tooltip("The sight line ends this far above the follow target's pivot.")] public float targetHeight = 4f;

        sealed class Entry
        {
            public Renderer[] renderers;
            public Material[][] opaque;
            public Material[][] faded;
            public float alpha = 1f;
        }

        readonly List<Entry> _entries = new List<Entry>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        CinemachineBrain _brain;

        void OnEnable()
        {
            _entries.Clear();
            foreach (var go in occluders)
            {
                if (go == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                var e = new Entry { renderers = rs, opaque = new Material[rs.Length][], faded = new Material[rs.Length][] };
                for (int i = 0; i < rs.Length; i++) e.opaque[i] = rs[i].sharedMaterials;
                _entries.Add(e);
            }
        }

        void OnDisable()
        {
            foreach (var e in _entries) SetOpaque(e);
            foreach (var e in _entries)
                foreach (var mats in e.faded)
                    if (mats != null) foreach (var m in mats) if (m != null) Destroy(m);
            _entries.Clear();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (_brain == null || _brain.gameObject != cam.gameObject) _brain = cam.GetComponent<CinemachineBrain>();
            Transform target = _brain != null && _brain.ActiveVirtualCamera != null ? _brain.ActiveVirtualCamera.Follow : null;
            bool hasTarget = target != null;
            Vector3 a = cam.transform.position, b = hasTarget ? target.position + Vector3.up * targetHeight : a;
            float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            foreach (var e in _entries)
            {
                bool blocks = false;
                if (hasTarget)
                    foreach (var r in e.renderers)
                    {
                        if (r == null || !r.enabled) continue;
                        // oriented test: the segment in the renderer's own space against its local bounds
                        var t = r.transform; var s = t.lossyScale;
                        Vector3 la = t.InverseTransformPoint(a), lb = t.InverseTransformPoint(b);
                        var bb = r.localBounds;
                        bb.Expand(new Vector3(2f * padding / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 2f * padding / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 2f * padding / Mathf.Max(1e-4f, Mathf.Abs(s.z))));
                        Vector3 ld = lb - la; float llen = ld.magnitude; if (llen < 1e-5f) continue;
                        if (bb.IntersectRay(new Ray(la, ld / llen), out float hit) && hit < llen) { blocks = true; break; }
                    }
                float goal = blocks ? fadedAlpha : 1f;
                float next = Mathf.MoveTowards(e.alpha, goal, step * (1f - fadedAlpha));
                if (Mathf.Approximately(next, e.alpha)) continue;
                e.alpha = next;
                if (e.alpha >= 0.999f) SetOpaque(e); else SetFaded(e);
            }
        }

        static void SetOpaque(Entry e)
        {
            for (int i = 0; i < e.renderers.Length; i++)
                if (e.renderers[i] != null) e.renderers[i].sharedMaterials = e.opaque[i];
            e.alpha = 1f;
        }

        void SetFaded(Entry e)
        {
            for (int i = 0; i < e.renderers.Length; i++)
            {
                var r = e.renderers[i]; if (r == null) continue;
                if (e.faded[i] == null)
                {
                    var src = e.opaque[i]; var dst = new Material[src.Length];
                    for (int k = 0; k < src.Length; k++) dst[k] = src[k] != null ? MakeTransparent(src[k]) : null;
                    e.faded[i] = dst;
                }
                foreach (var m in e.faded[i])
                {
                    if (m == null || !m.HasProperty(BaseColor)) continue;
                    var c = m.GetColor(BaseColor); c.a = e.alpha; m.SetColor(BaseColor, c);
                }
                r.sharedMaterials = e.faded[i];
            }
        }

        static Material MakeTransparent(Material src)
        {
            var m = new Material(src) { name = src.name + " (fade)" };
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", true);
            return m;
        }
    }
}
