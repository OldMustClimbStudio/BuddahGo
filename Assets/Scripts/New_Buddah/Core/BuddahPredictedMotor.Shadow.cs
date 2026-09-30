using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using System;
using System.Text;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Integration;
using NewBuddah.PredictionV2.Simulation;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
using System.Collections.Generic;
#endif
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_PERF_PROBE
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
using UnityEngine;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;

namespace NewBuddah.PredictionV2.Core
{
    public partial class BuddahPredictedMotor
    {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
        private BuddahPredictionShadowScratch _realScratch;
        private BuddahPredictionShadowScratch _shadowScratch;
        private int _dLocConsecutive;
        private int _shadowActiveCompares;
        private int _shadowSkipCompares;
        // Phase 4b V5 Q3-B — reconcile-callback count. Cumulative-since-spawn; emitted in
        // [D-LOC HEARTBEAT] as rec-cb. Used as LatencySim engagement sanity check: under
        // simulated latency, FishNet fires reconcile more often → growth rate proportional.
        // 0 callbacks across a session under LatencySim = simulator not engaged.
        private uint _reconcileCallbackCount;
        private Vector3 _shadowPreClampVelocity;
        private bool _shadowPreTeleportHasPending;
        private BuddahPredictedTeleportEventData _shadowPreTeleportEvent;
        // Per-window divergence counters (reset at heartbeat).
        // V2b Step 1 Q4 + V4: impulse axis fully retired; only loc/tel/mod/hof divergence tracked.
        private int _dLocLocomotionDivCount;
        private int _dLocTeleportDivCount;
        // Cumulative consume counters (never reset — prove shadow steps actually consumed events).
        private uint _shadowTeleportConsumedCount;
        // Phase 3c — modifier shadow state. Snapshot of _modifierState captured at the
        // motor.cs:341 authoritative Resolve; fed into BuddahModifierStep for independent
        // Resolve+compare. Cumulative counter obeys L13 (compared>0 gate); per-window
        // divergence counter resets at heartbeat.
        private BuddahPredictedModifierState _shadowModifierStateSnapshot;
        private uint _shadowModifierConsumedCount;
        private int _dLocModifierDivCount;
        // Phase 3d — handoff shadow state. Snapshots pre-consume (before motor.cs
        // ConsumePendingLaunchHandoffEvent) so BuddahHandoffStep can parity-mirror
        // the consume → FromData → Advance pipeline. Cumulative counter obeys L13
        // (compared>0 gate); per-window divergence counter resets at heartbeat.
        private bool _shadowPreHandoffHasPending;
        private BuddahPredictedLaunchHandoffData _shadowPreHandoffEvent;
        private BuddahPredictedLaunchHandoffState _shadowPreHandoffState;
        private uint _shadowHandoffConsumedCount;
        private int _dLocHandoffDivCount;
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
        private BuddahPredictionTickContext BuildTickContext(
            in BuddahPredictedInputData data,
            Vector3 forwardDirection,
            float resolvedThrottle,
            float resolvedSteering)
        {
            uint tick = TimeManager != null ? TimeManager.LocalTick : data.GetTick();
            float dt = TimeManager != null ? (float)TimeManager.TickDelta : Time.fixedDeltaTime;
            float pushExtra = config != null ? config.PushExtraMaxSpeed : 0f;
            float pushGraceRemaining = _modifierState.PushGraceUntilTick > tick
                ? (_modifierState.PushGraceUntilTick - tick) * dt
                : 0f;

            return new BuddahPredictionTickContext(
                rbVelocityPreTick: _shadowPreClampVelocity,
                rbMass: rb != null ? rb.mass : 1f,
                fixedDeltaTime: dt,
                tick: tick,
                forwardDirection: forwardDirection,
                resolvedThrottle: resolvedThrottle,
                resolvedSteering: resolvedSteering,
                computedStats: _computedStats,
                pushGraceExtraSpeed: pushExtra,
                pushGraceRemaining: pushGraceRemaining,
                hasPendingTeleportPreConsume: _shadowPreTeleportHasPending,
                pendingTeleportEventId: _shadowPreTeleportEvent.EventId,
                pendingTeleportEventTick: _shadowPreTeleportEvent.EventTick,
                teleportTargetPosition: _shadowPreTeleportEvent.TargetPosition,
                teleportTargetRotation: _shadowPreTeleportEvent.TargetRotation,
                teleportFlag_SnapProgress: _shadowPreTeleportEvent.SnapProgress,
                teleportFlag_ZeroLinearVelocity: _shadowPreTeleportEvent.ZeroLinearVelocity,
                teleportFlag_ZeroAngularVelocity: _shadowPreTeleportEvent.ZeroAngularVelocity,
                teleportFlag_ResetModifiers: _shadowPreTeleportEvent.ResetModifiers,
                teleportFlag_ResetImpulseQueue: _shadowPreTeleportEvent.ResetImpulseQueue,
                teleportFlag_ResetPushGrace: _shadowPreTeleportEvent.ResetPushGrace,
                teleportFlag_RebaseTrails: _shadowPreTeleportEvent.RebaseTrails,
                shadowModifierStateSnapshot: _shadowModifierStateSnapshot,
                config: config,
                shadowPreHandoffHasPending: _shadowPreHandoffHasPending,
                shadowPreHandoffEvent: _shadowPreHandoffEvent,
                shadowPreHandoffState: _shadowPreHandoffState);
        }

