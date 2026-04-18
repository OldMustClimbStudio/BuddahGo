# Prediction Refactor Plan - Index

This is the canonical, industrial-grade refactor plan for the Client-Side Prediction (CSP) stack under `Assets/Scripts/New_Buddah/`. It supersedes any incremental patch advice given previously.

## Goal
Rebuild the prediction stack so every gameplay-affecting write is on the FishNet tick pipeline, every cross-system input is tick-aligned, and every visual artifact (own-Buddah jitter during gameplay, remote-Buddah flicker during intro) is removed at the architectural source.

## Scope
- Motor core, reconcile data, input data, modifier system
- Event channels (impulses, teleports, modifiers, mode changes)
- Visual layer and presentation bridges
- Bootstrap, mode switching, isolation
- Intro handoff, result handoff, respawn, combat, skill bridge
- Validation, migration, risks

## Read Order
Read sections in numerical order. Each file is a self-contained design contract under 100 lines. Cross-links point to dependencies.

| # | File | Topic |
|---|---|---|
| 1 | [01-design-principles.md](01-design-principles.md) | Non-negotiable rules every new file must satisfy |
| 2 | [02-directory-structure.md](02-directory-structure.md) | New folder layout and ownership |
| 3 | [03-data-contracts.md](03-data-contracts.md) | Replicate / Reconcile / Event payload shapes |
| 4 | [04-tick-pipeline.md](04-tick-pipeline.md) | Per-tick execution order on owner / server / spectator |
| 5 | [05-event-channel.md](05-event-channel.md) | Tick-aligned event dispatch and replay |
| 6 | [06-command-bus.md](06-command-bus.md) | External system entry point contract |
| 7 | [07-side-effect-migration.md](07-side-effect-migration.md) | Where each forbidden side effect moves to |
| 8 | [08-visual-layer.md](08-visual-layer.md) | Visual root, smoother, intro/result presentation |
| 9 | [09-integration-adapters.md](09-integration-adapters.md) | New bridges replacing the current Integration folder |
| 10 | [10-bootstrap-composition.md](10-bootstrap-composition.md) | Composition root, mode switch, prefab requirements |
| 11 | [11-intro-result-session.md](11-intro-result-session.md) | Intro handoff, result handoff, respawn flow |
| 12 | [12-migration-sequence.md](12-migration-sequence.md) | 8-phase, ~15-20 workday rollout |
| 13 | [13-validation-gates.md](13-validation-gates.md) | What must pass before each phase ships |
| 14 | [14-risks-contingencies.md](14-risks-contingencies.md) | Known risks, fallback strategies |
| 15 | [15-mental-model.md](15-mental-model.md) | One-paragraph post-refactor system summary |
| 16 | [16-api-spike-checklist.md](16-api-spike-checklist.md) | FishNet API spike executed before Phase 0 |
| H | [handoff-context.md](handoff-context.md) | Self-contained context for a fresh agent picking up execution |

## Deliverable Definition
The refactor is complete when:
1. `BuddahPredictedMotor.cs` contains no direct `rb.position` / `rb.velocity` / `rb.mass` / `rb.Sleep` / `Initialize` writes inside `[Replicate]`.
2. Every gameplay-affecting state field consumed by `[Replicate]` exists in `BuddahPredictedReconcileData`.
3. No external system writes prediction state outside the CommandBus on the owner client.
4. Visual root has zero gameplay-affecting writes.
5. Validation gates in [13-validation-gates.md](13-validation-gates.md) all pass.

## Companion FishNet References
- [../fishnet-prediction.md](../fishnet-prediction.md) - MUST / MUST NOT contract
- [../fishnet-rules.md](../fishnet-rules.md) - global FishNet conformance rules
- [../architecture.md](../architecture.md) - layer ownership

## Status
Draft v1 - awaiting review before Phase 0 (skeleton) begins.
