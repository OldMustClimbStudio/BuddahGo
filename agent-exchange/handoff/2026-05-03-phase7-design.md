# phase7-visual-jitter — Design Q&A

**Recon reference:** `agent-exchange/handoff/2026-05-03-phase7-recon.md` (commits `edd55ce` initial + `fea8a40` Surface 5 amendment); reviewer Recon row stamped at `fd576cd`.
**Status:** DESIGN PROPOSAL — no code changes yet
**Branch:** `feat/phase7-visual-jitter-evaluation`
**Q answer order:** Q1 → Q2 → Q0 → Q4 → Q3 → Q5 (per `process-flow.md` Stage 3 dependency tree)
**Rule 12 honored:** zero pre-fill of Design / Verify rows in `phase7-contract.md`.

---

## Q1 — Capture mechanism

**Picked:** **Q1 = A' (reuse existing `BuddahPredictionVisualShakeProbe.cs`)**, per recon Section 5b option (a) and Recon-row stamp accepting the lean revision.

**Justification:**
- `Assets/Scripts/New_Buddah/Debug/BuddahPredictionVisualShakeProbe.cs` (176 LOC, gated behind `BUDDAH_PREDICTION_VISUAL_PROBE`) already implements every requirement of the original Q1=A scaffold: pinned visual root pose sampled in `LateUpdate` (post-animation, post-camera); pre-allocated ring buffer of per-frame Δposition + Δrotation; `[D-VIS HEARTBEAT]` emit every N frames with `pos-dmax / pos-dp99 / pos-davg` and `rot-dmax / rot-dp99 / rot-davg` aggregates; L15 zero-quaternion → identity normalize; runtime-attach via `BuddahPredictionVisualRootBridge.GetVisualRoot()`; `[DisallowMultipleComponent]` + G1-compliant zero-allocation hot-path.
- Capture target requirement from recon Section 2 KEY FINDING (visible pixels = `_graphicalObject` child transform smoothed by FishNet's `NetworkObject` built-in smoother, NOT motor.transform) is satisfied by the probe's `_visualRoot` field, which is wired to the same transform via `VisualRootBridge.GetVisualRoot()`.
- Reuse over rebuild also captures the lessons embedded in the V3 probe (L15 zero-quat handling, G1 zero-allocation contract, runtime-fallback resolution path) for free, instead of risking re-introducing solved bugs.

**Scope (Phase 7 IMPLEMENT):**
- **Zero code changes** to `BuddahPredictionVisualShakeProbe.cs` itself.
- **Add `BUDDAH_PREDICTION_VISUAL_PROBE`** to `Project Settings → Player → Scripting Define Symbols` for the Phase 7 SMOKE Editor session(s). This is the only "engagement" lever; once the symbol is set, the probe compiles in and `BuddahPredictionBootstrap` runtime-attaches it (see motor wiring in `BuddahPredictionBootstrap.cs:108-111` for the pattern; the probe uses the same `VisualRootBridge` resolver).
- **Add an offline analyzer** at `Tools/Analysis/AnalyzeJitter.ps1` (~80 LOC, no dependency on Python or external tooling) that:
  - Ingests one or more Editor.log files (per Phase 7 SMOKE path naming convention).
  - Greps lines starting with `[D-VIS HEARTBEAT]`.
  - Parses the per-heartbeat aggregate values (`pos-dmax`, `pos-dp99`, `pos-davg`, `rot-dmax`, `rot-dp99`, `rot-davg`, `owner=`, `frame=`, `window=`).
  - Reports per-path tabular summary + cross-path comparison (Path-A vs Path-B-no-LatencySim vs Path-B-100ms; HOST vs CLIENT for 2-peer paths).
  - Computes Q3 absolute threshold pass/fail per peer per path, and Q3 secondary 110% regression check between Path-B-no-LatencySim and Path-B-100ms per peer.
- **Wiring spot-check during SMOKE setup:** the V3 probe's runtime-attach assumes `BuddahPredictionBootstrap` instantiates it after the visual-root bridge resolves. Implementer confirms during SMOKE setup that `[D-VIS HEARTBEAT]` lines actually appear in Editor.log within the first 1-2s of PlayMode (per Rule 1-D post-smoke event sanity check). If not, fall back to manual `AddComponent<BuddahPredictionVisualShakeProbe>()` on the Buddah prefab instance for the duration of SMOKE.

**Risk:**
- **R1.1 — V3 probe was authored for a different gating purpose** (gameplay-shake observation in Phase 4 V3). Repurposing it for Phase 7 means Phase 7's results are interpreted through V3's design choices (60-frame default heartbeat window, ring-buffer overwrite policy, etc.). Mitigation: document these design choices verbatim in the Phase 7 verify report so reviewer can independently judge whether they're appropriate for Phase 7's semantics. Configurable via the `_heartbeatFrames` SerializeField (default 60, min 16) — no code change required to tune.
- **R1.2 — `BUDDAH_PREDICTION_VISUAL_PROBE` is a project-wide define.** If any other code path is gated on the same symbol, enabling it for Phase 7 SMOKE may bring unrelated code online. Mitigation: implementer greps `BUDDAH_PREDICTION_VISUAL_PROBE` across the repo before Stage 4 IMPLEMENT to confirm only the V3 probe references it. Documented in IMPLEMENT step 1.
- **R1.3 — Define symbol must be REMOVED post-SMOKE.** Phase 7 is observation-only; SMOKE-required defines must not persist into production builds. Mitigation: documented as the last step of Stage 5 SMOKE per-path procedure (remove `BUDDAH_PREDICTION_VISUAL_PROBE` after final SMOKE run + before VERIFY). Verify report Stage A includes "scripting-define-symbols clean" check.

**Open questions:** none — Recon row already stamped Q1=A' as DECIDED.

---

## Q2 — Comparison baseline

**Picked:** **Q2 = (B) Path B no-LatencySim vs Path B 100ms LatencySim only.** Fall back from initial lean (C) per the apples-to-oranges concern flagged in recon Section 6.

**Justification:**
- Recon Section 6 surfaced pre-Phase-4b SHA `2c63bc5` as the only candidate for option (A). That commit predates the entire prediction-v2 stack (Phase 0+1 scaffolding lands AFTER it). Buddah movement on `2c63bc5` runs through the LEGACY (pre-V2) code path, with FishNet prediction either disabled or in legacy-only mode.
- Comparing legacy-path visual jitter to post-V5 prediction-stack visual jitter is **a comparison between different rendering pipelines**, not a measurement of Phase 4b's refactor impact on a single pipeline. Specifically: legacy path may not even use the FishNet `_graphicalObject` smoother (or use it differently), so the Section 2 KEY FINDING ("captured position is post-smoother") is no longer a stable invariant across the comparison.
- An honest characterization of "did Phase 4b regress visual fidelity" requires comparing two states of the same pipeline. Since V1 onward all use the same prediction-v2 visual pipeline, the cleanest within-pipeline comparison is **the LatencySim-vs-no-LatencySim delta on post-V5 dev tip** — i.e., option (B). This isolates "reconcile-replay visual cost under realistic network conditions" from "rendering-pipeline differences", which is the more actionable measurement and the one that ties directly to Phase 4b's wire-format + reconcile-callback work.
- The retrospective interpretation "legacy-path is what V1-V5 wanted to replace, so by definition refactor-impact = legacy vs post-V5" is acknowledged but rejected: V1-V5's stated goal was correctness (FATAL=0 / determinism / spawn-window latch / cross-peer Tier 1 chain), NOT visual fidelity. Visual fidelity was an implicit constraint, not a measured target. Repurposing Phase 7 to retroactively grade Phase 4b on a target Phase 4b never claimed would produce a misleading number — Phase 4b might "fail" Q3 on metrics it never optimized for, even though the new pipeline is otherwise correct.
- (B) is the bounded measurement Phase 7 can actually defend.

**Scope (Phase 7 SMOKE):** drop Path A from "single-machine 30s baseline" to "single-machine 30s sanity" — used only to confirm probe wiring + emit format works in isolation, not as a comparison reference. The two real comparisons are within the 2-peer Path B family.

**Risk:**
- **R2.1 — Loses the "did the refactor regress vs pre-refactor" answer.** Acknowledged. Phase 7 explicitly does NOT answer that question. If a future stakeholder asks, the answer is "out of Phase 7 scope; would require Phase 7.5 with a checkout-`2c63bc5` SHA-pinned baseline run, which is non-trivial due to scene + scripts compatibility across the V1-V5 era". Documented in PR description Carry-forward flags.
- **R2.2 — Path A 30s sanity becomes vestigial.** Mitigation: keep it for probe-wiring confirmation (per Q1 R1.3), but explicitly NOT a Q3 gate — drop the contract's Path-A gate from "Q3 absolute threshold met" to "probe emits at least 1 `[D-VIS HEARTBEAT]` line within 30s, confirming wiring works".

**Open questions:**
- **OQ2.1** — should the contract's Path-A strict-gate section be amended in this PR's Stage 3 commit, or left as-is and addressed by reviewer at Stage 6 anomaly disposition? Implementer recommends amend now (cleaner verify-report semantics); reviewer decides.

---

## Q0 — Metric definition

**Picked:** **Q0 = (C) report both (A) trend metric + (B) snap-event count, BUT with (B) primarily delegated to Q4 sibling probe.** Vocabulary aligned with the existing V3 probe emit.

**Picked metrics + vocabulary:**

| Phase 7 metric | Source emit | Definition | Window |
|---|---|---|---|
| `pos-davg` | `BuddahPredictionVisualShakeProbe` `[D-VIS HEARTBEAT]` line | **arithmetic mean** of per-frame Δposition magnitudes (meters) over the heartbeat ring-buffer window | 60-frame ring (1s @ 60fps) |
| `pos-dp99` | same | 99th-percentile of per-frame Δposition magnitudes (meters) | same |
| `pos-dmax` | same | maximum per-frame Δposition magnitude in the window (meters) | same |
| `rot-davg` | same | arithmetic mean of per-frame Δrotation angle (degrees) over the window | same |
| `rot-dp99` | same | 99th-percentile per-frame Δrotation (degrees) | same |
| `rot-dmax` | same | maximum per-frame Δrotation in the window (degrees) | same |
| `rec-snap-max` | **NEW**: `BuddahPredictionReconcileSnapProbe` `[D-REC HEARTBEAT]` line (Q4) | maximum per-`[Reconcile]`-event pre/post position diff (meters) over rec ring-buffer window | per heartbeat (TBD by Q4 design — proposed 60 events or 1 second whichever first) |
| `rec-snap-p99` | same | 99th-percentile rec-snap distance (meters) | same |
| `rec-snap-avg` | same | arithmetic mean rec-snap distance (meters) | same |
| `rec-events-window` | same | count of `[Reconcile]` callback events recorded in the window | same |

**Justification:**
- Aligning vocabulary with existing emit names (`pos-dmax / pos-dp99 / pos-davg` + `rot-*`) saves analyzer plumbing (no field-rename mapping) and keeps the Phase 4b digest-line vocabulary consistent (both Phase 4b's `[D-LOC HEARTBEAT]` and Phase 7's `[D-VIS HEARTBEAT]` follow the `<prefix> field=value` convention).
- Recon Section 5b explicit caveat: `pos-davg` is **arithmetic mean of |Δposition|**, not strict RMS. If reviewer or downstream verify needs RMS specifically, the analyzer can compute `rms = sqrt(mean(Δp²))` offline by re-greping per-frame deltas — but that requires a per-frame log line, which V3 probe does NOT emit (only per-heartbeat aggregates). Phase 7 accepts arithmetic-mean as the trend metric; RMS is out of scope for Phase 7. Documented as Q0 design choice, not a defect.
- Q0=B "frame fraction with Δv > threshold" is not directly emitted by V3 probe (no per-frame log + no built-in threshold counter). Two options were considered:
  - (option-B-approx) Approximate via heartbeat-window `pos-dmax` outlier counting at ~1-second granularity. Coarse but workable.
  - (option-B-delegate) Delegate primary snap-event detection to Q4's `BuddahPredictionReconcileSnapProbe`, which by construction fires only on `[Reconcile]` callbacks (i.e., the events most likely to produce a visible snap). Snap events that don't correspond to a reconcile event are presumed rare and detected only via `pos-dmax` outliers as a backup signal.
- **Decided:** option-B-delegate. Reasoning: Phase 4b V5 measured `rec-cb=2595` callbacks per 53 HBs on the CLIENT under 100ms LatencySim — reconciles fire VERY frequently and are by far the dominant source of potential snap events. Q4's per-event probe captures these at full resolution. The remaining "non-reconcile snap" failure mode (e.g., a buggy direct rb write) would manifest as `pos-dmax` outliers in Q0, providing a backup signal at heartbeat granularity. Two probes covering the failure modes at appropriate timescales is more honest than one probe approximating both.

**Risk:**
- **R0.1 — `pos-davg` mean vs RMS.** Mean masks variance: a stream `[1, 1, 1, 1]` and a stream `[0, 2, 0, 2]` have the same mean. Mitigation: `pos-davg` is paired with `pos-dp99` and `pos-dmax` — variance shows up there. The triple is sufficient to characterize jitter shape. Reviewer to validate at Stage 6.
- **R0.2 — Owner/spectator interp differ.** V3 probe emits `owner=true/false` per heartbeat. Owner uses `_ownerInterpolation: 1` tick smoothing; spectator uses `_spectatorInterpolation: 2`. These produce DIFFERENT smoothness baselines even on a non-jittery system. Phase 7 analyzer MUST tabulate per-`owner` separately and Q3 thresholds MUST be evaluated per-`owner` not aggregated. Documented in analyzer spec.
- **R0.3 — Heartbeat frame count alignment.** Default `_heartbeatFrames = 60` ≈ 1s @ 60fps, but if the Editor runs at lower fps (heavy SMOKE machines), 60 frames may correspond to 1.5-2s. Mitigation: analyzer parses `frame=` from the emit line and computes elapsed seconds per heartbeat as `(frame_N+1 - frame_N) / target_fps_assumed_60`; if the rate diverges by >10%, analyzer flags the run as "non-nominal cadence" and reviewer disposes per Stage 6 anomaly resolution.

**Open questions:**
- **OQ0.1** — should Q3 absolute thresholds (Q3 below) gate on `pos-dmax`, `pos-dp99`, or `pos-davg`? Implementer proposes `pos-dp99` as primary (filters out single-frame outliers but catches sustained jitter) + `pos-dmax` as secondary spike-cap. Reviewer to confirm.

---

## Q4 — Reconcile snap probe (sibling to V3 probe)

**Picked:** **Q4 = (C) capture rec-snap-distance + defer analyzer fail-condition**, implemented as a **sibling probe**, NOT as an extension of `JitterCapture` (which doesn't exist anyway under Q1=A').

**Sibling probe design:**

| Property | Value |
|---|---|
| File | `Assets/Scripts/New_Buddah/Debug/BuddahPredictionReconcileSnapProbe.cs` |
| Define gate | `#if BUDDAH_PREDICTION_RECONCILE_PROBE` (separate symbol from `BUDDAH_PREDICTION_VISUAL_PROBE`) |
| Compiles out when undefined | yes — entire file under `#if`, real path byte-identical |
| Component model | `[DisallowMultipleComponent] sealed class` + MonoBehaviour, mirroring `BuddahPredictionVisualShakeProbe` |
| LOC budget | ~40 LOC (excluding define/using/namespace boilerplate) |
| Hook point | post `_reconcileCallbackCount++` at `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:550` (per recon Section 3 verified location). Implementation: motor.cs adds a `UNITY_EDITOR`-guarded event (e.g., `internal static event Action<Vector3, Vector3>? OnReconcileSampled`) fired with `(prePos, postPos)` AFTER the counter increment AND AFTER state restoration completes. The probe subscribes in `OnEnable`, unsubscribes in `OnDisable`. |
| Pre/post position capture | `prePos` = motor.transform.position observed at the START of the `[Reconcile]` callback (line 549, before state restoration); `postPos` = motor.transform.position observed at the END of the callback after state restoration. The event fires once with the pair. |
| Ring buffer | `float[]` snap-distance buffer of configurable size (`_eventWindowSize`, default 60 — 60 reconcile events per heartbeat, NOT 60 frames). One write per `OnReconcileSampled` event; one Array.Copy + Array.Sort per heartbeat. Zero per-event allocation (G1 compliant). |
| Heartbeat trigger | every `_eventsPerHeartbeat` events (default 60). Emit only after buffer has at least `_eventWindowSize` samples (i.e., warmup period before first emit, mirroring V3 probe's `_samplesCollected < window` gate). |
| Output prefix | `[D-REC HEARTBEAT]` (parallels `[D-VIS]` and `[D-LOC]`) |
| Emit format | `[D-REC HEARTBEAT] frame=<N> events-in-window=<W> rec-snap-max=<F5> rec-snap-p99=<F5> rec-snap-avg=<F5> owner=<bool>` |
| Owner tag | resolves via serialized `NetworkObject` reference (mirroring V3 probe), with runtime fallback to `GetComponentInParent<NetworkObject>()` if unset |
| L15 normalize | NOT applicable (no quaternion math in this probe; only Vector3 magnitude) |

**Justification:**
- Sibling-probe design preserves V3 probe's authorship contract (Phase 4 reviewer V3 + G1 binds remain on the original file; Phase 7 doesn't touch them).
- Separate define (`BUDDAH_PREDICTION_RECONCILE_PROBE` vs `BUDDAH_PREDICTION_VISUAL_PROBE`) lets reviewer or driver toggle them independently. Phase 7 SMOKE enables both; future runs can enable just one.
- Hook-point choice — motor.cs:550 — is the minimum-touch insertion that captures the actual reconcile snap event without entering the [Reconcile] callback's reset logic. Adding a `UNITY_EDITOR`-guarded event (single line) preserves the Phase 4b "no behavior change" promise per the process-flow IMPLEMENT discipline note.
- Ring buffer keyed on **events**, not frames, because reconciles are event-triggered (their cadence depends on LatencySim + network conditions), not frame-rate. A frame-keyed window would over- or under-sample depending on rec-cb rate.

**Scope (Phase 7 IMPLEMENT for Q4):**
- New file: `Assets/Scripts/New_Buddah/Debug/BuddahPredictionReconcileSnapProbe.cs` (~40 LOC + boilerplate, under `#if BUDDAH_PREDICTION_RECONCILE_PROBE`).
- New `.meta` file (Unity auto-generates).
- Single-line addition to `BuddahPredictedMotor.cs` near line 550: `UNITY_EDITOR`-guarded event declaration + invoke. Exact placement TBD by Stage 4 IMPLEMENT but pre-cleared with reviewer that this is the ONE motor.cs touch allowed in Phase 7.
- Optional (nice-to-have, not blocking): `BuddahPredictionBootstrap` runtime-attach path for the Q4 probe paralleling the V3 probe wiring. If skipped, driver manually `AddComponent<BuddahPredictionReconcileSnapProbe>` for SMOKE.
- Analyzer extension: `Tools/Analysis/AnalyzeJitter.ps1` greps `[D-REC HEARTBEAT]` lines too and reports `rec-snap-max / rec-snap-p99 / rec-snap-avg / events-in-window` per peer per path.

**Risk:**
- **R4.1 — Adding an event to motor.cs is technically a behavior change** (heap ref + method-table slot). Acknowledged but assessed as zero-runtime-cost when `BUDDAH_PREDICTION_RECONCILE_PROBE` undefined (event field compiles out under `#if UNITY_EDITOR && BUDDAH_PREDICTION_RECONCILE_PROBE` or similar). Reviewer pre-clearance for the motor.cs touch already in Recon row stamp. Stage 6 verify confirms `git diff origin/dev -- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` shows ONLY the guarded-event line.
- **R4.2 — Pre/post position semantics inside `[Reconcile]` callback.** The "pre" position observed at the start of the callback is the predicted position about to be replaced. The "post" position is the authoritative position after state restoration. The DISTANCE between them is the snap. But: if the [Reconcile] callback also runs replay logic (re-applying queued inputs since the reconcile tick), the "post" we observe at the end-of-callback may be at the replayed-tip position, not the authoritative-tick position. Mitigation: implementer reads the current [Reconcile] callback body at IMPLEMENT to determine which point is "the snap" semantically. If post-replay is the right anchor (visible to player), capture there; if authoritative-tick is the right anchor (root cause measure), capture earlier. Documented in IMPLEMENT step.
- **R4.3 — Rec-cb rate variance across LatencySim.** V5 measured ~2595 callbacks per 53 HBs on CLIENT under 100ms LatencySim — that's ~50 reconciles/HB. Path-B no-LatencySim may have far fewer. Heartbeat default `_eventsPerHeartbeat=60` may produce 1-2 heartbeats per minute on the no-LatencySim path. Mitigation: analyzer reports per-heartbeat counts; if window count below 30 per minute on no-LatencySim, run extends to 90s for that path to gather enough events. Driver accepts variance up-front in SMOKE plan.

**Open questions:**
- **OQ4.1** — should the motor.cs event be `internal static event` (probe subscribes via static handler) or `internal event` (probe needs motor reference)? Implementer proposes `internal static event` — simplest plumbing, no reference plumbing through Bootstrap. Reviewer to confirm at IMPLEMENT diff review.

---

## Q3 — Pass criteria (absolute thresholds + secondary regression check)

**Picked:** **Q3 = (B) primary absolute threshold + (A) secondary regression check.** Concrete numbers below are **anchor proposals from implementer**, calibrated against Buddah's max planar speed and frame timescales, but **subject to reviewer validation against the retrospective "what is good" reference table** (which the implementer does not have direct citation for; numbers below are honestly arrived at from first principles + recon Section 4 motion-budget analysis).

**Calibration anchors (first-principles derivation):**
- `BuddahPredictedMotorConfig.maxSpeed = 80f` (m/s) — from `Assets/Scripts/New_Buddah/Config/BuddahPredictedMotorConfig.cs:12`.
- Push-grace adds another `pushExtraMaxSpeed = 6f` → `FinalMaxSpeed` peaks at ~86 m/s when push-grace is active.
- At max steady-state planar speed (80 m/s) and 60fps render: per-frame Δposition ≈ **80/60 = 1.333 m/frame**.
- Steady-state (no jitter) per-frame Δp variance is dominated by `Time.unscaledDeltaTime` jitter (~±5% nominal) + reconcile-replay corrections.
- Steady-state `pos-davg` at full speed should sit at ~1.33 m; variance ~ ±0.07 m. `pos-dp99` and `pos-dmax` should be within `pos-davg + 0.20 m` for a smooth pipeline. Beyond that, the eye starts perceiving stutter on a fast-moving character.

**Q3 absolute threshold (primary):**

| Gate | Metric | Threshold | Per-peer per-path |
|---|---|---|---|
| G3.1 | `pos-dp99` − `pos-davg` | < **0.30 m** (i.e., 99th-percentile frame Δp does not exceed mean by more than 30 cm) | each per `owner=true` and `owner=false`; both Path-B paths |
| G3.2 | `pos-dmax` − `pos-davg` | < **0.60 m** (max single-frame Δp does not exceed mean by more than 60 cm) | same |
| G3.3 | `rot-dp99` | < **8°/frame** at any point during continuous-motion segment | same |
| G3.4 | `rot-dmax` | < **15°/frame** | same |
| G3.5 (Q4) | `rec-snap-p99` | < **0.50 m** (99th-percentile per-event reconcile snap distance) | same |
| G3.6 (Q4) | `rec-snap-max` | < **1.50 m** | same |

**Q3 secondary regression check:**
- G3.SEC.1: `pos-dp99` (Path-B-100ms-LatencySim) ≤ **110%** of `pos-dp99` (Path-B-no-LatencySim) per peer per `owner=` flag.
- G3.SEC.2: `rec-snap-p99` (Path-B-100ms-LatencySim) ≤ **300%** of `rec-snap-p99` (Path-B-no-LatencySim) per peer. (Higher tolerance: reconciles should be rarer on no-LatencySim, so percentage comparison is noisier; absolute G3.5 still applies.)

**Justification:**
- 30 cm dp99-vs-davg gate at 80 m/s steady-state means tolerated jitter is ~22% of nominal per-frame Δp. Visually, this corresponds to roughly 2-3 frame-stutters per second window. Above this, the user perceives "the camera is stuttering" rather than smooth motion.
- 60 cm dmax-vs-davg gate is ~45% of nominal — single worst frame per second; allows for one reconcile snap per second budget without falling foul.
- Rotation thresholds (8°/15° per frame) target the same "perceptible stutter" boundary on rotation. At a typical Buddah turn rate (TBD from Q4 IMPLEMENT inspection of FinalTurnTorque, but assume <360°/s steady-state = 6°/frame), a sustained 8°/frame jitter is visibly wrong; 15°/frame single-frame is "the model just snapped".
- Rec-snap thresholds (50 cm p99, 150 cm max) accept that 100ms LatencySim WILL produce some snaps — the question is whether they exceed the visible-cliff threshold. 50 cm at 80 m/s is roughly 0.4 frames of motion catching up; visually, the eye perceives this as "minor correction" not "teleport". 150 cm is ~1 frame of motion; visible but tolerable.
- These numbers are first-principles starting points. **Reviewer is invited to override with the retrospective "what is good" reference table values** — if reviewer-table values are tighter, accept the tighter; if looser, document the rationale and accept.

**Risk:**
- **R3.1 — Calibration honesty:** these thresholds are implementer-proposed, NOT empirically derived from a known-good baseline. They could be too tight (cause false-fail on a normally-acceptable Buddah pipeline) or too loose (let real regression slip through). Mitigation: Path-B-no-LatencySim is treated as "the baseline pipeline state we are willing to ship" — if it FAILS G3.1-G3.4, Phase 7 escalates to "the no-LatencySim pipeline itself violates user perception threshold; either (a) thresholds are wrong and reviewer relaxes, or (b) Phase 7.5 retrofit is needed even before LatencySim discussion". This is a discriminator the contract welcomes, per project memory `project_phase7_jitter.md` (visible jitter is pre-existing, Phase 7 task is to quantify it).
- **R3.2 — Frame-rate sensitivity.** Thresholds are derived assuming 60fps. If SMOKE machine runs at 30fps (heavy load), per-frame Δp doubles (2.67 m at 80 m/s) and thresholds in absolute terms may not gate the same perceptual quality. Mitigation: analyzer reads `frame=` cadence per heartbeat, computes effective fps, and rescales thresholds proportionally if effective fps < 50. Documented as analyzer feature; reviewer validates at Stage 6.
- **R3.3 — `owner=true` vs `owner=false` will systematically differ.** Owner runs predicted forward sim (interp tick=1); spectator runs delayed reconciled state (interp tick=2 + adaptive). Spectator `pos-dp99` may be naturally larger due to the longer interpolation buffer. Same gate applied to both means owner is more likely to pass than spectator. Mitigation: gates apply to BOTH but reviewer is empowered to differentiate at Stage 6 if a one-sided pattern emerges (e.g., "spectator side shows G3.1 fail at 0.35 m — implementer retroactively splits owner/spectator threshold; this is a Stage 6 amendment, not a Phase 7 retry").

**Open questions:**
- **OQ3.1** — does the reviewer "what is good" reference table override any of G3.1-G3.6? If yes, please drop in the table values and implementer adopts before SMOKE. If table is informal / not portable to this design, design Q&A signs off with the implementer-proposed numbers and Stage 6 disposes any failures via reviewer judgment.
- **OQ3.2** — should G3.5 / G3.6 (rec-snap) gate Path-B-no-LatencySim too, or only Path-B-100ms? Implementer proposes gating both but expecting near-zero `rec-snap-p99` on no-LatencySim (reconciles should be vanishingly rare absent latency). If no-LatencySim shows non-trivial rec-snap, that's a separate finding worth flagging.

---

## Q5 — Escalation path

**Picked:** **Q5 = (A) Phase 7.5 retrofit.** No recon evidence affects this Q.

**Justification:**
- Phase 7 is a measurement phase by contract. The fix scope for any regression observed is unknown until the regression is characterized (which numbers fail, on which path, owner vs spectator, sustained vs spike). Inline fix would conflate measurement with remediation, exactly the anti-pattern Phase 7 was carved out of Phase 4b to avoid.
- Phase 7.5 retrofit contract drafted ONLY if Phase 7 verify finds regression. Per Q5-A: contract draft at `agent-exchange/handoff/<date>-phase7-5-kickoff-draft.md` if escalation fires; Phase 6 / Phase 8 remain unblocked (Phase 7.5 runs parallel).

**Risk:**
- **R5.1 — User concern of "are we just shelving the jitter?"** Acknowledged. Phase 7.5 escalation IS shelving in the short term (Phase 7 closes without fix), but with explicit scope of the regression captured. The alternative (Phase 7 stays open until fix lands) blocks Phase 6/8 indefinitely, which is the cost project memory `project_phase7_jitter.md` already flags as undesirable.

**Open questions:** none.

---

## Cross-cutting concerns

### CC.1 — Two probes, two defines, two heartbeat lines

Phase 7 SMOKE engages BOTH `BUDDAH_PREDICTION_VISUAL_PROBE` (V3 probe → `[D-VIS HEARTBEAT]`) AND `BUDDAH_PREDICTION_RECONCILE_PROBE` (Q4 probe → `[D-REC HEARTBEAT]`). Editor.log will contain interleaved lines from both. Analyzer must grep each prefix independently. Pre-existing `[D-LOC HEARTBEAT]` from V5 motor instrumentation will ALSO be present (rec-cb counter, etc.); analyzer may optionally cross-reference for sanity (e.g., `[D-REC]` events-in-window should track `[D-LOC]` rec-cb delta across the same wall-time interval — if they diverge, one of the probes is broken).

### CC.2 — Define-symbol cleanup discipline

Both `BUDDAH_PREDICTION_VISUAL_PROBE` and `BUDDAH_PREDICTION_RECONCILE_PROBE` MUST be removed from Project Settings post-SMOKE per Q1 R1.3. Verify report Stage A includes "scripting-define-symbols clean (neither define present in `ProjectSettings/ProjectSettings.asset`)" check. If any define persists into the merged PR, that's a Stage 6 BLOCKER per Phase 4b "no behavior change" promise.

### CC.3 — Wire-format / atomic deployment coordination

This is observation-only Phase. No wire-format changes. No coordination required with other phases, including Phase 6 / Phase 8. Phase 7's findings inform but do not constrain Phase 6 design.

### CC.4 — Interaction with project memory `project_phase7_jitter.md`

Memory states: "visible high-frequency buddah jitter is pre-existing (confirmed at V2b Step 1 PR #34)". Phase 7's job is to quantify this. Q3 thresholds intentionally bias toward "current pipeline ought to pass; if it doesn't, that's surfacing the pre-existing concern as a measurement". Reviewer should consider: if Path-B-no-LatencySim (the pipeline state we're willing to ship today) FAILS G3.1-G3.4, Phase 7 has done its job — the pre-existing jitter is now quantified — and Phase 7.5 retrofit per Q5-A should fire on that finding alone, regardless of Path-B-100ms regression result.

