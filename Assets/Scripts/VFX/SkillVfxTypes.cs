using BuddahGo.Rendering;
using UnityEngine;

namespace BuddahGo.VFX
{
    // Vocabulary of Docs/vfx/README.md section 3: time phases, visual tiers and functional layers.
    public enum SkillVfxPhase
    {
        Buf = 0,
        Chg = 1,
        Prj = 2,
        Imp = 3,
        Dis = 4,
        Lin = 5,
    }

    public enum SkillVfxTier
    {
        Core = 0,
        Secondary = 1,
        Accent = 2,
    }

    public enum SkillVfxFunction
    {
        Glow,
        Flash,
        Distortion,
        Smoke,
        Sparks,
        Decal,
        Mesh,
        Light,
        Post,
    }

    // What a layer's start/end numbers are measured in. The phase's absolute start comes from gameplay values, so a
    // layer sheet written in these units follows the design tables without hand edits (README 3.4).
    public enum SkillVfxWindowUnit
    {
        Seconds,
        PhaseFraction,
        ImpFrames,
        LightSeconds,
    }

    public enum SkillVfxAnchor
    {
        Self,
        CasterFeet,
        DetachAtStart,
        // Placed once when the layer starts at a fixed point of the caster's frame (casterOffset / casterEuler on the
        // layer), turned with the launch direction and scaled with the caster, then left in the world.
        CasterRelative,
        // Same offset as CasterRelative, but kept on the caster every frame of the window (buffs, trails, zones that
        // move with the caster), turned with the caster's own facing.
        CasterAttached,
        // Kept on the caster's position (and scale) every frame like CasterAttached, but keeps the facing of the
        // moment it started: the caster turning does not turn it (禅定缓流's ground zone).
        CasterFollow,
    }

    // How a caster-anchored layer follows the caster's body scale (巨大化 ×4, its anti ×0.3; Docs/vfx/giant.md).
    public enum SkillVfxCasterScaling
    {
        // The layer is scaled with the caster at once: every particle's size, spread and motion (a magnifying glass).
        Whole,
        // Area systems follow the caster over scaleUpSeconds / scaleDownSeconds with an alpha dip, the other systems
        // (ScalingMode Shape) only spread their spawn area and keep their grain, and a surge ring marks the change.
        Blend,
        // The layer keeps its authored size at any body scale; only its caster offset follows the body (a route
        // laid on the ground in world units, e.g. 金足破阵's ink route, Docs/vfx/yuanqi-giant-charge.md).
        None,
    }

    // How a layer's content follows a window that is longer or shorter than the one it was tuned for (the phase length
    // comes from gameplay, so a design table change moves it). k = live window / window at the reference length.
    public enum SkillVfxTimeScaling
    {
        // Content plays at its authored timing; only the window (when it starts and stops emitting) moves.
        Fixed,
        // Slow motion / fast forward: every particle system runs at 1 / k speed, so delays, lifetimes and motion all
        // scale together and the effect looks the same, just longer or shorter.
        Stretch,
        // Only the schedule scales: start delays, emission lengths and burst times × k; particles themselves keep
        // their authored lifetime and speed (a 10 % delay stays at 10 % of the window).
        Rhythm,
    }

    // Visual-only numbers of one VFX table row (README section 5), resolved for one effect instance.
    [System.Serializable]
    public struct SkillVfxVisualValues
    {
        public float chgRatio;
        public int impFrames;
        public float disSeconds;
        public float linSeconds;
        public float bufFadeSeconds;
        public float trailSeconds;
        public float scaleMultiplier;
        public float lightSeconds;
        public float lightIntensity;

        public static SkillVfxVisualValues Defaults => new SkillVfxVisualValues
        {
            chgRatio = 1f,
            impFrames = 4,
            disSeconds = 0.4f,
            linSeconds = 0f,
            bufFadeSeconds = 0.3f,
            trailSeconds = 0.25f,
            scaleMultiplier = 1f,
            lightSeconds = 0.3f,
            lightIntensity = 1f,
        };
    }

    // Pure rules shared by the controller and the EditMode tests.
    internal static class SkillVfxRules
    {
        internal const float FramesPerSecond = 60f;

