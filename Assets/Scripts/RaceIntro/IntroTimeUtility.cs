using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;

public enum IntroRuntimeState
{
    Idle,
    AssignmentsReceived,
    IntroPrepared,
    VisualPrepared,
    VisualStarted,
    IntroRunning,
    WaitingForGo,
    AuthoritativeGoIssued,
    AuthoritativeHandoffPending,
    AuthoritativeHandoffApplied,
    GameplayLive,
    Cancelled
}

public readonly struct IntroSequenceTiming
{
    public readonly int SequenceId;
    public readonly double IntroStartNetworkTime;
    public readonly double GoNetworkTime;

    public IntroSequenceTiming(int sequenceId, double introStartNetworkTime, double goNetworkTime)
    {
        SequenceId = sequenceId;
        IntroStartNetworkTime = introStartNetworkTime;
        GoNetworkTime = goNetworkTime;
    }

    public bool IsValid => SequenceId >= 0 && IntroStartNetworkTime >= 0d && GoNetworkTime >= IntroStartNetworkTime;
}

public static class IntroTimeUtility
{
    public static double GetNetworkTimeSeconds()
    {
        if (InstanceFinder.TimeManager == null)
            return Time.unscaledTimeAsDouble;

        // Intro timing must use the synchronized server tick domain.
        // LocalTick is client-local and will drift relative to server-issued introStart/go times.
        return InstanceFinder.TimeManager.TicksToTime(InstanceFinder.TimeManager.GetPreciseTick(TickType.Tick));
    }

    public static double GetIntroElapsedSeconds(IntroSequenceTiming timing, double networkTimeSeconds)
    {
        if (!timing.IsValid)
            return 0d;

        return Mathf.Max(0f, (float)(networkTimeSeconds - timing.IntroStartNetworkTime));
    }

    public static double GetIntroDurationSeconds(IntroSequenceTiming timing)
    {
        return timing.IsValid ? System.Math.Max(0d, timing.GoNetworkTime - timing.IntroStartNetworkTime) : 0d;
    }

    public static double GetClampedIntroNetworkTime(IntroSequenceTiming timing, double networkTimeSeconds)
    {
        if (!timing.IsValid)
            return networkTimeSeconds;

        return System.Math.Min(System.Math.Max(networkTimeSeconds, timing.IntroStartNetworkTime), timing.GoNetworkTime);
    }

    public static double GetTimelineSeekSeconds(IntroSequenceTiming timing, double networkTimeSeconds, double timelineDurationSeconds)
    {
        double introDuration = GetIntroDurationSeconds(timing);
        if (timelineDurationSeconds <= 0d || introDuration <= 0d)
            return 0d;

        double clampedElapsed = System.Math.Min(GetIntroElapsedSeconds(timing, networkTimeSeconds), introDuration);
        double normalized = introDuration > 0d ? clampedElapsed / introDuration : 0d;
        return System.Math.Max(0d, System.Math.Min(timelineDurationSeconds, timelineDurationSeconds * normalized));
    }

    public static bool HasReachedIntroStart(IntroSequenceTiming timing, double networkTimeSeconds)
    {
        return timing.IsValid && networkTimeSeconds >= timing.IntroStartNetworkTime;
    }

    public static bool HasReachedGo(IntroSequenceTiming timing, double networkTimeSeconds)
    {
        return timing.IsValid && networkTimeSeconds >= timing.GoNetworkTime;
    }
}
