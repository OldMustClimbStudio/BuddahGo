# Phase 3c Pre-Execution Audit — Modifier Shadow + L7 Lifecycle Fix

Date: 2026-04-19
Branch: refactor/prediction-v2 (Phase 3b merged or PR open)
Scope: read-only. No code written. Findings for review before 3c implementation.
Files inspected:
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedModifierResolver.cs` (73 lines)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedModifierState.cs` (37 lines)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotorComputedStats.cs` (19 lines)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` — Resolve call sites and downstream usage
  - `Assets/Scripts/New_Buddah/Bootstrap/BuddahMovementModeSwitcher.cs` (65 lines)
  - `Assets/Scripts/New_Buddah/Integration/BuddahPredictionLegacyIsolationBridge.cs` (43 lines)
  - `Docs/lessons-log.md` — L7 entry (lines 250-256)

---

## §0 — Resolver Body Inventory

### Signature
```csharp
public static BuddahPredictedMotorComputedStats
  Resolve(BuddahPredictedModifierState state, BuddahPredictedMotorConfig config, uint tick)
```

### Inputs (3 total)
1. `state` — `BuddahPredictedModifierState` (struct, passed **by value**). 14 fields:
   - 8 `uint *UntilTick` deadlines: `RootUntilTick`, `AccelUntilTick`,
     `PostRootAccelUntilTick`, `ScaleUntilTick`, `InvertTurnUntilTick`,
     `PushGraceUntilTick`, `SuppressSteeringUntilTick`, `RoomBypassUntilTick`.
   - 6 float payload values: `AccelExtraForwardForce`, `AccelExtraMaxSpeed`,
     `PostRootAccelExtraForwardForce`, `PostRootAccelExtraMaxSpeed`,
     `ScaleMultiplier`, `ScaleMassMultiplier`, `ScaleForwardForceMultiplier`.
     (7 floats actually — re-counting.)
2. `config` — `BuddahPredictedMotorConfig` (MonoBehaviour-attached ScriptableObject-ish ref).
   Resolver reads 3 fields: `config.ForwardForce`, `config.MaxSpeed`, `config.TurnTorque`.
   All other config fields (PushExtraMaxSpeed, TurnInputMultiplier, TurnDecayPerSecond, PushGraceSeconds) are NOT read by Resolve — they're read by motor elsewhere.
3. `tick` — `uint`, current-tick deadline for the 8 `> tick` activity comparisons.

### Outputs
`BuddahPredictedMotorComputedStats` struct, 12 fields:
  - 4 float "Final" fields: `FinalForwardForce`, `FinalMaxSpeed`, `FinalTurnTorque`, `FinalSteeringSign`.
  - 5 bool "Is" flags: `IsRooted`, `IsInvertTurnActive`, `IsPushGraceActive`, `IsSteeringSuppressed`, `IsRoomBypassActive`.
  - 3 float "Scale" fields: `ScaleMultiplier`, `ScaleMassMultiplier`, `ScaleForwardForceMultiplier`.

### Internal flow (resolver body, lines 10-71)
1. Initialize `result` with config defaults (`ForwardForce`, `MaxSpeed`, `TurnTorque`), steering sign = 1, scale multipliers = 1.
2. Compute 8 activity bools from `state.*UntilTick > tick` comparisons.
3. If `scaleActive`: overwrite 3 scale fields with `Mathf.Max(0.1f, state.Scale*)` floor-clamped values.
4. Unconditional: `FinalTurnTorque *= ScaleMultiplier`, `FinalForwardForce *= ScaleForwardForceMultiplier`.
5. If `accelActive`: add `state.AccelExtra*` to `FinalForwardForce` and `FinalMaxSpeed`.
6. If `postRootAccelActive`: add `state.PostRootAccelExtra*` to same.
7. If `invertTurnActive`: `FinalSteeringSign = -1`.
8. If `rootActive`: hard-zero `FinalForwardForce` and `FinalMaxSpeed`.
9. `Mathf.Max(0, ...)` clamps on FinalMaxSpeed, FinalForwardForce, FinalTurnTorque.
10. Assign 4 Is* flags (rootActive, invertTurnActive, pushGraceActive, suppressSteeringActive, roomBypassActive → Is* ditto; `pushGraceActive` and `suppressSteeringActive` both map to dedicated output fields).
11. Return `result`.

