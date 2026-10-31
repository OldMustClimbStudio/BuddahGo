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
    //        and ObserversRpcs, the anti-skill RPC removed (appended back at the end of SkillExecutor on 2026-10-31 when the _anti variants returned), new backfire SyncVars; 缘起 (wave 2) appends
    //        SkillExecutor.SkillConsumedObserversRpc / YuanqiObserversRpc (YuanqiPresentationPayload) and
    //        SkillVfxReplicator.StopVfxAllObserversRpc, and impulse source BodyImpact=5; presentation
    //        interfaces (wave 3) add SkillExecutor.CastObserversRpc's surchargeStacks and serverTick
    //        arguments (and BackfireSkillUiTargetRpc's serverTick), YuanqiPresentationPayload.Direction and the appended SkillExecutor.YuanqiImpactObserversRpc
    //        (BuddahBodyImpact hits). Still v2: the wave is unreleased.
    public static class PredictionProtocol
    {
        public const int Version = 2;
    }
}
