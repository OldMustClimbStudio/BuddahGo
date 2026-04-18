# 08 - Visual Layer

[Back to index](index.md)

The visual layer is the user-facing fix for the two reported symptoms: own-Buddah shaking during gameplay and remote-Buddah flickering during intro. Both are downstream of the same architectural rule: visual must be read-only against gameplay state.

## New Files

| File | Role |
|---|---|
| `Visual/BuddahPredictionVisualRoot.cs` | replaces `BuddahPredictionVisualRootBridge` |
| `Visual/BuddahPredictionCameraAdapter.cs` | replaces `BuddahPredictionCameraBridge` |
| `Visual/BuddahPredictionPresentationAdapter.cs` | replaces `BuddahPredictionPresentationBridge` |

## VisualRoot Contract

Responsibilities:
- Holds a reference to the graphical subtree under the predicted root.
- Runs in `LateUpdate` only.
- Reads `PredictionManager.SmoothedPosition / SmoothedRotation` or equivalent smoother output.
- Does not touch `Rigidbody`, does not call adapters, does not perform gameplay gating.

Removed responsibilities:
- `ApplyVisualRootStabilization` mutating position every LateUpdate - gone.
- `ShouldApplyOwnerVisualStabilization` with non-owner side effects - gone.
- Intro lock-position logic that ran on non-owners - gone. This directly fixes the remote intro flicker.

## Why the Jitter Happens

Today's motor rewrites `rb.position` and/or calls `PredictionRigidbody.Initialize` mid-tick inside `[Replicate]`. This means every reconcile replay produces a different rigidbody snapshot, which the graphical smoother then chases, producing the shaking you see on every Buddah's model during gameplay. After the refactor:

1. `[Replicate]` touches only `PredictionRigidbody` via force / velocity APIs.
2. All teleports run in `BuddahTeleportStep`, which uses `PredictionRigidbody.MovePosition(...) / .MoveRotation(...)` and is deterministic because the triggering event id is in `ReplicateData`.
3. Reconcile restores `RbState` via `PredictionRigidbody.Reconcile` - replay reproduces the same path every time.
4. Visual smoother sees a monotonic target, so no shake.

## Why the Intro Flicker Happens

Today's `BuddahPredictionVisualRootBridge.ShouldApplyOwnerVisualStabilization` returns `true` (driven by `ShouldLockVisualRootDuringIntroOrPresentation`) before it checks `_networkObject.IsOwner`, so non-owner peers run owner-intended stabilization logic during intro and fight the incoming network transform / smoother updates. Fix:

1. `IsOwner` gate is the very first check in any visual method.
2. During intro, non-owners run the standard prediction smoother only. No extra snap.
3. Spline-driven intro motion is delivered to non-owners via reconcile - not via a parallel kinematic handoff that bypasses prediction.

## PresentationAdapter

- Subscribes to an `OnPostTick` visual-snapshot event the motor fires (post-reconcile-safe).
- Publishes: linear speed, angular speed, current mode, active modifier kind, push-grace fraction.
- All VFX systems read only this snapshot.

## CameraAdapter

- LateUpdate reads smoothed transform.
- Never applies forces, never calls into bus, never reads `Rigidbody.velocity` directly (uses post-tick snapshot).

## Acceptance Criteria

- Remote intro flicker not reproducible over 10 consecutive intros.
- Owner and remote gameplay shake not reproducible at `Fixed Timestep = 0.02`, prediction 60 Hz, 100 ms RTT.
- LateUpdate CPU on visual root reduced by removing stabilization loop.

See [09-integration-adapters.md](09-integration-adapters.md) for how gameplay state reaches presentation adapters.
