using Cinemachine;
using FishNet.Object;
using UnityEngine;

public class CinemachineLocalPlayerFollower : MonoBehaviour
{
    private CinemachineVirtualCamera _virtualCamera;
    private PlayerCamera _currentLocalPlayer;

    private void Awake()
    {
        _virtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (_virtualCamera == null)
        {
            Debug.LogError("CinemachineLocalPlayerFollower must be on a GameObject with CinemachineVirtualCamera!");
        }
    }

    private void LateUpdate()
    {
        if (_virtualCamera == null)
            return;

        // Find the local player (the one that is the owner on this client)
        PlayerCamera localPlayer = FindLocalPlayer();

        if (localPlayer != null && _virtualCamera.Follow != localPlayer.transform)
        {
            _virtualCamera.Follow = localPlayer.transform;
            _virtualCamera.enabled = true;
        }
        else if (localPlayer == null && _virtualCamera.Follow != null)
        {
            // No local player found, disable the camera
            _virtualCamera.Follow = null;
            _virtualCamera.enabled = false;
        }
    }

    private PlayerCamera FindLocalPlayer()
    {
        PlayerCamera[] allPlayers = FindObjectsOfType<PlayerCamera>();

        foreach (PlayerCamera player in allPlayers)
        {
            if (player.IsOwner)
                return player;
        }

        return null;
    }
}
