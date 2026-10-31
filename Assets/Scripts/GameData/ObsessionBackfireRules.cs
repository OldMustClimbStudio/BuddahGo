using UnityEngine;

/// <summary>Pure obsession and backfire-penalty math used by <see cref="ObsessionFigure"/>.</summary>
internal static class ObsessionBackfireRules
{
    // Absorbs float error in seconds / tickDelta so an exact multiple of the tick does not round up one tick.
    private const double TickRoundingEpsilon = 1e-6;

    /// <summary>Sigmoid backfire chance in percent: Pmax / (1 + e^(-k (x - mid))), Pmax at or above keepPMaxAtOrAboveX.</summary>
    internal static float ProbabilityPercent(float obsessionValue, float minValue, float maxValue,
        float pMaxPercent, float k, float midpointX, float keepPMaxAtOrAboveX)
    {
        float x = Mathf.Clamp(obsessionValue, minValue, maxValue);
        float pMax = Mathf.Clamp(pMaxPercent, 0f, 100f);
        float steepness = Mathf.Max(0.0001f, k);
        float keepAt = Mathf.Clamp(keepPMaxAtOrAboveX, minValue, maxValue);

        if (x >= keepAt)
            return pMax;

        float exp = Mathf.Exp(-steepness * (x - midpointX));
        return Mathf.Clamp(pMax / (1f + exp), 0f, 100f);
    }

    /// <summary>Natural decay for one frame; returns the value unchanged while the backfire penalty pauses it.</summary>
    internal static float Drain(float current, float minValue, float maxValue, float perSecond, float deltaTime, bool paused)
    {
        if (paused || current <= minValue || perSecond <= 0f)
            return current;
        return Mathf.Clamp(current - perSecond * deltaTime, minValue, maxValue);
    }

    /// <summary>current × ratio clamped to the range; used for the backfire end (×0.8) and MultiplyServer.</summary>
    internal static float Scale(float current, float ratio, float minValue, float maxValue)
        => Mathf.Clamp(current * Mathf.Max(0f, ratio), minValue, maxValue);

    /// <summary>Server tick at which a penalty of <paramref name="seconds"/> ends; 0 (the inactive sentinel) for no penalty.</summary>
    internal static uint EndTick(uint nowTick, float seconds, double tickDelta)
    {
        if (seconds <= 0f)
            return 0u;
        double ticks = System.Math.Ceiling(seconds / System.Math.Max(0.0001, tickDelta) - TickRoundingEpsilon);
        ulong end = (ulong)nowTick + (ulong)System.Math.Max(1.0, ticks);
        return end >= uint.MaxValue ? uint.MaxValue : (uint)end;
    }

    /// <summary>A second backfire while one is active keeps the later end and never stacks the end ratio.</summary>
    internal static uint ExtendEndTick(uint currentEndTick, uint candidateEndTick)
        => currentEndTick > candidateEndTick ? currentEndTick : candidateEndTick;

    /// <summary>The penalty has reached its end tick but has not been ended yet (end tick still set).</summary>
    internal static bool IsEndDue(uint endTick, uint nowTick) => endTick != 0u && nowTick >= endTick;

    internal static float SecondsLeft(uint endTick, uint nowTick, double tickDelta)
    {
        if (endTick == 0u || nowTick >= endTick)
            return 0f;
        return (float)((endTick - nowTick) * tickDelta);
    }
}
