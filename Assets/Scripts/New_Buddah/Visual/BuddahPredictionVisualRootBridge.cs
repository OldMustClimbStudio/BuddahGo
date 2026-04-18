using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

namespace NewBuddah.PredictionV2.Visual
{
    [DisallowMultipleComponent]
    public class BuddahPredictionVisualRootBridge : MonoBehaviour
    {
        private const float PostIntroVisualLockPositionThreshold = 0.75f;
        private const float PostIntroVisualLockYawThreshold = 10f;
        private const float PostIntroVisualUnlockPositionThreshold = 0.25f;
        private const float PostIntroVisualUnlockYawThreshold = 4f;
        // Post-intro visual lock only engages briefly after intro/external control ends, so the
        // one-shot snap covers the handoff transition without fighting FishNet's graphical smoother
        // during normal high-speed gameplay.
        private const int PostIntroVisualLockMaxFramesAfterExit = 4;

        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictedMotor predictedMotor;
        [SerializeField] private BuddahPredictionPresentationBridge presentationBridge;
        [SerializeField] private Transform movementRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Rigidbody movementRigidbody;
        [SerializeField] private Transform cameraFollowTarget;
        [SerializeField] private Rigidbody cameraFollowRigidbody;
        [SerializeField] private PlayerScaleEffect visualScaleEffect;
        [Header("Owner Visual Stabilization")]
        [SerializeField] private bool stabilizeOwnerVisualRootInPrediction = true;
        [SerializeField] private bool lockVisualRootDuringIntroAndPresentation = true;

        private NetworkObject _networkObject;
        private bool _hasCachedVisualLocalPose;
        private Vector3 _cachedVisualLocalPosition;
        private Quaternion _cachedVisualLocalRotation = Quaternion.identity;
        private Vector3 _cachedVisualLocalScale = Vector3.one;
        private bool _lastStabilizationApplied;
        private string _lastStabilizationReason = "init";
        private bool _fishNetGraphicalSmoothingSuppressed;
        private RaceBodyIntroStateController _introStateController;
        private int _lastIntroOrExternalActiveFrame = int.MinValue / 2;

        private void Awake()
        {
            ResolveReferences();
            _networkObject = GetComponent<NetworkObject>();
            CacheVisualLocalPose(force: true);
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (bootstrap == null)
                return;

            BuddahPredictionDebugState debugState = bootstrap.DebugState;
            Transform resolvedMovementRoot = GetMovementRoot();
            Transform resolvedVisualRoot = GetVisualRoot();
            UpdateIntroOrExternalActiveTracking();
            UpdateFishNetGraphicalSmoothingState(resolvedMovementRoot, resolvedVisualRoot);
            StabilizeOwnerVisualRootIfNeeded(resolvedMovementRoot, resolvedVisualRoot, debugState);

            debugState.movementRootName = resolvedMovementRoot != null ? resolvedMovementRoot.name : "null";
            debugState.visualRootName = resolvedVisualRoot != null ? resolvedVisualRoot.name : "null";
            debugState.motorVisualPosDelta =
                resolvedMovementRoot != null && resolvedVisualRoot != null
                    ? Vector3.Distance(resolvedMovementRoot.position, resolvedVisualRoot.position)
                    : 0f;
            debugState.motorVisualYawDelta =
                resolvedMovementRoot != null && resolvedVisualRoot != null
                    ? Quaternion.Angle(resolvedMovementRoot.rotation, resolvedVisualRoot.rotation)
                    : 0f;
            debugState.visualScaleSource = GetVisualScaleSource();

            if (presentationBridge != null)
            {
                debugState.presentationControlActive = presentationBridge.IsPresentationControlActive;
                debugState.externalControlSource = presentationBridge.CurrentExternalControlSource;
            }
            else
            {
                debugState.presentationControlActive = false;
                debugState.externalControlSource = debugState.introControlActive
                    ? "intro"
                    : (debugState.externalKinematicControlActive ? "external" : "none");
            }
        }

        private void OnDisable()
        {
            RestoreFishNetGraphicalSmoothingIfNeeded();
        }

