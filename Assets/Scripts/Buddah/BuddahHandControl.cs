using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;


public class BuddahHandControl : MonoBehaviour
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

    private InputSystem_Actions inputActions;
    private InputAction handRotationAction;
    private Animator animator;

    private float handYawOffsetDeg;
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
        }

        TryAutoAssignHandBones();
        CacheBaseRotationsIfNeeded(force: true);
    }

    private void OnEnable()
    {
        inputActions?.Enable();
        CacheBaseRotationsIfNeeded(force: false);
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        HandRotationAxis = ReadHandRotationAxis();

        if (Mathf.Abs(HandRotationAxis) > 0.001f)
        {
            handYawOffsetDeg += HandRotationAxis * rotationSpeedDegPerSec * Time.deltaTime;
        }
    }

    private void LateUpdate()
    {
        // Apply after Animator writes bone transforms each frame.
        ApplyHandRotation(handYawOffsetDeg);
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
}
