# Phase 7 — Visual Jitter Quantitative Re-evaluation Contract

**Phase ID:** phase7-visual-jitter
**Branch:** `feat/phase7-visual-jitter-evaluation` (to be cut from dev tip post Phase 4b CLOSEOUT — dev tip should include PR #41 + #42 + #43 merges)
**Risk:** LOW (observation-only by default; escalates only if regression is uncovered)
**Status:** KICKOFF (Stage 1 stamped 2026-05-03; Stage 2 RECON authorized)
**Predecessor:** Phase 4b (V1→V5) DONE per dev tip after PR #43 merge (archive backfill closeout — full Phase 4b documentation closure)
**Unblocked by:** task #36 + V5 closeout (PR #41) + retrospective housekeeping (PR #42) + archive backfill (PR #43)

> Promoted from `agent-exchange/handoff/2026-05-03-phase7-kickoff-draft.md`
> on 2026-05-03 by Yonezawa explicit trigger ("开phase7"). Kickoff stamp
> applied by cowork-reviewer per process-flow Stage 1 protocol. Companion
> process flow doc remains at `agent-exchange/handoff/2026-05-03-phase7-process-flow.md`.

---

## Scope (locked)

**In scope:**
- Define a quantitative jitter metric for the BuddahPredicted character on screen during 2-peer LAN play (baseline + 100ms LatencySim)
- Implement a `JitterCapture.cs` Editor utility that logs `(tick, transform.position, transform.rotation, Time.unscaledDeltaTime)` per rendered frame to a per-session file
- Run a 3-path smoke: Path A single, Path B 2-peer no-LatencySim, Path B 2-peer 100ms LatencySim — capture jitter file per peer per path
- Build a small offline analyzer script (PowerShell preferred) that ingests the jitter files and produces metric values (per Q0 definition)
- Compare metrics across paths to (a) detect regression vs Phase 4b expectations and (b) quantify reconcile-replay visual cost under LatencySim
- Document findings in a Phase 7 verify report (per templates Template 4 post-L22)

**Out of scope:**
- Any code change to `BuddahPredictedMotor.cs` / `BuddahPredictedReconcileData.cs` / channel layer / RPC pathways (Phase 4b is final on these)
- Phase 6 teleport + handoff cut-over (separate phase, but findings here MAY inform Phase 6 design; handoff via PRE-WORK section)
- Phase 8 cleanup (Roslyn analyzer + adapter caching + L9 ClampPlanarSpeed)
- ParticleSystem / sound effect timing under reconcile (separate concern; can be Phase 7.5 if surfaced)

---

## PRE-WORK questions (must answer in design Q&A phase before implementation)

### Q0 — Jitter metric definition

What single number constitutes "jitter" for this phase's PASS/FAIL gate? Pick:
- (A) **RMS frame-to-frame Δ position** (over a sliding window, e.g., last 60 frames). Cleanest single number; sensitive to small consistent wiggles + large infrequent snaps.
- (B) **Frame fraction with Δv > threshold** (e.g., percentage of frames where instantaneous velocity delta exceeds 5 m/s). Better for catching reconcile snaps; less sensitive to consistent micro-wiggles.
- (C) **Both** — report (A) for trend tracking + (B) for snap-event count. Two numbers per session.

Recommendation lean: **(C)** — they measure different failure modes. (A) catches reconcile-replay smoothness erosion (e.g., constant micro-corrections). (B) catches discrete rollback snaps (e.g., reconcile invalidating predicted position by >X meters).

### Q1 — Capture mechanism

How is the per-frame data captured? Pick:
- (A) **Editor-only `JitterCapture.cs` MonoBehaviour** attached to the BuddahPredicted character; logs to file in `OnEnable` → `LateUpdate` → `OnDisable`. Simple; only captures Editor PlayMode runs.
- (B) **Conditional-compile capture in `BuddahPredictedRepresentation.cs`** (or whatever interpolates rendered position) under `#if BUDDAH_PREDICTION_JITTER_CAPTURE` define. Captures runtime + Editor. More integration risk; requires touching prediction-stack files.
- (C) **External screen-recording + post-process video analysis**. No code change; very high analysis burden; not reproducible.

Recommendation lean: **(A)** — keeps Phase 7 strictly observation-only (no #if defines bleeding into prediction stack). 30 LOC. PlayMode-Editor coverage is sufficient for the comparison study; production runtime jitter is a separate concern best handled at Phase 7.5 if needed.

### Q2 — Comparison baseline

What is the reference state to compare against?
- (A) **Pre-Phase 4b SHA** (whatever the dev tip was before V1 merged). Tests whether Phase 4b *as a whole* changed visual fidelity.
- (B) **Phase 4b post-V5 only** (Path A vs Path B baseline vs Path B LatencySim). Tests whether LatencySim itself introduces visible regression vs no-latency baseline.
- (C) **Both** — full Phase 4b regression check + LatencySim cost characterization.

Recommendation lean: **(C)**, but if (A) requires unworkable git checkout / scene compatibility issues, drop to **(B)** — the LatencySim-vs-baseline delta is the more actionable measurement (ties directly to Phase 4b's wire-format + reconcile-callback work).

### Q3 — Pass criteria

What is the PASS/FAIL gate?
- (A) **No regression vs baseline** (chosen via Q2). Specific: post-V5 metric ≤ 110% of baseline metric (10% tolerance).
- (B) **Absolute perceptual threshold** met (e.g., RMS Δposition < 5cm/frame; snap-event count < 1 per 30s session).
- (C) **No regression AND absolute threshold met** (both must pass).

Recommendation lean: **(B) primary + (A) secondary** — absolute is the user-facing concern; regression is the dev-process check. If absolute fails, gate fails regardless of regression. If absolute passes but regression > 110%, raise as Phase 7.5 retrofit candidate (don't block Phase 7 close).

### Q4 — Reconcile snap behavior characterization

V5 measured `rec-cb=2595` callbacks per 53 HBs on the CLIENT under 100ms LatencySim — i.e., reconcile fires VERY frequently. Each reconcile resnap'd the predicted state to authoritative. Did each one cause a visible snap? Pick:

- (A) **Add `rec-snap-distance` field to JitterCapture** — record distance between pre-reconcile predicted position and post-reconcile authoritative position per [Reconcile] callback. Measure 95th percentile snap distance + max.
- (B) **Defer** — Phase 7 Q4 measurement is out of scope; rely on Q3 absolute threshold to catch any visible snap regardless of cause.
- (C) **Add probe AND deferred analyzer** — capture the field (cheap); analyze only if Q3 absolute fails.

Recommendation lean: **(C)** — capturing the field is low cost (~5 LOC) and the analyzer can be skipped if Q3 passes. If Q3 fails, having `rec-snap-distance` already in the data set saves a re-run.

### Q5 — Escalation path if regression observed

If Phase 7 verify finds visual regression, what happens?
- (A) **Escalate to Phase 7.5 retrofit** — open a follow-up phase to fix; Phase 7 closes as "regression found, retrofit scheduled"
- (B) **Block Phase 7 merge** — Phase 7 stays open until fix lands inline
- (C) **Document + accept** — Phase 4b traded jitter for correctness; document the trade and close

Recommendation lean: **(A)** — Phase 7 is a measurement phase, not a fix phase. If it finds regression, the fix scope is unknown and deserves its own phase contract. Phase 7 closes on completion of measurement, not on resolution.

---

## Strict gates (preliminary — finalized after Q0 + Q3 + Q4 design)

### Path A — single-machine 30s Editor PlayMode (wiring sanity only, NOT a comparison reference)

> **Amended 2026-05-03 per Stage 3 Q2 disposition (OQ2.1 resolved → amend now per Rule 3 strict-gate-in-contract):** Path A demoted from "baseline comparison reference" to "probe wiring sanity only". Q2 fall-back to (B) Path-B-no-LatencySim vs Path-B-100ms-LatencySim only — Q1=A' delivers `[D-VIS HEARTBEAT]` lines into Editor.log (NOT `.csv` per the original scaffold), and the legacy-vs-post-V5 framing is rejected because V1-V5's stated goal was correctness, not visual fidelity (retroactively grading Phase 4b on a target it never claimed produces a misleading number).

- Editor.log captured at `agent-exchange/console/raw/<date>-phase7-single-jitter.log` with non-trivial size
- At least 1 `[D-VIS HEARTBEAT]` line AND at least 1 `[D-REC HEARTBEAT]` line emitted within first 5 seconds of PlayMode (confirms BOTH probes wire correctly via `BuddahPredictionVisualRootBridge.GetVisualRoot()` + motor.cs:550 hook respectively)
- NO Q3 absolute threshold gate applied to Path A — passes are tabulated for transparency but do NOT pass/fail the run

If either heartbeat line is missing, abort SMOKE + inspect probe attachment before proceeding to Path B (per design SMOKE plan annotations). This is a Rule 1-D post-smoke event sanity check.

### Path B — 2-peer LAN 60s no LatencySim (primary baseline per Q2=B)
- Editor.log files for HOST + CLIENT at `agent-exchange/console/raw/<date>-phase7-{host,client}-jitter.log`
- Q0 metric values per peer per `owner=true/false` flag (R0.2 — owner/spectator interp differ; gates evaluated per-flag, not aggregated)
- Q3 absolute thresholds (G3.1-G3.6 per design) met per peer per `owner=` flag
- Q4 `rec-snap-{max,p99,avg}` per peer recorded; if non-trivial on no-LatencySim path, flag as informational finding (per OQ3.2 disposition — gate applies, near-zero expected)
- SUCCESS CRITERIA SC.1/SC.2/SC.3 driver observations annotated in SMOKE digest

### Path B — 2-peer LAN 60s with LatencySim 100ms RTT symmetric (primary characterization per Q2=B)
- Editor.log files for HOST + CLIENT at `agent-exchange/console/raw/<date>-phase7-{host,client}-100ms-jitter.log`
- Q0 metric values per peer per `owner=` flag + Q4 rec-snap distribution per peer
- Q3 absolute thresholds (G3.1-G3.6) met per peer per `owner=` flag
- Q3 secondary regression check: G3.SEC.1 `pos-dp99` Path-B-100ms ≤ **110%** of Path-B-no-LatencySim per peer per `owner=` flag; G3.SEC.2 `rec-snap-p99` Path-B-100ms ≤ **300%** of Path-B-no-LatencySim per peer (higher tolerance because no-LatencySim baseline is near-zero, so % comparison noisier)
- SUCCESS CRITERIA SC.1/SC.2/SC.3 driver observations annotated; reviewer cross-references with numerics at Stage 6
- Define-symbols cleanup confirmed post-SMOKE: `BUDDAH_PREDICTION_VISUAL_PROBE` AND `BUDDAH_PREDICTION_RECONCILE_PROBE` removed from Project Settings → Player → Scripting Define Symbols before VERIFY

---

## Deliverables

1. ⏸ Recon report — `agent-exchange/handoff/<date>-phase7-recon.md`
2. ⏸ Design Q&A — `agent-exchange/handoff/<date>-phase7-design.md`
3. ⏸ Implementation — `Assets/Scripts/New_Buddah/Debug/JitterCapture.cs` (~50 LOC) + offline analyzer in `Tools/Analysis/AnalyzeJitter.ps1` (~80 LOC) — both Editor-only / dev-only, no production runtime impact
4. ⏸ Path A jitter capture — `agent-exchange/console/raw/<date>-phase7-single-jitter.csv`
5. ⏸ Path B no-LatencySim jitter captures — `<date>-phase7-host-jitter.csv` + `<date>-phase7-client-jitter.csv`
6. ⏸ Path B 100ms LatencySim jitter captures — `<date>-phase7-host-100ms-jitter.csv` + `<date>-phase7-client-100ms-jitter.csv`
7. ⏸ Independent verify report — `agent-exchange/handoff/<date>-phase7-verify.md` (Template 4 post-L22 form)
8. ⏸ PR description with Q0 metric tables per Template 3
9. ⏸ Lessons-log entries (if any new failure modes — observation phases often surface unexpected things)
10. ⏸ Phase 7 closeout summary in PR body (close task #27 + task #36; if regression found, file Phase 7.5 contract draft)

---

## Carry-forward flags (do NOT action in this phase)

- **Phase 6 — Teleport + Handoff cut-over (task #26)** — Phase 7's findings about reconcile snap behavior under LatencySim MAY inform Phase 6 design (specifically whether the teleport-snap-vs-smooth tradeoff Phase 6 must make has visual evidence). Document Phase 7 Q4 findings in Phase 6 contract PRE-WORK section when Phase 6 kicks off.
- **Phase 8 — L9 ClampPlanarSpeed + Roslyn analyzer + adapter caching** — Phase 7 may incidentally surface ClampPlanarSpeed visual artifacts (since it strips same-tick queued impulses, predicted vs replayed velocity may diverge visibly). Note in Phase 8 contract.
- **Phase 7.5 (conditional) — Visual jitter retrofit** — only created if Phase 7 verify finds regression per Q5-A; else this flag is dropped at Phase 7 closeout.

---

## Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-03 | cowork-reviewer | Contract stamped post Phase 4b CLOSEOUT (PR #41 V5 closeout + PR #42 retrospective housekeeping + PR #43 archive backfill all merged to dev). 6 PRE-WORK Q seeded with leans (Q0=C, Q1=A, Q2=C with B fallback, Q3=B-primary+A-secondary, Q4=C, Q5=A) per kickoff draft companion at `agent-exchange/handoff/2026-05-03-phase7-kickoff-draft.md`. Process flow + dependency map at `agent-exchange/handoff/2026-05-03-phase7-process-flow.md` (~280 lines, 7 stages × concrete checklist + RECON target inventory + Q answer dependency tree + IMPLEMENT file scaffold + SMOKE per-path procedure + VERIFY 5-stage list + closeout decision tree + Rules→Stages mapping). Implementer (Claude Code) authorized to cut `feat/phase7-visual-jitter-evaluation` from dev tip + begin Stage 2 RECON. Helper script (`Tools/Harness/Get-BuddahGoHarnessContext.ps1`) should now surface this contract automatically per V5 housekeeping addition — implementer should run helper before RECON to confirm. |
| Recon | 2026-05-03 | cowork-reviewer | RECON delivered as commit `edd55ce`, Surface 5 amended in `fea8a40` post reviewer follow-up (PR #44 branch tip). 6 inventory items + 4 KEY FINDINGs (initial 2 from edd55ce: KF1 _graphicalObject smoother target / KF2 pre-4b SHA `2c63bc5` apples-to-oranges; amendment 2 from fea8a40: Q0+Q1 reuse-VisualShakeProbe / Q4 sibling-probe pattern). Pre-flight blocker (helper PS 5.1 parser fail at line 360 due to U+23F8 in regex string) caught by implementer at Stage 2 entry per stop rule, fixed via Option 3 (regex changed to date-pattern match, pure ASCII), validated end-to-end. **Q1 lean revision DECIDED: Q1=A'(reuse-existing-probe)** — option (a) in Section 5b. Phase 7 IMPLEMENT scope drops from "~50 LOC new file" to "~0 LOC code + `BUDDAH_PREDICTION_VISUAL_PROBE` define + analyzer reads `[D-VIS HEARTBEAT]` lines". Q0=C metric vocabulary aligns with existing `pos-dmax / pos-dp99 / pos-davg` + `rot-dmax / rot-dp99 / rot-davg` emit. Q0=B "frame fraction with Δv > threshold" approximation accepted: heartbeat-window `pos-dmax` outlier counting at ~1-second granularity matches user-perception timescale; primary snap-event detection delegated to Q4 dedicated sibling probe. **Q4 sibling-probe pattern ACCEPTED:** new `BuddahPredictionReconcileSnapProbe.cs` ~40 LOC under separate `BUDDAH_PREDICTION_RECONCILE_PROBE` define, hooks motor.cs:550 post `_reconcileCallbackCount++`. **Q2 apples-to-oranges:** still preserved as design Q&A risk to address. **Section 1 self-correction:** no further amendment needed — original wording strictly correct (no `*Representation` class); Sections 2 + 5b together establish `BuddahPredictionVisualRootBridge.GetVisualRoot()` as the canonical resolver, sufficient via cross-reference. **L23 + Rule 2 "Tooling change execution-test" sub-clause:** first application held under stress test (author-side execution test passed; reviewer cross-execution fell back to grep semantic-equivalence due to no-pwsh-in-Linux-container). **Rule 12 honored:** zero pre-fill of reviewer-signed rows. Verify reports at `agent-exchange/handoff/2026-05-03-phase7-recon-verify.md` (initial) + this Recon row stamp paraphrases the Surface 5 follow-up disposition. **Stage 3 DESIGN-QA AUTHORIZED.** |
| Design | 2026-05-03 | cowork-reviewer | Design Q&A delivered as commit `c409618` at `agent-exchange/handoff/2026-05-03-phase7-design.md` (284 lines, Template 2 form). All 6 Q answered in dependency order Q1→Q2→Q0→Q4→Q3→Q5 with rationale + scope + risks + open questions. **Reviewer disposition of all 5 open questions:** **OQ2.1** = AMEND NOW per Rule 3 (strict-gate-in-contract); contract Path A demoted to "wiring sanity only" + Path B sections updated to specify Editor.log capture (not .csv) + per-`owner=` flag evaluation + define-cleanup gate — applied via this same commit's contract edit. **OQ0.1** = ACCEPT — `pos-dp99` primary gate (sustained-jitter detector) + `pos-dmax` secondary spike-cap; both already in design G3.1+G3.2. **OQ3.1** = ACCEPT implementer-proposed thresholds as-is — reviewer's earlier "what is good" table was calibrated against an INCORRECT max-speed assumption (~6 m/s vs actual 80 m/s per `BuddahPredictedMotorConfig.cs:12`); when scaled to actual speed, reviewer numbers converge with implementer first-principles (G3.1 ~30cm at 22% of nominal per-frame Δp = 1.33 m/frame at 80 m/s steady-state). Implementer thresholds stand as design baseline; if Path-B-no-LatencySim itself fails any gate, that surfaces pre-existing visible jitter as a measurement → Phase 7.5 fires per Q5-A even though Phase 7 is "just measurement". **OQ3.2** = ACCEPT — gate G3.5/G3.6 applied to BOTH B-paths; near-zero rec-snap expected on no-LatencySim (reconciles vanishingly rare absent latency); non-trivial rec-snap on no-LatencySim is itself a flagged finding. **OQ4.1** = ACCEPT `internal static event` — single-event single-subscriber pattern matches "one Buddah motor per Editor session" reality and avoids per-instance reference threading through Bootstrap. **Q1=A' / Q2=B / Q0=C-with-B-delegate / Q4 sibling-probe / Q3 anchor / Q5=A** all confirmed; SUCCESS CRITERIA SC.1/SC.2/SC.3 (rubber-banding / push recoil / hitbox-visual desync) folded into SMOKE driver workflow. **R1.3 + define-symbol cleanup discipline** ratified as Stage 6 verify gate: `BUDDAH_PREDICTION_VISUAL_PROBE` AND `BUDDAH_PREDICTION_RECONCILE_PROBE` MUST be removed from `ProjectSettings/ProjectSettings.asset` Scripting Define Symbols before merge; verify confirms via `git diff origin/dev -- ProjectSettings/ProjectSettings.asset` showing no scripting-define-symbol residue. Rule 12 honored — implementer pre-filled none of Recon/Design/Verify rows; this Design row is reviewer-original. **Stage 4 IMPLEMENT AUTHORIZED.** Implementer cuts: (1) `Assets/Scripts/New_Buddah/Debug/BuddahPredictionReconcileSnapProbe.cs` ~40 LOC under `#if BUDDAH_PREDICTION_RECONCILE_PROBE`, (2) single `UNITY_EDITOR`-guarded `internal static event Action<Vector3,Vector3>? OnReconcileSampled` declaration + invoke at motor.cs:550 area (the ONLY motor.cs touch authorized in Phase 7), (3) `Tools/Analysis/AnalyzeJitter.ps1` ~80 LOC PowerShell ASCII-only ingester for `[D-VIS HEARTBEAT]` + `[D-REC HEARTBEAT]` lines. R1.2 grep confirms `BUDDAH_PREDICTION_VISUAL_PROBE` only references the V3 probe — to be re-confirmed at Stage 4 IMPLEMENT step 1. |
| Implementation | ⏸ | | |
| Smoke | ⏸ | | |
| Verify | ⏸ | | |
| Merge | ⏸ | | |

---

## Notes for the implementer (when Phase 7 actually starts)

1. **Helper script will surface this contract automatically** once `git mv`/`git rm` lands — per the V5 closeout housekeeping addition to `Get-BuddahGoHarnessContext.ps1`. Run the helper before Stage 2 RECON to confirm the contract is being read correctly.

2. **Recon Stage 2 should specifically include:**
   - Inventory: where does the visible character actually render? (BuddahPredictedRepresentation? FishNet TickSmoother? Both?) This determines where JitterCapture attaches.
   - Inventory: does the existing FishNet `NetworkTickSmoother` already do interpolation between predicted ticks? If yes, capture should be at the *visible transform* (after smoother), not the rb position.
   - Cross-ref: V5 verify report's `rec-cb=2595` finding — what does that callback frequency translate to in 60Hz frame terms?

3. **Recon should use git plumbing per L22 + Rule 2** — verify "no project-side custom representation/interpolation file exists" via `git ls-files | grep -i representation` (this preliminary draft did that grep — only FishNet built-ins surfaced; project may have its own that the grep missed; recon must confirm).

4. **Q5 escalation — be conservative.** If Phase 7 finds even mild regression (5-10% over baseline), file Phase 7.5 immediately rather than rationalizing as "within tolerance." Visual jitter is the user-facing fidelity gate; tolerances should be set BEFORE measurement (in Q3), not after seeing the numbers.

5. **Rule 11 SMOKE driver discipline applies:** Phase 7 SMOKE has no time-sensitive probes per current draft (jitter accumulates over 60s, not within a 1.2s window), so Rule 11 is moot here. But if Q4 Phase 7.5 ends up needing reconcile-snap-onset timing measurement, Rule 11 becomes mandatory.

6. **Rule 12 reviewer-sign-off-row discipline applies:** implementer SHOULD NOT pre-fill the Recon / Design / Verify rows in this contract. Implementer pre-fills are limited to Implementation + Smoke rows (own-authorship). Reviewer authors all reviewer-signed rows independently after the relevant stage completes.
