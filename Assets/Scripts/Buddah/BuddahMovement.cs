using FishNet.Connection;
using FishNet.Object;
using NewBuddah.PredictionV2.Integration;
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
    // Phase 6: Legacy mode retains pre-Phase-6 velocity-inherit. PredictionV2
    // race-start uses stop-then-countdown via prediction motor (motor.EnterRaceStartLock
    // driven by RoomStateManager._raceStartTick SyncVar). launchInheritTime is now
    // unreferenced (BeginLaunchHandoff retired in Area 2); launchBlendTime still
    // used by legacy _launchState Blend path (UpdateLaunchState ~line 475).
    [SerializeField, Min(0f)] private float launchInheritTime = 0.2f;
    [SerializeField, Min(0f)] private float launchBlendTime = 0.32f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private BuddahPredictionHandoffBridge predictionHandoffBridge;

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
    private bool _authoritativeHandoffPending;
    private bool _predictionLaunchHandoffActive;
    private float _suppressSteeringTimer;
    private bool _lastInputGateEnabled;
    private string _lastInputGateReason = string.Empty;

    public bool IsSkillRooted => _skillRootCount > 0;
    public bool IsUsingAutoInput => _currentInputSource != null && _currentInputSource.AllowMovementWhenGameplayBlocked;
    public bool IsLaunchHandoffActive => TryGetPredictionHandoffBridge(out BuddahPredictionHandoffBridge bridge) && bridge.IsPredictionHandoffActive()
        ? bridge.IsLaunchHandoffActive()
        : _launchState != LaunchState.Normal || _externalKinematicControlActive || _introControlActive || _authoritativeHandoffPending || _predictionLaunchHandoffActive;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        movementAction = inputActions.Player.Movement;
        _playerInputSource = new PlayerBuddahInputSource(movementAction);
        _currentInputSource = _playerInputSource;
        _inputInitialized = true;

        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (predictionHandoffBridge == null)
            predictionHandoffBridge = GetComponent<BuddahPredictionHandoffBridge>();
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
        bool shouldEnableOwnerInput = ShouldEnableOwnerInputNow(out _);
        RefreshInputSourceForCurrentControlState(shouldEnableOwnerInput);
    }

    public void DisableAllInput()
    {
        SetInputSource(_disabledInputSource);
    }

    public void SetIntroControlActive(bool active)
    {
        if (active)
            _authoritativeHandoffPending = false;

        if (TryGetPredictionHandoffBridge(out BuddahPredictionHandoffBridge bridge) && bridge.TrySetIntroControlActive(active))
        {
            _introControlActive = active;
            Debug.Log($"[IntroState][Movement:{name}] Intro control mirrored to prediction active={active} owner={IsOwner}");
            RefreshLocalControlState();
            return;
        }

        _introControlActive = active;
        Debug.Log($"[IntroState][Movement:{name}] Intro control active={active} owner={IsOwner}");
        RefreshLocalControlState();
    }

    // Phase 6 — BuddahMovement.BeginLaunchHandoff retired with the owner-side
    // RPC chain (Section 11.1 option a). Race-start lock is now driven by
    // server-side SyncVar (RoomStateManager._raceStartTick); spline-side driver
    // no longer initiates the handoff. Legacy non-prediction (_launchState
    // Inherit / Blend / ApplyLaunchInheritedVelocity) path retained — Phase 6
    // applies to PredictionV2 mode only.

    public void SetExternalKinematicControlActive(bool active)
    {
        if (active)
            _authoritativeHandoffPending = false;

        if (TryGetPredictionHandoffBridge(out BuddahPredictionHandoffBridge bridge) && bridge.TrySetExternalKinematicControlActive(active))
        {
            _externalKinematicControlActive = active;
            if (active)
            {
                _launchState = LaunchState.Normal;
                _launchStateTimer = 0f;
            }
            Debug.Log($"[IntroState][Movement:{name}] External kinematic mirrored to prediction active={active} owner={IsOwner}");
            RefreshLocalControlState();
            return;
        }

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

        Debug.Log($"[IntroState][Movement:{name}] External kinematic active={active} owner={IsOwner}");
        RefreshLocalControlState();
    }

    private bool TryGetPredictionHandoffBridge(out BuddahPredictionHandoffBridge bridge)
    {
        if (predictionHandoffBridge == null)
            predictionHandoffBridge = GetComponent<BuddahPredictionHandoffBridge>();
        if (predictionHandoffBridge == null)
        {
            NewBuddah.PredictionV2.Bootstrap.BuddahPredictionBootstrap predictionBootstrap =
                GetComponent<NewBuddah.PredictionV2.Bootstrap.BuddahPredictionBootstrap>();
            if (predictionBootstrap != null && predictionBootstrap.IsPredictionModeActive())
                predictionHandoffBridge = gameObject.AddComponent<BuddahPredictionHandoffBridge>();
        }

        bridge = predictionHandoffBridge;
        return bridge != null;
    }

    private void SyncPredictionHandoffStateFromBridge()
    {
        if (!TryGetPredictionHandoffBridge(out BuddahPredictionHandoffBridge bridge) || !bridge.IsPredictionHandoffActive())
            return;

        if (!bridge.TryGetPredictionControlState(out BuddahPredictionHandoffBridge.PredictionHandoffControlState predictionState))
            return;

        bool introActive = predictionState.IntroControlActive;
        bool externalActive = predictionState.ExternalKinematicControlActive;
        bool handoffPending = predictionState.AuthoritativeLaunchHandoffPending;
        bool handoffConsumedOrActive = predictionState.LaunchHandoffConsumedOrActive;

        bool hadPending = _authoritativeHandoffPending;
        bool changed = _introControlActive != introActive
                       || _externalKinematicControlActive != externalActive
                       || _authoritativeHandoffPending != handoffPending
                       || _predictionLaunchHandoffActive != handoffConsumedOrActive;

        _introControlActive = introActive;
        _externalKinematicControlActive = externalActive;
        _authoritativeHandoffPending = handoffPending;
        _predictionLaunchHandoffActive = handoffConsumedOrActive;

        if (hadPending && !_authoritativeHandoffPending && _predictionLaunchHandoffActive)
        {
            Debug.Log($"[IntroHandoff][Movement:{name}] Pending state cleared because prediction consumed/active owner={IsOwner}");
        }

        if (changed)
        {
            Debug.Log(
                $"[IntroHandoff][Movement:{name}] Synced prediction state owner={IsOwner} intro={_introControlActive} " +
                $"external={_externalKinematicControlActive} handoffPending={_authoritativeHandoffPending} active={_predictionLaunchHandoffActive}");
        }
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
        SyncPredictionHandoffStateFromBridge();
        bool shouldEnableOwnerInput = ShouldEnableOwnerInputNow(out string inputGateReason);
        bool allowLocalControl = IsOwner;
        if (rb != null)
        {
            bool useKinematic = !allowLocalControl || _externalKinematicControlActive;
            if (rb.isKinematic != useKinematic)
                rb.isKinematic = useKinematic;

            CollisionDetectionMode targetCollisionMode = !useKinematic
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;
            if (rb.collisionDetectionMode != targetCollisionMode)
                rb.collisionDetectionMode = targetCollisionMode;
        }

        SetInputEnabled(shouldEnableOwnerInput);
        RefreshInputSourceForCurrentControlState(shouldEnableOwnerInput);
        LogInputGateState(shouldEnableOwnerInput, inputGateReason);
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

    private bool ShouldEnableOwnerInputNow(out string reason)
    {
        if (!IsOwner)
        {
            reason = "not-owner";
            return false;
        }

        if (_authoritativeHandoffPending)
        {
            reason = "authoritative-handoff-pending";
            return false;
        }

        if (_externalKinematicControlActive)
        {
            reason = "external-kinematic-control";
            return false;
        }

        if (_introControlActive)
        {
            reason = "intro-control";
            return false;
        }

        RoomStateManager room = RoomStateManager.Instance;
        if (room == null)
        {
            reason = "room-state-unavailable";
            return true;
        }

        bool roomAllowsOwnerInput = room.ShouldEnableOwnerMovementInputNow();
        reason = room.GetOwnerMovementInputGateReason();
        if (!roomAllowsOwnerInput)
            return false;

        return true;
    }

    private void LogInputGateState(bool enabled, string reason)
    {
        if (_lastInputGateEnabled == enabled && string.Equals(_lastInputGateReason, reason, System.StringComparison.Ordinal))
            return;

        _lastInputGateEnabled = enabled;
        _lastInputGateReason = reason ?? string.Empty;

        RoomStateManager room = RoomStateManager.Instance;
        string phase = room != null ? room.CurrentMatchSessionPhase.ToString() : "none";
        bool movementUnlocked = room != null && room.IsGameplayMovementUnlocked;
        bool matchPhase = room != null && room.IsMatchPhaseActive;
        bool resultPhase = room != null && room.IsResultPhaseActive;
        Debug.Log(
            $"[InputGate][Movement:{name}] enabled={enabled} reason={reason} owner={IsOwner} " +
            $"intro={_introControlActive} external={_externalKinematicControlActive} phase={phase} " +
            $"handoffPending={_authoritativeHandoffPending} match={matchPhase} result={resultPhase} movementUnlocked={movementUnlocked}");
    }

    private void RefreshInputSourceForCurrentControlState(bool shouldEnableOwnerInput)
    {
        IBuddahInputSource desiredSource = shouldEnableOwnerInput ? _playerInputSource : _disabledInputSource;

        SetInputSource(desiredSource);
    }
}