---

## SUCCESS CRITERIA — operator-perception red lines

In addition to the numeric Q3 gates above, Phase 7 SMOKE driver MUST visually monitor for the three "human-eye red lines" raised in cowork-reviewer's retrospective discussion. These are operator-judgment criteria, NOT analyzer metrics. Driver records observation in SMOKE digest; reviewer disposes at Stage 6.

### SC.1 — Reconcile rubber-banding

**What to watch:** high-frequency, small-amplitude position oscillation visible as the character "vibrates" in place or along the motion path during continuous movement. NOT discrete snaps — sustained micro-correction texture.

**When most likely to appear:** Path-B-100ms-LatencySim, CLIENT side, during continuous push-pull interaction. This is the failure mode V5 `rec-cb=2595` per 53 HBs implies; the question is whether it's perceptible.

**Driver action:** during the 60s continuous-motion segment, fix gaze on the spectator-view CLIENT character (not the local-control HOST view). Note any "fuzzy" or "vibrating" appearance. Annotate timestamp + side (HOST / CLIENT) in SMOKE digest if observed.

### SC.2 — Push recoil smoothness

**What to watch:** during peer-vs-peer push interaction, does the pushed character decelerate-and-accelerate in a continuous curve, or does the velocity envelope show "step function" discontinuities (sudden velocity changes between frames)?

