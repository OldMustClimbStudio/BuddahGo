
using FishNet.Object;
using Cinemachine;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Rigidbody followTargetRigidbody;
    [SerializeField] private Vector3 directionalOffsetPerSpeed = new Vector3(0.25f, 0f, 0.25f);
    [SerializeField] private Vector3 maxDirectionalOffset = new Vector3(4f, 0f, 4f);

    private CinemachineVirtualCamera _cinemachineCamera;
    private CinemachineTransposer transposer;
    private CinemachineFramingTransposer framingTransposer;
    private Vector3 baseFollowOffset;
    private Vector3 baseTrackedOffset;
    private Vector3 currentOffset = Vector3.zero;
    [SerializeField] private float offsetSmoothTime = 0.1f;

    private void Awake()
    {
        if (_cinemachineCamera == null)
            _cinemachineCamera = GetComponent<CinemachineVirtualCamera>();

        if (_cinemachineCamera == null)
            _cinemachineCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (_cinemachineCamera != null)
        {
            transposer = _cinemachineCamera.GetCinemachineComponent<CinemachineTransposer>();
            framingTransposer = _cinemachineCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

            if (transposer != null)
                baseFollowOffset = transposer.m_FollowOffset;

            if (framingTransposer != null)
                baseTrackedOffset = framingTransposer.m_TrackedObjectOffset;

            if (followTargetRigidbody == null && _cinemachineCamera.Follow != null)
                followTargetRigidbody = _cinemachineCamera.Follow.GetComponent<Rigidbody>();
        }

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponentInParent<Rigidbody>();
    }

    // This method is called on the client after the object is spawned in.
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only setup camera logic if this is the owner
        if (!IsOwner)
            return;

        // Get the virtual camera - the external script will handle following
        if (_cinemachineCamera == null)
            _cinemachineCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (_cinemachineCamera == null)
            return;

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponentInParent<Rigidbody>();

        // Cache Transposer/FramingTransposer for offset calculations
        if (_cinemachineCamera != null)
        {
            transposer = _cinemachineCamera.GetCinemachineComponent<CinemachineTransposer>();
            framingTransposer = _cinemachineCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

            if (transposer != null)
                baseFollowOffset = transposer.m_FollowOffset;

            if (framingTransposer != null)
                baseTrackedOffset = framingTransposer.m_TrackedObjectOffset;
        }

        Debug.Log("PlayerCamera: Initialized for local player " + transform.name);
    }

    private void LateUpdate()
    {
        if (!IsOwner || followTargetRigidbody == null)
            return;

        Vector3 planarVelocity = followTargetRigidbody.velocity;
        planarVelocity.y = 0f;
        float speed = planarVelocity.magnitude;
        Vector3 planarDir = speed > 0.0001f ? (planarVelocity / speed) : Vector3.zero;
        
        Vector3 targetOffset = Vector3.Scale(planarDir * speed, directionalOffsetPerSpeed);
        targetOffset = new Vector3(
            Mathf.Clamp(targetOffset.x, -maxDirectionalOffset.x, maxDirectionalOffset.x),
            Mathf.Clamp(targetOffset.y, -maxDirectionalOffset.y, maxDirectionalOffset.y),
            Mathf.Clamp(targetOffset.z, -maxDirectionalOffset.z, maxDirectionalOffset.z));

        // Smoothly transition to target offset
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.deltaTime / offsetSmoothTime);

        if (transposer != null)
        {
            transposer.m_FollowOffset = baseFollowOffset + currentOffset;
        }
        else if (framingTransposer != null)
        {
            framingTransposer.m_TrackedObjectOffset = baseTrackedOffset + currentOffset;
        }
    }

    public void SetFieldOfView(float fov)
    {
        if (_cinemachineCamera == null)
            return;

        var lens = _cinemachineCamera.m_Lens;
        lens.FieldOfView = fov;
        _cinemachineCamera.m_Lens = lens;
    }
}