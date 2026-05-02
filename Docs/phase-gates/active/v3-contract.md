# V3 — Skill Site Migration to CombatAdapter Contract

**Phase ID:** phase4b-v3
**Branch:** feat/phase4b-v3-skill-site-migrate (cut from dev @ 3181bc4 — V2b Step 1 merge commit)
**Risk:** MEDIUM (mechanical refactor + adapter expansion; fewer architectural unknowns than Step 1)
**Status:** KICKOFF

---

## Scope (locked)

**In scope:**
- Migrate 4 skill site callsites from `BuddahPredictionCombatRouting.TryRouteImpulse` → `bootstrap.CombatAdapter.TryRouteImpulse`:
  - `Assets/Scripts/Buddah/PushHitbox.cs:200` (melee push)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362` (charged projectile)
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212` (regular projectile)
  - `Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs:45` (debug box)
- **Expand `BuddahPredictionCombatAdapter.TryRouteImpulse` to subsume CombatRouting's responsibilities**:
  - Feed OLD path (`motor.TryApplyServerAuthoritativeImpulse`) for `_legacyShadowScratch` continuity until V4 retires LEGACY_SHADOW define
  - Handle PushTargetBox fallback for non-PredictionV2 victims (`bootstrap` null OR `IsPredictionModeActive() == false`)
  - Existing NEW path (channel enqueue + RPC) preserved
- CombatRouting becomes structurally dead but kept in tree (V4 deletes).
- Skills get a single entry point. No more fan-out indirection.

**Out of scope:**
- V4 (CombatRouting deletion + LEGACY_SHADOW define retirement + `pendingImpulseSummary` cleanup)
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation

---

## PRE-WORK questions (must answer in design Q&A phase before code)

### Q0 — Adapter call signature for non-V2 victims
Skills today call `CombatRouting.TryRouteImpulse(victim, ...)` and don't know whether victim is V2 or legacy. Post-V3 callsite should be `victim.GetComponent<BuddahPredictionBootstrap>()?.CombatAdapter?.TryRouteImpulse(...)` — but for legacy victims (no bootstrap), this short-circuits to null and the skill loses its hit.

Pick:
- (A) **Adapter handles fallback internally**: even non-V2 victims go through adapter; adapter detects no bootstrap and falls back to PushTargetBox. Requires adapter to NOT depend on bootstrap for entry guards (or use a static entry helper).
- (B) **Static helper method**: introduce `BuddahPredictionCombatAdapter.RouteImpulseStatic(victim, ...)` (or new `BuddahPredictionRouter` static class) that does adapter-or-PushTargetBox dispatch. Skills call the static helper. Adapter instance method remains as today.
- (C) **Skills check victim type**: explicit `if (bootstrap.IsPredictionV2) adapter.TryRouteImpulse else PushTargetBox.TryApplyServerImpulse` at each callsite. Spreads logic.

Recommendation: lean (B) — keeps instance adapter clean, gives skills a stable static entry, mirrors today's CombatRouting static dispatch shape. Q-answer phase finalizes.

### Q1 — OLD path feed location
Adapter today only enqueues to NEW channel. After V3, who calls `motor.TryApplyServerAuthoritativeImpulse` to keep `_legacyShadowScratch` populated?

Pick:
- (A) **Adapter internal dual-feed**: `adapter.TryRouteImpulse` calls `motor.TryApplyServerAuthoritativeImpulse` first (OLD), then enqueues to channel (NEW). Mirrors CombatRouting's current fan-out logic but moved into adapter. Single entry point.
- (B) **Static helper does dual-feed**: if Q0 picks (B), the static helper does both calls. Adapter instance method stays NEW-only.
- (C) **No OLD feed; retire LEGACY_SHADOW now**: bring V4's shadow retirement into V3. Drop `_legacyShadowScratch` writes + `[D-IMP LEG FATAL]` gate. Strict gate downgrades to "no FATAL on D-LOC axis only" until next observation system arrives.

Recommendation: depends on Q0. If Q0 = (B), then Q1 = (B). If Q0 = (A), then Q1 = (A). Q1 = (C) is aggressive — consolidates V3 + part of V4 but loses shadow comparison early. Lean toward NOT (C) to preserve safety net through V3's mechanical changes.

### Q2 — DebugState mirror status check
V2b Step 0 audit flagged DebugState mirror as V3 PRE-WORK. V2b Step 1 implementation already mirrored 6 fields in NEW drain (motor.cs:1923-1929) + `pendingImpulseCount` (motor.cs:1906). The 1 remaining gap is `pendingImpulseSummary` (documented as "transitional staleness, V4 cleanup").

Confirm:
- (A) DebugState mirror is effectively done; V3 does NOT need to add anything; `pendingImpulseSummary` defers to V4.
- (B) V3 should add `pendingImpulseSummary` builder to channel now.
- (C) V3 audits DebugState consumer code (Inspector / HUD) and explicitly drops fields no longer needed.

Recommendation: lean (A) — Step 1 already covered the bulk; defer summary builder to V4 alongside CombatRouting deletion to keep V3 tightly scoped.

### Q3 — GetComponent caching micro-opt
Step 0 carry-forward: adapter does `victim.GetComponent<BuddahPredictionBootstrap>()` per call. CombatRouting did the same. After V3 expansion, adapter may also need GetComponent<PushTargetBox> for fallback. That's 2 GetComponent calls per impulse.

Pick:
- (A) Implement now in V3 (cache bootstrap + pushTargetBox refs at adapter init or first-call).
- (B) Defer to V4 / Phase 8 perf cleanup.

Recommendation: lean (A) — V3 is expanding adapter anyway, cache in same pass. Cost is small.

