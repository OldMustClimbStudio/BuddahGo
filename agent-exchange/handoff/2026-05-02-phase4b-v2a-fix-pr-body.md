# phase4b-v2a-fix: relocate inverted-shadow drain to PostTick (L16)

**Base:** `dev`
**Branch:** `fix/phase4b-v2a-postick-drain`

## Summary

V2a's observation gate was structurally blind on non-server peers because `ConsumePendingImpulseEvents_InvertedShadow` was called inside `RunInputs` (the `[Replicate]` callback). FishNet replays `RunInputs` N times per tick during reconcile; replay 1 drained the single-consumer FIFO and replays 2..N saw an empty channel + per-replay scratch reset, leaving `_legacyShadowScratch.ImpulseRan = false` at PostTick compare time. The 2-peer LAN CLIENT smoke showed `inv-imp-compared = 0` across 73 heartbeats despite 2 `[CommandBus]:Recv ch=Impulse` confirming RPC delivery and OLD-side `imp-compared = 2` confirming the impulse landed.

This PR moves the drain from `RunInputs` to `TimeManager_OnPostTick`, immediately before `Shadow_CompareAndReport`. PostTick fires exactly once per tick post-replay, so the FIFO drains once and the result survives to the compare.

## Scope contract — observation only, NOT replay safety

**V2a observation gate validated at PostTick granularity. RunInputs-level replay safety is V2b authority-flip prerequisite, NOT solved by this patch.**

PostTick relocation makes V2a's diagnostic counter (`inv-imp-compared`) reflect reality. It does NOT make the bus channel safe for production-path consumers. When V2b promotes the NEW path to rb writer, the channel itself must adopt OLD's `EventTick` + `ConsumeReady` tick-stamped model, OR the drain must move to a different replay-safe lifecycle hook. See `Docs/lessons-log.md` L16 (FIFO blindness) and L17 (phase-skew rule) for the rules.

## Files changed

- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs`
  - Remove drain call from `RunInputs` (was lines 429-434 on dev tip).
  - Add drain call inside `TimeManager_OnPostTick` under the `(IsOwner || IsServerInitialized)` gate, immediately before `Shadow_CompareAndReport`.
  - Per-tick reset of `_legacyShadowScratch = default` in `RunInputs` (~line 388-394) **preserved** — symmetry with `_realScratch` / `_shadowScratch` resets, OLD's tick-stamped queue model unchanged.
  - Update `ConsumePendingImpulseEvents_InvertedShadow` docstring with L16 lifecycle note pointing forward to V2b prerequisites.
- `Docs/lessons-log.md`
  - Add **L16** (FIFO + reconcile blindness) — committed in initial fix commit.
  - Add **L17** (phase-skew rule) — committed in follow-up post-Path-B-verification commit.

## Compile gates

- Only 1 callsite of `ConsumePendingImpulseEvents_InvertedShadow` remains (`grep -n` confirms motor.cs:258 PostTick + motor.cs:1944 method definition; old motor.cs:433 RunInputs callsite removed).
- `#if BUDDAH_PREDICTION_LEGACY_SHADOW` guard preserved at both the call and reset sites.
- No public API change. No new fields. No serialization impact.

## Re-test gate (Path A + Path B verified)

### Path A — host-only Editor, 30s smoke (3 pushes)

| Metric | Value | Gate |
|---|---|---|
| `inv-imp-compared` (final) | 3 | ≥ 1 ✅ |
| `inv-imp-div` (final HB) | 0 | = 0 ✅ |
| `[D-IMP INV FATAL]` | 0 | = 0 ✅ |
| `[D-LOC FATAL]` | 0 | = 0 ✅ |
| `[CommandBus]:ClearAll` | 4 | = 4 per spawn ✅ |
| OLD `imp-compared` | 3 | matches INV ✅ |

Path A digest: [agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathA-host.log](agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathA-host.log)

### Path B — 2-peer LAN, 60s smoke — bidirectional phase-skew block

The strict `FATAL = 0` gate is **replaced** by bidirectional phase-skew analysis. Both peers showed mirror-image FATAL patterns confirming observation-phase mismatch, NOT real divergence.

#### CLIENT (local Editor)

| Metric | Value | Status |
|---|---|---|
| `inv-imp-compared` (final) | **6** | ✅ matches `[CommandBus]:Recv = 6` |
| OLD-side `imp-compared` (final) | **6** | ✅ matches NEW |
| `inv-imp-div` (final HB) | 0 | ✅ |
| `[D-LOC FATAL]` | 0 | ✅ |
| `[CommandBus]:ClearAll` | 8 | ✅ (4 × 2 spawns) |
| `[CommandBus]:FirstInvoke` | 1 | ✅ |
| Forward FATAL count (`ranOld=False ranNew=True`) | 5 | (phase-skew) |
| Reverse FATAL count (`ranOld=True ranNew=False`) | 0 | — |

