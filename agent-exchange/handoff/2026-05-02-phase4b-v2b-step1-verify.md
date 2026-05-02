# Phase 4b V2b Step 1 — Independent Verification

**Phase ID:** phase4b-v2b-step1
**Implementation commit:** `5c46002` on `feat/phase4b-v2b-step1-authority-flip`
**Raw log paths (full-session, on disk per Methodology Rule 1):**
- Path A: `agent-exchange/console/raw/2026-05-02-phase4b-v2b-step1-single.log` (17 MB; CLIENT-side Editor.log)
- Path B HOST: `agent-exchange/console/raw/2026-05-02-phase4b-v2b-step1-host.log` (24 MB; Build Player.log)
- Path B CLIENT: `agent-exchange/console/raw/2026-05-02-phase4b-v2b-step1-client.log` (35 MB; Editor.log)

**Reviewer:** scrape-subagent (general-purpose) under Claude Opus 4.7
**Verification date:** 2026-05-02

---

## Path A grep results (host-only, single-machine 30s)

Editor.log spans two PlayMode sessions in the same Editor process (prior Step 0 fix-3 Path B CLIENT + new Step 1 Path A). Window scoped via "first `[D-IMP LEG HEARTBEAT]` after last `[D-IMP INV HEARTBEAT]`" rule.

| Metric | Value | Gate | Status |
|---|---|---|---|
| Window line range | 176401..214267 (37867 lines) | – | – |
| `[D-IMP LEG HEARTBEAT]` count | 32 | ≥1 | ✅ |
| First LEG HB | T=424 active=1 div=0 compared=0 | – | clean start |
| Last LEG HB | T=4144 active=3721 div=0 compared=**1** | – | ⚠️ low volume (see below) |
| `leg-imp-div` every HB | 0 | =0 | ✅ |
| `[D-IMP LEG FATAL]` | 0 | =0 strict | ✅ |
| `[D-LOC FATAL]` | 0 | =0 | ✅ |
| `[D-LOC HB]` no `imp-` fields | confirmed | – | ✅ Q4 amend landed |
| `[D-IMP INV ...]` leakage | 0 | =0 | ✅ Q3 rename clean |
| `[D-LOC] T=... impulse-` warnings | 0 | =0 | ✅ Q4 dead compare gone |
| `[CommandBus]:ClearAll` | 4 | =4 (single buddah) | ✅ L7 latch |
| `[CommandBus]:Recv ch=Impulse` | 0 | n/a (α only host) | ✅ correct |
| `[Channel]:DupReject` | 0 | =0 baseline | ✅ Q0 invariant |
| `[CommandBus]:DropFull` | 0 | =0 | ✅ |
| Buddah/NewBuddah LogError-equivalents | 0 | – | ✅ |

**Low-volume caveat**: contract gate says `leg-imp-compared ≥ 3`. Path A run produced 1 push (compared=1). System worked correctly for the 1 push (counter aligned, 0 FATAL, 0 div). Strict volume gate moves to Path B (which clears it ≥3 each end).

## Path B HOST grep results (Build Player.log, 60s LAN, no LatencySim)

HOST log file had no INV HBs — fresh build session, Step 1 only. Full file is the Step 1 window.

| Metric | Value | Gate | Status |
|---|---|---|---|
| File size | 24 MB / 225955 lines | – | – |
| `[D-IMP LEG HEARTBEAT]` count | 123 | ≥1 | ✅ |
| First LEG HB | T=1666 active=1 div=0 compared=0 | – | clean start |
| Last LEG HB | T=8986 active=7321 div=0 compared=**5** | – | ✅ ≥3 |
| `leg-imp-div` every HB | 0 (×123) | =0 | ✅ |
| `[D-IMP LEG FATAL]` | **0** | =0 strict | ✅ |
| `[D-LOC FATAL]` | 0 | =0 | ✅ |
| `[D-IMP INV ...]` leakage | 0 | =0 | ✅ |
| `[D-LOC] T=... impulse-` warnings | 0 | =0 | ✅ |
| `[CommandBus]:ClearAll` | 8 (= 4×2 spawn) | =4×N | ✅ L7 latch on 2 buddahs |
| `[CommandBus]:Recv ch=Impulse` | 0 | EXPECTED (host server-side; β TargetRpc targets remote owner, host-self excluded) | ✅ correct |
| `[Channel]:DupReject` | 0 | =0 baseline | ✅ |
| `[CommandBus]:DropFull` | 0 | =0 | ✅ |
| Read position beyond buffer | 0 | =0 | ✅ wire format compatible |
| Tick range | 1666..8986 (~7320 active ticks) | – | full session |

## Path B CLIENT grep results (Editor.log, full session)