### Q4 — Static helper class location (only if Q0/Q1 = B)
If we add a static helper, where does it live?
- (A) New file `BuddahPredictionRouter.cs` in `Assets/Scripts/New_Buddah/Integration/`
- (B) Static method on `BuddahPredictionCombatAdapter` (e.g., `BuddahPredictionCombatAdapter.RouteImpulse(victim, ...)` static)
- (C) Static method on `BuddahPredictionBootstrap` (e.g., `BuddahPredictionBootstrap.RouteImpulse(victim, ...)`)

Recommendation: lean (A) — separate file matches CombatRouting's pattern, easier to find, cleanest delete in V4 if router becomes thin.

---

## Strict gates

V3 inherits Step 1's strict gates because LEG axis is still active until V4. Adapter expansion must NOT break any Step 1 invariant.

### Path A — single-machine 30s Editor PlayMode
- All Step 1 Path A gates (LEG FATAL = 0, leg-imp-div = 0 across full session, leg-imp-compared ≥ 3, ClearAll = 4, DupReject = 0, DropFull = 0, no spurious D-LOC impulse warnings)
- **NEW V3 gates**:
  - 0 calls to `BuddahPredictionCombatRouting.TryRouteImpulse` from skill sites (post-migration grep verification)
  - PushTargetBox fallback path verified live: hit a non-V2 victim if available, OR test that adapter returns false gracefully when bootstrap is null
  - Visual: push lands on host buddah, PushGrace activates (same as Step 1)

### Path B — 2-peer LAN 60s (HOST + CLIENT, no LatencySim)
- All Step 1 Path B gates (both ends, full-session raw to disk per Methodology Rule 1)
- **NEW V3 gates**:
  - 0 spurious "OLD path silent" symptoms (LegacyShadow drain count matches NEW drain count per peer per spawn)
  - HOST `_legacyShadowScratch.ImpulseDrainCount` matches HOST `_realScratch.ImpulseDrainCount` for HOST-owned victim ticks
  - CLIENT same alignment
  - LEG axis stays at div=0 throughout (proves adapter's OLD feed expansion didn't introduce asymmetry)

### Methodology compliance (Phase A discipline)
- Raw log on disk per Rule 1 (clear/rename Editor.log before smoke per the new sub-rule)
- Independent verification per Rule 2 (reviewer greps Step 1 session boundary)
- Lessons-log update in same commit per Rule 5 (if any new failure mode surfaces)

---

## Deliverables

1. ⏸ Recon report — `agent-exchange/handoff/<date>-phase4b-v3-recon.md`
2. ⏸ Design Q&A — `agent-exchange/handoff/<date>-phase4b-v3-design.md`
3. ⏸ Implementation — code changes on `feat/phase4b-v3-skill-site-migrate`
4. ⏸ Path A raw log — `agent-exchange/console/raw/<date>-phase4b-v3-single.log`
5. ⏸ Path B raw logs — `<date>-phase4b-v3-host.log` + `<date>-phase4b-v3-client.log`
6. ⏸ Independent verification report — `agent-exchange/handoff/<date>-phase4b-v3-verify.md`
7. ⏸ PR with full description per Template 3 of `Docs/phase-gates/templates.md`
8. ⏸ Lessons-log updates if new failure modes exposed

---

## Carry-forward flags (do NOT action in this phase)

- **V4 PRE-WORK** — `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan. After V3, OLD path body still exists but is fed only by adapter's dual-feed. V4 removes the define + deletes OLD feed call from adapter + deletes CombatRouting + deletes `BuddahPredictedImpulseEventQueue` + LegacyShadow drain in motor.
- **V4 PRE-WORK** — `pendingImpulseSummary` cleanup (channel summary builder OR drop the field).
- **V5 PRE-WORK** — Reconcile-before-RPC double-apply edge case explicit probe under LatencySim (carry-forward from Step 1 Q2).
- **Phase 7** — Visual jitter investigation. Step 1 confirmed jitter is pre-existing (not Step 1 regression). Task #36 already tracking.

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | cowork-reviewer | contract stamped post V2b Step 1 merge (PR #34 @ 3181bc4) |
| Recon | 2026-05-02 | cowork-reviewer | recon report at `agent-exchange/handoff/2026-05-02-phase4b-v3-recon.md`. Independent verification: 4 spot-checks (PushTargetBox usage population, 4 callsite two-tier fallback pattern, motor._impulseEventQueue feed convergence at TryQueueImpulseEvent, PushTargetBox direct rb.AddForce usage). KEY FINDING confirmed: 3 victim categories (V2 Buddah / Legacy Buddah / PushTargetBox debug) make Q0=(B) static router strongly preferred. ONE calibration: recon's "PushTargetBox Rule 7 violation" flag re-categorized as "V4 code-style cleanup" — PushTargetBox is not in prediction stack (no Bootstrap, no PredictionRigidbody, no reconcile), so strict Rule 7 doesn't apply. Q-answer phase authorized with Q0-Q4 lean previews approved. |
| Design | 2026-05-02 | cowork-reviewer | Q-answer doc at `agent-exchange/handoff/2026-05-02-phase4b-v3-design.md`. All 9 numbered items APPROVED: Q0=(B) router file, Q1=(B) dual-feed in router, Q1-sub drop try/catch, Q2=(A) DebugState already done, Q3=(A)-revised no cache target materializes, Q4=(A) new file location, PushHitbox verbose log dropped, fallbackToLegacy field preserved per CLAUDE.md hard-stop, single-line callsite migration. Sub-observations forwarded for PR description: malformed-V2 graceful fallback to Tier 2/3 + dual-defense gate (router + adapter both check IsPredictionModeActive). |
| Implementation | ⏸ | | |
| Smoke | ⏸ | | |
| Verify | ⏸ | | |
| Merge | ⏸ | | |
