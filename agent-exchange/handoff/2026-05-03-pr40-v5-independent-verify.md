# PR #40 — phase4b-v5 — Independent Stage 6 VERIFY (cowork-reviewer)

**Date:** 2026-05-03
**Reviewer:** cowork-reviewer (Claude Opus 4.7, harness)
**Branch:** `feat/phase4b-v5-latency-terminal-gate` @ `b53c383` (origin tip; local ref file is corrupt — verify operates on origin ref via git plumbing per L22)
**Base:** `dev` (post PR #39 merge `3e3b4f0`)
**Verify discipline:** L22-compliant — `git show <ref>:<path>` + raw-log independent grep (NOT trust-digest, NOT Read on working tree as primary)
**Reviewer note:** v5-contract committed at `b53c383` already pre-fills the Implementation/Smoke/**Verify** rows under "cowork-reviewer" signature. Per Phase Gate System discipline, the Verify-row CONTENT is a *claim I must independently validate*, not a sign-off I provided. This document is the actual independent verify.

---

## Result: ✅ STRICT PASS — sign off Stage 6, authorize merge

All 9 V4-inherited gates clean across all 3 logs; Q3-B engagement gate exceeded 519× the design threshold; cross-peer Tier 1 chain verified via exact byte-level impulse-vector match for logicalIds 1 and 2; Q1 protocol-version handshake LIVE-VERIFIED via downstream evidence (16 cross-peer Recvs); Q0 reconcile-before-RPC race NOT manifested at 100ms (0 DupReject + 16 monotonic logical IDs).

Two documentation issues to correct before merge: (1) A3 anomaly note in Smoke ledger is itself incorrect (file content matches file names); (2) A2 spawn-window 60-tick probe is now a 3rd-recurrence pattern — candidate lesson L23.

---

## Stage A — git plumbing verify of V5 IMPLEMENT (commit `4b60773`)

### A.1 PredictionProtocol.cs (Q1 new file)

```
git show origin/feat/phase4b-v5-latency-terminal-gate:Assets/Scripts/Network/PredictionProtocol.cs
→ public static class PredictionProtocol { public const int Version = 1; }
  + history doc + bump policy
```
✓ Matches design Q1-A.

### A.2 SteamLobbyManager.cs (Q1 4 insertion points)

```
git show <ref>:Assets/Scripts/Network/Lobby/SteamLobbyManager.cs | grep -n "PREDICTION_PROTOCOL\|PredictionProtocol\|l_pv"
:96   "// See PredictionProtocol.cs for version semantics + bump policy."
:97   public const string KEY_PREDICTION_PROTOCOL_VERSION = "l_pv";
:420  lobby.SetData(KEY_PREDICTION_PROTOCOL_VERSION, PredictionProtocol.Version.ToString());
:458  string remotePvStr = lobby.GetData(KEY_PREDICTION_PROTOCOL_VERSION);
:473  if (!remotePvParsed || remotePv != PredictionProtocol.Version)
:477  FireJoinFailed($"Prediction protocol mismatch: host={remoteLabel}, you={PredictionProtocol.Version}. ...");
```
✓ All 4 design-spec sites present (line numbers shifted from design's :95/:417/:454/:457-462 by +1-16 due to KEY constant insertion above the originally-cited anchor — expected).

### A.3 BuddahPredictedMotor.cs (Q3-B 3 sites)

```
git show <ref>:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | grep -n "_reconcileCallbackCount\|rec-cb"
:97-99  doc comment for _reconcileCallbackCount field
:100    private uint _reconcileCallbackCount;
:550    _reconcileCallbackCount++;          // top of [Reconcile]
:1280   HEARTBEAT idle-format includes  rec-cb={_reconcileCallbackCount} (both sides idle)
:1293   HEARTBEAT active-format includes rec-cb={_reconcileCallbackCount}
```
✓ All 3 sites match design Q3-B.

### A.4 Stage 6 pre-grep gate (L22 self-application)

```
git status --short -- <V5 scope: 3 src + 1 contract + 6 logs/handoff>
→ working-tree drift: SteamLobbyManager.cs, motor.cs, v5-contract.md
```

Drift discriminated:
| File | Working-tree size | HEAD size | Δ | Diagnosis |
|---|---|---|---|---|
| `SteamLobbyManager.cs` | 29,254 | 30,463 | −1,209 | mount truncates last ~1.2KB (file ends mid-token `private void Handl`) |
| `motor.cs`             | 112,990 | 113,501 | −511 | mount truncates last ~511B (ends mid-token `> ti`) |
| `v5-contract.md`       | 10,878 | 13,096 | −2,218 | mount truncates last 2 ledger rows (ends mid-token `cross-cutting concer`) |

HEAD content via `git show <ref>:<path> | tail -5` confirms files end with proper `}` / `}` / Merge-row table footer. Drift is verification-machine artifact (Linux container reading large Windows-mounted files), NOT project drift. **Stage 6 pre-grep gate effectively PASSES** — HEAD content is the source of truth per L22, and HEAD content is intact.

This actually *exercises* L22's value: had verification used Read on working tree as primary source, the truncated views would have hidden the table termination + falsely flagged code-completeness. Git plumbing (`git show <ref>:<path>`) bypasses the mount layer entirely.

---

## Stage B — Independent raw-log grep (V4-inherited gates)

| Gate | Path A (single, 142 HBs) | Path B HOST (212 HBs) | Path B CLIENT (53 HBs) | Pass? |
|---|---:|---:|---:|:---:|
| `D-LOC FATAL` | 0 | 0 | 0 | ✓ |
| `D-IMP LEG/INV FATAL` | 0 | 0 | 0 | ✓ |
| `leg-imp-div=[1-9]` | 0 | 0 | 0 | ✓ |
| `inv-imp-div=[1-9]` | 0 | 0 | 0 | ✓ |
| `DropFull` | 0 | 0 | 0 | ✓ |
| `DupReject` | 0 | 0 | 0 | ✓ |
| `[CommandBus]:ClearAll` | 16 (4 spawns × 4) | 8 (2 spawns × 4) | 8 (2 spawns × 4) | ✓ |
| `FirstInvoke` | 1 | 0 | 1 | ✓ ≤1 per channel |
| `schema mismatch / version mismatch` | 0 | 0 | 0 | ✓ |

All 9 strict-gate metrics clean across all 3 logs.

---

## Stage C — V5-specific gate verification

### C.1 Q3-B reconcile-callback engagement (LatencySim active proof)

| Log | rec-cb final | HBs | rec-cb / HB ratio |
|---|---:|---:|---:|
| Path A single (no LatencySim) | 0 | 142 | 0.00 |
| Path B HOST 100ms | 0 | 212 | 0.00 (server-self [Reconcile] is no-op — expected) |
| Path B CLIENT 100ms | **2595** | 53 | **48.96** |

Q3-B threshold per design: `>=5` triggers PASS. Actual: **2595 = 519× threshold**. LatencySim 100ms is overwhelmingly engaged on the client side. Threshold ABSOLUTELY EXCEEDED.

### C.2 Q1 protocol-version handshake (LIVE-VERIFIED via downstream evidence)

```
grep "Prediction protocol mismatch" host-100ms.log client-100ms.log
→ 0 hits in both files
```

Q1 success path is silent by design (only FireJoinFailed emits a log on mismatch). Downstream evidence:
- Client log `[Lobby] Joining 109775243814677295...` then `[Lobby] Joined "My Room" | Host=76561199070814639` → join completed (no kick)
- 16 cross-peer `[CommandBus]:Recv` events on client logicalIds 1→16 → impulse delivery is functional (would be 0 if Q1 hard-blocked join)

Q1 is functionally proven. The "no logs" outcome is the *expected v1↔v1 success state*.

### C.3 Q0 reconcile-before-RPC race (NOT MANIFESTED)

Per design Q0-A "observe-first":
- 0 `DupReject` events on client (channel never saw a duplicate-LogicalId attempt)
- 16 monotonic logicalIds 1→16 with no gaps or repeats
- 0 `D-LOC FATAL` on impulse-vector mismatch

Q0 race NOT observed at 100ms. Per design: this defers Q0-B preemptive mitigation; Q0-B blueprint remains in design doc for retrofit if any future LatencySim profile (200/300ms) surfaces the race.

### C.4 Cross-peer Tier 1 chain (L7 latch + adapter MarkReady proof)

Independent grep produces EXACT byte-level vector match for first two impulses:

| Source | Line evidence |
|---|---|
| HOST `[PushHitbox] Hit` | `impulse=(83.34, 0.00, -55.27)` |
| CLIENT `[CommandBus]:Recv` | `linear=(83.34, 0.00, -55.27) ... eventTick=6720 logicalId=1` |
| HOST `[PushHitbox] Hit` | `impulse=(-67.63, 0.00, 73.67)` |
| CLIENT `[CommandBus]:Recv` | `linear=(-67.63, 0.00, 73.67) ... eventTick=7487 logicalId=2` |

These are not just close — they're identical floating-point strings. This proves:
1. Server-authority Tier 1 push correctly fires PushHitbox.Hit
2. Server emits TargetEnqueueImpulse with exact server-tick payload
3. Client receives + dedups + binds to LogicalId monotonically
4. L7 latch (adapter MarkReady) survived V4 corrective merge AND V5 IMPLEMENT (impulse delivery would never reach client channel if MarkReady fired against unset adapter)

---

## Stage D — Anomaly resolution

### D.1 A3 anomaly (file-name swap claim) — DOCUMENT IS WRONG, files are correct

V5 contract Smoke ledger row asserts:
> "file-named-host=37MB CONTENT-IS-CLIENT, file-named-client=57MB CONTENT-IS-HOST per role markers"

Independent grep of role markers proves files are correctly named:

| File | Lobby markers | Connection markers | Conclusion |
|---|---|---|---|
| `host-100ms.log` (37M) | `[Lobby] Created — ID=...677295 name=My Room` | `Starting Host (Server + Client)... [Host] Host started successfully. /ᐠ｡ꞈ｡ᐟ\ joined` | **HOST machine** |
| `client-100ms.log` (57M) | `[Lobby] Joining 109775243814677295... [Lobby] Joined "My Room"` | `Starting Client → connecting to 76561199070814639` | **CLIENT machine** |

The Joining lobby ID `109775243814677295` matches the host-file's Created lobby ID exactly. File names match content. **A3 documentation is incorrect** — it should be removed or rewritten in the contract Smoke row. The metric attributions in the same row (212 HBs / 13 PushHitbox Hit / 14 Router stack on HOST; 53 HBs / 16 Recv / rec-cb=2595 on CLIENT) are consistent with the actual file content (verified above), so the underlying analysis is right — only the A3 explanatory note is wrong.

### D.2 A2 spawn-window 60-tick probe — 3rd partial-pass, candidate L23 lesson

First spawn marker on client: HEARTBEAT `T=1062`
First impulse Recv on client: `eventTick=6720`
Delta: 5,658 ticks ≈ 113 seconds (50Hz physics) ⇒ first push happened ~113s into the session, far past the 60-tick (~1.2s) spawn-window grace target.

V5 contract C3 prerequisite was "peer-2 push CLIENT buddah within 3-5s of CLIENT spawn." Actual: ~113s. Probe was partial-pass for the **3rd consecutive smoke** (V3 Path A, V4 Path A, now V5 Path B CLIENT). Pattern is operator-timing dependent — humans cannot reliably hit a sub-2s push window after spawn.

**Disposition for V5:** ACCEPTED per design Q3.2 escape clause ("partial-pass with note when cross-peer Tier 1 chain at any later push independently proves L7 latch"). The exact byte-level vector match in Stage C.4 above provides that independent proof.

**Forward proposal — candidate L23:** spawn-window probe needs either (a) a scripted auto-push triggered from a SpawnWindowProbe.cs Editor utility on first character spawn, OR (b) the probe is dropped from the strict-gate list and the cross-peer Tier 1 chain becomes the canonical L7 verification. Three recurrences strongly suggest the operator-driven probe design has a structural flaw, not a discipline problem.

I'm not adding L23 in this verify pass — that's a lesson worth surfacing as a discrete proposal for Yonezawa to confirm/reject after Phase 4b closeout. Note in V5 PR description.

---

## Stage E — Sign-off matrix

| Check | Method (L22-compliant) | Result |
|---|---|---|
| Branch tip = `b53c383` | `git rev-parse origin/<branch>` | ✓ |
| PR #40 carries 10 files +1020/-6 vs dev | `git diff --stat origin/dev origin/<branch>` | ✓ |
| Q1 PredictionProtocol.cs at HEAD | `git show <ref>:<path>` | ✓ |
| Q1 SteamLobbyManager 4 sites at HEAD | `git show <ref>:<path>` | ✓ all 4 present |
| Q3-B motor.cs 3 sites at HEAD | `git show <ref>:<path>` | ✓ field+`++`+2 HEARTBEAT formats |
| Stage 6 pre-grep gate | `git status --short` filtered + sha-after-CRLF-strip | ✓ drift is mount-truncation artifact (git plumbing intact) |
| 9 strict gates × 3 logs | independent grep (NOT trust digest) | ✓ all 27 cells clean |
| Q3-B engagement gate (rec-cb≥5) | independent grep | ✓ 2595 = 519× threshold |
| Q1 handshake LIVE | downstream evidence (16 cross-peer Recvs) | ✓ |
| Q0 race observation | DupReject=0 + monotonic logicalIds | ✓ NOT manifested |
| Cross-peer Tier 1 chain | exact vector match at logicalIds 1+2 | ✓ byte-identical |
| A3 file-name swap claim | independent role-marker grep | ✗ **A3 is wrong** — files correctly named |
| A2 spawn-window probe | first-push tick vs first-spawn tick | △ partial-pass 3rd recurrence — design-tolerated; candidate L23 |

**STRICT PASS confirmed via L22-compliant git plumbing + independent raw-log grep.** Authorize merge of PR #40 → dev.

---

## Pre-merge action items for Yonezawa (none block merge)

1. **A3 correction in v5-contract Smoke row** — change "CONTENT-IS-CLIENT/CONTENT-IS-HOST" to "files correctly named per independent role-marker grep; original A3 misread reverted". Can be a follow-up commit on dev or amended into the merge commit message.
2. **PR #40 description should mention**: (a) carries V5 IMPLEMENT + SMOKE+VERIFY+recon/design+contract; (b) corrective PR #39 already merged so wire-format change atomic; (c) propose L23 follow-up about spawn-window probe automation; (d) propose Q2-B 200ms expansion eligibility.
3. **Q3.4 atomic deployment**: PR #40 changes wire format only via Q1 lobby metadata key (new key `l_pv`, doesn't affect existing keys), and adds a single `uint` to motor.cs ([Reconcile] payload size unchanged — `_reconcileCallbackCount` is local-only, not in ReconcileData struct). Wire-format risk for PR #40 itself: **near-zero**. Active dev-branch builds will see new metadata key but ignore it; client→host with v0 lobby metadata gets the friendly mismatch message.
4. **V5 contract Verify row content**: my actual verification *agrees* with what the pre-filled row claims (STRICT PASS, 9 gate classes match, cross-peer Tier 1 chain verified, Q1 LIVE, Q3-B 519× threshold, Q0 not manifested) — but the row was written before my verify ran. For audit integrity, recommend amending the Verify row to cite this report `agent-exchange/handoff/2026-05-03-pr40-v5-independent-verify.md` as the actual sign-off artifact, with the words "see report linked for independent grep evidence."

---

## Reflective lesson on verify discipline

Two L22 wins in this verify pass:
1. **Working-tree truncation flagged + bypassed**. Pre-grep gate caught real-looking drift (`AM` flag + content sha mismatch even after CRLF strip), but `git show <ref>:<path>` showed HEAD content was intact. Reading working tree as primary verification source would have falsely concluded files were broken/incomplete.
2. **A3 documentation false-positive caught**. The Smoke ledger asserted file-name swap based on operator self-correction; independent role-marker grep contradicted the claim. If verify had skipped raw-log inspection ("digest already says so"), the wrong A3 narrative would have shipped into the archive.

One unresolved discipline concern (not blocking):
3. **Pre-filling reviewer-signed rows in active contract before reviewer verifies.** This is convenient (one less round-trip) but inverts the sign-off direction — reviewer's job becomes "validate or reject pre-written claims" rather than "author the sign-off based on independent investigation". Both work, but the former mode is more cognitively susceptible to confirmation bias. Worth a brief discussion at Phase 4b closeout about whether "implementer pre-fills reviewer rows in good faith, reviewer revises in writing" is the right convention.

L22 + L21 hold up under self-application.
