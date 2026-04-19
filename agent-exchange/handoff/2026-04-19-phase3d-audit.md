# Phase 3d Pre-Execution Audit — Handoff Shadow + (possibly) L12-shape fix for LaunchHandoff

Date: 2026-04-19
Branch: refactor/prediction-v2 (Phase 3c pushed as f16c9b0)
Scope: read-only. No code written. Findings for review before 3d implementation.

Files inspected:
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs`
    (handoff-related regions: 43-55, 92-97, 215-235, 279-301, 320-348,
     419-420, 456, 469-500, 523-533, 667-708, 791-994, 1027-1062, 1119-1140,
     1713-1823, 1826-1863, 1920-1923, 1996-2045)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs`
    (struct, 50 lines)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffState.cs`
    (struct, 54 lines — includes `FromData` derivation)
  - `Assets/Scripts/New_Buddah/Simulation/BuddahHandoffStep.cs`
    (Phase 0 stub, empty sealed class)
  - 3c audit + closeout (skeleton reuse)
  - `Docs/lessons-log.md` L12 + L13
  - `Docs/prediction-refactor-plan/phase-6-prerequisites.md`

---

## §0 — Body Inventory

### What is the handoff's "step" shape?

**Hybrid — single-slot consume (like teleport) PLUS state-resolve tick tail
(like modifier)**.

Two distinct code paths together:

1. **ConsumePendingLaunchHandoffEvent** (motor.cs:1713-1762) — single-slot
   consume. Gates on `_hasPendingLaunchHandoffEvent` + `StartTick <=
   currentTick`. On consume: clears pending flags, bumps
   `_lastConsumedLaunchHandoffEventId`, derives `_handoffState` via
   `BuddahPredictedLaunchHandoffState.FromData`, mutates rb pose + velocity
   + angular, mutates `_modifierState.SuppressSteeringUntilTick /
   RoomBypassUntilTick` via `Math.Max`, then calls `RefreshLaunchState`.
   Heavy: 14 physical / state mutations per consume.

2. **RefreshLaunchState** (motor.cs:1792-1824) — per-tick state resolver.
   Called unconditionally 4 times per RunInputs pass (pre-consume at 326,
   post-consume at 347, and nested inside `ApplyLaunchHandoffInputScaling`
   at 1828 + `ApplyLaunchInheritedVelocity` at 1850). Pure function of
   `_handoffState.InheritEndTick / BlendEndTick / StartTick` vs
   `currentTick`. Mutates `CurrentState`, `BlendAlpha`, `IsActive` in
   `_handoffState`. **Not queue-based and not state-derived like Modifier's
   Resolve — it's a tick-driven state machine advancer.**

### Inputs, outputs, mutations

| Function | Inputs read | Fields written | Pure? | Side effects |
|----------|-------------|----------------|-------|--------------|
| `ConsumePendingLaunchHandoffEvent` | `_hasPendingLaunchHandoffEvent`, `_pendingLaunchHandoffEvent`, `currentTick`, `rb.velocity` (for debug snapshot only) | `_pendingLaunchHandoffEvent` slot flags, `_awaitingAuthoritativeLaunchHandoff`, `_localPreHandoffBypassUntilTick`, `_lastConsumedLaunchHandoffEventId`, `_handoffState` (full), `_modifierState.SuppressSteeringUntilTick`, `_modifierState.RoomBypassUntilTick`, `_introControlActive`, `_externalKinematicControlActive`, `rb.isKinematic`, `rb.position`, `rb.rotation`, `rb.velocity`, `rb.angularVelocity`, `_splineProgressTracker` (external), `bootstrap.DebugState.*`, `RoomStateManager.Instance` (external). | No | Physical rb mutations + `rb.Sleep/WakeUp`, `ClearPendingForces`, `InitializePredictionRigidbody`, spline snap, debug logs, RoomStateManager gameplay-live report. |
| `AdjustLaunchHandoffForArrivalTick` | `eventData`, `currentTick`, `TimeManager.TickDelta`, `rb.velocity` (nope — snapshot-only). | `eventData` fields (StartTick, SnapshotPosition, SnapshotRotation, SnapshotForward) — returns modified copy. | **Yes (pure static-style)** — takes struct by value, returns struct; no field writes. | None. |
| `RefreshLaunchState` | `_handoffState` fields, `currentTick` | `_handoffState.CurrentState`, `.BlendAlpha`, `.IsActive` | Almost (mutates `_handoffState` but no external effects). | `bootstrap.LogVerbose` on transition (verbose log only). |
| `ApplyLaunchHandoffInputScaling` | `_handoffState`, `currentTick`, `ref throttle`, `ref steering` | `throttle`, `steering` (via `ref`) | Pure-style (calls `RefreshLaunchState` which mutates `_handoffState` — not truly pure). | Throttle/steering output mutation only. |
| `ApplyLaunchInheritedVelocity` | `_handoffState`, `rb.velocity`, `currentTick` | `rb.velocity` (via `SetPredictionVelocitiesSafely`), `bootstrap.DebugState.postHandoffSpeed` | No | Physical velocity mutation. |
| `BuddahPredictedLaunchHandoffState.FromData` | `BuddahPredictedLaunchHandoffData` | returns `BuddahPredictedLaunchHandoffState` | **Yes (pure static)** | None. |

### Hidden-branch inventory

1. **`AdjustLaunchHandoffForArrivalTick`** has float arithmetic:
   - `elapsedSeconds = staleTicks * TickDelta` — deterministic under
     integer × float with fixed tick delta.
   - `projectedPosition = SnapshotPosition + SnapshotVelocity * elapsedSeconds`
     — scalar multiply + Vector3 add. Deterministic.
   - `projectedRotation = ProjectRotationForward(SnapshotRotation, SnapshotAngularVelocity, elapsedSeconds)`:
     - Reads `angularVelocity.magnitude` — sqrt, floating-point.
     - `Vector3 axis = angularVelocity / angularSpeed` — scalar divide.
     - `angleDegrees = angularSpeed * elapsedSeconds * Mathf.Rad2Deg` — FP chain.
     - `Quaternion.AngleAxis(angleDegrees, axis) * rotation` — trigonometric
       sin/cos inside AngleAxis + quaternion multiply.
   - Unity's `Quaternion.AngleAxis` uses C++ sin/cos internally. Under the
     Unity 2022 Mono runtime on identical inputs, two calls produce
     bit-identical output (single-thread, no FMA fast-math flags). **Low
     but nonzero drift risk** if Unity's internal impl changes across
     editor/standalone builds.
   - `projectedForward.y = 0; Normalize()` — sqrt + divide. Same determinism
     assumption.
   - Shadow is safe to mirror if it runs the SAME function on the SAME
     snapshot struct. Epsilon 1e-4 (3c convention) handles any residual FP
     noise.
2. **`RefreshLaunchState`** branches on `InheritEndTick / BlendEndTick /
   StartTick` relative to `currentTick`. Pure integer comparisons →
   bit-deterministic. BlendAlpha computed via `(float)elapsedBlendTicks /
   blendTicks` + `Mathf.Clamp01` → deterministic.
3. **`ApplyLaunchHandoffInputScaling`** branches on `_handoffState.CurrentState`:
   - `Inherit`: throttle/steering hard-zero.
   - `Blend`: throttle/steering *= BlendAlpha.
   - `Normal`: no-op.
   Pure deterministic given state.
4. **`ConsumePendingLaunchHandoffEvent`** gate at line 1715-1719 uses
   purely boolean + uint comparison. Deterministic.

### Verdict on purity

- `FromData`: pure static.
- `AdjustLaunchHandoffForArrivalTick`: effectively pure (returns modified
  struct copy, no field writes).
- `RefreshLaunchState`: state-mutating but the mutations are bit-
  deterministic given identical `_handoffState` + `currentTick` inputs.
- `ApplyLaunchHandoffInputScaling` / `ApplyLaunchInheritedVelocity`:
  state-mutating PLUS physical (rb.velocity) mutations.

**Phase 3d shadow approach**: mirror only the derived STATE fields, not
the physical mutations. Shadow runs its own copy of `FromData` +
`AdjustLaunchHandoffForArrivalTick` + `RefreshLaunchState`-equivalent
logic on a struct-by-value snapshot of `_pendingLaunchHandoffEvent` and
`_handoffState`, produces its own `BuddahPredictedLaunchHandoffState`
shadow, compares 15 fields. Safe — no blocker from handoff body side.

---

## §1 — Call-Site Inventory

### Reads of `_handoffState` / `_pendingLaunchHandoffEvent` / related flags

| # | File:Line | Reader | What it reads | Purpose |
|---|-----------|--------|----------------|---------|
| 1 | motor.cs:92  | `IsLaunchHandoffActive` property | `_handoffState.IsActive` + other control flags | Public API — external queriers |
| 2 | motor.cs:96  | `IsAuthoritativeLaunchHandoffPending` property | `_awaitingAuthoritativeLaunchHandoff`, `_hasPendingLaunchHandoffEvent` | Public API for pending-auth |
| 3 | motor.cs:97  | `IsPredictionLaunchHandoffConsumedOrActive` | `_handoffState.IsActive` | Public API |
| 4 | motor.cs:215 | `CreateReconcile` | (indirect) via `IsLocalPreHandoffBypassActive(currentTick)` | `movementAllowed` reconcile gate |
| 5 | motor.cs:221 | `CreateReconcile` | `_handoffState` (struct by value) → serialized into reconcile data | Ground-truth broadcast to clients |
| 6 | motor.cs:279 | `BuildReplicateData` | `preHandoffBypassActive = IsLocalPreHandoffBypassActive(currentTick)` | `movementAllowed` input gate |
| 7 | motor.cs:326 | `RunInputs` pre-consume | (call) `RefreshLaunchState(currentTick)` — mutates `_handoffState` | Advance state machine before Resolve |
| 8 | motor.cs:345 | `RunInputs` | (call) `ConsumePendingLaunchHandoffEvent(currentTick)` | **Authoritative consume** |
| 9 | motor.cs:347 | `RunInputs` post-consume | (call) `RefreshLaunchState(currentTick)` — mutates `_handoffState` | Re-advance after consume |
| 10 | motor.cs:369 | `RunInputs` writer-relinquish | `authoritativePending = IsPredictionAuthoritativeHandoffPending()` | Relinquish-writer gate |
| 11 | motor.cs:419 | `RunInputs` input shaping | (call) `ApplyLaunchHandoffInputScaling(...)` | Zeroes/blends throttle+steering during Inherit/Blend |
| 12 | motor.cs:420 | `RunInputs` velocity inheritance | (call) `ApplyLaunchInheritedVelocity(currentTick)` | Overrides rb.velocity during Inherit/Blend |
| 13 | motor.cs:456 | `RunInputs` tail | (call) `UpdateHandoffDebug(currentTick)` | Debug state mirror |
| 14 | motor.cs:469 | `ReconcileState` | `authoritativePending = IsPredictionAuthoritativeHandoffPending()` | Transform-reconcile skip gate |
| 15 | motor.cs:489 | `ReconcileState` | Writes `_handoffState = data.HandoffState` | **Reconcile path — overwrites _handoffState** |
| 16 | motor.cs:496-500 | `ReconcileState` | `_ = data.PendingHandoff`, `_ = data.HasPendingHandoff`, etc. | **Dummy reads — handoff pending slot NOT reconciled** |
| 17 | motor.cs:523 | `ReconcileState` | (call) `UpdateHandoffDebug(data.GetTick())` | Debug state |
| 18 | motor.cs:555, 601 | `ShouldRelinquishPredictionWriterDuringIntroOrPending`, `ShouldSkipTransformReconcileDuringIntroOrPendingHandoff` | authoritative-pending reason | Control-flow gates |
| 19 | motor.cs:671 | `SetPredictionIntroControlActive` | (call) `RefreshLaunchState(...)` | Intro-off edge |
| 20 | motor.cs:686-688 | `SetPredictionExternalKinematicControlActive` | Writes `_handoffState = default`, `_awaitingAuthoritativeLaunchHandoff = false`, `_localPreHandoffBypassUntilTick = 0u` | **External-control edge clears handoff** |
| 21 | motor.cs:1754 | `ConsumePendingLaunchHandoffEvent` | (call) `RefreshLaunchState(currentTick)` | Initial advance after new `_handoffState` |
| 22 | motor.cs:2040-2045 | `UpdateHandoffDebug` | (call) `RefreshLaunchState(currentTick)` + reads `_handoffState` | Debug mirror |
| 23 | motor.cs:2102 | `BuildStatusSummary` (?) | `_handoffState.IsActive` | Debug summary line |

### Authoritative consume moment for shadow

**motor.cs:345** — `ConsumePendingLaunchHandoffEvent(currentTick)` inside
RunInputs. This is the single atomic moment where:
- Pending slot `_pendingLaunchHandoffEvent` is consumed (cleared).
- `_handoffState` is populated via `FromData`.
- `_modifierState` side-mutations applied.
- `RefreshLaunchState` runs (internal call at motor.cs:1754).

Shadow must snapshot **before** line 345 and compare **after** line 347
(the second `RefreshLaunchState` call at post-consume). Line 347 is the
parallel of 3c's motor.cs:348 for modifier — it's the post-consume,
pre-input-shaping state-advance moment.

### Reconcile-path overwrite — motor.cs:489

```csharp
_handoffState = data.HandoffState;
```

Server serializes `_handoffState` into reconcile data. Client receives
and overwrites its local `_handoffState`. **The pending slot
`_pendingLaunchHandoffEvent` is NOT reconciled** — only the derived
state. This means:

- On client reconcile-replay, after line 489, the next RunInputs runs
  `RefreshLaunchState` at 326 using the reconciled `_handoffState`,
  then `ConsumePendingLaunchHandoffEvent` at 345 which is a NO-OP if
  `_hasPendingLaunchHandoffEvent=false` (which it is after an earlier
  consume).
- Shadow replay behavior mirrors the same flow: snapshot of empty
  pending slot → `ConsumePendingLaunchHandoffEvent` does nothing →
  shadow compares the reconciled `_handoffState` against its own
  computed state (which on a replay tick with no new event, would
  be purely `RefreshLaunchState`-advanced from the same reconciled
  `_handoffState`). **Should match.**

**Phase 8 watchpoint parallel to Entry 1**: if `_handoffState`
serializes with different precision than the `_pendingLaunchHandoffEvent`
payload (which is NOT reconciled at all), the mismatch is structural not
numeric — shadow cannot observe it because the pending slot is
inaccessible post-reconcile. No new Phase 8 entry is needed unless
handoff *does* get a pending-slot serializer in Phase 4+.

---

## §2 — Shadow Design Sketch

### Shadow step name: `BuddahHandoffStep` (existing Phase 0 stub)

Similar to 3c: flesh out the existing stub. Pure-static `Run` method.

### Snapshot timing (pre-consume, before motor.cs:345)

Place the snapshot immediately BEFORE the call to
`ConsumePendingLaunchHandoffEvent` — analogous to 3b's
`_shadowPreTeleportEvent` snapshot. Can live in the same `#if` block at
motor.cs:330-342 alongside the existing 3b snapshots:

