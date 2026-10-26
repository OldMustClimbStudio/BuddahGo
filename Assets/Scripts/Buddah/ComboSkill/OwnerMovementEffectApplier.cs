using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

internal sealed class OwnerMovementEffectApplier
{
    // Legacy (prediction-off) freeze reuses the slow-trap root; this is the minimum hand-back acceleration window.
    private const float LegacyFreezeReleaseSeconds = 0.05f;

    private readonly SkillExecutor _owner;

    internal OwnerMovementEffectApplier(SkillExecutor owner)
    {
        _owner = owner;
    }

    internal void ApplyAccelerationServer(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyAcceleration(extraForwardForce, extraMaxSpeed, durationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyAccelerationOwner(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyAcceleration(extraForwardForce, extraMaxSpeed, durationSeconds, "SkillExecutor.Target");
            _owner.PlayFeelLocal("acceleration_local");

            if (extraForwardForce > 0f || extraMaxSpeed > 0f)
                _owner.ShowAccelerationTrail(durationSeconds);

            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed acceleration to PredictionV2 bridge.");
            return;
        }

        var move = _owner.GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement.");
            return;
        }

        if (move.IsSkillRooted)
        {
            GameLog.Verbose($"[SkillExecutor][Target] Accel ignored because rooted. ({extraForwardForce}, {extraMaxSpeed}, {durationSeconds}s)");
            return;
        }

        var effect = _owner.GetComponent<MovementAccelerationEffect>();
        if (effect == null)
            effect = _owner.gameObject.AddComponent<MovementAccelerationEffect>();

        effect.ApplyOrRefresh(move, extraForwardForce, extraMaxSpeed, durationSeconds);
        _owner.PlayFeelLocal("acceleration_local");

        if (extraForwardForce > 0f || extraMaxSpeed > 0f)
            _owner.ShowAccelerationTrail(durationSeconds);

        GameLog.Verbose($"[SkillExecutor][Target] Accel: +{extraForwardForce} forwardForce, +{extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }

    internal void ApplyRootThenAccelerationServer(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyRootThenAcceleration(rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyRootThenAccelerationOwner(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyRootThenAcceleration(rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed root-then-acceleration to PredictionV2 bridge.");
            return;
        }

        var move = _owner.GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement for root-then-accel.");
            return;
        }

        var effect = _owner.GetComponent<MovementRootThenAccelerationEffect>();
        if (effect == null)
            effect = _owner.gameObject.AddComponent<MovementRootThenAccelerationEffect>();

        effect.ApplyOrRestart(move, rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);

        GameLog.Verbose($"[SkillExecutor][Target] RootThenAccel: root={rootDurationSeconds}s, accel=({extraForwardForce},{extraMaxSpeed}) for {accelDurationSeconds}s");
    }

    internal void ApplyInvertTurnInputServer(float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyInvertTurn(durationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyInvertTurnInputOwner(float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyInvertTurn(durationSeconds, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed invert-turn to PredictionV2 bridge.");
            return;
        }

        var move = _owner.GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement for invert-turn.");
            return;
        }

        var effect = _owner.GetComponent<MovementInvertTurnInputEffect>();
        if (effect == null)
            effect = _owner.gameObject.AddComponent<MovementInvertTurnInputEffect>();

        effect.ApplyOrRefresh(move, durationSeconds);

        GameLog.Verbose($"[SkillExecutor][Target] InvertTurnInput for {durationSeconds}s");
    }

    // Keep the SkillExecutor forwarding signature aligned with presentation settings.
    // Enter/restore durations are consumed by ScaleSkillPresentation, not server physics.
    internal void ApplyScaleServer(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds, float massMultiplier, float forwardForceMultiplier)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyScale(scaleMultiplier, durationSeconds, massMultiplier, forwardForceMultiplier, "SkillExecutor.Server");
    }

    internal void ApplyScaleOwner(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds, float massMultiplier, float forwardForceMultiplier)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyScale(scaleMultiplier, durationSeconds, massMultiplier, forwardForceMultiplier, "SkillExecutor.Target");

            var visualEffect = _owner.GetComponent<PlayerScaleEffect>();
            if (visualEffect == null)
                visualEffect = _owner.gameObject.AddComponent<PlayerScaleEffect>();

            visualEffect.ApplyOrRefresh(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds, massMultiplier, forwardForceMultiplier);
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed movement scale to PredictionV2 bridge and kept visual scale effect locally.");
            return;
        }

        var effect = _owner.GetComponent<PlayerScaleEffect>();
        if (effect == null)
            effect = _owner.gameObject.AddComponent<PlayerScaleEffect>();

        effect.ApplyOrRefresh(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds, massMultiplier, forwardForceMultiplier);

        GameLog.Verbose($"[SkillExecutor][Target] Scale x{scaleMultiplier:0.##} for {durationSeconds}s (enter={enterDurationSeconds:0.##}, restore={restoreDurationSeconds:0.##})");
    }

    // Freeze (frost lock): no movement, steering or pushing for the duration.
    internal void ApplyFreezeServer(float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyFreeze(durationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyFreezeOwner(float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyFreeze(durationSeconds, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed freeze to PredictionV2 bridge.");
            return;
        }

        var move = _owner.GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement for freeze.");
            return;
        }

        var effect = _owner.GetComponent<MovementRootThenAccelerationEffect>();
        if (effect == null)
            effect = _owner.gameObject.AddComponent<MovementRootThenAccelerationEffect>();

        effect.ApplyOrRestart(move, durationSeconds, 0f, 0f, LegacyFreezeReleaseSeconds);

        GameLog.Verbose($"[SkillExecutor][Target] Freeze (legacy root) for {durationSeconds}s");
    }

    // Ends the shared acceleration deadline early (缘起 consumes 神足通). It also ends a slow-trap debuff on the
    // same modifier slot; callers decide whether that is acceptable.
    internal void EndAccelerationServer()
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryEndModifier(BuddahPredictedModifierCommandType.Acceleration, "SkillExecutor.Server");
    }

    internal void EndAccelerationOwner()
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryEndModifier(BuddahPredictedModifierCommandType.Acceleration, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed end-acceleration to PredictionV2 bridge.");
            return;
        }

        var effect = _owner.GetComponent<MovementAccelerationEffect>();
        if (effect != null)
            effect.CancelAndRestore();

        GameLog.Verbose("[SkillExecutor][Target] End acceleration (legacy).");
    }

