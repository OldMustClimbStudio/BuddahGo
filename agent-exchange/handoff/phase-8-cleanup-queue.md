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
