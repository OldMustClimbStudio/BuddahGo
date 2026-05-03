# phase4b-v5 — Independent Verification

**Raw log paths:**
- `agent-exchange/console/raw/2026-05-03-phase4b-v5-single.log` (22 MB) — Path A baseline (HOST role, 2-peer no-LatencySim)
- `agent-exchange/console/raw/2026-05-03-phase4b-v5-host-100ms.log` (37 MB) — **content is CLIENT** (file label swapped per role markers)
- `agent-exchange/console/raw/2026-05-03-phase4b-v5-client-100ms.log` (57 MB) — **content is HOST** (file label swapped)

**Reviewer:** Claude Opus 4.7 (cowork-reviewer, V5 risk:MEDIUM full-grep verification + L22 git-plumbing primary self-application)
**Verification date:** 2026-05-03

---

## Self-application of L22 (verification command targets)

Per lessons-log L22 + methodology Rule 2 sub-clause "Verification target — git plumbing over working tree" added in PR #39:

| Command | Result | Pass |
|---|---|---|
| `git status --short` filtered to V5 scope | clean — only LFS noise + agent-exchange artifacts (non-scope) | ✅ |
| `git show HEAD:Assets/Scripts/Network/PredictionProtocol.cs` | new file at HEAD (commit 4b60773) | ✅ |
| `git show HEAD:Assets/Scripts/Network/Lobby/SteamLobbyManager.cs` includes `KEY_PREDICTION_PROTOCOL_VERSION` | confirmed | ✅ |
| `git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` includes `_reconcileCallbackCount` | confirmed | ✅ |
| `git diff origin/dev -- <V5-scope>` | matches IMPLEMENT commit | ✅ |
| `gh pr diff 40` | exact change set verified | ✅ |
| Read tool used as primary verification source | NO (used only for navigation) | ✅ L22-compliant |

---

## Path A grep results (HOST role, baseline no-LatencySim, 2-peer ~150s)

```
[D-LOC FATAL]:                                 0          ✅
[D-IMP LEG ...]:                               0          ✅
[CommandBus]:ClearAll:                         16         (4×N=4 spawns, longer session)
[CommandBus]:DropFull:                         0          ✅
[Channel]:DupReject:                           0          ✅
[CommandBus]:Recv ch=Impulse:                  4          (HOST observed remote-owner enqueue chain)
  - Recv carries eventTick=N AND logicalId=N: 4/4         ✅
  - Monotonic logicalId 1→2→3→4:              ✅
[D-LOC HEARTBEAT] rows:                        142
rec-cb field present in HEARTBEAT format:      35 sites   ✅ Q3-B INSERTION CONFIRMED
rec-cb cumulative VALUE (max across HBs):      0          ✅ HOST baseline (FishNet [Reconcile] on server-self = no-op)
Compile errors (CS):                           4          ⚠ A1 pattern (CS2001 cold-start, Tundra ExitCode:0 recovery)
Exceptions:                                    0          ✅
[PushHitbox] Hit:                              1          ✅ visual smoke
Router stack frames:                           12         ✅ Tier 2 dispatch
```

**Path A verdict:** ✅ PASS as HOST baseline. Q3-B insertion confirmed via field present + 0 baseline (structurally enforced).

---

## Path B HOST grep results (LatencySim 100ms, raw file `client-100ms.log` per swap)

```
[D-LOC FATAL]:                                 0          ✅
[D-IMP LEG ...]:                               0          ✅
[CommandBus]:ClearAll:                         8          (4×N=2 spawns)
[CommandBus]:DropFull:                         0          ✅
[Channel]:DupReject:                           0          ✅
[CommandBus]:Recv ch=Impulse:                  0          ✅ EXPECTED (server originates impulses)
[D-LOC HEARTBEAT] rows:                        212
rec-cb max value:                              0          ✅ HOST role (server-self [Reconcile] no-op)
Compile errors:                                0          ✅
Exceptions:                                    0          ✅
[PushHitbox] Hit (server-only):                13         ✅ Tier 1 server authority active
Router stack frames:                           14         ✅ Tier 2 dispatch
StartHost:                                     11         ✅ host init confirmed
```

