using UnityEngine;

namespace BuddahGo.VFX
{
    // Layer switch for the ink-volume palm prefab (Docs/vfx/palm-mesh-tripo.md section 13): tick the layers to show,
    // or pick one in Solo to see it alone. Works in edit mode (OnValidate) and at runtime (Apply). Mesh = the hand's
    // own MeshRenderer on this object, Volume = the ray-marched fog box (child named Volume). The 3D fluid draws on its
    // own hidden world box and is not switched here.
    [ExecuteAlways]
    public sealed class PalmInkLayerSwitch : MonoBehaviour
    {
        public enum Layer { None, Mesh, Volume }

        [Tooltip("Show only this layer; None = use the toggles below.")]
        public Layer solo = Layer.None;
        public bool showMesh = false;
        public bool showVolume = true;

        void OnValidate() { Apply(); }
        void OnEnable() { Apply(); }

        public void Apply()
        {
            Set(GetComponent<MeshRenderer>(), Layer.Mesh, showMesh);
            Transform volume = transform.Find("Volume");
            if (volume != null)
                Set(volume.GetComponent<Renderer>(), Layer.Volume, showVolume);
        }

        void Set(Renderer renderer, Layer layer, bool toggled)
        {
            if (renderer != null)
                renderer.enabled = solo == Layer.None ? toggled : solo == layer;
        }
    }
}