        public void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<BuddahPredictedMotor>();
            if (presentationBridge == null)
                presentationBridge = GetComponent<BuddahPredictionPresentationBridge>();
            if (movementRoot == null)
                movementRoot = transform;
            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();
            if (visualRoot == null)
            {
                // Prefer the FishNet graphical object so that smoother-suppression logic can
                // correctly identify it as a distinct visual root from the physics root.
                Transform fishNetGraphical = _networkObject != null ? _networkObject.GetGraphicalObject() : null;
                visualRoot = (fishNetGraphical != null && fishNetGraphical != transform)
                    ? fishNetGraphical
                    : transform;
            }
            CacheVisualLocalPose();
            if (_introStateController == null)
                _introStateController = GetComponent<RaceBodyIntroStateController>();
            if (movementRigidbody == null)
                movementRigidbody = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
            if (cameraFollowTarget == null)
                cameraFollowTarget = visualRoot != null ? visualRoot : transform;
            if (cameraFollowRigidbody == null)
                cameraFollowRigidbody = movementRigidbody;
            if (visualScaleEffect == null)
                visualScaleEffect = GetComponent<PlayerScaleEffect>()
                    ?? GetComponentInParent<PlayerScaleEffect>()
                    ?? GetComponentInChildren<PlayerScaleEffect>(true);
        }

        public Transform GetMovementRoot()
        {
            return movementRoot != null ? movementRoot : transform;
        }

        public Transform GetVisualRoot()
        {
            return visualRoot != null ? visualRoot : GetMovementRoot();
        }

        public Rigidbody GetMovementRigidbody()
        {
            if (movementRigidbody == null)
                movementRigidbody = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
            return movementRigidbody;
        }

        public Transform GetCameraFollowTarget()
        {
            return cameraFollowTarget != null ? cameraFollowTarget : GetVisualRoot();
        }

        public Rigidbody GetCameraFollowRigidbody()
        {
            if (cameraFollowRigidbody == null)
                cameraFollowRigidbody = GetMovementRigidbody();
            return cameraFollowRigidbody;
        }

        public float GetVisualScaleMultiplier()
        {
            if (visualScaleEffect != null)
                return Mathf.Max(0.1f, visualScaleEffect.CurrentScaleMultiplier);

            if (predictedMotor != null)
                return Mathf.Max(0.1f, predictedMotor.CurrentScaleMultiplier);

            return 1f;
        }

        public string GetVisualScaleSource()
        {
            if (visualScaleEffect != null)
                return nameof(PlayerScaleEffect);
            if (predictedMotor != null && bootstrap != null && bootstrap.IsPredictionModeActive())
                return nameof(BuddahPredictedMotor);
            return "default";
        }

        private bool IsFishNetPredictionSmoothingVisualRoot(Transform resolvedVisualRoot)
        {
            if (_networkObject == null || resolvedVisualRoot == null)
                return false;

            // FishNet prediction smoothing already owns this graphical object.
            Transform graphicalObject = _networkObject.GetGraphicalObject();
            return graphicalObject == resolvedVisualRoot && _networkObject.PredictionSmoother != null;
        }

        private void UpdateFishNetGraphicalSmoothingState(Transform resolvedMovementRoot, Transform resolvedVisualRoot)
        {
            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();

            // Only owner has a local spline/stabilization provider; suppressing the smoother on
            // spectators leaves their visual root without any driver during intro and produces
            // the opponent-side tearing the client sees.
            bool isOwner = _networkObject != null && _networkObject.IsOwner;
            bool shouldSuppress =
                isOwner &&
                ShouldLockVisualRootDuringIntroOrPresentation(resolvedMovementRoot, resolvedVisualRoot, out _) &&
                IsFishNetPredictionSmoothingVisualRoot(resolvedVisualRoot);

            if (shouldSuppress == _fishNetGraphicalSmoothingSuppressed)
                return;

            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();

            if (_networkObject?.PredictionSmoother == null)
                return;

            TransformPropertiesFlag properties = shouldSuppress
                ? TransformPropertiesFlag.Unset
                : TransformPropertiesFlag.Everything;

            _networkObject.PredictionSmoother.SetSmoothedProperties(properties, false);
            _networkObject.PredictionSmoother.SetSmoothedProperties(properties, true);
            _fishNetGraphicalSmoothingSuppressed = shouldSuppress;
            bootstrap?.LogVerbose(
                $"visual root graphical smoothing suppressed={shouldSuppress} properties={properties}");
        }

