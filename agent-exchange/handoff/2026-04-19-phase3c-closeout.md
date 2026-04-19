# Phase 3c Close-Out Packet — for Claude Code

Date: 2026-04-19
Branch: refactor/prediction-v2
Status: V1 compile clean + V2 host-only PASS + V5 2-peer PASS (both peers).
Ready to commit; do NOT open PR until user green-lights.

---

## §1 — Final Commit Message

Paste verbatim into commit (for `git commit -F <file>`). Long on purpose —
captures the 3c architecture decisions, the V2+V5 results, the 19:1
CLIENT reconcile-replay signature as a diagnostic shape for future shadow
phases, and the unchanged deferred-work state (L7 at Phase 4, L12 at
Phase 6, Phase 8 Entry 1 observational-only pass).

```
Phase 3c — Modifier step shadow (observation only, append-only on motor)

Adds a parity-verified Euler shadow for the Modifier step of the tick
pipeline. Authority path is untouched. Shadow runs under
BUDDAH_PREDICTION_SHADOW on Standalone + Editor + Development Build only.
Any mismatch on the 12 ComputedStats fields resolved at motor.cs:348
produces a [D-LOC] warning; 60 consecutive divergences escalate to
[D-LOC FATAL] and stop.

=== What this lands ===
- Assets/Scripts/New_Buddah/Simulation/BuddahModifierStep.cs
  (was a Phase 0 stub; fleshed out as pure static mirror of
  BuddahPredictedModifierResolver.Resolve — single Run method, no state,
  no side effects. Per audit §0: re-running Resolve on identical inputs
  yields bit-identical output under C# / Mono / IL2CPP single-thread).
- Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs
  (extended, +2 fields: ShadowModifierStateSnapshot, Config; two new
  constructor args appended).
- Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs
  (extended, +2 fields: ModifierRan, ShadowComputedStats; and a DP6
  cleanup-tag comment added to ShadowLastConsumedModifierId —
  dead-since-Phase-3c, retained for Phase 0 compatibility, removal
  queued for Phase 8).
- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
  (append-only, all inside `#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
  && BUDDAH_PREDICTION_SHADOW`):
    * 3 new fields: _shadowModifierStateSnapshot,
      _shadowModifierConsumedCount, _dLocModifierDivCount.
    * 1 hook block (~16 lines) immediately after the post-consume
      Resolve at motor.cs:348 — snapshots _modifierState (struct by
      value), mirrors motor's just-written _computedStats into
      _realScratch, runs BuddahModifierStep.Run, increments
      cumulative consume counter on success.
    * BuildTickContext constructor call extended with 2 new named
      args (shadowModifierStateSnapshot, config).
    * Shadow_CompareAndReport extended: (a) anyRan gate now OR-s in
      ModifierRan from real and shadow scratches; (b) 12-field compare
      block (7 floats @ epsilon 1e-4, 5 bools exact); (c) mod-div
      counter reset at heartbeat; (d) modDiverged aggregated into
      _dLocConsecutive and anyDiverged; (e) FATAL message text
      updated to reference Phase 3c.
    * Heartbeat format upgraded to 3-line (unchanged line count, fields
      added):
        [D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
          loc-div=<n> imp-div=<n> tel-div=<n> mod-div=<n>
          imp-compared=<n> tel-compared=<n> mod-compared=<n>
      (both active + idle variants).
- Docs/prediction-refactor-plan/phase-4-prerequisites.md (new)
  Prereq-1 — L7 BuddahMovementModeSwitcher double-fire ApplyMode.
  Recommends Option B (adapter-side defer-until-first-replicate).
  Blocks Phase 4 adapter cut-over; does NOT block 3c.
- agent-exchange/handoff/phase-8-cleanup-queue.md (new)
  Entry 1 — Reconcile serializer precision audit for ModifierState
  vs ComputedStats. Phase 3c V5 CLIENT 19:1 ratio with zero mod-div
  passes this watchpoint OBSERVATIONALLY. Entry stays open; Phase 8
  must supply audit-level verification (grep consumers +
  serializer-field inspection), not just the observation.

=== Validation (parity-by-call-site-invariant gate model, 3a/3b/3c
    consistent) ===
V1 compile + editor load:
  PASS — reflection confirms all new types + fields loaded; no new
  errors or warnings attributable to 3c.

V2 host-only 30s + anti_acceleration x2:
  PASS —
    loc-div / imp-div / tel-div / mod-div = 0 on every heartbeat.
    mod-compared tail = 3841 (~60/sec, as expected).
    0 non-heartbeat [D-LOC]. 0 [D-LOC FATAL].
    Digest: agent-exchange/console/2026-04-19-phase3c-v2.log

V5 2-peer 60s HOST:
  PASS —
    All div counters = 0 across 96 heartbeats.
    mod-compared tail = 6601. imp-compared tail = 3 (cross-peer pushes
    from CLIENT — re-validates 3b impulse shadow).
    tel-compared = 0 (L12 not exercised; unchanged from Phase 6 prereq
    state).
    mod-compared / active-ticks ratio = 1.00 (server has no
    reconcile-replay; shadow fires once per physics tick).
    Digest: agent-exchange/console/2026-04-19-phase3c-v5-host.log

V5 2-peer 60s CLIENT:
  PASS —
    All div counters = 0 across 10 captured heartbeats.
    mod-compared tail = 63365. imp-compared / tel-compared = 0 at
    tail (CLIENT was not pushed and did not respawn).
    mod-compared / active-ticks ratio ≈ 19:1.
    Digest: agent-exchange/console/2026-04-19-phase3c-v5-client.log

=== Architecture note — the 19:1 CLIENT ratio is the "FishNet
    reconcile-replay signature" ===
CLIENT's `_shadowModifierConsumedCount` increments inside RunInputs at
motor.cs:362. FishNet 4.x re-runs RunInputs for every tick in the
reconcile-replay window PLUS the forward tick. OnPostTick (which
hosts Shadow_CompareAndReport) fires only on the forward tick, so
`_shadowActiveCompares` increments once per physics tick while
`_shadowModifierConsumedCount` increments (N+1) times per physics
tick, where N is the replay window depth. The observed ratio of
~19:1 on CLIENT vs 1:1 on HOST matches this model: N≈18 on CLIENT
(~300ms rollback over the lobby), N=0 on HOST.

Diagnostic utility of the ratio: if a future shadow phase lands with
CLIENT ratio ≈ 1:1, it either means (a) reconcile-replay is not
reaching RunInputs for that peer (FishNet config regression), or (b)
the shadow hook was placed outside RunInputs (architecture bug). Use
the ratio as a sanity check every V5, not as a gate value.

This observation does NOT promote to a lessons-log L-entry — it is
expected FishNet behavior, not a failure mode. Left as a commit-level
architecture note for future shadow phases to reference.

=== Serializer precision watchpoint — observational PASS ===
Phase 8 cleanup-queue Entry 1 asks whether
`BuddahPredictedReconcileData.ModifierState` and `ComputedStats` are
serialized with matching precision. If they were NOT, CLIENT's
reconciled `_computedStats` would diverge from
`Resolve(_modifierState, config, tick)` on any consumer reading
`_computedStats` between the reconcile write (motor.cs:465-ish) and
the next RunInputs re-Resolve. 19 shadow compares per physics tick
with zero mod-div across 10 captured heartbeats = no observable
drift. Phase 8 audit is still required (observation ≠ spec-level
proof — a race-window consumer could be rare enough to miss the
V5 capture window), but Phase 3c V5 passes the watchpoint at the
observational level.

=== What this does NOT land (explicit non-goals) ===
- No change to the motor authority path. The shadow is observation-
  only. Adapters are still on the legacy Switcher-routed code path
  pending Phase 4.
- No L7 fix (BuddahMovementModeSwitcher double-ApplyMode). Deferred
  to Phase 4 per Docs/prediction-refactor-plan/phase-4-prerequisites.md
  Prereq-1. Phase 3c's shadow does not depend on L7 (audit §3).
- No L12 fix (owner→server→owner teleport RPC silent drop). Deferred
  to Phase 6 per Docs/prediction-refactor-plan/phase-6-prerequisites.md
  Prereq-1. Phase 3c's shadow does not depend on L12 (teleport is
  3b's shadow scope; 3c only uses _modifierState, which L12 does not
  affect).
- No Phase 8 serializer audit. Watchpoint passes observationally via
  the V5 CLIENT 19:1 result; full audit deferred to Phase 8 per
  agent-exchange/handoff/phase-8-cleanup-queue.md Entry 1.
- No removal of `ShadowLastConsumedModifierId`. Kept as dead field
  with Phase 8 removal tag (DP6). Zero cost, preserves Phase 0 intent
  trail.

=== L-entry delta ===
None. Phase 3c did not surface a new failure mode. L13 (shadow PASS
requires compared>0) and L12 (RPC silent-drop) coined in 3b close-out
continue to apply unchanged. L7 (lifecycle double-fire) remains at
watchpoint severity with Phase 4 as the activation point.

=== Protected-file disclosure (L6-style, append-only observation) ===
BuddahPredictedMotor.cs modifications — all within
`#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW`:
  - Lines 83-89:   3-line field-group comment + 3 field declarations
                   (inside the 66-90 shadow field block).
  - Lines 349-364: post-consume shadow hook block (16 lines).
  - Line 1244:     shadowModifierStateSnapshot constructor arg.
  - Line 1245:     config constructor arg.
  - Line 1253:     anyRan gate extended with ModifierRan pair.
  - Lines 1261,
    1274:          heartbeat Debug.Log format strings (idle + active
                   variants) extended with mod-div + mod-compared.
  - Lines 1265,
    1278:          `_dLocModifierDivCount = 0` reset after heartbeat
                   emit (idle + active branches).
  - Line 1284:     `bool modDiverged = false;` declaration.
  - Lines 1394-
    1487:          12-field modifier compare block (94 lines).
  - Line 1492:     `if (modDiverged) _dLocModifierDivCount++;`.
  - Line 1494:     anyDiverged aggregation extended with modDiverged.
  - Line 1502:     FATAL message text: "Phase 3b" → "Phase 3c".
