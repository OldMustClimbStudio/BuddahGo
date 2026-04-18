# FishNet Architecture Index

Source: FishNet 4.x docs. Last refreshed: 2026-04-15.

## Core System Files

| System | File |
|---|---|
| Prediction | [fishnet-prediction.md](fishnet-prediction.md) |
| Replication | [fishnet-replication.md](fishnet-replication.md) |
| Ownership | [fishnet-ownership.md](fishnet-ownership.md) |
| Scene Management | [fishnet-scenes.md](fishnet-scenes.md) |
| Rules, Validation, Recovery | [fishnet-rules.md](fishnet-rules.md) |

## Global Authority Constraints

- Server is the single source of truth for all gameplay state.
- Only the server assigns or revokes ownership.
- Only the server calls `SceneManager` load/unload methods.
- SyncTypes are written only on the server (or via an authorized `ServerRpc`).
- In BuddahGo, `RoomStateManager` is the only permitted caller of scene transitions.

## Hard Stops

Any task touching the following must be escalated before editing:

- Reconcile structures, tick drift, or scene sync uncertainty.
- Cross-system ownership transfers that affect the prediction stack.
- Scene transitions that touch `RoomStateManager`, `IntroSequenceManager`, or result flow.
- Renaming serialized fields, public RPC entry points, or scene names used by orchestration.
