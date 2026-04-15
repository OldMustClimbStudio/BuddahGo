using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    [Serializable]
    public struct BuddahPredictedTeleportEventData
    {
        public uint EventId;
        public uint EventTick;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public float TargetProgress01;
        public BuddahPredictedTeleportSourceType SourceType;
        public bool SnapProgress;
        public bool ZeroLinearVelocity;
        public bool ZeroAngularVelocity;
        public bool ResetModifiers;
        public bool ResetImpulseQueue;
        public bool ResetPushGrace;
        public bool RebaseTrails;
        public bool Consumed;

        public BuddahPredictedTeleportEventData(
            uint eventId,
            uint eventTick,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            bool snapProgress,
            bool zeroLinearVelocity,
            bool zeroAngularVelocity,
            bool resetModifiers,
            bool resetImpulseQueue,
            bool resetPushGrace,
            bool rebaseTrails)
        {
            EventId = eventId;
            EventTick = eventTick;
            TargetPosition = targetPosition;
            TargetRotation = targetRotation;
            TargetProgress01 = targetProgress01;
            SourceType = sourceType;
            SnapProgress = snapProgress;
            ZeroLinearVelocity = zeroLinearVelocity;
            ZeroAngularVelocity = zeroAngularVelocity;
            ResetModifiers = resetModifiers;
            ResetImpulseQueue = resetImpulseQueue;
            ResetPushGrace = resetPushGrace;
            RebaseTrails = rebaseTrails;
            Consumed = false;
        }
    }
}