### Hidden-branch inventory (things that could make shadow diverge from motor)
  - **None found**. The 8 activity comparisons are pure arithmetic against `tick`. Order of operations is deterministic. No floating-point non-determinism (single-threaded, no FMA surprises in Mathf.Max).
  - **Time dependence**: all conditional branches are controlled by the single `tick` parameter vs state deadlines. If shadow's `tick` argument matches motor's `currentTick` at the call site, the branches align.
  - **No randomness**, no `Time.deltaTime`, no external I/O, no GetComponent, no ScriptableObject.LoadAsync. Pure struct-in / struct-out.

### Mutable side-effect inventory
  - **Zero side effects**. The method creates `result` local, returns by value. `state` is passed by value (struct copy) — cannot mutate caller. `config` is read-only (only 3 float reads, no writes). No static state touched.
  - **Re-entrant safety**: running Resolve twice on identical inputs produces BIT-IDENTICAL output (same IL codepath, no FP nondeterminism under C# / Mono / IL2CPP single-thread).

### Verdict
Pure-static, pure-function. Phase 3c's shadow approach (run Resolve twice on independent state copies and compare) is **safe** — no blocker from the resolver side.

---

## §1 — Motor Resolve Call-Site Inventory

Six Resolve call sites in `BuddahPredictedMotor.cs`. Plus reconcile-path _computedStats overwrite. Classified by purpose.

| # | File:Line | Context | Motor uses result for | Shadow mirror need |
|---|-----------|---------|------------------------|---------------------|
| 1 | motor.cs:203 | `CreateReconcile` (pre-serialize) | `IsRoomBypassActive` check for `movementAllowed`; result serialized into reconcile `data.ComputedStats` | Server-only code path. Shadow does not need to run here; the reconcile snapshot IS the ground truth that client shadow will compare against on replay. |
| 2 | motor.cs:271 | `BuildReplicateData` (pre-tick input build) | `IsRoomBypassActive` check for `movementAllowed` in input data; debug state | Observability-only; the produced `_computedStats` is overwritten at line 320 inside RunInputs before any locomotion consumes it. No shadow compare needed. |
| 3 | motor.cs:320 | `RunInputs` pre-consume | `ApplyResolvedMassMultiplier` at line 321 → writes `rb.mass` before PhysX Simulate | Motor-only scratch. Shadow doesn't run PhysX, so mass-multiplier mirror is not needed in 3c. |
| 4 | motor.cs:341 | `RunInputs` **post-consume** — AUTHORITATIVE for the tick's locomotion | `ClampPlanarSpeed` (FinalMaxSpeed + PushExtraMaxSpeed), `ApplyResolvedMassMultiplier` again, rooted gate, locomotion AddForce (`FinalForwardForce`), AddTorque (`FinalTurnTorque`, `FinalSteeringSign`), `IsSteeringSuppressed`, BuildTickContext pass-through to locomotion shadow | **Primary shadow compare site**. Shadow mirrors this Resolve, compares 12 fields against motor's just-written `_computedStats`. |
| 5 | motor.cs:1440 | `TryApplyModifierCommand` (after state mutation) | Re-resolve so downstream callers see updated stats immediately; used by debug state sync | Mid-tick; the final value at motor.cs:341 overwrites this mid-tick result in the next RunInputs call anyway. Shadow-compare not needed — but shadow snapshot must be taken AFTER this runs if the command fires within RunInputs. (It doesn't — `TryApplyModifierCommand` is invoked from adapter code, typically outside the RunInputs flow.) |
| 6 | motor.cs:1580 | `ConsumePendingTeleportEvent` (post-teleport-reset) | Re-resolve after possible `_modifierState = default` wipe at motor.cs:1527 when `ResetModifiers=true`; debug state sync | Mid-tick — between motor.cs:337 (ConsumePendingTeleportEvent call) and motor.cs:341 (final Resolve). The line 341 Resolve re-does the work, so this intermediate Resolve is observability-only. Shadow-compare not needed. |

### Reconcile-path _computedStats overwrite — motor.cs:464-465

```csharp
_modifierState = data.ModifierState;
_computedStats = data.ComputedStats;
```

Inside `[Reconcile] ReconcileState`. On client reconcile, motor OVERWRITES
both `_modifierState` and `_computedStats` from the server-broadcast reconcile
snapshot. After this, the next `[Replicate] RunInputs` fires Resolve at line
320 + 341 on the replayed tick — both based on the just-written `_modifierState`.

**Shadow implication**: on client reconcile-replay ticks, motor's `_computedStats`
at motor.cs:341 equals `Resolve(_modifierState, config, currentTick)`. Shadow
mirroring the same Resolve on the same `_modifierState` snapshot produces the
same output → divergence = 0 expected.

**Risk**: if the reconcile serializer serializes `_modifierState` and `_computedStats`
with different precision (e.g., quantizes `_computedStats` but not
`_modifierState`), then `data.ComputedStats` won't equal `Resolve(data.ModifierState, ...)`.
The _computedStats gets overwritten from data.ComputedStats at line 465, but
the NEXT Resolve at line 320 / 341 runs on data.ModifierState → produces a
FRESHLY-RESOLVED value. So the reconcile-imposed `_computedStats` survives only
until the next RunInputs tick. By the time shadow runs at 341, `_computedStats`
has already been re-Resolve'd from the reconciled `_modifierState`, and shadow's
Resolve matches it. **No divergence expected** from this path.

**Edge case**: if client's reconcile lands and RunInputs does NOT fire before
something else reads `_computedStats` (e.g., debug state sync), the stale-but-
reconciled `_computedStats` is observable. This is motor-internal and does not
affect shadow divergence. No action needed for 3c.

### Interaction between pre-consume (320) and post-consume (341)

Between 320 and 341, the following may mutate `_modifierState`:
  - `ConsumePendingTeleportEvent`:
    - `eventData.ResetModifiers` → `_modifierState = default` at motor.cs:1527
    - `eventData.ResetPushGrace` → `_modifierState.PushGraceUntilTick = 0` at motor.cs:1543
    - Then local Resolve at motor.cs:1580 (observability)
  - `ConsumePendingLaunchHandoffEvent`:
    - `_modifierState.SuppressSteeringUntilTick = Max(existing, handoffState.SuppressSteeringUntilTick)` at motor.cs:1625
    - `_modifierState.RoomBypassUntilTick = Max(existing, handoffState.RoomBypassUntilTick)` at motor.cs:1627
  - `ConsumePendingImpulseEvents`:
    - `ApplyPushGraceFromImpulse`: `_modifierState.PushGraceUntilTick = Max(existing, newUntil)` at motor.cs:1743

These mutations are what motivate the line 341 RE-Resolve — if _modifierState changed mid-tick, the pre-consume Resolve at line 320 is stale. Motor discards the 320 result at line 341.

---

## §2 — Shadow Design Sketch

### Single shadow step: `BuddahModifierStep.Run`

Pure static, mirrors motor.cs:341 post-consume Resolve:

```csharp
public static void Run(
    in BuddahPredictionTickContext ctx,
    in BuddahPredictedInputData input,
    ref BuddahPredictionShadowScratch scratch)
{
    var shadowStats = BuddahPredictedModifierResolver.Resolve(
        ctx.ShadowModifierStateSnapshot, ctx.Config, ctx.Tick);

    scratch.ShadowComputedStats = shadowStats;
    scratch.ModifierRan = true;
}
```

### Motor integration (no-code-here, just sketch)

Add under #if:
  - `BuddahPredictedModifierState _shadowModifierStateSnapshot;` — motor field
  - `uint _shadowModifierConsumedCount;` — cumulative counter (L13 compared gate)
  - `int _dLocModifierDivCount;` — per-window divergence counter

Insert immediately AFTER motor.cs:341 Resolve:
```csharp
#if ... && BUDDAH_PREDICTION_SHADOW
_shadowModifierStateSnapshot = _modifierState;  // struct copy
{
    var tickCtx = BuildTickContext(...);  // config + shadow snapshot fields fed in
    BuddahModifierStep.Run(in tickCtx, in data, ref _shadowScratch);
    if (_shadowScratch.ModifierRan)
        _shadowModifierConsumedCount++;
}
#endif
```

Motor's line 342 `ApplyResolvedMassMultiplier` runs next — unaffected (reads `_computedStats`, which is motor's just-resolved value).

### TickContext additions
  - `BuddahPredictedModifierState ShadowModifierStateSnapshot` — struct, fed from `_shadowModifierStateSnapshot`.
  - `BuddahPredictedMotorConfig Config` — reference. Motor already has `config` field; pass it into TickContext.

### ShadowScratch additions
  - `BuddahPredictedMotorComputedStats ShadowComputedStats` — all 12 fields.
  - `bool ModifierRan`.
  - (Existing reserved field `ShadowLastConsumedModifierId` is NOT used by 3c — it was reserved for a different design. Keep as dead field for Phase 0 compatibility; 3c does not populate.)

### Compare design (in `Shadow_CompareAndReport`)

Gate: `if (!_shadowScratch.ModifierRan) skip modifier compare block`.

For each of 12 ComputedStats fields:
  - Float fields: `|real - shadow| > 1e-4f` → divergence, log warning with delta.
  - Bool fields: `real != shadow` → divergence, log warning with both values.

**Epsilon per field**:

| Field | Type | Epsilon | Rationale |
|-------|------|---------|-----------|
| FinalForwardForce | float | 1e-4 | Scale + accel arithmetic; standard |
| FinalMaxSpeed | float | 1e-4 | Same |
| FinalTurnTorque | float | 1e-4 | Scale arithmetic |
| FinalSteeringSign | float | 1e-4 | In practice ±1 exactly |
| ScaleMultiplier | float | 1e-4 | Max(0.1, input) |
| ScaleMassMultiplier | float | 1e-4 | Same |
| ScaleForwardForceMultiplier | float | 1e-4 | Same |
| IsRooted | bool | exact | |
| IsInvertTurnActive | bool | exact | |
| IsPushGraceActive | bool | exact | |
| IsSteeringSuppressed | bool | exact | |
| IsRoomBypassActive | bool | exact | |

In theory, epsilon could be **0** since motor and shadow call the identical Resolve method on identical inputs — output is bit-identical. 1e-4 is kept only for consistency with 3a / 3b conventions and future-proofing against any compiler-level FP reordering.

### Heartbeat extension

```
[D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
  loc-div=<n> imp-div=<n> tel-div=<n> mod-div=<n>
  imp-compared=<n> tel-compared=<n> mod-compared=<n>
```

Format grows to 3 lines still (mod-div + mod-compared squeeze in). Or 4 lines — user preference.

### New D-LOC warning lines

```
[D-LOC] T=<n> mod-ran gate mismatch: real=<bool> shadow=<bool>    (shouldn't fire — mod always ran if locomotion did)
[D-LOC] T=<n> mod-field delta: field=<name> real=<val> shadow=<val> delta=<n>
[D-LOC] T=<n> mod-flag mismatch: flag=<name> real=<bool> shadow=<bool>
```

---

## §3 — L7 Lifecycle Fix Plan

### L7 recap (Docs/lessons-log.md:252-256)

**Symptom**: Phase 2 V8 test expected 2 `[CommandBus]:ClearAll` lines per
Buddah spawn (one bus-internal, one bridge-wrap). Actual: **4 lines per
spawn** across all 3 runs.

**Cause**: `BuddahMovementModeSwitcher.Awake()` and
`BuddahMovementModeSwitcher.OnEnable()` both call `ApplyRuntimeMode(force:true)`.
Awake + OnEnable fire sequentially on an enabled GameObject → `ApplyMode`
runs twice on spawn → 2 ClearAll pairs = 4 lines.

**Phase 2 assessment**: harmless (no adapter enqueues yet, ClearAll hits
empty channels both times).

**Phase 4+ threat**: any adapter that enqueues during init will have its
enqueue nuked by the second ClearAll.

### Current Switcher structure
[BuddahMovementModeSwitcher.cs:18-34](Assets/Scripts/New_Buddah/Bootstrap/BuddahMovementModeSwitcher.cs#L18-L34):
```csharp
private void Awake()       { ResolveReferences(); ApplyRuntimeMode(force: true); }
private void OnEnable()    { ResolveReferences(); ApplyRuntimeMode(force: true); }
private void Update()      { if (_appliedMode != runtimeMode) ApplyRuntimeMode(force: false); }
```

`ApplyRuntimeMode(force:true)` at line 48 bypasses the `_appliedMode == runtimeMode` short-circuit. `ApplyRuntimeMode` body at line 46-62 always calls `_legacyIsolationBridge.ApplyMode(...)` which issues ClearAll via the CommandBus.

### Three fix options

#### Option A — Switcher debounces (Switcher-side)

Make `Awake` set a `_awakeApplied = true` flag, and `OnEnable` check it:

```csharp
private bool _awakeApplied;
private void Awake()    { ResolveReferences(); ApplyRuntimeMode(force: true); _awakeApplied = true; }
private void OnEnable() { ResolveReferences(); if (!_awakeApplied) ApplyRuntimeMode(force: true); }
```

Unity's lifecycle: Awake fires once per component lifetime, OnEnable fires once per enable cycle. On first enable (start of scene), Awake runs then OnEnable — so _awakeApplied becomes true at Awake, OnEnable skips. On subsequent disable→enable, Awake does NOT re-fire, OnEnable does — we want ApplyMode to run then. But `_awakeApplied` is already true from initial Awake → OnEnable skips. **Bug**: re-enable after disable would skip ApplyMode.

Correct variant:
```csharp
private bool _appliedThisFrame;
private void Awake()    { ResolveReferences(); ApplyRuntimeMode(force: true); _appliedThisFrame = true; }
private void OnEnable() { ResolveReferences(); if (!_appliedThisFrame) ApplyRuntimeMode(force: true); _appliedThisFrame = true; }
private void LateUpdate() { _appliedThisFrame = false; }
```

Flag resets at end of frame, so re-enable in a later frame re-applies.

**Pros**:
- Contained to Switcher (single file).
- Preserves the `force:true` semantics.
- No ripple into adapter code.

**Cons**:
- Adds a `LateUpdate` callback just for a flag reset — small perf cost on every
  Buddah, every frame.
- The "first Awake+OnEnable" double-fire pattern is a common Unity hazard —
  Switcher-side fix doesn't help any OTHER component that might have similar
  doubled lifecycle calls.

#### Option B — Adapter defers enqueue (adapter-side)

Adapters (Phase 4 work: ImpulseCommandAdapter, TeleportCommandAdapter,
ModifierCommandAdapter, HandoffCommandAdapter) do NOT enqueue during `Awake`
or `OnEnable`. They defer until the first `[Replicate]` tick arrives.

**Implementation shape**:
- Adapters expose an `Initialize()` method called from a coordinator (likely
  BuddahPredictionBootstrap).
- Bootstrap's `OnNetworkStarted` (or equivalent late-bind hook) calls
  adapter.Initialize AFTER Switcher's double-ApplyMode dust has settled.
- Or: adapters register for the motor's first `[Replicate]` callback and
  enqueue any pending intent at that moment.

**Pros**:
- No change to Switcher — Switcher's double-fire is accepted behavior.
- Adapter init contract is explicit: "we enqueue on first replicate, not during
  component-init lifecycle". Clear invariant for Phase 4+.
- Sidesteps Unity lifecycle pitfalls across all future adapters.

**Cons**:
- Requires defining the coordinator contract (who calls adapter.Initialize,
  when). Bootstrap composition concern.
- Delays legitimate adapter activity by up to one tick. In practice
  imperceptible, but technically a one-tick latency regression on spawn.

#### Option C — Hybrid (Switcher exposes `IsInitComplete`)

Switcher adds a public `IsInitComplete` property. Adapters can check it before
enqueueing.

```csharp
public bool IsInitComplete => _appliedMode == runtimeMode && _appliedOnce;
```

Adapters in their `Awake`/`OnEnable`: `if (!switcher.IsInitComplete) return;`.
Subsequent normal-flow calls proceed.

**Pros**:
- Explicit contract, defensive.
- Cheap (property read).

**Cons**:
- Adapter-side boilerplate (every adapter adds the check).
- If an adapter forgets the check, the ClearAll-burst bug returns.

### Decision for 3c

Phase 3c's modifier **shadow** does NOT depend on L7. Shadow reads
`_modifierState` directly; ClearAll clears CommandBus channels, not
`_modifierState`. Shadow would not diverge regardless of which option lands.

**Recommendation**: defer L7 fix to Phase 4 (when first adapter is actually
written and can be audited together with the fix shape). Land 3c shadow
without touching Switcher. L7 remains a watchpoint, NOT a 3c blocker.

If the user wants to land L7 in 3c for cleanliness: **Option B** is the most
future-proof — it treats the Switcher double-fire as accepted and puts the
invariant on the adapter side where it belongs. But again, recommend defer.

### Does 3c shadow need to tolerate ClearAll burst?

No. `_modifierState` is a motor field, NOT a CommandBus channel. ClearAll
wipes bus channels (impulse / teleport / modifier / handoff command streams
for Phase 4 adapter → motor transit). Motor's `_modifierState` is separate
memory updated only by `TryApplyModifierCommand` (motor.cs:1383), teleport
reset (motor.cs:1527), or handoff merge (motor.cs:1625-1627). ClearAll on
spawn leaves `_modifierState` at `default` (zero-init struct), which is the
correct initial state anyway.

---

## §4 — V2 / V5 Scope Recommendation

### V1 (compile + editor load)
Required. Same as 3a/3b — confirm no release-build contamination, confirm
Unity Editor loads assembly.

### V2 (host-only 30s playtest)
**Sufficient for primary coverage**.

Scenario: drive + cast at least one modifier-bearing skill (acceleration,
root, scale, invert-turn, or steering-suppression).

PASS criteria per L13:
  - 0 non-heartbeat `[D-LOC]` warnings
  - 0 `[D-LOC FATAL]`
  - `mod-div = 0` on every heartbeat
  - `mod-compared > 0` at session tail
  - `loc-div / imp-div / tel-div = 0` (3a/3b parity preserved)

**Expected behavior**: `mod-compared` increments every active tick (Resolve
at motor.cs:341 runs unconditionally in RunInputs when ShouldRunPrediction
passes). Counter should grow fast — within ~2s of gameplay, `mod-compared`
will be in the hundreds.

**Skill selection for coverage breadth** (desirable, not gate-required):
  - Acceleration skill — exercises `state.AccelUntilTick` + `AccelExtra*`
    branches (lines 40-44 of resolver).
  - Root skill — exercises `state.RootUntilTick` branch (lines 55-59).
  - Scale skill — exercises scale branches (lines 30-38) — also exercises
    the multiplicative chain into FinalTurnTorque / FinalForwardForce.
  - Invert-turn + steering-suppression — flag-only branches (cheap coverage).

### V5 (2-peer 60s playtest)
**Recommended**.

Rationale: reconcile-replay path at motor.cs:464-465 overwrites
`_computedStats` from serialized reconcile data. Shadow running on client
reconcile-replay ticks exercises a distinct state-source (server-sent
serialized struct vs local Resolve output). This is where a **serialization
truncation bug** would surface — data.ComputedStats reconciled from server
won't equal `Resolve(data.ModifierState, config, tick)` if serializer has
precision mismatch.

Host-only V2 cannot exercise this path (host is the serializer AND the
consumer — same precision on both sides).

PASS criteria same as V2, per peer.

Event budget:
  - Host + client both drive (locomotion active).
  - At least one modifier cast per peer (to cover `mod-compared >= 1`).
  - Cross-peer pushes AND respawns are NOT required for 3c — impulse queue
    and teleport slot don't affect modifier shadow (they affect
    `_modifierState.PushGraceUntilTick` via push-grace, but that's covered by
    any impulse; and teleport's ResetModifiers flag is a distinct code path
    worth exercising but not required for pass).

### Summary

| Gate | Scope | Required | Rationale |
|------|-------|----------|-----------|
| V1 | Compile | YES | Standard |
| V2 | Host-only 30s + 1 modifier cast | YES | Primary coverage |
| V5 | 2-peer 60s + per-peer modifier cast | YES | Reconcile-replay coverage |

---

## §5 — Open Decision Points for Reviewer

Before starting 3c implementation, confirm:

1. **Epsilon policy** — use 1e-4 (3a/3b convention) or 0 (bit-identical Resolve)?
   **Recommendation**: 1e-4 for consistency; effectively no-op but future-proof.

2. **ShadowComputedStats storage placement** — 12 new fields on `BuddahPredictionShadowScratch`
   (existing pattern: scratch carries shadow output), or compare in-place without
   scratch copy?
   **Recommendation**: Add `ShadowComputedStats` struct field (12 fields) to
   ShadowScratch. Motor compares scratch vs `_computedStats` in
   `Shadow_CompareAndReport`. Matches 3a/3b pattern.

3. **Snapshot timing** — shadow mirrors motor.cs:341 exclusively, or also
   mirrors motor.cs:320 (pre-consume)?
   **Recommendation**: mirror 341 only. 320's result is discarded by motor at
   341; no gameplay observable effect other than ApplyResolvedMassMultiplier
   which shadow doesn't verify. Keep shadow minimal.

4. **L7 fix scope** — land in 3c, or defer to Phase 4?
   **Recommendation**: defer to Phase 4. 3c shadow doesn't depend on it; the
   fix is better designed alongside the first concrete adapter.

5. **Heartbeat format extension** — add `mod-div` + `mod-compared` to existing
   3-line heartbeat (potentially making line 2 or 3 long), or split into 4
   lines?
   **Recommendation**: extend existing 3-line format inline:
   ```
   [D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
     loc-div=<n> imp-div=<n> tel-div=<n> mod-div=<n>
     imp-compared=<n> tel-compared=<n> mod-compared=<n>
   ```

6. **Dead field `ShadowLastConsumedModifierId`** — keep as Phase 0 dead
   field for future-reservation, or remove now?
   **Recommendation**: keep. Zero cost, signals Phase 0 intent was considered
   and this phase intentionally chose a different design.

7. **TickContext `Config` field** — pass `BuddahPredictedMotorConfig`
   reference through TickContext, or have `BuddahModifierStep.Run` take
   `config` as a separate `in` parameter?
   **Recommendation**: add `Config` to TickContext. Consistent with other
   shadow steps that read everything from `ctx`. Trivial addition.

No code changes until these are resolved.
