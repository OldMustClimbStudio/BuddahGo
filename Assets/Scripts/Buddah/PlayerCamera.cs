
using FishNet.Object;
using Cinemachine;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private CinemachineVirtualCamera localCamera;
    [SerializeField] private Rigidbody followTargetRigidbody;
    [SerializeField] private Vector3 directionalOffsetPerSpeed = new Vector3(0.25f, 0f, 0.25f);
    [SerializeField] private Vector3 maxDirectionalOffset = new Vector3(4f, 0f, 4f);
    [SerializeField] private float baseFieldOfView = 60f;
    [Header("Speed Zoom")]
    [SerializeField] private bool zoomBySpeed = true;
    [SerializeField] private float speedForMaxZoomOut = 10f;
    [SerializeField] private float minSpeedFieldOfView = 50f;
    [SerializeField] private float maxSpeedFieldOfView = 72f;
    [SerializeField] private float maxSpeedDistanceOffset = 12f;
    [SerializeField] private float speedZoomSmoothTime = 0.2f;
    [Header("Scale Adaptation")]
    [SerializeField] private bool adaptToPlayerScale = true;
    [SerializeField] private float cameraDistanceScaleFactor = 1f;
    [SerializeField] private float cameraFovPerExtraScale = 0f;

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
    private float _speedFieldOfView;
    private float _speedFovVelocity;
    private float _baseFramingCameraDistance;
    private float _speedDistanceOffset;
    private float _speedDistanceVelocity;
    private PlayerScaleEffect _scaleEffect;
    private bool _missingScaleEffectLogged;

    private void Awake()
    {
        ResolveCameraReferences();
        EnsurePerspectiveCamera();

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponentInParent<Rigidbody>();

        _speedFieldOfView = baseFieldOfView;
    }

    // This method is called on the client after the object is spawned in.
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only setup camera logic if this is the owner
        if (!IsOwner)
            return;

        // Resolve local camera references and force-follow owner player transform.
        if (_cinemachineCamera == null)
            ResolveCameraReferences();

        if (_cinemachineCamera == null)
            return;

        EnsurePerspectiveCamera();
        _cinemachineCamera.Follow = transform;
        _cinemachineCamera.enabled = true;

        if (followTargetRigidbody == null)
            followTargetRigidbody = GetComponentInParent<Rigidbody>();

        Debug.Log("PlayerCamera: Initialized for local player " + transform.name);
    }

    private void LateUpdate()
    {
        if (!IsOwner || followTargetRigidbody == null)
            return;

        Vector3 absoluteVelocity = followTargetRigidbody.velocity;
        float absoluteSpeed = absoluteVelocity.magnitude;

        Vector3 planarVelocity = absoluteVelocity;
        planarVelocity.y = 0f;
        float planarSpeed = planarVelocity.magnitude;
        Vector3 planarDir = planarSpeed > 0.0001f ? (planarVelocity / planarSpeed) : Vector3.zero;
        
        Vector3 targetOffset = Vector3.Scale(planarDir * planarSpeed, directionalOffsetPerSpeed);
        targetOffset = new Vector3(
            Mathf.Clamp(targetOffset.x, -maxDirectionalOffset.x, maxDirectionalOffset.x),
            Mathf.Clamp(targetOffset.y, -maxDirectionalOffset.y, maxDirectionalOffset.y),
            Mathf.Clamp(targetOffset.z, -maxDirectionalOffset.z, maxDirectionalOffset.z));

        float smoothTime = Mathf.Max(0.001f, offsetSmoothTime);
        float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
        _directionalOffset = Vector3.Lerp(_directionalOffset, targetOffset, lerpFactor);

        float currentScaleMultiplier = GetCameraScaleMultiplier();
        Debug.Log($"[Camera] scale={currentScaleMultiplier}");
        Vector3 scaleCompensationOffset = Vector3.zero;
        Vector3 scaleCompensationTrackedOffset = Vector3.zero;
        float scaleFovOffset = 0f;

        if (adaptToPlayerScale && !Mathf.Approximately(currentScaleMultiplier, 1f))
        {
            float extraScale = (currentScaleMultiplier - 1f) * cameraDistanceScaleFactor;
            scaleCompensationOffset = baseFollowOffset * extraScale;
            scaleCompensationTrackedOffset = baseTrackedOffset * extraScale;
            scaleFovOffset = (currentScaleMultiplier - 1f) * cameraFovPerExtraScale;
        }

        UpdateSpeedZoom(absoluteSpeed);

        if (transposer != null)
        {
            Vector3 zoomDistanceOffset = GetSpeedDistanceVector();
            transposer.m_FollowOffset = baseFollowOffset + scaleCompensationOffset + zoomDistanceOffset + _directionalOffset + _runtimeOffset;
        }
        else if (framingTransposer != null)
        {
            framingTransposer.m_TrackedObjectOffset = baseTrackedOffset + scaleCompensationTrackedOffset + _directionalOffset + _runtimeOffset;
            framingTransposer.m_CameraDistance = Mathf.Max(0.01f, _baseFramingCameraDistance + _speedDistanceOffset);
        }

        ApplyLensZoom(scaleFovOffset);
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
        _speedFieldOfView = baseFieldOfView;
        _speedDistanceOffset = 0f;
        _runtimeOffsetDampVelocity = Vector3.zero;
        _runtimeFovVelocity = 0f;
        _speedFovVelocity = 0f;
        _speedDistanceVelocity = 0f;
    }

    public void SetLocalCamera(CinemachineVirtualCamera camera)
    {
        if (!IsOwner)
            return;

        localCamera = camera;
        _cinemachineCamera = camera;
        ResolveCameraReferences();

        if (_cinemachineCamera != null)
        {
            _cinemachineCamera.Follow = transform;
            _cinemachineCamera.enabled = true;
        }
    }

    private void ResolveCameraReferences()
    {
        if (_cinemachineCamera == null)
            _cinemachineCamera = localCamera != null ? localCamera : GetComponent<CinemachineVirtualCamera>();

        if (_cinemachineCamera == null)
            return;

        transposer = _cinemachineCamera.GetCinemachineComponent<CinemachineTransposer>();
        framingTransposer = _cinemachineCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (transposer != null)
            baseFollowOffset = transposer.m_FollowOffset;

        if (framingTransposer != null)
        {
            baseTrackedOffset = framingTransposer.m_TrackedObjectOffset;
            _baseFramingCameraDistance = framingTransposer.m_CameraDistance;
        }

        baseFieldOfView = _cinemachineCamera.m_Lens.FieldOfView;

        if (followTargetRigidbody == null && _cinemachineCamera.Follow != null)
            followTargetRigidbody = _cinemachineCamera.Follow.GetComponent<Rigidbody>();
    }

    private float GetCameraScaleMultiplier()
    {
        if (_scaleEffect == null)
            _scaleEffect = GetComponent<PlayerScaleEffect>();

        if (_scaleEffect == null)
            _scaleEffect = GetComponentInParent<PlayerScaleEffect>();

        if (_scaleEffect == null)
            _scaleEffect = GetComponentInChildren<PlayerScaleEffect>(true);

        if (_scaleEffect == null)
        {
            if (!_missingScaleEffectLogged)
            {
                Debug.LogWarning("[Camera] PlayerScaleEffect not found yet; fallback scale=1.");
                _missingScaleEffectLogged = true;
            }

            return 1f;
        }

        _missingScaleEffectLogged = false;

        return Mathf.Max(0.1f, _scaleEffect.CurrentScaleMultiplier);
    }

    private void UpdateSpeedZoom(float speed)
    {
        if (!zoomBySpeed)
        {
            _speedFieldOfView = baseFieldOfView;
            _speedDistanceOffset = 0f;
            _speedFovVelocity = 0f;
            _speedDistanceVelocity = 0f;
            return;
        }

        float normalizedSpeed = speedForMaxZoomOut > 0.001f
            ? Mathf.Clamp01(speed / speedForMaxZoomOut)
            : 0f;

        float targetFieldOfView = Mathf.Lerp(minSpeedFieldOfView, maxSpeedFieldOfView, normalizedSpeed);
        float targetDistanceOffset = normalizedSpeed * maxSpeedDistanceOffset;
        float safeSmoothTime = Mathf.Max(0.001f, speedZoomSmoothTime);
        _speedFieldOfView = Mathf.SmoothDamp(_speedFieldOfView, targetFieldOfView, ref _speedFovVelocity, safeSmoothTime);
        _speedDistanceOffset = Mathf.SmoothDamp(_speedDistanceOffset, targetDistanceOffset, ref _speedDistanceVelocity, safeSmoothTime);
    }

    private void ApplyLensZoom(float scaleFovOffset)
    {
        if (_cinemachineCamera == null)
            return;

        float targetFieldOfView = zoomBySpeed ? _speedFieldOfView : baseFieldOfView;
        SetFieldOfView(targetFieldOfView + scaleFovOffset + _runtimeFovOffset);
    }

    private Vector3 GetSpeedDistanceVector()
    {
        if (Mathf.Approximately(_speedDistanceOffset, 0f))
            return Vector3.zero;

        Vector3 zoomDirection = baseFollowOffset.sqrMagnitude > 0.0001f
            ? baseFollowOffset.normalized
            : Vector3.back;

        return zoomDirection * _speedDistanceOffset;
    }

    private void EnsurePerspectiveCamera()
    {
        if (_cinemachineCamera == null)
            return;

        var lens = _cinemachineCamera.m_Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Perspective;
        _cinemachineCamera.m_Lens = lens;

        if (Camera.main != null && Camera.main.orthographic)
            Camera.main.orthographic = false;
    }
}
