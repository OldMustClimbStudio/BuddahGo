# Six-racer foundation and opening — 2026-10-02

Scope: existing `feat/single-player-mode`, draft PR59, starting at `e9a8692`. Human is Slot1 (internal index0); five server-owned AI occupy stable remaining slots. This module ends at GO and real driving. Skills, racer progress/ranking migration, six-racer natural completion and result choreography remain a separate task.

## Implementation

- `RacerIdentity` is serialized on Buddah.prefab, with server-assigned synchronized ID/name. `RacerId.ForAI(0..4)` uses 10000..10004; human IDs remain ClientId. Scene-owned `RacerRegistry` implements `IRacerDirectory`; session teardown clears its service.
- `SessionLauncher` preserves requested AI count instead of forcing zero. Solo setup switches between five AI and zero-AI Practice. Difficulty selection is retained, with an explicit current Normal-profile notice; existing AIRacerDriver/profile/planner parameters are unchanged.
- Match spawning creates the human followed by five ownerless AI in the same synchronous server batch. The prefab's disabled steering provider is enabled for server AI only. AI are not fabricated network clients and are never inserted into room readiness/votes.
- Six unique spawn points replace modulo reuse. New 4/5/6-slot placeholder prefabs live under `Assets/Placeholder/Intro/`; each owns a unique path ID, launch point and forward reference. Six-car formation uses 10m lateral / 12m longitudinal gaps, versus the approximately 7.8m physical collider diameter.
- Solo skips slot shuffle and sorts by assigned racer ID, so the human takes index0. Existing Online shuffle behavior remains in code. Existing TransitionToRaceMap clip, shot timing and authoritative countdown/GO scheduling are retained.
- Ownerless server AI use the same authoritative handoff and completed-physics presentation history as the human; no TargetRpc is sent to EmptyConnection. The GO consumer and physical motion remain the existing motor path. Only the human reports connection gameplay-live readiness.

## Validation record

Initial 88/89: pre-spawn guard dereferenced FishNet's not-yet-cached NetworkObject in the registry fixture. Fixed guard; 89/89 targeted checks passed. Four strengthened foundation tests passed after increasing formation gaps, and passed again after adding per-slot path-registry resolution assertions.

A first private build succeeded, but its visible runtime stalled before intro because the scene's explicit spline registry contained only old paths. This run is failed evidence, preserved separately. Corrected the scene to use the registry's existing automatic discovery and rebuilt. Final non-Development private build: **Succeeded, 0 errors / 6 warnings**, 15.54 seconds. Warnings are two nographics lighting, two existing shader, and two missing Feel post-processing scripts. These are the receipt's warnings, not a claim that every historical warning disappeared.

Final visible Player: **2,336 + 2,365 = 4,701 frames**, normal first selection/intro, one controlled human finish solely to reach actual Rematch, second selection/intro, then Home with transport and registry services cleared. **0 captured runtime errors**. All six IDs are `[0,10000,10001,10002,10003,10004]`; the room stays at one connection and the registry at six entries. Human owner0 takes slot0 in both rounds; AI owner-1 take slots1..5, with their drivers enabled and the human driver disabled.

In each round all six first report GO on the same frame (2444 / 5136). The intro's measured minimum center separation is 9.99989m. There are 1,105 / 1,107 sampled intro frames with all six visual roots inside the camera viewport; actual group screenshots were inspected. Existing TransitionToRaceMap framing covers the formation without editing its animation assets. After GO all six are dynamic; every AI produces normal -2/0/+2 motor steering and moves (minimum sampled AI speed across the 1–6s observation windows: 32.216m/s). This is short driving evidence, not clean-racing qualification.

The human's measured GO boundary has no hold/reversal: minimum forward presentation speed 58.00487 / 58.09568m/s. Completed-physics presentation delay stays 16.66667ms, graphical queue stays empty/suspended, and actor/follow poses match LateUpdate to render. Only 15 / 18 render samples occur in the narrow GO window; frame rate is a material limitation below.

**Performance is not accepted:** with five AI and the private observer, post-GO median frame times are 128.24 / 106.06ms, maximum172.56 / 221.56ms. This is an instrumented observation, not the independent benchmark. The camera can step 15.08 / 13.53m between sampled frames after GO; do not reuse the prior 0-AI 60fps smoothness claim. A bounded production-build comparison/profiling task is needed; no planner/profile/pace retuning was performed here.

The existing leaderboard visibly collapses AI under OwnerId `-1`. The new identity directory is correct, but owner-keyed leaderboard/progress/results consumers remain for the next module. No six-racer ranking or result acceptance is implied.

Local evidence: `C:/Users/dwh88/Documents/Codex/2026-10-01/task-21/`: `tests-final.xml` (89/89 before asset-only strengthening), `tests-spacing.xml` and `tests-paths.xml` (4/4 each), build receipts/logs, `evidence-visible/summary.json`, `analysis-six.json`, `analysis.json`, raw frames/phases and screenshots. `six-racer-demonstration.png` shows both openings and driving. Derived opening images normalize GPU readback row orientation only; originals remain. `evidence-missing-paths/` retains the rejected first visible run. The first preliminary build was intentionally stopped after collider review; it is not counted as a passing build.

## Explicit limits / next module

No multiplayer or Steam two-client test, no pace tuning, no claimed six-person race/results or opponent-skill acceptance. Historical AI spin-fix average126.044s, 66 side contacts and four model anomalies remain open; the five-version pace budget is exhausted.

SplineProgressTracker remains untouched: the independent read-only diagnosis found a serialized 20m projection threshold and a likely out-of-corridor early-return cause, plus clamped distance / `_lastT` inconsistency. Per-frame rejection causality is not yet proven. Ranking/checkpoint correctness remains unqualified; coordinate its separate bounded repair after task21 releases ownership.

The next match module must migrate owner-keyed progress/ranking/finish/presentation consumers through RacerId/IRacerDirectory, add server-direct legal progress/skill paths, and qualify six-racer natural racing and result anchors/Timeline. Current controlled finish exists solely to reach real Rematch during opening verification.
