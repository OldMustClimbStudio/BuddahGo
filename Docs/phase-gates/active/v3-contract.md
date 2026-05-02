# V3 — Skill Site Migration to CombatAdapter Contract

**Phase ID:** phase4b-v3
**Branch:** feat/phase4b-v3-skill-site-migrate (cut from dev @ 3181bc4 — V2b Step 1 merge commit)
**Risk:** MEDIUM (mechanical refactor + adapter expansion superseded by router pattern; fewer architectural unknowns than Step 1)
**Status:** VERIFIED (Stages 5+6 PASS — awaiting Stage 7 MERGE approval)

---

## Scope (locked)

**In scope:**
- Migrate 4 skill site callsites from `BuddahPredictionCombatRouting.TryRouteImpulse` → `BuddahPredictionRouter.RouteImpulse` (new static helper):
  - `Assets/Scripts/Buddah/PushHitbox.cs:200` (melee push)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362` (charged projectile)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212` (regular projectile)
  - `Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs:45` (debug box)
- New static helper class `BuddahPredictionRouter` at `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs` with 3-tier dispatch:
  - Tier 1: V2 prediction Buddah → motor.TryApplyServerAuthoritativeImpulse (OLD-feed for `_legacyShadowScratch` continuity under `#if BUDDAH_PREDICTION_LEGACY_SHADOW`) + bootstrap.CombatAdapter.TryRouteImpulse (NEW-feed)
  - Tier 2: PushTargetBox debug → pushTargetBox.TryApplyServerImpulse (RaceMap-only dev objects)
  - Tier 3: Legacy Buddah → BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc (mode toggle off OR motor missing OR adapter null)
- Adapter unchanged (instance method preserved unchanged for V2 path; no expansion).
- CombatRouting.cs gets `[Obsolete]` attribute (V4 deletes the file entirely).

**Out of scope:**
- V4 (CombatRouting deletion + LEGACY_SHADOW define retirement + `pendingImpulseSummary` cleanup)
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation

---

## PRE-WORK answers (locked from design Q&A — all 9 sign-off items APPROVED 2026-05-02)

### Q0 — Adapter call signature for non-V2 victims
**(B) Static helper method.** New `BuddahPredictionRouter` static class with `RouteImpulse(...)` entry point. Skills call the static helper. Adapter instance method preserved unchanged for V2 path.

Justification: 3 victim categories (V2 / Legacy / PushTargetBox debug) make adapter-internal dispatch (option A) too polluted with legacy concerns. Static router gives skills a single entry point matching CombatRouting's existing static-dispatch shape — migration is mechanical.

### Q1 — OLD path feed location
**(B) Static helper does dual-feed.** Router calls BOTH `motor.TryApplyServerAuthoritativeImpulse` (OLD-feed, under `#if BUDDAH_PREDICTION_LEGACY_SHADOW`) AND `bootstrap.CombatAdapter.TryRouteImpulse` (NEW-feed) in the V2 victim branch. Adapter instance method stays unchanged.

Justification: follows naturally from Q0=(B). Single dispatch site, single fan-out site.

### Q1 sub-decision — Drop CombatRouting's try/catch fan-out
Post-Step-1, NEW IS authority. A NEW-path throw should escalate as FATAL, not be swallowed. Router calls adapter directly without try/catch — exceptions propagate normally.

### Q2 — DebugState mirror status check
**(A) DebugState mirror is effectively done.** Step 1's `ConsumeImpulseAuthoritativeEntry` already writes 6 DebugState fields per consumed impulse + `pendingImpulseCount` post-drain. The 1 remaining gap (`pendingImpulseSummary`) is explicitly deferred to V4 alongside CombatRouting deletion. V3 adds nothing.

### Q3 — GetComponent caching micro-opt — REVISITED
**(A)-with-revision.** Approved in principle, but adapter doesn't expand under approved Q0=B/Q1=B → no cache target materializes in V3. Router does fresh GetComponent calls per fire (matches today's CombatRouting cost). Documented for V4 reconsideration.

### Q4 — Static helper class location
**(A) New file `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs`.** Matches CombatRouting precedent (same folder, same static-class pattern). Sibling to CombatRouting during V3→V4 transition; both files visible, dead one marked `[Obsolete]`.

