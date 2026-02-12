using Cinemachine;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Rigidbody followTargetRigidbody;
    [SerializeField] private Vector2 offsetPerSpeed = new Vector2(0.2f, 0.15f);
    [SerializeField] private Vector2 maxExtraOffset = new Vector2(4f, 3f);

    public CinemachineVirtualCamera VirtualCamera => virtualCamera;
    private CinemachineTransposer transposer;
    private CinemachineFramingTransposer framingTransposer;
    private Vector3 baseFollowOffset;
    private Vector3 baseTrackedOffset;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        if (virtualCamera != null)
        {
            transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

            if (transposer != null)
            {
                baseFollowOffset = transposer.m_FollowOffset;
            }

            if (framingTransposer != null)
            {
                baseTrackedOffset = framingTransposer.m_TrackedObjectOffset;
            }
        }

        if (followTarget == null && virtualCamera != null)
        {
            followTarget = virtualCamera.Follow;
        }

        if (followTargetRigidbody == null && followTarget != null)
        {
            followTargetRigidbody = followTarget.GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        if (followTargetRigidbody == null)
        {
            return;
        }

        float speed = followTargetRigidbody.velocity.magnitude;
        float extraX = Mathf.Min(maxExtraOffset.x, speed * offsetPerSpeed.x);
        float extraY = Mathf.Min(maxExtraOffset.y, speed * offsetPerSpeed.y);

        if (transposer != null)
        {
            transposer.m_FollowOffset = new Vector3(
                ApplyMagnitude(baseFollowOffset.x, extraX),
                ApplyMagnitude(baseFollowOffset.y, extraY),
                baseFollowOffset.z);
        }
        else if (framingTransposer != null)
        {
            framingTransposer.m_TrackedObjectOffset = new Vector3(
                ApplyMagnitude(baseTrackedOffset.x, extraX),
                ApplyMagnitude(baseTrackedOffset.y, extraY),
                baseTrackedOffset.z);
        }
    }

    private static float ApplyMagnitude(float baseValue, float extra)
    {
        float sign = Mathf.Sign(baseValue);
        if (sign == 0f)
        {
            sign = 1f;
        }

        return sign * (Mathf.Abs(baseValue) + extra);
    }

    public void SetFieldOfView(float fov)
    {
        if (virtualCamera != null)
        {
            var lens = virtualCamera.m_Lens;
            lens.FieldOfView = fov;
            virtualCamera.m_Lens = lens;
        }
    }
}
