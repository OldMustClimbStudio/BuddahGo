# Screen diagnostics, log sampling and R8

Prediction and combo-input screen diagnostics have no `OnGUI` entry point in Editor,
Development or Release. Ordinary game HUD and Unity Console are retained. The former
`enableOnScreenDebug` and `debugHud` fields remain hidden compatibility fields, false
in `Buddah.prefab`; old serialized overrides cannot restore the removed rendering.
This implements the authorized screen-only change from
[PR #50](https://github.com/OldMustClimbStudio/BuddahGo/pull/50#discussion_r4146561530).

## Configure the retained logs

| Consumer | Setting and behavior |
| --- | --- |
| Prediction console mirror | [BuddahPredictionDebugOverlay](../Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugOverlay.cs): `mirrorSummaryToConsole` enables sampling; `consoleMirrorIntervalSeconds` defaults to 0.5 seconds and is clamped to at least 0.1 seconds. Initialized client non-owners are filtered out. |
| Runtime health/compatibility report | [BuddahPredictionBootstrap](../Assets/Scripts/New_Buddah/Bootstrap/BuddahPredictionBootstrap.cs): in Editor/Development, `HasHealthLogConsumer` is true when either non-null `debugSettings` has `enableVerboseLogs` enabled, or global `NetDebug.EnableVerboseLog` is enabled. [HealthReport](../Assets/Scripts/New_Buddah/Validation/BuddahPredictionRuntimeHealthReport.cs) samples at most every 0.5 seconds. |
| Release build | Mirror/health `LateUpdate` callbacks are absent. Conditional attributes strip diagnostic captures, refresh calls and Verbose calls/arguments. Release does not regain a Verbose channel. |

The mirror alone is not a health-string consumer. With no health consumer, registry
refresh and health formatting are skipped, including calls from lifecycle refreshes;
lifecycle flags still update. Disabling the mirror does not disable other event logs.
The prefab retains `mirrorSummaryToConsole: 1` and a 0.5-second interval.

## Sampling contract

- [BuddahPredictionLogSnapshot](../Assets/Scripts/New_Buddah/Debug/BuddahPredictionLogSnapshot.cs)
  captures diagnostic scalars at the original replicate/reconcile event sites. Reconcile
  tick, speed and deltas stay tied to that reconcile even if later replicates update
  live state. Replicate writer reasons are captured without concatenation. Before an
  event exists its summary is `n/a`.
- Formatting occurs when the mirror samples in `LateUpdate`, after Update writers.
  [BuddahDiagnosticLogSampling](../Assets/Scripts/New_Buddah/Debug/BuddahDiagnosticLogSampling.cs)
  advances the sample time even when text is unchanged; deduplication must not resume
  formatting every frame after a quiet interval. Unchanged summaries emit no new line.
- Removed modifier/pending-impulse UI strings have no remaining consumer. Gameplay
  state, scalar diagnostics, event log sites and the prediction component GUID remain
  intact. Timing/state correctness is separate from this logging policy; see the
  [prediction clock contracts](prediction-design.md).

## Checks and reproduction

[PredictionDiagnosticSamplingTests](../Assets/Tests/EditMode/PredictionDiagnosticSamplingTests.cs)
covers consumer/build gates, sample intervals, interval clamping, independently captured
replicate/reconcile values and Release argument stripping.
[PredictionDiagnosticWiringTests](../Assets/Tests/EditMode/PredictionDiagnosticWiringTests.cs)
covers health-consumer wiring, absence of both `OnGUI` methods, and prefab screen flags
with the console mirror retained.

The pure suite can run without launching an Editor, using an installed .NET SDK for
compilation, the installed Unity Mono runtime and an existing Unity NUnit assembly:

```powershell
./Tools/Validation/Run-DiagnosticSamplingChecks.ps1 `
  -NUnitAssembly '<existing project>/Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll' `
  -UnityEditorData '<Unity installation>/Editor/Data' `
  -OutputDirectory '<unique output directory>'
```

The [runner](../Tools/Validation/Run-DiagnosticSamplingChecks.ps1) varies Editor,
Development and Release symbols and also checks allocations of the capture methods.
Those checks are not a Player build, FishNet post-processing, or scene performance test.
For the actual Unity wiring tests, use a disposable project after it is available for
testing and run:

```powershell
& '<Unity>/Editor/Unity.exe' -batchmode -nographics -projectPath '<integrated project>' `
  -runTests -testPlatform EditMode -testFilter BuddahGo.Tests.PredictionDiagnostic `
  -testResults '<output>/diagnostics.xml' -logFile '<output>/diagnostics-editor.log'
```

The previously published [runtime record](optimization/review-runtime-round2.md#reproduction-and-evidence)
contains the 75/75 integrated EditMode result, including the three wiring tests, and
its version-specific limits. That historical result is not a new run or a result for
later snapshot-pair changes. In a functional check, inspect current owner mirror values,
non-owner filtering, disabled-mirror silence, health logging when enabled, and normal
HUD/intro/authority signals separately. Release should retain its existing log stripping.

## Published R8 comparison

The [Editor report](optimization/review-r8-editor.md) is the authoritative published
measurement record: three source cells, three fresh host/client pairs per cell,
30 seconds of warmup after gameplay unlock and 60 seconds of capture at 1920x1080.
It distinguishes existing P2 changes (A to B) from the log-only increment (B to C), retains
all per-run variation and limits the conclusions to that Editor workload.

Use the archived [ReviewR8Editor instructions](../Tools/Performance/ReviewR8Editor/README.md)
for exact probe/driver setup and replaying the published analysis. Preserve identical
source/settings on both peers, use distinct output directories and avoid concurrent
tests, builds or recording. Main Thread includes Editor work and waits; GC per frame
and per second are separate metrics. This is not a Player/Release performance claim.

The original five-repeat, 120-second Development-player proposal was not executed;
its protocol remains in this file's Git history. Do not mix its settings or the
standalone `summarize_r8.py` five-repeat validator with the measured three-repeat Editor
dataset. A final-stack P6 comparison was not part of the published R8 dataset.

To inspect the original proposal and auxiliary compiler-check record, use
`git show 4dfb45d:Docs/diagnostic-log-sampling-r8.md`.

## Integration boundary

The runtime backport is already on #50 and propagated through the stack. Its earlier
preparation/publication commands are historical, not a remaining task. #50 used the
monolithic motor; the P6 stack uses partial files. Do not apply both equivalent patches
again or bring later test-assembly setup into an earlier PR.

Sampling/wiring tests belong to #58's Editor-only test assembly. Performance probes and
drivers remain opt-in validation sources, outside production gameplay. This guide adds
no raw runtime logs, private measurements or video; existing published evidence retains
its original scope. Visual acceptance and complete late-client start presentation are
not established by diagnostic or performance checks.
