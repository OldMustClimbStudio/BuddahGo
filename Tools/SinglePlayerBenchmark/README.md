# Single-player benchmark tools

Python 3.10+ standard library only. Entry point: `benchmark.py`.
These commands never start Unity, a Player, Steam, or another driver.

See [the protocol](../../Docs/single-player/benchmark.md) and
[the sampler interface](sampler-interface.md). The existing strict14 driver owns
physical driving and lifecycle evidence. This package owns offline validation.

```powershell
python Tools/SinglePlayerBenchmark/benchmark.py plan --out "$env:TEMP/solo-plan.json"
python -m unittest discover -s Tools/SinglePlayerBenchmark -p "test_*.py" -v
python Tools/SinglePlayerBenchmark/test_benchmark.py --fixture "$env:TEMP/solo-synthetic"
python Tools/SinglePlayerBenchmark/benchmark.py run --bundle "$env:TEMP/solo-synthetic" --out "$env:TEMP/solo-report.json"
```

The synthetic run intentionally returns exit 1 with `SYNTHETIC_ONLY`; the fixture
is a test of the analysis, never a measured game result. `plan` emits an unfilled
capture contract, not an executable Player launch. Fill metadata from the actual
capture/build, never from assumed settings. Actual capture belongs to the sole
Unity/Player coordinator.

## Player capture integration

The three C# templates now supply the opt-in sampler and the real-input operator:
`SoloContinuousAcceptanceRuntime.cs.txt`, `SoloBenchmarkFrames.cs.txt` and `SoloCrossingProbe.cs.txt`.
Copy them as `.cs` files into a temporary `Assets/Scripts/Match/` location, build a
Windows x64 **Development** Player with `BuildOptions.Development` and the extra
scripting define `BUDDAH_PRIVATE_SOLO_ACCEPTANCE`. `SoloBenchmarkBuild.cs.txt` is an
optional temporary Editor entry point implementing exactly these build options;
copy it to `Assets/Editor/SoloBenchmarkBuild.cs`, then the sole coordinator can use
`-executeMethod SoloBenchmarkBuild.BuildDevelopment -soloBenchmarkBuildOutput <new-private-directory>`.
It refuses an existing destination. Remove only this build's temporary assets and
their metas after building, preserving pre-existing private helper ownership.
Do not add that define to normal project settings. The three runtime templates do
not execute in the Editor or without the explicit command line. Use the same built artifact and
committed game source when running these modes on that artifact. Existing completed
strict14/Esc evidence may instead use separately hashed artifacts on the same clean
game head, as the manifest already permits; do not repeat strict14 just for GC:

```powershell
& $player --solo-acceptance benchmark3 --solo-output $privateCapture -logFile $privateLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
& $player --solo-acceptance escapes3 --solo-output $privateEscapes -logFile $privateEscLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
& $player --solo-acceptance strict14 --solo-output $privateEndurance -logFile $privateEnduranceLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

Run one Player at a time. Close the Editor for performance capture. `benchmark3`
uses seed 1729, quality index 2, 1280x720, vSync 0 and target 60 FPS; it requires
these observed settings at each window boundary and every sampled update. Three fresh sessions each drive
for 30 seconds before a 60-second window, then use real Esc/ConfirmQuit and verify
clean Home. This is bounded performance evidence, not completed-match evidence.
`escapes3` separately checks selection, intro before GO and racing after GO,
including actual reopen after each exit. `strict14` preserves the full-race plan.

The sampler buffers frames and ordered Dynamic-input frame/tick/key records; it
writes `frames.csv` and runtime-only `capture.json` after measurements. Merge
observed fields into the plan manifest and attach audited build/source hashes and
the two completed lifecycle receipts before using the offline validator. Missing
counter samples remain blank. The existing feedback controller's queries and
one-second diagnostic output remain part of this named instrumentation; it is
not a fixed-input tape or a production AI planner. Runtime settings are restored
on cleanup. Raw logs, screenshots and machine-local paths stay private.

The capture applies the requested frame cap after host/scene initialization and
before driving warmup, because FishNet updates the cap on connection startup.
The first rejected integration attempt observed 500 FPS requested by the host;
it produced no measured frames and is not a baseline.

In strict14 only, the read-only crossing witness records the actual start-line
OnTriggerEnter clock bracket and accepted lap changes, plus the exact stored GO
origin. `crossings.jsonl` is separate timing-accuracy evidence; the existing
polling residuals alone still do not prove trigger precision. No gameplay object
position, velocity, lap, progress or finish state is written by the witness.

## GC support and repeat capture

The 2026-10-01 Release evidence has 3 x 3600 frames and 10800 blank GC cells.
Unity 2022.3 lists `GC Allocated In Frame` as unavailable in Release Players
([Memory Profiler reference](https://docs.unity3d.com/2022.3/Documentation/Manual/ProfilerMemory.html)).
Development instrumentation is the selected supported capture path. Keep
`Debug.isDebugBuild`, build report options, artifact and helper hashes in provenance.
Do not enable Deep Profiling, Script Debugging or profiler auto-connect for this
capture. Native availability must still be verified on the actual target build.

After each driving warmup, the sampler requires a fresh GC counter sample before
opening a window. Release or unavailable Development counters abort through the
existing cleanup path, preserving `capture.complete=false` and a reason. Each
recorder starts separately per race and is disposed at window end, failure finish,
or external-exit cleanup. Nonwrapping recorder counts must advance exactly once
per completed frame; empty, stopped, stale, skipped or saturated samples remain
blank. A valid observed allocation value of zero remains zero. Partial samples
retain their observed values and missing counts, and cannot qualify the benchmark.
No heap delta or main-thread-only allocation fallback is substituted.

`capture.actual` is frozen at the first measured window start; each window records
`actual` and `actual_end`. `post_cleanup_actual` records the later Home settings,
which may legitimately be 500 FPS. Per-frame setting checks allocate no objects.
Copy `requested`, `actual`, `post_cleanup_actual`, and `snapshot_policy` into
`run.json.settings`; copy windows intact. Map `gc_method/gc_counter/gc_unit/sample_policy`
to `sampling.gc_counter.method/name/unit/sample_policy`, with the observed
availability/reason; map main availability/reason normally. Keep sample counts in
the original capture receipt. `plan` now defaults to Development and the new
snapshot/counter contract. Never fill measured settings from Home or CLI intent.

The coordinator must rebuild after final code integration, verify template hashes,
close the Editor, and recapture three fresh 30s + 60s driving windows in a NEW
private directory. Check every GC cell, availability/reason, window boundaries,
build label and zero reported missing samples before assembling the manifest.
Use only the measured Development frame/GC values together; never splice these GC
cells into old Release frames or compare the two build types as equivalent.
The old Release receipts stay unchanged and unqualified. Undecided budgets remain
null and prevent a pass or qualified baseline. This patch has no new real capture.

## Pure sampler regression checks

With .NET 8+ installed, `dotnet run --project Tools/SinglePlayerBenchmark/sampler-tests.csproj`
executes the actual sampler template against fake clock/settings/recorder APIs.
It covers fresh/stale/late samples, valid zero, preflight failures, recorder
cleanup, settings drift and Home snapshots. It does not launch Unity, a Player,
or a workload and cannot establish native counter availability.
