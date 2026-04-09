using FishNet.Connection;
using FishNet.Object;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class BuddahMovement : NetworkBehaviour
{
    private enum RotationMode
    {
        TorqueSteering,
        DirectHeadingControl
    }

    private enum LaunchState
    {
        Normal,
        Inherit,
        Blend
    }

    [Header("Movement (Physics)")]
    [SerializeField] public float forwardForce = 20f;
    [SerializeField] public float turnTorque = 12f;
    [SerializeField] public float turnDecayPerSecond = 8f;
    [SerializeField] public float maxSpeed = 8f;
    [SerializeField] public float turnInputMultiplier = 1f;

    [Header("Direct Heading Control")]
    [SerializeField, Min(1f)] private float directHeadingDegreesPerSecond = 420f;
    [SerializeField, Min(0f)] private float directHeadingAngularDamping = 18f;
    [SerializeField, Min(0f)] private float directHeadingSnapAngle = 2.5f;

    [Header("Push Reaction")]
    [SerializeField] public float pushGraceSeconds = 0.25f;
    [SerializeField] public float pushExtraMaxSpeed = 6f;

    [Header("Launch Continuity")]
    [SerializeField, Min(0f)] private float launchInheritTime = 0.2f;
    [SerializeField, Min(0f)] private float launchBlendTime = 0.32f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    private InputSystem_Actions inputActions;
    private InputAction movementAction;
    private PlayerBuddahInputSource _playerInputSource;
    private readonly DisabledBuddahInputSource _disabledInputSource = new DisabledBuddahInputSource();
    private IBuddahInputSource _currentInputSource;

    private float _pushGraceTimer;
    private float _roomStateBypassTimer;
    private bool _inputInitialized;
    private int _skillRootCount;
    private RotationMode _rotationMode;
    private LaunchState _launchState;
    private float _launchStateTimer;
    private Vector3 _launchInheritedVelocity;
    private Vector3 _launchInheritedForward;
    private bool _externalKinematicControlActive;
    private bool _introControlActive;
    private float _suppressSteeringTimer;

    public bool IsSkillRooted => _skillRootCount > 0;
    public bool IsUsingAutoInput => _currentInputSource != null && _currentInputSource.AllowMovementWhenGameplayBlocked;
    public bool IsLaunchHandoffActive => _launchState != LaunchState.Normal || _externalKinematicControlActive || _introControlActive;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        movementAction = inputActions.Player.Movement;
        _playerInputSource = new PlayerBuddahInputSource(movementAction);
        _currentInputSource = _playerInputSource;
        _inputInitialized = true;

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshLocalControlState();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        SetInputEnabled(false);
    }

    private void OnDisable()
    {
        SetInputEnabled(false);
    }

    private void Update()
    {
        if (IsOwner)
            RefreshLocalControlState();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || rb == null)
            return;

        if (_externalKinematicControlActive)
            return;

        IBuddahInputSource inputSource = _currentInputSource ?? _disabledInputSource;
        inputSource.Tick(Time.fixedDeltaTime, transform, rb);
        _rotationMode = inputSource.UseDirectHeadingControl ? RotationMode.DirectHeadingControl : RotationMode.TorqueSteering;

        if (IsRaceGameplayBlocked() && _roomStateBypassTimer <= 0f && !inputSource.AllowMovementWhenGameplayBlocked)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        float throttle = Mathf.Clamp01(inputSource.GetThrottle());
        float steering = Mathf.Clamp(inputSource.GetSteering(), -1f, 1f) * turnInputMultiplier;
        if (_suppressSteeringTimer > 0f)
            steering = 0f;
        float launchBlend01 = GetLaunchBlend01();
        bool isLaunchInherit = _launchState == LaunchState.Inherit;

        if (isLaunchInherit)
        {
            throttle = 0f;
            steering = 0f;
        }
        else if (_launchState == LaunchState.Blend)
        {
            throttle *= launchBlend01;
            steering *= launchBlend01;
        }

        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.0001f)
            forwardDir = Vector3.forward;
        forwardDir.Normalize();

        if (_rotationMode == RotationMode.DirectHeadingControl)
            ApplyDirectHeadingControl(inputSource.GetDesiredForward(), Time.fixedDeltaTime);

        forwardDir = transform.forward;
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.0001f)
            forwardDir = Vector3.forward;
        forwardDir.Normalize();

        rb.AddForce(forwardDir * (forwardForce * throttle), ForceMode.Force);

        if (_rotationMode == RotationMode.TorqueSteering && Mathf.Abs(steering) > 0.001f)
        {
            rb.AddTorque(Vector3.up * steering * turnTorque, ForceMode.Force);
        }
        else if (_rotationMode == RotationMode.TorqueSteering && turnDecayPerSecond > 0f)
        {
            Vector3 angular = rb.angularVelocity;
            angular.y = Mathf.MoveTowards(angular.y, 0f, turnDecayPerSecond * Time.fixedDeltaTime);
            rb.angularVelocity = angular;
        }

        Vector3 velocity = rb.velocity;
        Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
        float allowedMax = maxSpeed + (_pushGraceTimer > 0f ? pushExtraMaxSpeed : 0f);
        if (planar.magnitude > allowedMax)
        {
            Vector3 clamped = planar.normalized * allowedMax;
            rb.velocity = new Vector3(clamped.x, velocity.y, clamped.z);
        }

        if (_pushGraceTimer > 0f)
            _pushGraceTimer -= Time.fixedDeltaTime;

        if (_roomStateBypassTimer > 0f)
            _roomStateBypassTimer -= Time.fixedDeltaTime;

        if (_suppressSteeringTimer > 0f)
            _suppressSteeringTimer -= Time.fixedDeltaTime;

        UpdateLaunchState(Time.fixedDeltaTime);
    }

    public void SetInputSource(IBuddahInputSource inputSource)
    {
        IBuddahInputSource next = inputSource ?? _disabledInputSource;
        if (ReferenceEquals(_currentInputSource, next))
            return;

        _currentInputSource?.OnDeactivated(transform, rb);
        _currentInputSource = next;
        _currentInputSource?.OnActivated(transform, rb);
        _rotationMode = _currentInputSource != null && _currentInputSource.UseDirectHeadingControl
            ? RotationMode.DirectHeadingControl
            : RotationMode.TorqueSteering;
    }

    public void RestorePlayerInputSource()
    {
        RefreshInputSourceForCurrentControlState();
    }

    public void DisableAllInput()
    {
        SetInputSource(_disabledInputSource);
    }

    public void SetIntroControlActive(bool active)
    {
        _introControlActive = active;
        RefreshInputSourceForCurrentControlState();
    }

    public void BeginLaunchHandoff(LaunchHandoffSnapshot snapshot, float bypassRoomStateSeconds, float suppressTurnInputSeconds = 0.15f, bool clearAngularVelocity = true, int debugSequenceId = 0, bool enableDebugLogs = false)
    {
        _externalKinematicControlActive = false;
        _introControlActive = false;
        _roomStateBypassTimer = Mathf.Max(_roomStateBypassTimer, bypassRoomStateSeconds);
        _suppressSteeringTimer = Mathf.Max(0f, suppressTurnInputSeconds);

        _launchInheritedVelocity = snapshot.Velocity;
        _launchInheritedForward = snapshot.Forward;
        _launchInheritedForward.y = 0f;
        if (_launchInheritedForward.sqrMagnitude < 0.0001f)
            _launchInheritedForward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
        _launchInheritedForward.Normalize();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.position = snapshot.Position;
            rb.rotation = snapshot.Rotation;
            rb.velocity = snapshot.Velocity;
            rb.angularVelocity = clearAngularVelocity ? Vector3.zero : snapshot.AngularVelocity;
        }

        if (launchInheritTime > 0f)
        {
            _launchState = LaunchState.Inherit;
            _launchStateTimer = launchInheritTime;
        }
        else if (launchBlendTime > 0f)
        {
            _launchState = LaunchState.Blend;
            _launchStateTimer = launchBlendTime;
        }
        else
        {
            _launchState = LaunchState.Normal;
            _launchStateTimer = 0f;
        }

        RefreshInputSourceForCurrentControlState();
    }

    public void SetExternalKinematicControlActive(bool active)
    {
        _externalKinematicControlActive = active;
        if (rb != null && IsOwner)
        {
            if (active)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = active;
        }

        if (active)
        {
            _launchState = LaunchState.Normal;
            _launchStateTimer = 0f;
        }

        RefreshInputSourceForCurrentControlState();
    }

    public void TeleportPredictedMotor(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
    {
        if (rb == null || !IsOwner)
            return;

        rb.position = position;
        rb.rotation = rotation;
        rb.velocity = velocity;
        rb.angularVelocity = angularVelocity;
        rb.Sleep();
        rb.WakeUp();
    }

    public void SetSkillRooted(bool rooted)
    {
        if (rooted)
        {
            _skillRootCount++;
            return;
        }

        _skillRootCount = Mathf.Max(0, _skillRootCount - 1);
    }

    [TargetRpc]
    public void ApplyPushImpulseTargetRpc(NetworkConnection conn, Vector3 impulse)
    {
        ApplyPushAndTorqueLocal(impulse, 0f);
    }

    [TargetRpc]
    public void ApplyPushImpulseAndTorqueTargetRpc(NetworkConnection conn, Vector3 impulse, float hitTurnTorqueImpulse)
    {
        ApplyPushAndTorqueLocal(impulse, hitTurnTorqueImpulse);
    }

    private void ApplyPushAndTorqueLocal(Vector3 impulse, float hitTurnTorqueImpulse)
    {
        if (!IsOwner || rb == null)
            return;

        _pushGraceTimer = pushGraceSeconds;
        rb.WakeUp();
        rb.AddForce(impulse, ForceMode.Impulse);
        if (Mathf.Abs(hitTurnTorqueImpulse) > 0.001f)
            rb.AddTorque(Vector3.up * hitTurnTorqueImpulse, ForceMode.Impulse);
    }

    private void RefreshLocalControlState()
    {
        bool allowLocalControl = IsOwner;
        if (rb != null)
        {
            bool useKinematic = !allowLocalControl || _externalKinematicControlActive;
            rb.isKinematic = useKinematic;
            rb.collisionDetectionMode = !useKinematic
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;
        }

        SetInputEnabled(IsOwner && !_externalKinematicControlActive);
        RefreshInputSourceForCurrentControlState();
    }

    private bool IsRaceGameplayBlocked()
    {
        return !ResultAreaInteractionGate.ShouldAllowMovementInput(gameObject);
    }

    private void SetInputEnabled(bool enabled)
    {
        if (!_inputInitialized || inputActions == null)
            return;

        if (enabled)
            inputActions.Enable();
        else
            inputActions.Disable();
    }

    private void ApplyDirectHeadingControl(Vector3 desiredForward, float deltaTime)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude < 0.0001f)
            return;

        desiredForward.Normalize();

        Vector3 currentForward = transform.forward;
        currentForward.y = 0f;
        if (currentForward.sqrMagnitude < 0.0001f)
            currentForward = desiredForward;
        currentForward.Normalize();

        float angleToTarget = Vector3.Angle(currentForward, desiredForward);
        Quaternion currentRotation = rb.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(desiredForward, Vector3.up);

        if (angleToTarget <= directHeadingSnapAngle)
        {
            rb.MoveRotation(targetRotation);
        }
        else
        {
            Quaternion nextRotation = Quaternion.RotateTowards(currentRotation, targetRotation, directHeadingDegreesPerSecond * deltaTime);
            rb.MoveRotation(nextRotation);
        }

        Vector3 angular = rb.angularVelocity;
        angular.x = 0f;
        angular.z = 0f;
        angular.y = Mathf.MoveTowards(angular.y, 0f, directHeadingAngularDamping * deltaTime);
        rb.angularVelocity = angular;
    }

    private float GetLaunchBlend01()
    {
        if (_launchState != LaunchState.Blend || launchBlendTime <= 0.0001f)
            return 1f;

        return 1f - Mathf.Clamp01(_launchStateTimer / launchBlendTime);
    }

    private void UpdateLaunchState(float deltaTime)
    {
        if (_launchState == LaunchState.Normal)
            return;

        if (_launchState == LaunchState.Inherit)
        {
            ApplyLaunchInheritedVelocity(0f);
            _launchStateTimer -= deltaTime;
            if (_launchStateTimer <= 0f)
            {
                if (launchBlendTime > 0f)
                {
                    _launchState = LaunchState.Blend;
                    _launchStateTimer = launchBlendTime;
                }
                else
                {
                    _launchState = LaunchState.Normal;
                    _launchStateTimer = 0f;
                }
            }

            return;
        }

        if (_launchState == LaunchState.Blend)
        {
            float blend01 = GetLaunchBlend01();
            ApplyLaunchInheritedVelocity(blend01);
            _launchStateTimer -= deltaTime;
            if (_launchStateTimer <= 0f)
            {
                _launchState = LaunchState.Normal;
                _launchStateTimer = 0f;
            }
        }
    }

    private void ApplyLaunchInheritedVelocity(float blend01)
    {
        if (rb == null)
            return;

        Vector3 currentVelocity = rb.velocity;
        Vector3 inheritedPlanar = new Vector3(_launchInheritedVelocity.x, 0f, _launchInheritedVelocity.z);
        Vector3 currentPlanar = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        Vector3 planar = Vector3.Lerp(inheritedPlanar, currentPlanar, Mathf.Clamp01(blend01));
        rb.velocity = new Vector3(planar.x, currentVelocity.y, planar.z);
        rb.angularVelocity = Vector3.zero;
    }

    private void RefreshInputSourceForCurrentControlState()
    {
        IBuddahInputSource desiredSource = (_externalKinematicControlActive || _introControlActive)
            ? _disabledInputSource
            : _playerInputSource;

        SetInputSource(desiredSource);
    }
}