**When most likely to appear:** both Path-B paths, on the side BEING pushed during a peer-push event. Smoother characters arc; step-function characters jerk.

**Driver action:** during 60s continuous-motion segment, deliberately have one peer push the other multiple times. Record subjective rating (smooth / mostly smooth / visibly jerky / clearly broken) in SMOKE digest. Reviewer interprets against `pos-dp99` and `pos-dmax` numerics for the same time window.

### SC.3 — Hitbox/visual desync (visual lags rb 1-2 frames)

**What to watch:** when peer A and peer B make contact (push interaction), does the visual contact happen at the same moment the gameplay registers the collision, or does the visual mesh appear to penetrate the other character's mesh BEFORE the push response fires?

**Why this matters:** the visual root is FishNet-smoothed (interp 1 or 2 ticks); the rigidbody collides at the rb's actual position. If smoothing introduces visible-vs-rb lag on the order of 1-2 render frames, players will perceive "the hitbox is wrong" even though the gameplay is correct.

**When most likely to appear:** Path-B-100ms-LatencySim, both sides, during peer push events. Especially noticeable with the spectator-interp-2-tick character.

**Driver action:** during the 60s segment, deliberately stage 2-3 peer-push events while maintaining a viewing angle that shows the contact line clearly. Annotate "no visible desync" / "minor desync at peer side X" / "clear desync, visual penetrates" per event in SMOKE digest. Reviewer cross-references with `rec-snap-max` for the same events.