#### HOST (build)

| Metric | Value | Status |
|---|---|---|
| `inv-imp-compared` (final) | **7** | ✅ |
| OLD-side `imp-compared` (final) | **7** | ✅ matches NEW |
| `inv-imp-div` (final HB) | 0 | ✅ |
| `[D-LOC FATAL]` | 0 | ✅ |
| `[CommandBus]:ClearAll` | 8 | ✅ (4 × 2 spawns) |
| `[CommandBus]:Recv` | 0 | ✅ (host is RPC sender, local-enqueue branch) |
| Forward FATAL count (`ranOld=False ranNew=True`) | 0 | — |
| Reverse FATAL count (`ranOld=True ranNew=False`) | 6 | (phase-skew) |

#### Bidirectional symmetry verdict

- **Total events match Recv on each side**: CLIENT compared = OLD imp-compared = Recv = 6. HOST compared = OLD imp-compared = 7.
- **`inv-imp-div = 0` final HB on both sides** — no per-window divergence accumulation.
- **`D-LOC FATAL = 0` on both sides** — production-criticality OLD shadow gate fully clean.
- **FATAL pattern is mirror-image**: CLIENT 5 forward + HOST 6 reverse, no same-direction asymmetry on either side.

This is the phase-skew fingerprint: same event observed by both paths but on different lifecycle phases (OLD drains in `RunInputs` forward pass; NEW drains in `PostTick` per L16 fix). When an RPC arrives between these phases, the side whose phase has already passed misses that single tick; the next tick captures it. No volume divergence — game behavior unaffected because OLD path remains rb-writing authority.

**V2b authority-flip prerequisite (NON-NEGOTIABLE):** before promoting NEW path to rb writer, V2b STEP 0 is to tick-stamp `BuddahPredictionEventChannel<T>.Entry` with `EventTick`, port OLD's `ConsumeReady(currentTick, ...)` semantics into `BuddahPredictionCommandBus`, and re-verify Path A + Path B with strict `FATAL = 0` gate. The phase-skew is harmless under V2a observation but would be a real desync under V2b authority because each peer's rb would be written on a different tick.

Path B digest: [agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathB.log](agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathB.log)

## V3 unblock condition

**ALL THREE required:**
1. Re-test gate above PASS with bidirectional phase-skew documented — ✅ (Path A clean, Path B bidirectional symmetry confirmed).
2. L16 + L17 logged (`Docs/lessons-log.md`) — ✅ committed in this PR.
3. PR merged to `dev`.

Then V3 prep is authorized: 4-skill-site cut-over (PushHitbox, ChargedHandProjectileRuntime, HandPushProjectileRuntime, BuddahPredictionImpulseDebugBox) from `BuddahPredictionCombatRouting` to `BuddahPredictionCombatAdapter`.

## Side observation: subjective visual jitter perception

User reports that visual jitter feels smaller post-fix compared to V2a pre-fix
test session. This fix is observation-layer only (drain phase relocation, no rb
writes); theoretically should not affect visual behavior.

Possible explanations (no decision made):
- Network condition variance between sessions (uncontrolled variable)
- Cognitive bias from knowing the bug existed
- Marginal CPU savings from reduced TryDequeue calls during reconcile replay
- Unidentified side effect via channel state read elsewhere in codebase

**Action**: NOT investigated in this PR. Recorded for Phase 7 visual layer
investigation. Re-evaluate after V2b tick-stamp lands — if jitter perception
remains stable or improves further with no rb-affecting changes, the original
jitter root cause may be CPU-budget-related rather than visual-root-tracking.

## Out of scope

- **Visual jitter** (`[D-VIS HEARTBEAT] pos-dmax` 8-9m repeats observed in pre-fix CLIENT log). Phase 7 territory; separate investigation after this PR merges. See side observation above.
- **Channel tick-stamping** (V2b STEP 0 prerequisite, not addressed here — observation-only fix).
- **Strict FATAL=0 gate restoration** (achievable only after channel tick-stamping; tracked under V2b STEP 0).

## Reference
- L16 (FIFO + reconcile blindness): `Docs/lessons-log.md`
- L17 (phase-skew rule): `Docs/lessons-log.md`
- Pre-fix V2a digests showing the structural blindness:
  - Path A pre-fix pass: `agent-exchange/console/2026-04-19-phase4b-v2a-host.log`
  - Path B pre-fix false-pass (the bug): `agent-exchange/console/2026-04-19-phase4b-v2a-client.log`
- Post-fix verification:
  - Path A: `agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathA-host.log`
  - Path B: `agent-exchange/console/2026-05-02-phase4b-v2a-fix-pathB.log`