- `_shadowPreHandoffHasPending` (bool — `_hasPendingLaunchHandoffEvent`
  before consume)
- `_shadowPreHandoffEvent` (`BuddahPredictedLaunchHandoffData` — struct
  copy of `_pendingLaunchHandoffEvent`)
- `_shadowPreHandoffState` (`BuddahPredictedLaunchHandoffState` — struct
  copy of `_handoffState` BEFORE consume; needed because handoff
  already has a state even without a pending event)
- `_shadowPreAwaitingAuthoritative` (bool, for gate mirror —
  `_awaitingAuthoritativeLaunchHandoff` — if shadow wants to mirror the
  flag clearing behavior)

### Shadow Run (pseudo-sketch, not code)

```
BuddahHandoffStep.Run(in ctx, in input, ref scratch)
{
    // 1. Advance pre-consume state
    var shadowState = ctx.ShadowPreHandoffState;
    shadowState = RefreshLaunchStateShadow(shadowState, ctx.Tick);

    // 2. Consume if eligible
    if (ctx.ShadowPreHandoffHasPending
        && ctx.ShadowPreHandoffEvent.StartTick <= ctx.Tick)
    {
        var adjusted = AdjustLaunchHandoffForArrivalTickShadow(
            ctx.ShadowPreHandoffEvent, ctx.Tick, ctx.FixedDeltaTime);
        shadowState = BuddahPredictedLaunchHandoffState.FromData(adjusted);
        shadowState = RefreshLaunchStateShadow(shadowState, ctx.Tick);
        scratch.HandoffRan = true;
        scratch.ShadowLastConsumedHandoffId = adjusted.EventId; // DP6
                                                                 // candidate: if
                                                                 // reusable
    }

    scratch.ShadowHandoffState = shadowState;
}
```

