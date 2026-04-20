# Phase 4 Baseline — Combined Decision Digest

## §1 Coverage

| Side | Log source | Scrape status | Duration |
|---|---|---|---|
| CLIENT (Editor) | `Editor.log` line 144908+ | `2026-04-19-phase4-baseline-v3-client.log` + `2026-04-19-phase4-baseline-v13-client.log` | ~203s V13 / ~121s V3 |
| HOST (Dev Build) | `Player.log` → raw/ | `2026-04-19-phase4-baseline-v3-host.log` + `2026-04-19-phase4-baseline-v13-host.log` | ~720s V13 / ~515s V3 |

Option B (2-peer 2-Buddah, 90s minimum) **satisfied**. Both ends captured from the same 2-peer session.

## §2 Verdict summary

- **V3 (visual shake)**: captured cleanly on both ends. Primary metric = `pos-davg-mean`. `dp99` degenerate (equals dmax at window=60). HOST is the cleaner reference (symmetric across owner streams); CLIENT Editor inflates deltas ~2.5×.
- **V13 (perf)**: GC.Alloc captured cleanly on both ends. **rep-*-ms INVALID on both ends** — custom ProfilerMarker needs active Profiler sampling, Development Build alone does not satisfy this.
- **Shadow parity (D-LOC)**: preserved on both ends. 0 FATAL, all divs=0, counter progression matches Phase 3d V5 pattern. G1 observational-neutrality runtime-confirmed.

## §3 Decisions

### Decision 1: V13 rep-*-ms capture method → **M2 (Stopwatch)**

HOST Dev Build emitting `rep-*-ms = 0.0000` invalidates Claude Code's M3 option ("HOST Build bypasses Editor gotcha"). The real choice is M1 vs M2.

**Reviewer decision: M2 — switch to `System.Diagnostics.Stopwatch`**.

Rationale:
- M1 requires turning Profiler recording ON every session on both peers (Editor Profiler Record button + Build Autoconnect Profiler). Human footgun + harder-to-reproduce numbers (Profiler overhead inflates measurements; tolerance drift across sessions).
- M2 is a one-time ~30-line PR behind the same `#if BUDDAH_PREDICTION_PERF_PROBE` gate. Works identically in Editor, Development Build, and Release Build. Independent of Unity's Profiler state.
- M2 preserves Reviewer G1 (OFF-state binary byte-identical) because Stopwatch calls collapse with the define.
- Post-4a delta analysis becomes deterministic — no "did someone forget to press Record" uncertainty.

**Scope of M2 micro-PR** (to be handed to Claude Code AFTER 4a V1 lands):
- Remove the `ProfilerMarker s_runInputsMarker` + `using var markerScope = s_runInputsMarker.Auto();` in `BuddahPredictedMotor.cs` (lines 69-75 + 329-331).
- Replace with a static `long[] _runInputsTicksRing` + index rotation, measured via `Stopwatch.GetTimestamp()` before/after the RunInputs body (or the ShouldRunPrediction early-return path).
- Expose ring read-only to `BuddahPredictionPerfProbe` via a static accessor; probe converts ticks → ms via `1000.0 / Stopwatch.Frequency`.
- All behind `#if BUDDAH_PREDICTION_PERF_PROBE`.
- Remove the `Unity.Profiling` using from the motor when define off (already guarded — no change needed).

**Timing**: M2 is NOT blocking Phase 4a V1. Proceed with 4a using GC.Alloc-only V13 gate for now; add rep-*-ms metric back post-4a-merge when M2 lands.

### Decision 2: V3 `_heartbeatFrames` tuning → **accept pos-davg-mean as primary comparator; no re-capture**

`dp99 ≡ dmax` at window=60 (floor(60*0.99)=59=max index) is a known design limitation. Options considered:
- Bump to 120 → p99 at index 118 (2 below max), becomes distinct. But requires code change + baseline re-capture.
- Bump to 500 → p99 at index 494, much more meaningful. Bigger window → heartbeat cadence drops to every ~8s.
- Accept pos-davg-mean as primary, dp99 as secondary.

