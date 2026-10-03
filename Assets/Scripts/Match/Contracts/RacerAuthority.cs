using FishNet.Object;

namespace BuddahGo.Match
{
    // The single answer to "does this process drive this racer?": the owning client for a human,
    // the server for an assigned AI. Human RPC ownership is unchanged; only server AI bypass that RPC.
    public static class RacerAuthority
    {
        public static bool IsProgressAuthority(NetworkBehaviour actor) => actor != null && HasLocalControl(actor.NetworkObject);
        public static bool HasLocalControl(NetworkObject racer) => racer != null && (racer.IsOwner || IsServerAI(racer));
        public static bool IsServerAI(NetworkBehaviour actor) => actor != null && IsServerAI(actor.NetworkObject);
        // An ownerless object is not enough: a disconnecting human is briefly ownerless on the server too.
        public static bool IsServerAI(NetworkObject racer) => racer != null && racer.IsServerInitialized
            && racer.TryGetComponent(out RacerIdentity identity) && identity.IsAssigned && identity.IsAI;
        public static bool TryGetId(NetworkBehaviour actor, out RacerId id)
        {
            id = default;
            if (actor == null) return false;
            if (actor.TryGetComponent(out RacerIdentity identity) && identity.IsAssigned) { id = identity.Id; return true; }
            // Legacy online prefabs may not have received a RacerIdentity assignment.
            if (!RacerId.IsHumanValue(actor.OwnerId)) return false;
            id = RacerId.FromClient(actor.OwnerId); return true;
        }
        public static bool Matches(NetworkBehaviour actor, int id) => TryGetId(actor, out var racer) && racer.Value == id;
    }
}