`RefreshLaunchStateShadow` and `AdjustLaunchHandoffForArrivalTickShadow`
are static helpers on `BuddahHandoffStep` (not on motor). They mirror
motor.cs:1792-1824 and motor.cs:1764-1790 respectively, taking inputs by
value. Deferred question: **should we re-implement them in
`BuddahHandoffStep`, OR expose motor's private `AdjustLaunchHandoffForArrivalTick`
as `internal static` to call directly?**
- Re-implement (3c pattern): pure mirror, independence verified.
  Drift risk if motor's version changes.
- Expose `internal static`: zero duplication, motor's version IS the
  shadow's version. Parity-by-construction but loses the independent-
  computation guarantee.
- **Recommendation (not decided)**: re-implement. Keep parity-by-
  call-site-invariant model (3a/3b/3c consistent). Audit diff monthly.

### Compare fields (BuddahPredictedLaunchHandoffState has 15 fields)

| # | Field | Type | Epsilon | Rationale |
|---|-------|------|---------|-----------|
| 1 | IsActive | bool | exact | |
| 2 | EventId | uint | exact | |
| 3 | EventTick | uint | exact | |
| 4 | StartTick | uint | exact | |
| 5 | InheritEndTick | uint | exact | |
| 6 | BlendEndTick | uint | exact | |
| 7 | SuppressSteeringUntilTick | uint | exact | |
| 8 | RoomBypassUntilTick | uint | exact | |
| 9 | CurrentState (enum) | enum/int | exact | |
| 10 | BlendAlpha | float | 1e-4 | Clamp01 on integer ratio |
| 11 | SnapshotPosition | Vector3 | 1e-4 per axis (magnitude OK) | Projected via velocity*seconds |
| 12 | SnapshotRotation | Quaternion | Quaternion.Angle 1e-4 deg | ProjectRotationForward sin/cos |
| 13 | SnapshotVelocity | Vector3 | 1e-4 magnitude | Carried verbatim from eventData |
| 14 | SnapshotAngularVelocity | Vector3 | 1e-4 magnitude | Carried verbatim |
| 15 | SnapshotForward | Vector3 | 1e-4 magnitude | Projected + normalized |

Plus:
- `HandoffRan` gate flag (bool, exact) — match 3b pattern.
- Optional: track `_shadowLastConsumedHandoffId` in scratch (DP6 candidate;
  already has a dead field with this exact name).

### Heartbeat format

Current 3-line format:
```
[D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
  loc-div=<n> imp-div=<n> tel-div=<n> mod-div=<n>
  imp-compared=<n> tel-compared=<n> mod-compared=<n>
```

Extension options:

**Option A — squeeze into existing 3 lines** (8 counters, 2 per line):
```
[D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
  loc-div=<n> imp-div=<n> tel-div=<n> mod-div=<n> hof-div=<n>
  imp-compared=<n> tel-compared=<n> mod-compared=<n> hof-compared=<n>
```
Lines 2/3 grow; parseability slightly worse; digest parsers may need
update.

**Option B — split into 4 lines**:
```
[D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
  div: loc=<n> imp=<n> tel=<n> mod=<n> hof=<n>
  compared: imp=<n> tel=<n> mod=<n> hof=<n>
```
Compact labeling, consistent fan-out. Breaking change for 3c digest
parsers (they key off `loc-div=`, `imp-compared=`, etc.).

**Recommendation**: Option A (inline extension). Keeps the
`<cat>-div=<n>` / `<cat>-compared=<n>` tokens stable for every existing
digest parser and subagent-automation. Heartbeat line length remains
under ~150 chars.

### New D-LOC warning lines (proposed)

```
[D-LOC] T=<n> handoff-ran gate mismatch: real=<bool> shadow=<bool>
[D-LOC] T=<n> handoff-consume cursor mismatch: realId=<n> shadowId=<n>
[D-LOC] T=<n> handoff-state field delta: field=<name> real=<v> shadow=<v>
[D-LOC] T=<n> handoff-state flag mismatch: flag=<name> real=<v> shadow=<v>
[D-LOC] T=<n> handoff-pose delta: pos=<dpos> rot=<drot-deg> vel=<dvel>
```

---

## §3 — L12 Applicability Check (critical)

### Answer: **YES — structurally identical to teleport's L12**.

The client-initiated launch-handoff chain has the **exact same two-hop
RPC-with-silent-return pattern** that caused L12 in 3b:

