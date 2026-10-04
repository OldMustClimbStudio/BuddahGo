# Thrust-vector acceptance scripts (2026-10-02)

Both scripts drive the Development Player built by `Assets/Editor/ThrustVectorAcceptanceBuild.cs`
(`Unity.exe -batchmode -quit -executeMethod ThrustVectorAcceptanceBuild.Build`). Since 2026-10-03 both the
build and the scripts default to the git-ignored project `Logs/` folder: the Player at
`Logs/thrust-vector-build/BuddahGoThrust.exe` (build env `THRUST_BUILD_OUTPUT`, script env `THRUST_EXE`) and full
runs under `Logs/thrust-vector/runs/<name>/` (script env `THRUST_ROOT`). The 2026-10-02 runs used
`C:/Users/dwh88/Documents/Codex/2026-10-02/claude-thrust/`.
Each run copies summary/configuration/events/perf (and race-results) into `../rollout-2026-10-02/player/<name>/`,
resolved relative to the script; those copies are committed, with CSV/JSONL stored via Git LFS.

- `lap.sh <name> <profile.json> <laps 1|3> <aiCount> [timeoutSec]` — one racer driven by the profile
  (`--ai-a1-output` / `--ai-a2-output`), optional server AI sharing the same profile (`--ai-count`),
  GO+2..11 s frame-time window in summary.json (perfMedianMs / perfP95Ms / perfTicks).
- `race.sh <name> "<p0;p1;...;p5>"` — six-car three-lap race, one profile per car
  (`--ai-profiles`; p0 drives the harness car, p1.. the server AI by RacerId). Prints the ranking from
  `race-results.json` (laps completed, total, best, average, side contacts). The match ends by product
  rule when the first car finishes, so other cars only record completed laps.

Profiles: `Tools/ai/difficulty-{easy,normal,hard}.json` (the shipped tiers). The 2026-10-02 variant profiles
(`normal.json` V5 beam, `thrust-vector-design/wall*/nobrake*.json`) and their runs were removed on 2026-10-04;
read them at commit `20401e5`.