### Cross-cutting decisions (sub-items 7-9 from sign-off)
- **PushHitbox verbose Debug.Log lines DROPPED** in migration. Matches silent majority pattern (other 3 callsites had no per-callsite log). `[CommandBus]:Recv` covers V2 victim observation; `BuddahPredictionPushTargetBox.verboseLogs` covers PushTargetBox debug.
- **`BuddahPredictionImpulseDebugBox.fallbackToLegacy` toggle LEFT IN PLACE** as dead serialized field. Per CLAUDE.md hard-stop on serialized field removal, V4 cleanup item.
- **Single-line callsite migration**: each of 4 callsites becomes 1-line `BuddahPredictionRouter.RouteImpulse(...)` replacing the 4-7 line two-tier dispatch block.

---

## Strict gates

V3 inherits Step 1's strict gates because LEG axis is still active until V4. Router expansion must NOT break any Step 1 invariant.

### Path A — single-machine 30s Editor PlayMode
- All Step 1 Path A gates (LEG FATAL = 0, leg-imp-div = 0 across full session, leg-imp-compared ≥ 3, ClearAll = 4, DupReject = 0, DropFull = 0, no spurious D-LOC impulse warnings)
- **NEW V3 gates**:
  - 0 production callsites of `BuddahPredictionCombatRouting.TryRouteImpulse` (post-migration grep verification — Methodology Rule 2 mandate; only allowed hits are in CombatRouting.cs comments + Router.cs cref docstrings)
  - PushTargetBox fallback path verified live: hit a non-V2 victim if available
  - Visual: push lands on host buddah, PushGrace activates (same as Step 1)

### Path B — 2-peer LAN 60s (HOST + CLIENT, no LatencySim)
- All Step 1 Path B gates (both ends, full-session raw to disk per Methodology Rule 1)
- **NEW V3 gates**:
  - HOST `_legacyShadowScratch.ImpulseDrainCount` matches HOST `_realScratch.ImpulseDrainCount` for HOST-owned victim ticks
  - CLIENT same alignment
  - LEG axis stays at div=0 throughout (proves router's OLD-feed didn't introduce asymmetry)

### Methodology compliance
- Raw log on disk per Rule 1 (clear/rename Editor.log before smoke per the session-scoping sub-rule)
- Independent verification per Rule 2 (reviewer greps independently)
- Lessons-log update in same commit per Rule 5 (V3 expected to produce 0 new lessons — mechanical refactor)

---

## Deliverables

1. ✅ Recon report — `agent-exchange/handoff/2026-05-02-phase4b-v3-recon.md`
2. ✅ Design Q&A — `agent-exchange/handoff/2026-05-02-phase4b-v3-design.md`
3. ✅ Implementation — PR #36 commit `6e736e1` on `feat/phase4b-v3-skill-site-migrate` (7 production files +109/-40)
4. ⏸ Path A raw log — `agent-exchange/console/raw/<date>-phase4b-v3-single.log`
5. ⏸ Path B raw logs — `<date>-phase4b-v3-host.log` + `<date>-phase4b-v3-client.log`
6. ⏸ Independent verification report — `agent-exchange/handoff/<date>-phase4b-v3-verify.md`
7. ⏸ PR description updated with metric tables per Template 3
8. ⏸ Lessons-log: probably 0 new entries (mechanical refactor)

---

## Carry-forward flags (do NOT action in this phase)

