# 04 - Tick Pipeline

[Back to index](index.md)

The tick pipeline is the same on owner, server, and spectator. Differences are limited to which inputs they consume and which channels they own.

## Owner Path (per FixedUpdate / TimeManager OnTick)

1. `OnTick` fires on motor.
2. `BuddahPredictionInputAdapter` samples Steering/Throttle/MovementAllowed (if `IsOwner`).
3. `BuddahPredictionCommandBus` snapshots `LastConsumedXxxId` for each channel.
4. `CreateReplicateData()` builds `BuddahPredictedInputData`. **First line of the method short-circuits with `return default;` when `!IsOwner`** - this is a Phase 1 hard requirement (current `BuildReplicateData` lacks this guard).
5. `[Replicate]` runs steps from `Simulation/` in fixed order:
   - `BuddahHandoffStep` (apply intro / launch / kinematic transitions through `PredictionRigidbody`)
   - `BuddahTeleportStep` (consume teleport events <= `LastConsumedTeleportId`)
   - `BuddahImpulseStep` (consume impulse events <= `LastConsumedImpulseId`)
   - `BuddahModifierStep` (consume modifier events, advance timers)
   - `BuddahLocomotionStep` (compute forward force / turn torque, clamp by MaxSpeed + push grace)
   - `PredictionRigidbody.Simulate()` is called exactly once at the end
6. `OnPostTick`:
   - `CreateReconcile()` builds full `BuddahPredictedReconcileData`
   - Debug snapshot written
   - Trail rebases / one-shot side effects deferred from `[Replicate]` are flushed here

## Server Path
Same as owner path but with two differences:

- Step 2 is replaced by reading the input that arrived from the owner.
- After step 5, the server is the canonical source for reconcile.

## Spectator Path
Replicate runs with `default` inputs (since `!IsOwner`). The spectator follows reconcile snapshots. State Forwarding stays ON so spectators receive both replicate and reconcile from server.

## Reconcile

`[Reconcile]` restores:

1. `RbState` via `PredictionRigidbody.Reconcile`
2. Modifier stack
3. Event queue cursors (drains pending entries newer than restored cursors back to a re-replay queue)
4. Handoff phase + timers
5. Push grace + suppress timers
6. Progress01

After reconcile, FishNet replays queued replicate ticks. Because every consumed-event-id is stored in `BuddahPredictedReconcileData`, replays consume the same events in the same order, producing the same final state. This is the core fix for the jitter.

## Forbidden Activities Inside `[Replicate]`

| Activity | Why | Replacement |
|---|---|---|
| `Debug.Log` | breaks replay-pure assumption performance | OnPostTick aggregator |
| `GetComponent` | allocation + lookup cost | wired in `Bootstrap` |
| Direct `rb.position` / `rb.velocity` | bypasses `PredictionRigidbody` | `PredictionRigidbody` API only |
| `Initialize(rb)` mid-tick | resets engine state | only at bootstrap or post-tick teleport |
| `_skillExecutor.ResetActiveSkillEffectsForOwner()` | external side effect | OnPostReconcile only |
| `NotifyTeleportTrailRebases()` | calls into Visual | OnPostTick deferred queue |
| `rb.Sleep / WakeUp / mass = ...` | mutates engine config | not allowed; remove entirely |

See [05-event-channel.md](05-event-channel.md) for how external commands become tick-aligned events without touching the motor mid-tick.
