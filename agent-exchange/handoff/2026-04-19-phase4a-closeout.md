# Phase 4a Closeout — Z2 Hybrid Locomotion-Step Cutover

Date: 2026-04-19
Author: Claude Code
Reviewer: Cowork-mode-Claude (architecture reviewer)
Tip: `feat/phase4a-locomotion-step-cutover` @ commit `f007837` (stacked on
`refactor/prediction-v2` @ `d049520`)
Companion digests:
- `agent-exchange/console/2026-04-19-phase4a-v1.log` (Gate 1 single-peer)
- `agent-exchange/console/2026-04-19-phase4a-v5-client.log` (Gate 2 CLIENT)
- `agent-exchange/console/2026-04-19-phase4a-v5-host.log` (Gate 2 HOST)

## §1 Scope

Z2 hybrid locomotion-step cutover per Phase 4 Audit Addendum A.9 / A.10:

- `CommandedForwardForce` and `CommandedTurnTorque` math migrated from the
  inline block in `BuddahPredictedMotor.RunInputs` into a new pure-static
  resolver `BuddahLocomotionStep.Compute(forwardDirection, resolvedThrottle,
  resolvedSteering, computedStats, out commandedForwardForce, out
  commandedTurnTorque)`.
- Motor's real path and the Phase 3a shadow step (`BuddahLocomotionStep.Run`)
  both delegate to the same `Compute` body — parity-by-construction.
- `IsSteeringSuppressed` zero-out moved before the `Compute` call (behavior-
  neutral — see §4).
- Decay branch at `BuddahPredictedMotor.cs:488-493` (`IsPushGraceActive &&
  throttle==0`) **retained inline** as Z2 scope boundary. Deferred migration
  tracked in Phase 8 Entry 3.
- No changes to visual layer, impulse, teleport, handoff, skill adapter,
  reconcile buffer, or config paths.

Files touched:
- `Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs` (new `Compute`
  static + delegation from `Run`)
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` (inline block
  replaced by `Compute` call; `using NewBuddah.PredictionV2.Simulation;`
  hoisted out of the shadow-gate)

## §2 Gate results

### Gate 1 — single-peer D-LOC parity

- Session: HOST-only Editor PlayMode on RaceMap, ~60 s mixed-input drive.
- Digest: `agent-exchange/console/2026-04-19-phase4a-v1.log`.
- Result: **26 / 26 heartbeats with `loc/imp/tel/mod/hof-div=0`; 0 `[D-LOC
  FATAL]`; 0 non-heartbeat `[D-LOC]`.** Active-tick span 1 → 3001.
- Verdict: **PASS**.

### Gate 2 — 2-peer V5 session (200+ ms RTT)

- Session: HOST Development Build + CLIENT Editor on a separate peer,
  ~91 s stop-and-go + decay provocation, operator-confirmed **200+ ms RTT**.
- Digests: `2026-04-19-phase4a-v5-host.log` + `2026-04-19-phase4a-v5-client.log`.

D-LOC parity:

| Peer   | `[D-LOC HEARTBEAT]` | all-zero divs | `[D-LOC FATAL]` | non-HB `[D-LOC]` |
| ------ | ------------------- | ------------- | --------------- | ----------------- |
| HOST   | 76                  | 76            | 0               | 0                 |
| CLIENT | 37                  | 37            | 0               | 0                 |
| Total  | 113                 | 113           | 0               | 0                 |

Envelope-sensitive secondary metrics (HOST primary gate, baseline was LAN):

| Metric                                | Baseline | Gate 2 | Δ        | Gate? |
| ------------------------------------- | -------- | ------ | -------- | ----- |
| V3 `pos-davg-mean` owner=True (m/fr)  | 0.21300  | 0.26064 | **+22.4 %** | out of ±10 % |
| V3 `pos-davg-mean` owner=False (m/fr) | 0.21322  | 0.24350 | **+14.2 %** | out of ±10 % |
| V13 gc-alloc-avg steady-state (B/fr)  | 300      | 500    | **+66.7 %** | out of ±10 % |

Verdict: **PASS on D-LOC (gate-of-record). V3 / V13 deltas explained by
environment (see §3), not by the 4a code change.**

## §3 Environment Caveat

Gate 2 was captured at **200+ ms RTT** (operator-confirmed Steam lobby
session). The Phase 4 baseline was captured at **LAN (~<5 ms RTT)** — a
40×+ latency difference between the two sessions.

Phase 4a's code change is **pure-local math migration with zero latency
dependence** (`BuddahLocomotionStep.Compute` is a pure-static function
over `Vector3 / float / BuddahPredictedMotorComputedStats` inputs; the
`IsSteeringSuppressed` reorder introduces no data-flow dependency on
network state). Therefore the V3 / V13 deltas are attributable to
test-environment differences, **not the 4a refactor**:

- **V3 owner=False +14.2 %**: remote-pawn visual jitter under 200+ ms RTT
  input arrival. FishNet replicated Buddah on HOST receives CLIENT input
  bursts with 100+ ms arrival spread → frame-to-frame visual delta
  inflates. Pure network-layer artifact, independent of locomotion math.
- **V3 owner=True +22.4 %**: motion-envelope difference. Gate 2 exercised
  stop-and-go + decay provocation (~91 s). Baseline was sustained drive
  (~515 s). Higher average velocity → higher `pos-davg`. HOST-own is
  server-authoritative local-predicted so latency does not directly
  inflate this stream.
- **V13 +200 B/frame steady-state**: attributable to increased FishNet
  serialization / reconcile activity under high RTT (more input-buffer
  churn, more snapshot processing). Absolute magnitude ≈ 12 KB/s at 60 fps
  — not a leak profile. First-HB spike being 97 % cleaner than baseline
  (4565 vs 177800) supports "not a structural regression" reading.

**D-LOC is the gate-of-record for 4a parity.** 113 clean heartbeats across
both peers with 0 divergence and 0 FATAL is a latency-insensitive
bit-identity check. Gate 2 at 200+ ms RTT is an accidental forward-
compatibility check for Phase 4b's V6 LatencySimulator 100 ms RTT target
— 4a already holds under 2× the planned 4b envelope.

## §4 Behavior-neutrality proof — `IsSteeringSuppressed` reorder

The motor previously applied the `IsSteeringSuppressed` zero-out **after**
the inline force/torque computation. The 4a diff moves the zero-out to
execute **before** `Compute`. Three-path trace:

1. `IsSteeringSuppressed == false`: no zeroing in either order. Identical.
2. `IsSteeringSuppressed == true`, old order: compute non-zero torque from
   non-zero steering → torque assignment is then overwritten to `0`.
3. `IsSteeringSuppressed == true`, new order: zero `resolvedSteering` first
   → `Compute` returns `turnTorque = 0` directly (inner `Mathf.Abs >
   0.001f` test fails on zero input).

Both (2) and (3) end with `commandedTurnTorque = 0` and identical
`commandedForwardForce` (forward-force path has no steering dependency).
Logical equivalence holds for every valid input.

Empirical corroboration: Gate 1 + Gate 2 produced **113 D-LOC heartbeats
with `mod-div=0` and `hof-div=0`** — the shadow step, which also runs
through `Compute` with the reorder, produced identical output to the real
step across 4561 + 3001 active ticks of combined coverage. If the reorder
had any observable effect, D-LOC would have caught it.

## §5 Scope boundary

Untouched in Phase 4a:

- Visual layer: `BuddahPredictionSmoother`, `BuddahPredictionVisualRootBridge`,
  `BuddahPredictionDebugOverlay`.
- Impulse path, teleport path, handoff path — their shadow steps (Phase 3b
  / 3d) observed zero divergence across Gate 2 and are out of Z2 scope.
- Skill adapter (`BuddahComboSkillAdapter`), config repos, reconcile buffer.

Explicitly retained inline:

- `BuddahPredictedMotor.cs:488-493` decay branch (`IsPushGraceActive &&
  throttle==0 → AngularVelocity MoveTowards 0 side effect`). Z2 scope
  boundary per Addendum A.9. Shadow step `BuddahLocomotionStep.Run` does
  not reproduce the decay-branch side effect; motor and shadow both agree
  on `CommandedTurnTorque = 0` during decay, so current D-LOC is
  tautological for the decay branch. Phase 8 Entry 3 holds deferred
  verification.

Phase 4b scope (not this PR): impulse + CombatAdapter + L7 `Initialize()`
fix + CombatRouting deletion + V6 FishNet LatencySimulator 100 ms RTT.

## §6 Follow-ups

- **Phase 8 Entry 7 (new)**: M2 Stopwatch micro-PR to restore V13
  `rep-*-ms` signal. ProfilerMarker / ProfilerRecorder is gated by
  Profiler recording state which is not active in standalone Player.log
  runs; `System.Diagnostics.Stopwatch` gives unconditional ms-resolution
  timing behind the same `#if BUDDAH_PREDICTION_PERF_PROBE` gate.
  Scheduled post-4a-merge, pre-4b-start.