        private void RestoreFishNetGraphicalSmoothingIfNeeded()
        {
            if (!_fishNetGraphicalSmoothingSuppressed)
                return;

            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();

            if (_networkObject?.PredictionSmoother == null)
                return;

            _networkObject.PredictionSmoother.SetSmoothedProperties(TransformPropertiesFlag.Everything, false);
            _networkObject.PredictionSmoother.SetSmoothedProperties(TransformPropertiesFlag.Everything, true);
            _fishNetGraphicalSmoothingSuppressed = false;
        }

        private void CacheVisualLocalPose(bool force = false)
        {
            if (visualRoot == null)
                return;

            if (_hasCachedVisualLocalPose && !force)
                return;

            _cachedVisualLocalPosition = visualRoot.localPosition;
            _cachedVisualLocalRotation = visualRoot.localRotation;
            _cachedVisualLocalScale = visualRoot.localScale;
            _hasCachedVisualLocalPose = true;
        }

        private void StabilizeOwnerVisualRootIfNeeded(Transform resolvedMovementRoot, Transform resolvedVisualRoot, BuddahPredictionDebugState debugState)
        {
            bool applied = false;
            string reason = "inactive";

            if (ShouldApplyOwnerVisualStabilization(resolvedMovementRoot, resolvedVisualRoot, out reason))
            {
                bool isIntroSplineReason =
                    string.Equals(reason, "intro-visual-lock", System.StringComparison.Ordinal) ||
                    string.Equals(reason, "intro-visual-lock-legacy", System.StringComparison.Ordinal);

                if (!(isIntroSplineReason && TryApplyIntroSplineVisualPose(resolvedMovementRoot, resolvedVisualRoot)))
                    ApplyVisualRootStabilization(resolvedMovementRoot, resolvedVisualRoot);
                applied = true;
            }

            if (debugState != null)
            {
                debugState.ownerVisualRootStabilizationApplied = applied;
                debugState.ownerVisualRootStabilizationEnabled = stabilizeOwnerVisualRootInPrediction;
                debugState.ownerVisualRootStabilizationReason = reason;
            }

            if (bootstrap != null && (applied != _lastStabilizationApplied || !string.Equals(reason, _lastStabilizationReason, System.StringComparison.Ordinal)))
            {
                _lastStabilizationApplied = applied;
                _lastStabilizationReason = reason;
                bootstrap.LogVerbose($"visual stabilization ownerApplied={applied} reason={reason}");
            }
        }

        private bool ShouldApplyOwnerVisualStabilization(Transform resolvedMovementRoot, Transform resolvedVisualRoot, out string reason)
        {
            if (ShouldLockVisualRootDuringIntroOrPresentation(resolvedMovementRoot, resolvedVisualRoot, out reason))
                return true;

            if (!stabilizeOwnerVisualRootInPrediction)
            {
                reason = "disabled";
                return false;
            }

            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
            {
                reason = "prediction-inactive";
                return false;
            }

            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();

            if (_networkObject == null || !_networkObject.IsOwner)
            {
                reason = "not-owner";
                return false;
            }

            if (resolvedMovementRoot == null || resolvedVisualRoot == null)
            {
                reason = "missing-root";
                return false;
            }

            if (resolvedMovementRoot == resolvedVisualRoot)
            {
                reason = "same-root";
                return false;
            }

            if (IsFishNetPredictionSmoothingVisualRoot(resolvedVisualRoot))
            {
                reason = "fishnet-graphical-smoother";
                return false;
            }

            if (presentationBridge != null && presentationBridge.IsPresentationControlActive)
            {
                reason = "presentation-control";
                return false;
            }

            if (TryGetIntroVisualLockState(out bool introControlActive, out bool externalControlActive))
            {
                if (introControlActive)
                {
                    reason = "intro-control";
                    return false;
                }

                if (externalControlActive)
                {
                    reason = "external-control";
                    return false;
                }
            }

            reason = "active";
            return true;
        }

