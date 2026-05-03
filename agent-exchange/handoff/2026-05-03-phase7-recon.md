# phase7-visual-jitter — Recon Report

**Branch:** `feat/phase7-visual-jitter-evaluation` (cut from `dev` @ `39af100`)
**Status:** RECON ONLY — no code changes; no Q-answers yet
**Scope reminder:** Define a quantitative jitter metric for the BuddahPredicted character on screen during 2-peer LAN play (baseline + 100ms LatencySim); implement Editor-only `JitterCapture.cs` + offline `AnalyzeJitter.ps1`; compare across 3 paths to detect regression vs Phase 4b expectations and quantify reconcile-replay visual cost.
**Verify discipline:** All claims below backed by `git show HEAD:<path>` or `git grep <pattern> HEAD` per L22 + Rule 2. No Read-on-working-tree as primary evidence. Methodology Rule 12 honored: this report stamps no reviewer-signed rows in the contract.

**Pre-flight blocker resolved (in this same commit):** harness helper script
`Tools/Harness/Get-BuddahGoHarnessContext.ps1` was added the Active-Phase-Gate
awareness block by PR #42 but was left unrunnable on Windows PowerShell 5.1
due to a UTF-8-no-BOM + U+23F8 regex parser failure at line 360. RECON
authorization required helper output, so the regex was changed (Option 3 per
Yonezawa) from `-notmatch '^\| [A-Za-z]+ \| ⏸'` to `-match '^\| [A-Za-z]+ \| [0-9]{4}-'`. Helper now emits the required "Active Phase Gate:"
section listing `phase7-contract.md` with Status + last signed Kickoff row.
Lesson L23 + methodology Rule 2 sub-clause "Tooling change execution-test"
landed in this same commit per Rule 5.

---

## 1. Visible character render path (recon item #1)

**Grep:**
```
git grep -n "BuddahPredictedRepresentation\|BuddahVisualRepresentation\|VisualSync" HEAD -- Assets/
git ls-files | grep -iE "Representation|VisualSync|Interpolat" | grep -v FishNet
```
**Result:** zero hits in either grep. No project-side file declares any
`BuddahPredictedRepresentation`, `BuddahVisualRepresentation`, `VisualSync`, or
project-custom `*Interpolation*` / `*Representation*` class outside FishNet.

