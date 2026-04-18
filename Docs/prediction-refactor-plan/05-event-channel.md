# 05 - Event Channel

[Back to index](index.md)

The event channel turns external system writes into tick-aligned, replay-safe inputs. It is the single mechanism that makes prediction reconcile correctly under skill, combat, intro, and respawn pressure.

## One Channel per Effect Family

| Channel | File | Owner-side enqueue | Tick-side consume |
|---|---|---|---|
| Impulse | `BuddahPredictionEventChannel<ImpulseCmd>` | combat adapter | `BuddahImpulseStep` |
| Teleport | `...<TeleportCmd>` | respawn / intro / result adapter | `BuddahTeleportStep` |
| Modifier | `...<ModifierCmd>` | skill adapter | `BuddahModifierStep` |
| Handoff | `...<HandoffCmd>` | intro adapter | `BuddahHandoffStep` |

## Storage Layout

Each channel maintains:
1. A fixed-size ring buffer (default 64 entries; configured in `BuddahPredictionEventConfig`).
2. `NextSequence` allocated by the owner only.
3. `LastConsumedId` advanced inside `[Replicate]`.
4. A pending-replay shadow that is rebuilt during `[Reconcile]`.

Server runs the same channel - server channel `NextSequence` mirrors owner allocations from input data; server enqueue (e.g., server-only impulses from combat) is routed via `OwnerTargetRpc` so the owner allocates the id, then included in the next replicate frame.

## Allocation Rule

Only the owner client allocates ids. Server-originated impulses (e.g., from another player's skill hitting this Buddah) follow this round trip:

1. Server's `BuddahPredictionCombatAdapter.TryRouteImpulse` puts the impulse into a server-side staging queue keyed by network object.
2. Server sends `Target_EnqueuePredictionImpulse(victim, impulse)` to the victim's owner.
3. Owner's CommandBus allocates `NextSequence`, appends to ring, and the next `CreateReplicateData` carries `LastConsumedImpulseId` advancing.
4. Server reconciles based on the same id stream (because owner forwards consumed cursor in input data).

This is identical to the FishNet pattern of forwarding non-owner gameplay forces back through the owner's input pipeline.

## Pre-Owner-Connect Edge Cases

For respawn or intro events that occur before the owner exists (e.g., spectator-only intro):

- Server runs the channel as if it were owner.
- When ownership transfers, server hands current `NextSequence` and `LastConsumedId` snapshots to the new owner inside the next reconcile.

## Replay Safety

During reconcile replay, replicate runs again with the *same* `LastConsumedXxxId` cursors stored in the original input data. The step consumes the *same* events from the ring (which survive in the ring up to `RingHead` floor). The result is bit-identical motion.

If a ring overrun is about to happen (event older than `RingHead` is still referenced by a replay cursor), the channel:
1. Logs a warning out of hot path.
2. Discards the replay event (clamps cursor up).
3. Triggers a forced full snap teleport on next tick using last server reconcile.

See [06-command-bus.md](06-command-bus.md) for how external systems write to channels.
