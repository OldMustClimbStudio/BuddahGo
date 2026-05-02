# BuddahGo Phase Gate System

This folder governs how multi-agent phase work is scoped, executed, and verified
in the BuddahGo prediction refactor (Phase 4b onwards).

## Why this exists

The 4b refactor moves through phases (V1 → V2a → V2a fix → V2b Step 0 → V2b Step 1
→ V3 → V4 → V5). Each phase exposes new failure modes captured in
`Docs/lessons-log.md`. But scope, strict gates, PRE-WORK decisions, and process
discipline used to live in chat history and the task tool — both volatile. This
system fixes that.

## How to use this system

**Before any phase work**, read in order:
1. `Docs/phase-gates/methodology.md` — process discipline rules
2. `Docs/phase-gates/process-flow.md` — the 7-stage pipeline every phase follows
3. `Docs/phase-gates/active/<current-contract>.md` — stamped contract for this phase

**During phase work**, use `Docs/phase-gates/templates.md` for ALL outputs:
recon report, design Q&A, PR description, smoke verification report.

**After phase closes**, archive contract to `archive/`, create new active contract.

## Active phase

| Phase | Contract | Branch | Status |
|---|---|---|---|
| V3 — Skill Site Migration to CombatAdapter | [active/v3-contract.md](active/v3-contract.md) | feat/phase4b-v3-skill-site-migrate | TEST (Stage 5 — Implementation merged at PR #36) |

Note: `active/v2b-step1-contract.md` is also present in this folder as a
historical record preserved by the user (full original contract with sign-off
ledger). The canonical archived V2b Step 1 contract with FINAL outcome lives at
[archive/v2b-step1-contract.md](archive/v2b-step1-contract.md). Both copies
serve different purposes; do not delete either without explicit user direction.

## Closed phases (archive)

| Phase | Contract | PR | Key lesson(s) |
|---|---|---|---|
| V1 — Adapter scaffold | (pending Phase B backfill) | merged | L7 latch contract |
| V2a — Dual-feed | (pending Phase B backfill) | merged | L11 (FIFO/replay blindness) |
| V2a fix — PostTick relocate | (pending Phase B backfill) | merged | L17 (phase-skew bidirectional) |
| V2b Step 0 — Tick-stamp | (pending Phase B backfill) | merged | L18 (side-effect mirror) |
| V2b Step 1 — Authority Flip | [archive/v2b-step1-contract.md](archive/v2b-step1-contract.md) | [#34](https://github.com/OldMustClimbStudio/BuddahGo/pull/34) merged @ `3181bc4` | L19 (authority-flip dead-compare removal) |

## Project Roadmap — Phase 4b in context

The original prediction refactor plan (`Docs/prediction-refactor-plan/`) defines
9 phases (0-8). Phase 4b is the ongoing sub-decomposition of Phase 4 (Locomotion
+ Impulse cut over) using the V1-V5 sub-phase pattern this Phase Gate System
governs.

```
Phase 0 ✅  Foundation
Phase 1 ✅  Data Contracts
Phase 2 ✅  Event Channels + CommandBus
Phase 3 ✅  Simulation Steps shadow mode
Phase 4 🔄  Locomotion + Impulse cut over  ← we are here
   ├─ V1            ✅  Adapter scaffold + L7 latch
   ├─ V2a           ✅  Dual-feed inverted shadow
   ├─ V2a fix       ✅  PostTick relocate (L16, L17 lessons)
   ├─ V2b Step 0    ✅  Tick-stamp channel + ConsumeReady (L18)
   ├─ V2b Step 1    ✅  Authority flip (L19, PR #34 @ 3181bc4)
   ├─ V3            🔄  Skill site migration (PR #36 — Stage 5 TEST pending)
   ├─ V4            ⏸  CombatRouting deletion + LEGACY_SHADOW retirement
   └─ V5            ⏸  LatencySim 100ms RTT terminal gate
Phase 5 ⏸  Skill adapter cut over  ← scope overlap with V3/V4, reconcile post-V4
Phase 6 ⏸  Teleport + Handoff cut over (channels exist, no consumer yet)
Phase 7 ⏸  Visual layer + PredictionSmoother migration (jitter root cause)
Phase 8 ⏸  Cleanup + Roslyn analyzer
   └─ Entry 7       ✅  M2 Stopwatch for V13 rep-*-ms (done out-of-order during V2a era)
```

### Phase 5 ↔ V3 reconciliation note
Original Phase 5 "Skill adapter cut over" overlaps significantly with Phase 4b
V3 "Skill site migration to CombatAdapter". After V4 closes (4b done), revisit
whether Phase 5 has additional scope (e.g., SkillExecutor internal migration)
or should be marked complete by V3+V4 work.

### Phase 6 status
Phase 6 covers Teleport + Handoff channels. V2b Step 1 added `LogicalId` to
TeleportCmd / ModifierCmd / HandoffCmd payloads (forward consistency), but
motor has NO consumer drain for these channels yet. Phase 6 replicates the
Impulse path's Step-0 → Step-1 pattern for these two channels.

### Carry-forward queue (cross-phase decisions deferred)
- **V4 PRE-WORK**: `pendingImpulseSummary` builder OR drop the field
- **V4 PRE-WORK**: `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan
- **V5 PRE-WORK**: Reconcile-before-RPC double-apply edge case under LatencySim
- **Phase 7 prep**: visual jitter quantitative re-evaluation (task #36)

## Pipeline overview

```
KICKOFF → RECON → DESIGN-QA → IMPLEMENT → TEST → VERIFY → MERGE
   ↑                                                          ↓
   └─────────── (post-merge) → LESSONS UPDATE ────────────────┘
```

Each arrow is a sign-off boundary. See `process-flow.md` for owner / input /
output / halt-condition per stage.

## Cross-references

- `Docs/lessons-log.md` — failure rules (complementary to this system)
- `Docs/workflow.md` — pre-edit protocol (this system extends it for 4b phases)
- `Docs/harness-manifest.json` — machine-readable routing (orthogonal)
- `agent-exchange/handoff/` — recon / design / verify reports land here
- `agent-exchange/console/raw/` — full-session smoke logs land here
