using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.Network.Match
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class PlaceholderPlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeedDegrees = 720f;
        [SerializeField] private float _inputSendInterval = 0.05f;

        private CharacterController _characterController;
        private Vector2 _serverMoveInput;
        private Vector2 _lastSentMoveInput;
        private float _nextInputSendTime;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RefreshCharacterControllerState();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RefreshCharacterControllerState();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            RefreshCharacterControllerState();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RefreshCharacterControllerState();
        }

        private void Update()
        {
            if (IsOwner)
            {
                CaptureAndSubmitOwnerInput();
            }

            if (IsServer)
            {
                SimulateServerMovement();
            }
        }

        private void CaptureAndSubmitOwnerInput()
        {
            Vector2 moveInput = ReadMoveInput();

            if (IsServer)
            {
                _serverMoveInput = moveInput;
                return;
            }

            bool inputChanged = (moveInput - _lastSentMoveInput).sqrMagnitude > 0.0001f;
            bool sendKeepAlive = Time.unscaledTime >= _nextInputSendTime;

            if (!inputChanged && !sendKeepAlive)
                return;

            _lastSentMoveInput = moveInput;
            _nextInputSendTime = Time.unscaledTime + _inputSendInterval;
            SubmitMoveInputServerRpc(moveInput);
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 moveInput = Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    moveInput.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    moveInput.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    moveInput.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    moveInput.x -= 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                moveInput += gamepad.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(moveInput, 1f);
        }

        [ServerRpc]
        private void SubmitMoveInputServerRpc(Vector2 moveInput)
        {
            _serverMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void SimulateServerMovement()
        {
            if (_characterController == null || !_characterController.enabled)
                return;

            Vector3 move = new Vector3(_serverMoveInput.x, 0f, _serverMoveInput.y);
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeedDegrees * Time.deltaTime);
            }

            _characterController.SimpleMove(move * _moveSpeed);
        }

        private void RefreshCharacterControllerState()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();

            if (_characterController == null)
                return;

            bool shouldEnable = IsServerInitialized;
            if (_characterController.enabled != shouldEnable)
                _characterController.enabled = shouldEnable;
        }
    }
}
