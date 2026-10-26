using FishNet.Object;
using BuddahGo.Match;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class BuddahHandControl : NetworkBehaviour, IHandsImpulseScale
{
    [Header("Input")]
    [Tooltip("Read from InputSystem_Actions -> Player -> HandPush (mouse left / right button; W / UpArrow deprecated) and HandRotation (LeftArrow / RightArrow, fallback when there is no mouse).")]
    [SerializeField] private bool useGeneratedInputActions = true;

    [Header("Mouse Aim")]
    [Tooltip("Owner aims the hands with the cursor projected onto the ground plane. Off, or without a mouse, LeftArrow / RightArrow turn the hands.")]
    [SerializeField] private bool useMouseAim = true;

    [Tooltip("Time constant (seconds) of the exponential smoothing applied to the cursor target before the rotationSpeedDegPerSec cap. 0 = no smoothing.")]
    [SerializeField, Min(0f)] private float mouseAimSmoothingSeconds = 0.06f;

    [Tooltip("Cursor closer than this to the body (metres, multiplied by the body scale) holds the current aim target.")]
    [SerializeField, Min(0f)] private float mouseAimDeadRadius = 1.5f;

    [Tooltip("Height of the horizontal aim plane above the movement root (metres).")]
    [SerializeField] private float mouseAimPlaneHeightOffset = 0f;

    [Header("Freeze")]
    [Tooltip("After a freeze (禁锢) ends, the owner re-sends its hand yaw once after this delay: the server drops yaw updates while it still sees the racer frozen, so a turn made in that tail would otherwise stay unsynced.")]
    [SerializeField, Min(0f)] private float yawResendAfterFreezeSeconds = 0.3f;

    [Header("Hand Bones")]
    [Tooltip("Optional: if empty, will try to auto-find via Animator Humanoid LeftHand/RightHand.")]
    [SerializeField] private Transform leftHandBone;
    [Tooltip("Optional: if empty, will try to auto-find via Animator Humanoid LeftHand/RightHand.")]
    [SerializeField] private Transform rightHandBone;

    [Header("Rotation")]
    [Tooltip("Hand turn speed cap in degrees per second (mouse aim and the arrow-key fallback).")]
    [SerializeField] private float rotationSpeedDegPerSec = 180f;

    [Header("Hand Animation")]
    [Tooltip("Animator trigger fired on a left-hand push (mouse left button; W deprecated).")]
    [SerializeField] private string leftHandTriggerName = "LeftW";

    [Tooltip("Animator trigger fired on a right-hand push (mouse right button; UpArrow deprecated).")]
    [SerializeField] private string rightHandTriggerName = "RightUp";

    [Tooltip("If true, will replicate left/right hand trigger events to other clients.")]
    [SerializeField] private bool replicateToOthers = true;

    [Header("Push Hitbox")]
    [Tooltip("Prefab with: Trigger Collider + kinematic Rigidbody + PushHitbox script. Spawned ONLY on server for hit detection.")]
    [SerializeField] private PushHitbox pushHitboxPrefab;

    [Tooltip("Delay (seconds) between input/animation trigger and the actual hitbox becoming active on the server.")]
    [SerializeField] private float pushWindupSeconds = 0.10f;

    [Tooltip("How long (seconds) the push hitbox exists on the server.")]
    [SerializeField] private float pushLifetimeSeconds = 0.12f;

    [Tooltip("Forward offset from the character center to spawn the hitbox.")]
    [SerializeField] private float pushForwardOffset = 0.9f;

    [Tooltip("Side offset (left is -X, right is +X) to spawn the hitbox, making left/right hands feel distinct.")]
    [SerializeField] private float pushSideOffset = 0.25f;

    [Tooltip("Extra side bias for left-hand hitbox spawn (added to side offset).")]
    [SerializeField] private float pushLeftSideBias = -0.05f;

    [Tooltip("Extra side bias for right-hand hitbox spawn (added to side offset).")]
    [SerializeField] private float pushRightSideBias = 0.05f;

    [Tooltip("Vertical offset from character position to spawn the hitbox (approx chest/arm height).")]
    [SerializeField] private float pushHeightOffset = 1.0f;

    [Tooltip("Impulse strength applied to the victim (Impulse mode; mass matters).")]
    [SerializeField] private float pushImpulseStrength = 6.0f;

    [Tooltip("Server-side cooldown between pushes (seconds).")]
    [SerializeField] private float pushCooldownSeconds = 0.25f;

    [Header("Projectile Push Skill")]
    [Tooltip("Optional authored projectile prefab. If assigned, its collider is used for server hit detection and the same prefab is shown on clients.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Rotation offset applied to the projectile visual so imported hand models face the projectile travel direction.")]
    [SerializeField] private Vector3 projectileVisualLocalEuler = Vector3.zero;

    [Tooltip("Scale multiplier applied to the projectile visual on every client.")]
    [SerializeField] private Vector3 projectileVisualScale = Vector3.one;

    private float _nextPushServerTimeLeft;
    private float _nextPushServerTimeRight;
    private float _nextPushLocalTimeLeft;
    private float _nextPushLocalTimeRight;
    private float _projectilePushModeUntilServer;
    private bool _projectileUseChargedRuntimeServer;
    private float _projectileBuildUpSecondsServer;
    private float _projectileSpeedServer;
    private float _projectileLifetimeServer;
    private float _projectileImpulseStrengthServer;
    private Vector3 _projectileColliderSizeServer = new Vector3(1.25f, 1.1f, 1.8f);
    private float _projectileForwardOffsetServer;
    private float _projectileHeightOffsetServer;
    private bool _projectileIgnoreSolidWorldServer;
    private GameObject _projectileChargedVisualPrefabServer;
    private string _projectileChargedProgressPropertyServer = "Progress";
    private float _projectileChargedPushCooldownServer;
    private float _projectileDelayedPushSecondsServer;
    private uint _inputGeneration;

    public float ServerHandYaw => _syncedYawOffsetDeg.Value;
    public float HandRotationSpeed => rotationSpeedDegPerSec;
    public float ServerPushCooldown => GetActivePushCooldownServer(IsProjectilePushModeActiveServer(), _projectileUseChargedRuntimeServer);
    public float ProjectileBuffSecondsLeft => IsServerInitialized ? Mathf.Max(0f, _projectilePushModeUntilServer - Time.time) : 0f;

    /// <summary>Skill/Push "basic" impulseStrength (table value in play): the base of 缘起 impulse multipliers (#4's M).</summary>
    public float BasicPushImpulseStrength => pushImpulseStrength;

    /// <summary>
    /// Server, #1 金身巨掌 (IHandsImpulseScale): while true, the charged-projectile impulse is multiplied by the same
    /// body-scale sample that already sizes the projectile. Cleared by ResetSkillInputAndEffects.
    /// </summary>
    public bool ImpulseFollowsScale { get; set; }

    public bool CanPushServer(bool left) => IsServerInitialized && !IsMotorFrozen() && Time.time >= (left ? _nextPushServerTimeLeft : _nextPushServerTimeRight);

    public void InjectServerRotation(int axis, float tickDelta)
    {
        if (!RacerAuthority.IsServerAI(this) || !ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject)) return;
        // Frozen (禁锢): the hands stop turning, so the AI's rotation input is dropped.
        if (IsMotorFrozen())
        {
            HandRotationAxis = 0f;
            return;
        }
        HandRotationAxis = Mathf.Clamp(axis, -1, 1);
        _syncedYawOffsetDeg.Value = Mathf.DeltaAngle(0f, _syncedYawOffsetDeg.Value + HandRotationAxis * rotationSpeedDegPerSec * tickDelta);
    }

    public bool InjectServerPush(bool left)
    {
        if (!RacerAuthority.IsServerAI(this)) return false;
        return TryPushServer(left, ServerHandYaw, GetHandWorldPositionSnapshot(left), true);
    }

    public void ResetSkillInputAndEffects()
    {
        _inputGeneration++;
        if (IsServerInitialized) ClearProjectilePushModeServer();
        ClearProjectilePushModeLocal();
        HandRotationAxis = 0f;
        ImpulseFollowsScale = false;
    }

    /// <summary>
    /// Server: 缘起 consumed the charged-push buff. Only the buff ends; pushes already in windup keep their snapshot
    /// (no input generation bump), the impulse rewrite belongs to the 缘起 that set it.
    /// </summary>
    public void EndProjectilePushModeServer()
    {
        if (!IsServerInitialized)
            return;
        ClearProjectilePushModeServer();
        GameLog.Verbose("[HandControl] Projectile push mode ended early on server (consumed).");
    }

    /// <summary>Every peer: drops the local charged-push mode (presentation side of a consumed buff).</summary>
    public void EndProjectilePushModeLocal()
    {
        ClearProjectilePushModeLocal();
    }
    public void CancelPendingServerInput() { if (IsServerInitialized) _inputGeneration++; }

    private float _projectilePushModeUntilLocal;
    private bool _projectileUseChargedRuntimeLocal;
    private GameObject _projectileChargedVisualPrefabLocal;
    private string _projectileChargedProgressPropertyLocal = "Progress";
    private GameObject _projectileChargedLaunchEffectPrefabLocal;
    private float _projectileForwardOffsetLocal;
    private float _projectileHeightOffsetLocal;
    private float _projectileDelayedPushSecondsLocal;
    private float _projectileChargedPushCooldownLocal;

    private const string DefaultChargedProjectileProgressProperty = "Progress";

    private InputAction handPushAction;


    private InputSystem_Actions inputActions;
    private InputAction handRotationAction;
    private Animator animator;
    private NewBuddah.PredictionV2.Core.BuddahPredictedMotor _motor;
    private PlayerScaleEffect _scaleEffect;

    // Mouse aim (owner only).
    private Camera _aimCamera;
    private bool _hasMouseAimTarget;
    private float _smoothedMouseAimYawDeg;
    private static readonly List<RaycastResult> s_UiRaycastResults = new();

    // Owner: the cursor's aim-plane point, sampled once per frame (TryGetAimGroundPoint).
    private int _aimGroundPointFrame = -1;
    private bool _hasAimGroundPoint;
    private Vector3 _aimGroundPoint;

    // Owner: freeze edge for the post-freeze yaw resend (0 = none scheduled).
    private bool _wasFrozenLocal;
    private float _yawResendAt;

    private const float HandRotationAxisEpsilonDeg = 0.001f;

    // Owner-authoritative, body-relative and UNWRAPPED: observers Lerp the synced copy, so wrapping would spin them.
    private float handYawOffsetDeg;
    private float _lastSentYaw;
    private float _nextSendTime;
    private readonly FloatSyncVar _syncedYawOffsetDeg = new();
    private bool hasCachedBaseRotations;
    private Quaternion leftHandBaseLocalRotation;
    private Quaternion rightHandBaseLocalRotation;
    private Quaternion leftHandBaseWorldRotation;
    private Quaternion rightHandBaseWorldRotation;

    /// <summary>
    /// Current hand rotation input axis.
    /// -1 = turning left (LeftArrow), +1 = turning right (RightArrow), 0 = none. With mouse aim it is the sign of this frame's step.
    /// </summary>
    public float HandRotationAxis { get; private set; }

    /// <summary>Skill/Push "basic" row → BuddahHandControl fields ("recordField=componentField").</summary>
    public static readonly string[] PushBasicMap =
    {
        "rotationSpeedDegPerSec", "windupSeconds=pushWindupSeconds", "pushCooldownSeconds", "lifetimeSeconds=pushLifetimeSeconds", "impulseStrength=pushImpulseStrength",
        "forwardOffset=pushForwardOffset", "heightOffset=pushHeightOffset", "sideOffset=pushSideOffset", "leftSideBias=pushLeftSideBias", "rightSideBias=pushRightSideBias"
    };

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        _motor = GetComponent<NewBuddah.PredictionV2.Core.BuddahPredictedMotor>();
        // Skill/Push "basic" row owns the hand-push numbers; prefab values stay as fallback. Explicit mapping only:
        // the charged row of the same sheet belongs to the projectile skill (SkillDatabase).
        ProjectConfigDatabase db = DesignTableOverlay.Db;
        if (db != null)
            DesignTableOverlay.ApplyMapped(this, DesignTableOverlay.Single(db.pushes, r => r.pushId == "basic"), PushBasicMap);

        if (useGeneratedInputActions)
        {
            inputActions = new InputSystem_Actions();
            // HandRotation (←/→) is only the fallback when there is no mouse; the mouse aims the hands.
            handRotationAction = inputActions.Player.HandRotation;
            // HandPush bindings: <Mouse>/leftButton = left hand, <Mouse>/rightButton = right hand (primary keys).
            // The action is PassThrough so a press of one button is never swallowed while the other is held.
            // 废弃内容（deprecated）: W/↑ 推掌绑定暂时保留，后续可能再调整；主键位是鼠标左/右键。见 Docs/gameplay/README.md §3
            handPushAction = inputActions.Player.HandPush;
        }

        TryAutoAssignHandBones();
        CacheBaseRotationsIfNeeded(force: true);
    }

    private void OnEnable()
    {
        GameLog.Verbose($"[HandControl] OnEnable. IsOwner={IsOwner}, netObj={gameObject.name}");
        CacheBaseRotationsIfNeeded(force: false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        inputActions?.Enable();
        GameLog.Verbose("[HandControl] inputActions Enabled (owner).");

        if (handPushAction != null)
        {
            handPushAction.performed += OnHandPushPerformed;
            GameLog.Verbose("[HandControl] Subscribed handPushAction.performed.");
        }
        else
        {
            Debug.LogWarning("[HandControl] handPushAction is NULL!");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        DisableInput();
    }

    private void OnDisable()
    {
        // Covers despawn / scene unload in-editor.
        DisableInput();
    }

    private void DisableInput()
    {
        if (handPushAction != null)
            handPushAction.performed -= OnHandPushPerformed;

        inputActions?.Disable();
    }


    private void Update()
    {
        RefreshProjectilePushModeExpiration();

        if (!IsOwner)
            return;

        if (IsRaceGameplayBlocked())
            return;

        UpdateHandAimLocal(Time.deltaTime);
        TrySendYawToServer();
    }

    private void UpdateHandAimLocal(float deltaTime)
    {
        // Frozen (禁锢): the hands stop turning; the aim input is ignored (the server drops yaw updates too).
        bool frozen = IsMotorFrozen();
        TrackLocalFreezeForYawResend(frozen);
        if (frozen)
        {
            HandRotationAxis = 0f;
            // Re-seed the smoothing from the held yaw when the freeze ends, instead of jumping to a stale target.
            _hasMouseAimTarget = false;
            return;
        }

        // A local modal (quit dialog) owns the mouse while it is open, as it owns steering.
        if (LocalInputBlock.IsBlocked)
        {
            HandRotationAxis = 0f;
            return;
        }

        if (TryGetMouseAimTarget(deltaTime, out float targetYawDeg))
        {
            // Recomputed every frame, so the hands keep their world aim while A/D turns the body.
            float step = HandAimMath.RateLimitedStep(handYawOffsetDeg, targetYawDeg, rotationSpeedDegPerSec, deltaTime);
            handYawOffsetDeg += step;
            HandRotationAxis = step > HandRotationAxisEpsilonDeg ? 1f : step < -HandRotationAxisEpsilonDeg ? -1f : 0f;
            return;
        }

        // Fallback without a mouse (or when the cursor ray misses the aim plane): LeftArrow / RightArrow.
        _hasMouseAimTarget = false;
        HandRotationAxis = ReadHandRotationAxis();
        if (Mathf.Abs(HandRotationAxis) > 0.001f)
        {
            handYawOffsetDeg += HandRotationAxis * rotationSpeedDegPerSec * deltaTime;
        }
    }

    /// <summary>
    /// Owner: the smoothed, body-relative cursor target in degrees. False when there is no mouse or camera,
    /// or the cursor ray misses the aim plane. Inside the dead radius the current target is held.
    /// </summary>
    private bool TryGetMouseAimTarget(float deltaTime, out float targetYawDeg)
    {
        targetYawDeg = handYawOffsetDeg;

        if (!TryGetCursorRay(out Ray cursorRay))
            return false;

        // transform is the movement root: the server rebuilds the push as AngleAxis(yaw, up) * transform.forward,
        // so the offset is measured from this forward, not from the smoothed visual root.
        Vector3 bodyPosition = transform.position;
        float scaleMultiplier = GetPushScaleMultiplier();
        HandAimSampleKind sample = HandAimMath.TryGetTargetYawOffset(
            cursorRay,
            GetAimPlaneHeight(),
            bodyPosition,
            transform.forward,
            mouseAimDeadRadius * scaleMultiplier,
            out float rawTargetYawDeg);

        if (sample == HandAimSampleKind.Miss)
            return false;

        if (!_hasMouseAimTarget)
        {
            _smoothedMouseAimYawDeg = handYawOffsetDeg;
            _hasMouseAimTarget = true;
        }

        if (sample == HandAimSampleKind.Aim)
            _smoothedMouseAimYawDeg = HandAimMath.SmoothAngle(_smoothedMouseAimYawDeg, rawTargetYawDeg, deltaTime, mouseAimSmoothingSeconds);

        targetYawDeg = _smoothedMouseAimYawDeg;
        return true;
    }

    /// <summary>
    /// Owner: the cursor's point on the horizontal aim plane this frame, i.e. the ground point the hands aim at (the
    /// dead radius does not apply). Presentation only; no reticle is drawn. False when this is not the owner, mouse aim
    /// is off or there is no mouse or camera, race gameplay or a local modal blocks the input, or the ray misses the
    /// plane this frame. Sampled once per frame, so any reader in any order gets the same point.
    /// </summary>
    public bool TryGetAimGroundPoint(out Vector3 point)
    {
        int frame = Time.frameCount;
        if (_aimGroundPointFrame != frame)
        {
            _aimGroundPointFrame = frame;
            _hasAimGroundPoint = TrySampleAimGroundPoint(out _aimGroundPoint);
        }
        point = _aimGroundPoint;
        return _hasAimGroundPoint;
    }

    private bool TrySampleAimGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (!IsOwner || IsRaceGameplayBlocked() || LocalInputBlock.IsBlocked)
            return false;
        if (!TryGetCursorRay(out Ray cursorRay))
            return false;

        var plane = new Plane(Vector3.up, new Vector3(0f, GetAimPlaneHeight(), 0f));
        if (!plane.Raycast(cursorRay, out float enter))
            return false;
        point = cursorRay.GetPoint(enter);
        return true;
    }

    // Owner mouse aim input: the camera ray through the cursor. False with mouse aim off, no mouse or no camera.
    private bool TryGetCursorRay(out Ray cursorRay)
    {
        cursorRay = default;
        Mouse mouse = Mouse.current;
        if (!useMouseAim || mouse == null)
            return false;

        if (_aimCamera == null || !_aimCamera.isActiveAndEnabled)
            _aimCamera = Camera.main;
        if (_aimCamera == null)
            return false;

        cursorRay = _aimCamera.ScreenPointToRay(mouse.position.ReadValue());
        return true;
    }

    private float GetAimPlaneHeight() => transform.position.y + mouseAimPlaneHeightOffset;

    // Owner: a freeze just ended locally → schedule one yaw resend (see yawResendAfterFreezeSeconds).
    private void TrackLocalFreezeForYawResend(bool frozen)
    {
        if (_wasFrozenLocal && !frozen)
            _yawResendAt = Time.time + yawResendAfterFreezeSeconds;
        _wasFrozenLocal = frozen;
    }

    private void RefreshProjectilePushModeExpiration()
    {
        if (IsServerInitialized
            && _projectilePushModeUntilServer > 0f
            && Time.time >= _projectilePushModeUntilServer)
        {
            ClearProjectilePushModeServer();
        }

        if (IsOwner
            && _projectilePushModeUntilLocal > 0f
            && Time.time >= _projectilePushModeUntilLocal)
        {
            ClearProjectilePushModeLocal();
        }
    }

    private void ClearProjectilePushModeServer()
    {
        _projectilePushModeUntilServer = 0f;
        _projectileUseChargedRuntimeServer = false;
        _projectileBuildUpSecondsServer = 0f;
        _projectileSpeedServer = 0f;
        _projectileLifetimeServer = 0f;
        _projectileImpulseStrengthServer = 0f;
        _projectileColliderSizeServer = new Vector3(1.25f, 1.1f, 1.8f);
        _projectileForwardOffsetServer = 0f;
        _projectileHeightOffsetServer = 0f;
        _projectileIgnoreSolidWorldServer = false;
        _projectileChargedVisualPrefabServer = null;
        _projectileChargedProgressPropertyServer = DefaultChargedProjectileProgressProperty;
        _projectileChargedPushCooldownServer = 0f;
        GameLog.Verbose("[HandControl] Projectile push mode expired on server. Reverting to normal push.");
    }

    private void ClearProjectilePushModeLocal()
    {
        _projectilePushModeUntilLocal = 0f;
        _projectileUseChargedRuntimeLocal = false;
        _projectileChargedVisualPrefabLocal = null;
        _projectileChargedProgressPropertyLocal = DefaultChargedProjectileProgressProperty;
        _projectileChargedLaunchEffectPrefabLocal = null;
        _projectileForwardOffsetLocal = 0f;
        _projectileHeightOffsetLocal = 0f;
        _projectileDelayedPushSecondsLocal = 0f;
        _projectileChargedPushCooldownLocal = 0f;
        GameLog.Verbose("[HandControl] Projectile push mode expired locally. Reverting to normal push.");
    }

    private void LateUpdate()
    {
        // Apply after Animator writes bone transforms each frame.
        if (IsOwner)
            ApplyHandRotation(handYawOffsetDeg);
        else
            ApplyHandRotation(_syncedYawOffsetDeg.InterpolatedValue());
    }

    private void TrySendYawToServer()
    {
        if (Time.time < _nextSendTime)
            return;

        bool resendDue = _yawResendAt > 0f && Time.time >= _yawResendAt;
        if (!resendDue && Mathf.Abs(handYawOffsetDeg - _lastSentYaw) < 0.1f)
            return;

        _yawResendAt = 0f;
        _lastSentYaw = handYawOffsetDeg;
        _nextSendTime = Time.time + 0.05f;
        SetHandYawServerRpc(handYawOffsetDeg);
    }

    [ServerRpc]
    private void SetHandYawServerRpc(float yawOffsetDeg)
    {
        // Frozen (禁锢): the hands stop turning on every peer, so the server keeps the synced yaw while frozen.
        if (IsMotorFrozen())
            return;
        _syncedYawOffsetDeg.Value = yawOffsetDeg;
    }

    private void TryAutoAssignHandBones()
    {
        if ((leftHandBone != null && rightHandBone != null) || animator == null)
        {
            return;
        }

        if (animator.isHuman)
        {
            if (leftHandBone == null)
            {
                leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }

            if (rightHandBone == null)
            {
                rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }
    }

    private void CacheBaseRotationsIfNeeded(bool force)
    {
        if (!force && hasCachedBaseRotations)
        {
            return;
        }

        if (leftHandBone != null)
        {
            leftHandBaseLocalRotation = leftHandBone.localRotation;
            leftHandBaseWorldRotation = leftHandBone.rotation;
        }

        if (rightHandBone != null)
        {
            rightHandBaseLocalRotation = rightHandBone.localRotation;
            rightHandBaseWorldRotation = rightHandBone.rotation;
        }

        hasCachedBaseRotations = leftHandBone != null || rightHandBone != null;
    }

    private void ApplyHandRotation(float yawOffsetDeg)
    {
        if (Mathf.Abs(yawOffsetDeg) <= 0.001f)
        {
            return;
        }

        // Ensure bones are assigned even if Animator appears later.
        if (leftHandBone == null || rightHandBone == null)
        {
            TryAutoAssignHandBones();
            CacheBaseRotationsIfNeeded(force: false);
        }

        // Apply around WORLD up to keep a stable yaw axis even when animation changes bone/local axes.
        Quaternion yawOffsetWorld = Quaternion.AngleAxis(yawOffsetDeg, Vector3.up);
        bool useAnimatedPoseAsBase = animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

        if (leftHandBone != null)
        {
            Quaternion baseWorldRot = useAnimatedPoseAsBase ? leftHandBone.rotation : leftHandBaseWorldRotation;
            leftHandBone.rotation = yawOffsetWorld * baseWorldRot;
        }

        if (rightHandBone != null)
        {
            Quaternion baseWorldRot = useAnimatedPoseAsBase ? rightHandBone.rotation : rightHandBaseWorldRotation;
            rightHandBone.rotation = yawOffsetWorld * baseWorldRot;
        }
    }

    // Fallback hand turning (no mouse, or the cursor ray misses the aim plane). Mouse aim is the primary input.
    private float ReadHandRotationAxis()
    {
        if (handRotationAction == null)
        {
            return 0f;
        }

        float axis = 0f;
        foreach (var control in handRotationAction.controls)
        {
            if (control is not KeyControl key)
            {
                continue;
            }

            if (key.keyCode == Key.LeftArrow && key.isPressed)
            {
                axis -= 1f;
            }
            else if (key.keyCode == Key.RightArrow && key.isPressed)
            {
                axis += 1f;
            }
        }

        return Mathf.Clamp(axis, -1f, 1f);
    }

    private void OnHandPushPerformed(InputAction.CallbackContext ctx)
    {
        // HandPush is a PassThrough action, so each bound control reports on its own (a Button action's conflict
        // resolution would swallow the second hand while the first button is held). Only presses push; releases
        // also arrive here and are ignored.
        if (!ctx.ReadValueAsButton())
            return;

        GameLog.Verbose($"[HandControl] HandPush PERFORMED. IsOwner={IsOwner}, control={ctx.control?.path}");

        if (!IsOwner || animator == null)
        {
            Debug.LogWarning($"[HandControl] Ignored HandPush. IsOwner={IsOwner}, animatorNull={animator==null}");
            return;
        }

        // Mouse clicks outside the race (intro, results) are normal, so this is not a warning.
        if (IsRaceGameplayBlocked())
        {
            GameLog.Verbose("[HandControl] Ignored HandPush: race gameplay is blocked.");
            return;
        }

        // A local modal (quit dialog) owns the input while it is open.
        if (LocalInputBlock.IsBlocked)
            return;

        // Frozen (禁锢) locks pushes: no animation here, and the server refuses the request too (TryPushServer).
        if (IsMotorFrozen())
            return;

        if (!TryResolvePushHand(ctx.control, out bool isLeft, out Mouse clickMouse))
        {
            Debug.LogWarning($"[HandControl] HandPush from an unsupported control: {ctx.control?.path}");
            return;
        }

        // A click on an interactive UI element (button, toggle...) belongs to the UI, not to the hands.
        if (clickMouse != null && IsPointerOverInteractiveUi(clickMouse.position.ReadValue()))
            return;

        GameLog.Verbose($"[HandControl] Push {(isLeft ? "left" : "right")} hand from {ctx.control.path}");

        float activeCooldown = GetActivePushCooldownLocal();
        if (isLeft)
        {
            if (Time.time < _nextPushLocalTimeLeft)
                return;
            _nextPushLocalTimeLeft = Time.time + activeCooldown;
        }
        else
        {
            if (Time.time < _nextPushLocalTimeRight)
                return;
            _nextPushLocalTimeRight = Time.time + activeCooldown;
        }

        TryStartPush(isLeft);
        StartDelayedPushAnimationLocal(isLeft);
    }

    /// <summary>
    /// Maps a HandPush control to a hand. Mouse left button = left hand, mouse right button = right hand (primary keys);
    /// <paramref name="clickMouse"/> is set for mouse buttons only.
    /// </summary>
    private static bool TryResolvePushHand(InputControl control, out bool isLeft, out Mouse clickMouse)
    {
        isLeft = false;
        clickMouse = null;

        if (control is ButtonControl button && button.device is Mouse mouse)
        {
            if (button == mouse.leftButton)
            {
                isLeft = true;
                clickMouse = mouse;
                return true;
            }

            if (button == mouse.rightButton)
            {
                clickMouse = mouse;
                return true;
            }

            return false;
        }

        // 废弃内容（deprecated）: W/↑ 推掌暂时保留，后续可能再调整；主键位是鼠标左/右键。见 Docs/gameplay/README.md §3
        if (control is KeyControl key)
        {
            if (key.keyCode == Key.W)
            {
                isLeft = true;
                return true;
            }

            if (key.keyCode == Key.UpArrow)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the topmost UI raycast hit under the pointer belongs to an active, interactable Selectable.
    /// Non-interactive HUD graphics (images, labels) do not block pushes.
    /// </summary>
    private static bool IsPointerOverInteractiveUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem) { position = screenPosition };
        s_UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointer, s_UiRaycastResults);

        bool blocked = false;
        if (s_UiRaycastResults.Count > 0 && s_UiRaycastResults[0].gameObject != null)
        {
            // The click goes to the topmost hit and bubbles up to its nearest Selectable parent.
            Selectable selectable = s_UiRaycastResults[0].gameObject.GetComponentInParent<Selectable>();
            blocked = selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }

        s_UiRaycastResults.Clear();
        return blocked;
    }

    private bool IsMotorFrozen()
    {
        return _motor != null && _motor.IsFrozen;
    }

    private void StartDelayedPushAnimationLocal(bool isLeft)
    {
        float delayedPushSeconds = GetChargedPushActionDelayLocal();
        if (delayedPushSeconds <= 0f)
        {
            PlayPushAnimationLocal(isLeft);
            return;
        }

        StartCoroutine(PlayPushAnimationAfterDelayLocal(isLeft, delayedPushSeconds));
    }

    private IEnumerator PlayPushAnimationAfterDelayLocal(bool isLeft, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        PlayPushAnimationLocal(isLeft);
    }

    private void PlayPushAnimationLocal(bool isLeft)
    {
        if (!IsOwner || animator == null)
            return;

        if (isLeft)
        {
            TriggerLeftHandLocal();
            if (replicateToOthers)
                PlayLeftHandServerRpc();
        }
        else
        {
            TriggerRightHandLocal();
            if (replicateToOthers)
                PlayRightHandServerRpc();
        }
    }

    private void TriggerLeftHandLocal()
    {
        animator.ResetTrigger(leftHandTriggerName);
        animator.SetTrigger(leftHandTriggerName);
    }

    private void TriggerRightHandLocal()
    {
        animator.ResetTrigger(rightHandTriggerName);
        animator.SetTrigger(rightHandTriggerName);
    }

    
    private void TryStartPush(bool isLeft)
    {
        // Snapshot the current LOCAL hand yaw offset at the moment of input.
        // We pass this to the server so the shove direction matches your hand-aim.
        float yawSnapshot = handYawOffsetDeg;
        Vector3 handWorldPositionSnapshot = GetHandWorldPositionSnapshot(isLeft);

        // Server spawns hitbox after windup. The server is authoritative for hit detection.
        RequestPushServerRpc(isLeft, yawSnapshot, handWorldPositionSnapshot);
    }

    [ServerRpc]
    private void RequestPushServerRpc(bool isLeft, float yawSnapshotDeg, Vector3 handWorldPositionSnapshot)
    {
        TryPushServer(isLeft, yawSnapshotDeg, handWorldPositionSnapshot, false);
    }

    private bool TryPushServer(bool isLeft, float yawSnapshotDeg, Vector3 handWorldPositionSnapshot, bool animateServerInput)
    {
        // Frozen (禁锢) locks pushes. Checked before any cooldown write, so a refused push does not burn the hand cooldown.
        if (IsMotorFrozen())
            return false;

        if (!ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject))
            return false;

        bool useProjectileMode = IsProjectilePushModeActiveServer();
        bool useChargedProjectileRuntime = _projectileUseChargedRuntimeServer;

        // Server-side cooldown to prevent spamming.
        if (isLeft)
        {
            float activeCooldown = GetActivePushCooldownServer(useProjectileMode, useChargedProjectileRuntime);
            if (Time.time < _nextPushServerTimeLeft)
                return false;
            _nextPushServerTimeLeft = Time.time + activeCooldown;
        }
        else
        {
            float activeCooldown = GetActivePushCooldownServer(useProjectileMode, useChargedProjectileRuntime);
            if (Time.time < _nextPushServerTimeRight)
                return false;
            _nextPushServerTimeRight = Time.time + activeCooldown;
        }

        if (!useProjectileMode && pushHitboxPrefab == null)
            return false;

        float projectileSpeed = _projectileSpeedServer;
        float projectileLifetime = _projectileLifetimeServer;
        float projectileImpulseStrength = _projectileImpulseStrengthServer;
        Vector3 projectileColliderSize = _projectileColliderSizeServer;
        float projectileBuildUpSeconds = _projectileBuildUpSecondsServer;
        bool projectileIgnoreSolidWorld = _projectileIgnoreSolidWorldServer;

        StartCoroutine(ServerSpawnPushAfterWindup(
            isLeft,
            yawSnapshotDeg,
            handWorldPositionSnapshot,
            useProjectileMode,
            useChargedProjectileRuntime,
            projectileBuildUpSeconds,
            projectileSpeed,
            projectileLifetime,
            projectileImpulseStrength,
            projectileColliderSize,
            projectileIgnoreSolidWorld));
        if (animateServerInput)
            StartCoroutine(AnimateServerInput(isLeft, useProjectileMode ? _projectileDelayedPushSecondsServer : 0f, _inputGeneration));
        return true;
    }

    private IEnumerator AnimateServerInput(bool left, float delay, uint generation)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (generation != _inputGeneration) yield break;
        if (left) PlayLeftHandObserversRpc(); else PlayRightHandObserversRpc();
    }

    private IEnumerator ServerSpawnPushAfterWindup(
        bool isLeft,
        float yawSnapshotDeg,
        Vector3 handWorldPositionSnapshot,
        bool useProjectileMode,
        bool useChargedProjectileRuntime,
        float projectileBuildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        bool projectileIgnoreSolidWorld)
    {
        uint generation = _inputGeneration;
        if (pushWindupSeconds > 0f)
            yield return new WaitForSeconds(pushWindupSeconds);

        if (generation != _inputGeneration) yield break;

        if (!useProjectileMode && pushHitboxPrefab == null)
            yield break;

        // Direction is character forward rotated by the yaw snapshot, so it matches hand aim.
        Vector3 dir = Quaternion.AngleAxis(yawSnapshotDeg, Vector3.up) * transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir.Normalize();

        float scaleMultiplier = GetPushScaleMultiplier();
        float forwardOffset = useProjectileMode ? _projectileForwardOffsetServer : 0f;
        float heightOffset = useProjectileMode ? _projectileHeightOffsetServer : 0f;
        Vector3 spawnPos = GetPushSpawnPosition(handWorldPositionSnapshot, isLeft, dir, scaleMultiplier, forwardOffset, heightOffset, useProjectileMode);

        Quaternion spawnRot = Quaternion.LookRotation(dir, Vector3.up);

        if (useProjectileMode)
        {
            if (useChargedProjectileRuntime)
            {
                SpawnChargedProjectileServer(
                    spawnPos,
                    dir,
                    scaleMultiplier,
                    projectileBuildUpSeconds,
                    projectileSpeed,
                    projectileLifetimeSeconds,
                    projectileImpulseStrength,
                    projectileColliderSize,
                    0f,
                    false,
                    projectileIgnoreSolidWorld);
                yield break;
            }

            SpawnProjectilePushServer(
                spawnPos,
                dir,
                scaleMultiplier,
                projectileSpeed,
                projectileLifetimeSeconds,
                projectileImpulseStrength,
                projectileColliderSize);
            yield break;
        }

        PushHitbox hb = Instantiate(pushHitboxPrefab, spawnPos, spawnRot);
        hb.ApplyScaleMultiplier(scaleMultiplier);

        Vector3 impulse = dir * pushImpulseStrength;
        hb.Init(base.NetworkObject, impulse, pushLifetimeSeconds);
    }

    private float GetPushScaleMultiplier()
    {
        // Cached: mouse aim reads it every frame on the owner.
        if (_scaleEffect == null)
            _scaleEffect = GetComponent<PlayerScaleEffect>();
        if (_scaleEffect == null)
            return 1f;

        return Mathf.Max(0.1f, _scaleEffect.CurrentScaleMultiplier);
    }

    private Vector3 GetPushSpawnPosition(
        Vector3 handWorldPosition,
        bool isLeft,
        Vector3 dir,
        float scaleMultiplier,
        float forwardOffset,
        float heightOffset,
        bool useProjectileMode)
    {
        float sideBias = isLeft ? pushLeftSideBias : pushRightSideBias;
        float side = ((isLeft ? -pushSideOffset : pushSideOffset) + sideBias) * scaleMultiplier;
        Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;

        Vector3 baseSpawnPosition = transform.position
               + Vector3.up * (pushHeightOffset * scaleMultiplier)
               + sideDir * side
               + dir * (pushForwardOffset * scaleMultiplier);

        if (!useProjectileMode)
            return baseSpawnPosition;

        // Projectile skills reuse the validated melee push offset, then add skill-specific tuning.
        return baseSpawnPosition
               + Vector3.up * (heightOffset * scaleMultiplier)
               + dir * (forwardOffset * scaleMultiplier);
    }

    private Vector3 GetHandWorldPositionSnapshot(bool isLeft)
    {
        Transform handBone = isLeft ? leftHandBone : rightHandBone;
        if (handBone != null)
            return handBone.position;

        return transform.position;
    }

    public void ActivateProjectilePushMode(
        float activeDurationSeconds,
        float projectileBuildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        float projectileForwardOffset,
        float projectileHeightOffset,
        GameObject chargedVisualPrefab,
        string chargedProgressProperty,
        float chargedPushCooldownSeconds,
        bool useChargedProjectileRuntime = false,
        bool ignoreSolidWorld = false,
        float delayedPushActionSeconds = 0f)
    {
        if (!IsServerInitialized)
            return;

        _projectilePushModeUntilServer = Mathf.Max(_projectilePushModeUntilServer, Time.time + Mathf.Max(0.05f, activeDurationSeconds));
        _projectileUseChargedRuntimeServer = useChargedProjectileRuntime;
        _projectileBuildUpSecondsServer = Mathf.Max(0f, projectileBuildUpSeconds);
        _projectileSpeedServer = Mathf.Max(0.01f, projectileSpeed);
        _projectileLifetimeServer = Mathf.Max(0.05f, projectileLifetimeSeconds);
        _projectileImpulseStrengthServer = Mathf.Max(0f, projectileImpulseStrength);
        _projectileColliderSizeServer = SanitizeColliderSize(projectileColliderSize);
        _projectileForwardOffsetServer = Mathf.Max(0f, projectileForwardOffset);
        _projectileHeightOffsetServer = Mathf.Max(0f, projectileHeightOffset);
        _projectileIgnoreSolidWorldServer = ignoreSolidWorld;
        _projectileChargedVisualPrefabServer = chargedVisualPrefab;
        _projectileChargedProgressPropertyServer = string.IsNullOrWhiteSpace(chargedProgressProperty) ? DefaultChargedProjectileProgressProperty : chargedProgressProperty;
        _projectileChargedPushCooldownServer = Mathf.Max(0f, chargedPushCooldownSeconds);
        _projectileDelayedPushSecondsServer = Mathf.Max(0f, delayedPushActionSeconds);
    }

    public void ConfigureProjectilePushModeLocal(
        float activeDurationSeconds,
        GameObject chargedVisualPrefab,
        string chargedProgressProperty,
        GameObject chargedLaunchEffectPrefab,
        float projectileForwardOffset,
        float projectileHeightOffset,
        float chargedPushCooldownSeconds,
        bool useChargedProjectileRuntime,
        float delayedPushSeconds)
    {
        _projectilePushModeUntilLocal = Mathf.Max(_projectilePushModeUntilLocal, Time.time + Mathf.Max(0.05f, activeDurationSeconds));
        _projectileUseChargedRuntimeLocal = useChargedProjectileRuntime;
        _projectileChargedVisualPrefabLocal = chargedVisualPrefab;
        _projectileChargedProgressPropertyLocal = string.IsNullOrWhiteSpace(chargedProgressProperty) ? DefaultChargedProjectileProgressProperty : chargedProgressProperty;
        _projectileChargedLaunchEffectPrefabLocal = chargedLaunchEffectPrefab;
        _projectileForwardOffsetLocal = Mathf.Max(0f, projectileForwardOffset);
        _projectileHeightOffsetLocal = Mathf.Max(0f, projectileHeightOffset);
        _projectileChargedPushCooldownLocal = Mathf.Max(0f, chargedPushCooldownSeconds);
        _projectileDelayedPushSecondsLocal = Mathf.Max(0f, delayedPushSeconds);
    }

    private bool IsProjectilePushModeActiveServer()
    {
        return IsServerInitialized && Time.time < _projectilePushModeUntilServer;
    }

    private float GetChargedPushActionDelayLocal()
    {
        // Preserve the original NaN behavior: !(>=) is not equivalent to < for unordered values.
        bool active = _projectileUseChargedRuntimeLocal && !(Time.time >= _projectilePushModeUntilLocal);
        return PushAttackTiming.GetActionDelay(active, _projectileDelayedPushSecondsLocal);
    }

    private float GetActivePushCooldownLocal()
    {
        bool active = _projectileUseChargedRuntimeLocal && Time.time < _projectilePushModeUntilLocal;
        return PushAttackTiming.GetCooldown(active, _projectileChargedPushCooldownLocal, pushCooldownSeconds);
    }

    private float GetActivePushCooldownServer(bool useProjectileMode, bool useChargedProjectileRuntime)
    {
        return PushAttackTiming.GetCooldown(useProjectileMode && useChargedProjectileRuntime,
            _projectileChargedPushCooldownServer, pushCooldownSeconds);
    }

    private void SpawnChargedProjectileServer(
        Vector3 spawnPos,
        Vector3 dir,
        float scaleMultiplier,
        float buildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        float hitTurnTorqueImpulse = 0f,
        bool allowSelfHit = false,
        bool ignoreSolidWorld = false)
    {
        Vector3 scaledColliderSize = projectileColliderSize * scaleMultiplier;
        Vector3 scaledVisualSize = projectileVisualScale * scaleMultiplier;
        // #1 金身巨掌: the impulse follows the same scale sample as the size, uncapped.
        float impulseScale = ImpulseFollowsScale ? scaleMultiplier : 1f;
        Vector3 impulse = dir * (projectileImpulseStrength * impulseScale);
        GameObject chargedVisualPrefab = _projectileChargedVisualPrefabServer;
        string chargedProgressProperty = string.IsNullOrWhiteSpace(_projectileChargedProgressPropertyServer) ? DefaultChargedProjectileProgressProperty : _projectileChargedProgressPropertyServer;

        ChargedHandProjectileRuntime.SpawnServer(
            base.NetworkObject,
            spawnPos,
            dir,
            projectileSpeed,
            buildUpSeconds,
            projectileLifetimeSeconds,
            impulse,
            scaledColliderSize,
            hitTurnTorqueImpulse,
            allowSelfHit,
            ignoreSolidWorld);

        SpawnChargedProjectileVisualObserversRpc(
            spawnPos,
            dir,
            projectileSpeed,
            buildUpSeconds,
            projectileLifetimeSeconds,
            scaledVisualSize);
    }

    private void SpawnProjectilePushServer(
        Vector3 spawnPos,
        Vector3 dir,
        float scaleMultiplier,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        float hitTurnTorqueImpulse = 0f,
        bool allowSelfHit = false,
        bool ignoreSolidWorld = false)
    {
        Vector3 scaledColliderSize = projectileColliderSize * scaleMultiplier;
        Vector3 scaledVisualSize = projectileVisualScale * scaleMultiplier;
        Vector3 impulse = dir * projectileImpulseStrength;

        HandPushProjectileRuntime.SpawnServer(
            base.NetworkObject,
            spawnPos,
            dir,
            projectileSpeed,
            projectileLifetimeSeconds,
            impulse,
            projectilePrefab,
            scaledColliderSize,
            hitTurnTorqueImpulse,
            allowSelfHit,
            ignoreSolidWorld);

        SpawnProjectileVisualObserversRpc(
            spawnPos,
            dir,
            projectileSpeed,
            projectileLifetimeSeconds,
            scaledVisualSize);
    }

    public void FireProjectileBurstImmediate(
        int projectileCount,
        float projectileSpacing,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        bool reverseDirection,
        bool allowSelfHit,
        bool ignoreSolidWorld = false,
        float hitTurnTorqueImpulse = 0f,
        float additionalForwardSpawnOffset = 0f,
        float additionalHeightSpawnOffset = 0f)
    {
        if (!IsServerInitialized)
            return;

        int safeProjectileCount = Mathf.Max(1, projectileCount);
        float safeSpacing = Mathf.Max(0f, projectileSpacing);
        float scaleMultiplier = GetPushScaleMultiplier();
        Vector3 outwardDir = GetCurrentAimDirectionServer();
        Vector3 burstAnchor = GetProjectileBurstAnchorPosition(outwardDir, scaleMultiplier, additionalForwardSpawnOffset, additionalHeightSpawnOffset);
        Vector3 travelDir = reverseDirection ? -outwardDir : outwardDir;
        Vector3 sideDir = Vector3.Cross(Vector3.up, outwardDir).normalized;

        for (int i = 0; i < safeProjectileCount; i++)
        {
            Vector3 spawnPos = ProjectileBurstPlanner.GetSpawnPosition(burstAnchor, sideDir, i, safeProjectileCount, safeSpacing, scaleMultiplier);

            SpawnProjectilePushServer(
                spawnPos,
                travelDir,
                scaleMultiplier,
                projectileSpeed,
                projectileLifetimeSeconds,
                projectileImpulseStrength,
                projectileColliderSize,
                hitTurnTorqueImpulse,
                allowSelfHit,
                ignoreSolidWorld);
        }
    }

    public void FireChargedProjectileImmediate(
        float buildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        float additionalForwardSpawnOffset = 0f,
        float additionalHeightSpawnOffset = 0f,
        float hitTurnTorqueImpulse = 0f,
        bool allowSelfHit = false,
        bool ignoreSolidWorld = false)
    {
        if (!IsServerInitialized)
            return;

        float scaleMultiplier = GetPushScaleMultiplier();
        Vector3 outwardDir = GetCurrentAimDirectionServer();
        Vector3 spawnPos = GetProjectileBurstAnchorPosition(outwardDir, scaleMultiplier, additionalForwardSpawnOffset, additionalHeightSpawnOffset);
        Vector3 scaledColliderSize = SanitizeColliderSize(projectileColliderSize * scaleMultiplier);
        Vector3 scaledVisualSize = projectileVisualScale * scaleMultiplier;
        Vector3 impulse = outwardDir * Mathf.Max(0f, projectileImpulseStrength);

        ChargedHandProjectileRuntime.SpawnServer(
            base.NetworkObject,
            spawnPos,
            outwardDir,
            projectileSpeed,
            buildUpSeconds,
            projectileLifetimeSeconds,
            impulse,
            scaledColliderSize,
            hitTurnTorqueImpulse,
            allowSelfHit,
            ignoreSolidWorld);

        SpawnChargedProjectileVisualObserversRpc(
            spawnPos,
            outwardDir,
            projectileSpeed,
            buildUpSeconds,
            projectileLifetimeSeconds,
            scaledVisualSize);
    }

    private Vector3 GetCurrentAimDirectionServer()
    {
        Vector3 dir = Quaternion.AngleAxis(_syncedYawOffsetDeg.Value, Vector3.up) * transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.y = 0f;
        return dir.normalized;
    }

    private Vector3 GetProjectileBurstAnchorPosition(Vector3 outwardDir, float scaleMultiplier, float additionalForwardSpawnOffset, float additionalHeightSpawnOffset)
    {
        float forwardDistance = (pushForwardOffset + Mathf.Max(0f, additionalForwardSpawnOffset)) * scaleMultiplier;
        float extraHeight = Mathf.Max(0f, additionalHeightSpawnOffset) * scaleMultiplier;

        if (leftHandBone != null && rightHandBone != null)
        {
            Vector3 midpoint = (leftHandBone.position + rightHandBone.position) * 0.5f;
            return midpoint
                   + Vector3.up * ((pushHeightOffset * 0.1f * scaleMultiplier) + extraHeight)
                   + outwardDir * forwardDistance;
        }

        return transform.position
               + Vector3.up * ((pushHeightOffset * scaleMultiplier) + extraHeight)
               + outwardDir * forwardDistance;
    }

    [ObserversRpc]
    private void SpawnProjectileVisualObserversRpc(
        Vector3 spawnPos,
        Vector3 dir,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        Vector3 scaledVisualSize)
    {
        HandPushProjectileRuntime.SpawnVisual(
            spawnPos,
            dir,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectilePrefab,
            projectileVisualLocalEuler,
            scaledVisualSize);
    }

    [ObserversRpc]
    private void SpawnChargedProjectileVisualObserversRpc(
        Vector3 spawnPos,
        Vector3 dir,
        float projectileSpeed,
        float buildUpSeconds,
        float projectileLifetimeSeconds,
        Vector3 scaledVisualSize)
    {
        GameObject resolvedVisualPrefab = _projectileChargedVisualPrefabLocal;
        string resolvedProgressProperty = !string.IsNullOrWhiteSpace(_projectileChargedProgressPropertyLocal) ? _projectileChargedProgressPropertyLocal : DefaultChargedProjectileProgressProperty;
        float scaleMultiplier = GetPushScaleMultiplier();
        Vector3 launchEffectWorldOffset = (-dir * (_projectileForwardOffsetLocal * scaleMultiplier)) + (Vector3.up * (-_projectileHeightOffsetLocal * scaleMultiplier));

        ChargedHandProjectileRuntime.SpawnVisual(
            spawnPos,
            dir,
            projectileSpeed,
            buildUpSeconds,
            projectileLifetimeSeconds,
            resolvedVisualPrefab != null ? resolvedVisualPrefab : projectilePrefab,
            projectileVisualLocalEuler,
            scaledVisualSize,
            resolvedProgressProperty,
            transform,
            _projectileChargedLaunchEffectPrefabLocal,
            launchEffectWorldOffset);
    }

    private static Vector3 SanitizeColliderSize(Vector3 size)
    {
        return ProjectileBurstPlanner.SanitizeColliderSize(size);
    }

    [ServerRpc]
    private void PlayLeftHandServerRpc()
    {
        PlayLeftHandObserversRpc();
    }

    [ObserversRpc]
    private void PlayLeftHandObserversRpc()
    {
        // 避免本地重复触发（本地已经播了）
        if (IsOwner) return;

        if (animator != null)
            animator.SetTrigger(leftHandTriggerName);
    }

    [ServerRpc]
    private void PlayRightHandServerRpc()
    {
        PlayRightHandObserversRpc();
    }

    [ObserversRpc]
    private void PlayRightHandObserversRpc()
    {
        if (IsOwner) return;
        if (animator != null)
            animator.SetTrigger(rightHandTriggerName);
    }

    private bool IsRaceGameplayBlocked()
    {
        return !ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject);
    }


}
