using UnityEngine;

public interface IBuddahInputSource
{
    bool AllowMovementWhenGameplayBlocked { get; }
    bool UseDirectHeadingControl { get; }

    void OnActivated(Transform root, Rigidbody rb);
    void OnDeactivated(Transform root, Rigidbody rb);
    void Tick(float deltaTime, Transform root, Rigidbody rb);

    float GetThrottle();
    float GetSteering();
    Vector3 GetDesiredForward();
}
