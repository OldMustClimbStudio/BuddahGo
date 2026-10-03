# Thrust-vector acceptance scripts (2026-10-02)

Both scripts drive the Development Player built by `Assets/Editor/ThrustVectorAcceptanceBuild.cs`
(`Unity.exe -batchmode -quit -executeMethod ThrustVectorAcceptanceBuild.Build`), expected at
`C:/Users/dwh88/Documents/Codex/2026-10-02/claude-thrust/build/BuddahGoThrust.exe`. Edit `ROOT` to move it.
Each run copies summary/configuration/events/perf (and race-results) into `../player/<name>/`.

- `lap.sh <name> <profile.json> <laps 1|3> <aiCount> [timeoutSec]` — one racer driven by the profile
  (`--ai-a1-output` / `--ai-a2-output`), optional server AI sharing the same profile (`--ai-count`),
  GO+2..11 s frame-time window in summary.json (perfMedianMs / perfP95Ms / perfTicks).
- `race.sh <name> "<p0;p1;...;p5>"` — six-car three-lap race, one profile per car
  (`--ai-profiles`; p0 drives the harness car, p1.. the server AI by RacerId). Prints the ranking from
  `race-results.json` (laps completed, total, best, average, side contacts). The match ends by product
  rule when the first car finishes, so other cars only record completed laps.

Profiles: `Tools/ai/normal.json` (V5 beam), `thrust-vector-design.json` (line follower, no walls),
`thrust-vector-wall*.json` (wall corridor variants), `thrust-vector-nobrake*.json` (no braking, race1 winner and its race2 variants).
