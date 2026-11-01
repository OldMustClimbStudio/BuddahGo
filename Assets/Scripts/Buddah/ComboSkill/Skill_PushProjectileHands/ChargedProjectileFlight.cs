using UnityEngine;

// Flight of the charged palm (如来神掌) after it launches (Docs/vfx/push-projectile-hands.md, "飞行速度"): speed =
// peak speed (Skill/Push charged `speed`) x a shape over normalised flight time (Skill_PushProjectileHands
// .projectileSpeedCurve). Position is the integral of that, so the server hitbox and every client's visual are at the
// same place at the same flight time whatever their frame rates.
public static class ChargedProjectileFlight
{
    private const int Samples = 128;

    // The shape the user tuned on the ink palm (PalmInkTimeline in VFX_Palm_Bullet, Docs/vfx/palm-mesh-tripo.md
    // 20.5) with its 1 s gather cut off (gameplay holds the palm for buildUpSeconds instead): the 4 s timeline keys
    // (0.25, 0) (0.32, 1) (0.65, 0.3, slope -1.2) (1, 0) mapped onto the 3 s flight.
    public static AnimationCurve DefaultSpeedCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.07f / 0.75f, 1f, 0f, 0f),
            new Keyframe(0.4f / 0.75f, 0.3f, -1.2f * 0.75f, -1.2f * 0.75f),
            new Keyframe(1f, 0f, 0f, 0f));
    }

    // cumulative[i] = area under max(0, curve) from 0 to i / Samples.
    public static float[] Cumulative(AnimationCurve speedCurve)
    {
        var cumulative = new float[Samples + 1];
        float previous = Mathf.Max(0f, speedCurve.Evaluate(0f));
        for (int i = 1; i <= Samples; i++)
        {
            float value = Mathf.Max(0f, speedCurve.Evaluate((float)i / Samples));
            cumulative[i] = cumulative[i - 1] + (previous + value) * 0.5f / Samples;
            previous = value;
        }
        return cumulative;
    }

    // Area travelled by normalised flight time u, as a fraction of peak speed x flight seconds.
    public static float DistanceFraction(float[] cumulative, float u)
    {
        float x = Mathf.Clamp01(u) * Samples;
        int i = Mathf.Min(Samples - 1, (int)x);
        return Mathf.Lerp(cumulative[i], cumulative[i + 1], x - i);
    }

    // Distance the palm covers over a whole flight (world units, before the Giant scale).
    public static float Range(AnimationCurve speedCurve, float peakSpeed, float flightSeconds)
    {
        return speedCurve == null ? 0f : peakSpeed * flightSeconds * Cumulative(speedCurve)[Samples];
    }
}