All additions respect the existing #if gates, preserve append-only
discipline, and leave the motor authority path untouched in
release/non-shadow builds.
```

---

## §2 — Closeout checklist

- [x] Compile clean (V1 — reflection-verified, no new errors/warnings)
- [x] V2 host-only PASS (mod-div=0, mod-compared tail=3841, 0 non-hb
      [D-LOC], 0 FATAL; anti_acceleration x2 cast exercised
      accel-active branches)
- [x] V5 2-peer HOST PASS (ratio 1:1, mod-compared tail=6601,
      imp-compared=3 cross-re-validates 3b, all div=0, 0 non-hb
      [D-LOC], 0 FATAL)
- [x] V5 2-peer CLIENT PASS (ratio 19:1 — reconcile-replay signature,
      mod-compared tail=63365, all div=0 across replay path too,
      0 non-hb [D-LOC], 0 FATAL)
- [x] Motor protected-region untouched outside `#if`-gated append-only
      shadow scope (see Appendix A below for exact lines)
- [x] Phase 4 carry-over registered (phase-4-prerequisites.md Prereq-1,
      L7)
- [x] Phase 6 carry-over unchanged (phase-6-prerequisites.md Prereq-1,
      L12 — inherited from 3b)
- [x] Phase 8 carry-over registered (phase-8-cleanup-queue.md Entry 1,
      serializer precision + DP6 dead-field removal)
