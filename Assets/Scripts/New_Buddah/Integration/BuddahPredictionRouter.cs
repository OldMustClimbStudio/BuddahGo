using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b V3 — single static dispatch entry point for skill -> victim impulse routing.
    /// Replaces the 4 skill callsite uses of <see cref="BuddahPredictionCombatRouting.TryRouteImpulse"/>
    /// and the per-callsite Tier-3 <c>BuddahMovement.ApplyPushImpulse{,AndTorque}TargetRpc</c> fallback.
    ///
    /// Three-tier dispatch (per V3 recon §3 victim topology):
    ///   1. V2 prediction Buddah  -> motor.TryApplyServerAuthoritativeImpulse (OLD-feed for
    ///                               _legacyShadowScratch continuity through V4) + bootstrap.CombatAdapter.TryRouteImpulse
    ///                               (NEW-feed, post-V2b-Step-1 rb-writing authority).
    ///   2. PushTargetBox debug   -> pushTargetBox.TryApplyServerImpulse (RaceMap-only dev objects).
    ///   3. Legacy Buddah         -> BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc (mode toggle off
    ///                               OR motor missing OR adapter null).
    ///
    /// Defense-in-depth: this router pre-validates IsPredictionModeActive, AND
    /// BuddahPredictionCombatAdapter.TryRouteImpulse re-validates internally (Step 0 fix-3).
    /// Both gates intentional — DO NOT remove adapter's gate as redundant; future callers that
    /// bypass router still need the adapter floor.
    ///
    /// Malformed-V2 graceful fallback: if a buddah has Bootstrap + IsPredictionModeActive=true but
    /// motor or adapter is null, the V2 branch falls through to Tier 2/3. Effect: degraded fallback
    /// to BuddahMovement RPC if BuddahMovement is also present. Intentional defensive behavior.
    ///
    /// V4 retirement plan: strip the OLD-feed line under #if BUDDAH_PREDICTION_LEGACY_SHADOW,
    /// retire the define, delete BuddahPredictionCombatRouting + the OLD path in motor, and
    /// reconsider whether this router is still needed (or can collapse into a single line at
    /// each skill callsite once Tier-3 legacy buddah RPCs are dropped).
    /// </summary>
    public static class BuddahPredictionRouter
    {
        public static bool RouteImpulse(
            NetworkObject victimNetworkObject,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            NetworkObject sourceObject)
        {
            if (victimNetworkObject == null)
                return false;

            // Tier 1 — V2 prediction Buddah.
            BuddahPredictionBootstrap bootstrap = victimNetworkObject.GetComponent<BuddahPredictionBootstrap>();
            if (bootstrap != null && bootstrap.IsPredictionModeActive())
            {
                BuddahPredictedMotor motor = victimNetworkObject.GetComponent<BuddahPredictedMotor>();
                if (motor != null && bootstrap.CombatAdapter != null)
                {
                    int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;
#if BUDDAH_PREDICTION_LEGACY_SHADOW
                    // OLD-feed for _legacyShadowScratch continuity through V4 retirement of LEGACY_SHADOW define.
                    motor.TryApplyServerAuthoritativeImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
#endif
                    // NEW-feed (post-V2b-Step-1 rb-writing authority): channel enqueue + LogicalId stamp + RPC.
                    return bootstrap.CombatAdapter.TryRouteImpulse(victimNetworkObject, impulse, turnTorqueImpulse, sourceType, sourceObject);
                }
                // Malformed V2 (bootstrap + mode active but motor/adapter null) -> fall through to Tier 2/3.
            }

            // Tier 2 — PushTargetBox debug target (RaceMap-only dev objects).
            BuddahPredictionPushTargetBox pushTargetBox = victimNetworkObject.GetComponent<BuddahPredictionPushTargetBox>();
            if (pushTargetBox != null)
            {
                int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;
                return pushTargetBox.TryApplyServerImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
            }

            // Tier 3 — Legacy buddah path. Single RPC variant covers both no-torque (turn=0f equivalent)
            // and with-torque cases via ApplyPushAndTorqueLocal -> matches BuddahMovement behavior pre-V3.
            BuddahMovement victimMove = victimNetworkObject.GetComponent<BuddahMovement>();
            if (victimMove != null)
            {
                victimMove.ApplyPushImpulseAndTorqueTargetRpc(victimNetworkObject.Owner, impulse, turnTorqueImpulse);
                return true;
            }

            // No handler for this victim type. Silent no-op matches today's CombatRouting +
            // skill-callsite double-fallback no-match behavior.
            return false;
        }
    }
}
