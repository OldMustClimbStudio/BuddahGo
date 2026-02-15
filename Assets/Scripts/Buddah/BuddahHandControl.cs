using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
// Add the appropriate using directive for PushHitbox (replace with actual namespace)
// using YourNamespace;


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

    private float _nextPushServerTimeLeft;
    private float _nextPushServerTimeRight;
    private float _nextPushLocalTimeLeft;
    private float _nextPushLocalTimeRight;

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
        animator = GetComponent<Animator>();

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
        if (!IsOwner)
            return;

        HandRotationAxis = ReadHandRotationAxis();

        if (Mathf.Abs(HandRotationAxis) > 0.001f)
        {
            handYawOffsetDeg += HandRotationAxis * rotationSpeedDegPerSec * Time.deltaTime;
        }

        TrySendYawToServer();
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

        if (!IsOwner || animator == null)
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
            if (Time.time < _nextPushLocalTimeLeft)
                return;
            _nextPushLocalTimeLeft = Time.time + pushCooldownSeconds;

            TriggerLeftHandLocal();
            if (replicateToOthers)
                PlayLeftHandServerRpc();
            TryStartPush(true);
        }
        else if (key.keyCode == Key.UpArrow)
        {
            if (Time.time < _nextPushLocalTimeRight)
                return;
            _nextPushLocalTimeRight = Time.time + pushCooldownSeconds;

            TriggerRightHandLocal();
            if (replicateToOthers)
                PlayRightHandServerRpc();
            TryStartPush(false);
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

        // Server spawns hitbox after windup. The server is authoritative for hit detection.
        RequestPushServerRpc(isLeft, yawSnapshot);
    }

    [ServerRpc]
    private void RequestPushServerRpc(bool isLeft, float yawSnapshotDeg)
    {
        // Server-side cooldown to prevent spamming.
        if (isLeft)
        {
            if (Time.time < _nextPushServerTimeLeft)
                return;
            _nextPushServerTimeLeft = Time.time + pushCooldownSeconds;
        }
        else
        {
            if (Time.time < _nextPushServerTimeRight)
                return;
            _nextPushServerTimeRight = Time.time + pushCooldownSeconds;
        }

        if (pushHitboxPrefab == null)
            return;

        StartCoroutine(ServerSpawnPushAfterWindup(isLeft, yawSnapshotDeg));
    }

    private IEnumerator ServerSpawnPushAfterWindup(bool isLeft, float yawSnapshotDeg)
    {
        if (pushWindupSeconds > 0f)
            yield return new WaitForSeconds(pushWindupSeconds);

        if (pushHitboxPrefab == null)
            yield break;

        // Direction is character forward rotated by the yaw snapshot, so it matches hand aim.
        Vector3 dir = Quaternion.AngleAxis(yawSnapshotDeg, Vector3.up) * transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir.Normalize();

        float sideBias = isLeft ? pushLeftSideBias : pushRightSideBias;
        float side = (isLeft ? -pushSideOffset : pushSideOffset) + sideBias;

        Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;

        Vector3 spawnPos = transform.position
                   + Vector3.up * pushHeightOffset
                   + sideDir * side
                   + dir * pushForwardOffset;

        Quaternion spawnRot = Quaternion.LookRotation(dir, Vector3.up);

        PushHitbox hb = Instantiate(pushHitboxPrefab, spawnPos, spawnRot);

        Vector3 impulse = dir * pushImpulseStrength;
        hb.Init(base.NetworkObject, impulse, pushLifetimeSeconds);
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


}
