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
    [SerializeField] private string centerProperty = "_Center";
    [SerializeField] private Vector3 centerWorldOffset = new Vector3(0f, 1f, 0f);

    [Header("Duration Override")]
    [Tooltip("Optional duration override used by the observer feel timing. 0 = follow screen effect total duration.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = "blackcurtain_observers";
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        float actualDuration = ResolveVfxDuration(vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds);
        GameLog.Verbose($"[Skill_BlackCurtain][Server] Triggered by {caster.name}, totalDuration={actualDuration:0.00}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        var settings = new BlackCurtainPresentation.Settings
        {
            fullscreenMaterial = fullscreenMaterial,
            fullscreenMaterialName = fullscreenMaterialName,
            fullscreenShaderName = fullscreenShaderName,
            expandDurationSeconds = expandDurationSeconds,
            holdDurationSeconds = holdDurationSeconds,
            fadeOutDurationSeconds = fadeOutDurationSeconds,
            maxOpacity = maxOpacity,
            progressProperty = progressProperty,
            opacityProperty = opacityProperty,
            elapsedTimeProperty = elapsedTimeProperty,
            activeProperty = activeProperty,
            centerProperty = centerProperty,
            centerWorldOffset = centerWorldOffset,
        };
        if (!BlackCurtainPresentation.TryPlay(caster, isAnti, localIsCaster, GetType().Name, settings))
            return;

        float actualDuration = ResolveVfxDuration(vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, actualDuration);

        GameLog.Verbose($"[Skill_BlackCurtain][Observers] Local player affected by '{skillId}' (slot {slotIndex})");
    }

}