        private bool ShouldLockVisualRootDuringIntroOrPresentation(Transform resolvedMovementRoot, Transform resolvedVisualRoot, out string reason)
        {
            if (!lockVisualRootDuringIntroAndPresentation)
            {
                reason = "intro-lock-disabled";
                return false;
            }

            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
            {
                // In legacy movement mode, FishNet's PredictionSmoother is still active because
                // _enablePrediction is set on the NetworkObject independently of bootstrap mode.
                // Suppress it during intro to prevent graphical lag against the spline-driven rigidbody.
                if (_introStateController != null && _introStateController.IsIntroActive)
                {
                    reason = "intro-visual-lock-legacy";
                    return true;
                }
                reason = "prediction-inactive";
                return false;
            }

            if (resolvedMovementRoot == null || resolvedVisualRoot == null)
            {
                reason = "missing-root";
                return false;
            }

            if (resolvedMovementRoot == resolvedVisualRoot)
            {
                reason = "same-root";
                return false;
            }

            bool presentationControl = presentationBridge != null && presentationBridge.IsPresentationControlActive;
            bool introControlled = TryGetIntroVisualLockState(out bool introControlActive, out bool externalControlActive)
                && (introControlActive || externalControlActive);
            if (!presentationControl && !introControlled)
            {
                if (ShouldHoldPostIntroVisualLock(resolvedMovementRoot, resolvedVisualRoot))
                {
                    reason = "post-intro-visual-lock";
                    return true;
                }

                reason = "intro-lock-inactive";
                return false;
            }

            reason = presentationControl ? "presentation-visual-lock" : "intro-visual-lock";
            return true;
        }

        private bool TryGetIntroVisualLockState(out bool introControlActive, out bool externalControlActive)
        {
            introControlActive = false;
            externalControlActive = false;

            if (predictedMotor != null && bootstrap != null && bootstrap.IsPredictionModeActive())
            {
                introControlActive = predictedMotor.IsPredictionIntroControlActive;
                externalControlActive = predictedMotor.IsPredictionExternalKinematicControlActive;
                return true;
            }

            BuddahPredictionDebugState state = bootstrap != null ? bootstrap.DebugState : null;
            if (state == null)
                return false;

            introControlActive = state.introControlActive;
            externalControlActive = state.externalKinematicControlActive;
            return true;
        }

        private void UpdateIntroOrExternalActiveTracking()
        {
            if (IsIntroOrExternalControlCurrentlyActive())
                _lastIntroOrExternalActiveFrame = Time.frameCount;
        }

        private bool IsIntroOrExternalControlCurrentlyActive()
        {
            if (predictedMotor != null && bootstrap != null && bootstrap.IsPredictionModeActive()
                && (predictedMotor.IsPredictionIntroControlActive || predictedMotor.IsPredictionExternalKinematicControlActive))
            {
                return true;
            }

            if (_introStateController != null && _introStateController.IsIntroActive)
                return true;

            BuddahPredictionDebugState state = bootstrap != null ? bootstrap.DebugState : null;
            if (state != null && (state.introControlActive || state.externalKinematicControlActive))
                return true;

            return false;
        }

