# V4 — CombatRouting Deletion + LEGACY_SHADOW Retirement Contract (ARCHIVED)

**Phase ID:** phase4b-v4
**Branch:** feat/phase4b-v4-cleanup (cut from dev @ 06882bb — V3 PR #36 merge)
**Risk:** LOW-MEDIUM (deletion-heavy; loss of LEG-axis observation safety net)
**Status:** ✅ MERGED — PR #37 to dev (2026-05-03)

---

## Scope (locked)

**In scope — deletion / retirement:**
- `BuddahPredictionCombatRouting.cs` (entire file deleted)
- `BUDDAH_PREDICTION_LEGACY_SHADOW` define retired from ProjectSettings + 12 `#if` blocks across 3 files
- Motor: `_legacyShadowScratch` + counters + `ConsumePendingImpulseEvents_LegacyShadow` + helper + LEG FATAL/HEARTBEAT emit code
- Motor: `_impulseEventQueue` field + `TryApplyServerAuthoritativeImpulse` + `TryQueueImpulseEvent` + `QueueImpulseEventTargetRpc` (top-level non-`#if`)
- `BuddahPredictedImpulseEventQueue.cs` (file deleted)
- `BuddahPredictedReconcileData.ImpulseQueueState` field + `BuddahPredictedImpulseRingSnapshot.cs` (file deleted)
- Snapshot chain: `BuddahImpulseStep.cs` (file deleted) + `TickContext.ImpulsePendingSnapshot` field/param/assign + `_shadowPreImpulsePendingSnapshot` field/writer
- L7 latch wrapper strip at motor.cs:143-149 + :382-396 (KEEP body — `_combatAdapterInitialized` field + `MarkReady()` call survive unconditionally)
- `BuddahPredictionImpulseDebugBox.fallbackToLegacy` serialized field removed
- Q0 channel BuildPendingSummary added; 3 motor writer sites retargeted (CC3 sequencing)
- Refactor-plan footer added to chapters 02 / 09 / 12 (Q3.5)

**Out of scope:**
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation
- Phase 6 Teleport / Handoff cut-over

---

## PRE-WORK answers (from design Q&A v2 — all 4 picks + Q3 5 sub-sections APPROVED)

- **Q0** = (A) Add `BuildPendingSummary()` to channel
- **Q1** = (A) Remove `fallbackToLegacy` field
- **Q2** = (B) Defer adapter caching to Phase 8
- **Q3** = (A) Atomic deletion with 5 sub-sections:
  - **Q3.1** L7 latch CRITICAL: strip wrapper at BOTH :143-149 + :382-396, KEEP body
  - **Q3.2** Spawn-window probe added to strict gate
  - **Q3.3** 12-step cascade deletion order
  - **Q3.4** 5-point deployment coordination plan (atomic + Steam build + lobby handshake V5 PRE-WORK + rollback + PR mandate)
  - **Q3.5** Refactor-plan footer (lean (b))
- **Q4** = (A) D-LOC FATAL only + visual smoke + counter sanity + spawn-window probe

---

## Final outcome

✅ **STRICT PASS, MERGED.**

- PR #37 → `dev` (2026-05-03)
- 4 implementation commits + 1 closeout commit (`ed34937`)
- 17 dead identifier strings: 0 hits in Assets/* runtime code (1 historical breadcrumb comment at motor.cs:398, harmless)
- Q3.1 L7 latch CRITICAL verification PASSED at runtime via cross-peer Tier 1 chain (CLIENT log :144885 — `linear=(66.00, 0.00, 75.13) eventTick=6460 logicalId=1` — exact triple-field match with HOST hit)
- Wire format change (`ImpulseQueueState` removed from reconcile struct) deployed atomically per Q3.4

### Smoke results (3 sessions, 279 HEARTBEAT rows, 100% digest match)

| Path | HBs | LEG/INV FATAL | LOC FATAL | ClearAll | Recv (CLIENT) | Notes |
|---|---|---|---|---|---|---|
| Path A (single, ~80s) | 40 | 0 | 0 | 4 | n/a | 1 spawn |
| Path B HOST (~150s) | 132 | 0 | 0 | 8 | n/a | 2 spawns |
| Path B CLIENT (~150s) | 107 | 0 | 0 | 12 | 4 (all eventTick+logicalId) | 3 spawns + CS2001 cold-start dance recovered |

### Anomalies (all documented / accepted)
- **A1**: CLIENT 4 CS2001 errors at lines 46357-46363 = pre-recompile Unity cold-start dance after `git rm` of CombatRouting/BuddahImpulseStep/BuddahPredictedImpulseEventQueue files. Tundra ExitCode: 0 immediate recovery. Cross-validated CLIENT actually ran V4 code via wire format compatibility + 0 LEG-axis logs + 0 schema mismatch. NOT a violation. Promoted to lessons-log L20.
- **A2**: Spawn-window 60-tick distance probe partial-pass (5986 ticks) — user did not push within first ~5s spawn window. Per Q3.2 "partial-pass with note" design tolerance; cross-peer Tier 1 chain at CLIENT log :144885 independently proves L7 latch correctness. NOT a latch defect.

### Lessons added (new entries in same closeout commit)
- **L20**: CS2001 post-`git rm` recovery pattern (compile-time evidence vs runtime live references distinction)
- **L21**: Risk:HIGH phase full-grep verification mandate + negative claims demand positive verification

---

## Carry-forward delivered to V5 / Phase 6 / Phase 7 / Phase 8

- **V5 PRE-WORK**: Reconcile-before-RPC double-apply edge case explicit probe under LatencySim 100ms (carried since Step 1 Q2)
- **V5 PRE-WORK**: Lobby protocol version handshake (`predictionProtocolVersion` integer in lobby metadata + connection-time negotiation) — V4 deferred per Q3.4 plan
- **Phase 7**: Visual jitter quantitative re-evaluation (task #36) — unblocked once V5 closes
- **Phase 8**: Adapter caching opportunity (Q2 deferred) + L9 ClampPlanarSpeed + Roslyn analyzer

---

## Sign-off ledger (final)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | cowork-reviewer | contract stamped post V3 merge |
| Recon | 2026-05-03 | cowork-reviewer | recon v2 (commit a502f1d) after v1 reviewer challenge: 1 CRITICAL L7 latch field name + disposition error, 3 HIGH (BuddahImpulseStep orphan + snapshot cascade trace + comment surface), 2 MEDIUM (refactor-plan + wire format coordination). v2 verified via 8 spot-checks line-by-line. |
| Design | 2026-05-03 (REVOKED v1 → RE-SIGNED v2) | cowork-reviewer | v1 (commit 52556c1) had factual error at Q3.1 line 121 caught by user's trust-but-verify second pass. v2 (commit ebb47b4) added second BEFORE/AFTER for :143-149 region + corrected parenthetical + Q3.3 step 5 expansion + steps 1+4 wording fix. v2 verified via FULL claim-by-claim grep per risk:HIGH discipline (lesson L21 born here). |
| Implementation | 2026-05-03 | cowork-reviewer | PR #37 with 4 commits (d46d9bf Q0+CC3, b66c53b Q1+Steps1-4, 258b472 Steps5-6, b4ea50c Steps7-12). 17 dead identifiers verified 0 runtime hits. Q3.1 L7 latch wrapper strip + body keep applied to BOTH :124-127 and :358-368 (lines shifted post-deletion). |
| Smoke | 2026-05-03 | Yonezawa (driver) + Claude (scrape) | 3 raw logs at `agent-exchange/console/raw/2026-05-03-phase4b-v4-{single,host,client}.log` (commit ed34937). 279 total HBs / 4 CLIENT Recv lines all carry both `eventTick=N` AND `logicalId=N`. CLIENT 4 CS2001 cold-start dance documented as A1. |
| Verify | 2026-05-03 | cowork-reviewer | Independent FULL-grep verification (per risk:HIGH discipline + V4 reflective lesson). All 9 metric classes match 100%. Cross-peer Tier 1 chain at CLIENT :144885 PROVES L7 latch survived. STRICT PASS. Verify report at `agent-exchange/handoff/2026-05-03-phase4b-v4-verify.md`. |
| Merge | 2026-05-03 | Yonezawa | PR #37 merged to dev. Q3.4 atomic deployment satisfied (no peer was on dev with V3 wire format at merge time). |
