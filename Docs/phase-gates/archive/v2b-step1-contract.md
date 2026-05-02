# V2b Step 1 — Authority Flip Contract (ARCHIVED)

**Phase ID:** phase4b-v2b-step1
**Branch:** feat/phase4b-v2b-step1-authority-flip (cut from dev @ 49e3c2f)
**Risk:** HIGH (authority flip; gameplay-affecting)
**Status:** ✅ MERGED — PR #34 to dev @ 3181bc4 (2026-05-02)

---

## Scope (locked)

**In scope:**
- Flip rb-write authority from OLD `_impulseEventQueue` drain to NEW `CommandBus.ImpulseChannel` ConsumeReady drain.
- OLD code moves under `#if BUDDAH_PREDICTION_LEGACY_SHADOW` to become pure shadow (counter only, no rb / no state mutation).
- 4 gameplay actions (rb.WakeUp, AddForce, AddTorque, ApplyPushGraceFromImpulse) move ENTIRELY to NEW drain.
- HEARTBEAT prefix rename `inv-` → `leg-` (legacy shadow). Update L17/L18 cross-refs.
- Server-stamped EventId added to ImpulseCmd for cross-system cursor.

**Out of scope:**
- V3 (skill site migration from CombatRouting to CombatAdapter direct calls)
- V4 (CombatRouting deletion + PushTargetBox fallback audit)
- V5 (LatencySimulator 100ms RTT 2-peer terminal gate)
- L9 ClampPlanarSpeed fix (Phase 8)

---

## PRE-WORK answers (locked from V2b Step 0 audit + recon sign-off)

### Q0 — Cross-fire dedup mechanism
**Hybrid (single-emit invariant + EventId dedup belt-and-braces).**
Server-stamps cmd-side EventId. Channel TryEnqueue checks recent-IDs against EventId, drops duplicates. 4 bytes/cmd cost.

### Q1 — rb-write integration
**Replace OLD entirely.** NEW drain at OLD's callsite. OLD body moves under `#if BUDDAH_PREDICTION_LEGACY_SHADOW` and **strips all 4 gameplay actions** — only scratch counter writes remain. The 4 gameplay actions (rb.WakeUp, _predictionRigidbody.AddForce/AddTorque, ApplyPushGraceFromImpulse) move ENTIRELY to NEW drain. NEW drain MUST also set `_impulseConsumedThisTick = true` to preserve `FinalizeImpulseDebugAfterSimulate` post-Simulate work.

### Q2 — Reconcile state hydration
**None.** ImpulseQueueState field on BuddahPredictedReconcileData is vestigial (line 31 declared, line 300 `default`, line 643 `_=`). NEW path inherits the OLD model (RPC-fed only, replay-safe via EventTick gate). Design doc must include explicit reconcile-sequence walk-through proving channel and motor state stay aligned.

### Q3 — HEARTBEAT field semantic flip
**Rename `inv-` → `leg-`.** Code-level prefix in HEARTBEAT log strings, field names, comments. DO NOT rename historical raw log filenames in `agent-exchange/console/raw/` (immutable session records). Update L17/L18 cross-refs to note prefix change post-Step 1.

### Q4 — Test gate semantics (AMENDED 2026-05-02 post design Q&A)
**Volume-divergence (cnt mismatch) and ran-flag mismatch (ranNew != ranLeg) remain FATAL.** **Cursor compare is REMOVED**, not retrofitted via LogicalId.

Original contract direction (cross-system cursor via LogicalId) was rejected during design because:
- It would require unifying LogicalId across OLD's `motor._nextImpulseEventId` and NEW's adapter `_nextLogicalId` counters
- That requires CombatRouting refactor to generate one shared ID at routing entry point — explicitly out of scope per V3
- LogicalId on cmd payload is per-peer-channel-instance dedup mechanism (Q0), not cross-path cursor

**Additionally REMOVE early-shadow impulse compare** at motor.cs:426-428 (`BuddahImpulseStep.Run` early call) + motor.cs:1481-1494 (D-LOC impulse cursor + ran-flag compare). Phase 3b's pure-functional determinism check on OLD step function; post-flip `_realScratch` is NEW-driven and `_shadowScratch` reads OLD `_impulseEventQueue` with different ID space → cursor would always mismatch → spurious warnings. Kill the dead compare instead of half-stripping. `_shadowImpulseConsumedCount` counter and `_shadowScratch.ImpulseRan/ShadowLastConsumedImpulseId` writes also drop. LEG axis (`_realScratch` vs `_legacyShadowScratch` count + ranFlag) replaces it as sole impulse-correctness signal.

