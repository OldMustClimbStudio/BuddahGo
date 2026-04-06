using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class PlayerBuddahInputSource : IBuddahInputSource
{
    private readonly InputAction _movementAction;

    public bool AllowMovementWhenGameplayBlocked => false;
    public bool UseDirectHeadingControl => false;

    public PlayerBuddahInputSource(InputAction movementAction)
    {
        _movementAction = movementAction;
    }

    public void OnActivated(Transform root, Rigidbody rb)
    {
    }

    public void OnDeactivated(Transform root, Rigidbody rb)
    {
    }

    public void Tick(float deltaTime, Transform root, Rigidbody rb)
    {
    }

    public float GetThrottle()
    {
        return 1f;
    }

    public float GetSteering()
    {
        if (_movementAction == null)
            return 0f;

        float horizontal = 0f;
        foreach (InputControl control in _movementAction.controls)
        {
            if (control is not KeyControl key)
                continue;

            if (key.keyCode == Key.A && key.isPressed)
                horizontal -= 1f;
            else if (key.keyCode == Key.D && key.isPressed)
                horizontal += 1f;
        }

        return Mathf.Clamp(horizontal, -1f, 1f);
    }

    public Vector3 GetDesiredForward()
    {
        return Vector3.zero;
    }
}