| Step | Teleport (motor.cs:886-931 Phase 3b) | Launch-Handoff (motor.cs:791-840 current) |
|------|---------------------------------------|-------------------------------------------|
| Owner entry | `RequestAuthoritativeTeleportFromOwner` → if `IsServerInitialized` route to direct else send ServerRpc, unconditionally `return true`. | `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:791) → if `IsServerInitialized` route to direct else send ServerRpc, **unconditionally `return true` at motor.cs:839**. |
| ServerRpc | `RequestTeleportServerRpc` (motor.cs:997) `[ServerRpc(RequireOwnership = true)]` → calls `TryApplyServerAuthoritativeTeleport`. | `RequestLaunchHandoffServerRpc` (motor.cs:957) `[ServerRpc(RequireOwnership = true)]` → calls `TryApplyServerAuthoritativeLaunchHandoff`. |
| Server enqueue | `TryApplyServerAuthoritativeTeleport` → `TryQueueTeleportEvent` + `QueueTeleportEventTargetRpc`. Returns enqueue result. | `TryApplyServerAuthoritativeLaunchHandoff` (motor.cs:842) → `TryQueueLaunchHandoffEvent` + `QueueLaunchHandoffTargetRpc`. Returns `queuedOnServer`. |
| TargetRpc | `QueueTeleportEventTargetRpc` (motor.cs:1065) → `TryQueueTeleportEvent` on owner. | `QueueLaunchHandoffTargetRpc` (motor.cs:1027) → `TryQueueLaunchHandoffEvent` on owner. |
| Silent-drop surfaces | ServerRpc outbound OR TargetRpc return — neither acked. Owner's `return true` at line 930 is unconditional. | ServerRpc outbound OR TargetRpc return — neither acked. Owner's `return true` at motor.cs:839 is unconditional. |
| Visual fallback | FishNet reconcile broadcasts post-teleport rb pose — user sees respawn despite prediction-queue drop. | FishNet reconcile broadcasts `_handoffState` AND server mutates rb pose on server-direct path → client sees reconciled pose/velocity after some delay. User sees launch despite prediction-queue drop on client. |
| Shadow signature | `tel-compared = 0` on CLIENT for client-initiated teleport. | **Predicted**: `hof-compared = 0` on CLIENT for client-initiated launch-handoff. |

### V5 2-peer prediction for client-initiated launch-handoff

**Expectation**: CLIENT `hof-compared = 0` while HOST `hof-compared >= 1`,
in any scenario where client initiates the launch-handoff. HOST sees
server-direct enqueue + consume, CLIENT's TargetRpc return may or may
not arrive; even if it does, the owner's `_pendingLaunchHandoffEvent`
slot may be cleared by a reconcile-driven event consumption before the
shadow observes it.

This is the exact L12 shape. No new lesson needed — it reuses L12 verbatim,
only the event category changes from teleport to handoff.

### Decision path options (NOT decided — flag §5-4 for reviewer)

#### Option X — Defer L12 fix to Phase 6 (alongside teleport fix)

Pros:
- Smaller Phase 3d scope. 3d PR is pure observation-only, mirrors 3c.
- L12 fix shape (result-TargetRpc + commit-gate) is easier to reason
  about when designed for ONE category at a time (teleport first,
  then handoff as follow-up).
- Phase 3d V5 CLIENT `hof-compared=0` becomes the proving trace for
  the handoff-side L12 (parallel to how V5 R2 CLIENT `tel-compared=0`
  proved the teleport-side L12).
- Parity-by-call-site-invariant gate model stays clean —
  observation-only phase.

Cons:
- Phase 6 scope grows: teleport cut-over AND handoff L12 fix in the
  same PR, or Phase 6 splits into 6a/6b.
- Handoff L12 remains latent for longer. If a gameplay regression
  correlates with handoff silent-drop, we can't close it until
  Phase 6+.

#### Option Y — Fix handoff L12 in Phase 3d

Pros:
- Closes TWO L12-shape bugs in one authoring pass (teleport and
  handoff get the same result-TargetRpc pattern at motor layer).
- Phase 3d delivers both shadow + authority-path fix. Tighter Phase
  6 scope later.
- L13 gate rewording: "CLIENT hof-compared >= 1 for client-initiated
  handoff" becomes a concrete pass gate for 3d V5.

Cons:
- Phase 3d stops being pure observation-only — it adds authority-path
  code (new TargetRpc result return). Breaks the 3a/3b/3c append-only
  promise.
- Risk: a half-finished L12 fix in 3d might interact with the still-
  pending teleport L12 fix in Phase 6, producing two different
  result-TargetRpc patterns that need reconciling later.
- Blast radius: new code on owner-server-owner flow. V5 coverage
  gate must verify the fix under adverse network conditions —
  which our V5 harness doesn't simulate (real Steam lobby, single
  LAN pair).

#### Option Z — Split the difference (not a clean option, flagging)

Phase 3d lands handoff shadow + a DIAGNOSTIC-ONLY logging addition to
the silent-return paths (not a fix, just visibility). Phase 6 lands
the actual fix for both. Risks cluttering the motor with disposable
logging.

---

## §4 — V2 + V5 Scope

### V2 host-only — what triggers server-direct handoff enqueue?

`RequestAuthoritativeLaunchHandoffFromOwner` at motor.cs:791 with
`IsServerInitialized=true` routes to `TryApplyServerAuthoritativeLaunchHandoff`
directly (motor.cs:807-815). This bypasses the ServerRpc hop entirely —
it's the host-owner code path.

**What triggers it in gameplay?**
- The launch-handoff is called at intro-to-gameplay transition — when
  the race intro sequence ends and gameplay control hands off to the
  predicted motor. Search term: `LaunchHandoffSnapshot` caller.
- Scenario: HOST plays the intro → intro finishes → host-owned Buddah
  calls `RequestAuthoritativeLaunchHandoffFromOwner` with
  `IsServerInitialized=true` → server-direct enqueue → `hof-compared
  >= 1` on HOST within ~2 seconds of gameplay start.

**V2 PASS gate**:
- `hof-div = 0` on every heartbeat
- `hof-compared >= 1` at session tail (proves consume path was
  exercised)
- `loc-div / imp-div / tel-div / mod-div = 0` (3a/3b/3c preserved)
- 0 non-heartbeat `[D-LOC]`
- 0 `[D-LOC FATAL]`

V2 scenario: HOST-only intro → let gameplay start → drive for ~10s →
optionally cast any skill (irrelevant to handoff). 30s total enough.

### V5 2-peer — what triggers client-initiated owner→server→owner chain?

Same intro-to-gameplay transition, but from the CLIENT peer. Client's
own Buddah runs intro locally on client's machine; when its intro
completes, CLIENT calls `RequestAuthoritativeLaunchHandoffFromOwner`
with `IsServerInitialized=false` → ServerRpc hop → TargetRpc return.

Expected per §3 L12 analysis:
- **HOST**: sees CLIENT's ServerRpc → server-direct enqueue →
  `hof-compared >= 1` on HOST for CLIENT's buddah (server-side
  observer of client-owned motor).
- **CLIENT**: `hof-compared` depends on whether TargetRpc return
  arrives AND whether pending slot survives to be observed by the
  shadow. Per L12 pattern, expect `hof-compared = 0` OR `>= 1` —
  outcome is the signal. If 0: L12-shape confirmed for handoff.

**V5 gate wording proposal (per L13 rule (a))**:

Pass path: **EITHER peer must have `hof-compared >= 1`, AND both peers
must have `hof-div = 0` on every heartbeat, AND no non-hb `[D-LOC]`,
AND no FATAL.**

Rationale: if we adopt Option X (defer L12 fix), CLIENT
`hof-compared = 0` is EXPECTED and does NOT fail the gate — it's the
observed L12 signature. HOST must have `hof-compared >= 1` to prove
the server-side enqueue path is live. Per-peer combination satisfies
the L13 "path exercised at least once per session" rule.

Pass path if Option Y: **BOTH peers must have `hof-compared >= 1`** —
because the fix is supposed to propagate the enqueue to the owner.
Failing this gate = fix is not working.

**V5 scenario**: HOST hosts race, CLIENT joins, both run intro, both
hit gameplay transition. ~60s window post-intro. No skills needed.

### V2 / V5 risk: intro trigger is session-start-only

Handoff fires once per Buddah per race. If the session's race ends
before V5 heartbeats stabilize, `hof-compared` could show `1` at
first heartbeat then never move — not a regression, just "event
category that fires at most once per session." Digest parsers should
not panic if `hof-compared` plateaus early.

---

## §5 — Open Decision Points for Reviewer

Before starting 3d implementation, confirm:

1. **Epsilon policy** — sweep 1e-4 for floats, exact for uints/bools/enums?
   Or a larger epsilon on `SnapshotRotation` (`Quaternion.Angle` compares
   degrees, not scalar)?
   **Recommendation**: sweep 1e-4, BlendAlpha @ 1e-4, Quaternion.Angle @
   1e-4 degrees. Consistent with 3c.

2. **ShadowScratch layout for handoff state** — add
   `ShadowHandoffState` (`BuddahPredictedLaunchHandoffState`, struct by
   value, 15 fields) plus a `HandoffRan` bool. Reuse existing
   `ShadowLastConsumedHandoffId` field (currently DP6-tagged as dead)
   OR leave it dead and add a fresh field?
   **Recommendation**: reuse the dead field — remove the DP6 tag if
   it's now live, and flag in commit that the Phase 0 design
   assumption of per-event cursor turned out to match the handoff
   category even though it was wrong for modifier. Saves a scratch
   field.

3. **Snapshot timing** — where in motor.cs to put the pre-consume
   snapshot?
   **Recommendation**: extend the existing 3b `#if` block at motor.cs:
   330-342 with handoff snapshot fields (`_shadowPreHandoffHasPending`,
   `_shadowPreHandoffEvent`, `_shadowPreHandoffState`). Parallel to the
   existing teleport pre-consume snapshot pattern.

4. **L12 fix scope** — Option X (defer to Phase 6), Option Y (fix in 3d),
   or Option Z (diagnostic-only logging in 3d)?
   **Recommendation**: Option X — defer. Pros/cons in §3. Phase 3d V5
   CLIENT `hof-compared=0` acts as the reproducer; Phase 6 fixes both
   teleport and handoff with the same result-TargetRpc pattern. Keeps
   3d append-only.

5. **Heartbeat format extension** — Option A (inline) or Option B
   (4-line split)?
   **Recommendation**: Option A (inline). Keeps existing digest parser
   keys intact; avoids churn in subagent parsing scripts.

6. **`ShadowLastConsumedHandoffId` handling** — see DP-2 above. Options:
   (a) reuse and remove DP6 tag, (b) keep DP6 tag but also add
   new `HandoffRan` gate (redundant but clean),
   (c) delete field, add fresh uint.
   **Recommendation**: (a) — reuse. Zero cost, preserves Phase 0
   intent for handoff.

7. **TickContext new field list** — exact fields to add:
   - `BuddahPredictedLaunchHandoffData ShadowPreHandoffEvent`
   - `bool ShadowPreHandoffHasPending`
   - `BuddahPredictedLaunchHandoffState ShadowPreHandoffState`
   - `bool ShadowPreAwaitingAuthoritative` (optional — mirror flag)

   Plus possibly a `uint PendingLaunchHandoffEventId` convenience,
   redundant with ShadowPreHandoffEvent.EventId.
   **Recommendation**: add the 3 required + skip the flag mirror (not
   needed for compare; the flag is pre-consume internal).

