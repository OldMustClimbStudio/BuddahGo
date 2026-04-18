# 13 - Validation Gates

[Back to index](index.md)

Each gate is a binary pass/fail. A phase is not done until all gates listed for that phase return pass.

## V1 - Compile + No New Warnings
- `Editor.log` shows zero compile errors.
- No new warnings about `Rigidbody`, `PredictionRigidbody`, or FishNet annotations.

## V2 - Reconcile Field Audit
- For every field read inside `[Replicate]`, grep confirms the field is restored inside `[Reconcile]`.
- Automated by a small Roslyn-based pass introduced in Phase 4 (or grep + manual checklist).

## V3 - Gameplay Shake (Owner + Spectator)
- Reproduce: 4-player race, 60-second loop, 60-100 ms RTT, prediction at 60 Hz.
- Pass: no visible shake on own or other Buddah models. Verify on screen-record at 240 fps capture.

## V4 - Intro Flicker (Remote)
- Reproduce: 4-player race, observe non-owner intro on each peer for 10 consecutive matches.
- Pass: no back-and-forth flicker. Spline + handoff motion appears smooth on remote.

## V5 - Skill Determinism
- Reproduce: cast acceleration / root-then-acceleration / invert-turn / scale once each, with 0/50/150 ms RTT.
- Pass: motor's final position differs by less than physics epsilon between owner and server reconcile snapshots.

## V6 - Combat Determinism
- Reproduce: server-issued impulse hits non-owner Buddah at 100 ms RTT.
- Pass: owner's predicted position converges to server reconcile within one tick after impulse landing; no rubber band.

## V7 - Respawn Snap
- Reproduce: trigger respawn during high-speed turn.
- Pass: position, rotation, velocity all snap on the same tick on owner and server. No double-snap. Trail rebases on next frame.

## V8 - Mode Switch
- Reproduce: switch from Legacy to PredictionV2 and back twice.
- Pass: no leaked components, no leaked event channel entries, no console errors. Reconcile data is fresh after each switch.

## V9 - Channel Backpressure
- Synthetic test: enqueue 256 impulses inside one tick.
- Pass: channel returns false beyond capacity, no exception, motor remains responsive.

## V10 - Editor + Playmode Tests
- All existing tests pass.
- New tests added in Phase 0 and Phase 8 pass.

## V11 - Manifest Integrity
- `Docs/harness-manifest.json` references new `Docs/prediction-refactor-plan/index.md` under prediction system `requiredDocs`.
- `Get-BuddahGoHarnessContext.ps1 -Intent refactor -Systems prediction` returns the new docs.

## V12 - Code Boundary
- Roslyn rule: no `Rigidbody.{position,rotation,velocity,angularVelocity,mass,Sleep,WakeUp}` writes outside whitelisted simulation step files.
- Pass: zero violations.

## V13 - Performance Budget
- Profile: motor `[Replicate]` time at 60 Hz, 8 active Buddahs.
- Pass: average per-tick cost no worse than +10 percent vs pre-refactor baseline (initial PredictionRigidbody indirection cost is amortized over the wins).

## Phase-to-Gate Mapping

| Phase | Required gates |
|---|---|
| 0 | V1 |
| 1 | V1, V11 |
| 2 | V1, V8 |
| 3 | V1, V2, V5 (shadow compare) |
| 4 | V1, V2, V3, V6, V13 |
| 5 | V1, V2, V5 |
| 6 | V1, V2, V7, V8 |
| 7 | V1, V3, V4 |
| 8 | V1, V2, V12, V11, V10 |

See [14-risks-contingencies.md](14-risks-contingencies.md) for failure handling.
