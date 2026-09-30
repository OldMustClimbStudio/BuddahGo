using UnityEngine;

internal static class ScaleSkillPresentation
{
    internal static void ApplyScaleEffect(SkillExecutor caster, float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds)
    {
        if (caster == null)
            return;

        var effect = caster.GetComponent<PlayerScaleEffect>();
        if (effect == null)
            effect = caster.gameObject.AddComponent<PlayerScaleEffect>();

        effect.ApplyOrRefresh(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds);
    }

    internal static void ResetLocalCamera(SkillExecutor caster, string logPrefix)
    {
        var camera = caster.GetComponentInChildren<PlayerCamera>(true);
        if (camera == null)
            camera = caster.GetComponentInParent<PlayerCamera>();

        if (camera == null)
        {
            Debug.LogWarning($"[{logPrefix}][Owner] Missing PlayerCamera for local camera effect.");
            return;
        }

        camera.ResetRuntimeEffects();
        GameLog.Verbose($"[{logPrefix}][Owner] Applied local camera reinforcement.");
    }
}
