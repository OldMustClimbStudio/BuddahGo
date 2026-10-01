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