---

## Path B CLIENT grep results (LatencySim 100ms, raw file `host-100ms.log` per swap)

```
[D-LOC FATAL]:                                 0          ✅
[D-IMP LEG ...]:                               0          ✅
[CommandBus]:ClearAll:                         8          (4×N=2 spawns)
[CommandBus]:DropFull:                         0          ✅
[Channel]:DupReject:                           0          ✅
[CommandBus]:Recv ch=Impulse:                  16         ✅ ≥1 gate met
  - Recv carries eventTick=N AND logicalId=N: 16/16      ✅ per gate
  - Monotonic logicalId 1→16, no gaps:        ✅
[D-LOC HEARTBEAT] rows:                        53
rec-cb max value (Q3-B engagement gate):       2595       ✅✅ MASSIVELY EXCEEDED ≥5 threshold
rec-cb growth trajectory:                      0→3→...→2430→2501→2595  ✅ LatencySim ENGAGED
Compile errors during runtime:                 0          ✅
Exceptions:                                    0          ✅
[PushHitbox] Hit:                              0          ✅ EXPECTED (CLIENT side has no server authority)
StartClient:                                   4          ✅ client init confirmed
```

---

## Cross-peer alignment verification (Tier 1 chain)

HOST hit #1: `impulse=(83.34, 0.00, -55.27)` victim=Buddah(Clone) owner=0
↓ via CombatAdapter.TryRouteImpulse → Target_EnqueueImpulse RPC
CLIENT Recv #1: `linear=(83.34, 0.00, -55.27) eventTick=6720 logicalId=1` ✅ EXACT MATCH

HOST hit #2: `impulse=(-67.63, 0.00, 73.67)` victim=Buddah(Clone) owner=0
↓ via CombatAdapter.TryRouteImpulse → Target_EnqueueImpulse RPC
CLIENT Recv #2: `linear=(-67.63, 0.00, 73.67) eventTick=7487 logicalId=2` ✅ EXACT MATCH

Sample of 2 of 16 verified; remaining 14 pattern-conformant per:
- Monotonic LogicalId 3→16 (no gaps)
- Zero DupReject across full session
- Zero D-LOC FATAL across 53 + 212 = 265 HEARTBEAT rows total

L7 latch correctness proven: if V4 corrective merge had broken latch (`#if BUDDAH_PREDICTION_LEGACY_SHADOW` strip side-effect), `_initialized=false` → `TryRouteImpulse` returns false → 0 CLIENT Recvs. **16 CLIENT Recvs land with monotonic logicalIds = latch survived V4 corrective.**

---

## Q3-B engagement verification — DECISIVE

| Profile | Role | rec-cb final | Gate | Pass |
|---|---|---|---|---|
| Path A baseline | HOST | 0 | structurally enforced (server-self no-op) | ✅ |
| Path B 100ms LatencySim | HOST | 0 | structurally enforced | ✅ |
| Path B 100ms LatencySim | **CLIENT** | **2595** | ≥2× HOST baseline OR ≥5 absolute (whichever greater) | ✅✅ **519× the ≥5 threshold** |

**Conclusion:** LatencySim Inspector toggle persisted across Editor session; FishNet reconcile pipeline under 100ms simulated latency fires aggressively as designed. CLIENT-side rec-cb growth from 0 → 2595 across the session unambiguously proves LatencySim engagement. Q3-B probe successfully validated its purpose.

---

## Q1 protocol-version handshake — LIVE VERIFIED

Both peers built fresh from V5 branch HEAD (commit `4b60773`) → both report `PredictionProtocol.Version = 1`. CLIENT joined HOST under LatencySim 100ms with no `Prediction protocol mismatch` log, no FireJoinFailed event, no early disconnect. 16 cross-peer impulse Recvs landed CLIENT-side — would be 0 if Q1 handshake had hard-rejected the connection.

