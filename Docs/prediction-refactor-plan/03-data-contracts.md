# 03 - Data Contracts

[Back to index](index.md)

These are the canonical structs that flow through the tick pipeline. They are append-only - existing fields keep their wire layout for the migration window.

## BuddahPredictedInputData (Replicate)

| Field | Type | Source | Notes |
|---|---|---|---|
| `Steering` | `float` | owner sample | -1..1 |
| `Throttle` | `float` | owner sample | 0..1 |
| `MovementAllowed` | `bool` | gate adapter | snapshot at tick build |
| `OwnerInputLive` | `bool` | input adapter | false = ignore steering/throttle |
| `LastConsumedImpulseId` | `uint` | command bus | tick-aligned cursor |
| `LastConsumedTeleportId` | `uint` | command bus | tick-aligned cursor |
| `LastConsumedModifierId` | `uint` | command bus | tick-aligned cursor |
| `LastConsumedHandoffId` | `uint` | command bus | tick-aligned cursor |
| `OwnerControlMask` | `byte` | composition | intro / external-kinematic / launch-handoff bits |

## BuddahPredictedReconcileData (Reconcile)

| Field | Type | Why it must exist |
|---|---|---|
| `RbState` | `PredictionRigidbody` | engine-required snapshot |
| `Progress01` | `float` | drives respawn snap and reads in `[Replicate]` |
| `PushGraceRemaining` | `float` | extends MaxSpeed - read each tick |
| `ModifierStack` | `BuddahPredictedModifierStateNet` | active accel/scale/invert effects |
| `ImpulseQueueState` | `EventQueueSnapshot` | head/tail/lastConsumedId per channel |
| `TeleportQueueState` | `EventQueueSnapshot` | same |
| `ModifierQueueState` | `EventQueueSnapshot` | same |
| `HandoffQueueState` | `EventQueueSnapshot` | same |
| `IntroControlActive` | `bool` | owner-side block of input |
| `ExternalKinematicActive` | `bool` | suppresses force application |
| `LaunchHandoffPhase` | `byte` | `None / Pending / Inheriting / Blending / Done` |
| `LaunchInheritRemaining` | `float` | tick-driven timer |
| `LaunchBlendRemaining` | `float` | tick-driven timer |
| `RoomStateBypassRemaining` | `float` | tick-driven timer |
| `SuppressTurnInputRemaining` | `float` | tick-driven timer |
| `NextEventIdAlloc` | `uint` | so server reconciles owner's id allocator |

## EventQueueSnapshot
A 4-field struct describing one ring buffer per channel:

```
struct EventQueueSnapshot {
  uint LastConsumedId;   // last id we have applied
  uint NextSequence;     // next id we will allocate
  ushort Count;          // pending entries (re-derived)
  ushort RingHead;       // ring index of oldest pending
}
```

The actual payload entries live in a tick-buffered ring on the motor (not in reconcile data), but they are deterministically rebuildable from the cursors plus the source channel.

## Event Payloads

| Channel | Payload | Sender |
|---|---|---|
| Impulse | `Vector3 LinearImpulse, float TurnImpulse, byte SourceType, int SourceObjectId` | combat |
| Teleport | `Vector3 Pos, Quaternion Rot, float Progress01, byte Source, byte Flags` | respawn / intro / result |
| Modifier | `byte Kind, float Magnitude, float Duration, byte StackPolicy` | skill |
| Handoff | `LaunchHandoffSnapshot, float Inherit, float Blend, float Bypass, float SuppressTurn, byte Flags` | intro |

`Flags` packs booleans like `clearAngularVelocity`, `rebaseTrails`, `resetPushGrace`.

## Audit Rule (Phase 1 Gate)

Every private field on the current `BuddahPredictedMotor` whose name starts with `_` and which is read inside `[Replicate]` (directly or via a method called from `[Replicate]`) must move into `BuddahPredictedReconcileData` in Phase 1, with restoration inside `[Reconcile]`. Layer 0 audit identified the following currently-missing groups: pending teleport/handoff event payloads + their consumed-id cursors, `_awaitingAuthoritativeLaunchHandoff`, `_localPreHandoffBypassUntilTick`, and the impulse queue snapshot. None of these may stay on the motor as plain fields after Phase 1.

## Serialization Gate (Phase 1 Gate)

Adding fields to `IReconcileData` / `IReplicateData` without serializing them is silent - compile passes, owner-only playtest passes, then remote peers desync at runtime. For every field added in Phase 1:
1. Confirm the serialization path (auto-attribute vs hand-written `Write`/`Read`). If FishNet auto-generates, confirm the field type is supported and no `[System.NonSerialized]`-equivalent is suppressing it.
2. If hand-written, diff both Write and Read - every new field must appear in both.
3. Before signing Phase 1 gate, add a one-shot `Debug.Log` of one new reconcile field in `[Reconcile]`, run the game with a remote peer, and confirm a non-default value arrives on the remote. Remove the log after verification. Rationale in `Docs/lessons-log.md` L5.

See [05-event-channel.md](05-event-channel.md) for queue mechanics and [04-tick-pipeline.md](04-tick-pipeline.md) for consumption order.
