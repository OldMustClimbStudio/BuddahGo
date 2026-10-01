# Sampler interface v1 (integration contract)

Owner: the existing single-player runtime harness. No second process, driver,
production gameplay change, or new AI is provided by these tools.

## Smallest integration

1. Keep `SoloContinuousAcceptanceRuntime`'s proven Dynamic `onBeforeUpdate` A/D
   input block and normal UI sequence. It is a deterministic **feedback controller**,
   not fixed-input replay and not production AI. Hash its source and parameter set.
2. A private, opt-in sampler in the SAME Player buffers completed frames. Use the
   existing R8 capture's preallocated-buffer/deferred-write approach, but no R8
   role, online matrix, Editor baseline, or machine-wide setting changes.
3. Capture three nonoverlapping 60-second windows, each after at least 30 seconds
   of uninterrupted natural driving. Set/reapply seed 1729 before each repeat's
   session/selection; begin each repeat on the same route/start policy. Finish a
   window only on the first completed frame at/after 60 seconds. If the race ends
   early, abort that window: do not join samples from different races or scenes.
4. Append frame struct: repeat (1..3), frame index, window-relative end time,
   unscaled frame interval ms, allocated bytes for that completed frame, optional
   main-thread ns. Align all fields to the SAME completed frame. No per-frame
   JSON, file writes, LINQ, object search or forced GC. Record invalid/empty
   recorder samples as unavailable, never as zero. Detect buffer overflow/drops.
5. Write `frames.csv` and `run.json` after measurement. `plan` creates the exact
   manifest template; the synthetic fixture demonstrates all populated fields.
   Preserve requested AND observed runtime settings per window; restore any
   temporarily changed settings in finally. No global machine changes.
6. Attach completed strict14 JSONL as `endurance.jsonl`, plus build/source provenance
   in `run.json.endurance`. It may come from the existing run on the same game
   source head; separate instrumented artifact hashes are expected. Do not repeat
   14 races just to collect three performance windows. An interrupted/old-head
   strict14 cannot be promoted into current-head completion evidence.
7. Attach three independently completed Esc checks as `escapes.jsonl` (selection,
   intro-before-GO, driving-after-GO). Each is an additional probe outside measured windows;
   do not count an aborted race among strict14's 14 complete races.

## Frame CSV

Exact header:

```text
repeat,frame,elapsed_seconds,frame_ms,gc_allocated_bytes,main_thread_ns
```

`frame` is the completed `Time.frameCount` index, strictly contiguous within each
window. `elapsed_seconds` starts after zero and is cumulative frame interval sum;
the final value equals `window.duration_seconds`. `frame_ms` is actual unscaled
frame interval, not CPU main-thread time. Blank GC/main cells mean unavailable.
Allocation zero is valid ONLY with available recorder and a fresh valid sample.
The supplied template uses `ProfilerRecorder` on a Development Player, starts
recorders before each driving warmup, and requires exactly one new nonwrapping
recorder sample per completed frame. Capacity saturation, stale values and missing
samples remain unavailable. A post-warmup GC preflight failure aborts the capture;
it does not synthesize values or proceed as qualified evidence. Record
counter availability and reason in the manifest. GC heap differences are NOT
allocated bytes. Use bytes, milliseconds, nanoseconds exactly as the header says.

## Manifest

Generate with `benchmark.py plan --out <private>/run.json`; top-level fields:

- `schema_version=1`, `profile=s1-practice-0ai`, `evidence_kind=real|synthetic`.
- `source`: full 40-digit head, `dirty` boolean, patch SHA256 if dirty (null if clean).
- `build`: `type=Release|Development`, Unity version, game version, backend,
  instrumented build artifact SHA256. Editor is rejected.
- `device`: non-identifying OS/CPU/GPU/API strings and RAM MiB; no account, device
  serial, machine name or absolute paths.
- `settings.requested` and `.actual`: width, height, quality (index), target_fps,
  vsync. `actual` is the first measured window's start snapshot, never Finish/Home.
  The current template adds `snapshot_policy=measured-window-v2` and
  `post_cleanup_actual` (Home/cleanup receipt; excluded from baseline comparison).
  Each window records `actual`, `actual_end`, and `settings_change_count=0` backed
  by per-sampled-update checks. Requested/start/end differences and in-window
  changes fail validation. Missing new-policy boundary evidence also fails.
  Legacy manifests remain readable without the policy; their settings checks are
  unchanged, so the old Home-overwritten evidence is still rejected.
