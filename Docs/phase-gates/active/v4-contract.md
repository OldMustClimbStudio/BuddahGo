# V4 — CombatRouting Deletion + LEGACY_SHADOW Retirement Contract

**Phase ID:** phase4b-v4
**Branch:** feat/phase4b-v4-cleanup (cut from dev @ V3 merge commit)
**Risk:** LOW-MEDIUM (deletion-heavy; loss of LEG-axis observation safety net is real)
**Status:** VERIFIED (Stages 5+6 PASS — awaiting Stage 7 MERGE approval)

---

## Scope (locked)

**In scope — deletion / retirement:**
- Delete `Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs` entirely (post-V3 [Obsolete], 0 production callers)
- Retire `BUDDAH_PREDICTION_LEGACY_SHADOW` define from ProjectSettings (Standalone + Editor)
- Delete `BuddahPredictedMotor.ConsumePendingImpulseEvents_LegacyShadow` + helper callback
- Delete `_legacyShadowScratch` field on motor + all writers
- Delete `_legacyImpulseDivCount` + `_legacyImpulseComparedCount` counters
- Delete `[D-IMP LEG HEARTBEAT]` + `[D-IMP LEG FATAL]` emit code in `Shadow_CompareAndReport`
- Delete `BuddahPredictedImpulseEventQueue` type (and its `.cs` file)
- Delete `motor.TryApplyServerAuthoritativeImpulse` + `motor.TryQueueImpulseEvent` + `motor.QueueImpulseEventTargetRpc`
- Delete `motor._impulseEventQueue` field
- Delete `BuddahPredictedImpulseRingSnapshot` if it's only referenced by the dead reconcile field — verify in recon
- Delete reconcile state's vestigial `ImpulseQueueState` field (V2b Step 1 Q2 confirmed vestigial — unused but still in struct)

**In scope — Router cleanup post-LEGACY_SHADOW retirement:**
- Strip the `#if BUDDAH_PREDICTION_LEGACY_SHADOW` block in `BuddahPredictionRouter.RouteImpulse` Tier 1 (the OLD-feed line `motor.TryApplyServerAuthoritativeImpulse` call). Tier 1 reduces to single adapter call.

**In scope — V3 carry-forward items:**
- `pendingImpulseSummary` resolution (PRE-WORK Q0)
- `BuddahPredictionImpulseDebugBox.fallbackToLegacy` dead serialized field removal (PRE-WORK Q1)
- Adapter caching micro-opt re-evaluation (PRE-WORK Q2)
- Strict gate recalibration without LEG axis (PRE-WORK Q4)

**Out of scope:**
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation
- Phase 6 Teleport / Handoff cut-over

---

## PRE-WORK questions (must answer in design Q&A phase before code)

### Q0 — `pendingImpulseSummary` resolution
V3 Step 1's NEW drain writes `pendingImpulseCount` from channel.Count, but `pendingImpulseSummary` was deferred — channel had no `BuildPendingSummary()` analogue to OLD's queue. Inspector value currently shows `_impulseEventQueue` contents (which V4 deletes).

Pick:
- (A) **Add summary builder to channel** — implement `BuildPendingSummary()` on `BuddahPredictionEventChannel<T>`, NEW drain writes both fields. Inspector keeps full info post-V4.
- (B) **Drop `pendingImpulseSummary` field** — audit DebugState consumers (Inspector / HUD / scene scripts), if no critical reader, delete the field. Inspector loses the summary string but keeps count.
- (C) **Stub-only** — leave field but write empty string post-V4. Avoids deleting public surface but leaves dead field.

Recommendation lean: (A) if cheap; (B) if consumers are minimal. Settle in design Q&A.

### Q1 — `BuddahPredictionImpulseDebugBox.fallbackToLegacy` removal
V3 sign-off preserved this serialized field per CLAUDE.md hard-stop. With LEGACY_SHADOW retiring, the field has no semantic — Tier 3 Legacy fallback in Router stays unconditional, the toggle gates nothing.

Pick:
- (A) **Remove the field** — V4's broader cleanup pass naturally releases the hard-stop (the field's reason to exist is gone). Inspector users editing the box lose the toggle but it was no-op anyway.
- (B) **Keep as marked-obsolete dev field** — `[Obsolete]` attribute, comment explaining no-op. Lower-disruption.

Recommendation lean: (A). The CLAUDE.md hard-stop exists to prevent serialized field removal from breaking existing scenes / prefabs. With LEGACY_SHADOW gone, the field is genuinely dead, not "potentially still useful" — removal is now correct, not premature.

