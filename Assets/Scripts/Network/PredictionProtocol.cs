namespace SteamMultiplayer.Network
{
    // Prediction wire-format protocol version. Bump on every serialized-field change to
    // BuddahPredictedReconcileData / BuddahPredictedReplicateData / [Replicate]/[Reconcile]/
    // TargetRpc payloads. Lobby host writes Version into Steam metadata; joiners compare and
    // hard-reject mismatch. See SteamLobbyManager Q1 insertion + V5 design Q1.
    //
    // History:
    //   v1 — Phase 4b V5 baseline (post-V4 wire format).
    public static class PredictionProtocol
    {
        public const int Version = 1;
    }
}
