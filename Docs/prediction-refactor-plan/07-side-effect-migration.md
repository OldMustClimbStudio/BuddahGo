# 07 - Side Effect Migration

[Back to index](index.md)

This is the per-call mapping that removes every illegal write from the current motor. Each row describes the current location of a forbidden side effect, where it goes after the refactor, and which channel/step owns it.

## Direct Rigidbody Writes

| Current location | Action | New owner |
|---|---|---|
| `BuddahPredictedMotor.cs` ~1213-1229 (teleport in [Replicate]) | move to `BuddahTeleportStep` consuming a Teleport event via `PredictionRigidbody.MovePosition() / .MoveRotation()` only | `Simulation/BuddahTeleportStep` |
| `BuddahPredictedMotor.cs` ~1277-1283 (handoff snap in [Replicate]) | move to `BuddahHandoffStep`, called via Handoff event | `Simulation/BuddahHandoffStep` |
| `rb.Sleep / WakeUp / mass=` calls | delete; replace with `PredictionRigidbody.Velocity(Vector3.zero)` and config-driven mass | n/a |
| `rb.position` / `rb.rotation` writes outside Replicate | forbidden post-refactor; only `PredictionRigidbody.MovePosition(...) / .MoveRotation(...)` inside steps | `PredictionRigidbody` |

## Bridge-Originated Writes

| Caller | Current path | New path |
|---|---|---|
| `SkillExecutor` -> `SkillMovementBridge.TryApplyAcceleration` | bridge calls motor.TryApplyModifierCommand directly | adapter calls `CommandBus.TryEnqueueModifier(ModifierCmd.Accel)` |
| `SkillExecutor` -> `TryApplyRootThenAcceleration` | dual write to modifier + rb | adapter enqueues `ModifierCmd.RootThenAccel` (single payload, parameterized phases) |
| `SkillExecutor` -> `TryApplyInvertTurn` / `TryApplyScale` | bridge call | `CommandBus.TryEnqueueModifier(...)` |
| `CombatRouting.TryRouteImpulse` | static method writes via predictedMotor.TryApplyServerAuthoritativeImpulse | `BuddahPredictionCombatAdapter` -> `CommandBus.TryEnqueueImpulse` (server side uses TargetRpc relay) |
| `RespawnBridge.TryRespawnToTrackProgress` | calls predictedMotor.RequestAuthoritativeTeleportFromOwner | `BuddahPredictionRespawnAdapter` -> `CommandBus.TryEnqueueTeleport` (server-authoritative variant) |
| `HandoffBridge.TryBeginLaunchHandoff` | calls predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner | `BuddahPredictionIntroAdapter` -> `CommandBus.TryEnqueueHandoff` |
| `HandoffBridge.TrySetIntroControlActive / SetExternalKinematicControlActive` | direct flag write on motor | bus emits ModifierCmd or HandoffCmd carrying the bit; motor reads it from ReconcileData on next tick |

## Skill-Executor Calls Inside Replicate

| Current call | Replacement |
|---|---|
| `_skillExecutor.ResetActiveSkillEffectsForOwner()` inside teleport block | move to `OnPostReconcile` event aggregator that the SkillExecutor subscribes to |
| `NotifyTeleportTrailRebases()` inside teleport block | move to `OnPostTick`; trail system reads a one-shot flag from the post-tick aggregator |
| `RoomStateManager.Instance?.ReportLocalGameplayLive(...)` inside handoff consume block | move to `OnPostTick` aggregator; a `LocalGameplayLiveSequenceId` field on the post-tick snapshot is published once per consumed sequence id |

## Reconcile-Time Engine Writes

| Current call | Replacement |
|---|---|
| `rb.isKinematic = data.ExternalKinematicControlActive` inside `[Reconcile]` (motor line 376) | drop direct write; reconcile only restores the bit into reconciled state. `OnPostReconcile` reads the restored `ExternalKinematicActive` and applies kinematic toggle through `PredictionRigidbody` (or via an isolated `BuddahKinematicApplyStep` called from post-reconcile, never inside `[Reconcile]`). |

## Visual Side Effects

| Current behavior | Refactor |
|---|---|
| `BuddahPredictionVisualRootBridge.ApplyVisualRootStabilization` overrides `visualRoot.localPosition` every LateUpdate | restrict to owner during intro only; verify with explicit role test inside method |
| Non-owner intro stabilization (current bug: `ShouldLockVisualRootDuringIntroOrPresentation` returns true before IsOwner check) | invert order: check `IsOwner` first; non-owner path returns false; remote intro flicker root cause |
| Smoother suppression toggles | gated by single `BuddahPredictionVisualMode` enum sourced from reconcile-aware control flags |

## Logging

| Current | New |
|---|---|
| `Debug.Log` inside `[Replicate]` and inside reconcile-replay paths | route through `BuddahPredictionDebugLogger` which is no-op unless `enableDebugLogs` set; never emits during reconcile replay |

See [08-visual-layer.md](08-visual-layer.md) for the visual rewrite, and [09-integration-adapters.md](09-integration-adapters.md) for adapter contracts.
