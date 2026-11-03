using System;
using UnityEngine;

// The look of the 一念光明 curtain, H1 墨眼 (Docs/vfx/blackcurtain-opening-research.md section 7), shared by the normal
// and the anti skill: the screen shader, the world layer that follows the caster, and every opening value as a curve
// over the opening progress p (0–1 across the table's expand time). Timing stays in the skill tables.
[CreateAssetMenu(fileName = "CurtainEye_Profile", menuName = "Skills/VFX/Black Curtain Eye Profile")]
public class BlackCurtainEyeProfile : ScriptableObject
{
    [Tooltip("FX/CurtainEye.")]
    public Shader shader;
    [Tooltip("World layer (taiji accretion disc, ink drawn in, collapse flash), tuned for a 1 s opening; its particle speed is scaled by 1 / expand time.")]
    public GameObject worldLayer;
    [Tooltip("Red world layer for the anti skill (prefab variant of the world layer, colours only).")]
    public GameObject worldLayerAnti;

    [Header("Look")]
    [Tooltip("Collapse flash and photon ring never exceed this gold value (HDR). Raise it to let the peak go toward white.")]
    public float flashCap = 1.6f;
    [Tooltip("Eye centre above the caster's pivot, scaled by the caster's size (the eye reads best on the body centre).")]
    public Vector3 centerWorldOffset = new Vector3(0f, 3.5f, 0f);

    [Header("Camera (off by default)")]
    public bool cameraKick;
    public float pushDegrees = 4f;
    public float shakeAmplitude = 0.08f;

    [Header("Curves (x = opening progress p, 0–1)")]
    public EyeCurves curves = new EyeCurves();

    // The four beats (research doc 4.2): 0–0.15 anticipation (bulge), 0.15–0.55 pull, 0.55–0.62 collapse (horizon snaps
    // open, ring peaks), 0.62–1 the horizon swallows the screen.
    public struct EyeParams
    {
        public float holeRadius, lens, twirl, radialBlur, bulge, ring, ringPhase, spiral, dispersion, edge, coreFlash;
    }

    // Keys default to flat tangents, so each value eases in from rest and eases out; only the swallow keeps a steep
    // in-tangent at p = 1 (it accelerates until the screen is covered).
    [Serializable]
    public class EyeCurves
    {
        [Tooltip("Anticipation: the frame bulges out before the pull.")]
        public AnimationCurve bulge = Curve(0f, 0f, 0.1f, 0.09f, 0.22f, 0f);
        [Tooltip("Event horizon radius in screen heights: grows from nothing, snaps open with an overshoot at the collapse, then swallows the screen.")]
        public AnimationCurve holeRadius = Swallow(Curve(0f, 0f, 0.08f, 0f, 0.55f, 0.07f, 0.585f, 0.18f, 0.63f, 0.16f, 1f, 2f), 9f);
        [Tooltip("Lensing strength (0–1); scaled down automatically as the horizon grows.")]
        public AnimationCurve lens = Curve(0.08f, 0f, 0.4f, 1f, 1f, 1f);
        [Tooltip("Twirl at the hole, radians.")]
        public AnimationCurve twirl = Curve(0.1f, 0f, 0.6f, 2.2f, 1f, 0.9f);
        [Tooltip("Radial smear toward the hole.")]
        public AnimationCurve radialBlur = Curve(0.18f, 0f, 0.55f, 3f, 0.7f, 3f, 1f, 0f);
        [Tooltip("Photon ring brightness (gold, capped by Flash Cap).")]
        public AnimationCurve ring = Curve(0.12f, 0f, 0.5f, 0.8f, 0.545f, 0.9f, 0.58f, 3.2f, 0.68f, 0.85f, 0.85f, 0.8f, 0.99f, 0f);
        [Tooltip("Ink shell spiral arms outside the ring.")]
        public AnimationCurve spiral = Curve(0.18f, 0f, 0.5f, 1f, 0.8f, 1f, 1f, 0f);
        [Tooltip("Two-tone ghost (gold / ink).")]
        public AnimationCurve dispersion = Curve(0.22f, 0f, 0.55f, 1f, 0.62f, 1f, 0.8f, 0f);
        [Tooltip("How much the horizon rim wobbles (1 = calm).")]
        public AnimationCurve edge = Curve(0.6f, 1f, 0.8f, 2f, 1f, 2f);
        [Tooltip("Collapse flash on the caster.")]
        public AnimationCurve coreFlash = Curve(0.53f, 0f, 0.58f, 1f, 0.7f, 0f);
        [Tooltip("Photon ring spin, radians per second.")]
        public float ringSpin = 5f;

        private static AnimationCurve Curve(params float[] timeValue)
        {
            var keys = new Keyframe[timeValue.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(timeValue[i * 2], timeValue[i * 2 + 1], 0f, 0f);
            return new AnimationCurve(keys);
        }

        private static AnimationCurve Swallow(AnimationCurve curve, float inTangent)
        {
            int last = curve.length - 1;
            Keyframe k = curve[last];
            k.inTangent = inTangent;
            curve.MoveKey(last, k);
            return curve;
        }
    }

    public EyeParams Evaluate(float p, float time)
    {
        EyeCurves c = curves;
        var e = new EyeParams
        {
            bulge = c.bulge.Evaluate(p),
            holeRadius = Mathf.Max(0f, c.holeRadius.Evaluate(p)),
            twirl = c.twirl.Evaluate(p),
            radialBlur = Mathf.Max(0f, c.radialBlur.Evaluate(p)),
            ring = Mathf.Max(0f, c.ring.Evaluate(p)),
            ringPhase = time * c.ringSpin,
            spiral = Mathf.Clamp01(c.spiral.Evaluate(p)),
            dispersion = Mathf.Clamp01(c.dispersion.Evaluate(p)),
            edge = c.edge.Evaluate(p),
            coreFlash = Mathf.Max(0f, c.coreFlash.Evaluate(p)),
        };
        e.lens = Mathf.Max(0f, c.lens.Evaluate(p)) * Mathf.Min(1f, 0.16f / Mathf.Max(e.holeRadius, 0.001f));
        return e;
    }
}
