using System;

namespace BuddahGo.Rendering
{
    public enum VfxQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    // Effects quality chosen in the graphics settings (Graphics/GraphicsOption vfx_quality). A stub until the VFX spec
    // defines what each tier changes: effects read Current or subscribe to Changed; nothing reacts to it yet.
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
