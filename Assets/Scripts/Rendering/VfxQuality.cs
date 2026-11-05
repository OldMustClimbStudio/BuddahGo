using System;

namespace BuddahGo.Rendering
{
    public enum VfxQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>
    /// What one VFX tier changes (Graphics.xlsx, GraphicsOptionLevel vfx_quality rows, columns vfx*). Layers that carry
    /// gameplay information are never dropped and use <see cref="GameplayEmissionScale"/> and their full trail length.
    /// </summary>
    [Serializable]
    public struct VfxTierSettings
    {
        /// <summary>Highest slot-layer priority kept (1 = core only, 3 = everything); gameplay layers always stay.</summary>
        public int KeepPriority;
        /// <summary>Particle emission (rate and burst counts) of cosmetic layers.</summary>
        public float EmissionScale;
        /// <summary>Particle emission of layers that carry gameplay information.</summary>
        public float GameplayEmissionScale;
        /// <summary>Whether slot layers with a real-time light play.</summary>
        public bool RealtimeLights;
        /// <summary>Trail length of cosmetic layers' own trails.</summary>
        public float TrailScale;
        /// <summary>How many 如来神掌 ink-smoke simulations run at once (nearest first); negative = no cap.</summary>
        public int InkFluidBudget;
        /// <summary>Ink-smoke cell size factor: the grid keeps its box and has this many times fewer cells per axis.</summary>
        public float InkFluidCellScale;

        /// <summary>
        /// The tier without the design tables (tests, a build without the Graphics rows): today's layer rules (Low keeps
        /// priority 1 at 40 %, Medium 1-2 at 70 %, High all; Low has no lights) and nothing else scaled.
        /// </summary>
        public static VfxTierSettings Fallback(VfxQualityTier tier)
        {
            var s = new VfxTierSettings
            {
                KeepPriority = 3,
                EmissionScale = 1f,
                GameplayEmissionScale = 1f,
                RealtimeLights = true,
                TrailScale = 1f,
                InkFluidBudget = -1,
                InkFluidCellScale = 1f,
            };
            if (tier == VfxQualityTier.Low)
            {
                s.KeepPriority = 1;
                s.EmissionScale = 0.4f;
                s.RealtimeLights = false;
            }
            else if (tier == VfxQualityTier.Medium)
            {
                s.KeepPriority = 2;
                s.EmissionScale = 0.7f;
            }
            return s;
        }
    }

    // Effects quality chosen in the graphics settings (Graphics/GraphicsOption vfx_quality); the graphics applier sets
    // the tier with its row's values. Readers: SkillVfxPhaseController when a slot starts (layers kept, emission,
    // trails, lights), InkFluidVolume every frame (simulation budget) and when a palm spawns (grid resolution).
    // A change therefore shows on the next effect; nothing restarts. Changed fires for a reader that wants more.
    // Client-local and never networked, so gameplay-relevant effects must not depend on it.
    public static class VfxQuality
    {
        public static VfxQualityTier Current { get; private set; } = VfxQualityTier.High;

        /// <summary>The current tier's values: from the tables once the settings are applied, else the fallback.</summary>
        public static VfxTierSettings Settings { get; private set; } = VfxTierSettings.Fallback(VfxQualityTier.High);

        public static event Action<VfxQualityTier> Changed;

        public static void Set(VfxQualityTier tier) => Set(tier, VfxTierSettings.Fallback(tier));

        public static void Set(VfxQualityTier tier, VfxTierSettings settings)
        {
            bool changed = tier != Current || !settings.Equals(Settings);
            Current = tier;
            Settings = settings;
            if (changed)
                Changed?.Invoke(tier);
        }
    }
}