### Additional findings flagged during audit (not counted in the 7)

8. **FATAL message text update**: if 3d lands modDiverged/hofDiverged,
   the `_dLocConsecutive` FATAL log at motor.cs:1502 says "Phase 3c
   shadow formula out of sync". 3d should update this to "Phase 3d"
   OR drop the phase tag entirely ("shadow formula out of sync"). Flag
   for commit-message visibility; not a code-shape decision.

9. **anyRan extension**: must add `HandoffRan` to the `anyRan` gate
   at motor.cs:1250-1253. Parallel to 3c's ModifierRan extension.
   Cosmetic but required for idle-skip branch accuracy (though the
   idle branch has never fired since 3c landed, because modifier
   fires every active tick — handoff might actually re-enable the
   idle branch on sessions with no handoff at all, BUT handoff DOES
   fire once per intro, so it only delays the first heartbeat, not
   idle-skip reentry). Minor.

10. **`RefreshLaunchStateShadow` helper duplication risk**: if motor's
    `RefreshLaunchState` body changes in Phase 4+ (e.g., adds a new
    state), the shadow's re-implementation becomes stale. DP-A (not
    a new DP but a note): either comment in shadow step "// mirror
    motor.cs:1792-1824 — keep in sync" OR add an edit-time check
    (test? assertion?). Flag for reviewer.

11. **External kinematic control clears handoff (motor.cs:686-688)**:
    `SetPredictionExternalKinematicControlActive(true)` wipes
    `_handoffState = default`. Shadow must be aware: if external
    control activates mid-session, shadow's cached
    `_shadowHandoffState` should also be reset, OR shadow must re-snapshot
    from motor's (now-default) state at next RunInputs entry. Since
    shadow snapshots pre-consume EVERY RunInputs tick, auto-reset is
    correct — no special handling needed. Confirmed safe.

### Hard constraints observed

- No `.cs` file changed.
- No implementation started.
- Findings above — report back. No decisions taken on §5-1 through
  §5-7 or additional items 8-11.

No code changes until reviewer resolves decision points. Report when
audit reviewed.

---

## Addendum A (2026-04-19) — DP-8 Extract-resolver investigation

Reviewer flagged DP-8: §2 originally offered only two options — (a)
re-implement the helpers inside `BuddahHandoffStep`, or (b) expose
motor's private helpers as `internal static`. The audit missed option
(c): extract `AdjustLaunchHandoffForArrivalTick` + `RefreshLaunchState`
into a new pure-static resolver class (parallel to
`BuddahPredictedModifierResolver`), then have motor and shadow both
call it. Three diagnostic questions below, followed by a concrete
recommendation.

### Q1 — Does thin-wrapper refactor lose the verbose-log transition detection?

**No.** The current code at motor.cs:1792-1824:

```csharp
BuddahPredictedLaunchState previousState = _handoffState.CurrentState; // line 1801
// ... mutations of _handoffState ...
if (bootstrap != null && previousState != _handoffState.CurrentState)
    bootstrap.LogVerbose($"handoff state transition {previousState} -> {_handoffState.CurrentState} tick=... id=...");
```

Captures `previousState` BEFORE mutation, compares AFTER mutation. If we
refactor to:

```csharp
var advanced = BuddahPredictedLaunchHandoffResolver.Advance(_handoffState, currentTick);
if (bootstrap != null && _handoffState.CurrentState != advanced.CurrentState)
    bootstrap.LogVerbose($"handoff state transition {_handoffState.CurrentState} -> {advanced.CurrentState} tick={currentTick} id={_handoffState.EventId}");
_handoffState = advanced;
```

The transition detection is preserved — `_handoffState.CurrentState` IS
the "previous" value until the assignment. The log side effect stays in
motor; the resolver stays pure. Zero semantic loss.

Alternative: resolver could return `(state, transitioned)` tuple, cleaner
API but unnecessary complexity. Stick with compare-after-return.

### Q2 — `AdjustLaunchHandoffForArrivalTick` visibility + callers

**Current state**:
- Declared `private` at motor.cs:1764.
- **Exactly one caller**: motor.cs:1725 inside
  `ConsumePendingLaunchHandoffEvent`.
- Verified via grep — no other references in `Assets/Scripts/` outside
  the audit doc itself.

**Dependency chain** (what it reads from motor state):
- `GetElapsedSeconds(staleTicks)` at motor.cs:1901 — reads
  `TimeManager.TickDelta`.
- `ProjectRotationForward` at motor.cs:1909 — already `private static`,
  genuinely pure.
- No other motor state reads.

**Refactor shape** (for option c):
- Hoist `AdjustLaunchHandoffForArrivalTick` body into
  `BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick(data, currentTick, tickDeltaSeconds)` — parameterize the tick delta instead of reading it from `TimeManager`.
