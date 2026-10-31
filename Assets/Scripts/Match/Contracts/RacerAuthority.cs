using FishNet.Object;

namespace BuddahGo.Match
{
    // The single answer to "does this process drive this racer?": the owning client for a human,
    // the server for an assigned AI. Human RPC ownership is unchanged; only server AI bypass that RPC.
    // A Practice training dummy is driven (parked) by the server too, but it is not a racer: it shares the
    // movement/physics/reset authority of server AI (HasLocalControl, IsServerControlled) and never the
    // race-progress authority (IsProgressAuthority), so no lap, timing, wrong-way or leaderboard logic runs for it.
    public static class RacerAuthority
    {
        // Laps, checkpoints, timing, progress reports and wrong-way correction: the owner or server AI only.
        public static bool IsProgressAuthority(NetworkBehaviour actor) => actor != null && IsProgressAuthority(actor.NetworkObject);
        public static bool IsProgressAuthority(NetworkObject racer) => racer != null && (racer.IsOwner || IsServerAI(racer));
        // Motor input writer, body physics (non-kinematic), presentation timeline and server-side resets.
        public static bool HasLocalControl(NetworkObject racer) => racer != null && (racer.IsOwner || IsServerControlled(racer));
        public static bool IsServerAI(NetworkBehaviour actor) => actor != null && IsServerAI(actor.NetworkObject);
        // An ownerless object is not enough: a disconnecting human is briefly ownerless on the server too.
        public static bool IsServerAI(NetworkObject racer) => racer != null && racer.IsServerInitialized
            && racer.TryGetComponent(out RacerIdentity identity) && identity.IsAssigned && identity.IsAI;
        public static bool IsServerDummy(NetworkBehaviour actor) => actor != null && IsServerDummy(actor.NetworkObject);
        // The dummy flag is set only before spawn on an ownerless object whose racer id stays unassigned.
        public static bool IsServerDummy(NetworkObject racer) => racer != null && racer.IsServerInitialized
            && (racer.Owner == null || !racer.Owner.IsValid)
            && racer.TryGetComponent(out RacerIdentity identity) && identity.IsTrainingDummy && !identity.IsAssigned;
        // Server AI or a server training dummy: everything the server runs locally because no owner connection exists.
        public static bool IsServerControlled(NetworkBehaviour actor) => actor != null && IsServerControlled(actor.NetworkObject);
        public static bool IsServerControlled(NetworkObject racer) => IsServerAI(racer) || IsServerDummy(racer);
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
