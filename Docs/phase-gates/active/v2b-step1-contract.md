# V2b Step 1 — Authority Flip Contract

**Phase ID:** phase4b-v2b-step1
**Branch:** feat/phase4b-v2b-step1-authority-flip (cut from dev @ 49e3c2f)
**Risk:** HIGH (authority flip; gameplay-affecting)
**Status:** DESIGN-QA

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

**Additionally REMOVE early-shadow impulse compare** at motor.cs:426-428 (`BuddahImpulseStep.Run` early call) + motor.cs:1481-1494 (D-LOC impulse cursor + ran-flag compare). These were Phase 3b's pure-functional determinism check on OLD step function; post-flip `_realScratch` is NEW-driven and `_shadowScratch` reads OLD `_impulseEventQueue` with different ID space → cursor would always mismatch → spurious warnings. Kill the dead compare instead of half-stripping. `_shadowImpulseConsumedCount` counter and `_shadowScratch.ImpulseRan/ShadowLastConsumedImpulseId` writes also drop. LEG axis (`_realScratch` vs `_legacyShadowScratch` count + ranFlag) replaces it as sole impulse-correctness signal.

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

## Deliverables

1. Recon report — `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-recon.md` ✅ DONE
2. Design Q&A — `agent-exchange/handoff/<date>-phase4b-v2b-step1-design.md` ⏸ PENDING
3. Implementation — code changes on `feat/phase4b-v2b-step1-authority-flip` ⏸
4. Path A raw log — `agent-exchange/console/raw/<date>-phase4b-v2b-step1-single.log` ⏸
5. Path B raw logs — `<date>-phase4b-v2b-step1-host.log` + `<date>-phase4b-v2b-step1-client.log` ⏸
6. Independent verification report — `agent-exchange/handoff/<date>-phase4b-v2b-step1-verify.md` ⏸
7. PR with full description per template ⏸
8. Lessons-log updates if new failure modes exposed ⏸

---

## Carry-forward flags (do NOT action in this phase)

- **V3 PRE-WORK** — DebugState mirror decision (Phase 4b V2b Step 0 audit C). After Step 1, OLD's `TryApplyServerAuthoritativeImpulse` no longer writes 6 DebugState fields. Inspector debug panel will lose data once V3 cuts callsites. Three options (A: NEW mirrors writes / B: audit-and-drop / C: source-tagged panel) — decide in V3 contract.
- **V3 PRE-WORK** — micro-opt: Adapter + CombatRouting redundant `GetComponent<Bootstrap>`. Pass bootstrap as explicit param in V3.
- **V4 PRE-WORK** — `BUDDAH_PREDICTION_LEGACY_SHADOW` define retirement plan once OLD code can be deleted entirely.
- **L17/L18 cross-ref update** — done as part of Q3 Step 1 implementation, not deferred.
- **V5 PRE-WORK** — explicit reconcile-before-RPC edge case test under LatencySim. Server enqueues impulse, sends reconcile state showing impulse-applied. Network reorder delivers reconcile FIRST, RPC SECOND on client. Risk: RPC arrives, channel enqueues with EventTick ≤ current tick, drain fires AGAIN, double-AddForce. OLD path had same risk theoretically but never observed in 4a/3d/3c (TargetRpc Reliable+Ordered, reconcile arrives ~hundreds of ms later). LatencySim 100-300ms must explicitly probe this. If observed: retrofit "lastConsumedImpulseTick" in reconcile state to gate channel ConsumeReady, or "lastConsumedLogicalId" set-clear pattern. If NOT observed: close out as documented limitation.
- **L19 candidate** — "Authority-flip retest gates compare LEG axis only; pre-flip per-axis D-LOC impulse compare becomes structurally dead and must be removed (not gated) to avoid spurious warnings." Add to lessons-log as part of Step 1 PR.

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 | Yonezawa + cowork-reviewer | contract stamped |
| Recon | 2026-05-02 | cowork-reviewer | recon report verified independently — `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-recon.md` |
| Design | 2026-05-02 | cowork-reviewer | Q0-Q4 design Q&A signed off; Q4 contract AMENDED (cursor compare dropped, early-shadow removed); design doc at `agent-exchange/handoff/<date>-phase4b-v2b-step1-design.md` |
| Implementation | 2026-05-02 | Yonezawa + Claude (impl) | code commit `5c46002` on branch `feat/phase4b-v2b-step1-authority-flip`. 11 production files + lessons-log L19. Q0 LogicalId hybrid dedup, Q1 OLD->LEG drain swap, Q2 no hydration, Q3 inv-/leg- rename, Q4 amended (early-shadow + dead D-LOC impulse compare removed). PredictionRigidbody integrity (Rule 7) verified: 0 direct rb.AddForce/AddTorque calls in Authoritative drain. Awaiting Path A + Path B raw-log smoke per Methodology Rule 1. |
| Smoke | 2026-05-02 | Yonezawa (Editor + Build run) + Claude (scrape) | Path A 30s host-only + Path B 60s 2-peer LAN both completed. 3 raw logs persisted to `agent-exchange/console/raw/2026-05-02-phase4b-v2b-step1-{single,host,client}.log` (17 / 24 / 35 MB). 3 digests at `agent-exchange/console/2026-05-02-phase4b-v2b-step1-{single,host,client}.log`. Path A: 1 push (compared=1 < contract ≥3, but volume gate cleared by Path B); Path B HOST 5 / CLIENT 8 compared. 0 LEG FATAL / 0 D-LOC FATAL / 0 INV leakage / 0 spurious D-LOC impulse warning / 0 DupReject / 0 Read-position-beyond-buffer across all 3 sessions. 8/8 CLIENT Recv lines carry both `eventTick=N` AND `logicalId=N` fields (Step 1 wire format end-to-end). LogicalIds 1..8 sequential confirms adapter monotonic counter. Visual smoke (push lands, PushGrace activates) — pending user-side confirmation. |
| Verify | 2026-05-02 | Claude (independent grep via subagent on raw files) | Verification report `agent-exchange/handoff/2026-05-02-phase4b-v2b-step1-verify.md`. Subagent grepped raw logs directly (not implementer's digest). All 3 sessions strict PASS. Per-peer counter divergence (HOST 5 vs CLIENT 8) explained as expected per-instance topology — what gates is `leg-imp-div=0` per-side, which holds. |
| Merge | ⏸ | | Awaiting visual smoke confirmation + PR review. |