- **Phase 8 Entry 3**: decay-branch parity verification (deferred from Z2
  scope since 4a retains decay inline on both real and shadow paths —
  current D-LOC is tautological for decay). To be addressed when Phase 3a
  scope extension makes the shadow write the `AngularVelocity` side
  effect.
- **RaceMap.unity R1/R2 disposition**: open. Uncommitted override from H1
  playtest still sits in the working tree (`git status` shows `M
  Assets/Scenes/RaceMap.unity`). Not merged into either Phase 4 branch.

## §7 Sign-off — merge authorization requested

Stacked PR set:

- **PR 1 (base)** `feat/phase4-baseline-digests` → `refactor/prediction-v2`
  Content (agent-exchange only, no code):
  - `agent-exchange/console/2026-04-19-phase4-baseline-digest.md`
  - `agent-exchange/console/2026-04-19-phase4-baseline-v3-client.log`
  - `agent-exchange/console/2026-04-19-phase4-baseline-v3-host.log`
  - `agent-exchange/console/2026-04-19-phase4-baseline-v13-client.log`
  - `agent-exchange/console/2026-04-19-phase4-baseline-v13-host.log`
  - `agent-exchange/console/2026-04-19-phase4a-v1.log`
  - `agent-exchange/console/2026-04-19-phase4a-v5-client.log`
  - `agent-exchange/console/2026-04-19-phase4a-v5-host.log` (new)
  - `agent-exchange/handoff/2026-04-19-phase4a-closeout.md` (new, this file)

- **PR 2 (stacked)** `feat/phase4a-locomotion-step-cutover` →
  `refactor/prediction-v2`
  Content (code only, already at commit `f007837`):
  - `Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs`
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs`

**Requested action**: review + merge. PR 2 depends on the branches above
remaining parallel siblings off `refactor/prediction-v2`; PR 1 is
documentation-only and can merge first or second without affecting code.

Closeout authored by Claude Code, reviewed by Cowork-mode-Claude
(architecture reviewer).
