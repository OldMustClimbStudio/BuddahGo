using UnityEngine;

[CreateAssetMenu(fileName = "Skill_BlackCurtain", menuName = "Skills/Actions/Normal/Black Curtain")]
public class Skill_BlackCurtain : SkillAction
{
    [Header("Screen Effect")]
    [Tooltip("Look of the curtain (H1 墨眼): shader, world layers and opening curves, shared with the other 一念光明 skill.")]
    [SerializeField] private BlackCurtainEyeProfile eyeProfile;
    [Min(0.05f)] public float expandDurationSeconds = 1.25f;
    [Min(0f)] public float holdDurationSeconds = 0.75f;
    [Min(0f)] public float fadeOutDurationSeconds = 0.35f;
    [Range(0f, 1f)] public float maxOpacity = 1f;

    [Header("Duration Override")]
    [Tooltip("Optional duration override used by the observer feel timing. 0 = follow screen effect total duration.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Header("VFX")]
    [Tooltip("Release slot played on the caster for the whole curtain, on every client. Empty = none.")]
    [SerializeField] public string releaseVfxId = "curtain_release";
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = "blackcurtain_observers";
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        BuddahGo.Match.SkillPerceptionState.ApplyCurtain(caster, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds);

        float actualDuration = ResolveVfxDuration(vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds);
        GameLog.Verbose($"[Skill_BlackCurtain][Server] Triggered by {caster.name}, totalDuration={actualDuration:0.00}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        // Before the curtain's own gate: the release is seen by everyone, affected or not.
        PlaySlotOnCaster(caster, releaseVfxId, ResolveVfxDuration(vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds));

        var settings = new BlackCurtainPresentation.Settings
        {
            eyeProfile = eyeProfile,
            expandDurationSeconds = expandDurationSeconds,
            holdDurationSeconds = holdDurationSeconds,
            fadeOutDurationSeconds = fadeOutDurationSeconds,
            maxOpacity = maxOpacity,
        };
        if (!BlackCurtainPresentation.TryPlay(caster, false, localIsCaster, GetType().Name, settings))
            return;

        float actualDuration = ResolveVfxDuration(vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, actualDuration);

        GameLog.Verbose($"[Skill_BlackCurtain][Observers] Local player affected by '{skillId}' (slot {slotIndex})");
    }

    public override void CollectVfxSlots(System.Collections.Generic.List<VfxSlotTiming> into)
    {
        AddVfxSlot(into, releaseVfxId, vfxDurationSeconds, expandDurationSeconds + holdDurationSeconds + fadeOutDurationSeconds,
            "SkillEffectBlackCurtain.expandDurationSeconds + Skill.effectDurationSeconds → holdDurationSeconds + fadeOutDurationSeconds");
    }
}
