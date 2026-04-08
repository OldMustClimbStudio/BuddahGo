using UnityEngine;

public class MiniMapPresenter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] protected RectTransform mapImage;
    [SerializeField] protected RectTransform playerIcon;

    [Header("Player")]
    [SerializeField] protected MiniMapPlayerLocator playerLocator;

    [Header("Absolute Mapping Inputs")]
    [SerializeField] protected float leftWorldZ = -1038f;
    [SerializeField] protected float rightWorldZ = 247f;
    [SerializeField] protected float bottomWorldX = 469f;
    [SerializeField] protected float topWorldX = -653.1f;

    [Header("Debug")]
    [SerializeField] protected Vector3 debugPlayerWorldPosition;
    [SerializeField] protected Vector2 debugNormalizedUV;
    [SerializeField] protected Vector2 debugUiOffset;
    [SerializeField] protected Vector2 debugMapAnchoredPosition;
    [SerializeField] protected string debugSkipReason;

    private MiniMapWorldMapper _mapper;

    protected virtual void Awake()
    {
        TryResolveLocator();
        BuildMapper();
    }

    protected virtual void OnValidate()
    {
        BuildMapper();
    }

    protected virtual void LateUpdate()
    {
        if (mapImage == null)
        {
            debugSkipReason = "mapImage is null";
            return;
        }

        if (mapImage.rect.width <= 0.0001f || mapImage.rect.height <= 0.0001f)
        {
            debugSkipReason = "mapImage rect size is zero";
            return;
        }

        if (_mapper == null)
            BuildMapper();

        TryResolveLocator();
        Transform player = playerLocator != null ? playerLocator.CurrentPlayer : null;
        if (player == null)
        {
            debugSkipReason = "local player not found";
            return;
        }

        debugSkipReason = string.Empty;
        debugPlayerWorldPosition = player.position;

        var uv = _mapper.WorldToUV(player.position.x, player.position.z);
        var offset = _mapper.UVToCenteredOffset(uv.u, uv.v);

        debugNormalizedUV = new Vector2(uv.u, uv.v);
        debugUiOffset = new Vector2(offset.offsetX, offset.offsetY);

        mapImage.anchoredPosition = -debugUiOffset;
        debugMapAnchoredPosition = mapImage.anchoredPosition;

        if (playerIcon != null)
            playerIcon.anchoredPosition = Vector2.zero;
    }

    private void TryResolveLocator()
    {
        if (playerLocator == null)
            playerLocator = FindFirstObjectByType<MiniMapPlayerLocator>(FindObjectsInactive.Exclude);
    }

    private void BuildMapper()
    {
        if (mapImage == null)
            return;

        _mapper = new MiniMapWorldMapper(
            leftWorldZ,
            rightWorldZ,
            bottomWorldX,
            topWorldX,
            mapImage.rect.width,
            mapImage.rect.height
        );
    }

    [ContextMenu("Rebuild Mapper")]
    public void RebuildMapperFromInspector()
    {
        BuildMapper();
    }
}
