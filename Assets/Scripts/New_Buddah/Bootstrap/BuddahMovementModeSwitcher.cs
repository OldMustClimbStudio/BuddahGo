using NewBuddah.PredictionV2.Integration;
using UnityEngine;

namespace NewBuddah.PredictionV2.Bootstrap
{
    [DisallowMultipleComponent]
    public class BuddahMovementModeSwitcher : MonoBehaviour
    {
        [SerializeField] private BuddahMovementRuntimeMode runtimeMode = BuddahMovementRuntimeMode.Legacy;
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private Core.BuddahPredictedMotor predictedMotor;

        private readonly BuddahPredictionLegacyIsolationBridge _legacyIsolationBridge = new();
        private BuddahMovementRuntimeMode _appliedMode = (BuddahMovementRuntimeMode)(-1);

        public BuddahMovementRuntimeMode RuntimeMode => runtimeMode;

        private void Awake()
        {
            ResolveReferences();
            ApplyRuntimeMode(force: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyRuntimeMode(force: true);
        }

        private void Update()
        {
            if (_appliedMode != runtimeMode)
                ApplyRuntimeMode(force: false);
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<Core.BuddahPredictedMotor>();

            bootstrap?.ResolveReferences();
        }

        public void ApplyRuntimeMode(bool force)
        {
            if (!force && _appliedMode == runtimeMode)
                return;

            if (bootstrap == null)
                return;

            _legacyIsolationBridge.ApplyMode(runtimeMode, bootstrap.LegacyComponentRefs, predictedMotor, bootstrap.DebugState, bootstrap.CommandBus);
            _appliedMode = runtimeMode;
            bootstrap.RefreshDebugBanner($"mode switch applied -> {runtimeMode}");

            if (runtimeMode == BuddahMovementRuntimeMode.Legacy)
                bootstrap.LogVerbose("Movement mode set to Legacy. Prediction motor remains isolated.");
            else
                bootstrap.LogVerbose("Movement mode set to PredictionV2. Legacy movement disabled.");
        }
    }
}
