using FishNet.Object;
using UnityEngine;

public class MiniMapController : MonoBehaviour
{
    private enum PositionSource
    {
        Transform,
        Rigidbody
    }

    [Header("UI References")]
    public RectTransform mapImage;
    public RectTransform playerIcon;
    public Transform player;

    [Header("Runtime Player Binding")]
    public bool autoBindPlayer = true;
    public bool rebindWhenMissing = true;
    public bool rebindIfTrackedStatic = true;
    public int staticFramesBeforeRebind = 120;
    public bool preferAutoBoundLocalPlayer = true;
    public string playerTag = "Buddah";
    public int rebindEveryNFrames = 10;
    public bool localPlayerOnly = true;
    public bool followPlayerRoot;
    public bool broadSearchFallback = true;
    public string nameHint = string.Empty;
    [SerializeField] private PositionSource positionSource = PositionSource.Transform;

    [Header("World Bounds")]
    public float worldMinX = -653f;
    public float worldMaxX = 469f;
    public float worldMinZ = -1038f;
    public float worldMaxZ = 247f;

    [Header("Legacy Flags (kept for compatibility)")]
    public bool invertX;
    public bool invertY = true;
    public bool swapXY = true;
    public bool clampToBounds = true;

    [Header("Movement")]
    public bool useLocalPositionFallback;
    public bool detectExternalOverride = true;
    public float movementSensitivity = 1f;

    [Header("Force Test Movement")]
    public bool forceTestMovement;
    public float testAmplitude = 200f;
    public float testFrequency = 1.5f;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool debugLogEachFrame;
    public int debugLogEveryNFrames = 30;
    public bool drawGizmos = true;

    [SerializeField] private int debugFrame;
    [SerializeField] private int debugStaticFrameCount;
    [SerializeField] private string debugBindSource;
    [SerializeField] private string debugSkipReason;
    [SerializeField] private string debugTrackedPlayerName;
    [SerializeField] private Vector3 debugCurrentPlayerWorldPosition;
    [SerializeField] private Vector3 debugPreviousPlayerWorldPosition;
    [SerializeField] private Vector3 debugPlayerDelta;
    [SerializeField] private Vector2 debugCurrentNormalized;
    [SerializeField] private Vector2 debugCurrentUIOffset;
    [SerializeField] private Vector2 debugCurrentMapAnchoredPosition;
    [SerializeField] private Vector2 debugMapRectSize;

    private int _lastRebindFrame = -9999;
    private int _staticFrameCount;
    private Vector3 _prevPlayerPos;
    private bool _hasPrev;
    private Rigidbody _trackedRigidbody;

    private void OnEnable()
    {
        TryBindPlayer("OnEnable");
    }

    private void LateUpdate()
    {
        debugFrame = Time.frameCount;

        if (forceTestMovement)
        {
            RunForceTestMovement();
            return;
        }

        if (autoBindPlayer && ShouldTryRebind())
        {
            TryBindPlayer("LateUpdate");
        }

        if (mapImage == null)
        {
            debugSkipReason = "mapImage is null";
            return;
        }

        if (player == null)
        {
            debugSkipReason = "local player not found";
            return;
        }

        if (mapImage.rect.width <= 0.0001f || mapImage.rect.height <= 0.0001f)
        {
            debugSkipReason = "mapImage rect size is zero";
            return;
        }

        debugSkipReason = string.Empty;
        Vector3 worldPos = GetTrackedPosition();
        debugCurrentPlayerWorldPosition = worldPos;
        debugTrackedPlayerName = player.name;
        debugMapRectSize = mapImage.rect.size;

        float zSpan = SafeNonZero(worldMaxZ - worldMinZ);
        float xSpan = SafeNonZero(worldMaxX - worldMinX);

        // Single absolute mapping model:
        // u: left(worldMinZ) -> right(worldMaxZ)
        // v: bottom(worldMaxX) -> top(worldMinX)
        float u = (worldPos.z - worldMinZ) / zSpan;
        float v = (worldMaxX - worldPos.x) / xSpan;

        if (clampToBounds)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }

        float offsetX = (u - 0.5f) * mapImage.rect.width;
        float offsetY = (v - 0.5f) * mapImage.rect.height;

        Vector2 uiOffset = new Vector2(offsetX, offsetY) * Mathf.Max(0.0001f, movementSensitivity);

        debugCurrentNormalized = new Vector2(u, v);
        debugCurrentUIOffset = uiOffset;

        mapImage.anchoredPosition = -uiOffset;
        debugCurrentMapAnchoredPosition = mapImage.anchoredPosition;

        if (useLocalPositionFallback)
        {
            Vector3 local = mapImage.localPosition;
            local.x = mapImage.anchoredPosition.x;
            local.y = mapImage.anchoredPosition.y;
            mapImage.localPosition = local;
        }

