using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace BuddahGo.Match
{
    // Serialized on the racer prefab. Connections remain exclusively human-owned.
    [DisallowMultipleComponent]
    public sealed class RacerIdentity : NetworkBehaviour
    {
        private readonly SyncVar<int> _id = new SyncVar<int>(-1);
        private readonly SyncVar<string> _displayName = new SyncVar<string>();
        // Practice training dummy (README A1): same prefab, ownerless, id stays -1 so it is never a racer
        // (no RacerDirectory, leaderboard, finish, results, catch-up gap or curtain). Skill targeting that scans
        // SkillExecutors still finds it; check this flag where a skill must treat it differently.
        private readonly SyncVar<bool> _isTrainingDummy = new SyncVar<bool>(false);
        public bool IsAssigned => _id.Value >= 0;
        public bool IsAI => RacerId.IsAIValue(_id.Value);
        public bool IsTrainingDummy => _isTrainingDummy.Value;
        public RacerId Id => RacerId.FromValue(_id.Value);
        public string DisplayName => _displayName.Value;
        public int ClientId => IsAI ? -1 : _id.Value;
        public void AssignBeforeSpawn(RacerId id, string displayName)
        {
            if (GetComponent<NetworkObject>().IsSpawned) throw new InvalidOperationException("Assign racer identity before spawn.");
            if (IsTrainingDummy) throw new InvalidOperationException("A training dummy cannot become a racer.");
            _id.Value = id.Value;
            _displayName.Value = string.IsNullOrWhiteSpace(displayName)
                ? (id.IsAI ? DesignTableOverlay.Text("racer.ai_fallback_prefix", "AI ") + (id.AIIndex + 1) : DesignTableOverlay.Text("racer.human_fallback", "Player"))
                : displayName.Trim();
        }
        // Marks an unspawned, ownerless prefab instance as a training dummy. The racer id stays unassigned (-1).
        public void AssignTrainingDummyBeforeSpawn(string displayName)
        {
            if (GetComponent<NetworkObject>().IsSpawned) throw new InvalidOperationException("Assign training dummy identity before spawn.");
            if (IsAssigned) throw new InvalidOperationException("A racer cannot become a training dummy.");
            _isTrainingDummy.Value = true;
            _displayName.Value = string.IsNullOrWhiteSpace(displayName) ? "Training Dummy" : displayName.Trim();
        }
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (IsAssigned) RacerDirectory.Current?.Register(this);
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!IsAI) return;
            foreach (var provider in GetComponents<MonoBehaviour>())
                if (provider is ISteeringOverride) provider.enabled = true;
        }
        public override void OnStopNetwork()
        {
            RacerDirectory.Current?.Unregister(this);
            base.OnStopNetwork();
        }
    }
}
