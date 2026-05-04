using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionHandoffBridge : MonoBehaviour
    {
        public readonly struct PredictionHandoffControlState
        {
            public readonly bool IntroControlActive;
            public readonly bool ExternalKinematicControlActive;
            public readonly bool AuthoritativeLaunchHandoffPending;
            public readonly bool LaunchHandoffConsumedOrActive;

            public PredictionHandoffControlState(
                bool introControlActive,
                bool externalKinematicControlActive,
                bool authoritativeLaunchHandoffPending,
                bool launchHandoffConsumedOrActive)
            {
                IntroControlActive = introControlActive;
                ExternalKinematicControlActive = externalKinematicControlActive;
                AuthoritativeLaunchHandoffPending = authoritativeLaunchHandoffPending;
                LaunchHandoffConsumedOrActive = launchHandoffConsumedOrActive;
            }
        }

        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictedMotor predictedMotor;
        private bool _loggedStateValid;
        private PredictionHandoffControlState _lastLoggedState;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<BuddahPredictedMotor>();
        }

        public bool IsPredictionHandoffActive()
        {
            return bootstrap != null && bootstrap.IsPredictionModeActive() && predictedMotor != null;
        }

        public bool IsLaunchHandoffActive()
        {
            return IsPredictionHandoffActive() && predictedMotor.IsLaunchHandoffActive;
        }

        public bool IsPredictionIntroControlActive()
        {
            return IsPredictionHandoffActive() && predictedMotor.IsPredictionIntroControlActive;
        }

        public bool IsPredictionExternalKinematicControlActive()
        {
            return IsPredictionHandoffActive() && predictedMotor.IsPredictionExternalKinematicControlActive;
        }

        public bool IsAuthoritativeLaunchHandoffPending()
        {
            return IsPredictionHandoffActive() && predictedMotor.IsAuthoritativeLaunchHandoffPending;
        }

        public bool IsAuthoritativeLaunchHandoffConsumedOrActive()
        {
            return IsPredictionHandoffActive() && predictedMotor.IsPredictionLaunchHandoffConsumedOrActive;
        }

        public PredictionHandoffControlState GetPredictionControlState()
        {
            if (!TryGetPredictionControlState(out PredictionHandoffControlState state))
                return default;

            return state;
        }

        public bool TryGetPredictionControlState(out PredictionHandoffControlState state)
        {
            state = default;

            if (!IsPredictionHandoffActive())
                return false;

            state = new PredictionHandoffControlState(
                predictedMotor.IsPredictionIntroControlActive,
                predictedMotor.IsPredictionExternalKinematicControlActive,
                predictedMotor.IsAuthoritativeLaunchHandoffPending,
                predictedMotor.IsPredictionLaunchHandoffConsumedOrActive);
            LogBridgeStateSnapshot(state);
            return true;
        }

        public bool TryGetPredictionControlState(out bool introControlActive, out bool externalKinematicControlActive, out bool authoritativeHandoffPending)
        {
            introControlActive = false;
            externalKinematicControlActive = false;
            authoritativeHandoffPending = false;

            if (!TryGetPredictionControlState(out PredictionHandoffControlState state))
                return false;

            introControlActive = state.IntroControlActive;
            externalKinematicControlActive = state.ExternalKinematicControlActive;
            authoritativeHandoffPending = state.AuthoritativeLaunchHandoffPending;
            return true;
        }

        private void LogBridgeStateSnapshot(PredictionHandoffControlState state)
        {
            if (_loggedStateValid
                && _lastLoggedState.IntroControlActive == state.IntroControlActive
                && _lastLoggedState.ExternalKinematicControlActive == state.ExternalKinematicControlActive
                && _lastLoggedState.AuthoritativeLaunchHandoffPending == state.AuthoritativeLaunchHandoffPending
                && _lastLoggedState.LaunchHandoffConsumedOrActive == state.LaunchHandoffConsumedOrActive)
            {
                return;
            }

            _loggedStateValid = true;
            _lastLoggedState = state;
            Debug.Log(
                $"[IntroHandoff][Bridge:{name}] prediction state intro={state.IntroControlActive} external={state.ExternalKinematicControlActive} " +
                $"pending={state.AuthoritativeLaunchHandoffPending} active={state.LaunchHandoffConsumedOrActive}");
        }

        public bool TrySetIntroControlActive(bool active)
        {
            if (!IsPredictionHandoffActive())
                return false;

            predictedMotor.SetPredictionIntroControlActive(active);
            return true;
        }

        public bool TrySetExternalKinematicControlActive(bool active)
        {
            if (!IsPredictionHandoffActive())
                return false;

            predictedMotor.SetPredictionExternalKinematicControlActive(active);
            return true;
        }

        // Phase 6 — owner-initiated TryBeginLaunchHandoff retired with the
        // RPC chain (Section 11.1 option a). Race-start lock is now driven by
        // server-side SyncVar (RoomStateManager._raceStartTick) consumed by
        // motor's per-tick gate; bridge no longer surfaces an entry point.
    }
}