- Hoist `ProjectRotationForward` as a sibling pure helper on the
  resolver class (or keep private-static on motor and expose internal —
  minor call, shadow doesn't need it standalone).
- Motor's `ConsumePendingLaunchHandoffEvent` calls
  `Resolver.ProjectForArrivalTick(eventData, currentTick, (float)TimeManager.TickDelta)` — the single callsite change.

No other call sites disrupted. Refactor is strictly local.

### Q3 — Resolver namespace / location

`BuddahPredictedModifierResolver` lives at:
- Path: `Assets/Scripts/New_Buddah/Core/BuddahPredictedModifierResolver.cs`
- Namespace: `NewBuddah.PredictionV2.Core`

The new resolver should mirror this exactly:
- Path: `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs`
- Namespace: `NewBuddah.PredictionV2.Core`

Single public static class with two methods:
- `public static BuddahPredictedLaunchHandoffState Advance(BuddahPredictedLaunchHandoffState state, uint currentTick)` — mirrors motor.cs:1792-1824 body, returns advanced struct (no log side-effect; motor wraps).
- `public static BuddahPredictedLaunchHandoffData ProjectForArrivalTick(BuddahPredictedLaunchHandoffData eventData, uint currentTick, float tickDeltaSeconds)` — mirrors motor.cs:1764-1790 body.

Potentially also `public static BuddahPredictedLaunchHandoffState FromData(BuddahPredictedLaunchHandoffData data)` if we want to consolidate — BUT `FromData` already lives as a static method on the `BuddahPredictedLaunchHandoffState` struct itself (BuddahPredictedLaunchHandoffState.cs:23). That's already pure + callable. Leave it where it is; resolver just calls it.

### Recommendation

**Option (c) — extract resolver.**

Rationale:
1. **Parity-by-construction with 3c**: motor and shadow call the SAME
   `BuddahPredictedLaunchHandoffResolver.Advance` method. Bit-identical
   output guaranteed by compiler; no independent re-implementation drift
   risk (kills audit note #10 before it becomes a real problem).
2. **Smaller post-3d audit surface**: no "keep in sync" comment to
   maintain; no "audit diff monthly" discipline. Drift literally
   impossible.
3. **Refactor is tiny**:
   - New file: `BuddahPredictedLaunchHandoffResolver.cs` (~60 lines — ~32
     from RefreshLaunchState + ~28 from AdjustLaunchHandoffForArrivalTick).
   - Motor edits: 3 sites — replace body of `RefreshLaunchState` (line
     1792-1824) with 3-line wrapper; replace body of
     `AdjustLaunchHandoffForArrivalTick` (line 1764-1790) with 2-line
     wrapper (or delete entirely and inline the resolver call at
     motor.cs:1725); move `ProjectRotationForward` body to the resolver.
4. **Transition verbose-log preserved** (Q1).
5. **Single callsite for AdjustLaunchHandoff** (Q2) — zero ripple.
6. **Namespace alignment trivial** (Q3).

### Tradeoffs of option (c) vs (a) re-implement

| Dimension | (a) Re-implement | (c) Extract resolver |
|-----------|------------------|----------------------|
| Motor 3d touch scope | Append-only shadow hook + fields | Append-only + 3-site body refactor (behavior preserved) |
| Drift risk | Monthly audit needed | Zero (compiler-enforced) |
| LOC delta | +~60 in BuddahHandoffStep + ~80 motor append | +~60 in resolver + ~10 motor append (thinner wrapper) + ~20 motor deltas at 3 body sites |
| Purity-verification cost | Independent re-impl → needs manual parity check | Shared code → trivially parity |
| L6 "append-only observation" doctrine | Strictly observed | Technically a local refactor to make extraction possible, BUT the refactor is behavior-preserving and the motor's net observable effect (calling same math) is unchanged. Reviewer judgment: is this a doctrine violation? |

### Doctrine note for reviewer

Option (c) does narrow the "append-only observation" claim that 3a/3b/3c
maintained. The behavior-preserving thin-wrapper refactor is safer than
it sounds (single caller, pure math, no RPC or network code involved),
and the commit message can explicitly carve out "Phase 3d lands a
behavior-neutral extraction of handoff resolver logic to enable the
shadow's parity-by-construction guarantee" as a principled exception. If
the reviewer wants to keep 3d strictly append-only, fall back to (a) —
at the cost of monthly drift audits.

My lean: (c), for the reasons above. User stated lean is (c) as well —
findings confirm it's the right call.

---

## Addendum B (2026-04-19) — Reconcile data dummy-read diagnostic

Audit §1 item 16 flagged that motor.cs:493-501 (now 493-501 after Phase
3c line-drift; also partners with motor.cs:228-236) contain `_ =
data.PendingHandoff` style dummy reads. Reviewer asked for 3-line
diagnostic. Findings:

### B-1 — When are these fields written in `CreateReconcile`?

**They are NOT**. `CreateReconcile` at motor.cs:204-239:

1. Lines 217-226 (ctor, from commit `2c63bc53` 2026-04-15): populates
   9 fields — `RigidbodyState`, `ModifierState`, `ComputedStats`,
   `HandoffState`, `IntroControlActive`, `ExternalKinematicControlActive`,
   `MovementAllowed`, `PlanarSpeed`, `ServerForward`.
2. Lines 228-236 (from commit `f43cc1ec` 2026-04-18, **Phase 0+1**):
   EXPLICIT default-assign of 9 fields — `PendingTeleport`,
   `HasPendingTeleport`, `LastConsumedTeleportId`, `PendingHandoff`,
   `HasPendingHandoff`, `LastConsumedHandoffId`,
   `AwaitingAuthoritativeLaunchHandoff`, `LocalPreHandoffBypassUntilTick`,
   `ImpulseQueueState`.

So on the **writer side**, these fields are always default/zero — server
never sends real values to client.

### B-2 — Git blame

- **Writer-side default-assign (motor.cs:228-236)**: `f43cc1ec`
  (Yonezawa-Akane, 2026-04-18) — "refactor: Phase 0+1 prediction v2
  scaffolding + data contracts".
- **Reader-side dummy reads (motor.cs:493-501)**: same commit `f43cc1ec`,
  same author, same day.
- **Reader-side real applies (motor.cs:487-491)**: older commit
  `2c63bc53` (Yonezawa-Akane, 2026-04-15) — "Stabilize Buddah intro
  handoff and spline movement". Those apply `_modifierState`,
  `_computedStats`, `_handoffState`, `_introControlActive`,
  `_externalKinematicControlActive` — the fields that ARE wired.

Phase 0+1 added the shape (struct fields + writer-side default-assigns +
reader-side dummy reads) as a single coordinated commit. The `_ = ...`
idiom suppresses CS0219 / CS8321 "never used" warnings while the data
contract waits for future-phase wire-up.

### B-3 — Classification

**(iii) Intentional retention — shape-only reservation for future phases.**

Supporting evidence:
- Writer AND reader sides are BOTH dummy/default. No data flows.
- The commit message and Docs/lessons-log.md L6 explicitly document this
  pattern: "Phase 0/1 deliberately zero-inits new reconcile fields and
  does NOT mirror motor private state... that mirroring is Phase 2/3
  work".
- The asymmetry is not handoff-specific — teleport has the exact same
  pattern (`PendingTeleport`, `HasPendingTeleport`,
  `LastConsumedTeleportId` all zeroed on write, dummy-read on apply),
  and `ImpulseQueueState` too. This is systemic scaffolding, not
  handoff-specific dead code.

NOT (i) dead serialization: the fields WILL be populated by a future
phase. NOT (ii) missed-apply bug: there is nothing to apply yet because
the server never writes real data.

### B-4 — Does this fold into Phase 6 L12 fix scope?

**Maybe — design choice, not required.**

The L12 fix as currently scoped in
`Docs/prediction-refactor-plan/phase-6-prerequisites.md` Prereq-1 uses
an **RPC-level commit-gate**: a result-TargetRpc that propagates the
server-side enqueue outcome back to the owner. This does NOT require the
reconcile-side `PendingTeleport` / `PendingHandoff` fields to become
live — the ack is its own independent channel.

However, an **alternative L12 fix shape** exists: instead of a
result-TargetRpc, **have the server serialize its
`_pendingTeleportEvent` / `_pendingLaunchHandoffEvent` slot into the
reconcile data**, and have the client's `ReconcileState` apply
those fields into its own `_pendingTeleportEvent` /
`_pendingLaunchHandoffEvent` slot. This would:
- Activate the dummy fields at motor.cs:228-236 + motor.cs:493-501 for
  real data flow.
- Replace "client owns a separate pending queue with delivery-by-TargetRpc"
  with "server is the source of truth; client's queue is
  reconcile-reconstructed".
- Align naturally with FishNet's prediction model (server state is
  ground truth; client rolls back and replays from server snapshots).

Tradeoffs:
- **Reconcile-delivery approach**: higher bandwidth (every reconcile
  tick carries pending-queue shape even when empty), but unifies the
  trust model. No per-event ack tracking. Dummy fields become live.
- **Result-TargetRpc approach** (current Phase 6 plan): lower
  bandwidth, simpler state machine, but owner/server maintain
  independent pending slots that need manual reconciliation via acks.

**Recommendation for Phase 6 planner (not a decision here)**: add the
reconcile-delivery approach as an explicit "Option B" in the
phase-6-prerequisites.md Prereq-1 fix-shape options. The dummy-field
scaffolding is a free win IF the planner adopts it. If not, keep
dummy-read pattern as documented "reserved for possible future wire-up"
per L6 doctrine.

**Does NOT block Phase 3d** — Phase 3d shadow works against motor's
local `_handoffState` regardless of which Phase 6 fix shape lands.

### Meta-note for audit consumers

This addendum's B classification is NOT a bug report. The dummy-read
pattern is L6-compliant scaffolding, working as designed. The only
action item is the architectural flag for Phase 6 planning. If the
Phase 6 author prefers the current result-TargetRpc plan, the dummy
fields can be removed in Phase 8 cleanup (add as a Phase 8 cleanup-queue
entry candidate at that time — not now, since the decision is still
pending).

---

## Addendum C — DP-8 refactor behavior-neutrality diff (2026-04-19, post-V2 FAIL)

**Context**: Phase 3d V2 host-only digest reported sustained `hof-div=120`
for ~7 heartbeats during the intro→gameplay pre-consume window, then
instant convergence to 0 exactly when handoff consumed (hof-compared
0→1). Reviewer directed: **(d) first — side-by-side diff of pre-3d
motor RefreshLaunchState vs new Resolver.Advance vs new motor thin
wrapper. Verify behavior-neutrality before re-running.**

**Compared revisions**:
- Pre-3d baseline: commit `f16c9b0` (Phase 3c merge), motor.cs:1764-1824
  (`AdjustLaunchHandoffForArrivalTick` + `RefreshLaunchState` +
  `GetElapsedSeconds` + `ProjectRotationForward`).
- Current (3d V1 uncommitted): `BuddahPredictedLaunchHandoffResolver.cs`
  (new) + motor.cs:1930-1940 (thin wrapper) + motor.cs:1874-1887
  (consume-site inlined verbose log).

### C.1 — Three-column side-by-side: `RefreshLaunchState`

**Pre-3d in-place body** (f16c9b0 motor.cs:1792-1824):
```csharp
private void RefreshLaunchState(uint currentTick)
{
    if (!_handoffState.IsActive)
    {
        _handoffState.CurrentState = BuddahPredictedLaunchState.Normal;
        _handoffState.BlendAlpha = 1f;
        return;
    }

    BuddahPredictedLaunchState previousState = _handoffState.CurrentState;

    if (_handoffState.InheritEndTick > currentTick && _handoffState.InheritEndTick > _handoffState.StartTick)
    {
        _handoffState.CurrentState = BuddahPredictedLaunchState.Inherit;
        _handoffState.BlendAlpha = 0f;
    }
    else if (_handoffState.BlendEndTick > currentTick && _handoffState.BlendEndTick > _handoffState.InheritEndTick)
    {
        _handoffState.CurrentState = BuddahPredictedLaunchState.Blend;
        uint blendTicks = _handoffState.BlendEndTick - _handoffState.InheritEndTick;
        uint elapsedBlendTicks = currentTick > _handoffState.InheritEndTick ? currentTick - _handoffState.InheritEndTick : 0u;
        _handoffState.BlendAlpha = blendTicks > 0u ? Mathf.Clamp01((float)elapsedBlendTicks / blendTicks) : 1f;
    }
    else
    {
        _handoffState.CurrentState = BuddahPredictedLaunchState.Normal;
        _handoffState.BlendAlpha = 1f;
        _handoffState.IsActive = false;
    }

    if (bootstrap != null && previousState != _handoffState.CurrentState)
        bootstrap.LogVerbose($"handoff state transition {previousState} -> {_handoffState.CurrentState} tick={currentTick} id={_handoffState.EventId}");
}
```

**Current `BuddahPredictedLaunchHandoffResolver.Advance`**:
```csharp
public static BuddahPredictedLaunchHandoffState Advance(
    BuddahPredictedLaunchHandoffState state, uint currentTick)
{
    if (!state.IsActive)
    {
        state.CurrentState = BuddahPredictedLaunchState.Normal;
        state.BlendAlpha = 1f;
        return state;
    }

    if (state.InheritEndTick > currentTick && state.InheritEndTick > state.StartTick)
    {
        state.CurrentState = BuddahPredictedLaunchState.Inherit;
        state.BlendAlpha = 0f;
    }
    else if (state.BlendEndTick > currentTick && state.BlendEndTick > state.InheritEndTick)
    {
        state.CurrentState = BuddahPredictedLaunchState.Blend;
        uint blendTicks = state.BlendEndTick - state.InheritEndTick;
        uint elapsedBlendTicks = currentTick > state.InheritEndTick
            ? currentTick - state.InheritEndTick : 0u;
        state.BlendAlpha = blendTicks > 0u ? Mathf.Clamp01((float)elapsedBlendTicks / blendTicks) : 1f;
    }
    else
    {
        state.CurrentState = BuddahPredictedLaunchState.Normal;
        state.BlendAlpha = 1f;
        state.IsActive = false;
    }

    return state;
}
```

**Current motor thin-wrapper** (motor.cs:1934-1940):
```csharp
private void RefreshLaunchState(uint currentTick)
{
    BuddahPredictedLaunchHandoffState advanced = BuddahPredictedLaunchHandoffResolver.Advance(_handoffState, currentTick);
    if (bootstrap != null && _handoffState.IsActive && _handoffState.CurrentState != advanced.CurrentState)
        bootstrap.LogVerbose($"handoff state transition {_handoffState.CurrentState} -> {advanced.CurrentState} tick={currentTick} id={_handoffState.EventId}");
    _handoffState = advanced;
}
```

### C.2 — Line-by-line equivalence table

| Pre-3d semantic | Resolver+Wrapper equivalent | Verdict | Notes |
|---|---|---|---|
| `!_handoffState.IsActive` → early exit after writing CurrentState=Normal, BlendAlpha=1f | Resolver writes to `state` copy, returns. Wrapper assigns `_handoffState = advanced`. Log gate: `_handoffState.IsActive` (pre-assignment value) is false → log skipped, matching pre-3d's early return. | ✅ | Struct copy-and-return assigns all fields back, but only `CurrentState`/`BlendAlpha` are modified — net effect same as pre-3d in-place writes. |
| `previousState = _handoffState.CurrentState;` capture before branch writes | Wrapper reads `_handoffState.CurrentState` at log-check time, which is BEFORE `_handoffState = advanced` assignment → same "before" value. | ✅ | Order of operations preserves capture semantics. |
| IsActive=true Inherit branch: writes CurrentState=Inherit, BlendAlpha=0f | Resolver identical writes to `state`, wrapper assigns back. | ✅ | Field set matches exactly. |
| IsActive=true Blend branch: writes CurrentState=Blend, BlendAlpha=clamp01(elapsed/total) | Resolver identical computation (same formula, same clamp, same 0u-fallback). | ✅ | uint arithmetic bit-identical single-thread. |
| IsActive=true Normal-deactivate branch: writes CurrentState=Normal, BlendAlpha=1f, IsActive=false | Resolver identical writes to `state`. | ✅ | Wrapper writes all three back via assignment. |
| Log condition: `bootstrap != null && previousState != _handoffState.CurrentState` (reached only on IsActive=true path — !IsActive branch returns before log) | Wrapper: `bootstrap != null && _handoffState.IsActive && _handoffState.CurrentState != advanced.CurrentState`. `_handoffState.IsActive` is pre-assignment value. | ✅ | Added `_handoffState.IsActive` gate is semantically equivalent to pre-3d's early-return-in-!IsActive-branch gating. |
| Log message uses `_handoffState.EventId` (post-mutation; but EventId unchanged by function) | Wrapper uses `_handoffState.EventId` (pre-assignment; same value since Advance doesn't write EventId) | ✅ | EventId invariant across both paths. |

### C.3 — Three-column side-by-side: `AdjustLaunchHandoffForArrivalTick` → `ProjectForArrivalTick`

**Pre-3d in-place** (f16c9b0 motor.cs:1764-1790 + motor.cs:1901-1908 GetElapsedSeconds + motor.cs:1910-1919 ProjectRotationForward):
- `if (currentTick <= eventData.StartTick) return eventData;` — early exit
- `staleTicks = currentTick - eventData.StartTick`
- `elapsedSeconds = GetElapsedSeconds(staleTicks)` → `TimeManager == null || staleTicks == 0u` → 0f, else `staleTicks * max(0.0001, TickDelta)`
- Project position/rotation/forward (same math)
- Inline `bootstrap?.LogVerbose("[HandoffDebug] stale handoff adjusted ...")` with `projectedPosition`
- Write `eventData.StartTick = currentTick`, etc.
- Return `eventData`

**Current `Resolver.ProjectForArrivalTick`**:
- Early-exit identical
- `staleTicks` identical
- `elapsedSeconds = tickDeltaSeconds > 0f ? staleTicks * max(0.0001f, tickDeltaSeconds) : 0f`
- Project identical
- NO bootstrap log (moved to caller)
- Field writes identical
- Return identical

**Current motor consume callsite** (motor.cs:1878-1890):
```csharp
uint preAdjustStartTick = eventData.StartTick;
float tickDeltaSeconds = TimeManager != null ? (float)TimeManager.TickDelta : 0f;
eventData = BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick(eventData, currentTick, tickDeltaSeconds);
if (bootstrap != null && eventData.StartTick != preAdjustStartTick)
{
    uint staleTicks = currentTick - preAdjustStartTick;
    bootstrap.LogVerbose($"[HandoffDebug] stale handoff adjusted eventId={eventData.EventId} staleTicks={staleTicks} oldStart={preAdjustStartTick} newStart={currentTick} projectedPos={eventData.SnapshotPosition}");
}
```

### C.4 — Line-by-line equivalence: projection + helpers

| Pre-3d semantic | Resolver equivalent | Verdict | Notes |
|---|---|---|---|
| `currentTick <= eventData.StartTick` → return eventData | Identical early-exit | ✅ | |
| `GetElapsedSeconds(staleTicks)` null-TM → 0f | `tickDeltaSeconds > 0f ? ... : 0f`, caller passes `TimeManager != null ? TickDelta : 0f` | ✅ | null-TM → tickDeltaSeconds=0 → returns 0f. |
| `GetElapsedSeconds(staleTicks)` durationTicks==0u → 0f | Inlined formula always runs once inside the `currentTick > eventData.StartTick` guard, so `staleTicks >= 1` always here | ✅ | Short-circuit was redundant in-context — safely eliminated. |
| `GetElapsedSeconds(staleTicks)` happy path → `staleTicks * max(0.0001, TickDelta)` | `staleTicks * max(0.0001f, tickDeltaSeconds)` | ✅ | Same constants, same multiplication. |
| `ProjectRotationForward` body (AngleAxis sin/cos) | Identical body, private to Resolver | ✅ | Bit-identical single-thread. |
| Inline bootstrap log at function body tail (after field writes, using `projectedPosition` = `eventData.SnapshotPosition`) | Caller-side log at motor.cs:1881-1887 after return, using `eventData.SnapshotPosition` (which is now the written projected value) | ✅ | Same string, same values. `eventData.StartTick != preAdjustStartTick` gate equivalent to pre-3d's "only logs if we ran past the early-exit" since Resolver only modifies StartTick past the early exit. |
| Field writes: `eventData.StartTick = currentTick; eventData.SnapshotPosition = ...; eventData.SnapshotRotation = ...; eventData.SnapshotForward = ...;` | Identical writes inside Resolver | ✅ | `SnapshotVelocity` / `SnapshotAngularVelocity` NOT overwritten (pre-3d preserves; Resolver preserves). |
| Return modified eventData | Return modified eventData | ✅ | |

### C.5 — Verdict: **CLEAN** (behavior-neutral)

DP-8 refactor introduces **no observable behavior change** in either
`RefreshLaunchState` or the projection/helper path. The Phase 3d V1
refactor is NOT the source of the sustained `hof-div=120` pre-consume
divergence. Step 1 produces **no candidate fix**.

### C.6 — Shadow field divergence shape in pre-consume window (what the 15-field compare sees)

The compare block at motor.cs:1535-1626 is gated on `anyRan`, not on
`_realScratch.HandoffRan` specifically — so it runs every tick that any
shadow step fired. In the pre-consume intro window (HasPending=true but
StartTick > currentTick, or HasPending=false awaiting the server RPC),
`_realScratch.HandoffRan == false` AND `_shadowScratch.HandoffRan ==
false` (both sides skip consume) — yet the 15-field `ShadowHandoffState`
comparison still runs.

Real-side captures `_realScratch.ShadowHandoffState = _handoffState` at
motor.cs:373, which is AFTER:
1. Line 335 pre-consume `RefreshLaunchState` Advance.
2. Line 356 `ConsumePendingTeleportEvent` (may write `_handoffState =
   default` at motor.cs:1806 if `eventData.ResetModifiers`).
3. Line 357 `ConsumePendingLaunchHandoffEvent` (may write `_handoffState
   = FromData(...)` at motor.cs:1892 if ripe).
4. Line 358 `ConsumePendingImpulseEvents` (no `_handoffState` write).
5. Line 359 post-consume `RefreshLaunchState` Advance.

Shadow-side snapshots `_shadowPreHandoffState = _handoffState` at
motor.cs:345, which is BEFORE (2)/(3)/(4)/(5) — so if ANY of those three
consumes fires in the window between line 345 and line 373, the shadow
runs `Advance` on the pre-consume snapshot while the real side captures
the post-consume state. Divergence guaranteed.

**BUT** the V2 digest's sustained pre-consume shape (7 heartbeats × 120
active-ticks = ~840 ticks) means consume is NOT firing each tick — the
divergence persists without any `HandoffRan` flip. This rules out
teleport-with-ResetModifiers bursts (which would be single-tick spikes
aligned with tel-compared increments) and rules out handoff consume
races (which would align with hof-compared increments — both were 0
across the entire pre-consume window per V2 digest).

**Remaining divergence hypotheses** (for Step 2/3 data):
1. `_handoffState` carries **non-default residual fields** from prior
   session state (EventId/SnapshotPosition/etc.) — snapshot and capture
   both see the same stale state → compare PASSES. Ruled out if hof-div
   > 0.
2. `_handoffState` is written **OUTSIDE RunInputs** between snapshot
   (line 345) and capture (line 373) — impossible within the same tick
   since OnTick / OnPostTick run sequentially on Unity main thread and
   RunInputs is called in OnTick. UNLESS FishNet reconcile-replay
   re-enters RunInputs inside the same Unity frame with a mid-frame
   reconcile. Host 1:1 ratio should rule this out but the inference
   relies on heartbeat-counter equality which isn't strict proof.
3. Some OTHER path writes `_handoffState` or the scratch slots between
   line 373 (real capture) and line 382 (BuddahHandoffStep.Run which
   writes shadow capture). Lines 374-381 are inside `#if
   BUDDAH_PREDICTION_SHADOW` and contain: stats mirror, modifier step,
   handoff step. No external `_handoffState` write.
4. `_hasPendingLaunchHandoffEvent` flips during those same mid-RunInputs
   lines — but no setter fires between 345 and 373 unless
   `ConsumePendingLaunchHandoffEvent` at line 357 actually runs (which
   would also set `HandoffRan=true` on real side, flipping the ran-gate,
   flagging as cursor mismatch instead of silent field-only divergence).

**Writers to `_handoffState` outside `RefreshLaunchState`** (catalogued
for Step 3 if needed):
- motor.cs:508 `ReconcileState` → `_handoffState = data.HandoffState` (called during FishNet reconcile, NOT inside RunInputs in host-only 1:1 path)
- motor.cs:705 `SetPredictionExternalKinematicControlActive(true)` → `_handoffState = default` (called from intro orchestrator, typically OUTSIDE RunInputs)
- motor.cs:1806 `ConsumePendingTeleportEvent` with `ResetModifiers=true` → `_handoffState = default` (INSIDE RunInputs between snapshot and capture — would cause single-tick divergence spike, not 120-tick sustain)
- motor.cs:1892 `ConsumePendingLaunchHandoffEvent` → `_handoffState = FromData(...)` (INSIDE RunInputs between snapshot and capture — real consume tick)

### C.7 — Candidate fix drafts (descriptions only, no code)

**Candidate A** (not needed per CLEAN verdict — included for completeness): revert DP-8 thin-wrapper to in-place body. Would NOT fix divergence since wrapper is behavior-neutral.

**Candidate B** (potential if Step 2 shows pre-consume residual-state divergence): reset `_handoffState` fields (EventId, Snapshot*, etc.) to default values when `_handoffState.IsActive` transitions to false, inside `Advance`'s Normal-deactivate branch. Would eliminate "stale residual field" carry-over across handoff sessions. Risk: may break consumers that read `_handoffState.EventId` post-expire (e.g., motor.cs:1922 debug log reads `_handoffState.CurrentState`, not fields — seems safe, but needs audit).

**Candidate C** (structural — aligned with Phase 6 Option B): move compare of the 15 ShadowHandoffState fields BEHIND the `_realScratch.HandoffRan || _shadowScratch.HandoffRan` gate, same shape as teleport compare at motor.cs:1361. Pre-consume window would skip field compare entirely; only consume-tick and active-state ticks would trigger the 15-field compare. This aligns handoff compare shape with teleport (3b) and avoids false-positive divergence on state-carry fields. Risk: may miss REAL divergence in the active-state (Blend/Inherit) tick sequence where HandoffRan=false but state is genuinely active. Mitigation: extend gate to `HandoffRan || _handoffState.IsActive` equivalent (or add an "active" scratch flag mirroring IsActive).

**Recommendation**: Step 1 produces no fix. Proceed to Step 2 (rerun V2 with
console-clear-logs + short window to capture per-field warnings). Per-field
data will localize the divergent field(s), which will select between B
and C.

### C.8 — Next step gate

Step 1 = CLEAN. Per reviewer directive ("如果 Step 1 发现 behavior change → 直接跳 fix。不需要 (a)。"), advance to **Step 2** — rerun V2 with `console-clear-logs` pre-run and a short playtest window to capture per-field `[D-LOC]` warnings before MCP cache evicts them. Step 3 (audit addendum D) remains a fallback if Step 2 data doesn't localize root cause.

---

End of addenda. No `.cs` files changed. Addendum C is diff-and-review
only — no Phase 3d implementation modified. Awaiting reviewer directive
to proceed to Step 2.

