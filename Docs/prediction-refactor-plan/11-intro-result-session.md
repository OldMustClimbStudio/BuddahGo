# 11 - Intro, Result, and Session Flows

[Back to index](index.md)

These are the three race-scoped flows that currently mix scene orchestration with direct motor writes. Each is rewritten to go through the CommandBus and reconcile pipeline.

## Intro Flow

Current: `IntroSequenceManager` runs a spline on each client, then calls `HandoffBridge.TryBeginLaunchHandoff` which writes directly to motor fields mid-tick.

Refactor:

1. Server owns intro timing and spline definition.
2. For each Buddah, server publishes an authoritative `HandoffCmd` per phase (`IntroStart`, `IntroSplineActive`, `LaunchHandoff`, `LaunchBlendComplete`).
3. Server sends `Target_EnqueueHandoff` to the owner.
4. Owner's `BuddahHandoffStep` consumes the event tick-aligned.
5. During `IntroSplineActive`, motor sets `ExternalKinematicActive = true` in reconcile state. `PredictionRigidbody.SetKinematic` (or equivalent velocity-zero + gravity off) is applied through the step.
6. At `LaunchHandoff`, step seeds initial velocity via `PredictionRigidbody.Velocity` and sets `LaunchHandoffPhase = Inheriting`.
7. Timers `LaunchInheritRemaining` / `LaunchBlendRemaining` tick down in `[Replicate]`.

Result: No parallel per-client spline writes to `rb.position`. Remote intro flicker is gone because non-owner visuals are driven by the same reconcile stream the owner sees.

## Result Flow

Current: Race finish and result scene transitions call `ResultDecisionManager` which may pause motor via engine-level writes.

Refactor:

1. Server emits a `HandoffCmd` with phase `ResultPause` when the finish line is crossed.
2. Motor zero-velocities via `PredictionRigidbody.Velocity(Vector3.zero)` and sets a `ResultLocked` bit in reconcile data.
3. Input adapter reads `ResultLocked` to stop forwarding steering/throttle.
4. `ResultDecisionManager` continues to own scene transitions. It calls `BuddahPredictionMode.ApplyMode(Legacy)` on scene teardown, which clears channels and snapshot-dumps state.

## Respawn Flow

Current: `BuddahPredictionRespawnBridge.TryRespawnToTrackProgress` calls `predictedMotor.RequestAuthoritativeTeleportFromOwner` which runs inline mutations.

Refactor:

1. Caller (result presentation, respawn trigger, track reset) calls `BuddahPredictionRespawnAdapter.TryRespawnToTrackProgress(...)`.
2. Adapter constructs `TeleportCmd` with reset flags.
3. Server mirrors via `Target_EnqueueTeleport` so owner allocates the id.
4. Owner's next `[Replicate]` runs `BuddahTeleportStep`:
   - `PredictionRigidbody.MovePosition(pos)` / `.MoveRotation(rot)`
   - `.Velocity(Vector3.zero)` / `.AngularVelocity(Vector3.zero)`
   - Clears modifier stack
   - Sets push grace and suppress timers to zero
   - Sets `Progress01 = target`
5. `OnPostTick` emits a one-shot `TrailRebaseRequested` event the VFX/trail system subscribes to.
6. `OnPostReconcile` emits `SkillEffectsResetRequested` the SkillExecutor subscribes to.

## Race-Start Synchronization

The bootstrap mode switch + intro `HandoffCmd` sequence is idempotent: replays of the tick reach the same state because every effect is carried by a reconcile-stored event id.

## Interaction with Room Orchestration

- `RoomStateManager` remains the owner of scene loads.
- It calls `BuddahPredictionMode.ApplyMode(Legacy)` before scene unload and `ApplyMode(PredictionV2)` after the race scene finishes loading.
- No scene flow calls reach the motor directly.

See [12-migration-sequence.md](12-migration-sequence.md) for rollout phases.
