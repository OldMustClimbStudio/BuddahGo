using FishNet.Connection;
using FishNet.Object;
using NewBuddah.PredictionV2.Events.Payloads;
using UnityEngine;

namespace NewBuddah.PredictionV2.Events
{
    // Tick-aligned entry point for external systems that want to push effects into prediction.
    // See Docs/prediction-refactor-plan/06-command-bus.md for the routing rules and
    // Docs/prediction-refactor-plan/05-event-channel.md for channel mechanics.
    //
    // The bus lives on the Buddah prefab and owns the Impulse channel, its owner-side TryEnqueue entry
    // point, the server->owner Target_ relay and TryClearChannels. Teleport and handoff go through
    // the motor's own queues.
    //
    // Phase 4b V2b Step 0: TryEnqueue* signatures take a uint currentTick (server-canonical clock).
    // RPC handlers stamp via cmd.EventTick verbatim — the cmd carries the server's tick across the
    // wire. See agent-exchange/handoff/2026-05-02-phase4b-v2b-step0-design.md Q2 for rationale.
    //
    // Phase 4b V2b Step 1: TryEnqueue* signatures take an additional uint logicalId (Q0 dedup).
    // RPC handlers extract cmd.LogicalId and pass through. Recv log lines include logicalId for
    // wire-format verification at smoke-gate level.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionCommandBus : NetworkBehaviour
    {
        public const string LogPrefix = "[CommandBus]";
        private const int DefaultChannelCapacity = 64;
        // Sentinel LogicalId for the editor-only A4 probe path. Real adapter LogicalIds start at 1u
        // and increment per call; the probe uses a fixed sentinel so its dedup behavior is observable
        // (re-firing the probe twice in a row will [Channel]:DupReject the second one — expected).
        private const uint A4ProbeLogicalId = uint.MaxValue;

        private readonly BuddahPredictionEventChannel<ImpulseCmd> _impulse = new(DefaultChannelCapacity);

        private static bool s_firstInvokeImpulse;

        public BuddahPredictionEventChannel<ImpulseCmd> ImpulseChannel => _impulse;

        public bool TryEnqueueImpulse(in ImpulseCmd cmd, uint eventTick, uint logicalId)
        {
            if (_impulse.TryEnqueue(cmd, eventTick, logicalId, out _))
                return true;
            Debug.LogWarning($"{LogPrefix}:DropFull ch=Impulse capacity={_impulse.Capacity}", this);
            return false;
        }

        public int TryClearChannels(BuddahPredictionChannelMask mask)
        {
            int impulsePre = (mask & BuddahPredictionChannelMask.Impulse) != 0 ? _impulse.Count : 0;

            if ((mask & BuddahPredictionChannelMask.Impulse) != 0) _impulse.Clear();

            GameLog.Verbose(
                $"{LogPrefix}:ClearAll mask={mask} total={impulsePre} impulse={impulsePre}",
                this);
            return impulsePre;
        }

        // ASSUMPTION A4: TargetRpc on ClientHost fires exactly once.
        // First-invoke log below lets us catch double-fire / miss early.
        // If host observes [CommandBus:Recv] twice for one enqueue or never,
        // see Docs/prediction-refactor-plan/16-api-spike-checklist.md A4 contingency.
        //
        // V2b Step 0: cmd carries server-stamped EventTick. Handler passes it verbatim to
        // TryEnqueueImpulse — no client-local stamping (see L17 phase-skew root cause).
        // V2b Step 1: cmd also carries server-stamped LogicalId. Recv log line includes it for
        // wire-format verification (smoke gate). [TargetRpc] default channel is Reliable+Ordered
        // (FishNet 4.6.20 docs); duplicate arrivals would only happen via reconnect/retransmit
        // edge cases — Q0 dedup HashSet handles those silently.
        [TargetRpc(RunLocally = false, ExcludeServer = false)]
        public void Target_EnqueueImpulse(NetworkConnection target, ImpulseCmd cmd)
        {
            if (!s_firstInvokeImpulse)
            {
                s_firstInvokeImpulse = true;
                GameLog.Verbose($"{LogPrefix}:FirstInvoke ch=Impulse", this);
            }
            GameLog.Verbose(
                $"{LogPrefix}:Recv ch=Impulse linear={cmd.LinearImpulse} turn={cmd.TurnImpulse} srcType={cmd.SourceType} srcObj={cmd.SourceObjectId} eventTick={cmd.EventTick} logicalId={cmd.LogicalId}",
                this);
            TryEnqueueImpulse(cmd, cmd.EventTick, cmd.LogicalId);
        }

#if UNITY_EDITOR
        // Phase 2 V8 probe. On the host, right-click this component in the Inspector and pick the
        // context menu entry. Fires a single Target_EnqueueImpulse to the current Owner so you can
        // watch for A4 anomalies (double-fire or silent drop) in the console. Editor-only; this
        // method is omitted from builds. Move to a Tests/ harness in Phase 8.
        [ContextMenu("A4 Probe: Fire Test Impulse To Owner")]
        private void DebugA4FireTestImpulseToOwner()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning($"{LogPrefix}:A4Probe rejected - caller is not server", this);
                return;
            }
            if (Owner == null || !Owner.IsActive)
            {
                Debug.LogWarning($"{LogPrefix}:A4Probe rejected - no active owner on NetworkObject", this);
                return;
            }
            uint stampTick = TimeManager != null ? TimeManager.LocalTick : 0u;
            var cmd = new ImpulseCmd(
                linearImpulse: new Vector3(0f, 1f, 0f),
                turnImpulse: 0f,
                sourceType: 0,
                sourceObjectId: 0,
                eventTick: stampTick,
                logicalId: A4ProbeLogicalId);
            GameLog.Verbose($"{LogPrefix}:A4Probe fire Target_EnqueueImpulse -> ownerClientId={Owner.ClientId} eventTick={stampTick} logicalId={A4ProbeLogicalId}", this);
            Target_EnqueueImpulse(Owner, cmd);
        }
#endif
    }
}
