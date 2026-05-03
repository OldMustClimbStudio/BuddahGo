# V5 — LatencySimulator Terminal Gate + V4 Carry-forward Resolution Contract

**Phase ID:** phase4b-v5
**Branch:** feat/phase4b-v5-latency-terminal-gate (to be cut from dev @ V4 merge commit)
**Risk:** MEDIUM (LatencySim probe may surface edge cases; lobby handshake adds new wire surface)
**Status:** KICKOFF

---

## Scope (locked)

**In scope:**
- TransportManager LatencySimulator enabled at 100ms RTT symmetric (50ms each direction)
- 2-peer LAN smoke under simulated latency: bidirectional pushes ≥3 each direction, 60s+ session
- Reconcile-before-RPC double-apply edge case explicit probe (V5 PRE-WORK Q0 — carried from V2b Step 1 Q2 + V3 + V4)
- Lobby protocol version handshake decision + (if approved) implementation (V5 PRE-WORK Q1 — carried from V4 Q3.4)
- Strict-gate criteria: V4 gates carry-over + LatencySim-specific additions
- Phase 4b CLOSEOUT: marks the end of Phase 4b sub-phase chain (V1 → V5)

**Out of scope:**
- L9 ClampPlanarSpeed fix (Phase 8)
- Phase 7 visual jitter investigation (unblocked by V5 closure but separate phase)
- Phase 6 Teleport / Handoff cut-over (separate channel migration)
- Adapter caching (Phase 8 perf pass)

---

## PRE-WORK questions (must answer in design Q&A phase before implementation)

### Q0 — Reconcile-before-RPC double-apply mitigation strategy
**Carried from V2b Step 1 Q2 + V3 + V4 — final resolution in V5.**

Theoretical risk: server fires impulse at tick T_s, sends reconcile state (showing impulse-applied) at tick T_s+N, sends Target_EnqueueImpulse RPC for the same impulse. Network reorder delivers reconcile FIRST + RPC SECOND on client. RPC handler enqueues to channel with `EventTick=T_s`. Client at tick T_c (T_c > T_s+N) → drain fires → AddForce AGAIN → DOUBLE APPLICATION.

OLD path had same risk theoretically but never observed in 4a/3d/3c/V2b/V3/V4 (TargetRpc Reliable+Ordered + reconcile arrives ~hundreds of ms later than RPC).

Pick:
- (A) **Observe-first, defer mitigation**: run V5 LatencySim 100-300ms probe; if double-apply observed, retrofit mitigation in V5; if not observed, document as resolved (with caveat that future increase in `_shadowPreImpulsePendingSnapshot` reconcile state delivery scope could reintroduce the race).
- (B) **Implement mitigation preemptively** via `lastConsumedImpulseTick` in reconcile state — server includes this in reconcile data; client's ConsumeReady silently consumes-without-applying any entry where `EventTick ≤ lastConsumedImpulseTick`. ~15 LOC.
- (C) **Implement mitigation preemptively** via `lastConsumedLogicalId` set-clear pattern. Server includes recent-consumed LogicalId set; client's channel checks against it.

Recommendation lean: **(A)** — V5 is precisely the probe phase. Do not implement mitigation without evidence; implementer time better spent on probe + lobby handshake.

### Q1 — Lobby protocol version handshake
**Carried from V4 Q3.4 — `predictionProtocolVersion` integer in lobby metadata + connection-time negotiation.**

Pick:
- (A) **Implement in V5** — add integer constant to a shared types file, write to lobby metadata at host-create, read on client-join, kick mismatched joiner with friendly error. ~30 LOC across lobby flow + types.
- (B) **Defer to Phase 8 / dedicated lobby-protocol PR** — V5 stays focused on LatencySim probe.
- (C) **Document as accepted operational risk** — calendar coordination + Steam version lock remains the policy; no code change.

Recommendation lean: **(A)** — V4 Q3.4 explicitly flagged this as V5 PRE-WORK; calendar coordination is a fragile mitigation and gets weaker as team size grows. Cheap insurance.

### Q2 — LatencySim test profile breadth
Pick:
- (A) 100ms RTT only (V5 contract minimum)
- (B) 100 + 200ms (broader observation)
- (C) 100 + 200 + 300ms (terminal stress test)

Recommendation lean: **(A) first, expand to (B) if (A) clean** — fail-fast pattern. If 100ms reveals reconcile-before-RPC double-apply, address before expanding.

### Q3 — Strict-gate additions specific to LatencySim
Beyond V4's gate (D-LOC FATAL=0 / visual smoke / counter sanity / spawn-window probe), what should V5 add?

Pick:
- (A) **No new probe** — existing gates are sufficient under simulated latency
- (B) **Add reconcile-replay-count metric** — count `OnReconcile` callbacks per session, flag if > expected baseline (LatencySim should increase reconcile frequency proportionally; absence indicates LatencySim not actually engaged)
- (C) **Add explicit "double-apply detection" gate** — track per-event consumed count via LogicalId; FATAL if any LogicalId consumed >1 time per peer

Recommendation lean: **(B) + (C)** — both are cheap and address the V5-specific risk class. (B) confirms LatencySim is real, (C) catches the Q0 edge case if it manifests.

### Q4 — Phase 4b closeout actions
V5 = end of Phase 4b sub-phase chain. What must close out when V5 merges?

