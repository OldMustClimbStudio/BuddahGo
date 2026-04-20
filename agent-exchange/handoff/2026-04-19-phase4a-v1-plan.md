# Phase 4a V1 — Locomotion Step Cut-over Plan + Test Gates

Date: 2026-04-19
Branch: `feat/phase4a-locomotion-step-cutover` (branched from `refactor/prediction-v2` at tip `d049520`)
Scope: Z2 hybrid per audit Addendum A.9/A.10 — migrate `CommandedForwardForce` + `CommandedTurnTorque` computation to `BuddahLocomotionStep.Compute`; retain decay branch inline.
Status: V1 code landed; **awaiting single-peer D-LOC parity check → 2-peer V5 gate → closeout + PR**.

---

## §1 What this commit lands

### Files changed

- `Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs` (+44 / -7)
  - NEW `public static void Compute(forwardDirection, resolvedThrottle, resolvedSteering, computedStats, out commandedForwardForce, out commandedTurnTorque)` — pure-static, zero Unity API beyond Vector3 math + Mathf.Abs. Bit-identical output on identical inputs under single-threaded C# / Mono / IL2CPP.
  - `Run(in ctx, in input, ref scratch)` body delegates to `Compute` for the commanded-value math; clamp-mirror block + scratch writes preserved.
  - Class-level comment updated to document Compute contract + decay-branch deferral.

- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` (+17 / -5)
  - `using NewBuddah.PredictionV2.Simulation;` hoisted out of `#if BUDDAH_PREDICTION_SHADOW` (real path now references `BuddahLocomotionStep.Compute`).
  - Inline locomotion block restructured:
    - `IsSteeringSuppressed → resolvedSteering = 0f` moved BEFORE the Compute call (behavior-neutral: forward force does not depend on steering).
    - `Vector3 forwardForce = ...` inline expression + `float turnTorque = ...` inline expression replaced with a single `BuddahLocomotionStep.Compute(...)` call producing both out params.
    - `_predictionRigidbody.AddForce(forwardForce, Force)` + active-steering `_predictionRigidbody.AddTorque(Vector3.up * turnTorque, Force)` apply sites unchanged — they just consume Compute's out params now.
    - `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW` captures (`_realScratch.CommandedForwardForce` + `_realScratch.LocomotionRan` + `_realScratch.CommandedTurnTorque`) preserved at same logical points.
  - Decay branch (`else if (config.TurnDecayPerSecond > 0f) { ... AngularVelocity(...) }`) **UNCHANGED** per Z2 hybrid scope.
  - Shadow step call (`BuddahLocomotionStep.Run(in tickCtx, in data, ref _shadowScratch);`) inside shadow gate **UNCHANGED** — parity-by-construction compare harness continues.

### Files NOT changed

