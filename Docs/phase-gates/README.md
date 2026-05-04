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

Three concurrent tracks as of 2026-05-04:

1. **Phase 7 — visual jitter measurement + root-cause framework.** Stages 1–4 done (Kickoff → RECON → DESIGN → IMPLEMENT review). Stage 5 SMOKE digest identified H1''' (handoff state capture variance) as session-level jitter root cause. Stage 6 VERIFY + Stage 7 MERGE pending closeout. Active contract: `Docs/phase-gates/active/phase7-contract.md`. Recon at `agent-exchange/handoff/2026-05-03-phase7-recon.md`. Design at `agent-exchange/handoff/2026-05-03-phase7-design.md`. Implement review at `agent-exchange/handoff/2026-05-03-phase7-stage4-implement-review.md`.
2. **Phase 7.5-A.1 — smoother config retrofit (in flight, parallel ship).** Scope: `_extrapolation:0` + `_adaptiveInterpolation:0` on Buddah prefab + `FrameRateLockGuard@120` + optional `LocalTransformTickSmoother.cs:98` vendor hack. Attacks H1''' at the smoother-config layer (orthogonal to Phase 6's architectural attack). Design framework at `agent-exchange/handoff/2026-05-03-phase7-5-design-framework.md`. No standalone phase-gate contract — folded into Phase 7 closeout commit chain. **Ships before Phase 6 branch cuts.**
3. **Phase 6 — race-start handoff redesign (Stage 2 RECON SIGN-OFF 2026-05-04).** Replace velocity-inherit/Blend handoff state machine with stop-then-countdown pattern (decelerate → 3s lock → simultaneous server-driven unlock). Architectural elimination of H1''' M1/M2/M3 confounds at the handoff moment. Active contract: [`Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md`](active/phase6-race-start-handoff-redesign-contract.md). RECON report at `agent-exchange/handoff/2026-05-04-phase6-recon.md`; reviewer verify at `agent-exchange/handoff/2026-05-04-phase6-recon-verify.md`. Stage 3 DESIGN-QA authorized; awaiting implementer kickoff. Stage 4 IMPLEMENT branch (`feat/phase6-race-start-handoff-redesign`) cuts from dev tip; subsequent stage commits accumulate on the same branch through one Phase 6 PR.

Phase 4b sub-phase chain (V1 → V5) **COMPLETE** as of 2026-05-03 (PR #40).

Note: `active/v2b-step1-contract.md` is preserved here as a historical record by user direction. The canonical archived V2b Step 1 contract with FINAL outcome lives at [archive/v2b-step1-contract.md](archive/v2b-step1-contract.md). Both copies serve different purposes; do not delete either without explicit user direction.

## Closed phases (archive)

| Phase | Contract | PR | Key lesson(s) |
|---|---|---|---|
| V1 — Adapter scaffold | [archive/v1-contract.md](archive/v1-contract.md) | [#29](https://github.com/OldMustClimbStudio/BuddahGo/pull/29) merged @ `39c5217` | L7 (lifecycle hooks double-fire; latch contract) |
| V2a — Dual-feed | [archive/v2a-contract.md](archive/v2a-contract.md) | [#31](https://github.com/OldMustClimbStudio/BuddahGo/pull/31) merged @ `fe17418` | 0 new lessons during V2a; observation gap surfaced post-merge → L16 (FIFO/replay blindness) born in V2a-fix |
| V2a fix — PostTick relocate | [archive/v2a-fix-contract.md](archive/v2a-fix-contract.md) | [#32](https://github.com/OldMustClimbStudio/BuddahGo/pull/32) merged @ `bd47f04` | L16 (FIFO/replay blindness) + L17 (phase-skew bidirectional) |
| V2b Step 0 — Tick-stamp | [archive/v2b-step0-contract.md](archive/v2b-step0-contract.md) | [#33](https://github.com/OldMustClimbStudio/BuddahGo/pull/33) merged @ `49e3c2f` | L18 (dual-path side-effect mirror audit) |
| V2b Step 1 — Authority Flip | [archive/v2b-step1-contract.md](archive/v2b-step1-contract.md) | [#34](https://github.com/OldMustClimbStudio/BuddahGo/pull/34) merged @ `3181bc4` | L19 (authority-flip dead-compare removal) |
| V3 — Skill Site Migration | [archive/v3-contract.md](archive/v3-contract.md) | [#36](https://github.com/OldMustClimbStudio/BuddahGo/pull/36) merged | 0 new lessons + Methodology Rule 1-D / Rule 2 trust-hierarchy / Rule 10 commit hygiene |
| V4 — CombatRouting Deletion + LEGACY_SHADOW Retirement | [archive/v4-contract.md](archive/v4-contract.md) | [#37](https://github.com/OldMustClimbStudio/BuddahGo/pull/37) merged + corrective [#39](https://github.com/OldMustClimbStudio/BuddahGo/pull/39) | L20 (CS2001 cold-start vs runtime live refs) + L21 (risk:HIGH full-grep mandate + negative-claim positive verification) + L22 (git plumbing over working tree for negative-claim verification) |
| V5 — LatencySim Terminal Gate + Carry-forward Resolution | [archive/v5-contract.md](archive/v5-contract.md) | [#40](https://github.com/OldMustClimbStudio/BuddahGo/pull/40) merged @ `14e4757` | 0 new lessons (Q-gates all clean at 100ms LatencySim) + Methodology Rule 11 (SMOKE driver hard-precondition for time-sensitive probes) |

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
Phase 4 ✅  Locomotion + Impulse cut over  ← DONE via Phase 4b sub-phase chain V1→V5
   ├─ V1            ✅  Adapter scaffold + L7 latch
   ├─ V2a           ✅  Dual-feed inverted shadow
   ├─ V2a fix       ✅  PostTick relocate (L16, L17 lessons)
   ├─ V2b Step 0    ✅  Tick-stamp channel + ConsumeReady (L18)
   ├─ V2b Step 1    ✅  Authority flip (L19, PR #34 @ 3181bc4)
   ├─ V3            ✅  Skill site migration (PR #36 — Methodology Rule 1-D / 2-trust-hierarchy / 10 born here)
   ├─ V4            ✅  CombatRouting deletion + LEGACY_SHADOW retirement (PR #37 + corrective #39 — L20 / L21 / L22 / Rule 2 sub-clause)
   └─ V5            ✅  LatencySim 100ms RTT terminal gate + Q1 protocol-version handshake + Q3-B reconcile probe (PR #40 — Rule 11 SMOKE driver precondition)
Phase 5 ✅  Skill adapter cut over  ← marked COMPLETE by V3+V4 work; ComboSkill grep returned 0 residue at V5 closeout
Phase 6 ⏸  Teleport + Handoff cut over (channels exist, no consumer yet)
Phase 7 🔓  Visual layer + PredictionSmoother migration (jitter root cause) — UNBLOCKED, awaits user KICKOFF trigger
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
- **Phase 6 PRE-WORK**: replicate V2b Step 0/1 tick-stamp + authority-flip pattern for Teleport + Handoff channels (motor has channels but no consumer drain)
- **Phase 7 prep**: visual jitter quantitative re-evaluation (task #36) — **unblocked at V5 close**
- **Phase 8**: L9 ClampPlanarSpeed fix + Roslyn analyzer + adapter caching
- **V6 / Phase 7 prep candidate**: Q2 200ms LatencySim broader observation (eligible per V5 Q2-B trigger; deferred at V5 closeout per user direction)
- **Q0-B retrofit (conditional)**: reconcile-before-RPC double-apply mitigation. Race NOT observed at 100ms. Preemptive code shape preserved in `agent-exchange/handoff/2026-05-03-phase4b-v5-design.md` for future-regime pivot if 200ms+ surfaces it.

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
