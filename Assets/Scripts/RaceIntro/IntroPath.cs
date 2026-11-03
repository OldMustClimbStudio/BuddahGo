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
}
