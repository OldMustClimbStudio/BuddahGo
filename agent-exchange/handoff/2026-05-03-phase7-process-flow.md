# Phase 7 — Process Flow + Dependency Map (companion to KICKOFF draft)

**Date authored:** 2026-05-03
**Author:** cowork-reviewer (Claude Opus 4.7, harness)
**Purpose:** Companion to `2026-05-03-phase7-kickoff-draft.md`. The contract draft
defines WHAT Phase 7 does (scope + Q&A + gates). This doc defines HOW Phase 7
flows through the 7 stages and what each stage's concrete artifacts look like.

---

## Dependency chain (do not start Phase 7 until upstream is clear)

```
[housekeeping PR]    [task #38 archive backfill]
       ↓                       ↓
       └───────────┬───────────┘
                   ↓
          [dev clean + active/ has only phase7-contract.md after promotion]
                   ↓
          [Phase 7 KICKOFF stamp by cowork-reviewer]
                   ↓
                   ├─→ Stage 2 RECON (implementer)
                   ↓
                   ├─→ Stage 3 DESIGN-QA (implementer + reviewer)
                   ↓
                   ├─→ Stage 4 IMPLEMENT (implementer)
                   ↓
                   ├─→ Stage 5 SMOKE (driver = Yonezawa, scrape = Claude Code)
                   ↓
                   ├─→ Stage 6 VERIFY (cowork-reviewer, L22-compliant)
                   ↓
                   └─→ Stage 7 MERGE
                              ↓
                       [Phase 7 closeout decision tree]
                              ├─ no regression → close + Phase 6 / Phase 8 unblocked
                              └─ regression found → Phase 7.5 retrofit contract drafted
```

**Hard prerequisite (cannot skip):** task #38 archive backfill must close
BEFORE Phase 7 KICKOFF stamp. Reason: methodology Rule 10 (working tree
commit hygiene) — leaving 4 contracts unarchived while opening Phase 7
re-creates the active/ folder pollution that the V5 closeout housekeeping
just fixed.

---

## Per-stage concrete checklist

### Stage 1 — KICKOFF (estimated 5 minutes)

