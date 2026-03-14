using UnityEngine;

[CreateAssetMenu(fileName = "Skill_BlackCurtain_Anti", menuName = "Skills/Actions/Anti/Black Curtain Anti")]
public class Skill_BlackCurtain_Anti : SkillAction
{
    [Header("Screen Effect")]
    [Tooltip("Material used by the URP Full Screen Pass Renderer Feature.")]
    [SerializeField] private Material fullscreenMaterial;
    [Tooltip("Optional fallback when fullscreenMaterial is not assigned.")]
    [SerializeField] private string fullscreenMaterialName = "SG_BlackCurtain";
    [Tooltip("Optional fallback when material is looked up by shader name.")]
    [SerializeField] private string fullscreenShaderName = "Shader Graphs/SG_BlackCurtain";
    [Min(0.05f)] public float expandDurationSeconds = 1.25f;
    [Min(0f)] public float holdDurationSeconds = 0.75f;
    [Min(0f)] public float fadeOutDurationSeconds = 0.35f;
    [Range(0f, 1f)] public float maxOpacity = 1f;

    [Header("Shader Properties")]
    [SerializeField] private string progressProperty = "_Expansion";
    [SerializeField] private string opacityProperty = "_Opacity";
    [SerializeField] private string elapsedTimeProperty = "_ElapsedTime";
    [SerializeField] private string activeProperty = "_EffectActive";
    [SerializeField] private string centerProperty = "_Center";
    [SerializeField] private Vector3 centerWorldOffset = new Vector3(0f, 1f, 0f);

    [Header("Caster VFX (Optional)")]
    [Tooltip("VFX lifetime. 0 = follow screen effect total duration.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "black_curtain_anti_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = "blackcurtain_anti_observers";
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds;

        Debug.Log($"[Skill_BlackCurtain_Anti][Server] Triggered by {caster.name}, totalDuration={actualVfxDuration:0.00}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        Camera localCamera = ResolveLocalCamera();
        if (localCamera == null)
        {
            Debug.LogWarning("[Skill_BlackCurtain_Anti][Observers] No local camera found for screen effect.");
            return;
        }

        var viewController = localCamera.GetComponent<BlackCurtainViewController>();
        if (viewController == null)
            viewController = localCamera.gameObject.AddComponent<BlackCurtainViewController>();

        bool localShouldSeeEdge = localIsCaster ^ isAnti;
        Vector2 center = ResolveScreenCenter(localCamera, caster);
        Debug.Log($"[Skill_BlackCurtain_Anti][Observers] localIsCaster={localIsCaster}, isAnti={isAnti}, localShouldSeeEdge={localShouldSeeEdge}, camera={localCamera.name}");
        if (localShouldSeeEdge)
        {
            viewController.PlayWithEdge(
                fullscreenMaterial,
                fullscreenMaterialName,
                fullscreenShaderName,
                expandDurationSeconds,
                holdDurationSeconds,
                fadeOutDurationSeconds,
                maxOpacity,
                progressProperty,
                opacityProperty,
                elapsedTimeProperty,
                activeProperty,
                centerProperty,
                center);
        }
        else
        {
            viewController.PlayWithoutEdge(
                fullscreenMaterial,
                fullscreenMaterialName,
                fullscreenShaderName,
                expandDurationSeconds,
                holdDurationSeconds,
                fadeOutDurationSeconds,
                maxOpacity,
                progressProperty,
                opacityProperty,
                elapsedTimeProperty,
                activeProperty,
                centerProperty,
                center);
        }

        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds;
        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, actualVfxDuration, $"{skillId}_observers");

        Debug.Log($"[Skill_BlackCurtain_Anti][Observers] Local caster affected by '{skillId}' (slot {slotIndex})");
    }

    private static Camera ResolveLocalCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                return cameras[i];
        }

        return null;
    }

    private Vector2 ResolveScreenCenter(Camera localCamera, SkillExecutor caster)
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
