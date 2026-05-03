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
