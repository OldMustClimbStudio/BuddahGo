using UnityEngine;

internal static class BlackCurtainPresentation
{
    internal struct Settings
    {
        public Material fullscreenMaterial;
        public string fullscreenMaterialName;
        public string fullscreenShaderName;
        public float expandDurationSeconds;
        public float holdDurationSeconds;
        public float fadeOutDurationSeconds;
        public float maxOpacity;
        public string progressProperty;
        public string opacityProperty;
        public string elapsedTimeProperty;
        public string activeProperty;
        public string centerProperty;
        public Vector3 centerWorldOffset;
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

        bool localShouldSeeEdge = localIsCaster ^ isAnti;
        Vector2 center = ResolveScreenCenter(localCamera, caster, settings.centerWorldOffset);
        GameLog.Verbose($"[{logPrefix}][Observers] localIsCaster={localIsCaster}, isAnti={isAnti}, localShouldSeeEdge={localShouldSeeEdge}, camera={localCamera.name}");
        viewController.Play(
            settings.fullscreenMaterial,
            settings.fullscreenMaterialName,
            settings.fullscreenShaderName,
            settings.expandDurationSeconds,
            settings.holdDurationSeconds,
            settings.fadeOutDurationSeconds,
            settings.maxOpacity,
            settings.progressProperty,
            settings.opacityProperty,
            settings.elapsedTimeProperty,
            settings.activeProperty,
            settings.centerProperty,
            localShouldSeeEdge,
            center);
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

    private static Vector2 ResolveScreenCenter(Camera localCamera, SkillExecutor caster, Vector3 centerWorldOffset)
    {
        if (localCamera == null || caster == null)
            return new Vector2(0.5f, 0.5f);

        Vector3 worldPosition = caster.transform.position + centerWorldOffset;
        Vector3 viewportPosition = localCamera.WorldToViewportPoint(worldPosition);
        if (viewportPosition.z <= 0f)
            return new Vector2(0.5f, 0.5f);

        return new Vector2(
            Mathf.Clamp01(viewportPosition.x),
            Mathf.Clamp01(viewportPosition.y));
    }
}
