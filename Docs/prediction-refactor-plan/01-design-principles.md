# 01 - Design Principles

[Back to index](index.md)

These principles are hard constraints. Any new file that violates them is rejected in review regardless of intent.

## P1 - Single Write Authority on the Tick
Only `[Replicate]` may mutate prediction-managed physics state. `PredictionRigidbody` is the sole writer to the `Rigidbody`. No file outside the motor's tick entry point may call `rb.position`, `rb.rotation`, `rb.velocity`, `rb.angularVelocity`, `rb.mass`, `rb.Sleep`, `rb.WakeUp`, or `PredictionRigidbody.Initialize` during gameplay.

## P2 - Reconcile Completeness
Every field that `[Replicate]` reads must exist in `BuddahPredictedReconcileData` and be restored inside `[Reconcile]`. This includes event queue cursors, modifier stacks, push grace timers, handoff flags, and next-allocation ids. If a state influences motion and is not in reconcile data, it does not exist.

## P3 - Tick-Aligned External Input
External systems (skills, combat, intro, respawn, gate, input) submit commands via a CommandBus that stamps each command with `(eventId, eventTick)` and defers the actual effect to the next `[Replicate]` on the owner. Immediate-effect writes are forbidden.

## P4 - Owner-Only Input Construction
`CreateReplicateData` returns `default` when `!IsOwner`. Non-owner throttle stays at default. The server and spectators reach authoritative state through reconcile, not through simulated input.

## P5 - Replicate Must Be Replay-Pure
`[Replicate]` must be deterministic given `(ReplicateData, ReconcileData, config)`. No `Debug.Log`, no transform allocations, no `GetComponent`, no bridge lookups, no skill-executor side effects, no trail notifications, no Unity events. All I/O deferred to `OnPostTick` or `OnPostReconcile`.

## P6 - Visual Read-Only
Every file under `Visual/` or `Presentation/` reads `Rigidbody`/`Transform` but never writes them in ways that change gameplay. The visual root may interpolate its own local transform. It must not run gameplay stabilization logic that alters the predicted body or non-owner simulation.

## P7 - Mode Isolation
Legacy movement and prediction movement are never simultaneously enabled. Mode switching goes through one authoritative path that also resets reconcile snapshots and clears queues.

## P8 - Symmetry on All Peers
Owner, server, and spectator run the same `[Replicate]` / `[Reconcile]` pipeline with identical data contracts. Spectators are not a second-class path patched with ad hoc transforms.

## P9 - Logs Off the Hot Path
Any `Debug.Log` in prediction code sits behind a debug flag and never inside `[Replicate]`. Use `OnPostTick` or `OnPostReconcile` aggregators for debug snapshots.

## P10 - One Owner per Data
Each piece of state has one declared owner file. No shadow copies in bridges or bootstrap.

See [02-directory-structure.md](02-directory-structure.md) for how these principles shape the new layout.
