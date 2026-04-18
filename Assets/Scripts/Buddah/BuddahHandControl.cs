using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(NetworkObject))]
public class BuddahHandControl : NetworkBehaviour
{
    [Header("Input")]
    [Tooltip("Read from InputSystem_Actions -> Player -> HandRotation (LeftArrow / RightArrow).")]
    [SerializeField] private bool useGeneratedInputActions = true;

    [Header("Hand Bones")]
    [Tooltip("Optional: if empty, will try to auto-find via Animator Humanoid LeftHand/RightHand.")]
    [SerializeField] private Transform leftHandBone;
    [Tooltip("Optional: if empty, will try to auto-find via Animator Humanoid LeftHand/RightHand.")]
    [SerializeField] private Transform rightHandBone;

    [Header("Rotation")]
    [Tooltip("Degrees per second.")]
    [SerializeField] private float rotationSpeedDegPerSec = 180f;

    [Header("Hand Animation")]
    [Tooltip("Animator trigger fired when W is pressed (left-hand one-shot animation).")]
    [SerializeField] private string leftHandTriggerName = "LeftW";

    [Tooltip("Animator trigger fired when UpArrow is pressed (right-hand one-shot animation).")]
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
    /// -1 = LeftArrow, +1 = RightArrow, 0 = none.
    /// </summary>
    public float HandRotationAxis { get; private set; }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);

        if (useGeneratedInputActions)
        {
            inputActions = new InputSystem_Actions();
            handRotationAction = inputActions.Player.HandRotation;
            handPushAction = inputActions.Player.HandPush;
        }

        TryAutoAssignHandBones();
        CacheBaseRotationsIfNeeded(force: true);
    }

    private void OnEnable()
    {
        Debug.Log($"[HandControl] OnEnable. IsOwner={IsOwner}, netObj={gameObject.name}");
        CacheBaseRotationsIfNeeded(force: false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        inputActions?.Enable();
        Debug.Log("[HandControl] inputActions Enabled (owner).");

        if (handPushAction != null)
        {
            handPushAction.performed += OnHandPushPerformed;
            Debug.Log("[HandControl] Subscribed handPushAction.performed.");
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

        HandRotationAxis = ReadHandRotationAxis();

        if (Mathf.Abs(HandRotationAxis) > 0.001f)
        {
            handYawOffsetDeg += HandRotationAxis * rotationSpeedDegPerSec * Time.deltaTime;
        }

        TrySendYawToServer();
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
        Debug.Log("[HandControl] Projectile push mode expired on server. Reverting to normal push.");
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
        Debug.Log("[HandControl] Projectile push mode expired locally. Reverting to normal push.");
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

        if (Mathf.Abs(handYawOffsetDeg - _lastSentYaw) < 0.1f)
            return;

        _lastSentYaw = handYawOffsetDeg;
        _nextSendTime = Time.time + 0.05f;
        SetHandYawServerRpc(handYawOffsetDeg);
    }

    [ServerRpc]
    private void SetHandYawServerRpc(float yawOffsetDeg)
    {
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
        Debug.Log($"[HandControl] HandPush PERFORMED. IsOwner={IsOwner}, control={ctx.control?.path}");

        if (!IsOwner || animator == null || IsRaceGameplayBlocked())
        {
            Debug.LogWarning($"[HandControl] Ignored HandPush. IsOwner={IsOwner}, animatorNull={animator==null}");
            return;
        }

        if (ctx.control is not KeyControl key)
        {
            Debug.LogWarning("[HandControl] HandPush was not a KeyControl.");
            return;
        }

        Debug.Log($"[HandControl] Key = {key.keyCode}");

        if (key.keyCode == Key.W)
        {
            float activeCooldown = GetActivePushCooldownLocal();
            if (Time.time < _nextPushLocalTimeLeft)
                return;
            _nextPushLocalTimeLeft = Time.time + activeCooldown;
            TryStartPush(true);
            StartDelayedPushAnimationLocal(true);
        }
        else if (key.keyCode == Key.UpArrow)
        {
            float activeCooldown = GetActivePushCooldownLocal();
            if (Time.time < _nextPushLocalTimeRight)
                return;
            _nextPushLocalTimeRight = Time.time + activeCooldown;
            TryStartPush(false);
            StartDelayedPushAnimationLocal(false);
        }
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
        if (!ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject))
            return;

        bool useProjectileMode = IsProjectilePushModeActiveServer();
        bool useChargedProjectileRuntime = _projectileUseChargedRuntimeServer;

        // Server-side cooldown to prevent spamming.
        if (isLeft)
        {
            float activeCooldown = GetActivePushCooldownServer(useProjectileMode, useChargedProjectileRuntime);
            if (Time.time < _nextPushServerTimeLeft)
                return;
            _nextPushServerTimeLeft = Time.time + activeCooldown;
        }
        else
        {
            float activeCooldown = GetActivePushCooldownServer(useProjectileMode, useChargedProjectileRuntime);
            if (Time.time < _nextPushServerTimeRight)
                return;
            _nextPushServerTimeRight = Time.time + activeCooldown;
        }

        if (!useProjectileMode && pushHitboxPrefab == null)
            return;

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
        if (pushWindupSeconds > 0f)
            yield return new WaitForSeconds(pushWindupSeconds);

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
        var scaleEffect = GetComponent<PlayerScaleEffect>();
        if (scaleEffect == null)
            return 1f;

        return Mathf.Max(0.1f, scaleEffect.CurrentScaleMultiplier);
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
        bool ignoreSolidWorld = false)
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
        if (!_projectileUseChargedRuntimeLocal || Time.time >= _projectilePushModeUntilLocal)
            return 0f;

        return Mathf.Max(0f, _projectileDelayedPushSecondsLocal);
    }

    private float GetActivePushCooldownLocal()
    {
        if (_projectileUseChargedRuntimeLocal && Time.time < _projectilePushModeUntilLocal)
            return GetChargedProjectilePushCooldownLocal();

        return Mathf.Max(0f, pushCooldownSeconds);
    }

    private float GetActivePushCooldownServer(bool useProjectileMode, bool useChargedProjectileRuntime)
    {
        if (useProjectileMode && useChargedProjectileRuntime)
            return GetChargedProjectilePushCooldownServer();

        return Mathf.Max(0f, pushCooldownSeconds);
    }

    private float GetChargedProjectilePushCooldownLocal()
    {
        return _projectileChargedPushCooldownLocal > 0f
            ? _projectileChargedPushCooldownLocal
            : Mathf.Max(0f, pushCooldownSeconds);
    }

    private float GetChargedProjectilePushCooldownServer()
    {
        return _projectileChargedPushCooldownServer > 0f
            ? _projectileChargedPushCooldownServer
            : Mathf.Max(0f, pushCooldownSeconds);
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
        Vector3 impulse = dir * projectileImpulseStrength;
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
            float offsetIndex = i - ((safeProjectileCount - 1) * 0.5f);
            Vector3 spawnPos = burstAnchor + (sideDir * (offsetIndex * safeSpacing * scaleMultiplier));

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

    public void FireChargedProjectileBurstServerOnly(
        int projectileCount,
        float projectileSpacing,
        float buildUpSeconds,
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

        SpawnChargedProjectileBurst(
            projectileCount,
            projectileSpacing,
            buildUpSeconds,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectileImpulseStrength,
            projectileColliderSize,
            reverseDirection,
            allowSelfHit,
            ignoreSolidWorld,
            hitTurnTorqueImpulse,
            additionalForwardSpawnOffset,
            additionalHeightSpawnOffset,
            spawnServerHitboxes: true,
            spawnVisuals: false,
            visualPrefabOverride: null,
            progressPropertyOverride: null);
    }

    public void SpawnChargedProjectileBurstVisualLocal(
        int projectileCount,
        float projectileSpacing,
        float buildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        Vector3 projectileColliderSize,
        bool reverseDirection,
        float additionalForwardSpawnOffset = 0f,
        float additionalHeightSpawnOffset = 0f,
        GameObject visualPrefabOverride = null,
        string progressPropertyOverride = null)
    {
        SpawnChargedProjectileBurst(
            projectileCount,
            projectileSpacing,
            buildUpSeconds,
            projectileSpeed,
            projectileLifetimeSeconds,
            0f,
            projectileColliderSize,
            reverseDirection,
            allowSelfHit: false,
            ignoreSolidWorld: true,
            hitTurnTorqueImpulse: 0f,
            additionalForwardSpawnOffset,
            additionalHeightSpawnOffset,
            spawnServerHitboxes: false,
            spawnVisuals: true,
            visualPrefabOverride,
            progressPropertyOverride);
    }

    private void SpawnChargedProjectileBurst(
        int projectileCount,
        float projectileSpacing,
        float buildUpSeconds,
        float projectileSpeed,
        float projectileLifetimeSeconds,
        float projectileImpulseStrength,
        Vector3 projectileColliderSize,
        bool reverseDirection,
        bool allowSelfHit,
        bool ignoreSolidWorld,
        float hitTurnTorqueImpulse,
        float additionalForwardSpawnOffset,
        float additionalHeightSpawnOffset,
        bool spawnServerHitboxes,
        bool spawnVisuals,
        GameObject visualPrefabOverride,
        string progressPropertyOverride)
    {
        int safeProjectileCount = Mathf.Max(1, projectileCount);
        float safeSpacing = Mathf.Max(0f, projectileSpacing);
        float scaleMultiplier = GetPushScaleMultiplier();
        Vector3 outwardDir = GetCurrentAimDirectionServer();
        Vector3 burstAnchor = GetProjectileBurstAnchorPosition(outwardDir, scaleMultiplier, additionalForwardSpawnOffset, additionalHeightSpawnOffset);
        Vector3 travelDir = reverseDirection ? -outwardDir : outwardDir;
        Vector3 sideDir = Vector3.Cross(Vector3.up, outwardDir).normalized;
        Vector3 scaledColliderSize = SanitizeColliderSize(projectileColliderSize * scaleMultiplier);
        Vector3 scaledVisualSize = projectileVisualScale * scaleMultiplier;
        string resolvedProgressProperty = string.IsNullOrWhiteSpace(progressPropertyOverride) ? DefaultChargedProjectileProgressProperty : progressPropertyOverride;

        for (int i = 0; i < safeProjectileCount; i++)
        {
            float offsetIndex = i - ((safeProjectileCount - 1) * 0.5f);
            Vector3 spawnPos = burstAnchor + (sideDir * (offsetIndex * safeSpacing * scaleMultiplier));

            if (spawnServerHitboxes)
            {
                Vector3 impulse = travelDir * Mathf.Max(0f, projectileImpulseStrength);
                ChargedHandProjectileRuntime.SpawnServer(
                    base.NetworkObject,
                    spawnPos,
                    travelDir,
                    projectileSpeed,
                    buildUpSeconds,
                    projectileLifetimeSeconds,
                    impulse,
                    scaledColliderSize,
                    hitTurnTorqueImpulse,
                    allowSelfHit,
                    ignoreSolidWorld);
            }

            if (spawnVisuals)
            {
                ChargedHandProjectileRuntime.SpawnVisual(
                    spawnPos,
                    travelDir,
                    projectileSpeed,
                    buildUpSeconds,
                    projectileLifetimeSeconds,
                    visualPrefabOverride != null ? visualPrefabOverride : projectilePrefab,
                    projectileVisualLocalEuler,
                    scaledVisualSize,
                    resolvedProgressProperty,
                    transform);
            }
        }
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
        return new Vector3(
            Mathf.Max(0.05f, Mathf.Abs(size.x)),
            Mathf.Max(0.05f, Mathf.Abs(size.y)),
            Mathf.Max(0.05f, Mathf.Abs(size.z)));
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
