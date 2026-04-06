using UnityEngine;

public abstract class IntroPath : MonoBehaviour
{
    [Header("Terminal Pose")]
    [SerializeField] protected bool lockTerminalPositionToSlot = true;
    [SerializeField] protected bool lockTerminalForwardToSlot;

    protected Transform BoundLaunchPoint;
    protected Transform BoundForwardReference;

    public virtual void BindTerminalPose(Transform launchPoint, Transform forwardReference)
    {
        BoundLaunchPoint = launchPoint;
        BoundForwardReference = forwardReference;
    }

    public abstract Vector3 EvaluatePosition(float t);
    public abstract Vector3 EvaluateTangent(float t);

    public virtual Quaternion EvaluateRotation(float t)
    {
        Vector3 tangent = EvaluateTangent(t);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = transform.forward;

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.forward;

        return Quaternion.LookRotation(tangent.normalized, Vector3.up);
    }

    public virtual Vector3 GetTerminalPosition()
    {
        if (lockTerminalPositionToSlot && BoundLaunchPoint != null)
            return BoundLaunchPoint.position;

        return EvaluatePosition(1f);
    }

    public virtual Vector3 GetTerminalForward()
    {
        if (lockTerminalForwardToSlot && BoundForwardReference != null)
            return BoundForwardReference.forward.normalized;

        return EvaluateTangent(1f).normalized;
    }
}