- **V4 PRE-WORK** — `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan. After V3, OLD path body still exists but is fed only by router's dual-feed line under `#if`. V4 removes the define + deletes that single line + deletes CombatRouting + deletes `BuddahPredictedImpulseEventQueue` + LegacyShadow drain in motor.
- **V4 PRE-WORK** — `pendingImpulseSummary` cleanup (channel summary builder OR drop the field).
- **V4 PRE-WORK** — `BuddahPredictionImpulseDebugBox.fallbackToLegacy` dead serialized field removal (CLAUDE.md hard-stop release for V4 cleanup pass).
- **V4 PRE-WORK** — re-evaluate adapter caching opportunity (Q3-revision deferred).
- **V5 PRE-WORK** — Reconcile-before-RPC double-apply edge case explicit probe under LatencySim (carry-forward from Step 1 Q2).
- **Phase 7** — Visual jitter investigation. Step 1 confirmed jitter is pre-existing (not Step 1 regression). Task #36 already tracking.

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | cowork-reviewer | contract stamped post V2b Step 1 merge (PR #34 @ 3181bc4) |
| Recon | 2026-05-02 | cowork-reviewer | recon report at `agent-exchange/handoff/2026-05-02-phase4b-v3-recon.md`. Independent verification: 4 spot-checks (PushTargetBox usage population, 4 callsite two-tier fallback pattern, motor._impulseEventQueue feed convergence at TryQueueImpulseEvent, PushTargetBox direct rb.AddForce usage). KEY FINDING confirmed: 3 victim categories (V2 Buddah / Legacy Buddah / PushTargetBox debug) make Q0=(B) static router strongly preferred. ONE calibration: recon's "PushTargetBox Rule 7 violation" flag re-categorized as "V4 code-style cleanup" — PushTargetBox is not in prediction stack (no Bootstrap, no PredictionRigidbody, no reconcile), so strict Rule 7 doesn't apply (now codified in methodology.md Rule 7 scope clarification). Q-answer phase authorized with Q0-Q4 lean previews approved. |
| Design | 2026-05-02 | cowork-reviewer | Q-answer doc at `agent-exchange/handoff/2026-05-02-phase4b-v3-design.md`. All 9 numbered items APPROVED: Q0=(B) router file, Q1=(B) dual-feed in router, Q1-sub drop try/catch, Q2=(A) DebugState already done, Q3=(A)-revised no cache target materializes, Q4=(A) new file location, PushHitbox verbose log dropped, fallbackToLegacy field preserved per CLAUDE.md hard-stop, single-line callsite migration. Sub-observations forwarded for PR description: malformed-V2 graceful fallback to Tier 2/3 + dual-defense gate (router + adapter both check IsPredictionModeActive). |
| Implementation | 2026-05-02 | cowork-reviewer | PR #36 commit `6e736e1` on branch `feat/phase4b-v3-skill-site-migrate` (cut from dev @ 3181bc4). 7 production files +109/-40. New BuddahPredictionRouter.cs (88 LOC) + 4 skill-site single-line migrations + CombatRouting [Obsolete] attribute. Independent verification (Rule 2): router skeleton 100% matches design doc; design sub-observations baked into code comments (lines 22-29); 0 actual callsites of CombatRouting.TryRouteImpulse from production code (grep mandate met); 4/4 callsites use BuddahPredictionRouter.RouteImpulse; Q1 sub-decision (drop try/catch) honored; Methodology Rule 7 compliant (router itself does no rb writes, all tiers route through compliant downstream). All 9 sign-off items implemented faithfully. |
| Smoke | 2026-05-02 | Yonezawa | Path A (Host-only single peer): user-driven via ChargedHandProjectile self-push at DebugboxCanPush. Path B (2-peer LAN, 60s): user-driven on HOST + remote CLIENT. Editor.log cleared per Methodology Rule 1 sub-rule on all 3 logs (single Initialize block each). Raws on disk: `agent-exchange/console/raw/2026-05-02-phase4b-v3-{single,host,client}.log`. Path A digest also exposed Router.cs.meta YAML parse failure (line 11 trailing-space) — fixed inline by `9ab64f5` and re-tested clean. |
| Verify | 2026-05-02 | cowork-reviewer | Independent grep verification across all 3 raws (273 HEARTBEAT rows total). All strict gates met: 0 LEG FATAL / 0 INV FATAL / 0 CombatRouting.TryRouteImpulse calls / 0 DupReject / 0 exceptions / 0 compile errors. `leg-imp-div` MAX = 0 across all HBs. Router stack frames captured via PushTargetBox-mediated logs (Tier 2); Tier 1 V2-Buddah dispatches confirmed silent-by-design via cross-peer leg-imp-cons alignment. Stack trace evidence: `ChargedHandProjectileRuntime:236 → BuddahPredictionRouter:RouteImpulse:71 → PushTargetBox:TryApplyServerImpulse:48`. Implementer digests cross-checked against grep results — 100% metric match. Verify report: `agent-exchange/handoff/2026-05-02-phase4b-v3-verify.md`. |
| Merge | ⏸ | | (awaiting user approval — PR #36) |
