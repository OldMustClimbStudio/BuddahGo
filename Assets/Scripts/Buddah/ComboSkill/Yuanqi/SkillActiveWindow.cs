using System;

/// <summary>
/// When a skill counts as "生效中" on the state board, relative to its execution: the window starts
/// <see cref="StartOffsetSeconds"/> after execution and lasts <see cref="DurationSeconds"/>. A zero duration
/// registers nothing. Negative inputs are clamped to zero.
/// </summary>
public readonly struct SkillActiveWindow
{
    public SkillActiveWindow(float startOffsetSeconds, float durationSeconds)
    {
        StartOffsetSeconds = Math.Max(0f, startOffsetSeconds);
        DurationSeconds = Math.Max(0f, durationSeconds);
    }

    public float StartOffsetSeconds { get; }
    public float DurationSeconds { get; }

    public override string ToString() => $"+{StartOffsetSeconds:0.##}s for {DurationSeconds:0.##}s";
}
