using UnityEngine;

public sealed class DisabledBuddahInputSource : IBuddahInputSource
{
    public bool AllowMovementWhenGameplayBlocked => false;
    public bool UseDirectHeadingControl => false;

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
        return 0f;
    }

    public float GetSteering()
    {
        return 0f;
    }

    public Vector3 GetDesiredForward()
    {
        return Vector3.zero;
    }
}