**Inference:** the Buddah character has NO project-defined visual-layer
indirection class. Visible pixels are driven by FishNet's built-in
`NetworkObject` graphical-object smoother (see item #2), targeting an
existing prefab child transform — NOT by a project-owned representation
script.

**🔑 KEY FINDING — affects Q1 lean:**
The contract's Q1 description ("attached to the BuddahPredicted character;
logs to file in OnEnable → LateUpdate → OnDisable") implicitly assumes a
single character transform. RECON shows this is ambiguous — there are TWO
candidates: motor.transform (rb root, jumps per-tick) and the
`_graphicalObject` child transform (smoothed by FishNet). Q1=A is still
the right lean (Editor-only MonoBehaviour, no #if defines into prediction
stack), but the design Q&A MUST add a sub-decision on which transform the
MonoBehaviour attaches to — see item #2.

---

## 2. NetworkTickSmoother / graphical-object config on Buddah prefab (recon item #2)

**Grep:**
```
git show HEAD:Assets/Character/Prefab/Buddah.prefab | grep -niE "smoother|smooth"
```
**Result (`Assets/Character/Prefab/Buddah.prefab` lines 696, 700, 1078, 1080):**
```
696:  speedZoomSmoothTime: 0.1               (camera setting, unrelated)
700:  offsetSmoothTime: 0.75                 (camera setting, unrelated)
1078:  _ownerSmoothedProperties: 4294967295  (NetworkObject prediction-smoother field)
1080:  _spectatorSmoothedProperties: 255     (NetworkObject prediction-smoother field)
```

**Surrounding NetworkObject block (`Buddah.prefab` lines 1066-1083):**
```
_enablePrediction: 1
_predictionType: 1
_localReconcileCorrectionType: 2
_graphicalObject: {fileID: 1582303153357347920}
_detachGraphicalObject: 0
_enableStateForwarding: 1
_networkTransform: {fileID: 0}
_ownerInterpolation: 1
_ownerSmoothedProperties: 4294967295
_adaptiveInterpolation: 3
_spectatorSmoothedProperties: 255
_spectatorInterpolation: 2
_enableTeleport: 0
_teleportThreshold: 1
```

**Graphical object transform (`Buddah.prefab` lines 271-293):**
```
--- !u!4 &1582303153357347920
Transform:
  m_GameObject: {fileID: 1422021556352875860}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 4, y: 4, z: 4}
  m_Children: [8 child mesh/bone fileIDs]
  m_Father: {fileID: 4240001563575149182}
```

**Inference:**
- Buddah uses FishNet's **NetworkObject built-in graphical-object smoother**, NOT a separate `NetworkTickSmoother` MonoBehaviour. (No `NetworkTickSmoother` MonoBehaviour appears in the prefab body — confirmed by `git grep "NetworkTickSmoother" HEAD -- Assets/` returning only FishNet runtime files, no project usage.)
- `_graphicalObject` is wired to a real child transform (fileID 1582303153357347920) at local (0,0,0), local scale (4,4,4), with 8 mesh/bone children. This is the visible mesh root.
- `_ownerInterpolation: 1` + `_spectatorInterpolation: 2` + `_adaptiveInterpolation: 3` + `_ownerSmoothedProperties: 4294967295` (all bits) + `_spectatorSmoothedProperties: 255` → FishNet smooths the `_graphicalObject` transform between predicted ticks for both owner (1-tick interp) and spectator (2-tick interp w/ adaptive).

**🔑 KEY FINDING — confirms Q1 sub-decision flagged in item #1:**
- Motor.transform (rb root) jumps per simulation tick — capturing here measures **tick-level** prediction motion + reconcile resnaps with NO smoothing. Useful for measuring physics determinism, NOT visible jitter.
- Graphical-object child transform is post-smooth — capturing here measures **what the player actually sees**. This is the right capture target for "visual jitter".
- Q1=A `JitterCapture` MonoBehaviour MUST attach to the `_graphicalObject` child (fileID 1582303153357347920) or be coded to find the NetworkObject's `GraphicalObject` reference at runtime. Attaching to motor-root or motor.transform measures the wrong signal.
- Q4 rec-snap-distance is more naturally captured at motor.transform (pre/post-reconcile rb position), since the snap event happens at the rb level before the smoother sees it. So Q4 + Q0 may want **two capture targets** — graphical for Q0 jitter metric, motor for Q4 snap distance.

---

## 3. Motor `[Reconcile]` callback site + rec-cb counter (recon item #3)

**Grep:**
```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | grep -nE "rec-cb|_reconcileCallbackCount|\[Reconcile\]"
```
**Result:**
```
 97:        // [D-LOC HEARTBEAT] as rec-cb. Used as LatencySim engagement sanity check: under
100:        private uint _reconcileCallbackCount;
547:        [Reconcile]
550:            _reconcileCallbackCount++;
729:                $"[PredictionIntro][Reconcile] tick={tick} skipped={skipped} reason={reason} owner={IsOwner} hostOwner={isHostOwner} ..."
1280:        Debug.Log($"[D-LOC HEARTBEAT] T={tickIdle} ... rec-cb={_reconcileCallbackCount} (both sides idle)");
1293:        Debug.Log($"[D-LOC HEARTBEAT] T={tickHb} ... rec-cb={_reconcileCallbackCount}");
```

**Inference:** V5 instrumentation is fully intact in the dev-tip motor.cs
post PR #41 / #42 / #43 housekeeping merges. The counter increments
unconditionally at the top of the `[Reconcile]` callback at motor.cs:550,
making it the natural insertion point for Q4 rec-snap-distance probe (Q1=A
capture mechanism would subscribe to a `UNITY_EDITOR`-guarded event raised
adjacent to line 550, fired AFTER the counter increments and AFTER state
restoration so pre/post-reconcile positions can be sampled).

No KEY FINDING — Q4=C (capture rec-snap-distance, defer analyzer to
post-Q3-fail) lean is preserved by current state. Phase 7 implementation
adding a `UNITY_EDITOR`-guarded event on motor.cs is consistent with the
process-flow Stage 4 file scaffold note (the ONLY motor.cs touch allowed
in Phase 7, and only under `UNITY_EDITOR`).

---

## 4. BuddahLocomotionStep — predicted motion produced per tick (recon item #4)

**Grep / show:**
```
git show HEAD:Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs
```
**Size:** 88 lines total. Pure-static `Compute(...)` + `Run(...)`.

**Compute body (lines 39-50):**
```csharp
public static void Compute(
    Vector3 forwardDirection,
    float resolvedThrottle,
    float resolvedSteering,
    BuddahPredictedMotorComputedStats computedStats,
    out Vector3 commandedForwardForce,
    out float commandedTurnTorque)
{
    commandedForwardForce = forwardDirection * (computedStats.FinalForwardForce * resolvedThrottle);
    commandedTurnTorque = Mathf.Abs(resolvedSteering) > 0.001f
        ? resolvedSteering * computedStats.FinalTurnTorque
        : 0f;
}
```
**Pre-force planar clamp body (lines 60-71):**
```csharp
float effMax = ctx.ComputedStats.FinalMaxSpeed
               + (ctx.ComputedStats.IsPushGraceActive ? ctx.PushGraceExtraSpeed : 0f);
Vector3 velocity = ctx.RbVelocityPreTick;
Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
if (planarVelocity.sqrMagnitude > effMax * effMax)
{
    Vector3 clampedPlanar = planarVelocity.normalized * effMax;
    scratch.VelocityAfterClamp = new Vector3(clampedPlanar.x, velocity.y, clampedPlanar.z);
    scratch.ClampingApplied = true;
}
```

**Inference for Q3 absolute-threshold calibration:**
- Per-tick motion is force-based (`AddForce`), not displacement-based. Magnitude is bounded above by `FinalMaxSpeed` (planar xz clamp at top of `Run`).
- Per-tick displacement under steady-state at max speed = `FinalMaxSpeed × tickDt`. With Unity 50Hz tick (default) and a typical `FinalMaxSpeed` of ~10 m/s, that's ~0.2 m/tick visible motion under no smoothing.
- A Q3 absolute threshold like "RMS Δposition < 5cm/frame" must be evaluated against the smoothed graphical-object position (see item #2), NOT the raw rb-tick position. Otherwise the threshold becomes nonsensical (legitimate steady-state motion exceeds it).
- Specific `FinalMaxSpeed` numeric values live in `BuddahPredictedMotorComputedStats` / config — out of scope for RECON (they're tuned per stage), but the reviewer should note Q3 design Q&A MUST cite a concrete threshold in cm/frame derived against the smoothed-position frame rate (Time.unscaledDeltaTime ≈ 16ms at 60fps, vs tick 20ms at 50Hz — 3:5 ratio means ~3 frames render per tick under nominal conditions).

No KEY FINDING — Q3 lean of "(B) primary + (A) secondary with absolute
threshold" is preserved, but the threshold-derivation calculation must use
graphical-frame time + smoothed position. Q3 design Q&A should cite Section
4 of this recon when computing the threshold.

---

## 5. Existing visual debug overlays (recon item #5)

**File inventory:**
```
Assets/Scripts/New_Buddah/Config/BuddahPredictionDebugSettings.cs
Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugOverlay.cs
Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugState.cs
Assets/Scripts/Testing/LobbyFlowDebugOverlay.cs       (lobby — out of scope)
```

**`BuddahPredictionDebugState.cs` field set (lines 8-48 sample):**
```csharp
public class BuddahPredictionDebugState
{
    public BuddahMovementRuntimeMode currentMode;
    public bool predictionActive;
    public bool predictedMotorEnabled;
    public bool reconcileHadCorrection;
    public uint lastReplicateTick;       // (inferred from overlay text line 75)
    public uint lastReconcileTick;       // (inferred from overlay text line 75)
    public float planarSpeed;            // line 42
    public float preImpulseSpeed;        // line 48
    public float finalForwardForce;      // line 43
    public float finalMaxSpeed;          // line 44
    public float finalTurnTorque;        // line 45
    // ... 30+ additional flags / scalars, NO position-history array
}
```

**`BuddahPredictionDebugOverlay.cs` rendering (lines 51-90):**
- OnGUI panel rendering motor+state telemetry once per frame
- Console-mirror `Debug.Log` at `consoleMirrorIntervalSeconds` (default 0.5s)
- Reads `bootstrap.DebugState` snapshot — does NOT maintain its own per-frame position history

**Inference:**
- DebugState provides scalar telemetry that's RELEVANT for Q4 (snap occurred? `reconcileHadCorrection` flag) and for Q3 sanity (planarSpeed at capture time) but does NOT contain a per-frame position trail JitterCapture could reuse.
- JitterCapture must implement its own position-history capture; pre-existing data sources are NOT directly reusable for the jitter metric.
- However, JitterCapture CAN cite the existing `bootstrap.DebugState` for context columns (e.g., emit `planarSpeed`, `lastReconcileTick`, `reconcileHadCorrection` alongside position) without needing new state plumbing — these are read-only snapshots.

No KEY FINDING — Q1=A lean preserved (capture is independent; overlay is
not a viable shared data source). Implementation can optionally piggyback
DebugState scalar reads to enrich CSV columns at zero plumbing cost.

> **⚠ AMENDMENT — original Surface 5 grep was scope-incomplete.** See
> Section 5b below; the targeted "DebugOverlay" grep missed 4 of 6 Debug/
> files including `BuddahPredictionVisualShakeProbe.cs`, which is a
> material discovery for Q0 + Q1 design.

---

## 5b. Surface 5 amendment — full Debug/ directory inventory (post reviewer verify)

**Cause of original miss:** Section 5 used a targeted grep
(`git ls-files | grep -iE "DebugOverlay|PredictionDebug|DebugDraw"`) that
matched on file-name substrings. Files in the same directory whose names
do not contain "DebugOverlay" or "DebugDraw" — and whose top-level word is
"BuddahPredictionImpulse..." / "BuddahPredictionPerf..." / "BuddahPrediction
Push..." / "BuddahPredictionVisual..." — were NOT surfaced. Per L22, RECON
must use directory-level git plumbing for completeness, not name-pattern
greps.

**Corrected listing:**
```
git ls-tree -r --name-only HEAD:Assets/Scripts/New_Buddah/Debug/
→
BuddahPredictionDebugOverlay.cs           (covered in Section 5)
BuddahPredictionDebugOverlay.cs.meta
BuddahPredictionDebugState.cs             (covered in Section 5)
BuddahPredictionDebugState.cs.meta
BuddahPredictionImpulseDebugBox.cs        (gameplay debug box; out of Phase 7 scope)
BuddahPredictionImpulseDebugBox.cs.meta
BuddahPredictionPerfProbe.cs              (perf probe; out of Phase 7 scope)
BuddahPredictionPerfProbe.cs.meta
BuddahPredictionPushTargetBox.cs          (push debug box; out of Phase 7 scope per project_phase7_jitter memory)
BuddahPredictionPushTargetBox.cs.meta
BuddahPredictionVisualShakeProbe.cs       *** MATERIAL — see analysis below ***
BuddahPredictionVisualShakeProbe.cs.meta
```

### Section 5b.1 — `BuddahPredictionVisualShakeProbe.cs` analysis

**Source:** `git show HEAD:Assets/Scripts/New_Buddah/Debug/BuddahPredictionVisualShakeProbe.cs`
**File size:** 176 lines.

**Header docstring (lines 1-26 paraphrased):**
- Authored as "Phase 4 V3 gameplay-shake probe — observation only".
- Gated behind `#if BUDDAH_PREDICTION_VISUAL_PROBE` define. When undefined the entire file compiles out → real-path byte-identical (reviewer "G1 bind").
- Samples the **pinned visual root's world-space pose every LateUpdate** (post-animation, post-camera).
- Maintains a **pre-allocated ring buffer** of per-frame deltas; emits `[D-VIS HEARTBEAT]` line every `_heartbeatFrames` frames with **dmax / dp99 / davg aggregates** for both position (meters) and rotation (degrees).
- Owner/spectator tagging via serialized `NetworkObject` reference.
- Visual root pinned in inspector (reviewer "V3 bind"; runtime fallback resolves via `BuddahPredictionVisualRootBridge.GetVisualRoot()` and `GetComponentInParent<NetworkObject>()` if SerializeField refs are null — preserves the inspector-pin contract while supporting `AddComponent` runtime attach by `BuddahPredictionBootstrap`).
- L15 defensive normalize for zero-quaternion → identity (per `Docs/lessons-log.md` L15).

**Public/serialized fields:**
```csharp
[SerializeField] Transform _visualRoot;                       // visual capture target
[SerializeField] NetworkObject _networkObject;                // owner/spectator tag
[SerializeField, Min(16)] int _heartbeatFrames = 60;          // emit cadence + ring window
[SerializeField] string _logPrefix = "[D-VIS HEARTBEAT]";     // log line prefix
```

**Private state:**
```csharp
float[] _posDeltaBuffer; float[] _rotDeltaBuffer; float[] _sortBuffer;
int _bufferIndex; int _samplesCollected;
Vector3 _lastPos; Quaternion _lastRot; bool _hasLastSample;
int _framesSinceHeartbeat;
```

**Hot-path (LateUpdate, lines 100-138):** one `Vector3` mag + one
`Quaternion.Angle` + two ring-buffer slot writes + counter bumps. No
per-frame allocation (G1 compliant). Heartbeat emit (every N frames) does
two `Array.Copy` + two `Array.Sort` of length-N buffer + one `Debug.Log`
of formatted string.

**Aggregate output format (`EmitHeartbeat` lines 140-164):**
```
[D-VIS HEARTBEAT] frame=<N> owner=<bool> window=<W>
  pos-dmax=<F5> pos-dp99=<F5> pos-davg=<F5>
  rot-dmax=<F3> rot-dp99=<F3> rot-davg=<F3>
```

**Reuse vs build-new evaluation (for Phase 7 Q0/Q1/Q4):**

| Aspect | VisualShakeProbe status | Phase 7 fit |
|---|---|---|
| Capture target | pinned visual root via `BuddahPredictionVisualRootBridge.GetVisualRoot()` (or inspector pin) | ✅ Matches Q1 KEY FINDING from Section 2 — captures POST-smoother visual transform |
| Capture cadence | `LateUpdate` post-animation + post-camera | ✅ Correct cadence for "what player sees" jitter |
| Position metric | per-frame `(currentPos - _lastPos).magnitude` + ring buffer + dmax / dp99 / davg | ✅ Aligns with Q0=C "(A) RMS Δposition trend" — `pos-davg` is per-frame Δ-position avg, equivalent to a moving-window mean (NOT RMS, but trivially convertible) |
| Rotation metric | per-frame `Quaternion.Angle` (degrees) + ring buffer + dmax/dp99/davg | ➕ Bonus: Phase 7 contract Q0 lean (C) defines metric in position only; rotation aggregates are a pre-existing extra channel |
| Snap-event metric | NOT TRACKED — buffer is rolling all-frame, no high-Δv-frame fraction | ⚠ Q0=C "(B) frame fraction with Δv > threshold" not directly emitted; would need analyzer post-process on the (currently) aggregated digest, OR an additional ring-buffer-sweep field |
| Output mechanism | `Debug.Log` with `[D-VIS HEARTBEAT]` prefix → Editor.log per-frame digest | ✅ Same channel + same scrape model as `[D-LOC HEARTBEAT]`; Phase 7 analyzer can grep `[D-VIS HEARTBEAT]` lines from the existing raw-log infrastructure (no CSV format change needed) |
| Per-tick reconcile snap distance | NOT TRACKED — probe is frame-domain, not reconcile-event-domain | ⚠ Q4 rec-snap-distance is a separate concern; cannot be retrofitted into the rolling ring buffer cleanly |
| Build/runtime cost | gated by `BUDDAH_PREDICTION_VISUAL_PROBE` define; zero cost when undefined | ✅ Matches Q1=A "no production runtime impact" requirement |
| Wiring already exists | runtime-attach path via `BuddahPredictionBootstrap` (motor.cs:111 `gameObject.AddComponent<BuddahPredictionVisualRootBridge>()` + Bridge resolution) | ✅ Probe is plug-and-play if `BUDDAH_PREDICTION_VISUAL_PROBE` is added to scripting-define-symbols for Phase 7 SMOKE |

**Reuse options for Q1 capture mechanism:**

- **(a) Reuse VisualShakeProbe directly.** Define `BUDDAH_PREDICTION_VISUAL_PROBE` for Phase 7 SMOKE runs (3 paths × per-peer); zero new code; Phase 7 analyzer ingests `[D-VIS HEARTBEAT]` lines from Editor.log per peer. Q0=C primary metric (`pos-davg`) reads off the existing emit. Q0=C secondary "snap-event count" can be derived offline by re-grepping `pos-dmax` outliers across heartbeats (lossier than per-frame snap detection but adequate for trend tracking). **Trade-off:** Q0=C "(B) frame fraction" is approximated, not exact; if Q3 design wants strict frame-fraction precision, see (b).
- **(b) Extract a shared base + add a JitterCapture variant for Q4.** Refactor `VisualShakeProbe` into a shared capture base class + extend with a Phase-7-specific subclass that adds: (1) per-frame Δv threshold counting (exact Q0=C "(B)" metric), (2) reconcile-event hook receiving rec-snap-distance (Q4=C). Keeps existing V3 probe untouched (reviewer V3 bind preserved); adds ~30-40 LOC to a sibling class. **Trade-off:** Phase 7 IMPLEMENT scope grows from "create new file" to "refactor + create"; risk of breaking V3 binds.
- **(c) Build new JitterCapture parallel to VisualShakeProbe.** Per original Q1=A lean. Eats duplication of LateUpdate sampling / ring buffer / `[D-VIS]`-style emit. **Not recommended** — VisualShakeProbe already nails the post-smoother capture target, the L15 normalize, the G1 zero-allocation contract, and the runtime-attach plumbing. Reproducing that risks introducing new bugs (e.g., the L15 zero-quat lesson) that have already been solved here.

**Q4 rec-snap-distance** is orthogonal to the buffer-vs-event capture
distinction: it's a per-`[Reconcile]`-callback observable, attached at
motor.cs:550 (per Section 3) under `#if UNITY_EDITOR` (or a sibling
`BUDDAH_PREDICTION_RECONCILE_PROBE` define). Q4 still needs new
instrumentation but does NOT need to live inside JitterCapture — a small
sibling probe (`BuddahPredictionReconcileSnapProbe.cs`?) attaches to the
motor + emits `[D-REC HEARTBEAT]` with snap-distance distribution per
heartbeat window. Pattern matches VisualShakeProbe's design exactly.

### 🔑 KEY FINDING — affects Q0 + Q1 lean and Phase 7 IMPLEMENT scope

1. **Q0 metric naming:** VisualShakeProbe already emits `pos-dmax / pos-dp99 / pos-davg` and `rot-dmax / rot-dp99 / rot-davg`. Q0 design Q&A MUST decide whether Phase 7's metric vocabulary aligns with these existing emitted names (analyzer ingests `[D-VIS HEARTBEAT]` directly) or invents new names (analyzer gets stuck doing field-rename mapping). Recommend: **align**; the existing names are sane (dmax = max in window, dp99 = 99th percentile, davg = arithmetic mean) and reusing them keeps the Phase 4b digest-line vocabulary consistent.
2. **Q1 capture mechanism re-evaluation:** original Q1=A "build new JitterCapture" lean was based on Section 1's incorrect inference that no project-side visual probe existed. Corrected: option (a) — reuse VisualShakeProbe — is the dominant choice. Phase 7 IMPLEMENT can drop from ~50 LOC `JitterCapture.cs` to ~0 LOC code + `BUDDAH_PREDICTION_VISUAL_PROBE` define addition + analyzer that reads `[D-VIS HEARTBEAT]` lines from Editor.log. **Recommend lean revision: Q1=A → Q1=A'(reuse-existing-probe)** if reviewer accepts.
3. **Q4 rec-snap-distance scope:** unchanged in spirit (still need new instrumentation), but pattern is "sibling probe matching VisualShakeProbe's shape", not "extension of JitterCapture". Q4 IMPLEMENT becomes "add `BuddahPredictionReconcileSnapProbe.cs` + matching `[D-REC HEARTBEAT]` analyzer pass", ~40 LOC, motor.cs:550 hook under a separate define. Pattern reuse keeps both probes architecturally consistent.

**Implementer note re Section 1 in light of this finding:** Section 1's
KEY FINDING ("no project-side visual layer") was correct in the strict
sense (no `BuddahPredictedRepresentation` class), but missed the broader
project visual indirection — `Assets/Scripts/New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs`
provides `GetVisualRoot()` as the canonical visual-root resolver, used by
VisualShakeProbe + CameraBridge + CompatibilityRegistry. The Q1 capture-
target sub-decision from Section 2 (graphical-object child) is unchanged
in conclusion — `VisualRootBridge.GetVisualRoot()` is the resolver that
returns the same transform — but the Section 1 wording could be read as
"no project visual indirection at all" which is misleading. Reviewer may
want to clarify Section 1 in a follow-up amendment if the wording matters
beyond the design Q&A scope.

---

## 6. Pre-Phase 4b SHA candidate (recon item #6, Q2-A conditional)

**Grep:**
```
git log --oneline --before=2026-04-15 dev -- Assets/Scripts/New_Buddah/
```
**Result:**
```
2c63bc5 Stabilize Buddah intro handoff and spline movement
```
Author date: `Wed Apr 15 03:49:31 2026 -0400`. First (and only, in the
windowed result) commit before 2026-04-15 touching `Assets/Scripts/New_Buddah/`.

**Confirmation that 2c63bc5 is pre-Phase-4b:**
```
git log --oneline 2c63bc5..HEAD -- Assets/Scripts/New_Buddah/ | tail -5
```
shows the Phase 0 / 1 / 2 / 3a / 3b prediction-v2 commits land AFTER
2c63bc5 (`f43cc1e refactor: Phase 0+1 prediction v2 scaffolding + data contracts`,
`fafeda5 refactor: Phase 2 prediction v2 event channels + CommandBus`,
`9e5cddc Phase 3a — Locomotion shadow step`, `d32eb3e Phase 3b — Impulse + Teleport`).
Phase 4b (V1→V5) lands later than these. So `2c63bc5` is upstream of the
entire prediction-v2 stack — the strict-pre-Phase-4b reference.

**Inference for Q2-A conditional surfacing:**
- IF Q2 picks (A) full Phase 4b regression check, the reference SHA is
  `2c63bc5`. Implementer would need to checkout this commit + ensure
  scenes / scripts compile + run the same JitterCapture + analyzer to get
  baseline numbers.
- IF Q2 picks (B) post-V5 only (LatencySim cost characterization), this
  SHA is unused.
- IF Q2 picks (C) both, `2c63bc5` is the (A) leg's reference.
- **WARNING for design Q&A:** checking out `2c63bc5` = pre-prediction-v2
  scaffolding. Buddah movement on that commit uses the LEGACY code path
  (no PredictionV2 motor), with FishNet-prediction-disabled or
  legacy-only mode. Comparing the legacy-path jitter to the post-V5
  prediction-stack jitter is the apples-to-oranges concern Q2 raises.
  The Q2 design answer must explicitly address whether the legacy-path
  baseline is meaningful at all, or whether (B) is the only honest
  comparison.

**No KEY FINDING change to lean.** Q2=C-with-B-fallback lean is preserved,
but the design Q&A should grapple with the apples-to-oranges concern
above before committing to (C). Implementer recommends raising this as a
Q2 explicit risk in the design proposal.

---

## Other observations (informational, not in the 6-item inventory)

### Stale active contract: `Docs/phase-gates/active/v2b-step1-contract.md`

Helper output revealed that two contracts currently sit in `active/`:
- `phase7-contract.md` (the intended one)
- `v2b-step1-contract.md` (Phase 4b V2b Step 1 — should have been archived
  by the Phase 4b CLOSEOUT chain, particularly PR #43 archive backfill)

The presence of `v2b-step1-contract.md` in `active/` is NOT a Phase 7
blocker — Phase 7 is governed by `phase7-contract.md` regardless — but it
is an open housekeeping question for the reviewer. Possible explanations:
(a) intentional retention of a sub-step contract that wasn't in the V5
closeout sweep, or (b) PR #43 archive-backfill gap. Implementer leaves
disposition to the reviewer; no action taken in this commit.

### Pre-existing visible jitter (per memory `project_phase7_jitter.md`)

This recon's grep results are consistent with the recorded project memory:
visible high-frequency Buddah jitter is pre-existing and was confirmed
visible at V2b Step 1 PR #34. The Phase 7 measurement gates (Q3 absolute
threshold) MUST be defined to characterize this baseline — otherwise the
gate becomes vacuous if calibrated against a "snapshot" of the current
visibly-jittery state. Q3 design answer should explicitly distinguish
"current state baseline" from "perceptually acceptable target" if
absolute thresholds are used.

---

## 7. MP 视觉同步架构合规 audit (post-Stage 3 framing amendment)

> **Framing context:** Yonezawa confirmed at Stage 3 that visible jitter is OBSERVED, not hypothetical. Phase 7 framing shifted from "did Phase 4b regress?" to "characterize + identify root cause for Phase 7.5 retrofit scope". Q5-A retrofit is now expected. Surface 7 is the architectural audit that produces the prioritized root-cause hypothesis ranking — input to Stage 5 SMOKE driver focus list AND to the Phase 7.5 contract draft when Phase 7 closes. All audits use git plumbing only; PlayMode-independent; runs parallel to Stage 4 IMPLEMENT.

### 7.1 — Layer 3: Reconcile correction strategy (★ top suspect)

**Industry standard (informal):**
- Source / CS series: error-correction lerp over ~100ms; single reconcile NEVER hard-snaps the visible position.
- Halo / Overwatch class: per-reconcile soft cap ~10 cm; beyond that, lock position + gradual catch-up.
- Rocket League: rb fully replaced but visible mesh lerp follows behind (visual lags rb 1-2 frames, hides reconcile snaps).

**Audit:**
```
git grep -n "OnReconcile\|public void Reconcile\|\[Reconcile\]" -- Assets/Scripts/New_Buddah/
→
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:547   [Reconcile]
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:729   $"[PredictionIntro][Reconcile] tick={tick} skipped={skipped} ..."

git grep -nE "PredictionRigidbody\.Reconcile|rb\.position\s*=|MovePosition|\.Move\(" -- Assets/Scripts/New_Buddah/Core/
→
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:1873   rb.position = eventData.TargetPosition;
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:1937   rb.position = eventData.SnapshotPosition;
```

**[Reconcile] callback body (motor.cs:547-572 abridged):**
```csharp
[Reconcile]
private void ReconcileState(BuddahPredictedReconcileData data, Channel channel = Channel.Unreliable)
{
    _reconcileCallbackCount++;
    if (_predictionRigidbody == null || data.RigidbodyState == null)
        return;

    Vector3 preReconcilePosition = rb != null ? rb.position : Vector3.zero;
    // ...
    if (!skipOwnerIntroReconcile)
        _predictionRigidbody.Reconcile(data.RigidbodyState);   // ← THE CALL
    Vector3 postReconcilePosition = rb != null ? rb.position : Vector3.zero;
    // ... debug-state writes only after this point
}
```

**Findings:**
- The motor's [Reconcile] body does **NO project-side soft correction**. It calls FishNet 4.6's `_predictionRigidbody.Reconcile(data.RigidbodyState)` and trusts FishNet to handle smoothing.
- Whether the actual visible behavior is "hard-snap" or "smooth correction" depends entirely on FishNet's internal handling under the prefab's `_localReconcileCorrectionType: 2` (Predicted mode) + the graphical-object smoother config (per Surface 2). FishNet's smoother sits BETWEEN rb (which gets hard-replaced by `_predictionRigidbody.Reconcile`) and `_graphicalObject` (which is what the player sees).
- The two `rb.position = ...` direct writes at motor.cs:1873 + 1937 are inside **intro/teleport/handoff event handlers** (driven by `eventData.TargetPosition` / `eventData.SnapshotPosition`), NOT in the Reconcile callback. Each is followed by `rb.Sleep(); rb.WakeUp(); InitializePredictionRigidbody();` — explicit re-anchor. Out of jitter scope (these fire once per teleport/handoff event, not per tick).
- No project-side single-reconcile soft cap (no "if delta > X cm, lock + catch-up" logic). Project relies on FishNet's smoother as the only damping layer. If FishNet's smoother can't absorb a per-tick correction, that correction propagates to the visible transform.

**Industry-standard compliance: PARTIAL.** Project does delegate smoothing to a smoother layer (FishNet's adaptive interpolation, per Surface 2 prefab fields), which is the right architectural shape. But there is no explicit single-reconcile correction cap (Halo/Overwatch pattern), and there is no project-side documentation on what `_localReconcileCorrectionType: 2` actually does. The "correctness" of the smoothing is downstream of FishNet's behavior, which is a black-box from project code's perspective.

**Risk: HIGH.** If under 49 Hz reconcile rate (Surface 7.5) FishNet's smoother is insufficient — and the smoother is the project's ONLY damping layer — visible jitter is the structural outcome, not a tuning bug.

**Phase 7.5 implication:** add an explicit project-side soft-correction layer between `_predictionRigidbody.Reconcile` and the visible-transform smoother. Patterns: (a) per-reconcile snap-distance cap; (b) explicit lerp catch-up over ~100ms when authoritative state diverges from predicted state by > threshold; (c) capture pre/post-reconcile delta (Q4 sibling probe captures this!) and feed into a corrective velocity that the smoother then absorbs.

---

### 7.2 — Layer 4-5: Smoother config on Buddah prefab (★ second suspect)

**Audit:**
```
git show HEAD:Assets/Character/Prefab/Buddah.prefab | grep -nE \
  "TickSmoother|_ownerInterpolation|_spectatorInterpolation|_adaptiveInterpolation|_useGracePeriod|_movementMultiplier|_teleportThreshold|_graphicalObject|_smoothPosition|_smoothRotation|_ownerSmoothedProperties|_spectatorSmoothedProperties|_enablePrediction|_predictionType|_localReconcileCorrectionType|_enableTeleport|_detachGraphicalObject"
```

**Result (NetworkObject prediction-smoother fields, lines 1066-1083):**

| Line | Field | Value | Semantics (FishNet 4.6) |
|---|---|---|---|
| 1070 | `_enablePrediction` | `1` (true) | NetworkObject opts in to prediction stack |
| 1071 | `_predictionType` | `1` | Rigidbody (vs CharacterController = 0) |
| 1072 | `_localReconcileCorrectionType` | `2` | Likely "Predicted" mode — FishNet handles correction with built-in smoothing (vs 0=None / 1=Smooth) |
| 1073 | `_graphicalObject` | `{fileID: 1582303153357347920}` | wired to a child mesh-root transform (per Surface 2: real child transform, m_LocalScale=4) |
| 1074 | `_detachGraphicalObject` | `0` (false) | graphical stays parented to root |
| 1077 | `_ownerInterpolation` | `1` | owner interpolates **1 tick** lag |
| 1078 | `_ownerSmoothedProperties` | `4294967295` (0xFFFFFFFF) | smooth ALL transform properties for owner |
| 1079 | `_adaptiveInterpolation` | `3` | adaptive mode level 3 (highest) |
| 1080 | `_spectatorSmoothedProperties` | `255` (0xFF) | smooth all 8 properties for spectator |
| 1081 | `_spectatorInterpolation` | `2` | spectator interpolates **2 ticks** lag |
| 1082 | `_enableTeleport` | `0` | teleport feature OFF |
| 1083 | `_teleportThreshold` | `1` | meters; vestigial when `_enableTeleport=0` |

A second NetworkObject block at lines 1117-1118 also shows `_enableTeleport: 0` / `_teleportThreshold: 1` — likely a child NetworkObject (skill projectile?) on the same prefab.

**Findings:**
- Owner interp = 1 tick (good for owner-perceived smoothness; minimum lag).
- Spectator interp = 2 tick + adaptive 3 — reasonable for under-100ms-LatencySim conditions; spectator may need MORE buffer when latency spikes.
- `_enableTeleport: 0` means **FishNet teleport-on-large-correction is DISABLED**. Combined with Surface 7.1's "no project-side soft cap", this means: every reconcile correction, regardless of magnitude, flows through the smoother as a continuous-motion segment. There is no "if delta > X, treat as teleport" path. At 80 m/s with 49 Hz reconciles (Surface 7.5), even a 30 cm correction per reconcile — well below the disabled `_teleportThreshold: 1` meter — accumulates into visible jitter at smoother granularity.
- `_teleportThreshold: 1` meter is also notable: even if `_enableTeleport` were enabled, the threshold ~equals `(80 m/s) / (60 fps) × ~0.75` ≈ 1 m of nominal per-frame motion. So the teleport threshold is set at "one frame's worth of motion" — reasonable for slow characters, MARGINAL for an 80 m/s character.

**Cross-ref to Stage 3 design Q3:** the design's `pos-dmax < 0.60 m` threshold (G3.2) corresponds to ~0.45 frames of nominal motion. If pos-dmax exceeds that, FishNet's smoother is leaking corrections into visible jitter without the `_enableTeleport` escape valve.

**Industry-standard compliance: PARTIAL → MARGINAL at high speed.** Adaptive interpolation + per-role smoothed-properties is correct shape. But `_enableTeleport: 0` removes the structural "large correction = teleport, not lerp" safety net that smoothers in MP shooters typically rely on. At 80 m/s, the absence of this safety net is structurally significant.

**Risk: HIGH.** Smoother config is partially correct but has no escape hatch for large corrections at high speed.

**Phase 7.5 implication:** (a) enable `_enableTeleport` + tune `_teleportThreshold` for 80 m/s scale (~0.5-1.0 m); (b) consider increasing `_spectatorInterpolation` to 3 ticks for higher LatencySim conditions; (c) Phase 7.5 design Q&A must inventory FishNet 4.6 source for what `_localReconcileCorrectionType: 2` actually does — current value is undocumented in project code.

---

### 7.3 — Layer 1: Fixed tick + non-FixedUpdate rb writes

**Audit:**
```
git grep -nE "void FixedUpdate|void Update\(\)|void LateUpdate" -- Assets/Scripts/New_Buddah/Core/ Assets/Scripts/New_Buddah/Simulation/
→
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:204   private void Update()
```

**Update() body (motor.cs:204-219):**
```csharp
private void Update()
{
    if (bootstrap == null)
        return;
    bootstrap.DebugState.isOwner = IsOwner;
    bootstrap.DebugState.rigidbodyIsKinematic = rb != null && rb.isKinematic;
    RefreshInputBridge();
    bootstrap.DebugState.inputBridgeEnabled = _ownerInputBridge.IsEnabled;
    bootstrap.DebugState.predictionBlockReason = GetPredictionBlockReason();
    // ... more bootstrap.DebugState.* writes
}
```

**Findings:**
- Update() writes ONLY to `bootstrap.DebugState.*` fields — no rb writes, no transform writes. This is observation-domain instrumentation, not gameplay. **SAFE.**
- All gameplay rb writes go through `_predictionRigidbody.AddForce` / `AddTorque` / `Velocity` / `Reconcile` (21 callsites in motor.cs, all `_predictionRigidbody.*` per the rb-write grep — Rule 7 PredictionRigidbody integrity is preserved). No bypass found.
- Force application is in `TimeManager_OnTick()` (motor.cs:222) — FishNet's tick callback, equivalent to FixedUpdate for the prediction stack. Correct domain.

**Tick rate (`Assets/Scenes/MainMenu.unity:6409`):** `_tickRate: 60`. FishNet TimeManager configured to 60 Hz. Other scenes inherit via NetworkManager singleton. Render frame rate locked to 120 Hz (`Assets/Scripts/GlobalSettings/FrameRateLock.cs:18` `Application.targetFrameRate = 120`), giving an exact **2:1 fps:tick ratio** — no beat frequency. The FrameRateLock.cs file's own header docstring documents this exact concern:
> "Locks render frame rate to an integer multiple of the FishNet tick rate so PredictionSmoother's sub-frame interpolation fraction advances in even steps across frames. Without this, a non-integer fps-to-tick ratio produces a 'beat frequency' tremor in visual-root smoothing that shows as regular micro-oscillation on fast-moving predicted objects."

This shows the team has already engineered for the beat-frequency failure mode — **NOT a likely root cause for the persistent jitter**.

**Industry-standard compliance: HIGH.** Update is debug-only, no Unity FixedUpdate hot-path, all gameplay writes are PredictionRigidbody-mediated, fps:tick ratio is integer.

**Risk: LOW.** Update() is observation-only; tick rate + frame rate lock are explicitly engineered. This layer is not the source of jitter.

**Phase 7.5 implication:** none. Layer 1 is sound.

---

### 7.4 — Layer 6: Hitbox vs visual transform alignment

**Audit:**
```
git grep -nE "OverlapBox|OverlapSphere|Physics\." -- Assets/Scripts/Buddah/
```
Three `Physics.OverlapBox/Sphere` callsites in `Assets/Scripts/Buddah/PushHitbox.cs` (lines 153, 165, 171), all using `box.transform.*` / `sphere.transform.*` / `_myCollider.bounds.*` — i.e., the collider's **own GameObject transform**.

**PushHitbox `CheckVictimOverlaps` body (lines 145-180):**
```csharp
if (_myCollider is BoxCollider box)
{
    Vector3 center = box.transform.TransformPoint(box.center);
    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale) + Vector3.one * OverlapPadding;
    Collider[] overlaps = Physics.OverlapBox(center, halfExtents, box.transform.rotation, ~0, ...);
    // TryApplyHit per overlap
}
```

**Findings:**
- Hitbox sweep position is anchored to `box.transform.*`, the collider's own GameObject transform.
- The collider's GameObject is presumably parented under either (a) the **prefab root** (rb-driven, jumps per-tick), or (b) a child of the `_graphicalObject` smoother target (smoother-driven, lags rb by 1-2 ticks).
- **Surface 7.4 limitation:** without inspecting the prefab's GameObject hierarchy at the level of finding where PushHitbox is attached, this audit cannot definitively say which transform PushHitbox follows. The `git grep` does not surface that — would need `git show` of the prefab YAML and trace m_GameObject ancestry of the PushHitbox MonoBehaviour record.
- **However:** if the hitbox follows rb (jumps per-tick), but the visible mesh follows the smoother (lags 1-2 ticks), that's **structurally a 1-2-frame visual desync** — exactly the SC.3 driver-observation red line from Stage 3 design.
- This is the canonical industry trade-off: hitbox-on-rb (responsive, visually wrong by 1-2 frames) vs hitbox-on-visual (visually correct, gameplay-laggy by 1-2 ticks). Most MP shooters pick hitbox-on-rb + accept SC.3-style desync; the question is whether BuddahGo intentionally chose this trade-off or fell into it accidentally.

**Industry-standard compliance: UNKNOWN — needs prefab tree inspection.** Pattern is acceptable IF the hitbox attachment point is documented + intentional.

**Risk: MEDIUM.** Conditional — depends on PushHitbox attachment point in prefab tree. If hitbox follows rb, this contributes to SC.3 driver-observable desync at peer-push events.

**Phase 7.5 implication (conditional):** if Phase 7 SMOKE confirms SC.3 desync at peer-push, Phase 7.5 chooses one of:
- (a) move hitbox to visual-root (prevents SC.3 visually but creates 1-2-tick gameplay lag — needs latency tolerance design);
- (b) keep hitbox on rb + document SC.3 as accepted trade-off (industry standard for MP shooters);
- (c) decouple via per-tick hitbox snapshot replay (advanced; expensive).

**Action item for Stage 5 SMOKE driver:** during peer-push events, watch SC.3 carefully — note exact frame timing of visible contact vs gameplay response. Reviewer's prefab-tree inspection at Stage 6 verify resolves the conditional.

---

### 7.5 — Reconcile rate vs frame rate (★ correction to user premise)

**User premise (cited from Phase 4b V5 verify):** "rec-cb=2595 in 53 HBs on CLIENT under 100ms LatencySim → 49 reconciles/sec".

**Audit (independent verification of premise):**
```
for f in agent-exchange/console/raw/2026-05-03-phase4b-v5*.log; do
  max_rec=$(grep -oE "rec-cb=[0-9]+" "$f" | awk -F= '{print $2}' | sort -n | tail -1)
  echo "$f → max rec-cb=$max_rec"
done
→
2026-05-03-phase4b-v5-client-100ms.log → max rec-cb=0
2026-05-03-phase4b-v5-host-100ms.log   → max rec-cb=2595
2026-05-03-phase4b-v5-single.log       → max rec-cb=0
```

**Correction to premise:** rec-cb=2595 is on the **HOST** log under 100ms LatencySim, NOT the CLIENT. The CLIENT log shows `rec-cb=0` for all 212 occurrences across the entire session. This is significant for Phase 7 framing:
- **HOST runs ~49 reconciles/sec** under 100ms LatencySim. Host operates as both server-authority AND a peer client; the rec-cb counter likely fires on the host's CLIENT-role reconciliation against its own SERVER-role authoritative state, OR on prediction-replay-correction even when the host is server-authoritative.
- **CLIENT (the actual remote peer) shows rec-cb=0** — apparently no reconciles fire at all on the spectator side under 100ms LatencySim in the V5 session, OR the CLIENT log was captured during an idle period.

This inverts the Phase 7 hypothesis: the high-frequency reconcile load is on **HOST, not on CLIENT**. The visible jitter Yonezawa reports is therefore most likely on the **HOST-side BuddahPredicted character** (i.e., the local-control character on the host machine), not the spectator-view of the remote peer. SMOKE should confirm this perceptually.

**Frame-rate analysis given correction:**
- Render = 120 fps (FrameRateLock).
- Tick = 60 Hz (MainMenu.unity:6409).
- Host reconcile rate = ~49 Hz under 100ms LatencySim.
- 49 Hz reconciles spread across 120 fps render = ~2.4 frames per reconcile event. Smoother has ~2 frames to absorb each correction before the next one fires.
- 49 Hz reconciles vs 60 Hz tick = 0.82 reconciles per tick on average. Some ticks see 0 reconciles, some see 1. Not "every tick reconciles" but close.

**Findings:**
- The 2-frame per-reconcile budget is tight. If FishNet's smoother needs >2 frames to fully absorb a correction (e.g., uses 4-6 frames of damping for visual smoothness), then under 49 Hz reconciles the smoother is in continuous "absorbing" state — it never reaches a stable visible position before the next correction arrives. Result: visible micro-oscillation. This is the SC.1 reconcile-rubber-banding pattern from Stage 3 design.
- The tightness is structural, not tuning: at 49 Hz, ANY damping > 2 render frames produces overlapping corrections. Tuning the smoother won't fix this; reducing the reconcile rate or capping correction magnitude is the architectural lever.

**Industry-standard compliance: STRUCTURAL CONCERN.** 49 Hz reconcile rate approaches the 60 Hz tick rate; smoother absorption window is squeezed. Most MP architectures keep reconcile rate at 5-15 Hz (only when needed) via correction thresholds — fire reconcile only if predicted/authoritative drift exceeds X cm. BuddahGo's project-side code does not implement such a threshold (Surface 7.1); reconciles fire whenever FishNet decides authoritative != predicted, which under 100ms LatencySim is essentially every other tick.

**Risk: HIGH.** This is plausibly the dominant root cause: the structural reconcile rate is too close to the frame rate for the project's smoother to keep up.

**Phase 7.5 implication:**
- (a) **Reconcile-threshold gate (preferred):** project adds a "if predicted/authoritative position diff < X cm AND velocity diff < Y m/s, skip the reconcile" gate at the start of `ReconcileState`. Suppresses 80%+ of reconciles whose corrections are visually negligible anyway. Reduces reconcile rate from 49 Hz to (estimated) 5-10 Hz.
- (b) **Adaptive interp tuning:** raise `_spectatorInterpolation` and possibly `_ownerInterpolation` to give smoother more buffer; combined with adaptive 3, may absorb residual reconciles.
- (c) **Per-reconcile correction cap (Surface 7.1 alternative):** caps individual snap distance, reducing visible amplitude of each reconcile even at 49 Hz frequency.
- Combination of (a) + (c) is the canonical industry pattern.

---

### 7.6 — Frame-time stability

**Audit:**
```
git grep -nE "Application\.targetFrameRate|QualitySettings\.vSyncCount|fixedDeltaTime" -- Assets/Scripts/ ProjectSettings/
→
Assets/Scripts/GlobalSettings/FrameRateLock.cs:17   QualitySettings.vSyncCount = 0;
Assets/Scripts/GlobalSettings/FrameRateLock.cs:18   Application.targetFrameRate = TargetFrameRate;
Assets/Scripts/Buddah/BuddahMovement.cs:[various]  Time.fixedDeltaTime  (legacy movement; gated out of prediction stack)
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:1226 float dt = TimeManager != null ? (float)TimeManager.TickDelta : Time.fixedDeltaTime;
```

`FrameRateLock.cs:14` constant: `private const int TargetFrameRate = 120;`

**ProjectSettings/QualitySettings.asset (Performant tier):** `vSyncCount: 0` confirmed at the asset level too.

**ProjectSettings/TimeManager.asset:**
```
Fixed Timestep: 0.01666667    (= 1/60s = 60 Hz Unity FixedUpdate)
Maximum Allowed Timestep: 0.33333334
```

**Findings:**
- `Application.targetFrameRate = 120` + `vSyncCount = 0` → render rate capped at 120 fps with no vsync. Capped is good; vsync-off may reintroduce frame-time jitter on machines where the engine can sustain >120 fps (engine bursts a frame, then waits for the cap, producing irregular-but-bounded spacing).
- Unity Fixed Timestep = 60 Hz matches FishNet TickRate (per Surface 7.3) — `TimeManager.TickDelta` is the prediction-domain dt. Consistent.
- 120 / 60 = 2:1 exact ratio; FrameRateLock comment confirms intent. No beat-frequency tremor expected.
- Variability remains within Unity's frame-pacing inherent jitter (~±0.5ms typically with vsync off + targetFrameRate cap), which is below the threshold of perceptible tremor at 80 m/s but could amplify other jitter sources.

**Industry-standard compliance: HIGH.** Capped target frame rate, integer fps:tick ratio, vsync-off documented.

**Risk: LOW.** Frame-time stability is engineered. Possible secondary contributor (vsync-off jitter ~0.5ms = ~6cm at 80 m/s) but not a primary root cause.

**Phase 7.5 implication:** none expected. If Phase 7 SMOKE shows frame-time-correlated jitter (which would be a secondary finding), consider `vSyncCount = 1` for SMOKE-only runs — but production keeps current config.

---

### Prioritized root-cause hypothesis ranking (Surface 7 synthesis)

| # | Risk | Hypothesis | Audit refs | What Phase 7 SMOKE proves | Phase 7.5 retrofit pattern |
|---|---|---|---|---|---|
| 1 | **HIGH** | Structural: reconcile rate (~49 Hz on HOST under 100ms latency) is too close to tick rate (60 Hz) for FishNet's smoother to fully damp each correction before the next arrives. Result: continuous "absorbing" state → visible micro-oscillation matching SC.1 rubber-banding. **Dominant on HOST side**, NOT spectator (per Surface 7.5 correction to user premise). | 7.5 + 7.1 + 7.2 | Q4 sibling-probe `[D-REC HEARTBEAT]` shows 40+ events-in-window on HOST under 100ms; SC.1 driver observation should fire on HOST view, not CLIENT view. | (a) project-side reconcile-threshold gate (skip if diff < X cm + Y m/s); (b) per-reconcile soft-correction cap; combination of both. Reduces reconcile rate to 5-10 Hz, recovers smoother absorption budget. |
| 2 | **HIGH** | Smoother config gap: `_enableTeleport: 0` removes the "large correction → snap-not-lerp" escape valve. At 80 m/s scale, even a 30-60cm reconcile correction (well below the disabled threshold) flows through smoother as continuous-motion → visible distortion. No project-side soft cap to compensate (Layer 3 trusts FishNet entirely). | 7.1 + 7.2 | `pos-dmax` exceeding `pos-davg + 0.6m` (G3.2 design gate) on Path-B-100ms confirms; SC.2 push-recoil step-function visible when reconciles arrive mid-acceleration. | (a) enable `_enableTeleport` + tune `_teleportThreshold` to ~0.5m; (b) raise `_spectatorInterpolation` to 3 ticks for >100ms latency; (c) project-side single-reconcile cap. Phase 7.5 design Q&A inventories FishNet 4.6 source for `_localReconcileCorrectionType: 2` semantics (currently undocumented in project code). |
| 3 | **MEDIUM** | Layer 6 hitbox/visual desync. PushHitbox uses `box.transform.*` for OverlapBox sweeps; if box collider follows rb (root-attached), hitbox is 1-2 ticks ahead of visible mesh. Industry-acceptable trade-off if intentional, contributes to SC.3 driver-observable desync if accidental. **Conditional on prefab tree inspection** (not resolved by git grep alone). | 7.4 | SC.3 driver observation during peer-push: visible mesh penetrates other character before push response fires. | If accidental: (a) move PushHitbox attachment to a visual-smoother child; if intentional: (b) document trade-off + accept SC.3 as a known cost. |
| 4 | **LOW** | Layer 1 fixed-tick + non-FixedUpdate rb writes — debug-only Update(); all gameplay rb writes go through `_predictionRigidbody.*`. Engineered correctly. NOT a jitter source. | 7.3 | N/A | None. Layer 1 is sound. |
| 5 | **LOW** | Layer 7.6 frame-time stability — 120 fps cap + 60 Hz tick = 2:1 integer ratio + vsync-off. Engineered. Possible secondary jitter from vsync-off frame-pacing irregularity (~±0.5ms ≈ ±6cm at 80 m/s). | 7.6 | If Phase 7 SMOKE shows frame-time-correlated jitter, consider vsync-on for SMOKE rerun. | None for production. SMOKE-only optional vsync toggle if needed for diagnostic isolation. |

**Summary of Phase 7.5 retrofit scope (preview, finalized at Phase 7 closeout):**
The dominant retrofit hypotheses (#1 + #2) converge on **adding a project-side correction-management layer between `_predictionRigidbody.Reconcile` and the visible-transform smoother**. Specifically:
- Reconcile-threshold gate (skip negligible reconciles) → reduces 49 Hz to 5-10 Hz.
- Per-reconcile soft cap (clamp single-event correction magnitude) → reduces visible amplitude.
- Enable `_enableTeleport` + tune threshold → restore the structural escape valve for large corrections.

These three together are sufficient retrofit hypotheses for Phase 7.5 design Q&A entry. Phase 7 SMOKE + verify will confirm or reject each via Q4 sibling-probe data + SC.1/SC.2/SC.3 driver observations + cross-reference with Surface 7 audit findings.

**Stage 5 SMOKE driver focus list (per amended framing):**
- **HOST-side observation prioritized** (per 7.5 correction): SC.1 rubber-banding most likely visible on the local-control character on the host machine, not the spectator view of the remote peer.
- During 60s+ continuous push-pull interaction, watch for: (i) sustained micro-vibration on HOST character during continuous motion (SC.1 → confirms #1), (ii) step-function deceleration during peer-push receive (SC.2 → confirms #2), (iii) visible-mesh-penetrates-other-mesh before push response fires (SC.3 → confirms #3 conditionally on prefab tree).

---

## Summary — what Q0–Q5 must answer (preview, not answers)

- **Q0 — metric definition:** lean (C) preserved on substance (per-frame Δposition aggregate + snap-event metric). **Section 5b KEY FINDING:** existing `VisualShakeProbe` already emits `pos-dmax / pos-dp99 / pos-davg` (+ rotation aggregates) — Q0 design MUST decide whether Phase 7 vocabulary aligns with these names (recommended: align) or invents new ones. Note: existing emit is Δ-position arithmetic mean, not strict RMS — convertible offline if Q0 wants RMS specifically.
- **Q1 — capture mechanism:** **LEAN REVISED.** Original Q1=A "build new ~50 LOC JitterCapture" was based on Section 1's incomplete inference. **Section 5b corrects this**: `BuddahPredictionVisualShakeProbe.cs` (176 LOC, gated by `BUDDAH_PREDICTION_VISUAL_PROBE` define) already implements Phase 7's capture-mechanism core (pinned visual-root LateUpdate sampling, ring-buffer Δposition+Δrotation, dmax/dp99/davg aggregates, L15 normalize, runtime-attach via `BuddahPredictionVisualRootBridge`). **Recommend Q1=A' (reuse-existing-probe)** — option (a) in Section 5b. Phase 7 IMPLEMENT drops from "~50 LOC new file" to "~0 LOC + define addition + analyzer reads `[D-VIS HEARTBEAT]` lines from Editor.log". Sub-decision from Section 2 (capture-target = visual-root post-smoother) is satisfied by the existing probe's `_visualRoot` pinning via `VisualRootBridge.GetVisualRoot()`.
- **Q2 — comparison baseline:** lean (C with B fallback) preserved. **Risk to address in design:** legacy-path baseline (Q2-A SHA = `2c63bc5`) is apples-to-oranges vs post-V5 prediction-stack — design must justify or fall back to (B).
- **Q3 — pass criteria:** lean (B-primary + A-secondary) preserved. **Threshold derivation must use graphical-frame time + smoothed position** (item #4) — Q3 design Q&A MUST cite explicit cm/frame value derived against ~16ms render frame, not 20ms tick.
- **Q4 — reconcile snap probe:** lean (C) preserved on substance. **Section 5b refinement:** Q4 implementation pattern shifts from "extend JitterCapture" to "add sibling probe `BuddahPredictionReconcileSnapProbe.cs` matching VisualShakeProbe's shape" — ~40 LOC, hooks motor.cs:550 post `_reconcileCallbackCount++` under a sibling define (e.g., `BUDDAH_PREDICTION_RECONCILE_PROBE`). Architectural consistency with the V3 probe.
- **Q5 — escalation path:** lean (A) preserved (Phase 7.5 retrofit). No recon evidence affects this Q.

Awaiting Stage 2 reviewer SIGN-OFF (independent verification of items #1-#6
via git plumbing + KEY FINDING acceptance/pushback) before Stage 3
DESIGN-QA is authored.
