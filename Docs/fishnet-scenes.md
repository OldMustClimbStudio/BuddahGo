# FishNet — Scene Management

[Back to index](fishnet-architecture.md)

## How It Works

FishNet `SceneManager` is the sole authority for online scene operations. The server drives all loads and unloads. Clients do not call scene load methods.

| Method | Scope |
|---|---|
| `LoadGlobalScenes(sld)` | All current + future clients |
| `LoadConnectionScenes(conn(s), sld)` | Specified connections only |
| `UnloadGlobalScenes(sud)` | All clients — global scenes only |
| `UnloadConnectionScenes(conn(s), sud)` | Per-connection unload |

**SceneLoadData** is required for every load call. New scenes load by name. Existing instances load by `Scene` handle or reference.

**Scene Stacking**: multiple instances of the same scene can coexist. Name lookup returns the first match — always use handles or references when stacking.

**Scene Caching**: server keeps a scene alive after all clients leave, preserving state for reconnects.

**Visibility**: a client must be enrolled in a scene (via `SceneManager` load or `AddConnectionsToScene`) to observe `NetworkObject`s in it. The initial offline scene loaded by Unity does not auto-enroll connected clients.

**BuddahGo rule**: `RoomStateManager` is the only permitted caller of scene load/unload methods.

## MUST

- Call all load/unload from server code.
- Wrap all calls with `SceneLoadData` / `SceneUnloadData`.
- Load new scenes by name; use `Scene` reference or handle for existing instances.
- Subscribe to `SceneManager.OnLoadEnd` (server path) to cache references for later use.
- Enroll clients in a scene via FishNet `SceneManager` before expecting them to observe objects.
- Include objects that must survive a transition in `SceneLoadData.MovedNetworkObjects`.

## MUST NOT

- Call `UnloadConnectionScenes` on a global scene — use `UnloadGlobalScenes`.
- Look up scenes by name when scene stacking is active.
- Invoke scene load/unload from client code or systems other than `RoomStateManager`.
- Use `ReplaceOption.All` without verifying non-FishNet scenes (offline scene, UI) should also be replaced.

## Common Failure Cases

| Symptom | Cause |
|---|---|
| Client can't see `NetworkObject`s after load | Client not added to scene; never enrolled as observer |
| Wrong instance loaded in stacked sessions | Name lookup used instead of handle or reference |
| Scene never unloads on server | Server only auto-unloads when all connections removed; unload explicitly or use Scene Caching |
| `NetworkObject` missing after transition | Not in `SceneLoadData.MovedNetworkObjects` |
| Global scene unload fails | `UnloadConnectionScenes` called on a global scene |
