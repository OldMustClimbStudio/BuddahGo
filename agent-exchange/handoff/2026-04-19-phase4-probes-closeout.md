# Phase 4-probes Close-Out Packet — for Claude Code

Date: 2026-04-19
Branch: refactor/prediction-v2 (Phase 3d merged tip + probes PR head)
Status: V1 scaffolding complete. Compile-clean expected (pending editor
import verification). No real-path behavior change. Baseline capture,
RaceMap.unity disposition, and Phase 4a V1 implementation are all still
pending — this PR only ships observational infrastructure.

---

## §1 — Final Commit Message

Paste verbatim into commit (`git commit -F agent-exchange/handoff/2026-04-19-phase4-probes-commit-msg.txt`
or HEREDOC). Long on purpose — captures the instrumentation contract,
the three-island motor edit rationale, the scripting-define policy, and
the next-step dependencies for 4a V1 + 4b V1.

```
feat(prediction): phase 4 observational probes (V3 visual shake + V13 perf)

Lands two observation-only probe components + a gated ProfilerMarker on
BuddahPredictedMotor.RunInputs. All scope behind new scripting defines
BUDDAH_PREDICTION_VISUAL_PROBE + BUDDAH_PREDICTION_PERF_PROBE. When the
defines are undefined, every byte of real-path code compiles out ->
release build is byte-identical to pre-probes tip (reviewer G1 bind).

Purpose: allow objective baseline capture on 3d-tip + post-4a comparison
for the V3 (gameplay shake) and V13 (performance budget) gates per
Docs/prediction-refactor-plan/13-validation-gates.md. No Phase 4a V1
implementation begins until baselines are captured and reviewer issues
the implementation green-light.

=== What this lands ===

- Assets/Scripts/New_Buddah/Debug/BuddahPredictionVisualShakeProbe.cs (NEW)
  MonoBehaviour, entire class body gated `#if BUDDAH_PREDICTION_VISUAL_PROBE`.
  Samples the pinned [SerializeField] Transform _visualRoot world-space
  position + rotation every LateUpdate. Maintains pre-allocated float
  ring buffers sized at _heartbeatFrames (default 60 = 1 sec @ 60fps).
  Per heartbeat: Array.Copy into pre-allocated sort buffer, Array.Sort,
  compute dmax / dp99 / davg for pos (meters) + rot (degrees). Emits
  `[D-VIS HEARTBEAT] frame=N owner=<bool> window=M pos-dmax/p99/avg
  rot-dmax/p99/avg`. L15 zero-quaternion normalize applied before
  Quaternion.Angle to defuse default(Quaternion)=180° foot-gun.
  Ownership tag via [SerializeField] NetworkObject _networkObject
  (explicit inspector pin, no GetComponentInParent scanning per
  reviewer V3 bind).

- Assets/Scripts/New_Buddah/Debug/BuddahPredictionPerfProbe.cs (NEW)
  MonoBehaviour, entire class body gated `#if BUDDAH_PREDICTION_PERF_PROBE`.
  Starts two ProfilerRecorders on OnEnable: (1) ProfilerCategory.Scripts
  + "BuddahPredictedMotor.RunInputs" marker name; (2) ProfilerCategory.
  Memory + "GC.Alloc" marker name. Both use ProfilerRecorderOptions.
  SumAllSamplesInFrame so the per-frame value aggregates every Begin/
  End within a profiler frame (reconcile replay re-runs aggregate into
  one number). Update samples recorder.LastValue each frame into
  pre-allocated long ring buffers. Per heartbeat: Array.Copy + Sort,
  compute avg / p99 / max. Emits `[D-PERF HEARTBEAT] frame=N window=M
  rep-avg-ms/p99-ms/max-ms gc-alloc-avg-b/p99-b/max-b`.
  OnDisable disposes both recorders.

- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs (3 islands,
  all `#if BUDDAH_PREDICTION_PERF_PROBE` gated)
    * Line 15-17: `using Unity.Profiling;` inside probe-define gate
      (after the existing #if BUDDAH_PREDICTION_SHADOW + its using).
    * Lines 69-75: `static readonly ProfilerMarker s_runInputsMarker =
      new ProfilerMarker("BuddahPredictedMotor.RunInputs");` field
      declaration with an inline comment documenting name-pin
      discipline with BuddahPredictionPerfProbe.MotorReplicateMarkerName.
    * Lines 329-331: `using var markerScope = s_runInputsMarker.Auto();`
      as the first statement inside RunInputs body, BEFORE the
      `if (!ShouldRunPrediction() || _predictionRigidbody == null)
      return;` gate. Wraps the full method body (including the early
      return path) in a Begin/End marker scope.
  All three islands collapse to zero bytecode when BUDDAH_PREDICTION_PERF_PROBE
  is undefined. Motor.cs byte-diff when define OFF: zero.

- agent-exchange/handoff/phase-8-cleanup-queue.md — Entry 4 appended.
  Contingency: probes may remain indefinitely if useful for regression
  monitoring. Do NOT delete as part of a bundled cleanup sweep.

=== What this does NOT land ===

- No scripting define enabled in ProjectSettings/ProjectSettings.asset.
  Defines are documented in audit Addendum B §B.4 for local enablement
  during baseline capture + 4a/4b runs. Main Player Settings Standalone
  target stays at the pre-probe symbol list (FISHNET;FISHNET_V4;...;
  BUDDAH_PREDICTION_SHADOW).
- No V6LatencyProfile ScriptableObject class or asset. Reserved for
  4b V1 prep per audit Addendum B §B.2.2.
- No baseline log digests. Captures happen post-merge, pre-4a-V1, on
  the probes-PR-merge-commit tip per §3 Next Steps below.
- No scene component placement. Probes must be added to the Buddah
  prefab AND a bootstrap GameObject MANUALLY during baseline capture;
  the scene state committed in this PR is unchanged. Prevents accidental
  commit of probe component references to main scenes.
- No lessons-log L-entry. Per reviewer directive: probes introduce no
  new failure mode; skip L-entry.

=== Validation ===

V1 compile (defines OFF, default Standalone config):
  EXPECTED PASS — all new code is gated, motor edits are gated, ProjectSettings
  is unchanged. Post-import assets-refresh should report zero errors and zero
  new warnings. Reviewer verifies via Unity Editor import + console-get-logs.

V1 compile (defines ON, local Standalone override):
  EXPECTED PASS — probe classes compile, using directives resolve
  (Unity.Profiling is a first-party 2022 LTS namespace, no package
  dependency beyond the com.unity.modules.profiler module which ships
  by default). Motor.cs compiles with `using var markerScope = s_runInputsMarker.Auto();`
  as a valid C# 8+ expression under Unity 2022's Mono backend.

Runtime smoke (defines ON, single-peer Editor):
  EXPECTED — placing BuddahPredictionVisualShakeProbe on the Buddah
  prefab root with _visualRoot pinned to the visual child Transform
  emits `[D-VIS HEARTBEAT]` lines every ~60 frames during gameplay.
  Placing BuddahPredictionPerfProbe on a bootstrap GameObject emits
  `[D-PERF HEARTBEAT]` every ~60 frames with non-zero rep-avg-ms once
  physics ticks have run. BOTH probes are SET-UP on first baseline run,
  not bundled with the probes PR itself.

=== Reviewer G1 re-assertion ===

When BUDDAH_PREDICTION_VISUAL_PROBE and BUDDAH_PREDICTION_PERF_PROBE
are undefined (the default):
  - BuddahPredictionVisualShakeProbe.cs -> empty file (entire body gated
    top-level #if). Zero classes defined. Zero runtime behavior.
  - BuddahPredictionPerfProbe.cs -> same shape. Zero runtime behavior.
  - BuddahPredictedMotor.cs -> identical bytecode to pre-probe commit
    (the three motor edit islands compile out entirely). No real path
    branch, timing, allocation, register usage, inline decision change.
  - Scripting define list in ProjectSettings.asset -> unchanged.

=== Protected-file disclosure ===

BuddahPredictedMotor.cs modifications — all 3 islands inside
`#if BUDDAH_PREDICTION_PERF_PROBE`:
  - Lines 15-17: using-directive island.
  - Lines 69-75: ProfilerMarker static field island (with 3-line
    comment documenting the name-pin discipline + G1 compile-out
    guarantee).
  - Lines 329-331: `using var markerScope = s_runInputsMarker.Auto();`
    call site inside [Replicate] RunInputs entry, BEFORE the
    ShouldRunPrediction gate so the full method body falls under the
    profiler scope.

All additions respect the existing `#if` discipline and preserve the
append-only / observation-only mode. No motor authority-path change.
No Phase 4a Z2 cut-over is started — this PR is strictly probe
infrastructure.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## §2 — Pre-PR Checklist

- [x] `BuddahPredictionVisualShakeProbe.cs` created; entire body gated
      `#if BUDDAH_PREDICTION_VISUAL_PROBE`.
- [x] `BuddahPredictionPerfProbe.cs` created; entire body gated
      `#if BUDDAH_PREDICTION_PERF_PROBE`.
- [x] Motor.cs three-island edit applied (using directive, field,
      Auto-scope).
- [x] Marker name string pinned (`"BuddahPredictedMotor.RunInputs"` is
      referenced identically in motor field declaration and the probe's
      public const `MotorReplicateMarkerName`).
- [x] Ring + sort buffers pre-allocated at Awake; zero per-frame alloc
      (reviewer G1 bind — `Array.Copy` + `Array.Sort` in place, no LINQ,
      no List<T>).
- [x] V3 probe uses `[SerializeField]` pinned Transform + NetworkObject;
      zero `GetComponentInParent<>()` / `GameObject.Find*()` calls
      (reviewer V3 bind).
- [x] L15 zero-quaternion normalize applied on V3 probe's
      `Quaternion.Angle` compare (defensive; `default(Quaternion)`
      visual-root pose during very-early-frame would otherwise report
      180° false positive).
- [x] Entry 4 appended to `agent-exchange/handoff/phase-8-cleanup-queue.md`
      with verbatim contingency note.
- [x] Scripting define list in `ProjectSettings/ProjectSettings.asset`
      UNCHANGED (defines enabled locally, not in tracked config).
- [x] No new L-entry in `Docs/lessons-log.md` (reviewer directive:
      probes introduce no new failure mode).
- [x] No motor authority-path edit outside the 3 gated islands.
- [x] No adapter / Phase 4a Z2 / Phase 4b code touched.
- [ ] Unity Editor import + compile verification (defines OFF) — user
      to verify after pulling the branch.
- [ ] Unity Editor import + compile verification (defines ON, local
      override) — user to verify post-probe-placement during baseline
      capture.
- [ ] PR opened — to be done as part of this closeout action.
- [ ] Reviewer green-light → merge → advance to baseline capture.

---

## §3 — Next-step starter: baseline capture + 4a V1 entry

Phase 4-probes is observational scaffolding. Phase 4a V1 is **blocked on
three sequential prerequisites** before any real-path .cs edit:

### 3.1 — Probes PR merge

After reviewer green-light on this closeout:
1. `git add` the four files listed in §1 plus this closeout packet.
2. Commit with the §1 message (HEREDOC).
3. Push + open PR titled `feat(prediction): phase 4 observational probes (V3 visual shake + V13 perf)`.
4. Wait for reviewer merge.

### 3.2 — Baseline capture (post-merge, pre-4a V1) — Option B, 2-peer 2-Buddah

On the probes-PR-merge-commit tip:

1. On BOTH peers, enable both defines locally in
   `ProjectSettings/ProjectSettings.asset` Standalone target line
   (do NOT commit the change):
   ```
   Standalone: FISHNET;FISHNET_V4;...;BUDDAH_PREDICTION_SHADOW;BUDDAH_PREDICTION_VISUAL_PROBE;BUDDAH_PREDICTION_PERF_PROBE
   ```
2. Place `BuddahPredictionVisualShakeProbe` on the Buddah prefab root
   (per-Buddah placement is correct for V3 — visual transform is
   per-instance). Pin `_visualRoot` to the visual-layer Transform
   (inspect the prefab to identify the exact child the
   `BuddahPredictionVisualRootBridge.visualRoot` field points to).
   Pin `_networkObject` to the Buddah's NetworkObject.
3. Place `BuddahPredictionPerfProbe` **as a scene-singleton** on a
   single long-lived scene root (e.g., the bootstrap or network
   manager GameObject). The probe's Awake guard will emit
   `[D-PERF] Multiple BuddahPredictionPerfProbe instances found`
   and self-disable if placed more than once — do not work around
   that guard; it exists because `ProfilerRecorder` with
   `SumAllSamplesInFrame` returns the global sum, and duplicate
   instances only produce duplicate log lines.
4. Playtest shape: **2-peer, 2-Buddah** (HOST Build + CLIENT Editor,
   Phase 3d V5 shape). Duration: **90s** — raised from 60s per
   reviewer Option B spec; V13 singleton needs more frames for
   `SumAllSamplesInFrame` convergence, V3 dp99 / p99 also benefits
   from the longer window.
5. Session content: normal locomotion across full motion envelope —
   accel / cruise / decel / steering / decay (release-steering-
   while-spinning to hit the Z2-retained motor decay branch). **No
   deliberate skill firing** — baseline measures the Phase 3d tip
   locomotion + rendering hot path, not skill-induced variability.
6. Save logs (4 files, one per probe × peer):
   - `agent-exchange/console/2026-04-19-phase4-baseline-v3-host.log`
     (HOST Player.log `[D-VIS HEARTBEAT]` digest + table).
   - `agent-exchange/console/2026-04-19-phase4-baseline-v3-client.log`
     (CLIENT Editor.log `[D-VIS HEARTBEAT]` digest + table).
   - `agent-exchange/console/2026-04-19-phase4-baseline-v13-host.log`
     (HOST Player.log `[D-PERF HEARTBEAT]` digest + table).
   - `agent-exchange/console/2026-04-19-phase4-baseline-v13-client.log`
     (CLIENT Editor.log `[D-PERF HEARTBEAT]` digest + table).
7. If variance between consecutive 30s windows (three windows per
   90s session) exceeds 20%, extend the capture window to 120s or
   150s (reviewer G2 bind).
8. Revert the local ProjectSettings change on BOTH peers so main
   branch stays clean.

### 3.3 — RaceMap.unity disposition (Yonezawa)

Per audit Addendum C §C.6: open the scene in Unity, identify the
GameObject at `fileID: 4269900605184349933` (under the imported model
`Assets/Sources/地图/地图场景_3.9.fbx`, guid `eed43fce88059ab489967ad03a04edf6`).
Decide R1 (commit with a meaningful message naming the GameObject) or
R2 (revert). Either disposition before Phase 4a V1 branches off the
probes-PR-merge tip.

### 3.4 — 4a V1 green-light (reviewer)

With §3.1 merged, §3.2 logs captured, §3.3 resolved, the reviewer
issues the final 4a V1 implementation green-light. Phase 4a V1 then
starts on the Z2 scope boundary (audit §A.10) with baseline digests
pinned for post-4a V3 / V13 comparison.

---

## §4 — Carry-over reminders

Do not let these fall off the map between phases.

### Phase 4a (Z2 locomotion cut-over)
- **Blocked on §3.1 probes PR merge + §3.2 baseline capture + §3.3
  RaceMap disposition.** Implementation plan binds to audit §A.10
  Z2 scope boundary: migrate `CommandedForwardForce` computation + active-
  steering `CommandedTurnTorque` computation to step; retain motor's
  `AddForce`/`AddTorque` apply sites + entire decay branch inline.
  Shadow compare unchanged.

### Phase 4b (impulse + adapter + L7 + CombatRouting deletion)
- **Blocked on Phase 4a merge + L7 adapter Initialize() design pass.**
  See `Docs/prediction-refactor-plan/phase-4-prerequisites.md` Prereq-1
  Option B. V6 gate requires `V6LatencyProfile` wrapper class + asset
  spec per audit Addendum B §B.2.2; not yet implemented.

### Phase 6 (teleport + handoff cut-over)
- **Blocked on L12** for BOTH categories per
  `Docs/prediction-refactor-plan/phase-6-prerequisites.md`. Phase 3d V5
  bonus-coverage observation (CLIENT hof-compared=1 unexpectedly)
  flagged for Phase 6 planner — confirm L12 reproducibility before
  sign-off.

### Phase 8 (cleanup)
- Entry 1 — Reconcile serializer precision audit for ModifierState vs
  ComputedStats (unchanged from 3c).
- Entry 2 — Phase 3b teleport rotation compare normalization (L15
  defensive fix, deferred).
- **Entry 3 (NEW, per Phase 4 audit Addendum A Z2 selection)**:
  Migrate locomotion decay branch from motor to step — contingent on
  Phase 3a scope extension (do NOT delete trivially).
- **Entry 4 (NEW, per this PR)**: Remove V3 + V13 probes — contingent
  on all phases merged + no regression-monitoring need.

### Architecture observation (non-blocking)
- `[D-VIS HEARTBEAT]` + `[D-PERF HEARTBEAT]` join the `[D-LOC]` family
  of digest tags. Digest tooling that greps `^\[D-.+ HEARTBEAT\]` picks
  them up without per-tag changes.
- Motor's `ProfilerMarker` can be reused by Phase 5/6/7 perf probes
  if needed — marker is scoped at the full RunInputs body, which is
  the common measurement surface for every phase cut-over.

### Clarification 1 (V13 singleton placement) — confirmed

V13 probe is **scene-singleton by design**. No `NetworkObject`
reference was present in the probe prior to reviewer's clarification
(perf measurement is global, not per-instance). Reviewer-requested
`Awake` duplicate guard applied — `FindObjectsOfType<...>().Length > 1`
triggers an error log (`[D-PERF] Multiple BuddahPredictionPerfProbe
instances found ...`) and self-disables the secondary instance.
Defensive null-buffer guards added in `OnEnable` + `Update` so the
disabled-duplicate path does not crash. V3 probe remains per-Buddah
(correct: visual transforms are per-instance). Placement guidance
updated in §3.2.

### Clarification 2 (8-Buddah baseline feasibility) — SCOPE IMPACT

**Finding**: no single-peer N-Buddah test-spawn path exists in the
repository.

Evidence (greps across `Assets/Scripts/`):
- `DebugSpawn` / `TestSpawn` / `BotSpawn` / `DummyBuddah` /
  `AIBuddah` / `NpcBuddah` / `SpawnCount` / `FakePlayer` /
  `HeadlessClient` / `extraBuddah` / `buddahCount` — **zero matches**.
- `Spawn.*Buddah` / `BuddahSpawn` / `SpawnPlayer` / `SpawnCharacter`
  — matches only `Assets/Scripts/Network/Match/MatchSpawnManager.cs`.
- `MatchSpawnManager.cs:103-128`: `TrySpawnPlayer(NetworkConnection
  conn)` spawns **one** `_playerPrefab` per connected
  NetworkConnection. No loop, no count multiplier.
- `MatchSpawnManager.cs:92-99`: `SpawnForAllConnectedPlayers` iterates
  `ServerManager.Clients` — one spawn per client. Single-peer editor
  = 1 client = **1 Buddah maximum**.
- `Assets/Scripts/RaceIntro/` — grep for
  `Bot|AI|Dummy|Npc|TestPlayer|Debug.*Spawn|SpawnCount` returns
  **zero matches**.

**Scope impact**: reviewer's original spec of "8-Buddah 60s capture
on 3d-tip" is not achievable under any current spawn path. Single-
peer editor gives 1 Buddah. 2-peer (HOST Build + CLIENT Editor,
Phase 3d V5 shape) gives 2 Buddahs. Reaching 8 would require a
debug-only multi-spawn mechanism that does NOT exist today and is
out of scope for a probes PR.

**Options (for reviewer re-scope decision)**:
- **Option A — 1-peer 1-Buddah baseline**: minimal load, delta
  reference only. Biased low vs real match. Works today without
  any coordination.
- **Option B — 2-peer 2-Buddah baseline**: Phase 3d V5 shape (HOST
  Build + CLIENT Editor). Minimum multiplayer load. Matches the
  capture shape 4a V5 / 4b V5 will use for post-4a comparison —
  apples-to-apples delta math. Requires peer-2 coordination for
  every baseline + 4a V1 V5 run (already required for future V5s
  anyway).
- **Option C — defer baseline to 4a-V1-merge tip**: capture
  baseline and post-4a on adjacent commits rather than pre-/post-
  refactor tips. Whichever peer count is available at that moment
  becomes the baseline. Loses the "pre-refactor baseline" framing
  but preserves the +10% threshold math (pre-4a commit vs
  post-4a commit delta still valid).
- **Option D — add a debug-only multi-spawn to the probes PR**:
  NOT RECOMMENDED. Scope creep — probes PR is observational only
  and this would introduce authority-path spawn logic that needs
  reviewer audit, protected-file disclosure, and runtime
  validation of its own.

**Reviewer decision (2026-04-19): Option B selected**. 2-peer
2-Buddah baseline (HOST Build + CLIENT Editor, Phase 3d V5 shape).
Baseline duration raised to 90s. See §3.2 above for updated
procedure and log filename list.

Rejection rationale (reviewer, for audit trail):
- **A rejected**: 1-Buddah perf does not exercise concurrent-motor
  load pattern; regression blind spot on multi-Buddah physics
  contention.
- **C rejected**: baseline on 4a-merge-commit loses pre-vs-post
  comparison semantics; defeats V13 purpose of measuring the cut-
  over delta against pre-refactor state.
- **D rejected**: debug multi-spawn is network-authority-path
  scope creep in a probes PR.

**Forward work captured**: Entry 5 appended to
`agent-exchange/handoff/phase-8-cleanup-queue.md` — add a
debug-only multi-spawn mechanism post-Phase-4 so V13 can be
re-validated at 4 / 8 Buddahs, closing the concurrency blind spot
Option B leaves open.

---

## Appendix A — Protected-file disclosure (BuddahPredictedMotor.cs)

All three islands inside `#if BUDDAH_PREDICTION_PERF_PROBE`. Motor's
authority path is byte-identical to pre-probe commit when the define
is undefined.

| Lines     | Scope                                                            |
|-----------|------------------------------------------------------------------|
| 15-17     | `#if BUDDAH_PREDICTION_PERF_PROBE using Unity.Profiling; #endif` |
| 69-75     | `static readonly ProfilerMarker s_runInputsMarker = new ProfilerMarker("BuddahPredictedMotor.RunInputs");` + 4-line comment documenting name-pin discipline and G1 compile-out guarantee |
| 329-331   | `using var markerScope = s_runInputsMarker.Auto();` — first statement inside RunInputs body, wraps the ShouldRunPrediction early-return path |

**Grep verification command**:
```
grep -nE 'BUDDAH_PREDICTION_PERF_PROBE|s_runInputsMarker|Unity\.Profiling' Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
```

Expected output: 5 matches total (2 #if directives, 1 using directive,
1 field declaration, 1 Auto() call site). Any additional matches would
indicate scope creep in this PR and should be audited.

**Byte-diff verification (after Unity import, defines OFF)**:
```
# on probes-PR-merge-commit with defines OFF:
git diff --stat HEAD~1 -- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
# expected: +11 insertions, 0 deletions (3 gated islands with 2 header
# lines each + 1 content line each + 1 trailer)
```

The `+11` includes only the `#if / #endif` directive pairs and the
gated content, NONE of which contributes to bytecode when the define
is undefined. Motor's compiled assembly is unchanged.

---

End of packet. Awaiting Unity editor import verification + reviewer
green-light before PR open.
