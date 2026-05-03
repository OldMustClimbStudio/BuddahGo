# V5 — LatencySimulator Terminal Gate + V4 Carry-forward Resolution Contract (ARCHIVED)

**Phase ID:** phase4b-v5
**Branch:** feat/phase4b-v5-latency-terminal-gate (cut from V4-corrective HEAD; rebased on dev post PR #38 + #39 merge)
**Risk:** MEDIUM (LatencySim probe + lobby handshake wire surface)
**Status:** ✅ MERGED — PR #40 to dev (2026-05-03). **Phase 4b sub-phase chain (V1 → V5) COMPLETE.**

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
- **Phase 7** — Visual jitter quantitative re-evaluation (task #36) — **unblocked, awaits user trigger**
- **Phase 8** — L9 ClampPlanarSpeed fix + Roslyn analyzer + adapter caching (Q2 deferred from V4)
- **V6 / Phase 7 prep candidate** — Q2 200ms LatencySim broader observation (eligible per Q2-B trigger; deferred per user direction at V5 closeout)
- **Q0-B retrofit** — preemptive code shape preserved in `agent-exchange/handoff/2026-05-03-phase4b-v5-design.md` for if-observed pivot (race NOT observed at 100ms, but future regimes may surface it)

---

## Final outcome

✅ **STRICT PASS, MERGED. Phase 4b CLOSEOUT complete.**

- PR #40 → `dev` (2026-05-03), commit `4b60773` (IMPLEMENT) + `b53c383` (SMOKE+VERIFY+ledger)
- 4 IMPLEMENT files +43/-4 LOC: PredictionProtocol.cs new + SteamLobbyManager.cs Q1 inserts + BuddahPredictedMotor.cs Q3-B (field + ReconcileState `++` + 2 HEARTBEAT format updates) + ledger
- 407 HEARTBEAT rows clean across 3 sessions (142 Path A + 212 Path B HOST + 53 Path B CLIENT)
- Cross-peer Tier 1 chain verified via exact impulse-vector match HOST→CLIENT for logicalId 1+2

### Smoke results (3 sessions, 0 metric divergence vs digests)

| Path | HBs | LOC FATAL | LEG | ClearAll | Recv (CLIENT) | rec-cb max | Notes |
|---|---|---|---|---|---|---|---|
| Path A baseline (HOST role) | 142 | 0 | 0 | 16 | 4 (HOST observation) | 0 | structurally enforced HOST baseline |
| Path B HOST (100ms LatencySim) | 212 | 0 | 0 | 8 | 0 | 0 | 13 PushHitbox Hit / 14 Router stack |
| Path B CLIENT (100ms LatencySim) | 53 | 0 | 0 | 8 | **16** monotonic 1→16 | **2595** | **Q3-B engagement gate 519× exceeded** |

### Q gate verdicts

- **Q0** — reconcile-before-RPC race NOT observed at 100ms (per Q0-A defer); Q0-B preemptive blueprint preserved for future-regime pivot
- **Q1** — lobby protocol-version handshake LIVE verified (v1↔v1 join + 16 cross-peer Recvs); pre-V5 builds hard-rejected by design
- **Q2** — 100ms baseline STRICT PASS; 200ms expansion eligible but deferred per user direction at closeout
- **Q3** — Q3-B reconcile-callback-count probe DECISIVELY validated (rec-cb=2595 vs HOST baseline 0). Q3-C DROPPED per RECON KEY FINDING 5 (channel-internal LogicalId tracking structurally cannot detect cross-peer Q0 race)
- **Q4** — Phase 4b closeout actions executed at this contract's archival commit

### Anomalies (all documented / accepted)

- **A1**: 4 CS2001 cold-start errors at lines 46357-46363 → Tundra recompile `ExitCode: 0`. Per L20: non-issue.
- **A2**: Spawn-window 60-tick L7 distance gate partial-pass (5658 ticks vs 60-tick gate) — third occurrence (V4 Path B + V5 Path A + V5 Path B). Recv liveness + monotonic logicalId + rec-cb=2595 jointly prove latch correctness. Promoted to methodology.md Rule 11 in this closeout commit.
- **A3**: raw log file-name role swap (`host-100ms.log` content is CLIENT; `client-100ms.log` content is HOST). Determined unambiguously via role markers (rec-cb / PushHitbox Hit / StartHost vs StartClient counts). Digests named correctly per content; raw filename rename is housekeeping.

### Lessons added (this PR cycle, lifted from full Phase 4b chain)

- **L20** (added in V4 closeout PR #38): `git rm` of source files produces transient CS2001 "Source file could not be found" compile errors that recover automatically.
- **L21** (added in V4 closeout PR #38): Risk:HIGH phases require FULL claim-by-claim grep verification + negative claims demand independent positive verification.
- **L22** (added in V4 corrective PR #39, born during V5 IMPLEMENT staging): Negative-claim verification MUST query `git show HEAD:<path>` / `git status --short` / `git diff origin/<base>` — NOT Read/Grep on working tree. + methodology.md Rule 2 sub-clause "Verification target — git plumbing over working tree" + Stage 6 VERIFY pre-grep gate.

### Methodology rule added (this closeout commit)

- **Rule 11** — SMOKE driver hard-precondition for time-sensitive probes (born from A2's third partial-pass occurrence; spawn-window 60-tick gate require driver-confirmed early push BEFORE PlayMode entry, otherwise abort run).

### Phase 4b chain closeout actions (per Q4 6-condition list)

1. ✅ task #24 (Phase 4 umbrella) → COMPLETED via Phase 4b chain V1→V5
2. 🔓 task #36 (Phase 7 visual jitter prep) → unblocked; KICKOFF awaits user trigger (per Q4-2 lean b)
3. ✅ Phase 5 ↔ V3 reconciliation → grep `Assets/Scripts/Buddah/ComboSkill/` for `BuddahPredictionCombatRouting | TryApplyServerAuthoritativeImpulse | BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc | ConsumePendingImpulseEvents_LegacyShadow` returned **0 hits** → Phase 5 marked **COMPLETE** by V3+V4 cumulative work
4. ✅ Lessons-log → no new V5-specific lessons (existing rules covered all cases; L20/L21/L22 from V4-cycle work)
5. ✅ Methodology updates → Rule 11 added (spawn-window driver precondition)
6. ⏸ Phase 7 KICKOFF → defer per Q4-2 lean b; user explicit trigger required

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-03 | cowork-reviewer | contract stamped post V4 merge (PR #37). Phase 4b closeout phase — final sub-phase before unlocking Phase 7. 4 PRE-WORK Q seeded with carry-forwards from V2b Step 1 / V3 / V4. |
| Recon | 2026-05-03 | cowork-reviewer | recon report at `agent-exchange/handoff/2026-05-03-phase4b-v5-recon.md`. **Independent FULL-grep verification per V4 reflective lesson** (L21 risk-aware): 6 surface claims all 100% match — TransportManager+LatencySimulator API at `Assets/FishNet/Runtime/Managing/Transporting/`, SteamLobbyManager.cs at `Assets/Scripts/Network/Lobby/`, ImpulseQueueState 0 hits in Assets/ (V4 step 7 deletion confirmed), motor.cs:542 [Reconcile] callback site, CombatAdapter:43-46 MarkReady method, LastConsumed{Teleport,Handoff,Modifier,Impulse}Id existing mirror pattern across InputData.cs:14-17 + ReconcileData.cs:22/:26 (Q0-B reuse cleanly aligned). **KEY FINDING 5 (Q3-C drop) ACCEPTED**: Q3-C "per-peer LogicalId consumed >1 time" structurally cannot detect cross-peer reconcile-before-RPC double-apply because reconcile-state path applies impulse to rb DIRECTLY, bypassing channel — channel sees only 1 consume per LogicalId regardless. Q3-C is redundant with existing DupReject for in-channel duplicates AND incapable of detecting the V5 actual risk class. Drop Q3-C; rely on Q0-B (lastConsumedImpulseTick in reconcile state) IF race manifests. **Q lean previews approved**: Q0=(A) observe-first, Q1=(A) implement ~30 LOC, Q2=(A→B) 100ms first then 200ms if clean, Q3=(B retained, C dropped), Q4=closeout flow. **Working tree commit hygiene reminder issued**: V4 closeout files (archive/v4-contract.md + active/v5-contract.md + README + lessons-log L20/L21 + footer additions) currently uncommitted on `feat/phase4b-v4-cleanup` HEAD — must land before V5 IMPLEMENT cuts new branch (per Rule 10; V3 IMPLEMENT already paid this cost via workspace loss + chat-memory recovery). Stage 3 DESIGN-QA authorized post-commit. |
| Design | 2026-05-03 | cowork-reviewer | Q-answer doc at `agent-exchange/handoff/2026-05-03-phase4b-v5-design.md`. **Independent FULL-grep verification per V4 lesson L21**: all 4 SteamLobbyManager line-level claims (:95/:417/:454/:457-462) verified line-by-line against actual code; all 4 Q1 insertion points precise. Q3-B HEARTBEAT emit sites at motor.cs:1274/1287 verified — current format does NOT contain `rec-cb` (insertion targets clean). GameNetworkManager + RoomStateManager grep'd for `protocol\|version` returned 0 hits — untouched claim verified. Q0-B preemptive code shape is structurally sound (mirrors existing LastConsumedTeleportId/HandoffId pattern from recon Surface 6); minor uint-wraparound observation noted as IF-IMPLEMENTED concern (LogicalId would need ~4 billion impulses to wrap, practically never in session). **All 6 numbered items APPROVED**: Q0-A defer + Q0-B blueprint ready, Q1-A SteamLobbyManager-only + PredictionProtocol.cs new file ~35 LOC, Q2 100ms baseline + (B) conditional, Q3-B retained + Q3-C dropped per KEY FINDING 5, Q4 closeout 6-condition list. **Branch base decision**: reviewer recommends (b) proceed-on-current — closeout commit is Docs-only on `feat/phase4b-v4-cleanup`, V5 IMPLEMENT is conflict-free with PR #38 review timeline. CC1-CC4 cross-cutting concerns sound. Stage 4 IMPLEMENT authorized. |
| Implementation | 2026-05-03 | cowork-reviewer | PR #40 (Draft) at commit `4b60773`. 4 files +43/-4 LOC: PredictionProtocol.cs new + SteamLobbyManager.cs Q1 inserts (:95/:417/:454/:457-466) + BuddahPredictedMotor.cs Q3-B (`_reconcileCallbackCount` field + `++` in ReconcileState + 2 HEARTBEAT format updates) + this contract ledger. CRITICAL post-mortem mid-flight: V4 PR #37 partial-merge state discovered (6 files of unstaged deletions never reached HEAD); resolved via fix/phase4b-v4-corrective PR #39 + L22 + methodology Rule 2 sub-clause "Verification target — git plumbing over working tree". V5 branch rebased post-corrective-merge. |
| Smoke | 2026-05-03 | Yonezawa (driver) + Claude (scrape) | 3 raw logs at `agent-exchange/console/raw/2026-05-03-phase4b-v5-{single,host-100ms,client-100ms}.log` (single=22MB, file-named-host=37MB CONTENT-IS-CLIENT, file-named-client=57MB CONTENT-IS-HOST per role markers). 3 digests at `agent-exchange/console/2026-05-03-phase4b-v5-*.log`. Path A baseline (HOST role, 2-peer no-LatencySim, 142 HBs, rec-cb=0 structurally). Path B HOST (212 HBs, 13 PushHitbox Hits, 14 Router stack frames, rec-cb=0 expected). Path B CLIENT (53 HBs, 16 Recv monotonic logicalId 1→16, **rec-cb=2595** under 100ms LatencySim — Q3-B engagement gate 519× exceeded). A1 CS2001 cold-start per L20 + Tundra ExitCode:0. A2 spawn-window 60-tick partial-pass 3rd occurrence. A3 file-name role swap (raw rename recommended for archival). |
| Verify | 2026-05-03 | cowork-reviewer | Independent FULL-grep verification per L21 risk-aware + L22 git-plumbing primary self-application. Verify report at `agent-exchange/handoff/2026-05-03-phase4b-v5-verify.md`. All 9 metric classes match implementer digests 100%. Cross-peer Tier 1 chain verified via exact impulse-vector match HOST→CLIENT for logicalId 1+2 (`(83.34, 0.00, -55.27)` and `(-67.63, 0.00, 73.67)`). L7 latch survived V4 corrective merge (16 Recvs would be 0 if broken). Q0 race NOT manifested at 100ms (0 DupReject + 16 monotonic consumes). Q1 protocol-version handshake LIVE verified (v1↔v1 join successful, 16 cross-peer Recvs would be 0 if Q1 broke). Q3-B gate ABSOLUTELY EXCEEDED. STRICT PASS. Q2 200ms expansion eligible per design Q2-B trigger criteria; user decides whether to run optionally. |
| Merge | 2026-05-03 | Yonezawa | PR #40 merged to dev at commit `14e4757`. Q3.4 atomic deployment satisfied (no peer mid-playtest at merge time; Q1 protocol-version handshake adds future-proof guard against V5↔pre-V5 cross-version joins). Phase 4b sub-phase chain (V1 → V2a → V2a fix → V2b Step 0 → V2b Step 1 → V3 → V4 → V5) fully COMPLETE. |
