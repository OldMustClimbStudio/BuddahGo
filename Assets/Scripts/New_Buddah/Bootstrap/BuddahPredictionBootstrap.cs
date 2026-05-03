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
        // Phase 4b V2a — pure-C# adapter (not a UnityEngine.Object ref).
        // Field initializer runs before Awake; the bus reference is wired via
        // Initialize(commandBus) in Awake; the L7 latch (MarkReady) is flipped
        // later from BuddahPredictedMotor.RunInputs's first post-spawn tick.
        private readonly BuddahPredictionCombatAdapter _combatAdapter = new();
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
        public BuddahPredictionCombatAdapter CombatAdapter => _combatAdapter;
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
            // Phase 4b V2a — wire the bus reference into the adapter. The L7
            // contract (lessons-log L7) FORBIDS adapter enqueues during
            // Awake/OnEnable/OnStartNetwork; Initialize(bus) only stores the
            // reference and does NOT flip the L7 latch. The motor calls
            // _combatAdapter.MarkReady() from its first [Replicate] tick (after
            // BuddahMovementModeSwitcher's double-ApplyMode and its 4
            // [CommandBus]:ClearAll lines have all landed).
            _combatAdapter.Initialize(commandBus);
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
#if UNITY_EDITOR && BUDDAH_PREDICTION_RECONCILE_PROBE
            // Phase 7 Stage 4 extension2 wiring (per Yonezawa workflow simplification):
            // attach Q4 reconcile-snap probe per-Buddah. Probe self-resolves NetworkObject
            // via GetComponentInParent fallback. UNITY_EDITOR + define gated; compiles out
            // of production builds.
            if (GetComponent<BuddahPredictionReconcileSnapProbe>() == null)
                gameObject.AddComponent<BuddahPredictionReconcileSnapProbe>();
#endif
#if UNITY_EDITOR && BUDDAH_PREDICTION_FRAMETIME_PROBE
            // Phase 7 Stage 4 extension2 wiring: attach machine-global frame-time probe.
            // No NetworkObject ref needed (machine-global); attached on each Buddah is
            // harmless because [DisallowMultipleComponent] dedupes within a GameObject and
            // Time.unscaledDeltaTime is the same across all instances on the same machine
            // (multiple emits per HB across N Buddahs is acceptable extra signal at the
            // analyzer's per-(SourceFile, Owner) cadence groupby).
            if (GetComponent<BuddahPredictionFrameTimeProbe>() == null)
                gameObject.AddComponent<BuddahPredictionFrameTimeProbe>();
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
