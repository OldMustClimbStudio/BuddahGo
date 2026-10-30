using UnityEngine;

namespace BuddahGo.VFX
{
    // Layer switch for the ink-volume palm prefab (Docs/vfx/palm-mesh-tripo.md section 13): tick the layers to show,
    // or pick one in Solo to see it alone. Works in edit mode (OnValidate) and at runtime (Apply). Layers are found
    // by child name so the prefab can be rearranged freely: Mesh = the hand's own MeshRenderer on this object,
    // Volume = the ray-marched fog box, Smoke = eroded smoke puffs (SurfaceSmoke, optional), Wisps = the thin trails
    // (SurfaceWisps), Fluid = the ink-in-water sheets (children named InkFluid*; their hidden world quads follow the
    // child's renderer flag). Smoke keeps the old Plume enum slot.
    [ExecuteAlways]
    public sealed class PalmInkLayerSwitch : MonoBehaviour
    {
        public enum Layer { None, Mesh, Volume, Smoke, Wisps, Fluid }

        [Tooltip("Show only this layer; None = use the toggles below.")]
        public Layer solo = Layer.None;
        public bool showMesh = false;
        public bool showVolume = true;
        public bool showSmoke = true;
        public bool showWisps = true;
        public bool showFluid = true;

        void OnValidate() { Apply(); }
        void OnEnable() { Apply(); }

        public void Apply()
        {
            Set(Layer.Mesh, showMesh);
            Set(Layer.Volume, showVolume);
            Set(Layer.Smoke, showSmoke);
            Set(Layer.Wisps, showWisps);
            Set(Layer.Fluid, showFluid);
        }

        void Set(Layer layer, bool toggled)
        {
            bool on = solo == Layer.None ? toggled : solo == layer;
            if (layer == Layer.Mesh)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = on;
                return;
            }
            if (layer == Layer.Fluid)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform c = transform.GetChild(i);
                    if (!c.name.StartsWith("InkFluid")) continue;
                    var r = c.GetComponent<Renderer>();
                    if (r != null) r.enabled = on;
                }
                return;
            }
            Transform child = transform.Find(layer == Layer.Wisps ? "SurfaceWisps" : layer == Layer.Smoke ? "SurfaceSmoke" : layer.ToString());
            if (child == null) return;
            var renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = on;
            var ps = child.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = on;
                if (!on) ps.Clear(true);
            }
        }
    }
}
