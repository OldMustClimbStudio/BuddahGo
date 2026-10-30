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
        public float farThresholdMeters;
        public float postPulseSeconds;

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
            farThresholdMeters = 0f,
            postPulseSeconds = 0.15f,
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

        // README section 8: Low keeps priority 1 at 40 % emission, Medium 1-2 at 70 %, High everything. Layers that carry
        // gameplay information are never dropped. Low also turns every real-time light off.
        internal static bool Keeps(VfxQualityTier tier, int priority, bool gameplayInfo, SkillVfxFunction function)
        {
            if (function == SkillVfxFunction.Light && tier == VfxQualityTier.Low)
                return false;
            if (gameplayInfo)
                return true;
            switch (tier)
            {
                case VfxQualityTier.Low: return priority <= 1;
                case VfxQualityTier.Medium: return priority <= 2;
                default: return true;
            }
        }

        internal static float EmissionScale(VfxQualityTier tier)
        {
            switch (tier)
            {
                case VfxQualityTier.Low: return 0.4f;
                case VfxQualityTier.Medium: return 0.7f;
                default: return 1f;
            }
        }

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

        static float SafeRatio(float value, float reference)
        {
            return reference > 1e-4f ? Mathf.Max(1e-3f, Mathf.Abs(value)) / reference : 1f;
        }
    }
}