Q1 handshake live-verified at v1↔v1 baseline. Mismatch path is dead-code-tested per FireJoinFailed shape (live mismatch test would require manual `PredictionProtocol.Version = 2` on one peer + rebuild — not run in this Stage 5 cycle).

---

## Q0 reconcile-before-RPC race observation — NOT MANIFESTED

Per design Q0-A defer + observe-first under LatencySim 100ms:
- 0 `[Channel]:DupReject` on either peer across full session
- 16 Recvs all consumed exactly once (LogicalId monotonic 1→16, no gaps, no skip-and-replay)
- 0 D-LOC FATAL across 265 HBs
- Visual smoke per user driver: no double-impulse / overshoot artifacts reported

**Q0 race NOT observed at 100ms RTT.** Per design Q0-A: _"if not observed, document as resolved (with caveat that future increase in reconcile state delivery scope could reintroduce the race)"_. Q0-B preemptive code shape preserved in design doc for future-phase pivot at 200ms+ regime if Q2-B expansion ever lands.

---

## Cross-check vs implementer's digests

| Metric | Path A digest | Path A grep | Match | Path B HOST digest | Path B HOST grep | Match | Path B CLIENT digest | Path B CLIENT grep | Match |
|---|---|---|---|---|---|---|---|---|---|
| `[D-LOC FATAL]` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[D-IMP LEG ...]` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[CommandBus]:ClearAll` | 16 | 16 | ✅ | 8 | 8 | ✅ | 8 | 8 | ✅ |
| `[CommandBus]:DropFull` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[Channel]:DupReject` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[CommandBus]:Recv ch=Impulse` | 4 | 4 | ✅ | 0 | 0 | ✅ | 16 | 16 | ✅ |
| Recv eventTick + logicalId | 4/4 | 4/4 | ✅ | n/a | n/a | n/a | 16/16 | 16/16 | ✅ |
| Router stack frames | 12 | 12 | ✅ | 14 | 14 | ✅ | 0 | 0 | ✅ |
| `[D-LOC HEARTBEAT]` rows | 142 | 142 | ✅ | 212 | 212 | ✅ | 53 | 53 | ✅ |
| **rec-cb max** | **0** | **0** | ✅ | **0** | **0** | ✅ | **2595** | **2595** | ✅ |

All implementer digests match reviewer greps. **0 metric divergence.**

---

## Anomalies

### A1 — Path A 4 CS2001 errors at lines 46357-46363 (cold-start dance recurring)

**Symptom:** Identical to V4 A1 — CS2001 errors complaining about deleted files (`BuddahPredictionCombatRouting.cs` etc.).

**Root cause (per L20):** Unity incremental compiler metadata cache stale state. Tundra immediately recovered: `*** Tundra requires additional run (2.08 seconds), 5 items updated, 562 evaluated; ExitCode: 0`.

**V5 cross-validation that runtime ran V5 code:**
- `rec-cb` field appears in HEARTBEAT (Q3-B insertion landed) — pre-V5 wouldn't have this
- 0 `[D-IMP LEG ...]` — LEG axis retired in V4
- 0 schema mismatch across 142 HBs

**Verdict (per L20):** non-issue. Document for future deletion-heavy phases.

### A2 — Spawn-window 60-tick L7 distance gate not strictly met (third occurrence)

**Measurement (Path B CLIENT):** first HB at T=1062, first Recv at eventTick=6720 → distance 5658 ticks (~94s) >> 60-tick gate.

**Root cause:** user did not initiate combat within first 5s of CLIENT spawn. Lobby + LatencySim toggle interaction consumed early window. Same as V4 A2.

**Why this is partial-pass not failure (per V4 Q3.2 design language carry-over):**
- L7 latch correctness proven by 16 Recvs landing with monotonic logicalIds (latch fired correctly)
- rec-cb=2595 jointly proves both LatencySim engagement + reconcile pipeline health
- If V4 corrective merge had broken latch → 0 CLIENT Recvs → we have 16 → latch survived

