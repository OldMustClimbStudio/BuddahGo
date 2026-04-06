using System;

namespace SteamMultiplayer.Network.Results
{
    [Serializable]
    public class FinalMatchResultEntry
    {
        public int ClientId;
        public string PlayerName;
        public int FinalRank;
        public float FinalCompletionPercent;
        public bool IsFinished;
        public int FinishOrder;
        public double FinishServerTime;
        public int Lap;
        public int Checkpoints;
        public float DistanceOnTrack;
        public float LapProgress01;
    }
}
