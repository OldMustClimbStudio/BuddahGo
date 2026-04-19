# Phase 8 Cleanup Queue

Append-only TODO list for final-phase cleanup after all shadows have been
promoted into adapters and the refactor lands. Each entry names the scope,
the reason the cleanup was deferred, and a concrete acceptance criterion.

---

## Entry 1 — Reconcile serializer precision audit for ModifierState vs ComputedStats

**Source**: Phase 3c audit (`agent-exchange/handoff/2026-04-19-phase3c-audit.md` §1,
reconcile-path section).

**What to verify**: `BuddahPredictedReconcileData.ModifierState` and
`BuddahPredictedReconcileData.ComputedStats` use **matching precision /
quantization** in their respective FishNet serializers.

**Why deferred**: Phase 3c shadow tolerates any mismatch. On client
reconcile, motor writes both fields from the reconcile snapshot at
[BuddahPredictedMotor.cs:464-465](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L464-L465);
the next `[Replicate] RunInputs` at
[motor.cs:320 + motor.cs:341](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L320)
re-Resolves `_computedStats` from `_modifierState`, so the next tick's
shadow compare sees a freshly re-Resolve'd pair that matches by
construction. The stale-but-reconciled `_computedStats` is observable only
between the reconcile moment and the next RunInputs — which happens
inside the same frame for owner/server, and at most one replay tick for
remote observers.

**Risk of mismatch (what to audit)**: if the serializer quantizes
`ComputedStats` fields (e.g., packs floats into fixed-point) but does NOT
quantize `ModifierState` payloads (`AccelExtraForwardForce`,
`PostRootAccelExtraMaxSpeed`, `Scale*Multiplier`), then
`data.ComputedStats` on the receiving peer does NOT equal
`Resolve(data.ModifierState, config, tick)`. Any consumer that reads
`_computedStats` between reconcile and the next RunInputs gets a
precision-drifted value — historically a source of subtle push-mass and
forward-force drift across peers.

**Acceptance criterion**: one of the following, documented in the Phase 8
PR:
(a) both fields use identical precision (e.g., both raw `float`) — audit
    confirms serializer symmetry;
(b) deliberate quantization asymmetry is accepted AND there is no consumer
    that reads `_computedStats` between reconcile write (motor.cs:465) and
    the next RunInputs Resolve (motor.cs:320) — verified by grep of
    `_computedStats` readers;
(c) if (a)/(b) fail, unify precision (recommend raw `float` on both).

**First diagnostic step**: grep for `_computedStats` readers outside
RunInputs, and inspect `BuddahPredictedReconcileData` + its serializer
(auto-generated or hand-rolled?) to confirm field-by-field precision.

---

## Entry 2 — Phase 3b teleport rotation compare normalization

**Source**: `Docs/lessons-log.md` L15 (2026-04-19, Phase 3d V2 rerun).

