using FishNet.Object;
using UnityEngine;

public class MiniMapPlayerLocator : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private int searchEveryNFrames = 10;
    [SerializeField] private bool enableLogs;

    [Header("Debug")]
    [SerializeField] private string debugCurrentPlayerName;

    private int _lastSearchFrame = -9999;

    public Transform CurrentPlayer { get; private set; }

    private void Update()
    {
        if (CurrentPlayer == null || Time.frameCount - _lastSearchFrame >= Mathf.Max(1, searchEveryNFrames))
        {
            TryResolveLocalPlayer();
        }

        debugCurrentPlayerName = CurrentPlayer != null ? CurrentPlayer.name : "(null)";
    }

    [ContextMenu("Resolve Local Player Now")]
    public void TryResolveLocalPlayer()
    {
        _lastSearchFrame = Time.frameCount;

        Transform found = FindByBuddahMovementOwner();
        if (found == null)
            found = FindByPlayerCameraOwner();
        if (found == null)
            found = FindByNetworkObjectOwner();

        if (found != CurrentPlayer)
        {
            CurrentPlayer = found;
            if (enableLogs)
            {
                Debug.Log($"[MiniMapPlayerLocator] Local player => {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);
            }
        }
    }

    private static Transform FindByBuddahMovementOwner()
    {
        BuddahMovement[] all = FindObjectsByType<BuddahMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsOwner)
                return all[i].transform;
        }

        return null;
    }

    private static Transform FindByPlayerCameraOwner()
    {
        PlayerCamera[] all = FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            PlayerCamera playerCamera = all[i];
            if (playerCamera == null || !playerCamera.IsOwner)
                continue;

            Rigidbody rb = playerCamera.GetComponentInParent<Rigidbody>();
            return rb != null ? rb.transform : playerCamera.transform;
        }

        return null;
    }

    private static Transform FindByNetworkObjectOwner()
    {
        NetworkObject[] all = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsOwner)
                return all[i].transform;
        }

        return null;
    }
}
