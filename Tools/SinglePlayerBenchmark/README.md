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
Windows x64 Player with `BuildOptions.None` and the extra scripting define
`BUDDAH_PRIVATE_SOLO_ACCEPTANCE`, then remove the temporary assets and their metas.
Do not add that define to normal project settings. None of the templates execute in
the Editor or without the explicit command line. Use the same built artifact and
committed game source for all three modes:

```powershell
& $player --solo-acceptance benchmark3 --solo-output $privateCapture -logFile $privateLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
& $player --solo-acceptance escapes3 --solo-output $privateEscapes -logFile $privateEscLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
& $player --solo-acceptance strict14 --solo-output $privateEndurance -logFile $privateEnduranceLog -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

Run one Player at a time. Close the Editor for performance capture. `benchmark3`
uses seed 1729, quality index 2, 1280x720, vSync 0 and target 60 FPS; it requires
these observed settings at each window boundary. Three fresh sessions each drive
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

Latest sampler refinement compiled in a non-Development Windows x64 Player.
Its corrected frame cap and trigger witness still require a fresh runtime capture;
compilation does not establish frame-counter availability or timing accuracy.