- [x] Append-only / observation-only mode preserved (no adapter code
      written, no Switcher edit, no motor authority path change)
- [x] Parity-by-call-site-invariant gate model maintained (shadow step
      has no internal gate; motor owns skip-gating at RunInputs
      entry) — consistent with 3a/3b
- [x] L13 compared>0 rule satisfied on every peer (V2 and V5 both
      peers)
- [ ] PR opened — deferred until user green-light

---

## §3 — Phase 3d starter prompt (Handoff shadow)

Use this as the seed for Phase 3d pre-execution audit. Same skeleton
as Phase 3c audit (§0 body inventory / §1 call-site inventory / §2
design sketch / §3 lifecycle-fix plan if any / §4 V2+V5 scope / §5
open decision points).

```
Phase 3d — Handoff shadow + (possibly) L12-style failure-visibility
addition. Pre-execution audit ONLY. Do NOT write code yet.

Scope: add a parity-verified shadow for the LaunchHandoff step of the
tick pipeline. Authority path untouched. Shadow runs under
BUDDAH_PREDICTION_SHADOW, observation-only, append-only on
BuddahPredictedMotor.cs. Parity-by-call-site-invariant gate model
(consistent with 3a / 3b / 3c).

Files to inspect (seed list; expand as discovered):
  - Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
    (sections of interest: RefreshLaunchState, ApplyLaunchHandoffInputScaling,
     ApplyLaunchInheritedVelocity, ConsumePendingLaunchHandoffEvent,
     RequestAuthoritativeLaunchHandoffFromOwner, RequestHandoffServerRpc,
     any TargetRpc return-leg, reconcile write of HandoffState).
  - BuddahPredictedLaunchHandoffEventData / State structs.
  - BuddahPredictionTickContext (plan what new fields the shadow needs).
  - BuddahPredictionShadowScratch (plan what new fields, and whether
    ShadowLastConsumedHandoffId — another Phase 0 dead-field candidate —
    stays or goes).

§0 — Body inventory: what does RefreshLaunchState/ConsumePendingLaunchHandoffEvent
actually do? Does it consume events from a queue (like impulse) or a
single-slot (like teleport)? Pure-static or state-mutating? Any
hidden mid-tick branch that makes shadow divergence hard to reason
about?

§1 — Call-site inventory: where in motor does the handoff affect
downstream state (input scaling, velocity inheritance,
SuppressSteeringUntilTick, RoomBypassUntilTick, _externalKinematicControlActive,
_introControlActive)?

§2 — Shadow design sketch: where to snapshot handoff inputs (LIFO
queue? single slot?), what to compute in the step, what to compare,
epsilon policy.

§3 — L12 applicability check: `RequestAuthoritativeLaunchHandoffFromOwner`
at motor.cs:768-817 has the SAME silent-return pattern as the teleport
path that surfaced L12 in Phase 3b V5 R2 (motor.cs:886-931). Audit
predicts: client-initiated launch-handoff scenarios may produce
handoff-compared=0 on CLIENT while HOST sees handoff-compared>0 —
the exact L12 shape but for handoffs. Decision point: does 3d
implement the failure-visibility fix (a result-TargetRpc + owner-side
commit-gate) OR defer to Phase 6 alongside the teleport fix? Defer
is the conservative path (3d shadow can observe the bug without
fixing it, same way 3b did for teleport). Fix-in-3d is riskier but
closes two L12-shaped bugs in one PR.

§4 — V2 + V5 scope: V2 host-only exercises the server-direct
enqueue path; V5 2-peer is required to exercise the owner→server→owner
RPC chain (where L12 lives). If 3d defers L12 fix, V5 CLIENT's
handoff-compared is EXPECTED to be 0 in most sessions (client rarely
initiates handoff) — gate should use per-peer coverage combination
per L13 (a) rule.

§5 — Open decision points for reviewer:
  1. Epsilon policy (match 3c: 1e-4 for floats, exact for bools)?
  2. Shadow scratch layout for handoff state.
  3. Snapshot timing.
  4. L12 fix scope (defer to Phase 6 vs land in 3d).
  5. Heartbeat format extension (add hof-div + hof-compared as a
     fourth category, or keep 3 categories and fold handoff into
     existing).
  6. ShadowLastConsumedHandoffId dead-field handling (same DP6 tag
     pattern as 3c?).
  7. TickContext field additions.

Write findings to:
  agent-exchange/handoff/2026-04-XX-phase3d-audit.md

Do NOT change any .cs file. Do NOT start implementation. Report when
audit doc is ready for review.
```

