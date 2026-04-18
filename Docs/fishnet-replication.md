# FishNet — Replication

[Back to index](fishnet-architecture.md)

## How It Works

State sync uses **SyncTypes** (server → clients, interval-based) and **RPCs** (explicit calls across the network).

| SyncType | Use |
|---|---|
| `SyncVar<T>` | Single serializable value |
| `SyncList<T>` | Ordered list, per-entry operation callbacks |
| `SyncDictionary<K,V>` | Key-value map, per-operation callbacks |
| `SyncHashSet<T>` | Unordered unique set |

Value is applied before `OnChange` fires. Default send interval ≈ 100 ms.
SyncTypes always flush **after** RPCs in the same tick.

| RPC | Direction | Default restriction |
|---|---|---|
| `[ServerRpc]` | Client → Server | Owner only (`RequireOwnership = true`) |
| `[ObserversRpc]` | Server → All clients | Server only |
| `[TargetRpc]` | Server → One client | Server only |

## MUST

- Declare all SyncType fields as `readonly`.
- Write SyncType values only from server code (or via a `[ServerRpc]` workaround).
- Call `.Dirty(key/index)` after mutating a nested class or struct inside a SyncCollection.
- Keep all RPC and SyncType declarations inside a `NetworkBehaviour` subclass.
- Verify auto-serialization for custom types; provide custom serializers where auto-gen fails.

## MUST NOT

- Add `NetworkBehaviour`-derived components at runtime — not supported.
- Send `[ObserversRpc]` inside `OnStartServer`; observers not yet built. Use `OnSpawnServer` or `BufferLast = true`.
- Assume SyncType changes are immediately visible to clients — they arrive on the next send interval.
- Use `IsOwner` as a substitute for `RequireOwnership` on `[ServerRpc]`.
- Use Unity types without auto-serializer support (e.g. `Sprite`) in RPCs without a custom serializer.

## Common Failure Cases

| Symptom | Cause |
|---|---|
| Collection mutation not reaching clients | Nested container mutated without `.Dirty()` |
| `[ObserversRpc]` never delivered | Called in `OnStartServer` before observers registered |
| `[ServerRpc]` silently dropped | Caller is not owner and `RequireOwnership` is still `true` |
| Stale SyncVar after RPC | SyncTypes flush after RPCs by design; adjust with `SyncTypeSettings` if order matters |
| Runtime `NetworkBehaviour` missing | Component added dynamically — not supported |
| Serialization compile error | Custom type with no auto-serializer and no custom serializer defined |
