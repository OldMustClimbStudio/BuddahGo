# 12 - Migration Sequence

[Back to index](index.md)

The refactor ships in 8 phases over an estimated 15-20 working days. Each phase is independently mergeable and gated by the validation set in [13-validation-gates.md](13-validation-gates.md).

## Phase 0 - Skeleton (Day 1-2)

- Create new folders under `Assets/Scripts/New_Buddah/` per [02-directory-structure.md](02-directory-structure.md).
- Add empty class stubs for: simulation steps, event channels, command bus, adapters.
- No prefab change yet. Existing motor untouched.
- Compile only.
- The FishNet API spike is deferred; see [16-api-spike-checklist.md](16-api-spike-checklist.md) for the 6 API assumptions the implementation must flag with sentinel comments as each call site is added.
- **Cross-reference gate:** stub counts come from the API specs, not the `...` example list in 02. Verify: payloads count vs `06-command-bus.md` `TryEnqueueXxx`; adapters count vs `09-integration-adapters.md`; simulation steps vs `04-tick-pipeline.md`. Fix mismatches before V1. Rationale: `Docs/lessons-log.md` L1.

## Phase 1 - Data Contracts (Day 3-4)

- Extend `BuddahPredictedReconcileData` with new fields (queue cursors, handoff phase, timers). Append-only - no rename.
- Extend `BuddahPredictedInputData` with `LastConsumed*Id` and `OwnerControlMask`.
- Wire reads/writes inside motor temporarily as no-ops.
- Compile + existing playtest passes unchanged (because new fields are unused).

## Phase 2 - Event Channels + CommandBus (Day 5-7)

- Implement `BuddahPredictionEventChannel<T>`, `BuddahPredictionCommandBus`, payload structs.
- Add bus to prefab.
- Stand up channels but motor still uses old paths.
- Add the `Target_EnqueueXxx` RPCs.

## Phase 3 - Simulation Steps (Day 8-10)

- Implement `BuddahLocomotionStep`, `BuddahImpulseStep`, `BuddahModifierStep`, `BuddahTeleportStep`, `BuddahHandoffStep`.
- Inside motor's `[Replicate]`, run new steps in parallel (shadow mode) but keep old code as authority. Compare outputs in debug builds.
- This is the hardest phase. Time-budget half a week for divergence hunting.

## Phase 4 - Cut Over Locomotion + Impulse (Day 11-12)

- Replace old locomotion + impulse paths in `[Replicate]` with new steps.
- Remove `Debug.Log`, `_skillExecutor.Reset...`, `NotifyTeleportTrailRebases` from `[Replicate]`. Move to OnPostTick / OnPostReconcile aggregators.
- Remove `rb.Sleep / WakeUp / mass=` writes.
- Combat adapter goes live; `BuddahPredictionCombatRouting` deleted.

## Phase 5 - Cut Over Modifier + Skill Adapter (Day 13)

- Replace `SkillMovementBridge` and `ModifierBridge` with `BuddahPredictionSkillAdapter`.
- SkillExecutor migrates `.Server` and `.Target` paths to single adapter call.
- Old bridges deleted.

## Phase 6 - Cut Over Teleport + Handoff (Day 14-16)

- Replace inline teleport block with `BuddahTeleportStep`.
- Replace inline handoff block with `BuddahHandoffStep`.
- `RespawnBridge` and `HandoffBridge` replaced by adapters.
- Intro flow rewritten per [11-intro-result-session.md](11-intro-result-session.md).

## Phase 7 - Visual Layer (Day 17-18)

- New `BuddahPredictionVisualRoot` replaces bridge.
- Remove `ApplyVisualRootStabilization` runtime mutation.
- Verify remote intro flicker is gone (validation gate V4).
- Verify gameplay shake is gone (validation gate V3).

## Phase 8 - Cleanup (Day 19-20)

- Delete `BuddahPredictionLegacyIsolationBridge`, `BuddahPredictionPushTargetBox` legacy fallback (if scope allows), and any dead code.
- Add Roslyn analyzer rule rejecting `Rigidbody.position`/`velocity`/`mass` writes outside whitelisted files.
- Update `Docs/architecture.md`, `Docs/system-map.md`, this plan's status to "v1.0 shipped".

## Rollback Anchors

Each phase is rebasable independently because Phases 0-2 add only no-op state and Phase 3 runs in shadow mode. Phases 4+ are the real cuts; if any fails validation, revert to the prior phase commit.

See [13-validation-gates.md](13-validation-gates.md) for what passes/fails each phase.
