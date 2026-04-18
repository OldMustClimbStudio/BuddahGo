using UnityEngine;

namespace BuddahGo.GlobalSettings
{
    // Locks render frame rate to an integer multiple of the FishNet tick rate so
    // PredictionSmoother's sub-frame interpolation fraction advances in even steps
    // across frames. Without this, a non-integer fps-to-tick ratio produces a
    // "beat frequency" tremor in visual-root smoothing that shows as regular
    // micro-oscillation on fast-moving predicted objects.
    public static class FrameRateLock
    {
        private const int TargetFrameRate = 120;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
