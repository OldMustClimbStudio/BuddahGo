using UnityEngine;

/// <summary>
/// How the body scale reaches the 如来神掌 palm (user 2026-11-06): a scale skill row names the palm multiplier at its
/// full body scale (SkillEffectGiant.palmSizeMultiplier: giant ×2 at body ×4, giant_anti ×0.5 at body ×0.3), and the
/// palm follows the body linearly between 1 and that point, so it grows and shrinks with the body and is 1 again when
/// the body is back to 1. The charged palm's hitbox, visual and (#1 金身巨掌) impulse use this; the spawn offsets keep
/// following the body itself.
/// </summary>
internal static class PalmSizeRule
{
    internal const float MinPalmSizeMultiplier = 0.05f;
    private const float FlatScaleEpsilon = 0.0001f;

    internal static float Resolve(float bodyScale, float bodyScaleAtFull, float palmSizeAtFull)
    {
        float span = bodyScaleAtFull - 1f;
        if (Mathf.Abs(span) < FlatScaleEpsilon)
            return 1f;
        // Clamped: while a retarget (giant → giant_anti refresh) still has the body on the other side of 1 or beyond the
        // row's full scale, the palm stays between 1 and the row's value instead of extrapolating.
        float t = Mathf.Clamp01((bodyScale - 1f) / span);
        return Mathf.Max(MinPalmSizeMultiplier, 1f + t * (palmSizeAtFull - 1f));
    }
}
