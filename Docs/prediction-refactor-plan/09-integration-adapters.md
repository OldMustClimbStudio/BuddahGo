# 09 - Integration Adapters

[Back to index](index.md)

Adapters replace the current `Integration/` folder. Each adapter has one responsibility, one channel, and no direct motor reference.

## Shared Contract

Every adapter:
- Holds a reference to `BuddahPredictionBootstrap` only (not motor).
- Resolves `BuddahPredictionCommandBus` lazily once per `OnStartNetwork`.
- Returns `false` cleanly when prediction mode is inactive.
- Never reads/writes `Rigidbody` or `Transform` directly.

## BuddahPredictionSkillAdapter

Replaces `BuddahPredictionSkillMovementBridge` and `BuddahPredictionModifierBridge`.

```csharp
bool TryApplyAcceleration(float magnitude, float duration);
bool TryApplyRootThenAcceleration(float rootDuration, float accelMagnitude, float accelDuration);
bool TryApplyInvertTurn(float duration);
bool TryApplyScale(float scale, float duration);
```

Internally maps to `ModifierCmd` with `(Kind, Magnitude, Duration, StackPolicy)`. SkillExecutor's `.Server` and `.Target` RPC paths converge here so there is a single write site per-victim-per-tick.

## BuddahPredictionCombatAdapter

Replaces `BuddahPredictionCombatRouting` (the static class is deleted).

```csharp
bool TryRouteImpulse(NetworkObject victim, Vector3 linear, float turn,
                    BuddahPredictedImpulseSourceType source, NetworkObject sourceObject);
```

Implementation:
1. Server resolves victim's `BuddahPredictionCommandBus`.
2. Emits `Target_EnqueueImpulse` to victim's owner.
3. Also mirrors locally into server channel (same cmd) so server replicate consumes it.

If prediction mode is off, falls back to the legacy `BuddahPredictionPushTargetBox` path (kept for now; removed in migration phase 7).

## BuddahPredictionRespawnAdapter

Replaces `BuddahPredictionRespawnBridge`.

```csharp
bool TryRespawnToTrackProgress(Vector3 pos, Quaternion rot, float progress01,
                               BuddahPredictedTeleportSourceType source, string reason);
```

Emits a `TeleportCmd` with flags: `SnapProgress, ZeroLinearVelocity, ZeroAngularVelocity, ResetModifiers, ResetImpulseQueue, ResetPushGrace, RebaseTrails`. Trail rebase is a post-tick flag, not inline.

## BuddahPredictionIntroAdapter

Replaces `BuddahPredictionHandoffBridge`.

```csharp
bool TrySetIntroControlActive(bool active);
bool TrySetExternalKinematicControlActive(bool active);
bool TryBeginLaunchHandoff(LaunchHandoffSnapshot snapshot, float inherit, float blend,
                           float bypass, float suppressTurn, bool clearAngular,
                           int debugSequenceId, bool enableDebugLogs);
```

Internally emits a `HandoffCmd`. The "control active" flags become bit updates on a ModifierCmd that the motor applies on next tick via reconcile-aware state.

## BuddahPredictionGateAdapter

Replaces the gate reading in `BuddahPredictionMovementGateBridge`. Reads `ResultAreaInteractionGate.MovementAllowed` and exposes it to `BuddahPredictionInputAdapter`. No writes.

## BuddahPredictionInputAdapter

Owner-only. Samples Steering/Throttle in `Update` (so `GetKey*` single-frame keys work), caches in a thread-safe slot, and delivers to `CreateReplicateData` on the next tick build. Never calls the bus.

## Mode Check Helper

Every adapter has:

```csharp
private bool IsActive() => bootstrap != null
    && bootstrap.IsPredictionModeActive()
    && commandBus != null;
```

See [10-bootstrap-composition.md](10-bootstrap-composition.md) for how adapters are wired.

---

> **Historical note (Phase 4b V4, 2026-05-03):** This chapter references symbols
> retired during Phase 4b V4 cleanup, including `BuddahPredictionCombatRouting`,
> `BuddahPredictedImpulseEventQueue`, and the `BUDDAH_PREDICTION_LEGACY_SHADOW`
> define. The original architectural rationale captured here remains valid for
> understanding the design history; for current-state code paths, see
> [`Docs/phase-gates/active/v4-contract.md`](../phase-gates/active/v4-contract.md)
> (or `archive/v4-contract.md` post-merge) and the live router at
> [`Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs`](../../Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs).
