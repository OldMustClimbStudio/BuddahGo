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
    // Phase 2 scope: bus exists on the Buddah prefab and owns 4 channels + 4 owner-side TryEnqueue
    // entry points + 4 server->owner Target_ relays + TryClearChannels. No adapter calls into this
    // bus yet; the motor still uses old paths. Phase 3/4/5/6 cut adapters over.
    //
    // Phase 4b V2b Step 0: TryEnqueue* signatures take a uint currentTick (server-canonical clock).
    // RPC handlers stamp via cmd.EventTick verbatim — the cmd carries the server's tick across the
    // wire. See agent-exchange/handoff/2026-05-02-phase4b-v2b-step0-design.md Q2 for rationale.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionCommandBus : NetworkBehaviour
    {
        public const string LogPrefix = "[CommandBus]";
        private const int DefaultChannelCapacity = 64;

        private readonly BuddahPredictionEventChannel<ImpulseCmd> _impulse = new(DefaultChannelCapacity);
        private readonly BuddahPredictionEventChannel<TeleportCmd> _teleport = new(DefaultChannelCapacity);
        private readonly BuddahPredictionEventChannel<ModifierCmd> _modifier = new(DefaultChannelCapacity);
        private readonly BuddahPredictionEventChannel<HandoffCmd> _handoff = new(DefaultChannelCapacity);

        private static bool s_firstInvokeImpulse;
        private static bool s_firstInvokeTeleport;
        private static bool s_firstInvokeModifier;
        private static bool s_firstInvokeHandoff;

        public BuddahPredictionEventChannel<ImpulseCmd> ImpulseChannel => _impulse;
        public BuddahPredictionEventChannel<TeleportCmd> TeleportChannel => _teleport;
        public BuddahPredictionEventChannel<ModifierCmd> ModifierChannel => _modifier;
        public BuddahPredictionEventChannel<HandoffCmd> HandoffChannel => _handoff;

        public bool TryEnqueueImpulse(in ImpulseCmd cmd, uint eventTick)
        {
            if (_impulse.TryEnqueue(cmd, eventTick, out _))
                return true;
            Debug.LogWarning($"{LogPrefix}:DropFull ch=Impulse capacity={_impulse.Capacity}", this);
            return false;
        }

        public bool TryEnqueueTeleport(in TeleportCmd cmd, uint eventTick)
        {
            if (_teleport.TryEnqueue(cmd, eventTick, out _))
                return true;
            Debug.LogWarning($"{LogPrefix}:DropFull ch=Teleport capacity={_teleport.Capacity}", this);
            return false;
        }

        public bool TryEnqueueModifier(in ModifierCmd cmd, uint eventTick)
        {
            if (_modifier.TryEnqueue(cmd, eventTick, out _))
                return true;
            Debug.LogWarning($"{LogPrefix}:DropFull ch=Modifier capacity={_modifier.Capacity}", this);
            return false;
        }

        public bool TryEnqueueHandoff(in HandoffCmd cmd, uint eventTick)
        {
            if (_handoff.TryEnqueue(cmd, eventTick, out _))
                return true;
            Debug.LogWarning($"{LogPrefix}:DropFull ch=Handoff capacity={_handoff.Capacity}", this);
            return false;
        }

        public int TryClearChannels(BuddahPredictionChannelMask mask)
        {
            int impulsePre = (mask & BuddahPredictionChannelMask.Impulse) != 0 ? _impulse.Count : 0;
            int teleportPre = (mask & BuddahPredictionChannelMask.Teleport) != 0 ? _teleport.Count : 0;
            int modifierPre = (mask & BuddahPredictionChannelMask.Modifier) != 0 ? _modifier.Count : 0;
            int handoffPre = (mask & BuddahPredictionChannelMask.Handoff) != 0 ? _handoff.Count : 0;
            int total = impulsePre + teleportPre + modifierPre + handoffPre;

            if ((mask & BuddahPredictionChannelMask.Impulse) != 0) _impulse.Clear();
            if ((mask & BuddahPredictionChannelMask.Teleport) != 0) _teleport.Clear();
            if ((mask & BuddahPredictionChannelMask.Modifier) != 0) _modifier.Clear();
            if ((mask & BuddahPredictionChannelMask.Handoff) != 0) _handoff.Clear();

            Debug.Log(
                $"{LogPrefix}:ClearAll mask={mask} total={total} impulse={impulsePre} teleport={teleportPre} modifier={modifierPre} handoff={handoffPre}",
                this);
            return total;
        }

        // ASSUMPTION A4: TargetRpc on ClientHost fires exactly once.
        // First-invoke log below lets us catch double-fire / miss early.
        // If host observes [CommandBus:Recv] twice for one enqueue or never,
        // see Docs/prediction-refactor-plan/16-api-spike-checklist.md A4 contingency.
        //
        // V2b Step 0: cmd carries server-stamped EventTick. Handler passes it verbatim to
        // TryEnqueueImpulse — no client-local stamping (see L17 phase-skew root cause).
        [TargetRpc(RunLocally = false, ExcludeServer = false)]
        public void Target_EnqueueImpulse(NetworkConnection target, ImpulseCmd cmd)
        {
            if (!s_firstInvokeImpulse)
            {
                s_firstInvokeImpulse = true;
                Debug.Log($"{LogPrefix}:FirstInvoke ch=Impulse", this);
            }
            Debug.Log(
                $"{LogPrefix}:Recv ch=Impulse linear={cmd.LinearImpulse} turn={cmd.TurnImpulse} srcType={cmd.SourceType} srcObj={cmd.SourceObjectId} eventTick={cmd.EventTick}",
                this);
            TryEnqueueImpulse(cmd, cmd.EventTick);
        }

        // ASSUMPTION A4: TargetRpc on ClientHost fires exactly once. See comment above Target_EnqueueImpulse.
        [TargetRpc(RunLocally = false, ExcludeServer = false)]
        public void Target_EnqueueTeleport(NetworkConnection target, TeleportCmd cmd)
        {
            if (!s_firstInvokeTeleport)
            {
                s_firstInvokeTeleport = true;
                Debug.Log($"{LogPrefix}:FirstInvoke ch=Teleport", this);
            }
            Debug.Log(
                $"{LogPrefix}:Recv ch=Teleport pos={cmd.Pos} rot={cmd.Rot.eulerAngles} prog={cmd.Progress01:F3} src={cmd.Source} flags={cmd.Flags} eventTick={cmd.EventTick}",
                this);
            TryEnqueueTeleport(cmd, cmd.EventTick);
        }

        // ASSUMPTION A4: TargetRpc on ClientHost fires exactly once. See comment above Target_EnqueueImpulse.
        [TargetRpc(RunLocally = false, ExcludeServer = false)]
        public void Target_EnqueueModifier(NetworkConnection target, ModifierCmd cmd)
        {
            if (!s_firstInvokeModifier)
            {
                s_firstInvokeModifier = true;
                Debug.Log($"{LogPrefix}:FirstInvoke ch=Modifier", this);
            }
            Debug.Log(
                $"{LogPrefix}:Recv ch=Modifier kind={cmd.Kind} mag={cmd.Magnitude:F3} dur={cmd.Duration:F3} stack={cmd.StackPolicy} eventTick={cmd.EventTick}",
                this);
            TryEnqueueModifier(cmd, cmd.EventTick);
        }

        // ASSUMPTION A4: TargetRpc on ClientHost fires exactly once. See comment above Target_EnqueueImpulse.
        [TargetRpc(RunLocally = false, ExcludeServer = false)]
        public void Target_EnqueueHandoff(NetworkConnection target, HandoffCmd cmd)
        {
            if (!s_firstInvokeHandoff)
            {
                s_firstInvokeHandoff = true;
                Debug.Log($"{LogPrefix}:FirstInvoke ch=Handoff", this);
            }
            Debug.Log(
                $"{LogPrefix}:Recv ch=Handoff pos={cmd.SnapshotPosition} inherit={cmd.Inherit:F3} blend={cmd.Blend:F3} bypass={cmd.Bypass:F3} suppressTurn={cmd.SuppressTurn:F3} flags={cmd.Flags} eventTick={cmd.EventTick}",
                this);
            TryEnqueueHandoff(cmd, cmd.EventTick);
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
                eventTick: stampTick);
            Debug.Log($"{LogPrefix}:A4Probe fire Target_EnqueueImpulse -> ownerClientId={Owner.ClientId} eventTick={stampTick}", this);
            Target_EnqueueImpulse(Owner, cmd);
        }
#endif
    }
}
