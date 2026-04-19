# Phase 3b Pre-Execution Audit — Impulse + Teleport Shadow

Date: 2026-04-18
Branch: refactor/prediction-v2 (Phase 3a PR open, pending merge)
Scope: read-only. No code written. Findings to be reviewed before 3b implementation is authorized.
Files inspected:
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` (1823 lines)
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventData.cs`
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs`
  - `Assets/Scripts/New_Buddah/Core/BuddahPredictedTeleportEventData.cs`
  - `Assets/FishNet/Runtime/Object/Prediction/PredictionRigidbody.cs` (530 lines)

---

## 0. Tick-order invariant (critical for shadow wiring)

Inside `RunInputs` (motor.cs:287–411) the apply order is fixed:

```
reset scratches (#if shadow)
InitializePredictionRigidbody            (cold-path guard, idempotent if already init'd)
RefreshLaunchState + Resolve + ApplyMass
ConsumePendingTeleportEvent(currentTick) ← TELEPORT FIRST  (line 312)
ConsumePendingLaunchHandoffEvent(currentTick)
ConsumePendingImpulseEvents(currentTick) ← IMPULSE AFTER  (line 314)
RefreshLaunchState + Resolve + ApplyMass (second pass, state may have been wiped)
writer-relinquish gate / !MovementAllowed gate / IsRooted gate
ClampPlanarSpeed (with _shadowPreClampVelocity snapshot one line prior — 3a)
Locomotion commands (AddForce / AddTorque / AngularVelocity decay)
_predictionRigidbody.Simulate()          ← drains _pendingForces to PhysX
```

Two consequences for 3b:

**(T1)** Teleport-with-ResetImpulseQueue=true at tick N drops any impulses also queued for tick N (they are cleared before `ConsumePendingImpulseEvents` runs).

**(T2)** Teleport runs before `Resolve` is re-executed — so the post-teleport `_modifierState = default` change DOES affect that tick's ComputedStats (via the second Resolve call on line 316).

Shadow wiring MUST run Teleport step before Impulse step. Reversing the order will miss the queue-clear.

---

## 1. Impulse consume path — end-to-end

### 1.1 Event data carried into consume

`BuddahPredictedImpulseEventData` (Core/BuddahPredictedImpulseEventData.cs) — 7 fields:
  - `EventId` (uint)
  - `EventTick` (uint)
  - `Impulse` (Vector3, world-space)
  - `TurnTorqueImpulse` (float, about world-up)
  - `SourceType` (enum — for logging only; no motor-side behavior branches on it)
  - `SourceObjectId` (int — logging only)
  - `Consumed` (bool — flipped by queue, not read by motor)

**No magnitude cap. No direction rotate. No scale. No SourceType branching.** The event carries final world-space `Impulse` + final `TurnTorqueImpulse` already in the form the motor will consume. Upstream (`TryApplyServerAuthoritativeImpulse` motor.cs:663+) constructs these values; by the time the event enters the queue, transformations are baked.

### 1.2 Queue semantics (`BuddahPredictedImpulseEventQueue`)

`ConsumeReady(currentTick, predicate)` — motor.cs:1304 path:
  - Iterates `_pending` in REVERSE (`i = Count-1; i >= 0; i--`) — consumption is **LIFO within same-tick batch**.
  - Gate: `eventData.EventTick > currentTick` ⇒ skip (not consumed, stays in queue).
  - Gate: `predicate(eventData) == false` ⇒ skip (motor's lambda always returns true, so this never fires in current motor code).
  - On consume: sets `Consumed=true`, removes at index, pushes EventId into `_recentEventIds` (de-dup window of 64).

Dup guard: `TryEnqueue` rejects if EventId is either in `_recentEventIds` OR already present in `_pending`.

### 1.3 Inside the consume lambda (motor.cs:1309–1334)

Per event:
  1. Snapshot `rb.velocity` planar magnitude into `bootstrap.DebugState.preImpulseSpeed` (debug-only).
  2. Write 6 DebugState fields (EventId / Tick / SourceType / Vector / Torque / Consumed=true).
  3. Set `_impulseConsumedThisTick = true`.
  4. `rb.WakeUp()` — PhysX kick. Ensures sleeping body responds to force.
  5. **If `eventData.Impulse.sqrMagnitude > 0f`** → `_predictionRigidbody.AddForce(eventData.Impulse, ForceMode.Impulse)`.
  6. **If `Mathf.Abs(eventData.TurnTorqueImpulse) > 0.001f`** → `_predictionRigidbody.AddTorque(Vector3.up * eventData.TurnTorqueImpulse, ForceMode.Impulse)`.
  7. `ApplyPushGraceFromImpulse` → `_modifierState.PushGraceUntilTick = Max(current, tick + Ceil(PushGraceSeconds/TickDelta))`.

Post-loop (line 1336-1340): updates DebugState pendingCount / pendingSummary from remaining queue.

### 1.4 PhysX math the shadow must independently compute

`_predictionRigidbody.AddForce(impulse, ForceMode.Impulse)` queues to `_pendingForces` — nothing happens to `rb.velocity` yet (PredictionRigidbody.cs:318-322). `rb.velocity` only changes when `_predictionRigidbody.Simulate()` runs (same tick, line 407), which calls `Rigidbody.AddForce(v, ForceMode.Impulse)` → PhysX integrates:
  - `ForceMode.Impulse`: `Δv = impulse / mass` (one-shot, mass-weighted).
  - `ForceMode.Force` (the locomotion path): `Δv = force * fixedDeltaTime / mass` (per-step).
  - `ForceMode.Impulse` on torque: `Δω = torque / I` where I is inertia tensor (rotated into world). For `Vector3.up * torque` on a rigidbody whose inertia tensor is near-symmetric, the y-component dominates, but off-diagonal coupling exists and will drift if the body is tilted.

**Shadow force model**: since shadow captures commanded values at the call site (same pattern as 3a locomotion), the shadow does NOT need to re-run PhysX. Compare at the commanded-value layer:
  - Command: `AddForce(Impulse, Impulse-mode)` command equals shadow's reconstructed impulse.
  - Command: `AddTorque(Vector3.up * torque, Impulse-mode)` command equals shadow's reconstructed torque.

Velocity-delta compare is feasible but requires mirroring `rb.mass` + integrating Impulse-mode at the shadow level. Recommendation: defer velocity-delta compare to Phase 3c/3d; for 3b, gate on commanded impulse/torque deltas only (same "commanded-value compare" discipline that let 3a skip PhysX).

### 1.5 Intermediate transformations — summary for TickContext design

  | Stage                              | Transformation                                              | Shadow parameter required          |
  |------------------------------------|-------------------------------------------------------------|-------------------------------------|
  | Event enqueue (upstream)           | All scaling / direction-rotate already baked into `Impulse` | — (no work for 3b shadow)           |
  | Event consume gate                 | `EventTick <= currentTick`                                  | `currentTick` (already in TickCtx)  |
  | Impulse magnitude filter           | `sqrMagnitude > 0f`                                         | raw `Impulse` (pass-through)        |
  | Torque magnitude filter            | `|TurnTorqueImpulse| > 0.001f`                              | raw `TurnTorqueImpulse` (pass-through) |
  | PushGrace side effect              | writes `_modifierState.PushGraceUntilTick`                  | observable via post-consume `ComputedStats`, but shadow should NOT mirror writes in 3b — defer to 3c |
  | PhysX integration                  | `Simulate()` → mass-weighted velocity/angular deltas        | defer — 3b compares at commanded-value layer |

**Conclusion**: the impulse path is the easiest shadow case of the refactor. Zero intermediate transformations on the consume side. Shadow = "consume same events in same order against same cursor."

---

## 2. Teleport apply path — end-to-end

### 2.1 Event data carried into apply

`BuddahPredictedTeleportEventData` (Core/BuddahPredictedTeleportEventData.cs) — 14 fields:
  - `EventId`, `EventTick`, `Consumed` (queue plumbing — single-pending-slot, not a list)
  - `TargetPosition` (Vector3), `TargetRotation` (Quaternion), `TargetProgress01` (float)
  - `SourceType` (enum — logging only, no branch)
  - **Flags bits (7)** — each is a bool in the struct, not a packed flags-enum:
    1. `SnapProgress`          → snap spline tracker
    2. `ZeroLinearVelocity`    → zero rb.velocity + clear non-angular pending
    3. `ZeroAngularVelocity`   → zero rb.angularVelocity + clear angular pending
    4. `ResetModifiers`        → wipe `_modifierState`, `_handoffState`, `_hasPendingLaunchHandoffEvent`
    5. `ResetImpulseQueue`     → `_impulseEventQueue.Clear()` (also wipes `_recentEventIds`)
    6. `ResetPushGrace`        → `_modifierState.PushGraceUntilTick = 0u`
    7. `RebaseTrails`          → visual trail rebase (no physics impact)

### 2.2 Queue semantics

Single-slot, not a list:
  - `_hasPendingTeleportEvent` bool + `_pendingTeleportEvent` struct.
  - Enqueue overwrites (dup-guarded by `_lastConsumedTeleportEventId` + current-slot EventId — motor.cs:1057).
  - Consume gate: `_hasPendingTeleportEvent && _pendingTeleportEvent.EventTick <= currentTick`.
  - On consume: clears `_hasPendingTeleportEvent`, sets `_lastConsumedTeleportEventId`.

### 2.3 Apply body (motor.cs:1343–1427)

Ordered side effects, with attribution:

```
1. pre-state snapshot           → prePosition / preVelocity / preAngularSpeed (debug)
2. if (ResetModifiers)          → _modifierState = default
                                 → _handoffState = default
                                 → _hasPendingLaunchHandoffEvent = false
                                 → DebugState.modifiersClearedByTeleport = true
3. if (ResetImpulseQueue)       → _impulseEventQueue.Clear()
                                 → DebugState.impulseQueueClearedByTeleport = true
4. if (ResetPushGrace)          → _modifierState.PushGraceUntilTick = 0u
                                 → DebugState.pushGraceClearedByTeleport = true
5. UNCONDITIONAL                → _predictionRigidbody.ClearPendingForces()
                                  (wipes ALL pending AddForce/AddTorque — including
                                   locomotion commands if somehow queued already;
                                   in practice locomotion runs AFTER teleport so no
                                   conflict. But if launch-handoff step in between
                                   queued forces, they die here.)
6. if (ZeroLinearVelocity)      → rb.velocity = Vector3.zero        (direct write)
                                 → _predictionRigidbody.Velocity(Vector3.zero)
                                   (redundant write + RemoveForces(nonAngular:true))
7. if (ZeroAngularVelocity)     → rb.angularVelocity = Vector3.zero (direct write)
                                 → _predictionRigidbody.AngularVelocity(Vector3.zero)
                                   (redundant write + RemoveForces(nonAngular:false))
8. if (RebaseTrails)            → NotifyTeleportTrailRebases() [VISUAL, not physics]
                                 → DebugState.trailRebasedByTeleport = true
9. UNCONDITIONAL                → rb.position = TargetPosition     (direct)
                                 → rb.rotation = TargetRotation    (direct)
                                 → rb.Sleep(); rb.WakeUp()
                                 → InitializePredictionRigidbody()
                                    (re-inits PredictionRigidbody — this clears
                                     _pendingForces again via Initialize(rb)
                                     line 312. Redundant w/ step 5 but harmless.)
10. if (SnapProgress)           → _splineProgressTracker.SnapToTrackProgress(TargetProgress01)
                                 → DebugState.progressSnappedByTeleport = true
11. UNCONDITIONAL               → if (_skillExecutor != null && IsOwner)
                                     _skillExecutor.ResetActiveSkillEffectsForOwner()
                                    [SKILL-SYSTEM side effect; not physics]
12. UNCONDITIONAL               → _computedStats = Resolve(_modifierState, config, currentTick)
                                 → SyncModifierDebugState(currentTick)
                                 → UpdateTeleportDebugConsumed(...)
```

### 2.4 Flags bit inventory — shadow surface

| Flag                 | Physics side effect                                    | Shadow must observe? | Notes                                                    |
|----------------------|--------------------------------------------------------|----------------------|----------------------------------------------------------|
| `SnapProgress`       | none (spline tracker only)                             | NO                   | Not physics. Observation-only if at all.                 |
| `ZeroLinearVelocity` | writes rb.velocity=0, clears pending non-angular forces | YES                  | Mirror `ShadowPostTeleportVelocity` = Vector3.zero.       |
| `ZeroAngularVelocity`| writes rb.angularVelocity=0, clears pending angular forces | YES                | Mirror `ShadowPostTeleportAngular` = Vector3.zero.         |
| `ResetModifiers`     | resets `_modifierState`, `_handoffState`, handoff pending | DEFERRED to 3c      | Shadow compares the decision-to-reset boolean only.      |
| `ResetImpulseQueue`  | clears impulse pending + dedup window                  | YES                  | Mirror queue state for cursor compare on next tick.      |
| `ResetPushGrace`     | zeros `_modifierState.PushGraceUntilTick`              | DEFERRED to 3c       | Modifier state owned by 3c.                              |
| `RebaseTrails`       | none (visual)                                          | NO                   | Not physics. Skip entirely.                              |

**UNCONDITIONAL effects** (must be mirrored since they happen every teleport):
  - `ClearPendingForces` (step 5) — shadow should mark shadow-side pending-forces cleared.
  - `rb.position = TargetPosition`, `rb.rotation = TargetRotation` — mirror in `ShadowPostTeleportPosition / ShadowPostTeleportRotation`.
  - Sleep/WakeUp — not observable from shadow side; skip.
  - Second `InitializePredictionRigidbody` — re-clears pending forces; shadow should treat post-teleport pending-queue-state as "empty".

### 2.5 Independence gap callouts for 3b commit message

  1. **Spline progress snap** — shadow compares `SnapProgress` Flags read, NOT the spline tracker effect.
  2. **Trail rebase** — visual only, shadow ignores entirely.
  3. **Skill executor reset** — cross-system side effect; shadow compares the gate predicate (IsOwner && _skillExecutor != null), NOT the reset call.
  4. **Modifier / PushGrace resets** — 3c owns modifier shadow. 3b compares the Flags read and writes `ShadowPostModifierResetRequested = true` scratch; 3c closes by independently mirroring the resets.

---

## 3. PredictionRigidbody semantics — alignment with 3a model

Confirmed from `Assets/FishNet/Runtime/Object/Prediction/PredictionRigidbody.cs`:

### 3.1 Two write regimes — same model as 3a

**Queued (drained at Simulate):**
  - `AddForce(v, mode)`, `AddRelativeForce`, `AddTorque(v, mode)`, `AddRelativeTorque`
  - `AddExplosiveForce`, `AddForceAtPosition`
  - `MovePosition`, `MoveRotation`
  - All append an `EntryData` to `_pendingForces` (line 318-396). Nothing touches `rb.*` until `Simulate()`.
  - `Simulate()` (line 401-435): iterates `_pendingForces` in insertion order, forwards to `Rigidbody.AddForce` / `.AddTorque` / ... / `.MovePosition` / `.MoveRotation`. Then clears the list.

**Immediate (writes rb directly, clears matching pending):**
  - `Velocity(v)`: `Rigidbody.velocity = v` (2022 path) + `RemoveForces(nonAngular: true)` — wipes pending AddForce/AddRelativeForce/AddExplosiveForce entries, keeps torques and MovePosition/MoveRotation.
  - `AngularVelocity(v)`: `Rigidbody.angularVelocity = v` + `RemoveForces(nonAngular: false)` — wipes pending torques, keeps linear forces and Move*.
  - `ClearVelocities()`: both of the above.
  - `ClearPendingForces()`: `_pendingForces.Clear()` — wipes EVERYTHING regardless of type.
  - `ClearPendingForces(bool nonAngular)`: selective wipe (linear-only or angular-only).

**The 3a L8-B precedent holds for 3b**: any motor state that is *read during shadow gate or formula evaluation* AND *mutated immediately in the same tick* must be snapshotted at the read-point. In 3b, the candidates are:

  - `rb.position` before teleport body writes `rb.position = TargetPosition` → must snapshot to `_shadowPreTeleportPosition` if shadow wants a pre/post delta.
  - `rb.rotation` before teleport body writes `rb.rotation = TargetRotation` → same.
  - `rb.velocity` before teleport writes `rb.velocity = zero` (if ZeroLinearVelocity) → `_shadowPreTeleportVelocity` needed to reconstruct the zeroing decision.
  - `rb.velocity` before impulse AddForce runs (Impulse mode would change `rb.velocity` at Simulate, but PhysX owns the integration; snapshot only needed if shadow attempts velocity-delta compare — not recommended for 3b per §1.4).
  - `_impulseEventQueue` pre-consume snapshot for cursor/ordering check.
  - `_pendingTeleportEvent` + `_hasPendingTeleportEvent` pre-consume snapshot for cursor.

### 3.2 Impulse-specific semantics

`AddForce(impulse, ForceMode.Impulse)` from motor.cs:1325:
  - Queued in `_pendingForces`. NOT written to `rb.velocity` at call time.
  - Drained by `_predictionRigidbody.Simulate()` at line 407 — same tick, AFTER locomotion commands AND AFTER ClampPlanarSpeed.

**Interaction with ClampPlanarSpeed (V2 Run 1 bug territory)**:
  - Sequence: ConsumeImpulseEvents (queues AddForce Impulse) → ClampPlanarSpeed (reads `rb.velocity`, may call `Velocity(clampedVel)` which clears queued non-angular forces!).
  - Wait — this is a potential existing bug in motor: if an impulse is queued and ClampPlanarSpeed fires clamp in the same tick, the `Velocity(clampedVel)` call (PredictionRigidbody.cs:358, line 365 RemoveForces(nonAngular:true)) will strip the queued Impulse AddForce.
  - Check the actual code flow: `ConsumePendingImpulseEvents` runs at line 314, BEFORE the writer/movement/rooted early-returns, BEFORE `ClampPlanarSpeed` at line 351. So Impulse AddForce is queued, THEN clamp reads current `rb.velocity` (pre-Simulate, so Impulse hasn't applied yet — velocity is last tick's post-Simulate value). If clamp fires, it calls `Velocity(clampedVel)` which REMOVES the queued Impulse AddForce.
  - **This is a pre-existing behavior, not a bug the shadow should compensate for.** Shadow should match motor behavior bit-for-bit. If the motor eats impulses during clamp, the shadow must eat them too. Flag this as a potential L-entry subject if V5 surfaces anomalies, but do NOT try to "fix" it in 3b.

### 3.3 Reconcile path (FishNet machinery, not 3b shadow)

`Reconcile(PredictionRigidbody pr)` (line 466) clears `_pendingForces`, copies from `pr._pendingForces`, then calls `Rigidbody.SetState(pr.RigidbodyState)` to restore position/rotation/velocity/angular. During replay, `RunInputs` re-executes and re-queues forces. Shadow runs per-tick during replay the same way 3a does — no 3b-specific reconcile work needed.

---

## 4. Planned TickContext extension (proposal for review)

Additive to `BuddahPredictionTickContext` — NO existing field renamed/removed:

```csharp
// 3b additions — impulse/teleport snapshots
public readonly BuddahPredictedImpulseRingSnapshot ImpulseQueueStatePreConsume;
public readonly uint LastConsumedImpulseIdPreTick;
public readonly bool HasPendingTeleportPreConsume;
public readonly uint PendingTeleportEventIdPreConsume;
public readonly uint PendingTeleportEventTickPreConsume;
public readonly Vector3 TeleportTargetPositionPreConsume; // only valid when HasPendingTeleport
public readonly Quaternion TeleportTargetRotationPreConsume;
public readonly uint LastConsumedTeleportIdPreTick;
public readonly Vector3 RbPositionPreTeleport;
public readonly Quaternion RbRotationPreTeleport;
public readonly Vector3 RbVelocityPreTeleport; // distinct from 3a RbVelocityPreTick if teleport modifies velocity mid-tick
// Teleport Flags bits — snapshotted for gate comparison
public readonly bool TeleportFlag_ZeroLinearVelocity;
public readonly bool TeleportFlag_ZeroAngularVelocity;
public readonly bool TeleportFlag_ResetModifiers;
public readonly bool TeleportFlag_ResetImpulseQueue;
public readonly bool TeleportFlag_ResetPushGrace;
public readonly bool TeleportFlag_SnapProgress;
public readonly bool TeleportFlag_RebaseTrails;
```

Build site: `BuildTickContext` at motor.cs:1160 extended to snapshot these BEFORE `ConsumePendingTeleportEvent` / `ConsumePendingImpulseEvents` run. Current call site for `BuildTickContext` is pre-Simulate (post-consume) — this will need a second snapshot point pre-consume. Two options:

  - **Option A**: Split into `BuildTickContext_PreConsume` (called at motor.cs:~311, before line 312) + `BuildTickContext_PreSim` (current, at motor.cs:402). TickContext carries both snapshots.
  - **Option B**: Snapshot pre-consume state into standalone motor fields (`_shadowPreTeleportPending`, `_shadowPreImpulseRingSnapshot`, etc.) with the same discipline as `_shadowPreClampVelocity`; pass into single BuildTickContext.

Recommendation: **Option B**, matches 3a precedent for `_shadowPreClampVelocity` exactly. Fewer moving pieces in the build helper.

---

## 5. Planned ShadowScratch field activation

Already reserved in `BuddahPredictionShadowScratch.cs` (Phase 3a):
  - `ShadowLastConsumedImpulseId`
  - `ShadowLastConsumedTeleportId`
  - `ShadowLastConsumedModifierId` (3c)
  - `ShadowLastConsumedHandoffId` (3d)

New additions (proposal):
```csharp
public Vector3 ShadowPostImpulseVelocity;      // only if shadow attempts velocity-delta compare; otherwise drop
public Vector3 ShadowPostTeleportPosition;
public Quaternion ShadowPostTeleportRotation;
public bool ShadowImpulseRan;                  // gate-ran marker (LIFO consumed at least one event)
public bool ShadowTeleportRan;                 // gate-ran marker
public bool ShadowTeleportZeroedLinear;        // mirror of ZeroLinearVelocity branch decision
public bool ShadowTeleportZeroedAngular;       // mirror of ZeroAngularVelocity branch decision
public bool ShadowTeleportClearedImpulseQueue; // mirror of ResetImpulseQueue branch decision
```

Real side also needs two new flags on `_realScratch` to capture motor's actual branch-taken decisions (ImpulseRan, TeleportRan, TeleportZeroedLinear, etc.) — parallels 3a's `LocomotionRan` pattern.

---

## 6. Proposed gate mirror — impulse

`BuddahImpulseStep.Run`:
  - Gate: same as motor (no early-return; gate-by-call-site-invariant per 3a pattern). Motor invokes the step unconditionally inside RunInputs, so shadow is also unconditional.
  - Input: `ImpulseQueueStatePreConsume` snapshot + `currentTick`.
  - Body: walk snapshot in REVERSE (match motor LIFO), consume entries where `EventTick <= currentTick`, set `ShadowLastConsumedImpulseId = max(consumed EventIds)`, mark `ShadowImpulseRan = true` if any consumed.
  - Shadow does NOT apply impulse to any shadow rigidbody (no such thing). Compare at commanded-value layer: `ShadowLastConsumedImpulseId == _realScratch.LastConsumedImpulseIdAfterConsume`.

## 7. Proposed gate mirror — teleport

`BuddahTeleportStep.Run`:
  - Gate: same discipline.
  - Input: `HasPendingTeleportPreConsume` + `PendingTeleportEventTickPreConsume` + `currentTick` + flag snapshots.
  - Body:
    1. If `!HasPendingTeleportPreConsume` or `PendingTeleportEventTickPreConsume > currentTick` → `ShadowTeleportRan = false`, return.
    2. `ShadowTeleportRan = true`, `ShadowLastConsumedTeleportId = PendingTeleportEventIdPreConsume`.
    3. Copy flag reads into scratch (ShadowTeleportZeroedLinear = ctx.TeleportFlag_ZeroLinearVelocity, ...).
    4. `ShadowPostTeleportPosition = ctx.TeleportTargetPositionPreConsume`.
    5. `ShadowPostTeleportRotation = ctx.TeleportTargetRotationPreConsume`.
  - Compare: motor-side LastConsumedTeleportId, motor-side Flags-reads (captured into `_realScratch` at the branch sites), motor-side final rb.position/rotation = Target*.

---

## 8. New D-LOC lines (for heartbeat + fatal)

```
[D-LOC] T=<n> impulse-consume cursor mismatch: realId=<uint> shadowId=<uint>
[D-LOC] T=<n> impulse-ran gate mismatch: real=<bool> shadow=<bool>
[D-LOC] T=<n> teleport-consume cursor mismatch: realId=<uint> shadowId=<uint>
[D-LOC] T=<n> teleport-ran gate mismatch: real=<bool> shadow=<bool>
[D-LOC] T=<n> teleport-flag mismatch: flag=<name> real=<bool> shadow=<bool>
[D-LOC] T=<n> teleport-position delta=<float> over epsilon
[D-LOC] T=<n> teleport-rotation angular-delta-deg=<float> over epsilon
```

Heartbeat extension:
```
[D-LOC HEARTBEAT] T=<n> active-ticks=<n> skip-ticks=<n> loc-div=<n> imp-div=<n> tel-div=<n>
```

---

## 9. Risks flagged during audit

  1. **Impulse LIFO within same-tick batch**: motor's `ConsumeReady` walks `_pending` in reverse. Shadow MUST walk in reverse too. If shadow iterates forward over the snapshot, `ShadowLastConsumedImpulseId` will match (it's the max) but any per-event ordering check (if added later) will diverge.
  2. **Two `InitializePredictionRigidbody` calls**: once at RunInputs entry (line 299), again at teleport body end (line 1409). Second call only fires on teleport-consumed ticks. Shadow has nothing to mirror here — PredictionRigidbody is motor-owned. Document, don't duplicate.
  3. **Teleport+clamp+impulse same-tick**: pre-existing motor behavior where `ClampPlanarSpeed`'s `Velocity(clampedVel)` can strip a queued Impulse `AddForce`. Shadow must match bit-for-bit, NOT compensate. If V5 produces impulse-cursor divergence traceable to this, the shadow's cursor still marks the event consumed (it was consumed from the queue); only the velocity effect is lost. This is why §1.4 recommends commanded-value compare only.
  4. **No per-SourceType branching**: simplifies shadow, but means Shadow cannot surface "impulse from skill X applied differently than impulse from collision Y" distinctions. Fine for 3b scope.
  5. **ResetImpulseQueue at tick N wipes same-tick impulses**: teleport runs first in RunInputs. If shadow runs Teleport step before Impulse step (as §0 requires), `ShadowImpulseQueueStatePostTeleport` will be empty when ResetImpulseQueue=true. Sequence dependency is load-bearing.

---

## 10. Decision points for user review

Before I write 3b code, confirm:

  1. **Commanded-value compare only** for impulse (no PhysX velocity-delta)? Or should 3b attempt velocity-delta via mass snapshot?
  2. **Option B** (motor-side `_shadowPre*` fields + single BuildTickContext) over Option A (split pre-consume/pre-sim TickContexts)?
  3. **Flags-reads compare** for teleport — compare all 7 flags, or only the 4 that have physics effects (`ZeroLinear`, `ZeroAngular`, `ResetImpulseQueue`, `ResetPushGrace`)?
  4. **Independence gaps to defer**: OK to defer ResetModifiers + ResetPushGrace to 3c (they mutate modifier state, which 3c owns)?
  5. **New shadow files**: `BuddahImpulseStep.cs` + `BuddahTeleportStep.cs` as separate pure static steps (matches 3a one-file-per-step pattern), or fold into one `BuddahEventApplyStep.cs`? Recommendation: separate files — keeps diff review aligned with step boundaries.

No code changes until these are confirmed.
