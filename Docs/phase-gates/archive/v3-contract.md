# V3 — Skill Site Migration to CombatAdapter Contract (ARCHIVED)

**Phase ID:** phase4b-v3
**Branch:** feat/phase4b-v3-skill-site-migrate (cut from dev @ 3181bc4 — V2b Step 1 merge commit)
**Risk:** MEDIUM (mechanical refactor + adapter expansion superseded by router pattern; fewer architectural unknowns than Step 1)
**Status:** ✅ MERGED — PR #36 to dev (2026-05-02)

---

## Scope (locked)

**In scope:**
- Migrate 4 skill site callsites from `BuddahPredictionCombatRouting.TryRouteImpulse` → `BuddahPredictionRouter.RouteImpulse` (new static helper):
  - `Assets/Scripts/Buddah/PushHitbox.cs:200` (melee push)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362` (charged projectile)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212` (regular projectile)
  - `Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs:45` (debug box)
- New static helper class `BuddahPredictionRouter` at `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs` with 3-tier dispatch (V2 / PushTargetBox / Legacy)
- Adapter unchanged
- CombatRouting.cs gets `[Obsolete]` attribute (V4 deletes the file)

**Out of scope:**
- V4 (CombatRouting deletion + LEGACY_SHADOW define retirement + `pendingImpulseSummary` cleanup)
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation

---

## PRE-WORK answers (from design Q&A — all 9 sign-off items APPROVED 2026-05-02)

- **Q0** = (B) Static helper method (`BuddahPredictionRouter`)
- **Q1** = (B) Router does dual-feed; **Q1-sub** drop CombatRouting's try/catch
- **Q2** = (A) DebugState mirror already done in Step 1 (`pendingImpulseSummary` defers to V4)
- **Q3** = (A)-revised: no cache target materializes since adapter doesn't expand
- **Q4** = (A) New file at `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs`
- Cross-cutting:
  - PushHitbox verbose Debug.Log lines DROPPED (silent majority pattern)
  - `BuddahPredictionImpulseDebugBox.fallbackToLegacy` LEFT IN PLACE (CLAUDE.md hard-stop on serialized field removal — V4 cleanup item)
  - Single-line callsite migration

---

## Strict gates

### Path A — single-machine 30s
- All Step 1 Path A gates inherited
- 0 production callsites of `BuddahPredictionCombatRouting.TryRouteImpulse`
- Visual: push lands + PushGrace activates

### Path B — 2-peer LAN 60s
- All Step 1 Path B gates inherited
- HOST/CLIENT `_legacyShadowScratch.ImpulseDrainCount` matches `_realScratch.ImpulseDrainCount` for owned-victim ticks
- LEG axis stays at div=0 throughout

---

## Final outcome

✅ **STRICT PASS, MERGED.**

- PR #36 → `dev` (2026-05-02)
- 7 production files +109/-40
- New BuddahPredictionRouter.cs (88 LOC) + 4 skill-site single-line migrations + CombatRouting [Obsolete]

### Smoke results (3 sessions, 273 HBs total, 100% digest match)

| Path | HBs | leg-imp-compared | leg-imp-div | LEG FATAL | Other |
|---|---|---|---|---|---|
| Path A (single) | 40 | max=4 (≥3) | 0 | 0 | ClearAll=4, DupReject=0, Router stack=7 |
| Path B HOST | 101 | (digest verified) | 0 | 0 | ClearAll=8 (4×2 spawns) |
| Path B CLIENT | 132 | (digest verified) | 0 | 0 | 3 Recv lines with `eventTick=N logicalId=N` (Q0 wire format end-to-end) |

### Mid-test fix
- Router.cs.meta YAML parse warning at line 11 (trailing-space cleanup) → fixed inline by `9ab64f5`

### Lessons / Methodology updates landed in same PR
- 0 new lessons-log entries (mechanical refactor as expected)
- **Methodology Rule 1 sub-rule (D)** — post-smoke event sanity check (born from V3 Path A first-attempt 0-hit false PASS)
- **Methodology Rule 2 trust-hierarchy sub-clause** — downstream evidence > upstream stack frames (born from V3 Path A "Router stack=0" false NEGATIVE)
- **Methodology Rule 10** — working tree commit hygiene (born from mid-V3 Phase A docs loss + chat-memory recovery)

---

## Carry-forward delivered to V4 (now in v4-contract.md)

- **V4 PRE-WORK Q0**: `pendingImpulseSummary` cleanup (channel summary builder OR drop the field)
- **V4 PRE-WORK Q1**: `BuddahPredictionImpulseDebugBox.fallbackToLegacy` dead serialized field removal (LEGACY_SHADOW retiring naturally releases the CLAUDE.md hard-stop)
- **V4 PRE-WORK Q2**: Adapter caching opportunity re-evaluation (Q3-revision from V3)
- **V4 PRE-WORK Q3**: `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan (delete OLD path entirely)
- **V4 PRE-WORK Q4**: Strict gate recalibration without LEG axis safety net

## Carry-forward delivered to V5
- Reconcile-before-RPC double-apply edge case test under LatencySim (carried since Step 1 Q2)

## Carry-forward delivered to Phase 7
- Visual jitter quantitative re-evaluation (task #36 already tracking)

---

## Sign-off ledger (final)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | cowork-reviewer | contract stamped post V2b Step 1 merge (PR #34 @ 3181bc4) |
| Recon | 2026-05-02 | cowork-reviewer | 4 spot-checks (PushTargetBox population, 4 callsite two-tier fallback, motor._impulseEventQueue feed convergence, PushTargetBox direct rb.AddForce). KEY FINDING: 3 victim categories. Calibrated "Rule 7 violation" flag → "V4 code-style cleanup" (Rule 7 scope codified). |
| Design | 2026-05-02 | cowork-reviewer | All 9 sign-off items APPROVED. Sub-observations forwarded for PR description: malformed-V2 graceful fallback + dual-defense gate. |
| Implementation | 2026-05-02 | Yonezawa + Claude (impl) | PR #36 commit `6e736e1`. 7 production files +109/-40. Router skeleton 100% matches design doc; 4/4 callsites migrated; Q1 sub-decision (drop try/catch) honored; Methodology Rule 7 compliant. |
| Smoke | 2026-05-02 | Yonezawa | 3 raw logs (single/host/client) on disk. Editor.log cleared per Methodology Rule 1 sub-rule. Path A clean re-run after first-attempt 0-hit false PASS taught us Rule 1 sub-rule (D). Mid-test fix `9ab64f5` (Router.cs.meta YAML). |
| Verify | 2026-05-02 | cowork-reviewer | Verify report at `agent-exchange/handoff/2026-05-02-phase4b-v3-verify.md`. 273 HBs across 3 sessions, 100% strict-gate compliance, 100% digest match. Trust-hierarchy lesson born here → Methodology Rule 2 sub-clause. |
| Merge | 2026-05-02 | Yonezawa | PR #36 merged to dev. V3 closeout commit also added contract deliverables ✅ + 2 methodology sub-rules. |
