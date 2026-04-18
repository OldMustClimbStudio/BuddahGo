# FishNet — Ownership

[Back to index](fishnet-architecture.md)

## How It Works

Each `NetworkObject` has one optional owner: a `NetworkConnection` (client). The server has no owner identity — it is the authority that grants, transfers, and revokes ownership.

| Property | True when |
|---|---|
| `base.IsOwner` | Local client owns this object (client-side check only) |
| `base.IsController` | Local client owns it, OR server running with no assigned owner |
| `base.HasAuthority` | Server is running (useful for unowned objects on server) |
| `base.Owner` | The `NetworkConnection` currently assigned as owner |

**Assigning ownership (server only):**
```csharp
// At spawn
InstanceFinder.ServerManager.Spawn(go, ownerConnection);
// After spawn
networkObject.GiveOwnership(newOwnerConnection);
// Revoke
networkObject.RemoveOwnership();
```

**PredictedOwner**: lets client simulate ownership locally before server confirms. Call `TakeOwnership()` on the component. Override `OnTakeOwnership()` server-side to validate or reject.

`NetworkConnection` tracks: owned objects, scene membership, auth state, and custom developer data (not auto-synced).

## MUST

- Execute all ownership assignment, transfer, and revocation from server code only.
- Use `base.IsController` (not `IsOwner`) when the same path must also run for server-controlled unowned objects.
- Use `base.Owner.IsLocalClient` instead of `base.IsOwner` inside `OnStartServer` for clientHost.
- Implement server-side validation in `OnTakeOwnership()` when `PredictedOwner` grants access or power.

## MUST NOT

- Let clients call `GiveOwnership()` or `RemoveOwnership()` directly.
- Cache `NetworkConnection` references across scene transitions without validating the connection is still live.
- Use `IsOwner` as equivalent to `IsController` for server-running-unowned paths.

## Common Failure Cases

| Symptom | Cause |
|---|---|
| `[ServerRpc]` silently ignored | Non-owner calling with `RequireOwnership = true` |
| `IsOwner` false in `OnStartServer` (host) | `IsOwner` is client-side; use `base.Owner.IsLocalClient` |
| Two clients controlling one object | `PredictedOwner.TakeOwnership()` concurrent with no rejection logic |
| Unowned object ignores server input | Guard uses `IsOwner` instead of `IsController` |
