using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(NetworkObject))]
public class BuddahMovement : NetworkBehaviour
{
    [Header("Movement (Physics)")]
    [SerializeField] private float forwardForce = 20f;
    [SerializeField] private float turnTorque = 12f;
    [SerializeField] private float maxSpeed = 8f;


    [Header("Push Reaction")]
    [SerializeField] private float pushGraceSeconds = 0.25f;
    [SerializeField] private float pushExtraMaxSpeed = 6f;

    private float _pushGraceTimer;

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    private InputSystem_Actions inputActions;
    private InputAction movementAction;

    private bool _inputInitialized;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        movementAction = inputActions.Player.Movement;
        _inputInitialized = true;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Client-authoritative: only the owning client reads input and simulates physics.
        bool isLocalOwner = IsOwner;

        if (rb != null)
            rb.isKinematic = !isLocalOwner;

        SetInputEnabled(isLocalOwner);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        SetInputEnabled(false);
    }

    private void OnDisable()
    {
        // Covers despawn / scene unload in-editor.
        SetInputEnabled(false);
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

    private void FixedUpdate()
    {
        // Only simulate on the owning client.
        if (!IsOwner)
            return;

        float horizontal = 0f;
        if (movementAction != null)
        {
            foreach (var control in movementAction.controls)
            {
                if (control is not KeyControl key)
                {
                    continue;
                }

                if (key.keyCode == Key.A && key.isPressed)
                {
                    horizontal -= 1f;
                }
                else if (key.keyCode == Key.D && key.isPressed)
                {
                    horizontal += 1f;
                }
            }
        }

        // Constant forward push
        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();
        rb.AddForce(forwardDir * forwardForce, ForceMode.Force);

        // Turn via torque (A/D)
        if (Mathf.Abs(horizontal) > 0.001f)
        {
            rb.AddTorque(Vector3.up * horizontal * turnTorque, ForceMode.Force);
        }

        // Clamp max speed (horizontal plane)
// NOTE: pushes apply an impulse; allow a brief grace window where max speed is higher,
// otherwise the clamp will immediately eat the impulse and the shove will feel like "nothing happened".
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
}

    [TargetRpc]
    public void ApplyPushImpulseTargetRpc(NetworkConnection conn, Vector3 impulse)
    {
        // Only the owning client simulates physics (rb is non-kinematic there).
        if (!IsOwner || rb == null)
            return;

        _pushGraceTimer = pushGraceSeconds;
        rb.AddForce(impulse, ForceMode.Impulse);
    }
}