        private bool ShouldHoldPostIntroVisualLock(Transform resolvedMovementRoot, Transform resolvedVisualRoot)
        {
            if (bootstrap == null || !bootstrap.IsPredictionModeActive())
                return false;

            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();

            if (_networkObject == null || !_networkObject.IsOwner)
                return false;

            if (presentationBridge != null && presentationBridge.IsPresentationControlActive)
                return false;

            if (TryGetIntroVisualLockState(out bool introControlActive, out bool externalControlActive)
                && (introControlActive || externalControlActive))
            {
                return false;
            }

            if (resolvedMovementRoot == null || resolvedVisualRoot == null || resolvedMovementRoot == resolvedVisualRoot)
                return false;

            // Only engage for a brief window after intro/external control actually ended. Without
            // this gate, per-tick physics displacement (~1.2m at 60 m/s) keeps the position/yaw
            // delta above the threshold indefinitely, re-suppressing FishNet's smoother and
            // producing 50Hz stair-stepping during normal gameplay.
            int framesSinceIntroExit = Time.frameCount - _lastIntroOrExternalActiveFrame;
            if (framesSinceIntroExit < 0 || framesSinceIntroExit > PostIntroVisualLockMaxFramesAfterExit)
                return false;

            float positionDelta = Vector3.Distance(resolvedMovementRoot.position, resolvedVisualRoot.position);
            float yawDelta = Quaternion.Angle(resolvedMovementRoot.rotation, resolvedVisualRoot.rotation);
            bool wasHoldingPostIntro = _lastStabilizationApplied
                && string.Equals(_lastStabilizationReason, "post-intro-visual-lock", System.StringComparison.Ordinal);
            float positionThreshold = wasHoldingPostIntro
                ? PostIntroVisualUnlockPositionThreshold
                : PostIntroVisualLockPositionThreshold;
            float yawThreshold = wasHoldingPostIntro
                ? PostIntroVisualUnlockYawThreshold
                : PostIntroVisualLockYawThreshold;

            return positionDelta >= positionThreshold || yawDelta >= yawThreshold;
        }

        // Drives the visual root from the intro spline sampled at sub-tick render time, bypassing
        // the 50Hz stair-step that direct rigidbody.position writes cause on the movement root.
        private bool TryApplyIntroSplineVisualPose(Transform resolvedMovementRoot, Transform resolvedVisualRoot)
        {
            if (resolvedVisualRoot == null || resolvedMovementRoot == null)
                return false;

            if (resolvedVisualRoot == resolvedMovementRoot)
                return false;

            if (_introStateController == null)
                _introStateController = GetComponent<RaceBodyIntroStateController>();

            if (_introStateController == null)
                return false;

            if (!_introStateController.TrySampleVisualPoseAtRenderTime(out Vector3 splinePosition, out Quaternion splineRotation))
                return false;

            if (!_hasCachedVisualLocalPose)
                CacheVisualLocalPose(force: true);

            Vector3 worldPosition = _hasCachedVisualLocalPose
                ? splinePosition + splineRotation * _cachedVisualLocalPosition
                : splinePosition;
            Quaternion worldRotation = _hasCachedVisualLocalPose
                ? splineRotation * _cachedVisualLocalRotation
                : splineRotation;

            resolvedVisualRoot.SetPositionAndRotation(worldPosition, worldRotation);
            return true;
        }

        private void ApplyVisualRootStabilization(Transform resolvedMovementRoot, Transform resolvedVisualRoot)
        {
            if (resolvedMovementRoot == null || resolvedVisualRoot == null)
                return;

            if (!_hasCachedVisualLocalPose)
                CacheVisualLocalPose(force: true);

            if (resolvedVisualRoot.parent == resolvedMovementRoot && _hasCachedVisualLocalPose)
            {
                resolvedVisualRoot.localPosition = _cachedVisualLocalPosition;
                resolvedVisualRoot.localRotation = _cachedVisualLocalRotation;
                resolvedVisualRoot.localScale = _cachedVisualLocalScale;
                return;
            }

            if (_hasCachedVisualLocalPose)
            {
                Vector3 worldPosition = resolvedMovementRoot.TransformPoint(_cachedVisualLocalPosition);
                Quaternion worldRotation = resolvedMovementRoot.rotation * _cachedVisualLocalRotation;
                resolvedVisualRoot.SetPositionAndRotation(worldPosition, worldRotation);
            }
            else
            {
                resolvedVisualRoot.SetPositionAndRotation(resolvedMovementRoot.position, resolvedMovementRoot.rotation);
            }
        }
    }
}
