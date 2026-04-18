using System;

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
    public float introSpeedMetersPerSecond;
    public double goNetworkTime;
    public float handoffLeadTime;
}
