using NewBuddah.PredictionV2.Bootstrap;
using UnityEngine;

internal sealed class OwnerMovementEffectApplier
{
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
}
