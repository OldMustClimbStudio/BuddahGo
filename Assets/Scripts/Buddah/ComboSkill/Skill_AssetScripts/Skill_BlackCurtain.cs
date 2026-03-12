using UnityEngine;

[CreateAssetMenu(fileName = "Skill_BlackCurtain", menuName = "Skills/Actions/Normal/Black Curtain")]
public class Skill_BlackCurtain : SkillAction
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

    [Header("Caster VFX (Optional)")]
    [Tooltip("VFX lifetime. 0 = follow screen effect total duration.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "black_curtain_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds;
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, actualVfxDuration, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_BlackCurtain][Server] Triggered by {caster.name}, totalDuration={actualVfxDuration:0.00}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        SkillExecutor localExecutor = FindLocalExecutor();
        if (localExecutor == null || localExecutor == caster)
            return;

        Camera localCamera = ResolveLocalCamera();
        if (localCamera == null)
        {
            Debug.LogWarning("[Skill_BlackCurtain][Observers] No local camera found for screen effect.");
            return;
        }

        var effect = localCamera.GetComponent<BlackCurtainScreenEffect>();
        if (effect == null)
            effect = localCamera.gameObject.AddComponent<BlackCurtainScreenEffect>();

        effect.ApplyOrRefresh(
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
            activeProperty);

        Debug.Log($"[Skill_BlackCurtain][Observers] Local player affected by '{skillId}' (slot {slotIndex})");
    }

    private static SkillExecutor FindLocalExecutor()
    {
        SkillExecutor[] executors = FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        for (int i = 0; i < executors.Length; i++)
        {
            if (executors[i] != null && executors[i].IsOwner)
                return executors[i];
        }

        return null;
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
}
