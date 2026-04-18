# 02 - Directory Structure

[Back to index](index.md)

The current `Assets/Scripts/New_Buddah/` layout merges tick logic, side effects, and presentation. The refactor splits these into strict layers.

## Target Layout

```
Assets/Scripts/New_Buddah/
  Core/
    BuddahPredictedMotor.cs             (tick entry + PredictionRigidbody only)
    BuddahPredictedReplicateData.cs
    BuddahPredictedReconcileData.cs
    BuddahPredictedInputData.cs         (extended, see 03)
    BuddahPredictionTickContext.cs      (read-only view passed into sub-modules)
  Simulation/
    BuddahLocomotionStep.cs             (forward force, turn torque, clamping)
    BuddahImpulseStep.cs                (consumes impulse events)
    BuddahModifierStep.cs               (applies acceleration/scale/invert mods)
    BuddahTeleportStep.cs               (consumes teleport events)
    BuddahHandoffStep.cs                (intro/launch/kinematic)
  Events/
    BuddahPredictionEventChannel.cs     (append-only circular buffer per channel)
    BuddahPredictionEventIds.cs         (id allocator, owner-assigned, reconciled)
    BuddahPredictionCommandBus.cs       (external entry point)
    Payloads/*.cs                       (ImpulseCmd, TeleportCmd, ModifierCmd...)
  State/
    BuddahPredictedModifierState.cs     (pure data - no Unity refs)
    BuddahPredictionHandoffState.cs
    BuddahPredictionPushGraceState.cs
  Bootstrap/
    BuddahPredictionBootstrap.cs        (composition root, no AddComponent at runtime)
    BuddahPredictionMode.cs             (enum + switch orchestrator)
  Integration/
    BuddahPredictionSkillAdapter.cs     (replaces SkillMovementBridge)
    BuddahPredictionCombatAdapter.cs    (replaces CombatRouting)
    BuddahPredictionIntroAdapter.cs     (replaces HandoffBridge)
    BuddahPredictionRespawnAdapter.cs   (replaces RespawnBridge)
    BuddahPredictionGateAdapter.cs      (movement gate)
    BuddahPredictionInputAdapter.cs     (owner input sampling to CommandBus)
  Visual/
    BuddahPredictionVisualRoot.cs       (read-only smoother control)
    BuddahPredictionCameraAdapter.cs
    BuddahPredictionPresentationAdapter.cs
  Config/
    BuddahPredictedMotorConfig.cs
    BuddahPredictionEventConfig.cs      (queue sizes, timeouts)
  Debugging/
    BuddahPredictionDebugState.cs       (snapshot written OnPostTick only)
    BuddahPredictionDebugLogger.cs      (guard-gated)
```

## Ownership Rules
- `Core/` never references `Integration/` or `Visual/`.
- `Simulation/` steps are pure functions over `(TickContext, in ReplicateData, ref ReconcileScratch)`.
- `Events/` depends only on `Core/` and `State/`.
- `Integration/` depends on `Events/` but never directly on `Core/` runtime state.
- `Visual/` reads `Core/` snapshots (via getter) but never calls into `Simulation/` or `Events/`.
- `Bootstrap/` is the only place that wires everything and attaches at prefab author time - no runtime `AddComponent`.

## Deletion List
These files are removed or fully replaced:
- `BuddahPredictionLegacyIsolationBridge.cs` (folded into `BuddahPredictionMode`)
- `BuddahPredictionHandoffBridge.cs` (replaced by `BuddahPredictionIntroAdapter`)
- `BuddahPredictionSkillMovementBridge.cs` (replaced by `BuddahPredictionSkillAdapter`)
- `BuddahPredictionModifierBridge.cs` (folded into adapter + CommandBus)
- `BuddahPredictionRespawnBridge.cs` (replaced by `BuddahPredictionRespawnAdapter`)
- `BuddahPredictionCombatRouting.cs` (replaced by `BuddahPredictionCombatAdapter`)
- `BuddahPredictionVisualRootBridge.cs` (rewritten as `BuddahPredictionVisualRoot`)

See [03-data-contracts.md](03-data-contracts.md) for payload shapes.
