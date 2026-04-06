using FishNet;
using UnityEngine;

public static class IntroTimeUtility
{
    public static double GetNetworkTimeSeconds()
    {
        return InstanceFinder.TimeManager != null
            ? InstanceFinder.TimeManager.TicksToTime()
            : Time.unscaledTimeAsDouble;
    }
}
