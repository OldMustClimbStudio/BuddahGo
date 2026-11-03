using System;

namespace BuddahGo.Rendering
{
    public enum VfxQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    // Effects quality chosen in the graphics settings (Graphics/GraphicsOption vfx_quality). SkillVfxPhaseController
    // reads Current when an effect starts (SkillVfxRules decides emission and which layers a tier keeps); nothing
    // subscribes to Changed yet, it is there for a live switch from the menu.
    // Client-local and never networked, so gameplay-relevant effects must not depend on it.
    public static class VfxQuality
    {
        public static VfxQualityTier Current { get; private set; } = VfxQualityTier.High;

        public static event Action<VfxQualityTier> Changed;

        public static void Set(VfxQualityTier tier)
        {
            if (tier == Current)
                return;
            Current = tier;
            Changed?.Invoke(tier);
        }
    }
}
