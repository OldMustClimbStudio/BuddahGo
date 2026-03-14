
using FishNet.Object;
using Cinemachine;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Rigidbody followTargetRigidbody;
    [SerializeField] private Vector3 directionalOffsetPerSpeed = new Vector3(0.25f, 0f, 0.25f);
    [SerializeField] private Vector3 maxDirectionalOffset = new Vector3(4f, 0f, 4f);
    [SerializeField] private float baseFieldOfView = 60f;

    private CinemachineVirtualCamera _cinemachineCamera;
    private CinemachineTransposer transposer;
    private CinemachineFramingTransposer framingTransposer;
    private Vector3 baseFollowOffset;
    private Vector3 baseTrackedOffset;
    [SerializeField] private float offsetSmoothTime = 0.1f;
    private Vector3 _directionalOffset = Vector3.zero;
    private Vector3 _runtimeOffset = Vector3.zero;
    private float _runtimeFovOffset;
    private Vector3 _runtimeOffsetDampVelocity = Vector3.zero;
    private float _runtimeFovVelocity;

    private void Awake()
    {
        ResolveCameraReferences();

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
            ResolveCameraReferences();

        if (_cinemachineCamera == null)
            return;

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponentInParent<Rigidbody>();

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

        float smoothTime = Mathf.Max(0.001f, offsetSmoothTime);
        float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
        _directionalOffset = Vector3.Lerp(_directionalOffset, targetOffset, lerpFactor);

        if (transposer != null)
        {
            transposer.m_FollowOffset = baseFollowOffset + _directionalOffset + _runtimeOffset;
        }
        else if (framingTransposer != null)
        {
            framingTransposer.m_TrackedObjectOffset = baseTrackedOffset + _directionalOffset + _runtimeOffset;
        }

        SetFieldOfView(baseFieldOfView + _runtimeFovOffset);
    }

    public void SetFieldOfView(float fov)
    {
        if (_cinemachineCamera == null)
            return;

        var lens = _cinemachineCamera.m_Lens;
        lens.FieldOfView = fov;
        _cinemachineCamera.m_Lens = lens;
    }

    public void SetRuntimeOffset(Vector3 offset)
    {
        _runtimeOffset = offset;
    }

    public void SetRuntimeFieldOfViewOffset(float fovOffset)
    {
        _runtimeFovOffset = fovOffset;
    }

    public void DampenRuntimeOffset(Vector3 targetOffset, float smoothTime)
    {
        float safeSmoothTime = Mathf.Max(0.001f, smoothTime);
        _runtimeOffset = Vector3.SmoothDamp(_runtimeOffset, targetOffset, ref _runtimeOffsetDampVelocity, safeSmoothTime);
    }

    public void DampenRuntimeFieldOfViewOffset(float targetOffset, float smoothTime)
    {
        float safeSmoothTime = Mathf.Max(0.001f, smoothTime);
        _runtimeFovOffset = Mathf.SmoothDamp(_runtimeFovOffset, targetOffset, ref _runtimeFovVelocity, safeSmoothTime);
    }

    public void ResetRuntimeEffects()
    {
        _runtimeOffset = Vector3.zero;
        _runtimeFovOffset = 0f;
        _runtimeOffsetDampVelocity = Vector3.zero;
        _runtimeFovVelocity = 0f;
    }

    private void ResolveCameraReferences()
    {
        if (_cinemachineCamera == null)
            _cinemachineCamera = GetComponent<CinemachineVirtualCamera>();

        if (_cinemachineCamera == null)
            _cinemachineCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (_cinemachineCamera == null)
            return;

        transposer = _cinemachineCamera.GetCinemachineComponent<CinemachineTransposer>();
        framingTransposer = _cinemachineCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (transposer != null)
            baseFollowOffset = transposer.m_FollowOffset;

        if (framingTransposer != null)
            baseTrackedOffset = framingTransposer.m_TrackedObjectOffset;

        baseFieldOfView = _cinemachineCamera.m_Lens.FieldOfView;

        if (followTargetRigidbody == null && _cinemachineCamera.Follow != null)
            followTargetRigidbody = _cinemachineCamera.Follow.GetComponent<Rigidbody>();
    }
}
