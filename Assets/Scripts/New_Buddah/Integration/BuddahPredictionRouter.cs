using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b V3/V4 — single static dispatch entry point for skill -> victim impulse routing.
    ///
    /// Three-tier dispatch (per V3 recon §3 victim topology):
    ///   1. V2 prediction Buddah  -> bootstrap.CombatAdapter.TryRouteImpulse (channel enqueue +
    ///                               LogicalId stamp + RPC; rb-writing authority since V2b Step 1).
    ///   2. PushTargetBox debug   -> pushTargetBox.TryApplyServerImpulse (RaceMap-only dev objects).
    ///   3. Legacy Buddah         -> BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc (mode toggle off
    ///                               OR adapter null).
    ///
    /// Defense-in-depth: this router pre-validates IsPredictionModeActive, AND
    /// BuddahPredictionCombatAdapter.TryRouteImpulse re-validates internally (Step 0 fix-3).
    /// Both gates intentional — DO NOT remove adapter's gate as redundant; future callers that
    /// bypass router still need the adapter floor.
    ///
    /// Malformed-V2 graceful fallback: if a buddah has Bootstrap + IsPredictionModeActive=true but
    /// adapter is null, the V2 branch falls through to Tier 2/3. Intentional defensive behavior.
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
                if (bootstrap.CombatAdapter != null)
                {
                    // NEW-path (post-V2b-Step-1 rb-writing authority): channel enqueue + LogicalId stamp + RPC.
                    // Phase 4b V4: motor reference + OLD-feed call retired alongside LEGACY_SHADOW define.
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
