using UnityEngine;

namespace BuddahGo.GlobalSettings
{
    // Locks render frame rate to an integer multiple of the FishNet tick rate so
    // PredictionSmoother's sub-frame interpolation fraction advances in even steps
    // across frames. Without this, a non-integer fps-to-tick ratio produces a
    // "beat frequency" tremor in visual-root smoothing that shows as regular
    // micro-oscillation on fast-moving predicted objects.
    // Global/GlobalRule target_frame_rate owns the value; 120 is the fallback. This runs before the first scene; the
    // graphics settings (GraphicsSettingsApplier) then apply the player's VSync choice, on by default since tearing
    // showed in play tests. With VSync on the display's refresh rate paces frames instead of this cap.
    public static class FrameRateLock
    {
        private const int TargetFrameRate = 120;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            int configured = DesignTableOverlay.GlobalInt("target_frame_rate", TargetFrameRate);
            Application.targetFrameRate = configured > 0 ? configured : TargetFrameRate;
        }
    }
}
