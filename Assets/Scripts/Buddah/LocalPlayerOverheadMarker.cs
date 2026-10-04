using FishNet.Object;
using NewBuddah.PredictionV2.Visual;
using UnityEngine;

/// <summary>Placeholder presentation: identifies this client's own racer during the intro, until GO.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class LocalPlayerOverheadMarker : MonoBehaviour
{
    [SerializeField] private Transform markerVisual;
    [SerializeField, Min(0f)] private float heightAboveVisualRoot = 8.5f;
    [SerializeField, Min(1f)] private float targetPixelHeight = 32f;
    [SerializeField, Min(0.1f)] private float minimumScale = 1.25f;
    [SerializeField, Min(0.1f)] private float maximumScale = 8f;

    // The authored arrow is two units tall, with its tip at local Y=0.
    private const float ArrowHeight = 2f;
    private NetworkObject _networkObject;
    private BuddahPredictionVisualRootBridge _visualBridge;
    private RaceBodyIntroStateController _intro;
    private Camera _camera;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _visualBridge = GetComponent<BuddahPredictionVisualRootBridge>();
        _intro = GetComponent<RaceBodyIntroStateController>();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (markerVisual == null)
            return;

        bool visible = _networkObject != null && _networkObject.IsClientInitialized && _networkObject.IsOwner
            && _intro != null && _intro.IsIntroActive && !_intro.IsGoApplied;
        if (!visible)
        {
            SetVisible(false);
            return;
        }

        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main;
        if (_camera == null)
        {
            SetVisible(false);
            return;
        }

        Transform anchor = _visualBridge != null ? _visualBridge.GetVisualRoot() : transform;
        Vector3 position = anchor.position + Vector3.up * heightAboveVisualRoot;
        float depth = Vector3.Dot(position - _camera.transform.position, _camera.transform.forward);
        if (depth <= _camera.nearClipPlane)
        {
            SetVisible(false);
            return;
        }

        float viewHeight = _camera.orthographic ? 2f * _camera.orthographicSize
            : 2f * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float scale = Mathf.Clamp(viewHeight * targetPixelHeight / Mathf.Max(1, _camera.pixelHeight) / ArrowHeight,
            minimumScale, Mathf.Max(minimumScale, maximumScale));

        // Follow the rendered actor after the prediction bridge and face the final camera.
        // The marker is a separate root child, so the model's scale/roll do not distort it.
        markerVisual.SetPositionAndRotation(position, _camera.transform.rotation);
        markerVisual.localScale = Vector3.one * scale;
        SetVisible(true);
    }

    private void OnDisable() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (markerVisual != null && markerVisual.gameObject.activeSelf != visible)
            markerVisual.gameObject.SetActive(visible);
    }
}
