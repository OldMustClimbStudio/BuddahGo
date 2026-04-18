# FishNet — Rules, Validation, Recovery

[Back to index](fishnet-architecture.md)

## Consolidated Rules

| ID | Rule |
|---|---|
| A1 | Server is sole authority for all gameplay state. |
| A2 | Only server assigns, transfers, or removes ownership. |
| A3 | Only server calls `SceneManager` load/unload methods. |
| A4 | SyncTypes written only on server (or authorized `ServerRpc`). |
| A5 | `[ServerRpc]` requires ownership by default; document any override. |
| P1 | Every field that affects movement must be in `ReconcileData`. |
| P2 | `CreateReconcile()` called every `OnPostTick` on every peer. |
| P3 | All forces on predicted objects go through `PredictionRigidbody`. |
| P4 | `CharacterController` disabled/enabled around position set in `[Reconcile]`. |
| P5 | `IsFuture()` branches must be bounded or zero out velocity. |
| R1 | SyncType fields declared `readonly`. |
| R2 | Nested container mutation requires `.Dirty()`. |
| R3 | `[ObserversRpc]` never sent inside `OnStartServer`. |
| S1 | In BuddahGo, `RoomStateManager` is the only permitted scene load/unload caller. |
| S2 | Stacked-scene lookups use handle or reference, not name. |
| S3 | Clients must be enrolled via FishNet `SceneManager` to observe objects. |

## Validation Checklist

**Prediction**
- [ ] `ReconcileData` contains every field `[Replicate]` reads or writes.
- [ ] `CreateReconcile()` not gated behind `IsOwner` or `IsServer`.
- [ ] No direct `Rigidbody.AddForce` on predicted objects.
- [ ] `CharacterController` disable/enable wraps `[Reconcile]` position set.
- [ ] `GraphicalObject` is a non-root child on every predicted prefab.

**Replication**
- [ ] All SyncType fields are `readonly`.
- [ ] Nested container mutation followed by `.Dirty()`.
- [ ] No `[ObserversRpc]` inside `OnStartServer`.
- [ ] No `NetworkBehaviour` added dynamically at runtime.

**Ownership**
- [ ] `GiveOwnership` / `RemoveOwnership` only from server code.
- [ ] `RequireOwnership` value intentional on every `[ServerRpc]`.
- [ ] `base.Owner.IsLocalClient` used instead of `base.IsOwner` in `OnStartServer`.

**Scenes**
- [ ] All scene load/unload calls from `RoomStateManager`.
- [ ] New scenes by name; existing instances by handle or reference.
- [ ] Clients enrolled in scene before objects expected to be visible.
- [ ] `MovedNetworkObjects` populated for objects surviving transition.

## Recovery Strategy

**Prediction jitter / snap**
1. Log server state vs. local state inside `[Reconcile]` to find the diverging field.
2. Add that field to `ReconcileData` and restore it in `[Reconcile]`.
3. If visual-only, check `GraphicalObject` parenting and interpolation settings.
4. Never suppress `CreateReconcile()` or reduce reconcile rate as a workaround.
5. Hard stop: bridge layer (`BuddahPredictedMotor`, skill bridges) — escalate before editing.

**Replication not reaching clients**
1. Confirm the SyncType is `readonly` and the write is on server.
2. Check for missing `.Dirty()` on nested container mutation.
3. If `[ObserversRpc]` missing on join, move to `OnSpawnServer` or add `BufferLast = true`.

**Ownership rejected or ignored**
1. Log `IsOwner` and `IsController` at call site.
2. Confirm `RequireOwnership` value matches intended callers.
3. For host `IsOwner` false in `OnStartServer`, use `base.Owner.IsLocalClient`.

**Scene transition failure**
1. Confirm call is on server path.
2. Confirm correct method: `LoadGlobalScenes` vs. `LoadConnectionScenes`.
3. Verify clients enrolled in scene via `NetworkConnection.Scenes`.
4. For stacking, switch to handle/reference lookup.
5. Hard stop: any transition touching `RoomStateManager` or result/intro flow — escalate.
