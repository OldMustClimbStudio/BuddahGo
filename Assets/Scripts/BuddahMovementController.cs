using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class BuddahMovement : MonoBehaviour
{
    [Header("Movement (Physics)")]
    [SerializeField] private float forwardForce = 20f;
    [SerializeField] private float turnTorque = 12f;
    [SerializeField] private float maxSpeed = 8f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;

    private InputSystem_Actions inputActions;
    private InputAction movementAction;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        movementAction = inputActions.Player.Movement;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void FixedUpdate()
    {
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
        Vector3 velocity = rb.velocity;
        Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
        if (planar.magnitude > maxSpeed)
        {
            Vector3 clamped = planar.normalized * maxSpeed;
            rb.velocity = new Vector3(clamped.x, velocity.y, clamped.z);
        }
    }
}