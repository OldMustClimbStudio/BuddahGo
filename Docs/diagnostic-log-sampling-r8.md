# Screen diagnostics and R8 handoff — 2026-09-30

Addresses [PR #50 review comment 4146561530](https://github.com/OldMustClimbStudio/BuddahGo/pull/50#discussion_r4146561530).
The user explicitly authorized disabling local screen diagnostics in Editor,
Development and Release, retaining logs and ordinary game HUD/Unity Console.

## Isolation and implementation

Primary worktree: `.worktree/debug-log-sampling-20260930`, branch
`codex/debug-log-sampling-20260930`, baseline `95e57a0c8763bf79867098485215fd422aee0ddb`.
Remote #49/#50/#58 were checked before work and matched the supplied SHAs.
The original dev worktree's PackageManagerSettings change and all other WIP were untouched.
No push, PR comment, Editor launch, player launch or other-task process termination was performed.
The fresh checkout initially stalled; only its verified git processes were stopped and
its own checkout completed with LFS download disabled. Its own `initializing` lock was
then removed with `git worktree unlock .`. This checkout is for code, not ready-to-run assets.

Runtime commit: `00c036345560552a477ee6ece41c771140562799` (support commit also cleans
trailing whitespace in the two new metadata files; no history was rewritten).

- Remove `OnGUI` from prediction diagnostics and combo-input diagnostics. Retain their
  serialized flags as hidden, ignored compatibility fields, default false; prefab values
  are false too. Old scene/prefab overrides cannot re-enable rendering.
- Keep the prediction component's GUID and console mirror, including its log prefix,
  owner filtering and configurable interval. Sample in LateUpdate, after Update state
  writers. Advance the sampling deadline even when the resulting text is unchanged.
- Copy replicate/reconcile diagnostic scalars into a struct at their original event
  sites; format only when console logging samples them. Replicate writer reason is
  captured without concatenation. Reconcile speed/deltas/tick remain from that reconcile,
  even after later replicates. Before the first event the summary is `n/a`.
- Remove six hot-path UI string assignments: replicate, reconcile, modifiers and the
  three pending-impulse assignments. Modifier/pending strings have no remaining consumer.
  Existing gameplay/scalar diagnostic updates and event log sites remain intact.
- HealthReport's real consumer is bootstrap verbose logging, not the mirror (the mirror
  never prints health/compatibility strings). Build health/registry at most every 0.5 s
  while that consumer is enabled; no consumer means no report or registry formatting,
  including lifecycle refresh calls. Lifecycle flags themselves continue to update.
- Release has no mirror/health LateUpdate callback. Conditional attributes remove
  snapshot capture, report refresh and bootstrap Verbose calls/arguments. Existing
  `GameLog.Verbose` stripping stays in place. No Verbose channel was enabled for Release.

## Validation actually executed

| Check | Result and limit |
| --- | --- |
| Full runtime C# compile, UNITY_EDITOR | Pass, existing obsolete FishNet/unreachable/unused-field warnings only |
| Full runtime C# compile, DEVELOPMENT_BUILD without UNITY_EDITOR | Pass |
| Full runtime C# compile, neither verbose symbol | Pass |
| Full BuddahGo.Tests C# assembly, including both new suites | Pass |
| Compiled IL gate inspection | Pass: no diagnostic OnGUI in all 3 variants; callbacks and 2 capture callsites only in Editor/Dev; Release has zero GameLog.Verbose or diagnostic-refresh callsites |
| R8SceneCapture against installed Unity 2022.3.55f1c1 assemblies | Pass |
| Pure NUnit suite under existing Unity Mono, each of Editor/Dev/Release symbols | 7/7 each; 21/21 total |
| 100,000 replicate/reconcile capture pairs after warm-up, each configuration | 0 managed bytes on Unity Mono; supplemental method check only |
| `git diff --check` for final changes | Pass |
| Actual Unity EditMode wiring suite (3 tests) | Not run; coordinate existing Editor with main task |
| Actual player build / FishNet IL post-processing | Not run; C# compilation is not a Unity build |
| Screen/HUD/log visual check on host and remote client | Not run; main task owns real dual-end runtime |
| Scene profiler/GC R8 data | Not run; no actual scene measurements or percentage claim |

Compiler checks used the installed Unity Roslyn compiler and read-only cached dependency
references from `.worktree/bgr2-host/Library/Bee/artifacts/1900b0aE.dag`; all production
source paths were redirected to this isolated checkout. The other task's extra
`BuddahPredictedMotor.ReviewTrace.cs` source entry was excluded. Compiler output and
response files are in this worktree's ignored `Temp/DiagnosticValidation/`.
The Dev/Release checks vary preprocessor symbols but use cached dependency assemblies;
they do not replace target-specific Unity builds. No existing cache was modified.

The pure suite covers no consumer, build gate, unchanged-text intervals, interval clamp,
latest replicate state/reason, independently timed reconcile values, and Release argument
stripping. Re-run using only installed software:

```powershell
./Tools/Validation/Run-DiagnosticSamplingChecks.ps1 `
  -NUnitAssembly '<existing Unity project>/Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll' `
  -UnityEditorData 'C:/Program Files/Unity/Hub/Editor/2022.3.55f1c1/Editor/Data'
```

The first attempt used .NET 8 to run Unity's net35 NUnit assembly and failed with
`System.Runtime.Remoting.Messaging.CallContext` unavailable. The committed runner uses
the already installed Unity Mono runtime; all three runs then passed.

After integration, reserve approximately 5–10 minutes in the main task's existing Editor
for `BuddahGo.Tests.PredictionDiagnosticWiringTests` and a screen/log check. Tests verify
no diagnostic OnGUI method, prefab flags/mirror, and no-consumer/interval health behavior.
No Editor test result is claimed here. To run headless later, only after the project is
released by its current owner:

```powershell
& '<Unity>/Editor/Unity.exe' -batchmode -nographics -projectPath '<integrated project>' `
  -runTests -testPlatform EditMode -testFilter BuddahGo.Tests.PredictionDiagnostic `
  -testResults '<output>/diagnostics.xml' -logFile '<output>/diagnostics-editor.log'
```

Also verify owner mirror lines advance with current tick/state at 0.5 s, non-owner
filtering remains, disabled mirror emits no mirror logs, verbose health can be enabled,
and existing intro/authority validation signals still print. Inspect Release for absence
of prediction mirror/Verbose logs without changing the Console or normal HUD.

## R8 comparison design — no actual data yet

Do not call a screen-disabled increment the whole P2 improvement. Required P2 cells:

| Cell | Revision | Screen diagnostics | Meaning |
| --- | --- | --- | --- |
| A | `707376f3bf0e33ad8586eba8034a9fe33caf68c8` (#49) | Existing defaults | Pre-P2 baseline |
| B | `6129b59a3e4a7076963937d5b2b34eb7b0ecf5af` (#50) | Existing defaults | P2 code, diagnostics still on |
| C | #50 plus the runtime-only P2 backport below | Off | This decision's increment at P2 |
| D (stack supplement) | `95e57a0c8763bf79867098485215fd422aee0ddb` | Existing defaults | Current stack, diagnostics on |
| E (stack supplement) | `95e57a0` plus runtime commit | Off | Increment on current stack |

Report A→B as existing P2 changes, B→C as this increment, A→C as combined P2+decision,
and D→E as current-stack increment. A→D/E also contains P3–P6 and cannot isolate P2.
Do not include main task N10/R6/R9 WIP in one cell only. Resolve all source revisions
before captures; record the exact commit and probe file hash per cell.

Fixed protocol (perform sequentially after the dual-end task releases the machine):

1. Unity **2022.3.55f1c1**, Windows x64 **Mono Development** player, same quality level,
   same scripting defines, no Deep Profile/Script Debugging/Autoconnect Profiler. Keep
   verbose and mirror defaults identical across cells. Keep both endpoints' logs on disk.
   Do not compare Editor to player or Development to Release. Release gets a separate
   functional check; if profiled, create a separate entire A/B/C dataset.
2. Same machine, GPU/driver, resolution **1280×720**, windowed, VSync **0**, cap **60 fps**,
   same tick rate from the committed TimeManager configuration. Record its value before
   building and reject any difference. Fixed **2 players** (host + one client), same
   endpoint roles, transport/network conditions, loadout/seed/scene and input script.
   Both endpoints must run the same cell. Record each role separately; never pool them.
3. Use MainMenu → PropertySelection → **RaceMap** through the normal network flow.
   The primary workload is no input after handoff, identical selected spawn/loadout.
   Confirm handoff and prediction ticks are live during warm-up in both peers' logs.
   If this map/setup ends within the window or cannot remain in RaceMap, stop: define
   a viable fixed workload/window for ALL cells before taking any accepted captures.
   Do not accept differing partial durations. Input/combat workload may be a separate
   complete dataset, not mixed into these idle runs.
4. Copy the exact same `Tools/Performance/R8SceneCapture.cs.txt` to
   `Assets/Scripts/Testing/R8SceneCapture.cs` in each disposable measurement checkout;
   use one identical generated meta/GUID. It depends only on Unity, so it also works
   before P2/P6. Keep the probe as a documented measurement-only delta, outside PR code.
   Prepare local LFS assets/package cache using existing authorized sources first.
5. Warm up **30 seconds after RaceMap becomes active**, then record **120 seconds**.
   **5 independent fresh-process runs per cell**, interleaved
   A1/B1/C1, B2/C2/A2, C3/A3/B3, A4/C4/B4, B5/A5/C5. Close only your own measured
   players between repeats; do not run builds/tests/recordings concurrently.
   A/B/C needs at least **37.5 minutes** plus builds/entry; D/E adds **25 minutes**.
6. Launch with each endpoint's ordinary network arguments, plus (example for host B1):

```text
-screen-width 1280 -screen-height 720 -screen-fullscreen 0
-r8Capture -r8Label B -r8Revision 6129b59a3e4a7076963937d5b2b34eb7b0ecf5af
-r8Role host -r8Run 1 -r8Scene RaceMap -r8Warmup 30 -r8Seconds 120
-r8Output <absolute-unique-output-directory> -logFile <absolute-player-log>
```

Use unique cell/run/role filenames and an empty output directory to avoid overwrites.
The probe preallocates its buffer before warm-up, samples Unity ProfilerRecorder's
`Main Thread` and `GC Allocated In Frame`, and writes CSV/JSON only after capture ends.
It marks missing counters, early scene changes/quits and buffer overflow invalid.
Recorder LastValue and frame index refer to the previous completed frame. GC bytes are
whole-frame allocations, not retained heap growth. Main Thread duration includes waits
and is not isolated motor CPU time; use it as a whole-frame metric with that limit.
For attribution, record an additional identically configured profiler trace in every
cell and inspect motor diagnostics/HealthReport/OnGUI and GC.Alloc call stacks; keep
those instrumented runs separate from the primary dataset.

```powershell
python Tools/Performance/summarize_r8.py '<capture-directory>' > '<output>/per-run.csv'
```

The script rejects incomplete runs, missing frames, missing revision IDs, inconsistent
hardware/build/settings, and anything other than five repeats per label/role. It prints
per-run main-thread mean/p95, GC bytes/frame and bytes/second, then across-run medians
and min/max. Confirm all A/B/C roles exist yourself before comparing: the tool validates
present groups, it does not invent absent cells. Attach raw CSV/JSON, logs, exact hashes,
build settings and workload record. Compute any percentage only from these measured,
matched datasets, report the run variation, and identify the comparison explicitly.

## Integration boundary

The primary stack task remains the only publisher. The runtime commit is independent
of test assemblies and tools. Tests belong to #58/P6, where BuddahGo.Runtime,
InternalsVisibleTo and BuddahGo.Tests already exist; do not backfill their assembly setup
into #50. Support files/tests are in the next commit on the primary task branch.

A direct apply of the current-stack runtime patch onto #50 was tested with an isolated
temporary Git index and correctly failed: #50 still has a monolithic motor, whereas
P6 splits `.DebugState.cs` and `.Events.cs`. A separate local branch
`codex/debug-log-sampling-p2-backport` is prepared directly on #50; its first commit is
`543060dc1ece200baddcf7150b6eb8e17fc1ac49`, followed by metadata/format cleanup at final tip
`f1ee5ab929d7b78a6611148b53a6876474d6eb4f`. The final binary patch is
`Temp/DiagnosticValidation/pr50-backport.patch`; `git apply --cached --check` against
an isolated #50 index passed (exit 0), as did `git diff --check`.
This backport transplants only diagnostic hunks into the monolithic motor. It does not
pull later tick-clock fixes, RPC changes, tests or P6 structure into #50. It has been
diff-reviewed, not compiled or run as a complete #50 Unity project.

For earliest-PR publication, fast-forward/cherry-pick the P2 backport onto verified #50,
then propagate forward through the stack without rebasing/replacing published history.
At the P6 split, resolve the diagnostic hunks into the three motor partials using
`00c0363` as the intended final diagnostic behavior. Preserve all main task trace/R6/R9
changes. Do not blindly cherry-pick both equivalent implementations onto the same tip.
If only applying at #58, cherry-pick primary runtime then support commits; that does not
by itself resolve #50's reviewed head. Recheck remote tips before publication.

Shared motor conflict points: `Update()` pending text, writer-relinquished status call,
`ReconcileState()` summary block, `UpdateReplicateDebug()`, `SyncModifierDebugState()`,
`BuildModifierSummary()`, and `ConsumePendingImpulseEvents_Authoritative()` pending text.
No prediction data layout, RPC attributes/order, event consume order or game logic changes.

Complete changed-file inventory for the current-stack implementation/support:

```text
Assets/Character/Prefab/Buddah.prefab
Assets/Scripts/Buddah/ComboSkill/ComboSkillInput.cs
Assets/Scripts/New_Buddah/Bootstrap/BuddahPredictionBootstrap.cs
Assets/Scripts/New_Buddah/Config/BuddahPredictionDebugSettings.cs
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.DebugState.cs
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.Events.cs
Assets/Scripts/New_Buddah/Debug/BuddahDiagnosticLogSampling.cs (+ .meta)
Assets/Scripts/New_Buddah/Debug/BuddahPredictionLogSnapshot.cs (+ .meta)
Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugOverlay.cs
Assets/Scripts/New_Buddah/Validation/BuddahPredictionRuntimeHealthReport.cs
Assets/Tests/EditMode/PredictionDiagnosticSamplingTests.cs (+ .meta)
Assets/Tests/EditMode/PredictionDiagnosticWiringTests.cs (+ .meta)
Tools/Validation/Run-DiagnosticSamplingChecks.ps1
Tools/Performance/R8SceneCapture.cs.txt
Tools/Performance/summarize_r8.py
Docs/diagnostic-log-sampling-r8.md
```
