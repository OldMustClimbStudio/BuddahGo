// SANDBOX - throwaway file for FishNet PredictionRigidbody API verification.
// Delete after Docs/prediction-refactor-plan/16-api-spike-checklist.md is filled in.
//
// Usage:
//   1. Create an empty prefab in a sandbox scene.
//   2. Add NetworkObject + Rigidbody + this component.
//   3. Enter Play as host, then connect a second Steam/FishyFacepunch client.
//   4. Press number keys 1..6 on the host to trigger Q1..Q7 probes.
//   5. Observe console on both host and remote; copy output into the checklist.

using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

namespace NewBuddah.Sandbox
{
    [RequireComponent(typeof(Rigidbody))]
    public class PredictionApiSpike : NetworkBehaviour
    {
        private Rigidbody _rb;
        private PredictionRigidbody _pr;
        private int _rpcSelfFireCount;
        private uint _lastPostReconcileTick;
        private Vector3 _lastReplicateEntryPos;
        private float _reconcileForcedMassCheck = -1f;

        private readonly struct TestReplicateData : IReplicateData
        {
            public readonly byte Op; // 1=teleport-probe, 2=idle, 3=mass-probe
            public readonly Vector3 TeleportTo;
            public readonly float MassOverride;
            private readonly uint _tick;
            public TestReplicateData(byte op, Vector3 tp, float mass) { Op = op; TeleportTo = tp; MassOverride = mass; _tick = 0; }
            public uint GetTick() => _tick;
            public void SetTick(uint v) { /* wire-only */ }
            public void Dispose() { }
        }

        private readonly struct TestReconcileData : IReconcileData
        {
            public readonly PredictionRigidbody State;
            public readonly float SampledMass;
            private readonly uint _tick;
            public TestReconcileData(PredictionRigidbody s, float mass) { State = s; SampledMass = mass; _tick = 0; }
            public uint GetTick() => _tick;
            public void SetTick(uint v) { }
            public void Dispose() { }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _pr = new PredictionRigidbody();
            _pr.Initialize(_rb);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            TimeManager.OnTick += TM_OnTick;
            TimeManager.OnPostTick += TM_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            TimeManager.OnTick -= TM_OnTick;
            TimeManager.OnPostTick -= TM_OnPostTick;
        }

        private byte _pendingOp = 2;
        private Vector3 _pendingTp;
        private float _pendingMass = -1f;

        private void Update()
        {
            if (!IsOwner) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { _pendingOp = 1; _pendingTp = transform.position + Vector3.right * 5f; Debug.Log("[Spike] Q1 teleport probe queued"); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { Debug.Log("[Spike] Q3 probe: calling Initialize mid-life"); _pr.Initialize(_rb); }
            if (Input.GetKeyDown(KeyCode.Alpha5)) { Debug.Log("[Spike] Q5 target-rpc self-fire"); Target_SpikeEcho(Owner, ++_rpcSelfFireCount); }
            if (Input.GetKeyDown(KeyCode.Alpha7)) { _pendingOp = 3; _pendingMass = _rb.mass + 1f; Debug.Log($"[Spike] Q7 mass probe queued new={_pendingMass}"); }
        }

        private void TM_OnTick()
        {
            if (IsOwner)
                RunReplicate(BuildData(), default);
            else
                RunReplicate(default, default);
        }

        private TestReplicateData BuildData()
        {
            byte op = _pendingOp; Vector3 tp = _pendingTp; float m = _pendingMass;
            _pendingOp = 2; _pendingMass = -1f;
            return new TestReplicateData(op, tp, m);
        }

        [Replicate]
        private void RunReplicate(TestReplicateData d, ReplicateState s = ReplicateState.Invalid, Channel c = Channel.Unreliable)
        {
            _lastReplicateEntryPos = _rb.position; // Q6: check if same across replay
            Debug.Log($"[Spike][Rep] tick={TimeManager.LocalTick} state={s} op={d.Op} entryPos={_lastReplicateEntryPos} isOwner={IsOwner}");
            if (d.Op == 1) { _pr.Velocity(Vector3.zero); _pr.AngularVelocity(Vector3.zero); _pr.MovePosition(d.TeleportTo); /* Q1 */ }
            if (d.Op == 3) { _rb.mass = d.MassOverride; /* Q7 */ }
            _pr.Simulate();
        }

        private void TM_OnPostTick()
        {
            if (!IsServerInitialized) return;
            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            var data = new TestReconcileData(_pr, _rb.mass);
            Reconcile(data);
        }

        [Reconcile]
        private void Reconcile(TestReconcileData d, Channel c = Channel.Unreliable)
        {
            _pr.Reconcile(d.State);
            _reconcileForcedMassCheck = d.SampledMass;
            _lastPostReconcileTick = d.GetTick();
            Debug.Log($"[Spike][Rec] tick={d.GetTick()} sampledMass={d.SampledMass} postPos={_rb.position} isOwner={IsOwner} isServer={IsServerInitialized}");
        }

        [TargetRpc(ExcludeServer = false, RunLocally = true)]
        private void Target_SpikeEcho(NetworkConnection c, int seq)
        {
            Debug.Log($"[Spike][TargetRpc] seq={seq} isOwner={IsOwner} isServer={IsServerInitialized} isHost={IsHostInitialized}");
        }
    }
}