**Owner:** cowork-reviewer
**Inputs:** task #38 done + housekeeping PR merged + dev tip clean
**Actions:**
1. Yonezawa: `git mv agent-exchange/handoff/2026-05-03-phase7-kickoff-draft.md Docs/phase-gates/active/phase7-contract.md`
2. Yonezawa: commit + push (no PR — direct to dev or to Phase 7 branch's first commit)
3. cowork-reviewer: edit the Kickoff sign-off ledger row in `phase7-contract.md`:
   ```
   | Kickoff | 2026-05-XX | cowork-reviewer | Contract stamped post Phase 4b CLOSEOUT (dev tip <SHA>). 6 PRE-WORK Q seeded with leans (Q0=C, Q1=A, Q2=C, Q3=B+A, Q4=C, Q5=A) per kickoff draft. Implementer authorized to start Stage 2 RECON. |
   ```
4. cowork-reviewer: confirm `Tools/Harness/Get-BuddahGoHarnessContext.ps1`
   surfaces phase7-contract.md when run (verifies the V5 housekeeping
   addition works in practice). If not, file a Phase Gate System bug.

**Output:** active contract committed; Stage 2 authorized.

---

### Stage 2 — RECON (estimated 30-60 minutes)

**Owner:** implementer (Claude Code)
**Output:** `agent-exchange/handoff/<date>-phase7-recon.md` (Template 1 form)

**RECON target inventory (use this as the explicit grep-list):**

| # | Inventory item | Grep / inspect | Why it matters |
|---|---|---|---|
| 1 | Visible character render path | `git grep -n "BuddahPredictedRepresentation\|BuddahVisualRepresentation\|VisualSync"` | JitterCapture must attach to whatever transform actually drives screen pixels — could be motor.transform OR a separate representation node smoothed by FishNet TickSmoother |
| 2 | NetworkTickSmoother config on Buddah prefab | Open Buddah prefab in Unity Inspector OR `git grep -n "NetworkTickSmoother\|TickSmootherController" Assets/Scripts/ Assets/Prefabs/Characters/` | If smoother is configured, captured position is post-smooth (correct for visual jitter); if unconfigured, captured position == raw motor.transform (different metric meaning) |
| 3 | motor `[Reconcile]` callback site + rec-cb counter | `git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs \| grep -n "rec-cb\|_reconcileCallbackCount\|\[Reconcile\]"` | Q4 rec-snap-distance probe attaches here; need exact insertion point + verify V5 IMPLEMENT's counter still works post-housekeeping merge |
| 4 | BuddahLocomotionStep — predicted motion produced per tick | `git grep -n "RunInputs\|PredictRotation\|ApplyMovement" Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs` | Sets baseline expectation: how much motion happens per tick under normal play (informs Q3 absolute threshold calibration) |
| 5 | Existing visual debug overlays | `git grep -n "BuddahPredictionDebugOverlay\|DebugDraw" Assets/Scripts/New_Buddah/Debug/` | If overlay already shows position-history or velocity bars, JitterCapture can leverage same data source |
| 6 | Pre-Phase 4b SHA (if Q2-A is picked) | `git log --oneline --before=2026-04-15 dev` first commit, OR query Yonezawa for "what was dev tip before V1 IMPLEMENT" | Required if Q2 baseline = (A) full Phase 4b regression check |

**Per L22 + L21:** every claim in the recon report must be backed by
`git show HEAD:<path>` or `git grep <pattern> HEAD` (NOT Read on working
tree). Each surface gets a "evidence:" footer with the exact grep command
+ output snippet.

**KEY FINDING flag:** reserve a "KEY FINDING" callout in recon for any
discovery that changes a Q lean. Most likely candidates:
- If item #2 finds NO TickSmoother on Buddah prefab → JitterCapture must
  capture motor.transform directly + Q0 metric interpretation changes
- If item #3 finds rec-cb instrumentation got rolled back somehow → Q4
  must include re-instrumenting before measurement begins

---

### Stage 3 — DESIGN-QA (estimated 30 minutes)

**Owner:** implementer proposes; cowork-reviewer verifies + approves
**Output:** `agent-exchange/handoff/<date>-phase7-design.md` (Template 2 form)

**Q answer dependency tree** (answer in this order to avoid backtracking):

```
Q1 (capture mechanism)
  ↓
Q2 (baseline) — need to know if (A) requires SHA-checkout JitterCapture variant
  ↓
Q0 (metric definition) — needs Q1 to know what data is available
  ↓
Q4 (rec-snap probe) — needs Q1 + Q0 (probe extends capture mechanism + metric)
  ↓
Q3 (pass criteria) — needs Q0 + Q4 to set threshold values
  ↓
Q5 (escalation path) — needs Q3 (regression definition) to know when to fire
```

**Cowork-reviewer verify checklist for design Q&A:**
- Each Q answer cites recon items (especially Q1 ↔ recon #1+#2, Q4 ↔ recon #3)
- Q0 metric definition produces a single number per session per peer that
  can be tabulated cleanly in the verify report
- Q3 absolute threshold cites a specific numeric value (e.g., "RMS Δposition
  < 5cm/frame") not vague language
- Q5 escalation path explicitly names the Phase 7.5 retrofit branch /
  contract location if regression is found
- L22 self-check: design includes verify commands using git plumbing, not
  Read on working tree

---

### Stage 4 — IMPLEMENT (estimated 1-2 hours)

**Owner:** implementer (Claude Code)
**Branch:** `feat/phase7-visual-jitter-evaluation` cut from dev tip post-stamp

**File scaffold expectations:**

```
Assets/Scripts/New_Buddah/Debug/JitterCapture.cs          (NEW, ~50 LOC)
  namespace NewBuddah.PredictionV2.Debug
  {
      // Editor-only MonoBehaviour, attached to BuddahPredictedRepresentation
      // (or motor.transform if Q1 recon finds no representation layer).
      // OnEnable: open per-session CSV file (path resolved per Stage 5 naming convention)
      // LateUpdate: write (frameCount, Time.unscaledTime, transform.position.{x,y,z}, transform.rotation.{x,y,z,w}, Time.unscaledDeltaTime)
      // OnDisable: flush + close
      // Q4 extension: subscribe to motor's [Reconcile] callback (if Q1=A, expose via event;
      //   if Q1=B, hook directly under #if BUDDAH_PREDICTION_JITTER_CAPTURE)
      //   on reconcile: write (frameCount, "RECONCILE", preReconcilePos, postReconcilePos, snapDistance)
  }

Tools/Analysis/AnalyzeJitter.ps1                          (NEW, ~80 LOC)
  param([string]$Path, [string]$Metric = "all")
  # Ingest CSV(s), compute per Q0 definition:
  #   - RMS Δposition per 60-frame sliding window
  #   - frame fraction with |Δv| > threshold
  #   - 95th percentile + max rec-snap-distance (Q4)
  # Output: tabular report to stdout + optional JSON to file
  # Cross-file comparison mode: AnalyzeJitter.ps1 -Path baseline.csv -Compare candidate.csv
```

**Implementation discipline:**
- JitterCapture uses `#if UNITY_EDITOR` wrapping — guarantees zero production-build impact (Q1=A choice)
- File I/O uses `StreamWriter` with explicit `Flush()` per row to survive Editor crashes
- AnalyzeJitter.ps1 uses pure PowerShell + `Import-Csv` (no Python dependency required)
- No changes to motor.cs, channel layer, ReconcileData struct, or any Phase 4b-protected file. If Q4 Q1=A capture mechanism needs the [Reconcile] callback exposed, add a public event on motor.cs that fires AFTER `_reconcileCallbackCount++` — this is the ONLY motor.cs touch allowed in Phase 7 implementation, and it must be marked `[System.Diagnostics.Conditional("UNITY_EDITOR")]` or under `#if UNITY_EDITOR` to maintain Phase 4b's "no behavior change" promise

**Cowork-reviewer reviews diff for:**
- Rule 7 PredictionRigidbody integrity: JitterCapture does NOT call `rb.AddForce` / `rb.MovePosition` (it's read-only on transform)
- Rule 12: Implementation row in contract ledger NOT pre-filled with reviewer signature
- L22 self-check: any negative claim ("does not touch X") verified via `git diff origin/dev -- <X>` returning empty

---

### Stage 5 — SMOKE (estimated 20 minutes per path × 3 paths = 1 hour)

**Owner:** Yonezawa (driver) + Claude Code (scrape)

**Per-path procedure:**

| Path | Setup | Driver action | Capture file naming |
|---|---|---|---|
| A — single | Editor PlayMode, no LatencySim, single character spawn | Walk + jump for 30s, vary speeds | `agent-exchange/console/raw/<date>-phase7-single-jitter.csv` |
| B no-latency | 2-peer LAN, no LatencySim, both spawned | Both peers walk + push each other for 60s | HOST: `<date>-phase7-host-jitter.csv` / CLIENT: `<date>-phase7-client-jitter.csv` |
| B 100ms | 2-peer LAN, TransportManager `_latency=50` (host-loopback doubles to 100ms RTT), both spawned | Both peers walk + push each other for 60s | HOST: `<date>-phase7-host-100ms-jitter.csv` / CLIENT: `<date>-phase7-client-100ms-jitter.csv` |

**Driver discipline (Rule 11 reminder):** Phase 7 has NO time-sensitive
probes (jitter accumulates over 60s, not within a 1.2s window). Rule 11 is
moot for Phase 7 SMOKE. BUT — if Q4-C rec-snap-distance probe fires only on
[Reconcile] callbacks, driver must ensure enough rb activity over 60s to
trigger reconciliations. Recommend: continuous push-and-pull interaction,
not standing still.

**Yonezawa's pre-PlayMode self-check (Rule 1-D):** before dumping the CSV
to `raw/`, confirm the file is non-empty and contains both timestamp rows
AND (for paths B+C if Q4 active) at least 1 RECONCILE row. If any path
captures 0 reconcile rows under LatencySim 100ms, that's evidence
JitterCapture isn't wired correctly — re-implement before SMOKE re-run.

---

### Stage 6 — VERIFY (estimated 30 minutes, cowork-reviewer)

**Owner:** cowork-reviewer
**Output:** `agent-exchange/handoff/<date>-phase7-verify.md` (Template 4 post-L22 form)

**Stage A — git plumbing verify of IMPLEMENT:**
- `git show <branch-tip>:Assets/Scripts/New_Buddah/Debug/JitterCapture.cs` → file exists + UNITY_EDITOR guarded
- `git show <branch-tip>:Tools/Analysis/AnalyzeJitter.ps1` → file exists + executable PowerShell
- `git diff --stat origin/dev <branch-tip>` → only the 2 new files + (if Q4) optional event-exposure on motor.cs
- Stage 6 pre-grep gate: `git status --short` filtered to Phase 7 scope clean (modulo CRLF / mount-truncation discrimination per Rule 2 sub-clause)

**Stage B — independent capture-file inspection:**
- All expected CSV files present at `raw/` paths with non-trivial size
- AnalyzeJitter.ps1 runs end-to-end on each file without errors
- Q0 metric values reproduced independently from CSV (NOT trust implementer's digest)

**Stage C — Phase 7 specific gates:**
- C.1 Q3 absolute threshold met for all 3 paths × per-peer
- C.2 Q3 secondary regression check (Path-B-LatencySim ≤ 110% Path-B-no-LatencySim)
- C.3 Q4 rec-snap-distance distribution sane (max < some Q3-derived value)

**Stage D — anomaly resolution:**
- Document any visual jitter observed by driver during SMOKE that didn't
  show in metric (operator perception > number trumps)
- Document any single-frame outliers in CSV (could be Editor stutter, not
  predicted-motor jitter)

**Stage E — sign-off matrix + reflective lesson section.**

---

### Stage 7 — MERGE + Phase 7 CLOSEOUT (estimated 15 minutes)

**Owner:** Yonezawa (merge button) + cowork-reviewer (closeout)

**Closeout decision tree:**

```
Phase 7 verify result
├─ STRICT PASS (no regression, no Phase 7.5 needed)
│   ├─ Close task #27 (Phase 7 main)
│   ├─ Phase 6 (task #26) unblocked — ready for separate KICKOFF when scheduled
│   ├─ Phase 8 (task #28) unblocked
│   ├─ phase7-contract.md → archive/
│   ├─ Lessons-log: any new entries from observation phase
│   └─ README phase pointer update
│
└─ REGRESSION FOUND (Q5-A path)
    ├─ Phase 7 still merges (it's a measurement phase)
    ├─ Close task #27 with "regression found, Phase 7.5 retrofit scheduled"
    ├─ Author Phase 7.5 contract draft at agent-exchange/handoff/<date>-phase7-5-kickoff-draft.md
    ├─ Phase 7.5 contract scope: specific regression observed + retrofit hypothesis
    ├─ Phase 6 / Phase 8 STILL unblocked (Phase 7.5 is parallel, not blocking)
    └─ phase7-contract.md → archive/ + phase7-5-contract.md → active/ on KICKOFF
```

---

## Quick reference — which methodology rules apply where

| Rule | Where in Phase 7 |
|---|---|
| Rule 1 — Raw log discipline | Stage 5: CSV files at `raw/` per naming convention |
| Rule 1-D — Post-smoke event sanity check | Stage 5: confirm non-empty + ≥1 RECONCILE row before dumping |
| Rule 2 — Independent verification | Stage 6: cowork-reviewer reproduces metrics from CSV, NOT trust digest |
| Rule 2 — git plumbing primary (L22) | Stage 6: all "X file exists + correct" claims via `git show <ref>:<path>` |
| Rule 2 — Pre-grep gate drift discrimination | Stage 6: discriminate CRLF / mount-truncation drift before halting |
| Rule 3 — Strict gate in contract | Stage 6: Q3 thresholds set in design Q&A, locked when contract stamps Stage 4 sign-off |
| Rule 4 — Sign-off ledger sequential | All stages |
| Rule 5 — Lessons-log timing | Stage 4 commit: any new lessons land in same commit |
| Rule 6 — PRE-WORK persistence | Carry-forward to Phase 6 + Phase 8 contracts MUST land in those contracts' MD files |
| Rule 7 — PredictionRigidbody integrity | Stage 4 review: JitterCapture is read-only, does NOT touch rb |
| Rule 11 — SMOKE driver hard-precondition | Moot for current Phase 7 scope (no time-sensitive probes); revisit if Q4 Phase 7.5 needs onset-timing |
| Rule 12 — Reviewer sign-off authorship | All sign-off rows: implementer does NOT pre-fill reviewer rows |

---

## Approximate timeline (assumes Yonezawa availability)

| Stage | cowork-reviewer time | implementer time | driver time | wall clock |
|---|---|---|---|---|
| 1 KICKOFF | 5 min | — | — | 5 min |
| 2 RECON | 15 min review | 30-60 min | — | 1-2 days (reviewer may not be live) |
| 3 DESIGN-QA | 15 min review | 30 min | — | 1 day |
| 4 IMPLEMENT | 15 min diff review | 1-2 hours | — | 1 day |
| 5 SMOKE | 5 min spec review | 30 min scrape | 1 hour drive | 1 session |
| 6 VERIFY | 30 min | — | — | 1 day |
| 7 MERGE + closeout | 15 min | 5 min | — | 1 day |
| **Total** | ~100 min reviewer | ~3-4 hours implementer | ~1 hour driver | ~1 week wall |

Faster than V5 (which was 2-week wall) because Phase 7 is single-trip
observation; no mid-flight pivots expected unless Q4 surfaces snap-onset
issues that need Phase 7.5.

---

## Cross-reference index

- Contract draft: `agent-exchange/handoff/2026-05-03-phase7-kickoff-draft.md`
- Phase 4b retrospective housekeeping handoff: `agent-exchange/handoff/2026-05-03-phase4b-retrospective-housekeeping.md`
- Methodology rules: `Docs/phase-gates/methodology.md` (post V5 closeout has Rules 1-12)
- Templates: `Docs/phase-gates/templates.md` Template 4 (post-L22 form is the verify report shape Phase 7 will use)
- Helper script: `Tools/Harness/Get-BuddahGoHarnessContext.ps1` (post V5 closeout surfaces active phase)
