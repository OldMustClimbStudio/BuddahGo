namespace SteamMultiplayer.Network
{
    // Prediction wire-format protocol version. Bump on every serialized-field change to
    // BuddahPredictedReconcileData / BuddahPredictedInputData / [Replicate]/[Reconcile]/
    // TargetRpc payloads, and on RPC table changes (FishNet RPC ids follow declaration order).
    // Lobby host writes Version into Steam metadata; joiners compare and hard-reject mismatch.
    // See SteamLobbyManager Q1 insertion + V5 design Q1.
    //
    // History:
    //   v1 — Phase 4b V5 baseline (post-V4 wire format).
    //   v2 — Mechanics update (feat/mechanics-update, one bump for the whole wave): modifier commands
    //        Freeze=8 / EndModifier=9 / Burst=10 / MaxSpeedMultiplier=11; ModifierState gains
    //        FreezeUntilTick, BurstUntilTick/BurstDurationTicks/BurstExtraMaxSpeed,
    //        MaxSpeedPenaltyUntilTick/MaxSpeedPenaltyMultiplier and ComputedStats gains IsFrozen /
    //        IsBurstActive / IsMaxSpeedPenaltyActive (reconcile payload); new SkillExecutor TargetRpcs
    //        and ObserversRpcs, the anti-skill RPC removed, new backfire SyncVars; 缘起 (wave 2) appends
    //        SkillExecutor.SkillConsumedObserversRpc / YuanqiObserversRpc (YuanqiPresentationPayload) and
    //        SkillVfxReplicator.StopVfxAllObserversRpc, and impulse source BodyImpact=5.
    public static class PredictionProtocol
    {
        public const int Version = 2;
    }
}
