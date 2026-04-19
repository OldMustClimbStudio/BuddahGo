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
