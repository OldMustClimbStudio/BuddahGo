# 06 - CommandBus

[Back to index](index.md)

`BuddahPredictionCommandBus` is the only entry point external systems call. It enforces tick alignment so no caller can write directly to the motor or to the rigidbody.

## Public API (owner client)

```csharp
public sealed class BuddahPredictionCommandBus : NetworkBehaviour {
  bool TryEnqueueImpulse(in ImpulseCmd cmd);
  bool TryEnqueueTeleport(in TeleportCmd cmd);
  bool TryEnqueueModifier(in ModifierCmd cmd);
  bool TryEnqueueHandoff(in HandoffCmd cmd);
  bool TryClearChannels(BuddahPredictionChannelMask mask);
}
```

## Routing Rules

| Caller | Allowed channel |
|---|---|
| `BuddahPredictionSkillAdapter` | Modifier (and Impulse only for `RootThenAcceleration` initial pulse) |
| `BuddahPredictionCombatAdapter` | Impulse |
| `BuddahPredictionRespawnAdapter` | Teleport |
| `BuddahPredictionIntroAdapter` | Handoff (and one Teleport for spline-end snap) |
| `BuddahPredictionGateAdapter` | none - read-only `MovementAllowed` |
| `BuddahPredictionInputAdapter` | none - writes Steering/Throttle into `ReplicateData` directly |

## Server-Originating Commands

If the caller is on the server and the victim is owned by a remote client, the bus uses `TargetRpc` to relay:

```csharp
[TargetRpc(RunLocally = false, ExcludeServer = false)]
private void Target_EnqueueImpulse(NetworkConnection owner, ImpulseCmd cmd);
```

Owner-side handler appends to local channel. Server still appends to its mirror channel so that server's `[Replicate]` consumes the same event when the owner's input arrives with the matching `LastConsumedImpulseId`.

## Threading and Allocation

- All `TryEnqueueXxx` calls are main-thread only.
- Cmd structs are `readonly struct` to prevent allocations.
- Bus never calls into motor; motor pulls from bus during `[Replicate]`.

## Backpressure

If a channel is full:
1. Owner enqueue returns `false`.
2. Caller must decide whether to drop or coalesce - default policy: log + drop.
3. Critical channels (Teleport, Handoff) overwrite oldest unconsumed entry with a `Forced` flag, which triggers a hard reset path inside the step.

## Cancellation / Mode Reset

`TryClearChannels` is the only sanctioned way to drop pending events. It is called by `BuddahPredictionMode` during legacy<->prediction switching and by respawn at race restart.

## Failure Mode

If a caller bypasses the bus and writes directly to the motor or rigidbody, code review rejects the change. Lint rule (added to a Roslyn analyzer in Phase 4) flags any `Rigidbody` write inside `Assets/Scripts/New_Buddah/` outside the motor file.

See [07-side-effect-migration.md](07-side-effect-migration.md) for the per-side-effect mapping.
