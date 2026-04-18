# 16 - FishNet API Assumptions (Spike Deferred)

[Back to index](index.md)

The project has an integrated test pipeline that makes a one-off spike costly to set up. The spike path is therefore **skipped**. Each FishNet API assumption below is trusted based on FishNet 4.6.20 official documentation and source inspection. When implementation reaches the corresponding code site, the implementing agent must insert a sentinel log + explicit risk comment so that any real-world failure is caught early, without forcing a rollback of the overall plan direction.

## Skip Rule

- Do NOT change plan direction or top-level architecture when an assumption fails in practice.
- DO adjust the local implementation (name, call site, timing) and leave a note in the phase commit message + append a dated entry to the bottom of this file.
- If a failure forces an architectural change, stop and raise it to the user before editing 04 / 05 / 07 / 11 / 14.

## Pre-Write Grep Rule

Before writing the first call site of any FishNet API symbol (`PredictionRigidbody.X`, `TimeManager.X`, `[Replicate]`, `[Reconcile]`, `[TargetRpc]`, etc.) grep `Assets/FishNet/Runtime/` for the exact symbol. If not found, the name is wrong - resolve before writing. Do not infer FishNet API names from intuition. See `Docs/lessons-log.md` L2.

## Assumptions (to monitor during implementation)

### A1 - MovePosition / MoveRotation work inside [Replicate]

`PredictionRigidbody.MovePosition(Vector3)` and `.MoveRotation(Quaternion)` exist in FishNet 4.6.20 (confirmed at `Assets/FishNet/Runtime/Object/Prediction/PredictionRigidbody.cs:382,392`). Assumed that the written value survives the subsequent `Reconcile(state)` on all peers.

Implementation watchpoint: in `BuddahTeleportStep`, wrap the teleport call with:
```csharp
// ASSUMPTION A1: MovePosition value survives reconcile. If teleport visibly
// rubber-bands on remote peers, see Docs/prediction-refactor-plan/16-api-spike-checklist.md.
```

### A2 - Initialize(Rigidbody) is bootstrap-only safe

Assumed undefined behavior if called mid-life after first call. Plan restricts it to `Bootstrap`.

Implementation watchpoint: if any step needs to re-`Initialize`, stop - it is a design smell.

### A3 - OnPostReconcile fires on owner, server, and spectator

Assumed per FishNet docs. Used as the dispatch point for side effects moved out of `[Replicate]`.

Implementation watchpoint: if a spectator misses a side effect (e.g., trail rebase), fall back to `OnPostTick` + dirty flag. Update 04 / 08 accordingly with a dated note in this file.

### A4 - TargetRpc on ClientHost fires exactly once

Used by server-origin event relay in 05. Assumed single delivery when host is also server.

Implementation watchpoint: combat adapter self-fires a target RPC on host. If the host handler sees double-fires or misses, short-circuit with `if (IsServerStarted && Owner.IsLocalClient) { /* call handler directly */ }` per 14-R2 contingency.

### A5 - Reconcile replay gives deterministic entry state

Assumed that each replayed `[Replicate]` sees identical starting rigidbody state per tick. This is the basis for Phase 3 shadow-mode compare.

Implementation watchpoint: Phase 3 should include a one-shot log comparing owner vs server final position at end of `[Replicate]` for the same tick, to catch divergence early. If divergence is persistent, Phase 3 pauses and we revisit before Phase 4.

### A6 - Rigidbody.mass persists across Reconcile

Assumed that direct `rb.mass = value` is not overwritten by `PredictionRigidbody.Reconcile(state)`.

Implementation watchpoint: in `ModifierStep`, set mass via `rb.mass` after `Reconcile` restore (inside `OnPostReconcile`), not inside `[Replicate]`. If mass drifts between owner and server, add an `ActiveMass` field to ReconcileData and reapply every tick.

## Failure Log

Append dated entries when any assumption fails.

```
(empty)
```
