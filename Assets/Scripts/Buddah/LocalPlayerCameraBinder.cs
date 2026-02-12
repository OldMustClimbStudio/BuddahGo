using FishNet.Object;
using UnityEngine;

public class LocalPlayerCameraBinder : NetworkBehaviour
{
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Rigidbody followTargetRigidbody;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        if (cameraControl == null)
            return;

        if (followTarget == null)
            followTarget = transform;

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponent<Rigidbody>();

        cameraControl.SetFollowTarget(followTarget, followTargetRigidbody);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (!IsOwner)
            return;

        if (cameraControl != null)
            cameraControl.SetFollowTarget(null, null);
    }
}