- `Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs` — no new fields; step's `Run` still reads existing ctx.ForwardDirection / ctx.ResolvedThrottle / ctx.ResolvedSteering / ctx.ComputedStats.
- `Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs` — no new fields; scratch still carries the same commanded-value slots.
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs` — no reconcile shape change.
- Every other file in the prediction stack.

## §2 Z2 scope boundary re-assertion

Per audit §A.10 (reviewer-bound, 2026-04-19):

| Element | V1 disposition | Site |
|---------|----------------|------|
| `CommandedForwardForce` computation | **Migrated to BuddahLocomotionStep.Compute** | motor.cs → step |
| `_predictionRigidbody.AddForce(forwardForce, Force)` apply | **Retained in motor** | motor.cs unchanged |
| `CommandedTurnTorque` computation (active-steering branch) | **Migrated to BuddahLocomotionStep.Compute** | motor.cs → step |
| `_predictionRigidbody.AddTorque(Vector3.up * turnTorque, Force)` apply (active branch) | **Retained in motor** | motor.cs unchanged |
| Decay branch entire block (`!hasActiveSteering && config.TurnDecayPerSecond > 0f`) | **Retained in motor inline, unchanged** | motor.cs:488-493 |
| `IsSteeringSuppressed` zero-out gate | Retained in motor, **moved BEFORE Compute** (behavior-neutral) | motor.cs:458-459 |
| Shadow step call (`BuddahLocomotionStep.Run` on `_shadowScratch`) | **Retained** — parity-by-construction compare | motor.cs inside shadow gate |

## §3 Behavior-neutrality argument (for reviewer audit)

### Claim

The Z2 migration produces **bit-identical** observable outputs (rb AddForce value, rb AddTorque value, rb angular-velocity writes, reconcile data, shadow scratch) under identical per-tick inputs, for all input combinations.

### Proof sketch

For each of the three control paths through the inline locomotion block:

**Path 1: Active steering (`IsSteeringSuppressed=false`, `|resolvedSteering|>0.001f`)**
- Pre-4a: `forwardForce = forwardDirection * (FinalForwardForce * resolvedThrottle)`; `turnTorque = resolvedSteering * FinalTurnTorque`; AddForce(forwardForce); AddTorque(Vector3.up * turnTorque).
- Post-4a: `Compute(forwardDirection, resolvedThrottle, resolvedSteering, _computedStats, out forwardForce, out turnTorque)` → Compute body: `commandedForwardForce = forwardDirection * (FinalForwardForce * resolvedThrottle)` ✓; `commandedTurnTorque = |steering|>0.001 ? resolvedSteering * FinalTurnTorque : 0f` = `resolvedSteering * FinalTurnTorque` ✓ (gate true). Motor: AddForce(forwardForce); AddTorque(Vector3.up * turnTorque). **Identical AddForce + AddTorque inputs.**

**Path 2: Steering suppressed (`IsSteeringSuppressed=true`)**
- Pre-4a: forwardForce computed (unchanged by suppression), AddForce fires; then `resolvedSteering = 0f` → `|resolvedSteering|>0.001f` is FALSE → decay branch (if `config.TurnDecayPerSecond > 0f`) runs with direct `AngularVelocity(...)` write.
- Post-4a: `resolvedSteering = 0f` FIRST → Compute returns `turnTorque = 0f` (since `|0|>0.001f` false); `forwardForce` unchanged by this swap (doesn't depend on steering); AddForce fires with identical forwardForce. `|resolvedSteering|>0.001f` still FALSE → decay branch runs **UNCHANGED** with identical `AngularVelocity(...)` write. **Forward force identical; decay angular-velocity write identical.**

**Path 3: Decay-skipped (`!IsSteeringSuppressed`, `|resolvedSteering|<=0.001f`, `config.TurnDecayPerSecond == 0f`)**
- Pre-4a: forwardForce computed, AddForce fires; active branch skipped; decay branch condition false → no torque / no angular-velocity write.
- Post-4a: Compute produces forwardForce (same) + turnTorque=0f (unused since active branch skipped); AddForce fires identically; active branch skipped (same condition); decay branch skipped (same condition). **No torque, no AngularVelocity write.**

### Ordering-swap risk analysis (IsSteeringSuppressed before vs after)

The only reordering is moving `if (IsSteeringSuppressed) resolvedSteering = 0f;` from post-forward-force to pre-forward-force. `forwardForce = forwardDirection * (FinalForwardForce * resolvedThrottle)` — no steering dependency. The swap is observable only if `resolvedSteering` is read between the old position (post-forwardForce-computation) and the new position (pre-forwardForce-computation) — inspection confirms no reads in that window. Safe.

### Compiler-level risk analysis

The replacement of inline expressions with a static method call introduces a function-call frame. Unity 2022.3 Mono JIT inlines trivial static methods aggressively. Even without inlining, the overhead is a single `call` + register spill — well within V13's ±10% tolerance and invisible at 60Hz tick budget. `ProfilerMarker`-free path (since V13 rep-*-ms was deferred to M2 per digest §3 Decision 1).

## §4 Test gates (before PR open)

Per reviewer baseline digest §4:

### Gate 1: V1 single-peer D-LOC parity (REQUIRED, running first)

- Enter PlayMode on `RaceMap` as HOST-only (1 peer, 1 Buddah).
- Shadow define already ON by default (`BUDDAH_PREDICTION_SHADOW`).
- Drive 30-60s with varied input (accel, cruise, steering, release-to-decay).
- Expected: **every `[D-LOC HEARTBEAT]` row reports `loc-div=0 imp-div=0 tel-div=0 mod-div=0 hof-div=0`**. Zero non-heartbeat `[D-LOC]`, zero `[D-LOC FATAL]`.
- Save digest to `agent-exchange/console/2026-04-19-phase4a-v1.log` (following Phase 3c/3d naming).

**PASS criterion**: Phase 3d V2 parity pattern preserved — locomotion step is the authority AND its output matches shadow-step output by construction. Failure here means the migration introduced a computational divergence (should be impossible given the diff, but this is the trust-but-verify step).

### Gate 2: V5 2-peer D-LOC + V3 + V13 delta (REQUIRED, after Gate 1)

- HOST Build + CLIENT Editor, Phase 3d V5 shape.
- Defines enabled on BOTH peers: `BUDDAH_PREDICTION_VISUAL_PROBE`, `BUDDAH_PREDICTION_PERF_PROBE`, `BUDDAH_PREDICTION_SHADOW`.
- 90s session, 2 Buddahs, normal locomotion (no skills).

**D-LOC parity (per-peer)**:
- All div counters = 0 on every row.
- `mod-compared / active-ticks` ratio matches Phase 3d baseline (HOST ~1:1, CLIENT reconcile-replay multiple of 1:1).
- 0 non-heartbeat `[D-LOC]`, 0 FATAL.

**V3 delta (vs `2026-04-19-phase4-baseline-v3-host.log`)**:
- Primary: `|pos-davg-mean(4a) - pos-davg-mean(baseline)|` within **±10%** on HOST owner=True AND owner=False streams.
- Secondary: `|rot-davg-mean(4a) - rot-davg-mean(baseline)|` within **±10%**.
- Outliers ≥5m/frame excluded from comparison (intro-handoff single-frame snaps per audit Addendum C.4 + baseline digest anomaly 1).
- CLIENT inflation (2-3× vs HOST per baseline digest §2) accepted as Editor-overhead artifact.

**V13 delta (vs `2026-04-19-phase4-baseline-v13-host.log`)**:
- GC.Alloc only for 4a (rep-*-ms deferred to M2 micro-PR).
- `|gc-alloc-avg-b-mean(4a) - gc-alloc-avg-b-mean(baseline)|` within **±10%** on HOST.
- First-heartbeat outlier excluded (one-shot scene-load spike).

Digests: `2026-04-19-phase4a-v5-host.log` + `2026-04-19-phase4a-v5-client.log`.

### Gate 3: PR open (only after Gate 2 passes)

- `gh`-CLI unavailable locally; open manually via push URL.
- Target: `refactor/prediction-v2`.
- Title: `feat(prediction): phase 4a z2 locomotion step cut-over`
- Body: link this plan + the Gate 1 + Gate 2 digests + audit Addendum A.9/A.10.

## §5 Rollback strategy

If Gate 1 fails (unlikely given behavior-neutrality argument in §3):

1. Re-read the motor diff for any typo or reorder bug.
2. If the diff is correct and the test still fails, the issue is probably in the Compute body vs inline expression drift — compare character-by-character against the pre-4a inline math.
3. If still unresolved, revert the motor.cs edit (keep the step's Compute method since it's harmless), investigate whether Phase 3a shadow mismatch was already latent but hidden.

If Gate 2 fails specifically on V3 or V13 delta but Gate 1 passes:

- V3: confirm baseline was captured under same scene + input pattern. Re-run post-4a with tighter input-pattern match. The +10% tolerance is generous; a fail likely means real visual-root movement change, not measurement noise.
- V13 gc-alloc: Compute's out-param overhead is ~0 in bytes; any gc delta is suspicious. Check for accidental boxing or string allocation.

## §6 Carry-over

- Phase 8 cleanup-queue Entry 3 (decay-branch migration) explicitly retained: this 4a does NOT close Entry 3; Entry 3 remains contingent on Phase 3a shadow scope extension per Addendum A.6 Option Z1.
- Phase 8 Entry 7 (V13 rep-*-ms Stopwatch restoration via M2 micro-PR) unchanged — scheduled post-4a-merge, pre-4b-start.
- Phase 4b (impulse + CombatAdapter + L7 fix + V6 FishNet LatencySimulator) unchanged — blocked on 4a merge + M2 micro-PR.

## §7 Sign-off

V1 code landed. Awaiting user-driven single-peer Gate 1 + 2-peer Gate 2 runs. Closeout packet will be written after Gate 2 passes.

No PR opened. Branch `feat/phase4a-locomotion-step-cutover` pushed to origin for reviewer pre-review; merge waits until Gate 2 digest lands.