        private void Shadow_CompareAndReport()
        {
            bool anyRan = _realScratch.LocomotionRan || _shadowScratch.LocomotionRan
                          // Phase 4b V2b Step 1 — _shadowScratch.ImpulseRan dropped (Q4 amendment:
                          // early-shadow impulse step removed). _realScratch.ImpulseRan is now
                          // NEW-driven (channel drain authority).
                          || _realScratch.ImpulseRan
                          || _realScratch.TeleportRan || _shadowScratch.TeleportRan
                          || _realScratch.ModifierRan || _shadowScratch.ModifierRan
                          || _realScratch.HandoffRan || _shadowScratch.HandoffRan;
            if (!anyRan)
            {
                _dLocConsecutive = 0;
                _shadowSkipCompares++;
                if ((_shadowSkipCompares % 300) == 1)
                {
                    uint tickIdle = TimeManager != null ? TimeManager.LocalTick : 0u;
                    // V2b Step 1 Q4 / V4: impulse axis dropped from D-LOC HEARTBEAT (LEG axis retired).
                    GameLog.Verbose($"[D-LOC HEARTBEAT] T={tickIdle} active-ticks={_shadowActiveCompares} skip-ticks={_shadowSkipCompares}\n  loc-div={_dLocLocomotionDivCount} tel-div={_dLocTeleportDivCount} mod-div={_dLocModifierDivCount} hof-div={_dLocHandoffDivCount}\n  tel-compared={_shadowTeleportConsumedCount} mod-compared={_shadowModifierConsumedCount} hof-compared={_shadowHandoffConsumedCount} rec-cb={_reconcileCallbackCount} (both sides idle)");
                    _dLocLocomotionDivCount = 0;
                    _dLocTeleportDivCount = 0;
                    _dLocModifierDivCount = 0;
                    _dLocHandoffDivCount = 0;
                }
                return;
            }

            _shadowActiveCompares++;
            if ((_shadowActiveCompares % 120) == 1)
            {
                uint tickHb = TimeManager != null ? TimeManager.LocalTick : 0u;
                GameLog.Verbose($"[D-LOC HEARTBEAT] T={tickHb} active-ticks={_shadowActiveCompares} skip-ticks={_shadowSkipCompares}\n  loc-div={_dLocLocomotionDivCount} tel-div={_dLocTeleportDivCount} mod-div={_dLocModifierDivCount} hof-div={_dLocHandoffDivCount}\n  tel-compared={_shadowTeleportConsumedCount} mod-compared={_shadowModifierConsumedCount} hof-compared={_shadowHandoffConsumedCount} rec-cb={_reconcileCallbackCount}");
                _dLocLocomotionDivCount = 0;
                _dLocTeleportDivCount = 0;
                _dLocModifierDivCount = 0;
                _dLocHandoffDivCount = 0;
            }

            bool locDiverged = false;
            // Phase 4b V2b Step 1 — impDiverged removed (Q4 amendment: D-LOC impulse compare killed).
            bool telDiverged = false;
            bool modDiverged = false;
            bool hofDiverged = false;
            uint tick = TimeManager != null ? TimeManager.LocalTick : 0u;

            // Locomotion compare (3a).
            if (_realScratch.LocomotionRan != _shadowScratch.LocomotionRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} loc gate mismatch: realRan={_realScratch.LocomotionRan} shadowRan={_shadowScratch.LocomotionRan}");
                locDiverged = true;
            }
            else if (_realScratch.LocomotionRan)
            {
                if (_realScratch.ClampingApplied != _shadowScratch.ClampingApplied)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} clamp-gate mismatch: realClamp={_realScratch.ClampingApplied} shadowClamp={_shadowScratch.ClampingApplied}");
                    locDiverged = true;
                }
                else
                {
                    float fwdDelta = (_realScratch.CommandedForwardForce - _shadowScratch.CommandedForwardForce).magnitude;
                    float trnDelta = Mathf.Abs(_realScratch.CommandedTurnTorque - _shadowScratch.CommandedTurnTorque);
                    float velDelta = _realScratch.ClampingApplied
                        ? (_realScratch.VelocityAfterClamp - _shadowScratch.VelocityAfterClamp).magnitude
                        : 0f;

                    if (fwdDelta > 1e-4f || trnDelta > 1e-4f || velDelta > 1e-4f)
                    {
                        Debug.LogWarning($"[D-LOC] T={tick} fwd={fwdDelta:F6} trn={trnDelta:F6} vel={velDelta:F6} clamp={_realScratch.ClampingApplied}");
                        locDiverged = true;
                    }
                }
            }

            // V2b Step 1 Q4 + L19 + V4: D-LOC impulse compare retired. NEW (CommandBus.ImpulseChannel)
            // is sole impulse drain authority post-V4; no shadow comparison needed.

            // Teleport compare (3b) — ran flag + cursor + target pose + 7 flag reads.
            if (_realScratch.TeleportRan != _shadowScratch.TeleportRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} teleport-ran gate mismatch: real={_realScratch.TeleportRan} shadow={_shadowScratch.TeleportRan}");
                telDiverged = true;
            }
            else if (_realScratch.TeleportRan)
            {
                if (_realScratch.ShadowLastConsumedTeleportId != _shadowScratch.ShadowLastConsumedTeleportId)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-consume cursor mismatch: realId={_realScratch.ShadowLastConsumedTeleportId} shadowId={_shadowScratch.ShadowLastConsumedTeleportId}");
                    telDiverged = true;
                }

                float posDelta = (_realScratch.PostTeleportPosition - _shadowScratch.PostTeleportPosition).magnitude;
                if (posDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-position delta={posDelta:F6}");
                    telDiverged = true;
                }

                float rotDelta = Quaternion.Angle(_realScratch.PostTeleportRotation, _shadowScratch.PostTeleportRotation);
                if (rotDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-rotation angular-delta-deg={rotDelta:F6}");
                    telDiverged = true;
                }

                if (_realScratch.TeleportFlag_SnapProgress != _shadowScratch.TeleportFlag_SnapProgress)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=SnapProgress real={_realScratch.TeleportFlag_SnapProgress} shadow={_shadowScratch.TeleportFlag_SnapProgress}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ZeroLinearVelocity != _shadowScratch.TeleportFlag_ZeroLinearVelocity)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ZeroLinearVelocity real={_realScratch.TeleportFlag_ZeroLinearVelocity} shadow={_shadowScratch.TeleportFlag_ZeroLinearVelocity}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ZeroAngularVelocity != _shadowScratch.TeleportFlag_ZeroAngularVelocity)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ZeroAngularVelocity real={_realScratch.TeleportFlag_ZeroAngularVelocity} shadow={_shadowScratch.TeleportFlag_ZeroAngularVelocity}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetModifiers != _shadowScratch.TeleportFlag_ResetModifiers)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetModifiers real={_realScratch.TeleportFlag_ResetModifiers} shadow={_shadowScratch.TeleportFlag_ResetModifiers}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetImpulseQueue != _shadowScratch.TeleportFlag_ResetImpulseQueue)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetImpulseQueue real={_realScratch.TeleportFlag_ResetImpulseQueue} shadow={_shadowScratch.TeleportFlag_ResetImpulseQueue}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_ResetPushGrace != _shadowScratch.TeleportFlag_ResetPushGrace)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=ResetPushGrace real={_realScratch.TeleportFlag_ResetPushGrace} shadow={_shadowScratch.TeleportFlag_ResetPushGrace}");
                    telDiverged = true;
                }
                if (_realScratch.TeleportFlag_RebaseTrails != _shadowScratch.TeleportFlag_RebaseTrails)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} teleport-flag mismatch: flag=RebaseTrails real={_realScratch.TeleportFlag_RebaseTrails} shadow={_shadowScratch.TeleportFlag_RebaseTrails}");
                    telDiverged = true;
                }
            }

            // Modifier compare (3c) — 12 ComputedStats fields (4 floats + 5 bools + 3 floats).
            // Real-side ModifierRan is set at the motor.cs:341 hook; shadow-side is set by
            // BuddahModifierStep.Run. Resolver is pure-static, so any delta > 1e-4 on floats
            // or any bool mismatch points to a divergent _modifierState input — NOT
            // resolver floating-point drift (C# single-thread Resolve is bit-deterministic).
            if (_realScratch.ModifierRan != _shadowScratch.ModifierRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} mod-ran gate mismatch: real={_realScratch.ModifierRan} shadow={_shadowScratch.ModifierRan}");
                modDiverged = true;
            }
            else if (_realScratch.ModifierRan)
            {
                BuddahPredictedMotorComputedStats realStats = _realScratch.ShadowComputedStats;
                BuddahPredictedMotorComputedStats shadowStats = _shadowScratch.ShadowComputedStats;

                float fwdDelta = Mathf.Abs(realStats.FinalForwardForce - shadowStats.FinalForwardForce);
                if (fwdDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalForwardForce real={realStats.FinalForwardForce:F6} shadow={shadowStats.FinalForwardForce:F6} delta={fwdDelta:F6}");
                    modDiverged = true;
                }

                float msDelta = Mathf.Abs(realStats.FinalMaxSpeed - shadowStats.FinalMaxSpeed);
                if (msDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalMaxSpeed real={realStats.FinalMaxSpeed:F6} shadow={shadowStats.FinalMaxSpeed:F6} delta={msDelta:F6}");
                    modDiverged = true;
                }

                float ttDelta = Mathf.Abs(realStats.FinalTurnTorque - shadowStats.FinalTurnTorque);
                if (ttDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalTurnTorque real={realStats.FinalTurnTorque:F6} shadow={shadowStats.FinalTurnTorque:F6} delta={ttDelta:F6}");
                    modDiverged = true;
                }

                float ssDelta = Mathf.Abs(realStats.FinalSteeringSign - shadowStats.FinalSteeringSign);
                if (ssDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=FinalSteeringSign real={realStats.FinalSteeringSign:F6} shadow={shadowStats.FinalSteeringSign:F6} delta={ssDelta:F6}");
                    modDiverged = true;
                }

                float smDelta = Mathf.Abs(realStats.ScaleMultiplier - shadowStats.ScaleMultiplier);
                if (smDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleMultiplier real={realStats.ScaleMultiplier:F6} shadow={shadowStats.ScaleMultiplier:F6} delta={smDelta:F6}");
                    modDiverged = true;
                }

                float smmDelta = Mathf.Abs(realStats.ScaleMassMultiplier - shadowStats.ScaleMassMultiplier);
                if (smmDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleMassMultiplier real={realStats.ScaleMassMultiplier:F6} shadow={shadowStats.ScaleMassMultiplier:F6} delta={smmDelta:F6}");
                    modDiverged = true;
                }

                float sffDelta = Mathf.Abs(realStats.ScaleForwardForceMultiplier - shadowStats.ScaleForwardForceMultiplier);
                if (sffDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-field delta: field=ScaleForwardForceMultiplier real={realStats.ScaleForwardForceMultiplier:F6} shadow={shadowStats.ScaleForwardForceMultiplier:F6} delta={sffDelta:F6}");
                    modDiverged = true;
                }

                if (realStats.IsRooted != shadowStats.IsRooted)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsRooted real={realStats.IsRooted} shadow={shadowStats.IsRooted}");
                    modDiverged = true;
                }

                if (realStats.IsInvertTurnActive != shadowStats.IsInvertTurnActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsInvertTurnActive real={realStats.IsInvertTurnActive} shadow={shadowStats.IsInvertTurnActive}");
                    modDiverged = true;
                }

                if (realStats.IsPushGraceActive != shadowStats.IsPushGraceActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsPushGraceActive real={realStats.IsPushGraceActive} shadow={shadowStats.IsPushGraceActive}");
                    modDiverged = true;
                }

                if (realStats.IsSteeringSuppressed != shadowStats.IsSteeringSuppressed)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsSteeringSuppressed real={realStats.IsSteeringSuppressed} shadow={shadowStats.IsSteeringSuppressed}");
                    modDiverged = true;
                }

                if (realStats.IsRoomBypassActive != shadowStats.IsRoomBypassActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} mod-flag mismatch: flag=IsRoomBypassActive real={realStats.IsRoomBypassActive} shadow={shadowStats.IsRoomBypassActive}");
                    modDiverged = true;
                }
            }

            // Handoff compare (3d) — 15 LaunchHandoffState fields. Real side mirrors
            // motor's _handoffState after the post-consume RefreshLaunchState; shadow
            // side re-runs Advance / ProjectForArrivalTick / FromData on snapshotted
            // pre-consume state via BuddahHandoffStep. Both paths call
            // BuddahPredictedLaunchHandoffResolver, so output is bit-identical by
            // construction — any delta > 1e-4 or flag mismatch points to divergent
            // input state (snapshot timing drift, serializer precision, etc.), NOT
            // resolver noise.
            if (_realScratch.HandoffRan != _shadowScratch.HandoffRan)
            {
                Debug.LogWarning($"[D-LOC] T={tick} hof-ran gate mismatch: real={_realScratch.HandoffRan} shadow={_shadowScratch.HandoffRan}");
                hofDiverged = true;
            }
            else if (_realScratch.HandoffRan
                     && _realScratch.ShadowLastConsumedHandoffId != _shadowScratch.ShadowLastConsumedHandoffId)
            {
                Debug.LogWarning($"[D-LOC] T={tick} hof-consume cursor mismatch: realId={_realScratch.ShadowLastConsumedHandoffId} shadowId={_shadowScratch.ShadowLastConsumedHandoffId}");
                hofDiverged = true;
            }

            {
                BuddahPredictedLaunchHandoffState realHof = _realScratch.ShadowHandoffState;
                BuddahPredictedLaunchHandoffState shadowHof = _shadowScratch.ShadowHandoffState;

                if (realHof.IsActive != shadowHof.IsActive)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-flag mismatch: flag=IsActive real={realHof.IsActive} shadow={shadowHof.IsActive}");
                    hofDiverged = true;
                }
                if (realHof.CurrentState != shadowHof.CurrentState)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-flag mismatch: flag=CurrentState real={realHof.CurrentState} shadow={shadowHof.CurrentState}");
                    hofDiverged = true;
                }
                if (realHof.EventId != shadowHof.EventId)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=EventId real={realHof.EventId} shadow={shadowHof.EventId}");
                    hofDiverged = true;
                }
                if (realHof.EventTick != shadowHof.EventTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=EventTick real={realHof.EventTick} shadow={shadowHof.EventTick}");
                    hofDiverged = true;
                }
                if (realHof.StartTick != shadowHof.StartTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=StartTick real={realHof.StartTick} shadow={shadowHof.StartTick}");
                    hofDiverged = true;
                }
                if (realHof.InheritEndTick != shadowHof.InheritEndTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=InheritEndTick real={realHof.InheritEndTick} shadow={shadowHof.InheritEndTick}");
                    hofDiverged = true;
                }
                if (realHof.BlendEndTick != shadowHof.BlendEndTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=BlendEndTick real={realHof.BlendEndTick} shadow={shadowHof.BlendEndTick}");
                    hofDiverged = true;
                }
                if (realHof.SuppressSteeringUntilTick != shadowHof.SuppressSteeringUntilTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=SuppressSteeringUntilTick real={realHof.SuppressSteeringUntilTick} shadow={shadowHof.SuppressSteeringUntilTick}");
                    hofDiverged = true;
                }
                if (realHof.RoomBypassUntilTick != shadowHof.RoomBypassUntilTick)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field mismatch: field=RoomBypassUntilTick real={realHof.RoomBypassUntilTick} shadow={shadowHof.RoomBypassUntilTick}");
                    hofDiverged = true;
                }

                float alphaDelta = Mathf.Abs(realHof.BlendAlpha - shadowHof.BlendAlpha);
                if (alphaDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=BlendAlpha real={realHof.BlendAlpha:F6} shadow={shadowHof.BlendAlpha:F6} delta={alphaDelta:F6}");
                    hofDiverged = true;
                }

                float posDelta = (realHof.SnapshotPosition - shadowHof.SnapshotPosition).magnitude;
                if (posDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotPosition magnitude={posDelta:F6}");
                    hofDiverged = true;
                }

                // Normalize default(Quaternion)=(0,0,0,0) to Quaternion.identity before Angle compare.
                // Quaternion.Angle(default, default) returns 180° (dot=0, 2*acos(0)=180°), producing
                // a false-positive divergence when both sides hold uninitialized quaternion (typical
                // pre-consume steady state with IsActive=false). Both default quaternions ARE equal;
                // only the compare tool misreports. Normalizing preserves divergence detection for
                // any non-zero rotation. See Docs/lessons-log.md L15.
                Quaternion realRot = realHof.SnapshotRotation;
                Quaternion shadowRot = shadowHof.SnapshotRotation;
                if (realRot.x == 0f && realRot.y == 0f && realRot.z == 0f && realRot.w == 0f)
                    realRot = Quaternion.identity;
                if (shadowRot.x == 0f && shadowRot.y == 0f && shadowRot.z == 0f && shadowRot.w == 0f)
                    shadowRot = Quaternion.identity;
                float rotDelta = Quaternion.Angle(realRot, shadowRot);
                if (rotDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotRotation angle-deg={rotDelta:F6}");
                    hofDiverged = true;
                }

                float velDelta = (realHof.SnapshotVelocity - shadowHof.SnapshotVelocity).magnitude;
                if (velDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotVelocity magnitude={velDelta:F6}");
                    hofDiverged = true;
                }

                float angDelta = (realHof.SnapshotAngularVelocity - shadowHof.SnapshotAngularVelocity).magnitude;
                if (angDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotAngularVelocity magnitude={angDelta:F6}");
                    hofDiverged = true;
                }

                float fwdDelta = (realHof.SnapshotForward - shadowHof.SnapshotForward).magnitude;
                if (fwdDelta > 1e-4f)
                {
                    Debug.LogWarning($"[D-LOC] T={tick} hof-field delta: field=SnapshotForward magnitude={fwdDelta:F6}");
                    hofDiverged = true;
                }
            }

            if (locDiverged) _dLocLocomotionDivCount++;
            if (telDiverged) _dLocTeleportDivCount++;
            if (modDiverged) _dLocModifierDivCount++;
            if (hofDiverged) _dLocHandoffDivCount++;

            bool anyDiverged = locDiverged || telDiverged || modDiverged || hofDiverged;
            if (anyDiverged)
                _dLocConsecutive++;
            else
                _dLocConsecutive = 0;

            if (_dLocConsecutive >= 60)
            {
                // Phase-agnostic FATAL: lists the diverged categories present this
                // window instead of naming a specific phase. Easier to maintain across
                // future shadow phases without per-phase text churn.
                string categories;
                {
                    var sb = new StringBuilder();
                    if (locDiverged) sb.Append(sb.Length > 0 ? ",loc" : "loc");
                    // V2b Step 1 — imp axis dropped from D-LOC composite FATAL string (Q4 amendment).
                    if (telDiverged) sb.Append(sb.Length > 0 ? ",tel" : "tel");
                    if (modDiverged) sb.Append(sb.Length > 0 ? ",mod" : "mod");
                    if (hofDiverged) sb.Append(sb.Length > 0 ? ",hof" : "hof");
                    categories = sb.ToString();
                }
                Debug.LogError($"[D-LOC FATAL] T={tick} shadow formula divergence: {{{categories}}}");
                _dLocConsecutive = 0;
            }
        }
#endif


    }
}
