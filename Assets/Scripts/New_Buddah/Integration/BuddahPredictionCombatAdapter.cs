using FishNet.Connection;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Events;
using NewBuddah.PredictionV2.Events.Payloads;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b adapter for server-authoritative impulse routing.
    /// Post-V4 (CombatRouting + LEGACY_SHADOW retired): NEW path is sole rb-writing authority.
    /// Adapter enqueues into the victim's CommandBus.ImpulseChannel (owner-local) or relays via
    /// Target_EnqueueImpulse when the victim's owner is remote.
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
        // V2b Step 1 — Q0 hybrid dedup. Per-adapter monotonic counter; stamped on each cmd
        // construction, transported through TargetRpc, used by channel as dedup key. Per-adapter
        // (not per-channel-instance) so that the same logical event has the same LogicalId across
        // server-local + RPC fire paths — the channel instances are still independent (separate
        // _recentLogicalIds), but if a future caller double-fires on ONE peer, dedup catches it.
        private uint _nextLogicalId;

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

            // Defensive IsPredictionModeActive() gate (Step 0 fix-3). A mode toggle
            // PredictionV2 -> Legacy mid-game would leave _initialized=true but the V2 victim
            // would no longer accept channel enqueues; gate prevents stale state divergence.
            BuddahPredictionBootstrap bootstrap = victimNetworkObject.GetComponent<BuddahPredictionBootstrap>();
            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
                return false;

            int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;

            // V2b Step 0 — stamp EventTick with the server's TimeManager.LocalTick. CommandBus is a
            // NetworkBehaviour so its TimeManager is wired by FishNet on spawn. The cmd carries this
            // tick across TargetRpc; the client uses cmd.EventTick verbatim for ConsumeReady gate
            // (see lessons-log L17 phase-skew: client-local stamping at RPC arrival creates per-event
            // OLD/NEW clock drift). Adapter only runs server-side, so TimeManager.LocalTick here is
            // always the canonical server tick.
            uint stampTick = _commandBus.TimeManager != null ? _commandBus.TimeManager.LocalTick : 0u;
            // V2b Step 1 — pre-increment so first cmd has LogicalId=1u (0u reserved as "unset"
            // sentinel for default-init structs that haven't gone through this path).
            uint stampLogicalId = ++_nextLogicalId;
            ImpulseCmd cmd = new ImpulseCmd(impulse, turnTorqueImpulse, (byte)sourceType, sourceObjectId, stampTick, stampLogicalId);

            // Owner-aware enqueue (Q2 answer β):
            //   - owner null / invalid / IsHost (victim's owner is the host running
            //     this server-side code) → local TryEnqueueImpulse, no RPC needed.
            //   - else (remote-owner client) → Target_EnqueueImpulse RPC to the owner.
            // RPC failures (owner disconnected, etc.) silent-drop. NEW path is sole rb-writing
            // authority post-V4 (drains channel, applies forces).
            NetworkConnection owner = victimNetworkObject.Owner;
            if (owner == null || !owner.IsValid || owner.IsHost)
            {
                return _commandBus.TryEnqueueImpulse(cmd, cmd.EventTick, cmd.LogicalId);
            }

            // V2b Step 0 fix-3: dual-emit (server-local + RPC). Server-local enqueue ensures
            // HOST's serverside replica of the remote victim observes the event in its own
            // CommandBus.ImpulseChannel. The two channel instances (host serverside vs remote
            // owner) have independent _recentLogicalIds sets, so the SAME LogicalId travels to
            // both peers without dedup collision (V2b Step 1 Q0). If a future fault double-fires
            // on the SAME peer, dedup catches it via [Channel]:DupReject. See lessons-log L18.
            _commandBus.TryEnqueueImpulse(cmd, cmd.EventTick, cmd.LogicalId);
            _commandBus.Target_EnqueueImpulse(owner, cmd);
            return true;
        }
    }
}