**Verdict:** Partial-pass-with-note. **Recommendation for future smoke: encode "peer-2 push within 3-5s of CLIENT spawn" as user driver hard-precondition; if not met, abort run before recording.** This pattern has now repeated 3 times (V4 Path B, V5 Path A, V5 Path B) — clearly not a transient driver miss; should land as a SMOKE driver checklist item or automated test fixture.

### A3 — File-name role swap

**Symptom:** raw file `host-100ms.log` contains CLIENT session content; raw file `client-100ms.log` contains HOST session content.

**Determination via role markers:**
- HOST: 0 Recv, rec-cb=0, 13 PushHitbox Hit, 14 Router stack, StartHost=11
- CLIENT: 16 Recv, rec-cb=2595, 0 PushHitbox Hit, 0 Router stack, StartClient=4

**Recommendation:** rename raw files to match content before final archival. Digest filenames already correct (HOST digest at `2026-05-03-phase4b-v5-host-100ms.log`, CLIENT digest at `client-100ms.log`).

**Verdict:** non-issue for verification (role determination via content markers is unambiguous). Raw file rename is housekeeping for archival hygiene.

---

## Verdict

**✅ PASS** — All Path A baseline + Path B HOST + Path B CLIENT V5 strict gates met:

- **Zero LEG/LOC FATAL** across 407 HEARTBEAT rows total (142 + 212 + 53)
- **Q3-B reconcile-callback probe DECISIVELY validated**: rec-cb=2595 on CLIENT under 100ms LatencySim vs HOST baseline=0 → 519× the ≥5 threshold; LatencySim engagement unambiguous
- **Q1 protocol-version handshake LIVE verified**: v1↔v1 join successful, 16 cross-peer Recvs would be 0 if handshake broke
- **Q0 reconcile-before-RPC race NOT observed**: 0 DupReject + 16 monotonic Recv consumes + 0 D-LOC FATAL → race not manifested at 100ms (per Q0-A observe-first lean; Q0-B preemptive code shape preserved for future regime)
- **Cross-peer Tier 1 chain verified** via exact impulse-vector match (HOST→CLIENT) for logicalId 1 + 2; remaining 14 pattern-conformant
- **L7 latch survived V4 corrective merge** (16 Recvs land = MarkReady fired)
- **A1 CS2001 cold-start** identified per L20 + Tundra ExitCode:0 recovery
- **A2 Spawn-window 60-tick partial-pass** for third time — recommend encoding driver precondition for future smokes
- **A3 File-name role swap** — non-blocking, recommend rename for archival hygiene

**Q2 expansion to 200ms LatencySim:** per design Q2-B trigger criteria (100ms clean + ≥10 cross-peer Recvs), 100ms baseline IS clean and 16 cross-peer Recvs landed cleanly → **200ms expansion eligible**. User decides whether to run V5 200ms in this PR cycle (V5 strict gate satisfied at 100ms; 200ms is optional broader observation per Q2 design lean A→B).

**Stage 6 VERIFY: PASS. Ready for Stage 7 MERGE pending reviewer + user sign-off.**

---

## Lessons-log proposals (V5 closeout — defer commit until merge per Rule 5)

**No new lessons required for V5 IMPLEMENT correctness.** L22 (added in PR #39) covered the verification methodology gap; V5 IMPLEMENT itself surfaced no new failure modes.

**Methodology candidate (NOT a lesson, soft observation):** spawn-window 60-tick gate has now partial-passed 3 times in a row across V4 Path B + V5 Path A + V5 Path B. Either:
- (a) the gate is too strict for human-driven smoke (in which case relax to "spawn-window probe within first 30s")
- (b) the test driver checklist needs a hard-precondition for early-spawn push
- (c) automate via a programmatic post-spawn impulse fixture

User to consider whether (a)/(b)/(c) becomes a Phase 4b closeout methodology amendment or a Phase 7 smoke-tooling item.
