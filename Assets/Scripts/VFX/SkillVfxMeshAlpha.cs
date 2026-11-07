using UnityEngine;

namespace BuddahGo.VFX
{
    // A mesh (not a particle) inside a SkillVfxLayer, drawn with UberFXSG. The layer writes its window alpha into the
    // renderer's property block as _FxAlpha (SkillVfxLayer.Apply: alpha curve x window fades), which UberFXSG does not
    // read; this turns it into the shader's own terms right before the mesh renders: the colour-ramp alphas and the
    // Fresnel colour are multiplied by it, and the mesh grows from popFrom to full size about pivot as it fades in
    // (and shrinks back as it fades out). Used by 禅足冰封's ice block (FrostLockProto): the same v1.5 materials on
    // one-particle mesh systems rendered far fainter than on a MeshRenderer (Docs/vfx/yuanqi-frost-lock.md section 8).
    // Without a layer alpha (edit mode, prefab view) the mesh draws at full alpha and size.
    [RequireComponent(typeof(Renderer))]
    public sealed class SkillVfxMeshAlpha : MonoBehaviour
    {
        static readonly int FxAlphaId = Shader.PropertyToID("_FxAlpha");
        static readonly int LowsId = Shader.PropertyToID("_LastColor");
        static readonly int MidsId = Shader.PropertyToID("_MidColor");
        static readonly int HighsId = Shader.PropertyToID("_WhiteColor");
        static readonly int FresnelId = Shader.PropertyToID("_FresnelColor");

        [Tooltip("Size at alpha 0 (1 = no growth).")]
        [Range(0f, 1f)] public float popFrom = 0.88f;
        [Tooltip("Point the mesh grows about, in the parent's space (the body centre for the ice block).")]
        public Vector3 pivot;

        Renderer _renderer;
        MaterialPropertyBlock _block;
        Color _lows, _mids, _highs, _fresnel;
        Vector3 _baseScale, _basePosition;
        bool _ready;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
            Material material = _renderer.sharedMaterial;
            if (material == null)
                return;
            _lows = material.GetColor(LowsId);
            _mids = material.GetColor(MidsId);
            _highs = material.GetColor(HighsId);
            _fresnel = material.GetColor(FresnelId);
            _ready = true;
        }

        void OnWillRenderObject()
        {
            if (!_ready || !Application.isPlaying)
                return;
            _renderer.GetPropertyBlock(_block);
            float a = _block.isEmpty || !_block.HasFloat(FxAlphaId) ? 1f : Mathf.Clamp01(_block.GetFloat(FxAlphaId));
            _block.SetColor(LowsId, WithAlpha(_lows, a));
            _block.SetColor(MidsId, WithAlpha(_mids, a));
            _block.SetColor(HighsId, WithAlpha(_highs, a));
            _block.SetColor(FresnelId, _fresnel * a);
            _renderer.SetPropertyBlock(_block);

            float k = Mathf.Lerp(popFrom, 1f, a);
            transform.localScale = _baseScale * k;
            transform.localPosition = pivot + (_basePosition - pivot) * k;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a *= a;
            return c;
        }
    }
}