**Status**: UNGATED — landed defensively as part of L15 Rule (c)
("audit every Quaternion.Angle used in shadow compare / parity
validation paths for the same vulnerability").

**Target**: [BuddahPredictedMotor.cs:1376](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1376)
(Phase 3b teleport compare):
```csharp
float rotDelta = Quaternion.Angle(_realScratch.PostTeleportRotation, _shadowScratch.PostTeleportRotation);
```

**Why deferred, not fixed now**: The compare is gated by
`else if (_realScratch.TeleportRan)` at
[motor.cs:1361](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1361),
so it only runs when both sides have actually consumed a teleport event.
In that case both `PostTeleportRotation` values are written from
`eventData.TargetRotation`, which real teleport callers populate with
either `rb.rotation` (unit quaternion) or `Quaternion.identity`
(`(0, 0, 0, 1)`). `default(Quaternion) = (0, 0, 0, 0)` does not appear
in real teleport events today.

The vulnerability is structural but not observed. Risk materializes
only if a future teleport caller emits an event with `TargetRotation =
default(Quaternion)` — a constructor-default oversight rather than
intentional data. Until that happens, no observable symptom.

**Fix shape (when applied)**: same normalize-zero-quat-to-identity
pattern as landed in 3d at
[motor.cs:1599](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1599).
Consider extracting a shared `NormalizeZeroQuatToIdentity(ref Quaternion)`
helper in the same file if Entry 2 lands — two instances of identical
code is the threshold the CLAUDE.md "three similar lines is better than
a premature abstraction" rule tolerates; three would argue for the
helper.

**Acceptance criterion**: `motor.cs:1376` rotation compare no longer
reports `180° false-positive` if a teleport event with
`TargetRotation = default(Quaternion)` is ever enqueued. Unit test
probably overkill; diff-and-build confirms.

---

## Entry 3 — Migrate locomotion decay branch from motor to step (contingent on Phase 3a scope extension)

**Source**: `agent-exchange/handoff/2026-04-19-phase4-audit.md` Addendum A
(DP-6 R2 torque-decay equivalence proof, reviewer decision 2026-04-19
selecting Option Z2 hybrid-4a).

**Target**: [BuddahPredictedMotor.cs:459-463](../../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L459-L463)
— the decay branch that reads `rb.angularVelocity`, applies
`Mathf.MoveTowards(angularVelocity.y, 0f, config.TurnDecayPerSecond *
TickDelta)`, and writes back via `_predictionRigidbody.AngularVelocity(...)`.
Retained inline in Phase 4a per Z2 scope boundary; this entry tracks
the eventual migration.

**Why deferred**: Phase 3a shadow's `BuddahLocomotionStep` was
deliberately scope-reduced to commanded-scalar observation only
(`CommandedForwardForce` + `CommandedTurnTorque`). The step's own
comment at [BuddahLocomotionStep.cs:54](../../Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs#L54)
documents `"decay branch leaves scratch at zero"`. Phase 3a's
`loc-div=0` correctly certifies commanded-scalar parity within that
scope but does NOT certify the decay-side-effect path. Phase 4a
delete-immediately on the decay branch would silently drop the
`AngularVelocity(...)` write and produce observable V3 regression
(post-steering-release spin persists indefinitely instead of damping
at `config.TurnDecayPerSecond` rad/s/sec).

**Contingency note (VERBATIM, do not rewrite)**:

> Cannot be implemented as a Phase 8 trivial deletion. Requires: (a)
> Phase 3a scope extension to add `CommandedAngularVelocityAfterDecay`
> step field, (b) re-verification V5 2-peer playtest on the
> extended-scope shadow, (c) only then migrate the decay branch out of
> motor. If these prerequisites are not met, leave the decay branch
> inline permanently.

**Implementation sketch (if prerequisites met)**: add
`Vector3 CommandedAngularVelocityAfterDecay` (or
`float AngularYAfterDecay`) field on
`BuddahPredictionShadowScratch`. Extend `BuddahLocomotionStep.Run` to
mirror `Mathf.MoveTowards(ctx.RbAngularVelocityPreTick.y, 0f,
ctx.Config.TurnDecayPerSecond * ctx.TickDeltaSeconds)` when
`!hasActiveSteering && config.TurnDecayPerSecond > 0f`. Requires
`RbAngularVelocityPreTick` snapshot on `BuddahPredictionTickContext`
captured at motor.cs entry to `RunInputs` (pre-decay read-point). Add
a 4th locomotion compare slot in `Shadow_CompareAndReport` (epsilon
1e-4 on y-axis). Re-validate Phase 3a V2 + V5 before migrating the
motor decay branch out.

**Acceptance criterion (contingent)**: (a) Phase 3a extended-scope
V2 + V5 PASS with `loc-div = 0` on the new slot across heartbeats;
(b) motor.cs:459-463 decay block deleted; (c) step's commanded
angular-velocity-after-decay becomes the authority, motor applies it
via `_predictionRigidbody.AngularVelocity(computed)` at an apply-only
callsite; (d) V3 gameplay-shake regression test confirms
post-steering-release spin damping behavior is preserved.

**Do NOT delete the motor decay branch without completing (a)(b)(c)(d)
above in order.** Reason: prevent a future Phase 8 agent from
deleting the motor decay branch as part of a cleanup pass without
understanding the scope-contract implications documented in
Phase 4 audit Addendum A.

---

## Entry 4 — Remove V3 + V13 probes after full refactor cutover

**Source**: `agent-exchange/handoff/2026-04-19-phase4-probes-closeout.md`
(Phase 4-probes PR observational-instrumentation ship).

**Targets**:
- `Assets/Scripts/New_Buddah/Debug/BuddahPredictionVisualShakeProbe.cs`
- `Assets/Scripts/New_Buddah/Debug/BuddahPredictionPerfProbe.cs`
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` — three `#if BUDDAH_PREDICTION_PERF_PROBE` islands added by the probes PR (using directive, ProfilerMarker field, `using var markerScope = s_runInputsMarker.Auto();` at RunInputs entry)
- `ProjectSettings/ProjectSettings.asset` scripting defines — `BUDDAH_PREDICTION_VISUAL_PROBE` + `BUDDAH_PREDICTION_PERF_PROBE` (only if added to a per-environment target; probes PR documents them in audit Addendum B §B.4 but does NOT enable by default in main Player Settings)

**Contingency note (VERBATIM, do not rewrite)**:

> Only remove once all phases are merged and no observational
> requirement remains. Probes may remain indefinitely if they prove
> useful for regression monitoring.

**Why deferred**: the probes are observational-only, gated entirely
behind scripting defines, and produce byte-identical release builds
to pre-probes code when defines are undefined (reviewer G1 bind, see
Phase 4-probes closeout §Appendix A). There is no strict cleanup
obligation unless the probe infrastructure itself becomes a
maintenance burden OR Unity's `ProfilerRecorder` / `ProfilerMarker`
APIs change shape in a future Unity LTS that breaks the probe's
implementation without a trivial repair.

**Acceptance criterion (when applied)**: (a) every Phase 4/5/6/7
phase has merged; (b) no outstanding regression-monitoring task
references `[D-VIS HEARTBEAT]` or `[D-PERF HEARTBEAT]`; (c) removal
PR deletes the two probe `.cs` + `.meta` files, the motor's three
`#if BUDDAH_PREDICTION_PERF_PROBE` islands, and any ProjectSettings
define lines carrying the two symbols; (d) post-removal build
confirms release binary unchanged from pre-probe-PR commit hash OR
has only whitespace / unused-import differences.

**Do NOT remove the probes as part of a bundled cleanup sweep
without satisfying (a)(b)(c)(d).** The observational value may
outlast the Phase 8 window if Phase 9+ regression monitoring
continues.

---

## Entry 5 — Debug multi-spawn for perf profile stress testing

Background: V13 baseline was captured at 2-peer 2-Buddah due to absence of
single-peer multi-spawn path. Real match load is 4-8 Buddahs; V13 gate at
2 Buddahs has blind spot for concurrency-sensitive regressions.

Task: Add a debug-only Assets/Scripts/Debug/DebugBuddahSpawner.cs that

(a) is server-authoritative behind an editor-only [Server] RPC,

(b) spawns N configurable non-player-owned Buddahs for perf testing,

(c) is gated behind a new BUDDAH_DEBUG_SPAWN define,

(d) has its own mini-audit before merge.

Once available, re-run Phase 4 V13 baseline + Phase 4a V13 post at 4 / 8
Buddah counts; if pre-vs-post delta exceeds threshold at higher counts but
passed at 2, treat as post-hoc regression discovery and issue Phase 4
regression fix.

Priority: Medium. Not blocking current phase completion but closes out
V13 scope gap before Phase 7/8 sign-off.

---
