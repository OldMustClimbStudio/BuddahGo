# Practice V3 crossing timestamp repair

Source-only repair based on `56650e3` (gameplay baseline `9e428c1`). This is not final Practice acceptance. No Unity Editor, Player, build, process control, push, PR or merge was performed for this repair.

## Evidence and decision

The baseline handoff and `Logs/final-acceptance/strict14/crossings.jsonl` in the existing single-player worktree identify accepted natural line crossings independently of the periodic reports. M1 first-lap error is +183.333 ms against the unchanged ±100 ms requirement. Its later lap errors are -83.333/-66.667 ms; total error is +33.333 ms. M2 first-lap error is +100 ms. A zero lap-sum residual does not demonstrate crossing accuracy.

`Buddah.prefab` serializes a 0.2 s report interval. Previously `PlayerProgressReporter` supplied the later report clock to `RaceTiming`. Merely reporting a changed lap immediately would reduce this delay but still sample after a possible Update stall. The repair instead captures `MatchServices.Clock.Now` in `LapProgress` immediately after the existing acceptance checks increment the lap.

- Capture and consumption require owner + initialized server + `MainMenuHome` return target. No client-supplied timestamp or new RPC parameter is introduced; Online keeps its previous path.
- **2026-10-04 update:** the `AcceptedLapTiming` queue described below was replaced by `LapProgress.RecordCompletedLap`, which records the validated Solo crossing on the authoritative clock immediately; `RaceTiming` already rejects repeats, regressions and jumps, and the race-length clamp is kept. See [pr59-code-review-2026-10-04.md](pr59-code-review-2026-10-04.md) §8. The text below is the historical repair record.
- `AcceptedLapTiming` retains completed-lap timestamps in order, rejects repeated/regressive lap numbers, and drains once during the normal server progress report, before its existing timing observation and finish registration.
- Initial entry into lap 1 clears the buffer without recording a completed lap; the authoritative GO remains the first-lap origin. Network start/stop clears pending data, and clock/timing service replacement invalidates old pending records. Existing Rematch scene/actor recreation remains responsible for resetting race progress; no new in-place race reset protocol is introduced.
- The final crossing is observed before `RaceFinishManager` calls `Finish`. Existing `RaceTiming` idempotence retains that crossing rather than the later finish call time. Finish presentation, `FinishServerTime`, and leaderboard updates still occur at the normal report; this patch changes recorded lap/total timing, not presentation latency.
- Checkpoint/forward/cooldown/exit-distance validation, report interval, prefab data, RacerId mapping, completion → timing → finish → leaderboard → race-end order, and Online RPC behavior are preserved.

## Checks performed

Compiled the actual new helper plus existing `RaceTiming`/`MatchClock` and their actual NUnit test sources with local Roslyn, then invoked all test/test-case methods using the installed .NET Framework and the existing Unity package's NUnit DLL, in an isolated console process: **42 passed, 0 failed** (12 new cases, 22 RaceTiming, 8 MatchClock).

Coverage includes baseline +183.333 ms and a 1.25 s delayed report, GO/first completed lap, ordered multiple crossings before a report, duplicate consumption and triggers, final-lap idempotence, reset with the same services, replacement clock/timing services, invalid capture and race-length bounds. This compiles and executes pure logic only; it is not Unity/FishNet compilation, code generation, EditMode execution, or live trigger validation. An initial .NET 8 attempt could not execute the old NUnit DLL (`CallContext` type missing); the .NET Framework rerun resolved that harness incompatibility.

Local untracked runner and results are in the repair worktree's `Logs/v3-source-checks/`; committed regression tests are `Assets/Tests/EditMode/AcceptedLapTimingTests.cs`. Static checks verify capture placement, the unchanged original trigger/report bodies after removing only the added seams, the unchanged RPC signature and prefab interval, and matching unique new `.meta` GUIDs. `git diff --check` passes.

## Runtime handoff after strict14 releases ownership

1. Integrate the local repair commit through the coordinating task and freeze the new gameplay HEAD. Preserve the old raw evidence and build hashes. Populate LFS assets if using this source-only repair worktree, which was checked out with smudge disabled.
2. Compile in Unity and inspect new errors/FishNet processing. Run `AcceptedLapTimingTests`, `RaceTimingTests`, `MatchClockTests`, `RaceEndPolicyTests`, relevant `HandoffClockTests`, and existing `SoloSessionFlowTests`. Verify reset on actual Rematch and ReturnHome/re-entry, not just buffer unit tests. No Online runtime tests are required by this Solo task.
3. Produce a new non-Development build and drive a natural three-lap Practice race from GO. Use the existing independent accepted-trigger witness unchanged. Compare each individual lap and final total with the accepted-crossing brackets, allowing ±100 ms plus numeric roundoff only; do not expand tolerance by the reporting/polling interval. Inspect first line entry, all three completed laps, duplicate collider contacts, final result rows, and lap-sum consistency. Include a delayed-report/stall case to check that result publication delay does not alter captured times.
4. Repeat natural three-lap validation after actual Rematch and after ReturnHome/new Practice. Confirm no old pending timestamp or duplicate guard leaks, and preserve progress/finish/result ordering. Controlled/debug finishes cannot replace these samples.
5. Re-establish final-head strict14, Esc/ordinary Release smoke and the required Development/Release performance evidence under the existing acceptance protocol. Do not relabel old binaries or queued captures as the new HEAD; retain undecided budget and exclusive-performance limitations.

The continuing old strict14 remains evidence for its **old** source: natural three-lap completion, real Rematch/ReturnHome paths, results hold, counts/clock cleanup, scene lifecycle and memory trends, to the extent actually completed and recorded. Its raw timing also remains valid evidence of the V3 defect. It does not validate this patch. Initial black hold screenshots remain non-visual evidence; later visible captures keep their original provenance. V3 and final Practice acceptance remain pending new runtime evidence.
