# Prediction Refactor - Handoff Context

[Back to index](index.md)

This document is the entire context a fresh agent needs to execute the prediction refactor in this project. Read it top-to-bottom once, then proceed.

## 1. Project Routing (do this first)

1. Read `CLAUDE.md` at project root.
2. Run `Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent refactor -Systems prediction` to get current owner / protected files / validation sequences.
3. Read `Docs/prediction-refactor-plan/index.md` and follow the read order.
4. Read `Docs/fishnet-prediction.md` for the MUST / MUST NOT contract.

## 2. What Has Already Been Decided

- The plan is written and committed under `Docs/prediction-refactor-plan/` (16 sub-files plus index, all under 100 lines).
- `Docs/harness-manifest.json` already references `Docs/prediction-refactor-plan/index.md` under the `prediction` system's `requiredDocs`.
- Layer 0 static audit ran. Three doc patches landed in 03 / 04 / 07 covering audit gaps. Specifics: (a) ReconcileData must absorb pending teleport/handoff event payloads, consumed-id cursors, awaiting-handoff flag, pre-handoff bypass tick, and impulse queue snapshot; (b) `BuildReplicateData` must short-circuit with `return default;` when `!IsOwner`; (c) `RoomStateManager.ReportLocalGameplayLive` and `rb.isKinematic` writes inside `[Reconcile]` must be moved to post-reconcile aggregators.
- The user reproduces two specific symptoms that this refactor must fix: own + remote Buddah model violently shake during normal gameplay, and remote (non-owner) Buddah models flicker back-and-forth during intro. These are tracked under V3 / V4 in `13-validation-gates.md`.

## 3. What Has Not Yet Been Done

- No production code has been edited. All current `Assets/Scripts/New_Buddah/` files are at their pre-refactor state.
- The FishNet API spike is **deferred by user decision** (integrated test pipeline makes a one-off spike costly). See `16-api-spike-checklist.md` for 6 API assumptions (A1-A6). Each call site that touches one of those assumptions must carry an `// ASSUMPTION A#` sentinel comment + a one-line user-visible log the first time it runs, so real-world failure is caught without rolling back the plan. If an assumption fails, adjust local implementation only; append a dated entry to 16's Failure Log; never change plan direction without user approval.
- The `PredictionApiSpike.cs` file under `Assets/Scripts/Sandbox/` can be deleted - it is no longer needed.

## 4. Execution Sequence

Start directly at Phase 0 from `12-migration-sequence.md`. Each phase has gates listed in `13-validation-gates.md`; do not advance to the next phase until its gates pass. Report symptoms V3 / V4 explicitly during Phase 7. Whenever a phase introduces code that touches an FishNet API assumption (A1-A6 in `16-api-spike-checklist.md`), add the sentinel comment + a one-shot first-run log at that call site.

## 5. Critical Rules That Are Easy to Forget

- `BuddahPredictedMotor.cs` and `BuddahPredictedReconcileData.cs` are protected files. Touch them only inside the planned phases, with explicit awareness.
- Files under `Docs/` and `skills/` must stay under 100 lines per `CLAUDE.md`. If you grow a sub-file, split it.
- Do not introduce new `Rigidbody.position` / `velocity` / `mass` / `Sleep` / `WakeUp` writes anywhere outside the simulation steps. Phase 8 adds a Roslyn analyzer that will reject this.
- Keep changes inside the `prediction` manifest system per pass. If a change crosses into `skill-execution` or `race-and-results`, split it (CLAUDE.md hard stop).
- `SkillExecutor` is the only sanctioned skill cast entry point. Do not bypass.

## 6. Key File Locations

- Plan index: `Docs/prediction-refactor-plan/index.md`
- Spike checklist: `Docs/prediction-refactor-plan/16-api-spike-checklist.md`
- Spike code: `Assets/Scripts/Sandbox/PredictionApiSpike.cs`
- Current motor: `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` (1644 lines, protected)
- Current reconcile data: `Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs` (56 lines, protected)
- Visual flicker root cause: `Assets/Scripts/New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs` (`ShouldApplyOwnerVisualStabilization` runs before the IsOwner check)

## 7. When You Get Stuck

Follow `Docs/recovery.md`. If a reconcile / replay / desync error appears during a phase, that phase's commit must be reverted before any further code change.

## 8. Reporting Done

You are done with a phase when its gates in `13-validation-gates.md` all pass and you have a one-paragraph confirmation message stating which gates ran and what the observed result was. You are done with the refactor when `15-mental-model.md` exit criteria all hold.

## 9. Pivot Sync Checklist

If any strategy/direction change lands during this refactor (example: deferring the API spike), the pivot is not complete until all of these are updated in the same pass:
- the doc that owns the strategy (16 for spike, 11 for intro flow, etc.)
- this `handoff-context.md` (sections 3 and 4)
- `Docs/prediction-refactor-plan/index.md` status line
- the onboarding prompt template you hand to the next agent window
- any `skills/` file that referenced the old strategy

After editing, grep the plan for the pre-pivot phrasing. Non-empty grep = unfinished pivot. Rationale: `Docs/lessons-log.md` L4.