### Q2 — Adapter caching opportunity
V3 Q3-revision deferred caching since adapter didn't expand. Post-V4, Router's Tier 1 reduces to a single adapter call. Adapter still does GetComponent<Bootstrap> per fire (the IsPredictionModeActive defensive gate).

Pick:
- (A) **Cache bootstrap reference inside adapter** at first successful call (lazy-init pattern). Saves 1 GetComponent per impulse fire.
- (B) **Defer to Phase 8 perf cleanup** — V4 is cleanup-heavy already, don't conflate scopes.

Recommendation lean: (B). V4 is destructive enough; perf opt belongs in a focused pass with profiler measurements.

### Q3 — Define retirement procedure
`BUDDAH_PREDICTION_LEGACY_SHADOW` is referenced in:
- ProjectSettings/ProjectSettings.asset (Standalone + Editor define lists)
- Multiple `#if BUDDAH_PREDICTION_LEGACY_SHADOW` blocks in motor.cs, Router.cs, etc.

Pick:
- (A) **Strip define from ProjectSettings + delete `#if/#endif` wrapper code in same commit** — clean break. PR description verifies grep returns 0 hits for define name post-merge.
- (B) **Two-step**: first strip define from ProjectSettings (forces compile errors / dead code), fix all errors, then delete `#if` wrappers. Lower-risk but messier diff.

Recommendation lean: (A). The grep gate at PR review verifies completeness; (B)'s "intentional compile error" pattern is harder to review.

### Q4 — Strict gate recalibration
Post-V4, LEG axis disappears (no `_legacyShadowScratch` to compare against). Path A / Path B gates need new safety net.

Pick:
- (A) **D-LOC FATAL only** — locomotion / teleport / modifier / handoff axes remain. Impulse axis was already dropped in V2b Step 1 Q4 amendment; nothing new to remove. Strict gates simplify to D-LOC FATAL=0 + visual smoke + counter sanity check.
- (B) **Add NEW probe** — design a fresh observation layer (e.g., compare NEW drain count vs adapter enqueue count). Cost: design + impl effort.
- (C) **Keep LEG axis but invert authority understanding** — confusing, deprecated.

Recommendation lean: (A). V5 LatencySim probe is the next safety check. V4 → V5 should be quick succession, no need for extra observation in V4.

---

## Strict gates (preliminary — finalized after Q4 design)

Per Q4 lean (A):

### Path A — single-machine 30s Editor PlayMode
- `[D-LOC FATAL]` count = 0 (loc / tel / mod / hof axes — impulse axis dropped in Step 1 Q4 amendment; LEG axis dropped in V4)
- `[CommandBus]:ClearAll` = 4 (single buddah)
- `[CommandBus]:DropFull` = 0
- `[Channel]:DupReject` = 0 baseline
- 0 references to `BUDDAH_PREDICTION_LEGACY_SHADOW` define remaining (grep verification)
- 0 references to deleted symbols (`_legacyShadowScratch` / `ConsumePendingImpulseEvents_LegacyShadow` / `BuddahPredictionCombatRouting` / `BuddahPredictedImpulseEventQueue` / `TryApplyServerAuthoritativeImpulse` etc.)
- 0 spurious `[D-IMP LEG ...]` log lines (proves LEG axis fully retired)
- Methodology Rule 7 verified: zero direct `rb.AddForce` / `rb.AddTorque` calls in code paths still standing
- Methodology Rule 1 sub-rule (D): post-smoke event sanity check confirms ≥3 actual impulse hits landed
- Visual smoke: push lands on host buddah + PushGrace activates

### Path B — 2-peer LAN 60s
- All Path A criteria
- `[CommandBus]:ClearAll` = 4 × N spawns per end
- CLIENT `[CommandBus]:Recv ch=Impulse` ≥ 1 (β path liveness)
- CLIENT Recv line carries both `eventTick=N` AND `logicalId=N` fields

### Methodology compliance
- Raw log on disk per Rule 1 (clear/rename Editor.log before smoke per session-scoping sub-rule)
- Rule 1-D: confirm ≥3 actual hits landed before dumping
- Rule 2: independent reviewer grep, prefer downstream evidence over upstream stacks (trust hierarchy sub-clause)
- Rule 5: lessons-log update in same commit if any new failure mode surfaces
- Rule 7: PredictionRigidbody integrity (NEW drain unchanged from Step 1 — should remain compliant)
- Rule 10: commit immediately, no untracked-only state across sessions

---

## Deliverables