---

## SMOKE plan annotations (driver workflow)

Per process-flow Stage 5, each path runs as documented. Phase 7 design adds:

- **Pre-PlayMode setup:** add `BUDDAH_PREDICTION_VISUAL_PROBE;BUDDAH_PREDICTION_RECONCILE_PROBE` to Project Settings → Player → Scripting Define Symbols. Wait for Editor recompile completion.
- **Path A — single 30s sanity:** confirm `[D-VIS HEARTBEAT]` AND `[D-REC HEARTBEAT]` both appear in Editor.log within first 5s. If either missing, abort run + inspect probe attachment. Do NOT proceed to Path B until sanity passes.
- **Path B — both variants:** drive 60s+ continuous motion with explicit peer-push interaction (per SC.1, SC.2, SC.3 driver actions). Save Editor.log to `agent-exchange/console/raw/<date>-phase7-<role>-<latency-tag>-jitter.log` — note the file holds full Editor.log content, which the analyzer greps by prefix; not separated into `.csv` per the original contract scaffold (Q1=A' delivers .log not .csv).
- **Per-path digest entry:** SMOKE digest annotates SC.1 / SC.2 / SC.3 observations + initial driver impression + Editor.log file path. Analyzer numerics fill in at Stage 6 VERIFY.
- **Post-SMOKE cleanup:** REMOVE both define symbols. Confirm `git diff -- ProjectSettings/ProjectSettings.asset` shows no scripting-define-symbol residue. If residue, revert that file before pushing.

---

## Awaiting sign-off

Reviewer's checklist for Stage 3 DESIGN-QA SIGN-OFF:

- [ ] Q1 scope acceptance: Phase 7 IMPLEMENT = define addition + analyzer + zero V3-probe code change. Includes IMPLEMENT step "grep `BUDDAH_PREDICTION_VISUAL_PROBE` to confirm only V3 probe references it" (R1.2 mitigation).
- [ ] Q2 fall-back to (B) accepted: implementer's reasoning that "legacy-vs-post-V5 is cross-pipeline, not refactor-impact" stands; Path A demoted to wiring sanity only. (Alternatively reviewer overrides back to (C); design Q&A would amend.)
- [ ] OQ2.1 disposition: amend contract Path-A strict-gate now, or at Stage 6.
- [ ] Q0 vocabulary alignment confirmed; davg-mean-not-RMS accepted; option-B-delegate-to-Q4 accepted.
- [ ] OQ0.1 disposition: gate primary metric = `pos-dp99` + secondary `pos-dmax`?
- [ ] Q4 sibling-probe design accepted (file path, define symbol, hook point, emit format, ring-buffer keyed on events not frames).
- [ ] Motor.cs:550 single-line event addition pre-cleared.
- [ ] OQ4.1 disposition: `internal static event` vs `internal event`.
- [ ] Q3 thresholds either (a) adopted as-is, (b) overridden by reviewer's retrospective "what is good" table values, or (c) refined via collaborative pass before SMOKE.
- [ ] OQ3.1 / OQ3.2 disposition.
- [ ] SUCCESS CRITERIA SC.1 / SC.2 / SC.3 acknowledged + driver workflow agreed.
- [ ] Stage 4 IMPLEMENT authorized: implementer cuts the Q4 probe + analyzer + define addition.

Implementer stops here pending Stage 3 sign-off. Per Rule 12, Design row in `phase7-contract.md` is left as `⏸` for reviewer's independent stamp.
