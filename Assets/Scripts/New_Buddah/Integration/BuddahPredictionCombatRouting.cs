using System;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    public static class BuddahPredictionCombatRouting
    {
        public static bool TryRouteImpulse(
            NetworkObject victimNetworkObject,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            NetworkObject sourceObject)
        {
            // Early-null exit: nothing to relay (per V2a Q4 patch — keep this as
            // an early return; no fan-out for null victim).
            if (victimNetworkObject == null)
                return false;

            BuddahPredictionBootstrap bootstrap = victimNetworkObject.GetComponent<BuddahPredictionBootstrap>();
            BuddahPredictedMotor predictedMotor = victimNetworkObject.GetComponent<BuddahPredictedMotor>();
            int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;

            // OLD authoritative path. Single-exit refactor (V2a Q4): two
            // value-bearing branches converge into oldResult, then fan-out, then
            // return oldResult. Fan-out's return is discarded — OLD path is the
            // authority during V2a; V2b flips this.
            bool oldResult;
            if (bootstrap != null && predictedMotor != null && bootstrap.IsPredictionModeActive())
            {
                oldResult = predictedMotor.TryApplyServerAuthoritativeImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
            }
            else
            {
                BuddahPredictionPushTargetBox pushTargetBox = victimNetworkObject.GetComponent<BuddahPredictionPushTargetBox>();
                if (pushTargetBox != null)
                    oldResult = pushTargetBox.TryApplyServerImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
                else
                    oldResult = false;
            }

#if BUDDAH_PREDICTION_LEGACY_SHADOW
            // Phase 4b V2a fan-out: also enqueue onto the victim's CombatAdapter
            // → CommandBus.ImpulseChannel for the inverted-shadow drain.
            // try/catch swallows any throw so a NEW-side bug cannot poison OLD's
            // return value. Fan-out only fires for victims with a Bootstrap
            // (PredictionV2-active victims); PushTargetBox-only victims are
            // observed only by the OLD path. Mirrors L10 dual-branch coverage:
            // shadow only sees the predicted-queue branch.
            if (bootstrap != null)
            {
                try
                {
                    bootstrap.CombatAdapter?.TryRouteImpulse(victimNetworkObject, impulse, turnTorqueImpulse, sourceType, sourceObject);
                }
                catch (Exception fanoutEx)
                {
                    Debug.LogError($"[D-IMP LEG FATAL] CombatAdapter fan-out threw: {fanoutEx.GetType().Name}: {fanoutEx.Message}");
                }
            }
#endif

            return oldResult;
        }
    }
}
