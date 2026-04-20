using FishNet.Object;
using UnityEngine;
using NewBuddah.PredictionV2.Core;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b adapter for server-authoritative impulse routing.
    /// V1: transparent pass-through to BuddahPredictionCombatRouting.TryRouteImpulse.
    /// V2: body will switch to CommandBus.TryEnqueueImpulse (owner) +
    ///     Target_EnqueueImpulse RPC relay (remote victim on server).
    /// L7 contract: no enqueue during Awake/OnEnable/OnStartNetwork.
    /// All enqueues gated behind Initialize() late-bind — first call only
    /// permitted after first post-spawn [Replicate] tick.
    /// </summary>
    public sealed class BuddahPredictionCombatAdapter
    {
        private bool _initialized;

        public void Initialize()
        {
            _initialized = true;
        }

        public bool TryRouteImpulse(
            NetworkObject victimNetworkObject,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            NetworkObject sourceObject)
        {
            if (!_initialized) return false;

            // V1 transparent pass-through. V2 replaces body with CommandBus path.
            return BuddahPredictionCombatRouting.TryRouteImpulse(
                victimNetworkObject, impulse, turnTorqueImpulse, sourceType, sourceObject);
        }
    }
}
