# 15 - Post-Refactor Mental Model

[Back to index](index.md)

## One-Paragraph Summary

After the refactor, a Buddah's motion is defined entirely by two things: the owner's per-tick `ReplicateData` and the server's `ReconcileData`. Every external influence - skills, combat impulses, intro spline handoffs, respawn teleports, gate changes - is funneled through `BuddahPredictionCommandBus`, which allocates an owner-side event id, stores the event in a ring buffer, and advances a `LastConsumedXxxId` cursor on the next `ReplicateData`. The motor's `[Replicate]` is a pure function of those inputs and the reconcile state; it writes only through `PredictionRigidbody`; it runs the same steps on owner, server, and spectator; and it flushes side effects (trail rebases, skill-effect resets, debug logs) via `OnPostTick` / `OnPostReconcile` aggregators. The visual layer reads a smoother-driven snapshot and never writes back. Mode switches between legacy movement and prediction v2 are the only privileged path that clears state. The system's correctness rests on one rule the entire architecture is designed to enforce: if it affects motion, it lives in `ReconcileData`.

## Where to Look First When Something Goes Wrong

| Symptom | First suspect |
|---|---|
| Shake on every Buddah | a write inside `[Replicate]` bypassed `PredictionRigidbody`; check simulation steps |
| Remote-only flicker | visual root running owner-intended logic on non-owner; check `BuddahPredictionVisualRoot` role gates |
| Skill effect applied twice | SkillExecutor `.Server` and `.Target` both called adapter; confirm single convergence point |
| Teleport snaps back | Teleport id fell out of ring; check `PredictionOverrun` log |
| Desync after respawn | reset flags missing from `TeleportCmd`; audit flag mapping in `BuddahTeleportStep` |
| Intro handoff stutter | `HandoffCmd` reached before ownership established; check server hold-until-ready |

## Single-Owner Rule Cheat Sheet

| Concern | Owned by |
|---|---|
| Physics writes | `PredictionRigidbody` (via simulation steps only) |
| Event enqueue | `BuddahPredictionCommandBus` |
| Mode switch | `BuddahPredictionBootstrap.ApplyMode` |
| Side effect dispatch | `OnPostTick` / `OnPostReconcile` aggregators on motor |
| Visual smoothing | `BuddahPredictionVisualRoot` |
| Scene transitions | `RoomStateManager` (unchanged) |
| Skill dispatch | `SkillExecutor` (unchanged) |
| Config | `ProjectConfigRuntime` (unchanged) |

## What Agents Should Always Do

1. Route through `Get-BuddahGoHarnessContext.ps1 -Intent refactor -Systems prediction`.
2. Read [01-design-principles.md](01-design-principles.md) before any edit.
3. If the change adds a new external write, extend `BuddahPredictionCommandBus`; do not bypass it.
4. If the change adds state to `[Replicate]`, extend `BuddahPredictedReconcileData` in the same pass.
5. Run the gates in [13-validation-gates.md](13-validation-gates.md) before claiming completion.

## Exit Criterion

The refactor is "shipped" when:
- `BuddahPredictedMotor.cs` is under 400 lines.
- Every simulation step file is under 150 lines.
- Roslyn boundary analyzer passes with zero violations.
- Symptoms V3 and V4 are un-reproducible over 10 consecutive races.
- This plan's index `Status` line is updated to `v1.0 shipped`.
