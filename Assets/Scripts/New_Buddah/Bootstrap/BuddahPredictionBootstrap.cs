using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Debugging;
using NewBuddah.PredictionV2.Events;
using NewBuddah.PredictionV2.Integration;
using NewBuddah.PredictionV2.Validation;
using NewBuddah.PredictionV2.Visual;
using UnityEngine;

namespace NewBuddah.PredictionV2.Bootstrap
{
    [DisallowMultipleComponent]
    public class BuddahPredictionBootstrap : MonoBehaviour
    {
        public const string LogPrefix = "[BuddahPredictionV2]";

        [SerializeField] private BuddahMovementModeSwitcher modeSwitcher;
        [SerializeField] private Core.BuddahPredictedMotor predictedMotor;
        [SerializeField] private BuddahPredictedMotorConfig predictedMotorConfig;
        [SerializeField] private BuddahPredictionSkillMovementBridge skillMovementBridge;
        [SerializeField] private BuddahPredictionRespawnBridge respawnBridge;
        [SerializeField] private BuddahPredictionHandoffBridge handoffBridge;
        [SerializeField] private BuddahPredictionVisualRootBridge visualRootBridge;
        [SerializeField] private BuddahPredictionCameraBridge cameraBridge;
        [SerializeField] private BuddahPredictionPresentationBridge presentationBridge;
        [SerializeField] private BuddahPredictionCompatibilityRegistry compatibilityRegistry;
        [SerializeField] private BuddahPredictionRuntimeHealthReport runtimeHealthReport;
        [SerializeField] private BuddahPredictionCommandBus commandBus;
        [SerializeField] private BuddahPredictionDebugSettings debugSettings = new();
        [SerializeField] private BuddahLegacyComponentRefs legacyComponentRefs = new();
        [SerializeField] private BuddahPredictionDebugState debugState = new();

        private bool _visualRootBridgeAutoAdded;
        private bool _cameraBridgeAutoAdded;
        private bool _presentationBridgeAutoAdded;
        private bool _compatibilityRegistryAutoAdded;
        private bool _runtimeHealthReportAutoAdded;

        public BuddahMovementModeSwitcher ModeSwitcher => modeSwitcher;
        public Core.BuddahPredictedMotor PredictedMotor => predictedMotor;
        public BuddahPredictedMotorConfig PredictedMotorConfig => predictedMotorConfig;
        public BuddahPredictionSkillMovementBridge SkillMovementBridge => skillMovementBridge;
        public BuddahPredictionRespawnBridge RespawnBridge => respawnBridge;
        public BuddahPredictionHandoffBridge HandoffBridge => handoffBridge;
        public BuddahPredictionVisualRootBridge VisualRootBridge => visualRootBridge;
        public BuddahPredictionCameraBridge CameraBridge => cameraBridge;
        public BuddahPredictionPresentationBridge PresentationBridge => presentationBridge;
        public BuddahPredictionCompatibilityRegistry CompatibilityRegistry => compatibilityRegistry;
        public BuddahPredictionRuntimeHealthReport RuntimeHealthReport => runtimeHealthReport;
        public BuddahPredictionCommandBus CommandBus => commandBus;
        public BuddahPredictionDebugSettings DebugSettings => debugSettings;
        public BuddahLegacyComponentRefs LegacyComponentRefs => legacyComponentRefs;
        public BuddahPredictionDebugState DebugState => debugState;
        public BuddahMovementRuntimeMode RuntimeMode => modeSwitcher != null ? modeSwitcher.RuntimeMode : BuddahMovementRuntimeMode.Legacy;
        public bool WasVisualRootBridgeAutoAdded => _visualRootBridgeAutoAdded;
        public bool WasCameraBridgeAutoAdded => _cameraBridgeAutoAdded;
        public bool WasPresentationBridgeAutoAdded => _presentationBridgeAutoAdded;
        public bool WasCompatibilityRegistryAutoAdded => _compatibilityRegistryAutoAdded;
        public bool WasRuntimeHealthReportAutoAdded => _runtimeHealthReportAutoAdded;

