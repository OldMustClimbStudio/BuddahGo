using UnityEngine;
using UnityEngine.InputSystem;

namespace NewBuddah.PredictionV2.Integration
{
    public sealed class BuddahPredictionOwnerInputBridge
    {
        private InputSystem_Actions _inputActions;
        private InputAction _movementAction;
        private PlayerBuddahInputSource _legacyInputSource;
        private bool _initialized;
        private bool _enabled;

        public bool IsInitialized => _initialized;
        public bool IsEnabled => _enabled;

        public void Initialize()
        {
            if (_initialized)
                return;

            _inputActions = new InputSystem_Actions();
            _movementAction = _inputActions.Player.Movement;
            _legacyInputSource = new PlayerBuddahInputSource(_movementAction);
            _initialized = true;
        }

        public void SetEnabled(bool enabled)
        {
            Initialize();

            if (_enabled == enabled)
                return;

            _enabled = enabled;
            if (_enabled)
                _inputActions.Enable();
            else
                _inputActions.Disable();
        }

        public float ReadSteering()
        {
            if (!_initialized || !_enabled || _legacyInputSource == null)
                return 0f;

            return _legacyInputSource.GetSteering();
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            _inputActions.Disable();
            _enabled = false;
        }
    }
}