1. ⏸ Recon report — `agent-exchange/handoff/<date>-phase4b-v4-recon.md`
2. ⏸ Design Q&A — `agent-exchange/handoff/<date>-phase4b-v4-design.md`
3. ⏸ Implementation — code changes on `feat/phase4b-v4-cleanup`
4. ⏸ Path A raw log — `agent-exchange/console/raw/<date>-phase4b-v4-single.log`
5. ⏸ Path B raw logs — `<date>-phase4b-v4-host.log` + `<date>-phase4b-v4-client.log`
6. ⏸ Independent verification report — `agent-exchange/handoff/<date>-phase4b-v4-verify.md`
7. ⏸ PR description with metric tables per Template 3
8. ⏸ Lessons-log: probably 0 new entries (cleanup phase)

---

## Carry-forward flags (do NOT action in this phase)

- **V5 PRE-WORK** — Reconcile-before-RPC double-apply edge case explicit probe under LatencySim 100ms (carried from V2b Step 1 Q2 + V3)
- **Phase 7** — Visual jitter quantitative re-evaluation (task #36) — unblocked once V5 closes
- **Phase 8** — L9 ClampPlanarSpeed fix + Roslyn analyzer + adapter caching (deferred from V4 PRE-WORK Q2)
- **Phase 5 ↔ V3 reconcile** — original Phase 5 "Skill adapter cut over" scope vs V3+V4 actual delivery; revisit after V4 closes

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | cowork-reviewer | contract stamped post V3 merge (PR #36). Cleanup scope locked + 4 PRE-WORK questions seeded from V3 carry-forward. |
| Recon | 2026-05-03 | cowork-reviewer | recon report v2 at `agent-exchange/handoff/2026-05-03-phase4b-v4-recon.md` (commit a502f1d). v1 ran independently verified for 5 items (100% match) but reviewer challenged 7 missing/wrong items: 1 CRITICAL (L7 latch field name + disposition error — risked spawn-window impulse drop regression invisible to strict gate / visual smoke), 3 HIGH (BuddahImpulseStep orphan + snapshot cascade trace + comment cleanup surface), 2 MEDIUM (refactor-plan chapters + wire format peer coordination). v2 amendments processed all 7 via Item 2.3 (L7 latch corrected — STRIP wrapper KEEP body), Item 1.5 (snapshot cascade chain documented end-to-end), Item 1.6 (13-row comment cleanup table + post-merge grep mandate), Item 5.4 (5-point deployment coordination plan), Item 6 (4-chapter disposition with lean (b) footer). Reviewer re-verified cascade chain via 8 spot-checks (BuddahImpulseStep:30, TickContext.cs:24/:65/:94, motor.cs:99/:420/:1379, BuildTickContext callers :425/:489/:597) — 100% match. Q-answer phase authorized with CRITICAL Q3 caveat: motor.cs:382-396 wrapper strip + body keep + spawn-window smoke probe added to strict gate. |
| Design | 2026-05-03 (REVOKED v1) → 2026-05-03 (RE-SIGNED v2) | cowork-reviewer | v1 design (commit 52556c1) had MEDIUM-severity factual error at Q3.1 line 121 ("NOT in any LEGACY_SHADOW block" claim contradicted by independent grep at motor.cs:143-149). Sign-off revoked, design v2 (commit ebb47b4) requested with 3 amendments. **v2 verification (full claim-by-claim grep per risk:HIGH discipline, not sampled)**: (1) Q3.1 :143-149 BEFORE/AFTER snippet line-matched against actual code (DELETE 2 #if/#endif, KEEP 5 lines comment+field — math correct); (2) Q3.1 line 121 parenthetical replaced with correct cross-ref ("inside #if block at :143-149 — strip wrapper per second BEFORE/AFTER"); (3) Q3.3 step 5 expanded to 8-class top-level deletion list with explicit L7 latch wrapper-strip cross-ref to Q3.1; (4) Q3.3 steps 1+4 wording fixed ("Compile clean" + correct rationale). One non-blocking wording nit: step 5 says "12 #if regions on motor" but actual is 10 (other 2 in Router.cs + CombatRouting.cs); Item 2.2 table is accurate so implementer following row-by-row gets correct outcome. Stage 4 IMPLEMENT authorized. **Reflective note for system**: trust-but-verify second pass caught what first pass missed (Q3.1 factual error). For risk:HIGH phases, future reviewers should default to FULL claim-by-claim grep verification at Stage 3 sign-off, not spot-check sampling. Methodology Rule 2 sub-clause candidate; defer to V4 closeout for formal lesson capture (avoid mid-flight methodology churn). |
| Implementation | 2026-05-03 | cowork-reviewer | PR #37 with 4 commits: d46d9bf (Q0 channel BuildPendingSummary + 3 motor writer retargets — CC3 enforcement), b66c53b (Q1 fallbackToLegacy removal + Steps 1-4 BuddahImpulseStep orphan + TickContext snapshot chain + motor field/writer), 258b472 (Steps 5-6: motor LEGACY_SHADOW + top-level deletions + Router #if strip + queue file delete), b4ea50c (Steps 7-12: ImpulseQueueState + RingSnapshot + ProjectSettings strip + CombatRouting + comments + 3 docs footers). **Independent FULL-grep verification per risk:HIGH discipline (per V4 reflective lesson)**: all 17 dead identifier strings return 0 hits in Assets/* (1 exception: motor.cs:398 historical breadcrumb comment "V2b Step 1 Q4 / V4: BuddahImpulseStep early-shadow call retired (L19)" — text-only, harmless). **Q3.1 L7 latch CRITICAL verification PASSED**: both motor.cs:124-127 (field decl region, was :143-149) AND motor.cs:358-368 (latch body region, was :382-396) confirmed no `#if/#endif` wrappers; `_combatAdapterInitialized` field + `bootstrap.CombatAdapter.MarkReady()` call survive unconditionally. **CC3 sequencing PASSED**: channel `BuildPendingSummary()` at `BuddahPredictionEventChannel.cs:162`; 3 motor writer sites (motor.cs:211/:1738/:2198) all retarget to channel with null-fallback safety. **ProjectSettings strip PASSED**: `BUDDAH_PREDICTION_LEGACY_SHADOW` 0 hits in `ProjectSettings.asset`. Stage 5 SMOKE authorized. |
| Smoke | 2026-05-03 | Yonezawa | Path A (Host-only single peer, ~80s, 4.7k tick session): user fired ChargedHandProjectile at DebugboxCanPush. Path B (2-peer LAN, ~150s): local Editor=CLIENT, remote machine=HOST; bidirectional pushes via PushHitbox + ChargedProjectile + DebugBox. Editor.log cleared per Methodology Rule 1 sub-rule A on all 3 logs (single Initialize block each). Raws on disk: `agent-exchange/console/raw/2026-05-03-phase4b-v4-{single,host,client}.log`. Both peers built from V4 commit hash (atomic deployment per Q3.4 satisfied — wire format change deployed simultaneously). |
| Verify | 2026-05-03 | cowork-reviewer | Independent FULL-grep verification (per risk:HIGH discipline + V4 reflective lesson — not sampled spot-check) across all 3 raws (279 HEARTBEAT rows total: 40+132+107). All strict gates met: 0 LEG/LOC FATAL / 0 retired-symbol runtime stack frames / 0 DupReject / 0 DropFull / 0 runtime exceptions / 0 compile errors during runtime. **Cross-peer Tier 1 verification:** HOST PushHitbox Hit owner=0 `impulse=(66.00, 0.00, 75.13)` → CLIENT Recv #1 `linear=(66.00, 0.00, 75.13) eventTick=6460 logicalId=1` — **EXACT impulse-vector match** end-to-end. **L7 latch correctness PROVEN** via Recv liveness + monotonic logicalIds 1→2→3→4 (would be 0 if `#if BUDDAH_PREDICTION_LEGACY_SHADOW` strip broke `MarkReady()` call). Spawn-window 60-tick distance gate: NOT strictly met (5986 ticks), partial-pass-with-note per Q3.2 contract — user did not push within first ~5s spawn window; latch correctness signal stands independent. **Anomalies documented:** (A1) CLIENT 4 CS2001 errors at lines 46357-46363 = pre-recompile noise (Unity standard cold-start dance after `git rm` of .cs files; Tundra immediately recovered with `ExitCode: 0`). Cross-validated CLIENT actually ran V4 code via wire format compatibility + 0 LEG-axis logs + 0 schema mismatch. NOT a violation. (A2) Spawn-window 60-tick partial-pass per design tolerance. Verify report: `agent-exchange/handoff/2026-05-03-phase4b-v4-verify.md`. **Lessons-log proposals queued for V4 closeout:** (1) CS2001 post-`git rm` recovery pattern; (2) Methodology Rule 2 full-grep mandate for risk:HIGH phases. |
| Merge | ⏸ | | (awaiting user approval — PR #37, atomic deployment per Q3.4) |