        // CasterRelative anchor. The offset is in the caster's frame at caster scale 1 (x right, y up, z along the
        // launch direction); the frame is the launch direction flattened to yaw, so any release angle round the caster
        // turns the layer with it. Caster scale (PlayerScaleEffect scales the caster root) scales the offset; the
        // gameplay collider does not, so the anti variant and giant / small casters keep the layer at the same spot
        // on the caster's body.
        internal static void CasterRelativePose(Vector3 casterPosition, float casterScale, Quaternion launchRotation,
            Vector3 offset, Vector3 euler, out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = launchRotation * Vector3.forward;
            forward.y = 0f;
            Quaternion yaw = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            position = casterPosition + yaw * (offset * casterScale);
            rotation = yaw * Quaternion.Euler(euler);
        }

        // README section 8 and Graphics.xlsx (vfx_quality): a tier keeps layers up to its priority at its emission;
        // layers that carry gameplay information are never dropped. Low (fallback and table) turns real-time lights off.
        internal static bool Keeps(VfxTierSettings tier, int priority, bool gameplayInfo, SkillVfxFunction function)
        {
            if (function == SkillVfxFunction.Light && !tier.RealtimeLights)
                return false;
            return gameplayInfo || priority <= tier.KeepPriority;
        }

        internal static bool Keeps(VfxQualityTier tier, int priority, bool gameplayInfo, SkillVfxFunction function) =>
            Keeps(VfxTierSettings.Fallback(tier), priority, gameplayInfo, function);

        // Emission of a kept layer's shape systems: gameplay layers have their own scale so they stay readable on Low.
        internal static float EmissionScale(VfxTierSettings tier, bool gameplayInfo) =>
            Mathf.Clamp(gameplayInfo ? tier.GameplayEmissionScale : tier.EmissionScale, 0.05f, 1f);

        // Emission of a layer's detail systems (many small particles): the tier's scale on every layer, gameplay or not.
        internal static float DetailEmissionScale(VfxTierSettings tier) => Mathf.Clamp(tier.EmissionScale, 0.05f, 1f);

        // README section 8: the pack draws an effect's shape with systems of 1-3 large sprites and its detail (sparks,
        // flecks, embers) with systems of tens of particles. A system whose bursts and rate add up to more than this
        // many particles per cycle is detail; thinning it keeps the shape readable.
        internal const float DetailParticleThreshold = 4f;

        internal static float ParticlesPerCycle(float burstCount, float ratePerSecond, float duration) =>
            burstCount + Mathf.Max(0f, ratePerSecond) * Mathf.Max(0f, duration);

        internal static bool IsDetailSystem(ParticleSystem system)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            if (!emission.enabled)
                return false;
            float bursts = 0f;
            for (int b = 0; b < emission.burstCount; b++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(b);
                bursts += burst.count.constantMax * Mathf.Max(1, burst.cycleCount);
            }
            return ParticlesPerCycle(bursts, emission.rateOverTimeMultiplier, system.main.duration) > DetailParticleThreshold;
        }

        // Length of a layer's own trail: cosmetic layers follow the tier, gameplay layers keep the table's length.
        internal static float TrailSeconds(VfxTierSettings tier, bool gameplayInfo, float trailSeconds) =>
            gameplayInfo ? trailSeconds : trailSeconds * Mathf.Clamp(tier.TrailScale, 0.1f, 1f);

        // Seconds from the phase start for a start/end number in the given unit. phaseSeconds already includes chg_ratio
        // for the charge phase.
        internal static float ToSeconds(float value, SkillVfxWindowUnit unit, float phaseSeconds, SkillVfxVisualValues visual)
        {
            switch (unit)
            {
                case SkillVfxWindowUnit.PhaseFraction: return value * Mathf.Max(0f, phaseSeconds);
                case SkillVfxWindowUnit.ImpFrames: return value * Mathf.Max(1, visual.impFrames) / FramesPerSecond;
                case SkillVfxWindowUnit.LightSeconds: return value * Mathf.Max(0f, visual.lightSeconds);
                default: return value;
            }
        }

        // k of SkillVfxTimeScaling: the live window over the same window at the phase's reference length. Windows in
        // Seconds / ImpFrames / LightSeconds do not move with the phase, so they give 1; no reference also gives 1.
        internal static float TimeScale(float start, float end, float referenceStart, float referenceEnd)
        {
            float reference = referenceEnd - referenceStart;
            if (reference <= 1e-4f)
                return 1f;
            return Mathf.Clamp((end - start) / reference, MinTimeScale, MaxTimeScale);
        }

        internal const float MinTimeScale = 0.05f;
        internal const float MaxTimeScale = 20f;

