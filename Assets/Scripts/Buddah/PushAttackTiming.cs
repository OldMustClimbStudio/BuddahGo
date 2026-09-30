using UnityEngine;

internal static class PushAttackTiming
{
    internal static float GetActionDelay(bool activeChargedMode, float delayedPushSeconds)
    {
        return activeChargedMode ? Mathf.Max(0f, delayedPushSeconds) : 0f;
    }

    internal static float GetCooldown(bool activeChargedMode, float chargedCooldown, float normalCooldown)
    {
        return activeChargedMode && chargedCooldown > 0f ? chargedCooldown : Mathf.Max(0f, normalCooldown);
    }
}
