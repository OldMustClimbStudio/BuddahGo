using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace BuddahGo.Match
{
    // Scene-owned index; never changes connection rosters, readiness or votes.
    public sealed class RacerRegistry : MonoBehaviour, IRacerDirectory
    {
        private readonly Dictionary<RacerId, RacerIdentity> _racers = new Dictionary<RacerId, RacerIdentity>();
        public IReadOnlyCollection<RacerIdentity> All => _racers.Values;
        private void Awake() { RacerDirectory.Current = this; }
        private void OnDestroy()
        {
            _racers.Clear();
            if (ReferenceEquals(RacerDirectory.Current, this)) RacerDirectory.Current = null;
        }
        public bool TryGet(RacerId id, out RacerIdentity racer) => _racers.TryGetValue(id, out racer);
        public bool TryGetByObject(NetworkObject body, out RacerIdentity racer)
        {
            racer = body != null ? body.GetComponent<RacerIdentity>() : null;
            return racer != null && racer.IsAssigned && _racers.TryGetValue(racer.Id, out var registered) && registered == racer;
        }
        public void Register(RacerIdentity racer)
        {
            if (racer == null || !racer.IsAssigned) throw new ArgumentException("Racer must have an identity.");
            if (_racers.TryGetValue(racer.Id, out var existing) && existing != null && existing != racer)
                throw new InvalidOperationException("Duplicate RacerId " + racer.Id);
            _racers[racer.Id] = racer;
        }
        public void Unregister(RacerIdentity racer)
        {
            if (racer != null && racer.IsAssigned && _racers.TryGetValue(racer.Id, out var existing) && existing == racer)
                _racers.Remove(racer.Id);
        }
    }
}
