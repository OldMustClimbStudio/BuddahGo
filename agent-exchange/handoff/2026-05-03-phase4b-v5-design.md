# phase4b-v5 — Design Q&A

**Recon reference:** [`agent-exchange/handoff/2026-05-03-phase4b-v5-recon.md`](2026-05-03-phase4b-v5-recon.md)
**Branch:** feat/phase4b-v5-latency-terminal-gate (cut from feat/phase4b-v4-cleanup HEAD @ 6d6322f; equivalent to dev once PR #38 merges)
**Status:** DESIGN PROPOSAL — no code changes yet
**Risk (contract):** MEDIUM — Q0/Q1 add wire surface; LatencySim probe may surface edge cases

---

## Q0 — Reconcile-before-RPC double-apply mitigation strategy

**Picked:** **(A) Observe-first under LatencySim 100ms; defer mitigation. Q0-B preemptive code shape designed and ready for "if race observed" pivot.**

**Justification:**
- Theoretical risk has not been observed across V1/V2a/V2b/V3/V4 smoke (TargetRpc Reliable+Ordered semantics + reconcile arrives ~hundreds of ms after RPC in the no-LatencySim baseline).
- V5 IS the probe phase — the entire reason it exists is to see what shows up at 100ms RTT. Implementing mitigation preemptively spends implementer time on a problem we have no evidence of.
- Q0-B retrofit cost is bounded (~10-15 LOC; mirrors existing `LastConsumedTeleportId`/`LastConsumedHandoffId` pattern verified at recon Surface 3). If observed, V5 (or V6) lands the fix in a single follow-up commit.
- Per V5 contract Q0 verbatim: _"Recommendation lean: (A) — V5 is precisely the probe phase. Do not implement mitigation without evidence."_

**Q0-B preemptive code shape (READY for "if observed" pivot — DO NOT implement in V5 IMPLEMENT phase unless Stage 5/6 surfaces evidence):**

```csharp
// In Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs
// Insert at line 23, between LastConsumedTeleportId and PendingHandoff:

public uint LastConsumedImpulseLogicalId;  // Q0-B mitigation. 0u = unset sentinel.

// In BuddahPredictedReconcileData ctor (line ~31-41):
// (Add ctor parameter + assignment, OR rely on FishNet auto-init + server-side
//  CreateReconcile producer assignment — TBD by implementer.)
```

```csharp
// In Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs reconcile-state producer
// (CreateReconcile method — line not yet read; locate by grep for "new BuddahPredictedReconcileData(")
// SERVER ONLY — populate from authority-side channel state:

reconcileData.LastConsumedImpulseLogicalId =
    bootstrap?.CommandBus?.ImpulseChannel?.LastConsumedLogicalId ?? 0u;
```

```csharp
// In Assets/Scripts/New_Buddah/Events/BuddahPredictionEventChannel.cs
// Add public accessor (line ~62, next to LastConsumedId):

private uint _lastConsumedLogicalId;
public uint LastConsumedLogicalId => _lastConsumedLogicalId;

// Update RememberLogicalId (~:183) to also write _lastConsumedLogicalId = logicalId;
```

```csharp
// In ConsumeReady (channel.cs:107-131) — add gate before callback invocation:
// CLIENT-side replay: skip-without-applying any entry whose LogicalId
// has already been consumed authoritatively per reconcile state.
//
// Caller signature change: pass uint reconcileGuard (= server-reported
// LastConsumedImpulseLogicalId). Pass 0u to disable gate (server-side calls).

public int ConsumeReady(uint currentTick, uint reconcileGuard, ConsumeCallback callback)
{
    int consumed = 0;
    for (int i = 0; i < _pending.Count; )
    {
        Entry entry = _pending[i];
        if (entry.EventTick > currentTick) { i++; continue; }
        if (reconcileGuard != 0u && IsAlreadyConsumedAuthoritatively(entry.LogicalId, reconcileGuard))
        {
            // Silent skip-without-apply: dedup against authority.
            _pending.RemoveAt(i);
            RememberLogicalId(entry.LogicalId);
            // do NOT invoke callback — entry was applied authoritatively already.
            // Optional: Debug.Log($"[Channel]:Q0Skip logicalId={entry.LogicalId} guard={reconcileGuard}");
            continue;
        }
        if (!callback(in entry)) { i++; continue; }
        _pending.RemoveAt(i);
        RememberLogicalId(entry.LogicalId);
        _lastConsumedId = entry.Id;
        consumed++;
    }
    return consumed;
}

private static bool IsAlreadyConsumedAuthoritatively(uint logicalId, uint reconcileGuard)
{
    // LogicalId is per-adapter monotonic. Any logicalId <= reconcileGuard was
    // applied authoritatively per the latest reconcile state.
    return logicalId <= reconcileGuard;
}
```

**Risk (Q0-A defer):**
- If race manifests under LatencySim 100ms = retrofit lag of one PR cycle.
- Mitigation: Q3-B reconcile-replay-count probe (this design Q3) confirms LatencySim is engaged; if engaged + no observable double-apply, we have positive evidence not just absence-of-evidence.

**Open questions:** none — Q0-B blueprint is concrete enough to land same-week if needed.

---

## Q1 — Lobby protocol-version handshake

**Picked:** **(A) Implement in V5. Insertion point: SteamLobbyManager-only (per recon Surface 2 + Q1 line-level investigation below). GameNetworkManager / RoomStateManager / PropertiesSelectionManager untouched.**

**Justification:**
- V4 Q3.4 explicitly flagged this as V5 PRE-WORK. Calendar-coordination of wire-format changes is fragile and gets weaker as team grows.
- Recon-derived insertion blueprint estimates ~25-35 LOC at clean structurally-existing seams.
- Wire-format change in V5 is contingent on Q0 outcome — if Q0-A defer + no other V5 wire change lands, `predictionProtocolVersion` bump is still cheap forward-insurance for V6+.

**Line-level disposition per file (per user directive: 4 files):**

### Assets/Scripts/Network/Lobby/SteamLobbyManager.cs (PRIMARY EDIT — code change)

| Line | Change | Detail |
|---|---|---|
| `:95` (after `:94` `KEY_MAX_PLAYERS`) | INSERT | `public const string KEY_PREDICTION_PROTOCOL_VERSION = "l_pv";` |
| `:417` (after `:416` `lobby.SetData(KEY_MAX_PLAYERS, ...)` in CreateLobbyAsync) | INSERT | `lobby.SetData(KEY_PREDICTION_PROTOCOL_VERSION, PredictionProtocol.Version.ToString());` |
| `:454` (after `:453` `string hostSteamId = lobby.GetData(KEY_HOST_STEAM_ID)` in JoinLobbyAsync) | INSERT | Read remote: `string remotePvStr = lobby.GetData(KEY_PREDICTION_PROTOCOL_VERSION);` |
| `:457-462` (existing `if (string.IsNullOrWhiteSpace(hostSteamId))` block) | DUPLICATE-PATTERN | Add parallel block before line 457: parse `remotePvStr` → mismatch → `lobby.Leave()` + `FireJoinFailed($"Prediction protocol mismatch: host={remotePv}, you={PredictionProtocol.Version}. Update your client.")`; missing-key → graceful "treat as v0 mismatch" |
| `:21-33` (LobbyListItemData struct) | OPTIONAL INSERT | `public bool IsPredictionProtocolMatch;` field |
| `:626-642` (BuildLobbyListItemData) | OPTIONAL INSERT | Read + parse + compare; AND-clause into `IsJoinable` at `:641` |

**~25-35 LOC total** matches Q1-A contract estimate.

### Assets/Scripts/Network/PredictionProtocol.cs (NEW FILE — required)

```csharp
namespace SteamMultiplayer.Network
{
    /// <summary>
    /// Prediction wire-format protocol version.
    /// Bump this integer EVERY TIME a serialized field is added/removed from
    /// BuddahPredictedReconcileData, BuddahPredictedReplicateData, any [Reconcile]
    /// or [Replicate] payload, or any TargetRpc enqueue payload.
    ///
    /// History:
    ///   v1 — Phase 4b V5 baseline (post-V4 wire format: ImpulseQueueState removed,
    ///        eventTick + logicalId added to ImpulseCmd, LEGACY_SHADOW define retired).
    /// </summary>
    public static class PredictionProtocol
    {
        public const int Version = 1;
    }
}
```

**Why a new file:** keeps the constant out of any subsystem that might be conditionally-compiled or removed in a later phase. Single source of truth grep-searchable.

### Assets/Scripts/Network/Core/GameNetworkManager.cs (NO CODE CHANGE)

Investigated lines `:101-161` (StartHost/StartClient): pure NetworkManager wrappers. Protocol-version check fires upstream of this in SteamLobbyManager.JoinLobbyAsync, BEFORE StartClient is invoked at SteamLobbyManager.cs:475. **Conclusion: untouched.**

### Assets/Scripts/Network/Room/RoomStateManager.cs (NO CODE CHANGE)

Grepped for `OnLobbyJoined|JoinLobby|version|protocol` → 0 matches. RoomStateManager is post-connection scene state; no lobby-metadata path. **Conclusion: untouched.** If post-connection version-disagreement detection is desired in a future phase (defense-in-depth), file would be candidate; out of scope for V5.

### Assets/Scripts/Network/Session/PropertySelection/PropertiesSelectionManager.cs (NO CODE CHANGE)

Grepped for `protocol|version|OnConnect` → 0 matches. Post-connection session-level state. **Conclusion: untouched.**

**Risk (Q1):**
- **R1**: missing-`KEY_PREDICTION_PROTOCOL_VERSION` interpretation. Pre-V5 hosts have no `l_pv` key → joiner reads empty string. **Resolution:** treat empty/parse-fail as v0 (= mismatch with v1). Pre-V5 builds are not version-compatible with V5 builds anyway (wire format MAY differ if Q0-B retrofits later). Friendlier UX: "Lobby is on an older prediction protocol. Both sides need V5+ build."
- **R2**: Steam metadata 8KB cap. Adding one short integer key (~10 bytes) → no risk.
- **R3**: lobby-list filter blocks legitimate joins if version-compatible host runs slightly different protocol. **Resolution:** integer mismatch is intentionally hard-block (UX clearly explains).

**Open questions:** none.

---

## Q2 — LatencySim test profile breadth

**Picked:** **(A) 100ms RTT only as V5 mandatory gate; (B) 100 + 200ms expansion CONDITIONAL on (A) clean.**

**Justification:** fail-fast pattern. If 100ms reveals reconcile-before-RPC double-apply (Q0 race), address in V5 follow-up before exploring 200ms broader regime.

**100ms baseline gate criteria (V5 required):**
- All V4 strict gates inherited (`[D-LOC FATAL]=0`, `[D-IMP LEG ...]=0`, `ClearAll=4×N`, `DupReject=0`, no exceptions)
- Spawn-window 60-tick distance gate **strict-met** (per V5 smoke instruction: peer-2 push CLIENT buddah within 3-5s of CLIENT spawn)
- Reconcile-replay-count metric (Q3-B) > V4 baseline (proves LatencySim engaged)
- LogicalId monotonic + 0 DupReject (carry-over from V4)

**200ms expansion trigger criteria (CONDITIONAL):**
- All 100ms baseline gates **PASS strict** AND
- Stage 6 reviewer signs off 100ms profile clean AND
- ≥10 cross-peer impulse Recvs observed at 100ms with no anomaly

If ANY 100ms gate fails or partial-passes → DO NOT expand to 200ms in V5; surface as either:
- (a) Q0-B retrofit (if reconcile-before-RPC race observed), V5 follow-up commit
- (b) defer 200ms to V6 (if other anomaly outside Q0 scope)

**Risk:**
- 100ms profile may not stress-test enough to surface Q0 race. **Mitigation:** Q3-B reconcile-callback-count (this design Q3) is the sanity check that LatencySim is actually engaged. 0 reconcile callbacks at 100ms = LatencySim not running, retest after fix.
- 300ms regime not tested in V5. Acceptable — V5 is the FIRST LatencySim probe; 300ms terminal stress lives in V6 or Phase 8.

**Open questions:** none.

---

## Q3 — LatencySim-specific strict-gate additions

**Picked:** **(B) Q3-B reconcile-replay-count probe RETAINED. (C) Q3-C double-apply detection DROPPED — see KEY FINDING 5 in recon.**

### Q3-B — reconcile-replay-count probe (RETAINED)

**Code shape (~3 LOC + 2 emit-site updates):**

```csharp
// In Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
// Field declaration — locate in private fields region, near _shadowActiveCompares
//   (V5 implementer to find via grep "private uint _shadowActiveCompares" then add neighbor):
private uint _reconcileCallbackCount;

// In ReconcileState (motor.cs:542-544, increment at top before any return):
[Reconcile]
private void ReconcileState(BuddahPredictedReconcileData data, Channel channel = Channel.Unreliable)
{
    _reconcileCallbackCount++;  // V5 Q3-B — confirms LatencySim engaged.
    if (_predictionRigidbody == null || data.RigidbodyState == null)
        return;
    // ... existing body unchanged ...
}
```

```csharp
// In HEARTBEAT emit at motor.cs:1287 — append `rec-cb={count}`:
Debug.Log($"[D-LOC HEARTBEAT] T={tickHb} active-ticks={_shadowActiveCompares} skip-ticks={_shadowSkipCompares}\n  loc-div={_dLocLocomotionDivCount} tel-div={_dLocTeleportDivCount} mod-div={_dLocModifierDivCount} hof-div={_dLocHandoffDivCount}\n  tel-compared={_shadowTeleportConsumedCount} mod-compared={_shadowModifierConsumedCount} hof-compared={_shadowHandoffConsumedCount} rec-cb={_reconcileCallbackCount}");

// Mirror in idle-path emit at motor.cs:1274.
//
// Cumulative-since-spawn semantics — DO NOT reset per-HB. Cumulative growth
// across a session is the diagnostic signal (LatencySim engaged → growth rate
// proportional to latency).
```

**Strict-gate criterion:**
- Path A baseline (no LatencySim): `rec-cb` non-zero is acceptable (FishNet may fire reconcile for other corrections); just observe.
- Path B 100ms LatencySim: `rec-cb` final value > Path A `rec-cb` final value (per-HB-rate or final-row comparison both acceptable). If Path B `rec-cb` ≈ Path A `rec-cb` = LatencySim NOT engaged. HARD FAIL — do not proceed to verify.
- Specific gate text for V5 contract amendment: _"Path B `rec-cb` final-row value MUST exceed Path A `rec-cb` final-row value by ≥2× OR ≥5 callbacks (whichever is greater). Failure indicates LatencySim Inspector toggle didn't persist or Editor hot-reload bypass — investigate before retest."_

### Q3-C — DROPPED per KEY FINDING 5 in recon

**Decision:** Q3-C ("0 LogicalIds consumed >1 time per peer") is DROPPED because:
1. **Same-peer-instance double-consume is already covered** by `[Channel]:DupReject` (channel.cs:75-90). V4 strict gate `DupReject = 0` carry-over to V5 covers this.
2. **Cross-peer Q0 race is structurally invisible to channel-level LogicalId tracking.** The Q0 race scenario = server applies once + client receives reconcile-state showing applied + RPC delivers + client channel enqueues + ConsumeReady fires + client applies AGAIN. From CLIENT channel's perspective: SINGLE Recv → SINGLE consume → SINGLE callback fire = no DupReject. The double-apply manifests as **rb-state divergence vs. authoritative reconcile baseline**, not channel-level dedup signal.
3. Detecting cross-peer Q0 race requires either:
   - Q0-B reconcile-state gate (`LastConsumedImpulseLogicalId` skip) — preempts the race, no detection probe needed
   - rb-velocity/position divergence probe under reconcile — heavy instrumentation, only meaningful as post-hoc correctness check + signals are downstream of the race itself

**V5 disposition:**
- Rely on Q0-A defer + Q3-B reconcile-replay-count for engagement verification
- Rely on visual smoke + V4-inherited gates for behavioral correctness
- If reconcile-before-RPC race observed at 100ms (Q0 evidence), Q0-B preemptive code shape (this design Q0) lands as V5 follow-up OR V6 — no Q3-C instrumentation will help detect it any more reliably than the Q3-B engagement signal + visual smoke jointly already do

**Cross-ref:** recon Surface 5 KEY FINDING.

**Open questions:** none.

---

## Q4 — Phase 4b closeout actions

V5 = end of Phase 4b sub-phase chain (V1 → V5). PR description must explicitly tick each closeout item.

**Closeout actions list:**

### 1. Task #24 (Phase 4 umbrella) → MARK COMPLETED
Phase 4 "Locomotion + Impulse cut over" was decomposed into V1-V5; V5 merge closes it. PR body line: _"Phase 4 umbrella (task #24) → completed. Decomposition V1-V5 covered all original scope."_

### 2. Task #36 (Phase 7 visual jitter prep) → UNBLOCK DECISION
V5 contract says task #36 _"unblocks when V5 merges"_. Closeout decision required:
- **(a) IMMEDIATELY KICKOFF Phase 7** post-V5 merge — implementer ready; baseline jitter quantification design + recon
- **(b) DEFER Phase 7** until user explicit trigger — V5 closeout PR notes "Phase 7 unblocked, awaiting user kickoff"

**Recommended lean:** (b) — Phase 7 is a new investigation domain (visual layer + PredictionSmoother migration), not directly continuous with 4b. User-triggered kickoff matches phase-gate ownership pattern (reviewer-stamped contract).

### 3. Phase 5 ↔ V3 reconciliation (task #25) → FINAL DISPOSITION DECISION
README.md says: _"Original Phase 5 'Skill adapter cut over' overlaps significantly with Phase 4b V3 'Skill site migration to CombatAdapter'. After V4 closes (4b done), revisit whether Phase 5 has additional scope (e.g., SkillExecutor internal migration) or should be marked complete by V3+V4 work."_

V5 closeout decision required:
- **(a) MARK COMPLETE** — V3+V4 covered Phase 5 scope; remove Phase 5 row from roadmap or mark ✅ with crossref
- **(b) RESCOPE** — Phase 5 retains residual SkillExecutor work; document residual scope

**Recommended lean:** (a) MARK COMPLETE pending implementer confirmation that no residual SkillExecutor migration paths exist post-V4. V5 implementer to grep `Assets/Scripts/Buddah/ComboSkill/` for any remaining `BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc` or pre-V3 site references; if 0 → (a). If any → (b) with explicit residual scope list.

### 4. Lessons-log V5-specific entries (CONDITIONAL)
**Default:** no V5-specific entries — V5 is observation-phase using V4-established methodology.

**Triggers for new lesson:**
- Q0 race observed under 100ms LatencySim → L22 candidate (reconcile-before-RPC race fingerprint + Q0-B mitigation pattern)
- Q3-B reveals LatencySim engagement edge case → L23 candidate
- Q1 protocol-version handshake exposes Steam metadata edge case → L24 candidate

**Lesson commit discipline (carry-over):** lessons land in same commit as the implementation that exposed them, per V3 lessons.

### 5. Methodology updates (CONDITIONAL)
**Default:** no updates — V4 reflective discipline (L21 full-grep mandate + negative-claim positive verification) covered V5's risk surface.

**Triggers:**
- LatencySim probe surfaces methodology gap (e.g., latency-sensitive verification needs new sub-rule)
- Q0 race observation reveals current process didn't catch it → process improvement candidate

### 6. Phase 7 KICKOFF readiness — DEFAULT WAIT
Per Q4-2 lean (b): Phase 7 KICKOFF awaits explicit user trigger post-V5 merge. V5 closeout PR body line: _"Phase 7 (visual jitter, PredictionSmoother) unblocked. Awaiting user trigger for KICKOFF."_

**Open questions:** none — closeout is process-only, surfaces when V5 merges.

---

## Cross-cutting concerns

### C1 — Q0/Q1 wire-format coupling
If Stage 5 LatencySim 100ms surfaces Q0 race AND Q0-B retrofit lands in V5 follow-up commit:
- ReconcileData wire format changes (new uint field) → `predictionProtocolVersion` MUST bump (1 → 2)
- Q1's `KEY_PREDICTION_PROTOCOL_VERSION` write/read flips automatically (constant integer in PredictionProtocol.cs)
- All deployed builds must rebuild with v2; lobby-list filter automatically rejects v1↔v2 cross-joins
- **Atomic deployment** procedure per V4 Q3.4: confirm no peer mid-playtest before merging Q0-B retrofit PR

If Q0-A defer + no other V5 wire change lands → `predictionProtocolVersion` stays at v1; V5 merge is wire-stable.

### C2 — V5 IMPLEMENT scope summary
Code surface is small:
1. `Assets/Scripts/Network/PredictionProtocol.cs` — NEW, ~10 LOC
2. `Assets/Scripts/Network/Lobby/SteamLobbyManager.cs` — Q1 inserts ~25-35 LOC
3. `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` — Q3-B inserts ~3 LOC + 2 HEARTBEAT format string updates
4. **TransportManager LatencySim toggle** — Inspector flip + `_latency = 50` on NetworkManager scene asset (no Assets/Scripts edit)
5. (Conditional) Q0-B retrofit if Stage 5 surfaces evidence — ~15 LOC across motor + channel + ReconcileData + PredictionProtocol bump

Estimate: ~40-50 LOC core + Inspector tweak. Risk: MEDIUM but bounded.

### C3 — Stage 5 SMOKE procedure prerequisites
Before Stage 5 user-driven smoke runs:
1. Confirm `feat/phase4b-v5-latency-terminal-gate` branch is on dev-equivalent base (post-PR-#38 merge OR rebase if needed)
2. Confirm LatencySim Inspector toggle persists across Editor restart (`_enabled = true`, `_latency = 50`, `_simulateHost = true`)
3. Confirm Q3-B `rec-cb` field appears in HEARTBEAT log lines on a dry-run before 100ms test
4. SMOKE driver instruction (mandatory): peer-2 push CLIENT buddah within 3-5s of CLIENT spawn — ensures spawn-window 60-tick distance gate strict-met (V4 partial-pass remediation)

### C4 — V4 closeout PR #38 dependency
V5 branch was cut from `feat/phase4b-v4-cleanup` HEAD (6d6322f), which contains the closeout commit but is NOT on origin/dev yet (PR #38 still open). Two paths:
- **(a) Wait for PR #38 to merge** then rebase V5 branch on updated dev. Clean history.
- **(b) Proceed with V5 IMPLEMENT on current branch** — the closeout commit is process-only (Docs only); rebasing on dev once #38 merges is conflict-free.

**Recommended:** (a) IF PR #38 merges within 24h (typical reviewer turnaround). (b) IF user wants V5 IMPLEMENT to start immediately.

---

## Awaiting sign-off

Reviewer must approve before V5 IMPLEMENT phase:
1. **Q0-A defer + Q0-B preemptive code shape preserved** for "if observed" pivot
2. **Q1-A SteamLobbyManager-only protocol-version handshake** with PredictionProtocol.cs new file (~35 LOC total)
3. **Q2 100ms baseline + (B) expansion conditional** on clean
4. **Q3-B reconcile-replay-count probe** at motor.cs:542 + HEARTBEAT format string append + Q3-C explicitly DROPPED with KEY FINDING 5 cross-ref
5. **Q4 closeout actions list** with leans: task #24 close / task #36 defer-to-user / Phase 5↔V3 mark-complete-pending-grep / lessons-log conditional / methodology conditional / Phase 7 KICKOFF defer
6. **Branch base decision** (C4 path (a) wait-for-#38 OR (b) proceed-on-current)

Sign-off signal: reviewer comments "Stage 3 SIGN-OFF — Stage 4 IMPLEMENT authorized" + branch base path selection.
