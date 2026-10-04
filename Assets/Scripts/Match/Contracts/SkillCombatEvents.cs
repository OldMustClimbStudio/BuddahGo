using System;
using FishNet.Object;
using UnityEngine;

namespace BuddahGo.Match
{
    // Optional evidence hook. Detection/routing is deliberately distinct from applied motor response.
    public static class SkillCombatEvents
    {
        [Serializable]
        public struct Entry
        {
            public uint Tick;
            public string Stage, Kind;
            public int SourceId, TargetId;
            public bool Routed;
            public Vector3 Position;
        }
        public static event Action<Entry> Observed;
        public static void Record(string stage, string kind, NetworkObject source, NetworkObject target, Vector3 position, bool routed = false)
        {
            if (Observed == null) return;
            var sourceIdentity = source != null ? source.GetComponent<RacerIdentity>() : null;
            var targetIdentity = target != null ? target.GetComponent<RacerIdentity>() : null;
            Observed(new Entry { Stage = stage, Kind = kind, SourceId = sourceIdentity != null && sourceIdentity.IsAssigned ? sourceIdentity.Id.Value : -1,
                TargetId = targetIdentity != null && targetIdentity.IsAssigned ? targetIdentity.Id.Value : -1,
                Tick = FishNet.InstanceFinder.TimeManager != null ? FishNet.InstanceFinder.TimeManager.LocalTick : 0,
                Position = position, Routed = routed });
        }
    }
}
