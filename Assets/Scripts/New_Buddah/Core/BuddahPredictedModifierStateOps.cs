using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Pure modifier-state writes shared by BuddahPredictedMotor.TryApplyModifierCommand and the
    // reset paths. Callers convert seconds to ticks (they own the TimeManager); everything here is
    // clock-free so the server and owner copies stay identical and the rules are unit-testable.
    internal static class BuddahPredictedModifierStateOps
    {
        internal const float MinMaxSpeedMultiplier = 0.1f;
        internal const float MaxMaxSpeedMultiplier = 1f;
        private const float NeutralScale = 1f;

        // Two freezes do not add up: the later deadline wins (same stacking as every other modifier).
        internal static void ApplyFreeze(ref BuddahPredictedModifierState state, uint untilTick)
        {
            state.FreezeUntilTick = Math.Max(state.FreezeUntilTick, untilTick);
        }

        // A new burst restarts its decay window (deadline, duration and extra are one curve, so
        // they are written together rather than max-merged).
        internal static void ApplyBurst(ref BuddahPredictedModifierState state, float extraMaxSpeed, uint untilTick, uint durationTicks)
        {
            state.BurstUntilTick = untilTick;
            state.BurstDurationTicks = durationTicks;
            state.BurstExtraMaxSpeed = Mathf.Max(0f, extraMaxSpeed);
        }

        internal static void ApplyMaxSpeedMultiplier(ref BuddahPredictedModifierState state, float multiplier, uint untilTick)
        {
            state.MaxSpeedPenaltyUntilTick = Math.Max(state.MaxSpeedPenaltyUntilTick, untilTick);
            state.MaxSpeedPenaltyMultiplier = ClampMaxSpeedMultiplier(multiplier);
        }

        internal static float ClampMaxSpeedMultiplier(float multiplier)
        {
            return Mathf.Clamp(multiplier, MinMaxSpeedMultiplier, MaxMaxSpeedMultiplier);
        }

        // Ends the target early: deadline = Min(deadline, now), so an unset 0 stays 0 and nothing
        // is ever extended. Returns false for types that cannot be ended this way.
        internal static bool EndModifier(ref BuddahPredictedModifierState state, BuddahPredictedModifierCommandType target, uint currentTick)
        {
            switch (target)
            {
                case BuddahPredictedModifierCommandType.Acceleration:
                    // The slow trap shares this channel with negative magnitudes. Ending the
                    // acceleration buff must not cancel a slow placed by someone else.
                    if (state.AccelExtraForwardForce > 0f || state.AccelExtraMaxSpeed > 0f)
                    {
                        state.AccelUntilTick = Math.Min(state.AccelUntilTick, currentTick);
                        state.AccelExtraForwardForce = 0f;
                        state.AccelExtraMaxSpeed = 0f;
                    }
                    return true;
                case BuddahPredictedModifierCommandType.RootThenAcceleration:
                    state.RootUntilTick = Math.Min(state.RootUntilTick, currentTick);
                    state.PostRootAccelUntilTick = Math.Min(state.PostRootAccelUntilTick, currentTick);
                    return true;
                case BuddahPredictedModifierCommandType.Scale:
                    state.ScaleUntilTick = Math.Min(state.ScaleUntilTick, currentTick);
                    state.ScaleMultiplier = state.ScaleMassMultiplier = state.ScaleForwardForceMultiplier = NeutralScale;
                    return true;
                case BuddahPredictedModifierCommandType.Freeze:
                    state.FreezeUntilTick = Math.Min(state.FreezeUntilTick, currentTick);
                    return true;
                case BuddahPredictedModifierCommandType.Burst:
                    state.BurstUntilTick = Math.Min(state.BurstUntilTick, currentTick);
                    return true;
                case BuddahPredictedModifierCommandType.MaxSpeedMultiplier:
                    state.MaxSpeedPenaltyUntilTick = Math.Min(state.MaxSpeedPenaltyUntilTick, currentTick);
                    return true;
                default:
                    return false;
            }
        }

        // Skill reset (AI reset, training dummy cleanup). Push grace, steering suppression and
        // room bypass are not skill effects and stay; the backfire penalty persists by design.
        internal static void ClearActiveSkillModifiers(ref BuddahPredictedModifierState state)
        {
            state.RootUntilTick = 0;
            state.AccelUntilTick = 0;
            state.AccelExtraForwardForce = state.AccelExtraMaxSpeed = 0f;
            state.PostRootAccelUntilTick = 0;
            state.PostRootAccelExtraForwardForce = state.PostRootAccelExtraMaxSpeed = 0f;
            state.ScaleUntilTick = 0;
            state.ScaleMultiplier = state.ScaleMassMultiplier = state.ScaleForwardForceMultiplier = NeutralScale;
            state.InvertTurnUntilTick = 0;
            state.FreezeUntilTick = 0;
            state.BurstUntilTick = 0;
            state.BurstDurationTicks = 0;
            state.BurstExtraMaxSpeed = 0f;
        }

        // Teleport with ResetModifiers wipes the state, except the backfire max-speed penalty,
        // which must last through respawn until its own end.
        internal static BuddahPredictedModifierState PreservePersistentOnReset(BuddahPredictedModifierState state)
        {
            return new BuddahPredictedModifierState
            {
                MaxSpeedPenaltyUntilTick = state.MaxSpeedPenaltyUntilTick,
                MaxSpeedPenaltyMultiplier = state.MaxSpeedPenaltyMultiplier
            };
        }
    }
}
