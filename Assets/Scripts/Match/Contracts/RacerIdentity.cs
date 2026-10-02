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
        public bool IsAssigned => _id.Value >= 0;
        public bool IsAI => _id.Value >= 10000;
        public RacerId Id => IsAI ? RacerId.ForAI(_id.Value - 10000) : RacerId.FromClient(_id.Value);
        public string DisplayName => _displayName.Value;
        public int ClientId => IsAI ? -1 : _id.Value;
        public void AssignBeforeSpawn(RacerId id, string displayName)
        {
            if (GetComponent<NetworkObject>().IsSpawned) throw new InvalidOperationException("Assign racer identity before spawn.");
            _id.Value = id.Value;
            _displayName.Value = string.IsNullOrWhiteSpace(displayName) ? (id.IsAI ? "AI " + (id.Value - 9999) : "Player") : displayName.Trim();
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
