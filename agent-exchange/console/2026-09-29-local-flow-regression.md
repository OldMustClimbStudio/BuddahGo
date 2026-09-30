# Local multiplayer startup regression — 2026-09-29

Base: `dev` at `ce5c1c2`, with the three-file startup fix on `fix/local-multiplayer-flow`. Unity 2022.3.55f1c1. One regression run after the recorded P0 failure; no gameplay gate or roster mutation was used to bypass the fixed behavior.

## Changes and cause

- Authentication can register a host before its local ClientId is ready. Refresh its existing roster entry from `RoomStateManager.OnStartClient` on the server-host only.
- MainMenu had no configured ObserverManager. Add the existing FishNet SceneCondition so scene objects become visible after each client joins their scene.
- Mark the persistent RoomStateManager prefab as a global NetworkObject so the room remains replicated across scene transitions with the new scene condition.

References: [ObserverManager](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/components/managers/observermanager), [persisting NetworkObjects](https://fish-networking.gitbook.io/docs/guides/features/scene-management/persisting-networkobjects), [latency simulation](https://fish-networking.gitbook.io/docs/tutorials/simple/simulating-bad-network-connections).

## Actual run

Two separate Editor processes (original project and ParrelSync clone), using unsaved localhost Tugboat transport settings on port 17845. Each ran at a requested 60 fps with background execution. The driver invoked the existing ready, start, property/skill selection and skill-cast APIs. It did not set host flags or movement gates.

Both peers automatically completed room ready → property/skill selection → RaceMap → authoritative GO and movement unlock. Host/client had ClientIds 0/1 and matching ObjectIds 2/3 with distinct local ownership. The session sampled approximately 95 seconds normally, then 100 seconds with LatencySimulator configured to 100 ms on each peer.

| Evidence | Host | Client |
|---|---:|---:|
| Active gameplay duration | 195.095 s | 195.003 s |
| Captured D-LOC heartbeats | 236 | 91 |
| Nonzero loc/tel/mod/hof divergence windows | 0 | 0 |
| D-LOC FATAL | 0 | 0 |
| SceneId lookup failures | 0 | 0 |
| Maximum reconcile callback count | 0 | 9581 |
| Skill-cast requests | 6 | 6 |
| Maximum observed scale multiplier | 4 | 4 |

Both peers observed the same skill broadcast IDs: acceleration, slowtrap, blackcurtain, anti_blackcurtain, giant, push_projectile_hands, reverseturn. Initial host identity no longer required the previous test-fixture refresh. No Exception lines were found in either Editor log after the new connection started. Editor compilation succeeded; serialized diffs are limited to the intended observer component and room global flag, with existing GUIDs and network method signatures preserved.

## Scope

This verifies the two startup defects and a local R7-style normal/100ms prediction smoke test. It does not complete Steam lobby/P2P validation, a three-lap match and result votes, every base/anti skill combination, collision/respawn coverage, or a performance comparison. Existing debug UI/font/animation warnings are outside this fix. The release compilation guard is handled separately by PR #47.

Local generated evidence (not tracked): `Logs/local-flow-regression-host.jsonl`, the clone's `Logs/local-flow-regression-client.jsonl`, both Editor logs, `Logs/local-flow-regression-driver.cs.txt`, and the clone's `Logs/local-flow-fixed-client.png`.