    // Ends the motor scale deadline early. The visual PlayerScaleEffect is presentation and ends on its own path.
    internal void EndScaleServer()
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryEndModifier(BuddahPredictedModifierCommandType.Scale, "SkillExecutor.Server");
    }

    internal void EndScaleOwner()
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryEndModifier(BuddahPredictedModifierCommandType.Scale, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed end-scale to PredictionV2 bridge.");
            return;
        }

        GameLog.Verbose("[SkillExecutor][Target] End scale ignored: the legacy path has no motor scale (PlayerScaleEffect is presentation).");
    }

    // Burst (giant charge): instant extra max speed that decays over the duration.
    internal void ApplyBurstServer(float extraMaxSpeed, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyBurst(extraMaxSpeed, durationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyBurstOwner(float extraMaxSpeed, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyBurst(extraMaxSpeed, durationSeconds, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed burst to PredictionV2 bridge.");
            return;
        }

        GameLog.Verbose($"[SkillExecutor][Target] Burst +{extraMaxSpeed} for {durationSeconds}s ignored: no legacy burst effect.");
    }

    // Max speed multiplier (backfire penalty x0.85); does not touch the acceleration slot, so running buffs stay.
    internal void ApplyMaxSpeedMultiplierServer(float multiplier, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
            _owner.PredictionMovementBridge.TryApplyMaxSpeedMultiplier(multiplier, durationSeconds, "SkillExecutor.Server");
    }

    internal void ApplyMaxSpeedMultiplierOwner(float multiplier, float durationSeconds)
    {
        if (_owner.UsePredictionMovementBridge() && _owner.PredictionMovementBridge != null)
        {
            if (!_owner.IsServerInitialized)
                _owner.PredictionMovementBridge.TryApplyMaxSpeedMultiplier(multiplier, durationSeconds, "SkillExecutor.Target");
            GameLog.Verbose($"{BuddahPredictionBootstrap.LogPrefix} SkillExecutor routed max-speed multiplier to PredictionV2 bridge.");
            return;
        }

        GameLog.Verbose($"[SkillExecutor][Target] Max speed x{multiplier:0.##} for {durationSeconds}s ignored: no legacy max-speed multiplier.");
    }
}
