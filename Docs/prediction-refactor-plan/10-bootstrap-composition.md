# 10 - Bootstrap and Composition

[Back to index](index.md)

Bootstrap is the composition root. It is the only place that wires adapters, simulation steps, and event channels. All wiring is authored on the prefab; runtime `AddComponent` is removed.

## Prefab Structure

```
Buddah (NetworkObject, Rigidbody, PredictionRigidbody host)
  BuddahPredictionBootstrap
  BuddahPredictedMotor
  BuddahPredictionCommandBus
  BuddahPredictionInputAdapter
  BuddahPredictionGateAdapter
  BuddahPredictionSkillAdapter
  BuddahPredictionCombatAdapter
  BuddahPredictionRespawnAdapter
  BuddahPredictionIntroAdapter
  BuddahPredictionDebugState
  VisualRoot (child)
    BuddahPredictionVisualRoot
    BuddahPredictionPresentationAdapter
    BuddahPredictionCameraAdapter
```

The bootstrap holds serialized references to each adapter. No `GetComponent<T>` calls are allowed at runtime - only inside `OnValidate` for editor-time auto-wire.

## Mode Switch Authority

`BuddahPredictionMode` enum values:

| Mode | Legacy Movement | Prediction Motor | Visual Root |
|---|---|---|---|
| `Legacy` | enabled | disabled | read from legacy transform |
| `PredictionV2` | disabled | enabled | read from prediction smoother |
| `Transitioning` | disabled | disabled (holding) | frozen on last-known |

Mode transitions are driven by `BuddahPredictionBootstrap.ApplyMode(mode)`:

1. Transition to `Transitioning`.
2. `CommandBus.TryClearChannels(All)` - drops any pending events.
3. Snapshot current `Rigidbody` state into the prediction reconcile data (if going Legacy -> Prediction).
4. Enable/disable motor and legacy movement components.
5. Notify visual root of new mode.
6. Transition to target mode.

No component is enabled or disabled outside this path.

## Runtime Zero-Alloc Guarantee

- No `AddComponent` at runtime.
- No `GetComponent` at runtime outside `OnStartNetwork` one-shot resolver.
- No `FindObjectOfType` anywhere.

## Owner / Server Role Resolution

`BuddahPredictionBootstrap` exposes read-only helpers:

```csharp
bool IsOwnerClient { get; }
bool IsServerAuth { get; }
bool IsPredictionModeActive();
BuddahPredictionRoleMask RoleMask { get; }
```

Adapters use these; they never reach into `NetworkObject.Owner` directly. This guarantees one consistent role classification across the stack and removes the current footgun where `OnStartServer` reads `IsOwner` under clientHost.

## Failure Modes

| Failure | Behavior |
|---|---|
| Missing adapter reference at startup | log error, disable bootstrap, object does not become gameplay-active |
| Mode switch called on non-server | ignored; logged as warning |
| Adapter called before `OnStartNetwork` | returns `false` |

## Tests to Add

- Editor test: prefab has all adapters wired.
- Playmode test: legacy -> prediction mode switch produces a clean reconcile snapshot and no queue leak.

See [11-intro-result-session.md](11-intro-result-session.md) for the specific flows that use this bootstrap.
