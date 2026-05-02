using FishNet.Connection;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Events;
using NewBuddah.PredictionV2.Events.Payloads;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b adapter for server-authoritative impulse routing.
    /// V1: empty pass-through scaffold + Initialize() one-shot latch.
    /// V2a: dual-feed inverted shadow. Adapter enqueues into the victim's
    /// CommandBus.ImpulseChannel (owner-local) or relays via Target_EnqueueImpulse
    /// when the victim's owner is remote. The motor drains this channel as an
    /// observation-only inverted shadow under BUDDAH_PREDICTION_LEGACY_SHADOW.
    /// OLD CombatRouting path is still authority — adapter return value is
    /// discarded by the fan-out site (see BuddahPredictionCombatRouting).
    /// V2b will flip authority. V4 deletes CombatRouting + the legacy pre-guard.
    ///
    /// L7 contract (lessons-log): no enqueue during Awake/OnEnable/OnStartNetwork.
    /// Initialize(bus) wires the bus reference (called from Bootstrap.Awake).
    /// MarkReady() flips _initialized → true (called from motor's first
    /// post-spawn [Replicate] tick, AFTER BuddahMovementModeSwitcher's double
    /// ApplyMode and its 4 [CommandBus]:ClearAll lines have all landed).
    /// Until MarkReady runs, TryRouteImpulse returns false without touching
    /// the bus.
    /// </summary>
    public sealed class BuddahPredictionCombatAdapter
    {
        private BuddahPredictionCommandBus _commandBus;
        private bool _initialized;

        public bool IsReady => _initialized;

        public void Initialize(BuddahPredictionCommandBus commandBus)
        {
            _commandBus = commandBus;
        }

        public void MarkReady()
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
            if (!_initialized || _commandBus == null || victimNetworkObject == null)
                return false;

            int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;
            ImpulseCmd cmd = new ImpulseCmd(impulse, turnTorqueImpulse, (byte)sourceType, sourceObjectId);

            // Owner-aware enqueue (Q2 answer β):
            //   - owner null / invalid / IsHost (victim's owner is the host running
            //     this server-side code) → local TryEnqueueImpulse, no RPC needed.
            //   - else (remote-owner client) → Target_EnqueueImpulse RPC to the owner.
            // RPC failures (owner disconnected, etc.) silent-drop. No FATAL — the
            // OLD CombatRouting path is authority and is unaffected.
            NetworkConnection owner = victimNetworkObject.Owner;
            if (owner == null || !owner.IsValid || owner.IsHost)
            {
                return _commandBus.TryEnqueueImpulse(cmd);
            }

            _commandBus.Target_EnqueueImpulse(owner, cmd);
            return true;
        }
    }
}
