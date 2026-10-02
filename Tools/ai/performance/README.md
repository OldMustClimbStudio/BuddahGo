# Five-AI cost evidence (2026-10-02)

Source baseline: a9c9a17471a7073c5bab84f9bbb32013b7356213.
See ../../../Docs/single-player/performance-costs-2026-10-02.md for scope and limitations.

Each perf.csv is the original GO+2..11 second window, with headers unchanged.
Recompute: python analyze.py baseline-full baseline-light baseline-quiet compare-legacy compare-optimized compare-final

Scopes 0..4 = AI RacerId 10000..10004 Plan; 5 = other AI; 6 = BuildReplicateData inclusive;
7 = RunInputs; 8 = tracker; 9 = private observer; 10 = racing-line projection; 11 = model Step;
12 = net post-tick count; 13 = post-physics callback count; 14 = driver inclusive; 15 = VJitter.
Scopes overlap: do not add driver/build/plan/project/model times together.
wall_ms spans consecutive LateUpdates; dt_ms and main_ns describe different frame phases.
GPU recorder returned only zero, so GPU cost is unavailable. gc_bytes=-1 is unavailable.
Deep scopes 10/11 are disabled in compare-* (zeros mean disabled, not free).

All runs: five AI, ordinary intro/GO, 60-fps cap, vSync off, 1280x720 private non-Development Player.
baseline-* have per-call deep instrumentation and cannot be compared to shallow frame costs.
compare-legacy and compare-optimized share a binary. compare-final is the final optimized rebuild;
no repeated baseline or significance claim. Live trajectories differ with tick debt / physics scheduling.
No finish/Rematch/natural-lap evidence. Intro/output and process paths in full logs remain private.

The frozen Legacy*.cs implementations in Assets/Tests/EditMode are independent a9c9a17 oracles.
tests.json records final EditMode results. The first scalar arithmetic attempt failed exact equivalence
and was removed; that rejected test log remains in the task workspace.

Full source archives, private probes/builds, raw logs, available recorder names, all frame CSVs,
failed attempt, source/WIP manifests: C:/Users/dwh88/Documents/Codex/2026-10-02/task-2.
Private probe uses --handoff-observe ABSOLUTE_OUTPUT, --light, --no-jitter, --legacy, optional --deep.
It exits after GO+12s; it never reaches the inherited controlled-finish branch.
Initial baseline summary.json's syntheticFinishes=1 was an inherited constant, not an actual action;
the later probe corrected that metadata to 0. Raw evidence was retained unchanged.
