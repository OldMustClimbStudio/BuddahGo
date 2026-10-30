using UnityEngine;

namespace BuddahGo.VFX
{
    // Colour and body controls for the palm's Volume layer (FX/InkVolume, Docs/vfx/palm-mesh-tripo.md section 20.6),
    // on the component instead of the material: values go through a MaterialPropertyBlock, so the material asset is
    // untouched, every instance can differ, and edits show at once in edit mode. PalmInkTimeline writes its own
    // properties (_FxAlpha, _FxDissolve, _Spill, _Warp) into the same block, read-modify-write, so both coexist.
    // The first time it is enabled the fields are read from the material so nothing changes until you edit them.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class InkVolumeColor : MonoBehaviour
    {
        [Tooltip("Thick ink. Palette: warm olive #1C1C18, cyan-black #111A19.")] public Color inkColor = new Color(0.01f, 0.01f, 0.015f, 1f);
        [Tooltip("Thin vapour (the fog's light side).")] public Color thinColor = new Color(0.5f, 0.5f, 0.53f, 1f);
        [Tooltip("Overall density: higher = blacker, thicker.")] public float extinction = 0.25f;
        [Tooltip("0 = shell around the surface, 1 = whole solid.")] [Range(0f, 1f)] public float fill = 0f;
        [Tooltip("Inside: 0 = contour layers only, 1 = solid fog.")] [Range(0f, 1f)] public float innerSolid = 0.2f;
        [Tooltip("Vapour threshold: higher = less of the thin fog is drawn.")] [Range(0f, 1f)] public float threshold = 0.35f;
        [Header("Outer vapour (the mist outside the body)")]
        [Tooltip("Density of the vapour just outside the surface: 0 = none, 1 = as dense as the body. The timeline swells it while gathering and dissipating; this is the resting value.")] [Range(0f, 1f)] public float outerDensity = 0.3f;
        [Tooltip("How far the vapour reaches out from the surface (object units).")] [Range(0.5f, 40f)] public float outerFalloff = 4f;
        [Header("Form")]
        [Tooltip("Shading by the surface normal: 0 = flat, 1 = full light / shadow side.")] [Range(0f, 1f)] public float shadeStrength = 0.6f;
        [Tooltip("Light direction in the hand's object space.")] public Vector3 shadeLightDir = new Vector3(0.4f, 0.8f, 0.5f);
        [Tooltip("Silhouette edges go dark so the form reads.")] [Range(0f, 1f)] public float rimDark = 0.5f;
        [Tooltip("Rim darkening width: higher = thinner edge.")] [Range(0.5f, 8f)] public float rimDarkPower = 2f;
        [Header("Emission (dark outside, bright inside)")]
        [Tooltip("HDR: values above 1 feed bloom. Palette: charge amber #B38256, main gold #B5945A, warm core #ECCCA6.")] [ColorUsage(false, true)] public Color emissionColor = new Color(1f, 0.74f, 0.42f, 1f);
        [Tooltip("0 = off.")] [Range(0f, 8f)] public float emissionStrength = 0f;
        [Tooltip("Emission depth (object units): how deep inside the hand the glow reaches full strength. Skin and thin fingers stay dark; the thick palm core lights up.")] [Range(1f, 40f)] public float emissionInner = 12f;
        [SerializeField, HideInInspector] bool initialised;

        static readonly int InkId = Shader.PropertyToID("_InkColor");
        static readonly int ThinId = Shader.PropertyToID("_ThinColor");
        static readonly int ExtinctionId = Shader.PropertyToID("_Extinction");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int InnerSolidId = Shader.PropertyToID("_InnerSolid");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        static readonly int EmissionInnerId = Shader.PropertyToID("_EmissionInner");
        static readonly int SpillId = Shader.PropertyToID("_Spill");
        static readonly int SpillFalloffId = Shader.PropertyToID("_SpillFalloff");
        static readonly int ShadeStrengthId = Shader.PropertyToID("_ShadeStrength");
        static readonly int ShadeLightDirId = Shader.PropertyToID("_ShadeLightDir");
        static readonly int RimDarkId = Shader.PropertyToID("_RimDark");
        static readonly int RimDarkPowerId = Shader.PropertyToID("_RimDarkPower");
        MaterialPropertyBlock block;

        void OnEnable()
        {
            if (!initialised) ReadFromMaterial();
            Apply();
        }

        void OnValidate() { Apply(); }

        // Start from what the material shows now.
        public void ReadFromMaterial()
        {
            var mr = GetComponent<MeshRenderer>();
            var m = mr != null ? mr.sharedMaterial : null;
            if (m == null) return;
            if (m.HasProperty(InkId)) inkColor = m.GetColor(InkId);
            if (m.HasProperty(ThinId)) thinColor = m.GetColor(ThinId);
            if (m.HasProperty(ExtinctionId)) extinction = m.GetFloat(ExtinctionId);
            if (m.HasProperty(FillId)) fill = m.GetFloat(FillId);
            if (m.HasProperty(InnerSolidId)) innerSolid = m.GetFloat(InnerSolidId);
            if (m.HasProperty(ThresholdId)) threshold = m.GetFloat(ThresholdId);
            if (m.HasProperty(SpillId)) outerDensity = m.GetFloat(SpillId);
            if (m.HasProperty(SpillFalloffId)) outerFalloff = m.GetFloat(SpillFalloffId);
            if (m.HasProperty(EmissionColorId)) emissionColor = m.GetColor(EmissionColorId);
            if (m.HasProperty(EmissionStrengthId)) emissionStrength = m.GetFloat(EmissionStrengthId);
            if (m.HasProperty(EmissionInnerId)) emissionInner = m.GetFloat(EmissionInnerId);
            initialised = true;
        }

        public void Apply()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetColor(InkId, inkColor);
            block.SetColor(ThinId, thinColor);
            block.SetFloat(ExtinctionId, extinction);
            block.SetFloat(FillId, fill);
            block.SetFloat(InnerSolidId, innerSolid);
            block.SetFloat(ThresholdId, threshold);
            block.SetColor(EmissionColorId, emissionColor);
            block.SetFloat(EmissionStrengthId, emissionStrength);
            block.SetFloat(EmissionInnerId, emissionInner);
            // the timeline swells / collapses these over the gather and the dissipate; when it is not playing (or holding),
            // the resting values are what shows
            var tl = GetComponentInParent<PalmInkTimeline>();
            if (tl == null || !tl.playing) { block.SetFloat(SpillId, outerDensity); block.SetFloat(SpillFalloffId, outerFalloff); }
            block.SetFloat(ShadeStrengthId, shadeStrength);
            block.SetVector(ShadeLightDirId, shadeLightDir);
            block.SetFloat(RimDarkId, rimDark);
            block.SetFloat(RimDarkPowerId, rimDarkPower);
            mr.SetPropertyBlock(block);
        }
    }
}