Suggested reference reading for the audit: this doc §1 (commit
message), agent-exchange/handoff/2026-04-19-phase3c-audit.md (for
skeleton reuse), Docs/lessons-log.md L12-L13, Docs/prediction-refactor-plan/phase-6-prerequisites.md.

---

## §4 — Carry-over reminders

Do not let these fall off the map between phases.

### Phase 4 (first concrete adapter cut-over)
- **Blocked on L7** (Switcher double-fire ApplyMode). See
  [phase-4-prerequisites.md](../../Docs/prediction-refactor-plan/phase-4-prerequisites.md)
  Prereq-1. Recommended fix: Option B — adapter defers enqueue until
  first `[Replicate]` tick via an `Initialize()` late-bind hook.
  Acceptance criterion: V8-style re-run produces 2 `[CommandBus]:ClearAll`
  lines per Buddah spawn (not 4), OR an adapter-internal probe proves
  pending-enqueue survives the Switcher double-fire window.

### Phase 6 (teleport cut-over)
- **Blocked on L12** (owner→server→owner teleport RPC silent drop at
  motor.cs:886-931). See
  [phase-6-prerequisites.md](../../Docs/prediction-refactor-plan/phase-6-prerequisites.md)
  Prereq-1 (unchanged from 3b close-out). Acceptance criterion:
  client-initiated fall-respawn produces `tel-compared >= 1` on
  client peer's shadow heartbeat. Companion gap flagged at
  `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:768-817) —
  Phase 3d may surface the same L12 shape there; plan fix either
  in 3d or fold into Phase 6.

### Phase 8 (cleanup)
- **Entry 1**: serializer precision audit for ModifierState vs
  ComputedStats. See
  [phase-8-cleanup-queue.md](phase-8-cleanup-queue.md) Entry 1. Phase
  3c V5 CLIENT 19:1 + 0 mod-div passes this watchpoint OBSERVATIONALLY;
  Phase 8 must deliver audit-level evidence (grep `_computedStats`
  consumers outside RunInputs; inspect `BuddahPredictedReconcileData`
  serializer for field-by-field precision).
- **Entry-candidate (add when touching the area)**: remove
  `ShadowLastConsumedModifierId` dead field on
  `BuddahPredictionShadowScratch` (DP6 tag comment present). Cleanup-
  only — zero runtime impact. Note: `ShadowLastConsumedHandoffId`
  may become another DP6 candidate after Phase 3d audit decides how
  handoff identity is tracked.

### Architecture observation (non-blocking)
- The 19:1 CLIENT vs 1:1 HOST mod-compared/active-ticks ratio is the
  **FishNet reconcile-replay signature**. Use as a sanity check on
  every V5 going forward. Deviation ≈1 on CLIENT → FishNet config
  regression OR shadow hook placed outside RunInputs. Deviation >1
  on HOST → server unexpectedly runs replay path (inspect
  TimeManager / NetworkObserver config). See §1 commit body for
  full reasoning.

---

## Appendix A — Protected-file disclosure (BuddahPredictedMotor.cs)

Reiteration of the line-range manifest included in §1 for convenience.
All additions are inside `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) &&
BUDDAH_PREDICTION_SHADOW` gates. No change to non-shadow builds. No
authority-path edit. Append-only, observation-scope per L6.

| Lines            | Scope                                                                 |
|------------------|------------------------------------------------------------------------|
| 83-89            | 3-line group comment + 3 field declarations                           |
| 349-364          | Post-consume shadow hook block (snapshot + mirror + Run + counter)    |
| 1244             | BuildTickContext: `shadowModifierStateSnapshot:` arg                  |
| 1245             | BuildTickContext: `config:` arg                                       |
| 1253             | anyRan gate extended with `ModifierRan` pair                          |
| 1261             | Idle heartbeat Debug.Log: mod-div + mod-compared added                |
| 1265             | Idle heartbeat reset: `_dLocModifierDivCount = 0`                     |
| 1274             | Active heartbeat Debug.Log: mod-div + mod-compared added              |
| 1278             | Active heartbeat reset: `_dLocModifierDivCount = 0`                   |
| 1284             | `bool modDiverged = false;` local in Shadow_CompareAndReport          |
| 1394-1487        | 12-field modifier compare block (7 floats @ 1e-4, 5 bools exact)      |
| 1492             | `if (modDiverged) _dLocModifierDivCount++;`                           |
| 1494             | anyDiverged aggregation extended with `modDiverged`                   |
| 1502             | FATAL message text: "Phase 3b" → "Phase 3c"                           |

Audit verification command (for future review):
```
grep -nE 'Phase 3c|_shadowModifierStateSnapshot|_shadowModifierConsumedCount|_dLocModifierDivCount|ShadowComputedStats|BuddahModifierStep\.Run|mod-div|mod-compared|modDiverged' Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
```

---

End of packet. Awaiting user green-light before `git add / git commit -F
<§1 body> / git push / PR creation`.
