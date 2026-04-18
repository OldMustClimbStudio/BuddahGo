# FishNet — Prediction

[Back to index](fishnet-architecture.md)

## How It Works

CSP runs owner inputs locally and on the server each tick. Server sends `ReconcileData` back; client rolls back and replays if states diverge.

| Annotation | Hook | Purpose |
|---|---|---|
| `[Replicate]` | `OnTick` | Execute one tick of movement from a `ReplicateData` packet |
| `[Reconcile]` | `OnPostTick` | Roll back to server snapshot, replay queued inputs |
| `CreateReconcile()` | `OnPostTick` | Build and dispatch `ReconcileData` — called on every peer |

`PredictionRigidbody` wraps the raw `Rigidbody`. All force calls go through it; `.Simulate()` commits forces at end of `[Replicate]`.

`State Forwarding` broadcasts replicate + reconcile to spectators. Disabled = spectators need `NetworkTransform` or custom sync.

## MUST

- Call `CreateReconcile()` every `OnPostTick` on every peer, including non-owners.
- Include in `ReconcileData` every field `[Replicate]` reads (stamina, ground flag, velocity, etc.).
- Use `PredictionRigidbody` for all forces — never raw `Rigidbody.AddForce`.
- Gather single-frame keys (`GetKeyDown`) in `Update`; consume in the next `OnTick`.
- Return `default` in `CreateReplicateData()` when `!base.IsOwner`.
- Disable `CharacterController`, set position in `[Reconcile]`, then re-enable.
- Keep `GraphicalObject` as a child of the predicted root.

## MUST NOT

- Store state that affects movement outside `[Replicate]` unless it is also in `ReconcileData`.
- Call `Rigidbody.AddForce` directly on a predicted object.
- Skip `CreateReconcile()` on client or non-owner paths.
- Predict future ticks (`IsFuture()`) without a bounded window or velocity-zero fallback.
- Read `base.IsOwner` inside `OnStartServer` for clientHost — use `base.Owner.IsLocalClient`.

## Common Failure Cases

| Symptom | Cause |
|---|---|
| Constant jitter / snap | State variable not in `ReconcileData`; diverges each reconcile |
| Desync after sprint or jump | Stamina / cooldown / ground-flag missing from `ReconcileData` |
| Camera jitter | `GraphicalObject` not a child of predicted root |
| Spectator rubber-band | State Forwarding off, no alternate sync provided |
| CharacterController teleport on reconcile | Forgot disable/enable around position set |
| Physics pass-through on pause | `RigidbodyPauser.Pause()` makes body kinematic; prefer zeroing velocity |
