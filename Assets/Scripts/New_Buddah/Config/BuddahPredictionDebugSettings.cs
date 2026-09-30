using System;

namespace NewBuddah.PredictionV2.Config
{
    [Serializable]
    public class BuddahPredictionDebugSettings
    {
        public bool enableVerboseLogs;
        // Retained for serialized compatibility. Screen diagnostics are no longer rendered.
        [UnityEngine.HideInInspector] public bool enableOnScreenDebug;
        public bool dumpReplicate;
        public bool dumpReconcile;
        public bool dumpGateState;
    }
}
