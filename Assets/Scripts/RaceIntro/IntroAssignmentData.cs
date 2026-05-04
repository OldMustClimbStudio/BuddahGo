using System;
using UnityEngine.Serialization;

[Serializable]
public struct IntroAssignmentData
{
    public int sequenceId;
    public int playerOwnerId;
    public int playerObjectId;
    public int slotIndex;
    public string splineId;
    // Absolute intro timing is scheduled only after every client has prepared visuals.
    public double introStartNetworkTime;
    // Phase 6 — total traversal time T (seconds) for buddah to complete spline
    // (start velocity v_max = 2L/T, end velocity 0, linear deceleration). Replaces
    // pre-Phase-6 constant-velocity introSpeedMetersPerSecond. FormerlySerializedAs
    // preserves scene refs across the rename.
    [FormerlySerializedAs("introSpeedMetersPerSecond")]
    public float introTraversalTimeSeconds;
    public double goNetworkTime;
    public float handoffLeadTime;
}