---

## Strict gates

### Path A — single-machine 30s Editor PlayMode
- `[D-IMP LEG FATAL]` count = 0 (strict)
- `[D-LOC FATAL]` count = 0 (loc / tel / mod / hof axes — impulse axis dropped per Q4 amendment)
- `leg-imp-div=[1-9]` zero hits across full session
- `leg-imp-compared` ≥ 3
- `[CommandBus]:ClearAll` = 4 (single buddah)
- `[CommandBus]:DropFull` = 0
- `[Channel]:DupReject` = 0 baseline (Q0 belt-and-braces gate — non-zero means unexpected double-fire path)
- Zero spurious `[D-LOC] T=... impulse-` warnings (proves Q4 dead compare removal landed)
- Code review: zero direct `rb.AddForce` / `rb.AddTorque` calls in NEW drain (Methodology Rule 7)
- Visual smoke: push lands on host buddah (cube actually moves) + PushGrace activates (DebugState reads in scene)

### Path B — 2-peer LAN 60s (HOST + CLIENT, no LatencySim)
Both ends:
- All Path A criteria
- `leg-imp-compared` ≥ 3 each end
- `[CommandBus]:ClearAll` = 4 × N spawns (per end)
- CLIENT `[CommandBus]:Recv ch=Impulse` ≥ 1 (β path liveness)
- CLIENT Recv line carries both `eventTick=N` AND `logicalId=N` fields (wire-format verification for Q0 LogicalId addition)
- HOST `leg-imp-compared` matches OLD `imp-compared` for HOST-owned victim ticks
- CLIENT `leg-imp-compared` matches OLD `imp-compared` for CLIENT-owned victim ticks

### Path B with LatencySimulator 100ms (V5 prep — optional in Step 1)
Same gates as Path B, plus:
- No new FATAL classes introduced under simulated latency

---

## Final outcome

✅ **STRICT PASS, MERGED.**

- PR #34 → `dev` @ `3181bc4`
- Path A: 1 push (compared=1, OLD ≥3 deferred to Path B per agreement); 0 FATAL on all axes; visual smoke confirmed (push lands, PushGrace activates).
- Path B HOST: 5 compared, 0 FATAL.
- Path B CLIENT: 8 compared, 0 FATAL. 8/8 Recv lines carry `eventTick=N logicalId=N` (wire format end-to-end verified). LogicalIds 1..8 sequential.
- Independent verification report: `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-verify.md`
- L19 added to lessons-log (authority-flip dead-compare rule).
- L18 updated with naming-rename parenthetical.

## Carry-forward delivered to V3 / V4 / V5 (now in their respective contracts)

- V3 PRE-WORK: DebugState mirror — partially solved by Step 1 NEW drain; only `pendingImpulseSummary` remains for V4
- V3 PRE-WORK: GetComponent<Bootstrap> caching micro-opt
- V4 PRE-WORK: `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan
- V5 PRE-WORK: reconcile-before-RPC double-apply edge case test under LatencySim

---

## Sign-off ledger (final)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | Yonezawa + cowork-reviewer | contract stamped |
| Recon | 2026-05-02 | cowork-reviewer | recon report verified independently — `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-recon.md` |
| Design | 2026-05-02 | cowork-reviewer | Q0-Q4 design Q&A signed off; Q4 contract AMENDED (cursor compare dropped, early-shadow removed) |
| Implementation | 2026-05-02 | Yonezawa + Claude (impl) | code commit `5c46002`. 11 production files + L19. Rule 7 PredictionRigidbody integrity verified. |
| Smoke | 2026-05-02 | Yonezawa (Editor + Build run) + Claude (scrape) | 3 raw logs at `agent-exchange/console/raw/2026-05-02-phase4b-v2b-step1-{single,host,client}.log`. Path A 1 push, Path B HOST 5, CLIENT 8. 0 FATAL all axes. Visual smoke confirmed. |
| Verify | 2026-05-02 | cowork-reviewer (independent grep) | Verify report at `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-verify.md`. STRICT PASS confirmed across 3 sessions. |
| Merge | 2026-05-02 | Yonezawa | PR #34 merged to dev @ `3181bc4`. Per-instance topology divergence (HOST 5 vs CLIENT 8 compared) within expected `leg-imp-div=0` gate. |
