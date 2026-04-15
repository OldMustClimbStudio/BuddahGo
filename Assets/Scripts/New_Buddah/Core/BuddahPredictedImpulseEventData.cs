using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    [Serializable]
    public struct BuddahPredictedImpulseEventData
    {
        public uint EventId;
        public uint EventTick;
        public Vector3 Impulse;
        public float TurnTorqueImpulse;
        public BuddahPredictedImpulseSourceType SourceType;
        public int SourceObjectId;
        public bool Consumed;

        public BuddahPredictedImpulseEventData(
            uint eventId,
            uint eventTick,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            int sourceObjectId)
        {
            EventId = eventId;
            EventTick = eventTick;
            Impulse = impulse;
            TurnTorqueImpulse = turnTorqueImpulse;
            SourceType = sourceType;
            SourceObjectId = sourceObjectId;
            Consumed = false;
        }
    }
}