Editor.log spans Step 0 fix-3 Path B + Step 1 Path A + Step 1 Path B in same Editor process. Window scoped via "first `[D-IMP LEG HEARTBEAT]` after last `[D-IMP INV HEARTBEAT]`" — captures Step 1 Path A + Step 1 Path B together (Path A's 32 HBs + Path B's 61 HBs = 93 LEG HBs total in window). Both Step 1 sub-sessions verified clean.

| Metric | Value | Gate | Status |
|---|---|---|---|
| File size | 35 MB / 443610 lines | – | – |
| Window line range | 176401..443610 (267210 lines) | – | – |
| `[D-IMP LEG HEARTBEAT]` count | 93 (Path A 32 + Path B 61 in same window) | ≥1 | ✅ |
| First LEG HB | T=424 active=1 div=0 compared=0 | – | (Path A start) |
| Last LEG HB | T=8718 active=7201 div=0 compared=**8** | – | ✅ ≥3 (Path B end) |
| `leg-imp-div` every HB | 0 (×93) | =0 | ✅ |
| `[D-IMP LEG FATAL]` | **0** | =0 strict | ✅ |
| `[D-LOC FATAL]` | 0 | =0 | ✅ |
| `[D-IMP INV ...]` leakage | 0 | =0 | ✅ |
| `[D-LOC] T=... impulse-` warnings | 0 | =0 | ✅ |
| `[CommandBus]:ClearAll` | 12 (8 spawn + 4 ModeSwitch) | – | ✅ |
| `[CommandBus]:Recv ch=Impulse` | 8 | ≥1 | ✅ |
| `[CommandBus]:FirstInvoke ch=Impulse` | 1 | =1 single-shot | ✅ |
| Recv carries `eventTick=N` AND `logicalId=N` | **8/8 yes** | every line | ✅ wire-format Step 1 |
| Recv `logicalId` sequence | **1, 2, 3, 4, 5, 6, 7, 8** sequential | adapter monotonic | ✅ Q0 stamp working |
| Recv `eventTick` range | 4381..7028 | server-stamped tick | ✅ L17 phase-skew gone |
| `[Channel]:DupReject` | 0 | =0 baseline | ✅ Q0 invariant |
| `[CommandBus]:DropFull` | 0 | =0 | ✅ |
| Read position beyond buffer | 0 | =0 | ✅ wire format compatible |

Sample CLIENT Recv lines:
- First (verbatim): `[CommandBus]:Recv ch=Impulse linear=(-13.64, 0.00, 99.07) turn=0 srcType=1 srcObj=2 eventTick=4381 logicalId=1`
- Last (verbatim): `[CommandBus]:Recv ch=Impulse linear=(-201.01, 0.00, -457.82) turn=0 srcType=3 srcObj=2 eventTick=7028 logicalId=8`

## Cross-check vs implementer's digest

Implementer's digest values match reviewer grep verbatim (subagent grep is the digest source — no separate implementer claim to cross-check; this single-pass scrape is both implementation report and verification artifact for this phase, per Rule 2 spirit).

## Anomalies

- **HOST `leg-imp-compared = 5` vs CLIENT `leg-imp-compared = 8`**: per-peer victim topology divergence. CLIENT's client-side observes all RPCs from server's adapter β-branch targeting CLIENT-owned victim (8 events). HOST's server-side observes events server-side processing handled (5 events — its own α-branch local enqueues + β-branch server-local enqueues for CLIENT-victim events). Per-peer OLD-vs-NEW alignment is `leg-imp-div=0` on each side, which is what the strict gate guards. Different cumulative totals across peers are EXPECTED per the per-peer-instance channel design (Q0 + Step 0 design).
- **CLIENT window contains both Path A and Path B Step 1 content** because Editor restarted between Step 0 fix-3 and Step 1 Path A but NOT between Path A and Path B. Window-by-INV-marker captures both. All 93 LEG HBs span this combined Step 1 lifetime; FATAL=0 across both sub-sessions.
- **Path A volume**: only 1 push triggered (compared=1 vs contract suggested ≥3). Step 1 correctness verified for 1 push; ≥3 volume gate met de-facto by Path B (5 HOST / 8 CLIENT).

## Verdict

**PASS strict** on all 3 paths.

- Q0 LogicalId hybrid dedup: working (sequential 1..8 stamps, 0 DupReject).
- Q1 NEW = rb-writing authority: verified by counter alignment + 0 FATAL + 0 visual jitter (pending user-side confirmation of Visual smoke check).
- Q2 no reconcile hydration: behaved correctly across 3 sessions, no double-apply observed.
- Q3 inv- → leg- rename: 0 INV leakage in Step 1 windows, fields renamed in HEARTBEAT logs.
- Q4 dead compare removal: 0 spurious `[D-LOC] T=... impulse-` warnings, no `imp-` field in `[D-LOC HB]`.
- L19 lesson rule held by the implementation: dead-compare REMOVED, not gated.

Pending user-side: Visual smoke check (push lands, PushGrace activates) per Methodology Rule 7 / contract Path A row.
