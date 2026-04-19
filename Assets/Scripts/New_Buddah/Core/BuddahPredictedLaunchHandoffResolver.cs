using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Phase 3d — pure-static resolver for the launch-handoff tick tail.
    //
    // Hoisted from BuddahPredictedMotor.cs (previously motor.cs:1764-1790 and
    // motor.cs:1792-1824) so motor and BuddahHandoffStep can call the same
    // deterministic body. Enables parity-by-construction between the real
    // motor path and the shadow observation step — same code, same output.
    //
    // Zero external dependencies: no Unity TimeManager read, no bootstrap,
    // no rigidbody. The two methods take by-value structs and a tick delta
    // parameter. Bit-identical output on identical inputs (C# / Mono /
    // IL2CPP single-thread).
    //
    // Bootstrap.LogVerbose side effects that previously lived inside the
    // motor bodies are now emitted by motor wrappers at the call sites,
    // keeping this resolver pure.
    public static class BuddahPredictedLaunchHandoffResolver
    {
        // Advances the handoff state machine for the current tick.
        // Mirrors the previous RefreshLaunchState body verbatim. Returns the
        // advanced state; caller is responsible for writing it back and
        // emitting any state-transition log.
        public static BuddahPredictedLaunchHandoffState Advance(
            BuddahPredictedLaunchHandoffState state, uint currentTick)
        {
            if (!state.IsActive)
            {
                state.CurrentState = BuddahPredictedLaunchState.Normal;
                state.BlendAlpha = 1f;
                return state;
            }

            if (state.InheritEndTick > currentTick && state.InheritEndTick > state.StartTick)
            {
                state.CurrentState = BuddahPredictedLaunchState.Inherit;
                state.BlendAlpha = 0f;
            }
            else if (state.BlendEndTick > currentTick && state.BlendEndTick > state.InheritEndTick)
            {
                state.CurrentState = BuddahPredictedLaunchState.Blend;
                uint blendTicks = state.BlendEndTick - state.InheritEndTick;
                uint elapsedBlendTicks = currentTick > state.InheritEndTick
                    ? currentTick - state.InheritEndTick
                    : 0u;
                state.BlendAlpha = blendTicks > 0u
                    ? Mathf.Clamp01((float)elapsedBlendTicks / blendTicks)
                    : 1f;
            }
            else
            {
                state.CurrentState = BuddahPredictedLaunchState.Normal;
                state.BlendAlpha = 1f;
                state.IsActive = false;
            }

            return state;
        }

        // Projects a stale handoff eventData's snapshot fields forward to the
        // arrival tick. Mirrors the previous AdjustLaunchHandoffForArrivalTick
        // body verbatim. Returns the projected eventData (struct copy; caller
        // assigns the result).
        //
        // tickDeltaSeconds must equal TimeManager.TickDelta at the caller's
        // moment. If <= 0, projection collapses to zero elapsed (matches the
        // previous GetElapsedSeconds null-TimeManager / zero-duration
        // short-circuit). StartTick is always bumped to currentTick when
        // staleness > 0 — preserving the caller contract.
        public static BuddahPredictedLaunchHandoffData ProjectForArrivalTick(
            BuddahPredictedLaunchHandoffData eventData,
            uint currentTick,
            float tickDeltaSeconds)
        {
            if (currentTick <= eventData.StartTick)
                return eventData;

            uint staleTicks = currentTick - eventData.StartTick;
            float elapsedSeconds = tickDeltaSeconds > 0f
                ? staleTicks * Mathf.Max(0.0001f, tickDeltaSeconds)
                : 0f;
            Vector3 projectedPosition = eventData.SnapshotPosition + (eventData.SnapshotVelocity * elapsedSeconds);
            Quaternion projectedRotation = ProjectRotationForward(
                eventData.SnapshotRotation, eventData.SnapshotAngularVelocity, elapsedSeconds);
            Vector3 projectedForward = projectedRotation * Vector3.forward;
            projectedForward.y = 0f;
            if (projectedForward.sqrMagnitude < 0.0001f)
                projectedForward = eventData.SnapshotForward;
            if (projectedForward.sqrMagnitude < 0.0001f)
                projectedForward = Vector3.forward;
            projectedForward.Normalize();

            eventData.StartTick = currentTick;
            eventData.SnapshotPosition = projectedPosition;
            eventData.SnapshotRotation = projectedRotation;
            eventData.SnapshotForward = projectedForward;
            return eventData;
        }

        private static Quaternion ProjectRotationForward(
            Quaternion rotation, Vector3 angularVelocity, float elapsedSeconds)
        {
            float angularSpeed = angularVelocity.magnitude;
            if (angularSpeed <= 0.0001f || elapsedSeconds <= 0f)
                return rotation;

            Vector3 axis = angularVelocity / angularSpeed;
            float angleDegrees = angularSpeed * elapsedSeconds * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angleDegrees, axis) * rotation;
        }
    }
}
