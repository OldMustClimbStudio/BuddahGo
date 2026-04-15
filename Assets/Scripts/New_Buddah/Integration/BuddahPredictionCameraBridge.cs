using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Debugging;
using NewBuddah.PredictionV2.Visual;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionCameraBridge : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictionVisualRootBridge visualRootBridge;
        [SerializeField] private PlayerCamera playerCamera;
        [Header("Diagnostics")]
        [SerializeField] private bool forceDefaultFollowTargetForDiagnosis;

        private Transform _reportedFollowTarget;
        private Rigidbody _reportedFollowRigidbody;
        private string _reportedFollowSource = "none";

        public bool ForceDefaultFollowTargetForDiagnosis => forceDefaultFollowTargetForDiagnosis;

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (bootstrap == null || playerCamera == null)
                return;

            BuddahPredictionDebugState debugState = bootstrap.DebugState;
            debugState.cameraMode = playerCamera.CurrentPresentationMode.ToString();
            bool hasDefaultTarget = TryGetDefaultFollowTarget(out Transform defaultTarget, out _);
            debugState.cameraDefaultFollowTargetName = hasDefaultTarget && defaultTarget != null ? defaultTarget.name : "null";
            debugState.cameraReportedFollowTargetName = _reportedFollowTarget != null ? _reportedFollowTarget.name : "null";
            debugState.cameraFollowDebugSource = _reportedFollowSource;
            debugState.cameraForceDefaultFollowTarget = forceDefaultFollowTargetForDiagnosis;

            Transform resolvedFollowTarget = ResolveDebugFollowTarget(defaultTarget);
            debugState.cameraFollowTargetName = resolvedFollowTarget != null ? resolvedFollowTarget.name : "null";
        }

        public void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (visualRootBridge == null)
                visualRootBridge = GetComponent<BuddahPredictionVisualRootBridge>();
            if (playerCamera == null)
                playerCamera = GetComponent<PlayerCamera>() ?? GetComponentInChildren<PlayerCamera>(true);
        }

        public bool TryGetDefaultFollowTarget(out Transform followTarget, out Rigidbody followRigidbody)
        {
            if (bootstrap == null || !bootstrap.IsPredictionModeActive() || visualRootBridge == null)
            {
                followTarget = null;
                followRigidbody = null;
                return false;
            }

            followTarget = visualRootBridge.GetCameraFollowTarget();
            followRigidbody = visualRootBridge.GetCameraFollowRigidbody();
            return followTarget != null;
        }

        public void ReportFollowTarget(PlayerCamera.CameraPresentationMode mode, Transform followTarget, Rigidbody followRigidbody, string source = "unknown")
        {
            _reportedFollowTarget = followTarget;
            _reportedFollowRigidbody = followRigidbody;
            _reportedFollowSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;

            if (bootstrap == null)
                return;

            BuddahPredictionDebugState debugState = bootstrap.DebugState;
            debugState.cameraMode = mode.ToString();
            debugState.cameraReportedFollowTargetName = followTarget != null ? followTarget.name : "null";
            bool hasDefaultTarget = TryGetDefaultFollowTarget(out Transform defaultTarget, out _);
            debugState.cameraDefaultFollowTargetName = hasDefaultTarget && defaultTarget != null ? defaultTarget.name : "null";
            debugState.cameraFollowDebugSource = _reportedFollowSource;
            debugState.cameraForceDefaultFollowTarget = forceDefaultFollowTargetForDiagnosis;
            Transform resolvedFollowTarget = ResolveDebugFollowTarget(defaultTarget);
            debugState.cameraFollowTargetName = resolvedFollowTarget != null ? resolvedFollowTarget.name : "null";
        }

        private Transform ResolveDebugFollowTarget(Transform defaultTarget)
        {
            if (forceDefaultFollowTargetForDiagnosis)
                return defaultTarget;

            if (_reportedFollowTarget != null)
                return _reportedFollowTarget;

            return defaultTarget;
        }
    }
}
