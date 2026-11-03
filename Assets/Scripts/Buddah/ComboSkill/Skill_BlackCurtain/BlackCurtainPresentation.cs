using UnityEngine;

internal static class BlackCurtainPresentation
{
    internal struct Settings
    {
        public BlackCurtainEyeProfile eyeProfile;
        public float expandDurationSeconds;
        public float holdDurationSeconds;
        public float fadeOutDurationSeconds;
        public float maxOpacity;
    }

    internal static bool TryPlay(SkillExecutor caster, bool isAnti, bool localIsCaster, string logPrefix, Settings settings)
    {
        Camera localCamera = ResolveLocalCamera();
        if (localCamera == null)
        {
            Debug.LogWarning($"[{logPrefix}][Observers] No local camera found for screen effect.");
            return false;
        }

        var viewController = localCamera.GetComponent<BlackCurtainViewController>();
        if (viewController == null)
            viewController = localCamera.gameObject.AddComponent<BlackCurtainViewController>();

        // Normal cast: the caster sees the track edge and everyone else gets the victim view; the anti variant flips it.
        bool localShouldSeeEdge = localIsCaster ^ isAnti;
        GameLog.Verbose($"[{logPrefix}][Observers] localIsCaster={localIsCaster}, isAnti={isAnti}, localShouldSeeEdge={localShouldSeeEdge}, camera={localCamera.name}");
        Transform casterRoot = caster != null ? caster.transform : null;
        viewController.Play(
            settings.eyeProfile,
            ResolveCasterVisual(casterRoot),
            casterRoot,
            settings.expandDurationSeconds,
            settings.holdDurationSeconds,
            settings.fadeOutDurationSeconds,
            settings.maxOpacity,
            isAnti,
            localShouldSeeEdge);
        return true;
    }

    private static Camera ResolveLocalCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                return cameras[i];
        }

        return null;
    }

    // The root is tick-stepped while VisualRoot is smoothed every frame; the eye follows VisualRoot or it stutters.
    private static Transform ResolveCasterVisual(Transform casterRoot)
    {
        if (casterRoot == null)
            return null;
        var visualBridge = casterRoot.GetComponent<NewBuddah.PredictionV2.Visual.BuddahPredictionVisualRootBridge>();
        Transform visual = visualBridge != null ? visualBridge.GetVisualRoot() : null;
        return visual != null ? visual : casterRoot;
    }
}