        // Body fit (README section 4): the mesh is authored to the reference collider and keeps its proportions;
        // it is scaled uniformly so it fits inside the live collider (the anti volley's 60 × 50 × 20 box gives the
        // palm doc's "small hand, about 0.6"). Stretching per axis would squash a hand into a claw.
        internal static float ContainScale(Vector3 colliderSize, Vector3 referenceColliderSize)
        {
            return Mathf.Min(SafeRatio(colliderSize.x, referenceColliderSize.x),
                Mathf.Min(SafeRatio(colliderSize.y, referenceColliderSize.y), SafeRatio(colliderSize.z, referenceColliderSize.z)));
        }

        internal static float UniformScale(Vector3 colliderSize, Vector3 referenceColliderSize)
        {
            return SafeRatio(colliderSize.y, referenceColliderSize.y);
        }

        // Window alpha with fade-in/out in seconds; 0 outside [start, end).
        internal static float WindowAlpha(float t, float start, float end, float fadeIn, float fadeOut)
        {
            if (t < start || t >= end)
                return 0f;
            float a = 1f;
            if (fadeIn > 0f)
                a = Mathf.Min(a, (t - start) / fadeIn);
            if (fadeOut > 0f)
                a = Mathf.Min(a, (end - t) / fadeOut);
            return Mathf.Clamp01(a);
        }

        // Longest a particle of this start lifetime can live (seconds of particle time).
        internal static float MaxLifetime(ParticleSystem.MinMaxCurve lifetime)
        {
            switch (lifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                case ParticleSystemCurveMode.TwoConstants:
                    return lifetime.constantMax;
                default:
                    return lifetime.curveMultiplier;
            }
        }

        // Normalized age from which a particle now at `age` plays the rest of its own ending: the point on the
        // gradient's last segment (its fade-out) with the same alpha, so nothing jumps. Already fading: unchanged.
        internal static float EndingAge(Gradient gradient, float age)
        {
            GradientAlphaKey[] keys = gradient.alphaKeys;
            if (keys.Length < 2)
                return age;
            GradientAlphaKey from = keys[keys.Length - 2];
            GradientAlphaKey to = keys[keys.Length - 1];
            if (age >= from.time || from.alpha <= to.alpha)
                return age;
            float alpha = Mathf.Min(gradient.Evaluate(age).a, from.alpha);
            return Mathf.Lerp(from.time, to.time, Mathf.InverseLerp(from.alpha, to.alpha, alpha));
        }

        // Caster scale blend (SkillVfxCasterScaling.Blend, Docs/vfx/giant.md). A change smaller than this share of the
        // current scale is not a change (float noise, a body still settling).
        internal const float ScaleChangeThreshold = 0.02f;

        internal static bool ScaleChanged(float from, float to)
        {
            return Mathf.Abs(to - from) > ScaleChangeThreshold * Mathf.Max(Mathf.Abs(from), 1e-3f);
        }

        // Share of the way from the old scale to the new one at u = elapsed / blend seconds: growing eases out (fast
        // start behind the surge, slow landing), shrinking eases in and out.
        internal static float BlendProgress(float u, bool shrinking)
        {
            u = Mathf.Clamp01(u);
            if (shrinking)
                return u * u * (3f - 2f * u);
            float rest = 1f - u;
            return 1f - rest * rest * rest;
        }

        // Alpha of the area systems during a blend: dips by `depth` halfway, back to 1 at both ends.
        internal static float BlendAlpha(float u, float depth)
        {
            if (u <= 0f || u >= 1f)
                return 1f;
            return 1f - Mathf.Clamp01(depth) * Mathf.Sin(Mathf.PI * u);
        }

        // Detail particles born at area scale s: size × s^exponent (grain kept), emission × s² (density kept) within
        // [1 / max, max].
        internal static float DetailSize(float scale, float exponent)
        {
            return Mathf.Pow(Mathf.Max(scale, 1e-3f), exponent);
        }

        internal static float DetailEmission(float scale, float max)
        {
            float ceiling = Mathf.Max(1f, max);
            return Mathf.Clamp(scale * scale, 1f / ceiling, ceiling);
        }

        // Alpha share left of a particle that was alive when the blend started.
        internal static float HandoffAlpha(float elapsed, float seconds)
        {
            return seconds <= 0f ? 0f : 1f - Mathf.Clamp01(elapsed / seconds);
        }

        static float SafeRatio(float value, float reference)
        {
            return reference > 1e-4f ? Mathf.Max(1e-3f, Mathf.Abs(value)) / reference : 1f;
        }
    }
}
