using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    [DisallowMultipleComponent]
    public class BuddahPredictionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private Vector2 anchor = new Vector2(16f, 16f);
        [SerializeField] private Vector2 panelSize = new Vector2(760f, 560f);
        [Header("Console Mirror")]
        [SerializeField] private bool mirrorSummaryToConsole = true;
        [SerializeField, Min(0.1f)] private float consoleMirrorIntervalSeconds = 0.5f;

        private NetworkObject _networkObject;
        private float _lastConsoleMirrorTime = float.NegativeInfinity;
        private string _lastConsoleMirrorSummary = string.Empty;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();

            _networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (bootstrap == null || bootstrap.DebugState == null || !mirrorSummaryToConsole)
                return;

            if (_networkObject != null && _networkObject.IsClientInitialized && !_networkObject.IsOwner)
                return;

            if (Time.unscaledTime - _lastConsoleMirrorTime < consoleMirrorIntervalSeconds)
                return;

            string summary = BuildConsoleSummary(bootstrap.DebugState);
            if (string.Equals(summary, _lastConsoleMirrorSummary, System.StringComparison.Ordinal))
                return;

            _lastConsoleMirrorTime = Time.unscaledTime;
            _lastConsoleMirrorSummary = summary;
            Debug.Log($"[PredictionOverlay:{name}] {summary}");
        }

        private void OnGUI()
        {
            if (bootstrap == null || bootstrap.DebugSettings == null || !bootstrap.DebugSettings.enableOnScreenDebug)
                return;

            if (_networkObject != null && _networkObject.IsClientInitialized && !_networkObject.IsOwner)
                return;

            BuddahPredictionDebugState state = bootstrap.DebugState;
            Rect rect = new Rect(anchor.x, anchor.y, panelSize.x, panelSize.y);
            GUI.Box(rect, "Buddah Prediction V2");

            Rect labelRect = new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, rect.height - 34f);
            string text =
                $"Mode: {state.currentMode}\n" +
                $"Primary Track: {state.primaryTrack} | Complete: {state.primaryTrackComplete} | Fallback Active: {state.fallbackActive}\n" +
                $"Health: {state.healthSummary}\n" +
                $"Prediction Active: {state.predictionActive}\n" +
                $"Legacy Movement Enabled: {state.legacyMovementEnabled}\n" +
                $"Predicted Motor Enabled: {state.predictedMotorEnabled}\n" +
                $"Compat Count: {state.compatibilityLayerCount} | AutoAdd: {state.runtimeAutoAddSummary}\n" +
                $"Compat Layers: {state.compatibilitySummary}\n" +
                $"Fallback Paths: {state.fallbackSummary}\n" +
                $"High Risk: {state.highRiskFlagsSummary}\n" +
                $"System Map: {state.systemMapSummary}\n" +
                $"Prediction Block: {state.predictionBlockReason}\n" +
                $"Tick: {state.currentTick} | Replicate: {state.lastReplicateTick} | Reconcile: {state.lastReconcileTick}\n" +
                $"Allowed: {state.movementAllowed} | Blocked: {state.gateBlocked} | OwnerInput: {state.ownerInputLive}\n" +
                $"InputBridge: {state.inputBridgeEnabled} | rb.isKinematic: {state.rigidbodyIsKinematic}\n" +
                $"Steering: {state.steeringInput:0.000} | Sign: {state.finalSteeringSign:0.0} | Speed: {state.planarSpeed:0.00}\n" +
                $"ForwardForce: {state.finalForwardForce:0.00} | MaxSpeed: {state.finalMaxSpeed:0.00} | TurnTorque: {state.finalTurnTorque:0.00}\n" +
                $"Rooted: {state.rooted} | Invert: {state.invertTurnActive} | Scale: {state.scaleMultiplier:0.00}\n" +
                $"PushGrace: {state.pushGraceActive} | Suppress: {state.suppressSteeringActive} | Bypass: {state.roomBypassActive}\n" +
                $"Impulse Pending: {state.pendingImpulseCount} | LastId: {state.lastImpulseEventId} | LastType: {state.lastImpulseSourceType}\n" +
                $"Impulse Tick: {state.lastImpulseEventTick} | Consumed: {state.lastImpulseConsumed} | Torque: {state.lastImpulseTurnTorque:0.00}\n" +
                $"Impulse Vec: {state.lastImpulseVector} | Pre/Post Speed: {state.preImpulseSpeed:0.00}/{state.postImpulseSpeed:0.00}\n" +
                $"Teleport Id: {state.lastTeleportEventId} | Type: {state.lastTeleportSourceType} | Tick: {state.lastTeleportEventTick} | Done: {state.lastTeleportConsumed}\n" +
                $"Teleport Target: {state.lastTeleportTargetPosition} | Yaw: {state.lastTeleportTargetYaw:0.0} | Progress: {state.lastTeleportTargetProgress01:0.000}\n" +
                $"Teleport Pre/Post Pos: {state.preTeleportPosition} / {state.postTeleportPosition}\n" +
                $"Teleport Pre/Post Spd: {state.preTeleportSpeed:0.00}/{state.postTeleportSpeed:0.00} | Ang: {state.preTeleportAngularSpeed:0.00}/{state.postTeleportAngularSpeed:0.00}\n" +
                $"Teleport Clears M:{state.modifiersClearedByTeleport} I:{state.impulseQueueClearedByTeleport} P:{state.pushGraceClearedByTeleport} Snap:{state.progressSnappedByTeleport} Trail:{state.trailRebasedByTeleport}\n" +
                $"Intro: {state.introControlActive} | External: {state.externalKinematicControlActive} | Handoff: {state.handoffActive} | LaunchState: {state.launchState}\n" +
                $"Handoff Id: {state.lastHandoffEventId} | Tick: {state.lastHandoffEventTick} | Start: {state.handoffStartTick}\n" +
                $"Handoff LockedUntil: {state.handoffLockedUntilTick}\n" +
                $"Handoff Snapshot Pos: {state.handoffSnapshotPosition} | Yaw: {state.handoffSnapshotYaw:0.0}\n" +
                $"Handoff Snapshot Speed/Ang: {state.handoffSnapshotSpeed:0.00}/{state.handoffSnapshotAngularSpeed:0.00} | Pre/Post Speed: {state.preHandoffSpeed:0.00}/{state.postHandoffSpeed:0.00}\n" +
                $"Handoff Suppress: {state.handoffSuppressSteeringActive} | Handoff Bypass: {state.handoffRoomBypassActive}\n" +
                $"Movement Root: {state.movementRootName} | Visual Root: {state.visualRootName}\n" +
                $"Visual Stabilize(owner): {state.ownerVisualRootStabilizationApplied} | Enabled: {state.ownerVisualRootStabilizationEnabled} | Reason: {state.ownerVisualRootStabilizationReason}\n" +
                $"Camera Mode: {state.cameraMode} | Follow Target: {state.cameraFollowTargetName}\n" +
                $"Camera Default/Reported: {state.cameraDefaultFollowTargetName}/{state.cameraReportedFollowTargetName} | Source: {state.cameraFollowDebugSource} | ForceDefault: {state.cameraForceDefaultFollowTarget}\n" +
                $"Visual Scale Source: {state.visualScaleSource} | External Source: {state.externalControlSource} | PresentationCtrl: {state.presentationControlActive}\n" +
                $"Motor/Visual Delta Pos: {state.motorVisualPosDelta:0.000} | Yaw: {state.motorVisualYawDelta:0.0}\n" +
                $"Reconcile Correction: {state.reconcileHadCorrection} | PosDelta: {state.lastReconcilePositionDelta:0.000} | VelDelta: {state.lastReconcileVelocityDelta:0.000}\n" +
                $"VelDelta Planar/Y: {state.lastReconcileVelocityPlanarDelta:0.000}/{state.lastReconcileVelocityVerticalDelta:0.000}\n" +
                $"Ticks root={state.rootUntilTick} accel={state.accelUntilTick} post={state.postRootAccelUntilTick} scale={state.scaleUntilTick}\n" +
                $"Ticks invert={state.invertTurnUntilTick} pushGrace={state.pushGraceUntilTick} suppress={state.suppressSteeringUntilTick} bypass={state.roomBypassUntilTick}\n" +
                $"Pending Impulses: {state.pendingImpulseSummary}\n" +
                $"Modifiers: {state.activeModifiers}\n" +
                $"Legacy Duties: {state.legacyResponsibilitySummary}\n" +
                $"Validation Focus: {state.validationChecklistSummary}\n" +
                $"Movement Checks: {state.movementChecklistSummary}\n" +
                $"Combat Checks: {state.combatChecklistSummary}\n" +
                $"Presentation Checks: {state.presentationChecklistSummary}\n" +
                $"Legacy Checks: {state.legacyChecklistSummary}\n" +
                $"Replicate: {state.replicateSummary}\n" +
                $"Reconcile: {state.reconcileSummary}\n" +
                $"Lifecycle: {state.lastLifecycleMessage}";
            GUI.Label(labelRect, text);
        }

        private static string BuildConsoleSummary(BuddahPredictionDebugState state)
        {
            return
                $"tick={state.currentTick} launch={state.launchState} intro={state.introControlActive} external={state.externalKinematicControlActive} " +
                $"handoff={state.handoffActive} lockedUntil={state.handoffLockedUntilTick} allowed={state.movementAllowed} blocked={state.gateBlocked} " +
                $"speed={state.planarSpeed:0.00} postHandoff={state.postHandoffSpeed:0.00} visDelta={state.motorVisualPosDelta:0.000}/{state.motorVisualYawDelta:0.0} " +
                $"ownerVis={state.ownerVisualRootStabilizationApplied}:{state.ownerVisualRootStabilizationReason} " +
                $"rep='{state.replicateSummary}' rec='{state.reconcileSummary}'";
        }
    }
}