**Reviewer decision: accept pos-davg-mean as primary V3 gate**. Current baseline is sufficient for 4a delta comparison.

Post-4a evaluation rule:
- **PASS if** `|pos-davg-mean(4a) − pos-davg-mean(baseline)|` is within ±10% on both owner streams of the **HOST** side (cleaner reference).
- **SECONDARY**: `|rot-davg-mean(4a) − rot-davg-mean(baseline)|` within ±10%.
- **IGNORE**: `pos-dmax` single-frame intro-handoff outliers — exclude frame deltas ≥ 5m from the delta comparison OR document them as baseline noise.
- CLIENT side is secondary integrity check (expect 2-3× inflation vs HOST due to Editor overhead).

If dp99 becomes interesting in a later phase (e.g., Phase 6 teleport or Phase 7 visual layer regression chasing), bump `_heartbeatFrames` to 120 in a follow-up micro-PR — not blocking now.

### Decision 3: HOST baseline scrape → **COMPLETE**

HOST V3 + V13 digests written as peer files to CLIENT digests. No separate branch required for baseline data.

### Decision 4: Digest commit location → **commit to `refactor/prediction-v2` directly**

Options Claude Code proposed:
- (a) `feat/phase4-probes-wiring` — already merged, moot.
- (b) baseline-data branch — overkill for 5 digest files.
- (c) hold until HOST files land — HOST files now landed.

**Reviewer decision: direct commit to `refactor/prediction-v2`** under `agent-exchange/console/` alongside all prior phase digests (phase3a through phase3d already live there). Commit message: `docs(agent-exchange): phase 4 baseline digests (CLIENT + HOST, V3 + V13)`.

Files to commit:
- `agent-exchange/console/2026-04-19-phase4-baseline-v3-client.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-client.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-v3-host.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-host.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-digest.md` (this file)
- `agent-exchange/console/raw/2026-04-19-phase4-baseline-host.log` (~41.5 MB — keep for traceability; large but consistent with phase3d-v5 raw at 70 MB)

If 41.5 MB HOST raw exceeds git-lfs threshold or comfort level, alternative: keep raw locally, commit only the digests. Reviewer is fine with either.

## §4 Phase 4a V1 green light

**Green-lit**. Proceed with Phase 4a V1 implementation per Addendum A.9 (Z2 Hybrid):

- Migrate `CommandedForwardForce` and `CommandedTurnTorque` to `BuddahLocomotionStep`.
- **Retain** decay branch inline at `BuddahPredictedMotor.cs:459-463` (not migrated in 4a — documented in Phase 8 Entry 3 for later).
- V13 rep-*-ms gate: **GC.Alloc only** for 4a post-delta (rep-*-ms deferred until M2 micro-PR lands).
- V3 gate: pos-davg-mean within ±10% on HOST owner=True and owner=False streams; outliers (≥5m/frame) excluded.
- Shadow parity: D-LOC harness continues (same gates as Phase 3d).

Sequence:
1. Phase 4a V1 single-peer run → D-LOC parity check (same as Phase 3b/3c/3d pattern).
2. If V1 clean: Phase 4a V5 2-peer run → combined parity + V3/V13 delta vs this baseline.
3. If V5 clean: Phase 4a PR → merge → M2 micro-PR → Phase 4b (impulse + CombatAdapter + L7 fix, inverted shadow 4b with V6 FishNet LatencySimulator 100ms RTT).

## §5 Outstanding items carried forward

- **Phase 8 cleanup queue Entry 7 (new)**: V13 rep-*-ms restoration via Stopwatch (M2 micro-PR). Scheduled for post-Phase-4a-merge, pre-Phase-4b-start.
- Phase 8 Entry 5 (multi-spawn stress for 4/8-Buddah perf) unchanged.
- Phase 8 Entries 1-4 + 6 unchanged.

## §6 Sign-off

Baseline pinned. V3 + V13 + D-LOC digests linked. Phase 4a V1 authorized.
