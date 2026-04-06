using UnityEngine;

public struct LaunchHandoffSnapshot
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
    public Vector3 Forward;

    public static LaunchHandoffSnapshot Default(Transform source)
    {
        Vector3 forward = source != null ? source.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Quaternion rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return new LaunchHandoffSnapshot
        {
            Position = source != null ? source.position : Vector3.zero,
            Rotation = rotation,
            Velocity = Vector3.zero,
            AngularVelocity = Vector3.zero,
            Forward = forward.normalized
        };
    }
}