- `workload`: seed, seed_applied, route ID/hash, controller ID/hash, input_method,
  chosen loadout IDs and skin ID, ai_count=0, physical_driving=true, teleports=0,
  synthetic_finishes=0, time_scale=1, laps_to_finish=3. Hash controller with its parameter set; use
  fixed selection identities rather than silently selecting new random options.
- `sampling`: warmup_seconds, measure_seconds, repeats, gc_counter and main_counter
  availability/reason; `policy` fixes the versioned defaults to 30/60/3.
  For the new snapshot policy, `gc_counter` also records `method=ProfilerRecorder`,
  `name=GC Allocated In Frame`, `unit=bytes`, and
  `sample_policy=nonwrapping-count-advance-v2`. Availability means at least one
  measured sample exists; partial captures preserve blanks and cannot pass.
  This counter requires a Development capture; Release values cannot be relabeled.
  Runtime receipts also retain observed/missing sample counts for both counters.
- `windows`: repeat, seed, start_seconds/end_seconds (monotonic Player clock),
  warmup_actual_seconds (driving-only), frames, duration_seconds, dropped_frames,
  actual settings, input_sha256 (observed ordered tick/key stream), scene, route_id.
  Also record a unique `race_id` per repeat, `stage=driving`,
  `driving_start_seconds` (movement unlocked), `driving_end_seconds` (last observed
  continuously driving time), and zero `forced_gc_count`, `scene_change_count`,
  `ai_count`. Window start minus driving start equals actual warmup; window end
  must lie inside that interval. Repeat IDs must refer to different physical races.
- `endurance`: source head, build SHA256, operator SHA256 and JSONL SHA256. Evidence
  from another build is labeled separately and must share the measured game head.
- `escapes`: source head, build SHA256 and JSONL SHA256.
- `frames_sha256`, `complete`, `error_count` (whole performance capture),
  `budgets` (see generated template; null means undecided, NEVER automatic pass).

## Existing strict14 JSONL retained unchanged

The validator uses `plan`, `mainIndex`, `raceSerial`, `extra`, `realtime`, `kind`,
`note` and the existing checkpoint/timing/summary fields. Expected serials:
M1 M2 M3 E3 M4 M5 M6 E6 M7 M8 M9 E9 M10 E10.
Per race: selection-ready -> GO-first-observed -> movement-unlocked ->
natural-finish-timing -> results-interactive -> results-after-hold -> race-completed
-> real button (Rematch for main, ReturnHome for extra). Clean Home follows extras.
Timing arrays retain polling observation residuals and their uncertainty; they do
not establish exact-trigger precision. The report separates lap-total consistency
from `timing_precision=NOT_MEASURED`; this is not a full V3 precision certificate.
The source schema's irrelevant zero-initialized fields are ignored by kind, not
treated as measured values. No raw note/stack/position/username is copied to reports.

## Esc JSONL additions (three rows)

```json
{"stage":"selection","escape_pressed_seconds":1.0,"dialog_seconds":1.1,"confirm_seconds":1.2,"home_seconds":2.0,"reopened_seconds":3.0,"input_blocked":true,"dialog_visible":true,"confirm_button":"ConfirmQuit","home_clean":true,"buddahs":0,"reporters":0,"client_started":false,"server_started":false,"clock_cleared":true,"timing_cleared":true,"reopened":true,"error_count":0}
```

Repeat with stage `intro` (before GO) and `driving` (after GO). Results use ReturnHome, not Esc. Observe actual Esc/dialog/input block,
click the normal confirmation button, wait for clean Home, then reopen Practice.
Do not fabricate receipts from configuration or a successful summary alone.

## Limits

This Python package verifies evidence consistency, not the authenticity of arbitrary
files. Hashes bind artifacts against accidental mixing; the coordinator retains raw
logs and audited driver/build provenance privately. Prior non-Development samples
contained no GC observations; see README for the Development recapture procedure
and explicit limitations. Pure regression tests and compilation cannot establish
native recorder availability. Fresh final-code runtime measurement remains with
the sole coordinator. Missing measurements or undecided budgets remain
`NOT_PASSED`; no placeholder becomes a performance result.