        private void Awake()
        {
            ResolveReferences();
#if BUDDAH_PREDICTION_VISUAL_PROBE
            // Phase 4-probes wiring: attach the V3 visual-shake probe per-Buddah.
            // Runs on every peer (this MonoBehaviour lives on the replicated
            // Buddah prefab, so Awake fires on both HOST-instantiated and
            // CLIENT-replicated copies). The probe's own Awake resolves
            // _visualRoot via visualRootBridge and _networkObject via the
            // Buddah's own NetworkObject. Gated behind the probe define —
            // compiles out in release builds.
            if (GetComponent<BuddahPredictionVisualShakeProbe>() == null)
                gameObject.AddComponent<BuddahPredictionVisualShakeProbe>();
#endif
            RefreshDebugBanner("bootstrap initialized");
            LogVerbose($"bootstrap initialized. mode={RuntimeMode}");
        }

        public void ResolveReferences()
        {
            if (modeSwitcher == null)
                modeSwitcher = GetComponent<BuddahMovementModeSwitcher>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<Core.BuddahPredictedMotor>();
            if (predictedMotorConfig == null)
                predictedMotorConfig = GetComponent<BuddahPredictedMotorConfig>();
            if (skillMovementBridge == null)
                skillMovementBridge = GetComponent<BuddahPredictionSkillMovementBridge>();
            if (respawnBridge == null)
                respawnBridge = GetComponent<BuddahPredictionRespawnBridge>();
            if (handoffBridge == null)
                handoffBridge = GetComponent<BuddahPredictionHandoffBridge>();
            if (visualRootBridge == null)
            {
                visualRootBridge = GetComponent<BuddahPredictionVisualRootBridge>();
                if (visualRootBridge == null)
                {
                    visualRootBridge = gameObject.AddComponent<BuddahPredictionVisualRootBridge>();
                    _visualRootBridgeAutoAdded = true;
                }
            }
            if (cameraBridge == null)
            {
                cameraBridge = GetComponent<BuddahPredictionCameraBridge>();
                if (cameraBridge == null)
                {
                    cameraBridge = gameObject.AddComponent<BuddahPredictionCameraBridge>();
                    _cameraBridgeAutoAdded = true;
                }
            }
            if (presentationBridge == null)
            {
                presentationBridge = GetComponent<BuddahPredictionPresentationBridge>();
                if (presentationBridge == null)
                {
                    presentationBridge = gameObject.AddComponent<BuddahPredictionPresentationBridge>();
                    _presentationBridgeAutoAdded = true;
                }
            }
            if (compatibilityRegistry == null)
            {
                compatibilityRegistry = GetComponent<BuddahPredictionCompatibilityRegistry>();
                if (compatibilityRegistry == null)
                {
                    compatibilityRegistry = gameObject.AddComponent<BuddahPredictionCompatibilityRegistry>();
                    _compatibilityRegistryAutoAdded = true;
                }
            }
            if (runtimeHealthReport == null)
            {
                runtimeHealthReport = GetComponent<BuddahPredictionRuntimeHealthReport>();
                if (runtimeHealthReport == null)
                {
                    runtimeHealthReport = gameObject.AddComponent<BuddahPredictionRuntimeHealthReport>();
                    _runtimeHealthReportAutoAdded = true;
                }
            }
            if (commandBus == null)
                commandBus = GetComponent<BuddahPredictionCommandBus>();

            legacyComponentRefs.Resolve(gameObject);
            compatibilityRegistry?.ResolveReferences();
            runtimeHealthReport?.ResolveReferences();
        }

        public bool IsPredictionModeActive()
        {
            return RuntimeMode == BuddahMovementRuntimeMode.PredictionV2;
        }

        public void RefreshDebugBanner(string lifecycleMessage)
        {
            debugState.currentMode = RuntimeMode;
            debugState.predictionActive = IsPredictionModeActive();
            debugState.predictedMotorEnabled = predictedMotor != null && predictedMotor.enabled;
            debugState.legacyMovementEnabled = legacyComponentRefs.Movement != null && legacyComponentRefs.Movement.enabled;
            debugState.lastLifecycleMessage = lifecycleMessage;
            compatibilityRegistry?.RefreshRegistry();
            runtimeHealthReport?.RefreshHealthReport();
        }

        public void LogVerbose(string message)
        {
            if (debugSettings == null)
                return;

            if (debugSettings.enableVerboseLogs || NetDebug.EnableVerboseLog)
                Debug.Log($"{LogPrefix} {message}", this);
        }
    }
}