Pick (multi-select):
- task #24 (Phase 4 umbrella) → completed
- task #36 (Phase 7 prep visual jitter) → unblocked, becomes ready for kickoff
- Phase 5 ↔ V3 reconciliation (task #25) → final disposition decision
- Lessons-log: any V5-specific entries
- Methodology updates: any new sub-clauses surfaced by LatencySim probe
- Phase 7 KICKOFF: ready or wait for explicit user trigger

Recommendation: V5 closeout PR description must list all checkbox items + explicitly tick which are done.

---

## Strict gates (preliminary — finalized after Q3 design)

### Path A — single-machine 30s Editor PlayMode (no LatencySim, baseline)
- All V4 Path A gates inherited (D-LOC FATAL=0 + ClearAll=4 + DupReject=0 + DropFull=0 + spawn-window L7 probe + visual smoke)

### Path B — 2-peer LAN 60s with LatencySim 100ms RTT symmetric
- All V4 Path B gates inherited
- LatencySim engagement verification: reconcile callback count > baseline (per Q3 lean B)
- LogicalId double-apply gate: 0 LogicalIds consumed >1 time per peer (per Q3 lean C)
- Reconcile-before-RPC edge case observation: documented (per Q0 lean A)

### Path B optional — LatencySim 200ms / 300ms (if Q2 expansion approved)
- Same gates as 100ms profile
- Document any new failure modes; if observed, retrofit per Q0 mitigation

---

## Deliverables

1. ⏸ Recon report — `agent-exchange/handoff/<date>-phase4b-v5-recon.md`
2. ⏸ Design Q&A — `agent-exchange/handoff/<date>-phase4b-v5-design.md`
3. ⏸ Implementation — code on `feat/phase4b-v5-latency-terminal-gate`
4. ⏸ Path A raw log — `agent-exchange/console/raw/<date>-phase4b-v5-single.log`
5. ⏸ Path B raw logs (100ms) — `<date>-phase4b-v5-host-100ms.log` + `<date>-phase4b-v5-client-100ms.log`
6. ⏸ Path B raw logs (200/300ms if expanded) — naming TBD per Q2
7. ⏸ Independent verification report — `agent-exchange/handoff/<date>-phase4b-v5-verify.md`
8. ⏸ PR description with metric tables per Template 3
9. ⏸ Lessons-log entries (if any new failure modes)
10. ⏸ Phase 4b closeout summary in PR body (per Q4)

---

## Carry-forward flags (do NOT action in this phase)

- **Phase 6** — Teleport + Handoff channel cut-over (replicate Impulse path's V2b Step 0 → V2b Step 1 pattern for these 2 channels)
- **Phase 7** — Visual jitter quantitative re-evaluation (task #36) — **unblocked when V5 merges**
- **Phase 8** — L9 ClampPlanarSpeed fix + Roslyn analyzer + adapter caching (Q2 deferred from V4)

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-03 | cowork-reviewer | contract stamped post V4 merge (PR #37). Phase 4b closeout phase — final sub-phase before unlocking Phase 7. 4 PRE-WORK Q seeded with carry-forwards from V2b Step 1 / V3 / V4. |
| Recon | 2026-05-03 | cowork-reviewer | recon report at `agent-exchange/handoff/2026-05-03-phase4b-v5-recon.md`. **Independent FULL-grep verification per V4 reflective lesson** (L21 risk-aware): 6 surface claims all 100% match — TransportManager+LatencySimulator API at `Assets/FishNet/Runtime/Managing/Transporting/`, SteamLobbyManager.cs at `Assets/Scripts/Network/Lobby/`, ImpulseQueueState 0 hits in Assets/ (V4 step 7 deletion confirmed), motor.cs:542 [Reconcile] callback site, CombatAdapter:43-46 MarkReady method, LastConsumed{Teleport,Handoff,Modifier,Impulse}Id existing mirror pattern across InputData.cs:14-17 + ReconcileData.cs:22/:26 (Q0-B reuse cleanly aligned). **KEY FINDING 5 (Q3-C drop) ACCEPTED**: Q3-C "per-peer LogicalId consumed >1 time" structurally cannot detect cross-peer reconcile-before-RPC double-apply because reconcile-state path applies impulse to rb DIRECTLY, bypassing channel — channel sees only 1 consume per LogicalId regardless. Q3-C is redundant with existing DupReject for in-channel duplicates AND incapable of detecting the V5 actual risk class. Drop Q3-C; rely on Q0-B (lastConsumedImpulseTick in reconcile state) IF race manifests. **Q lean previews approved**: Q0=(A) observe-first, Q1=(A) implement ~30 LOC, Q2=(A→B) 100ms first then 200ms if clean, Q3=(B retained, C dropped), Q4=closeout flow. **Working tree commit hygiene reminder issued**: V4 closeout files (archive/v4-contract.md + active/v5-contract.md + README + lessons-log L20/L21 + footer additions) currently uncommitted on `feat/phase4b-v4-cleanup` HEAD — must land before V5 IMPLEMENT cuts new branch (per Rule 10; V3 IMPLEMENT already paid this cost via workspace loss + chat-memory recovery). Stage 3 DESIGN-QA authorized post-commit. |
| Design | ⏸ | | |
| Implementation | ⏸ | | |
| Smoke | ⏸ | | |
| Verify | ⏸ | | |
| Merge | ⏸ | | |
