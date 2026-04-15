using System;

namespace NewBuddah.PredictionV2.Config
{
    [Serializable]
    public class BuddahPredictionDebugSettings
    {
        public bool enableVerboseLogs;
        public bool enableOnScreenDebug = true;
        public bool dumpReplicate;
        public bool dumpReconcile;
        public bool dumpGateState;
    }
}