        if (playerIcon != null)
            playerIcon.anchoredPosition = Vector2.zero;

        if (_hasPrev)
        {
            debugPlayerDelta = worldPos - _prevPlayerPos;
            bool moved = debugPlayerDelta.sqrMagnitude > 0.000001f;
            _staticFrameCount = moved ? 0 : _staticFrameCount + 1;
            debugStaticFrameCount = _staticFrameCount;

            if (rebindIfTrackedStatic && _staticFrameCount >= Mathf.Max(1, staticFramesBeforeRebind))
            {
                player = null;
                _trackedRigidbody = null;
                _staticFrameCount = 0;
                debugSkipReason = "Tracked player static. Rebind triggered.";
            }
        }
        else
        {
            debugPlayerDelta = Vector3.zero;
            _hasPrev = true;
        }

        debugPreviousPlayerWorldPosition = _prevPlayerPos;
        _prevPlayerPos = worldPos;

        MaybeLog($"player={debugTrackedPlayerName} uv={debugCurrentNormalized} uiOffset={debugCurrentUIOffset} map={debugCurrentMapAnchoredPosition}");
    }

    private void RunForceTestMovement()
    {
        if (mapImage == null)
        {
            debugSkipReason = "forceTestMovement enabled but mapImage is null";
            return;
        }

        float x = Mathf.Sin(Time.unscaledTime * testFrequency) * testAmplitude;
        mapImage.anchoredPosition = new Vector2(x, mapImage.anchoredPosition.y);
        debugCurrentMapAnchoredPosition = mapImage.anchoredPosition;
        debugSkipReason = string.Empty;

        if (playerIcon != null)
            playerIcon.anchoredPosition = Vector2.zero;
    }

    private bool ShouldTryRebind()
    {
        if (!rebindWhenMissing && player != null)
            return false;

        return Time.frameCount - _lastRebindFrame >= Mathf.Max(1, rebindEveryNFrames);
    }

    private void TryBindPlayer(string reason)
    {
        _lastRebindFrame = Time.frameCount;

        if (!preferAutoBoundLocalPlayer && player != null)
        {
            debugBindSource = "existing-reference";
            return;
        }

        Transform found = FindLocalOwner();
        if (found == null && !string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            found = tagged != null ? tagged.transform : null;
            if (found != null)
                debugBindSource = "tag-search";
        }

        if (found == null && player != null)
        {
            found = player;
            debugBindSource = "existing-fallback";
        }

        player = found;
        _trackedRigidbody = ResolveRigidbody(player);
        _hasPrev = false;

        if (player == null)
        {
            debugBindSource = "none";
            if (enableDebugLogs)
                Debug.LogWarning($"[MiniMapController] {reason}: local player not found", this);
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"[MiniMapController] {reason}: bound '{player.name}' via {debugBindSource}", this);
        }
    }

    private Transform FindLocalOwner()
    {
        BuddahMovement[] movers = FindObjectsByType<BuddahMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
        {
            if (movers[i] != null && movers[i].IsOwner)
            {
                debugBindSource = "local-buddah-movement";
                return movers[i].transform;
            }
        }

        PlayerCamera[] cameras = FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].IsOwner)
            {
                debugBindSource = "local-player-camera";
                Rigidbody rb = cameras[i].GetComponentInParent<Rigidbody>();
                return rb != null ? rb.transform : cameras[i].transform;
            }
        }

        if (!broadSearchFallback)
            return null;

        NetworkObject[] networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        for (int i = 0; i < networkObjects.Length; i++)
        {
            if (networkObjects[i] != null && networkObjects[i].IsOwner)
            {
                debugBindSource = "local-network-object";
                return networkObjects[i].transform;
            }
        }

        return null;
    }

    private static Rigidbody ResolveRigidbody(Transform target)
    {
        if (target == null)
            return null;

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
            return rb;

        return target.GetComponentInParent<Rigidbody>();
    }

    private Vector3 GetTrackedPosition()
    {
        if (positionSource == PositionSource.Rigidbody && _trackedRigidbody != null)
            return _trackedRigidbody.position;

        return player != null ? player.position : Vector3.zero;
    }

    private void MaybeLog(string message)
    {
        if (!enableDebugLogs)
            return;

        if (debugLogEachFrame)
        {
            Debug.Log($"[MiniMapController] {message}", this);
            return;
        }

        int interval = Mathf.Max(1, debugLogEveryNFrames);
        if (Time.frameCount % interval == 0)
            Debug.Log($"[MiniMapController] {message}", this);
    }

    private static float SafeNonZero(float value)
    {
        const float minAbs = 0.0001f;
        if (Mathf.Abs(value) < minAbs)
            return value >= 0f ? minAbs : -minAbs;

        return value;
    }
}
