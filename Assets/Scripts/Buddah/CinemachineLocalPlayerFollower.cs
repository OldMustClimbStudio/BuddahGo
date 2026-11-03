using Cinemachine;
using FishNet.Object;
using NewBuddah.PredictionV2.Integration;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

public class CinemachineLocalPlayerFollower : MonoBehaviour
{
    [SerializeField] private RaceSpectatorTargetResolver spectatorTargetResolver;
    private CinemachineVirtualCamera _virtualCamera;
    private PlayerCamera _currentLocalPlayer;

    private void Awake()
    {
        _virtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (spectatorTargetResolver == null)
            spectatorTargetResolver = GetComponent<RaceSpectatorTargetResolver>();
        if (spectatorTargetResolver == null)
            spectatorTargetResolver = gameObject.AddComponent<RaceSpectatorTargetResolver>();

        if (_virtualCamera == null)
        {
            Debug.LogError("CinemachineLocalPlayerFollower must be on a GameObject with CinemachineVirtualCamera!");
        }
    }

    private void LateUpdate()
    {
        if (_virtualCamera == null)
            return;

        ResultPresentationTimelineBridge timelineBridge = ResultPresentationTimelineBridge.Instance;
        if (timelineBridge != null && timelineBridge.IsPresentationCameraLocked)
        {
            if (_currentLocalPlayer != null)
            {
                _currentLocalPlayer.ClearFollowTargetOverride();
                _currentLocalPlayer.SetPresentationMode(PlayerCamera.CameraPresentationMode.TimelineCamera);
                _currentLocalPlayer.GetComponent<BuddahPredictionCameraBridge>()?.ReportFollowTarget(
                    PlayerCamera.CameraPresentationMode.TimelineCamera,
                    null,
                    null,
                    "CinemachineFollower.TimelineLock");
            }

            _virtualCamera.enabled = false;
            return;
        }

        // Find the local player (the one that is the owner on this client)
        PlayerCamera localPlayer = FindLocalPlayer();
        PlayerCamera previousLocalPlayer = _currentLocalPlayer;

        if (previousLocalPlayer != null && previousLocalPlayer != localPlayer)
            previousLocalPlayer.ClearFollowTargetOverride();

        _currentLocalPlayer = localPlayer;

        if (localPlayer != null)
        {
            if (spectatorTargetResolver != null
                && spectatorTargetResolver.TryResolveSpectatorTarget(localPlayer, out PlayerCamera spectatorTarget))
            {
                localPlayer.SetFollowTargetOverride(
                    spectatorTarget.transform,
                    FindBody(spectatorTarget));
                localPlayer.SetPresentationMode(PlayerCamera.CameraPresentationMode.Spectator);
                localPlayer.GetComponent<BuddahPredictionCameraBridge>()?.ReportFollowTarget(
                    PlayerCamera.CameraPresentationMode.Spectator,
                    spectatorTarget.transform,
                    FindBody(spectatorTarget),
                    "CinemachineFollower.Spectator");
            }
            else
            {
                localPlayer.ClearFollowTargetOverride();
                RoomStateManager room = RoomStateManager.Instance;
                localPlayer.SetPresentationMode(room != null && room.IsResultPhaseActive
                    ? PlayerCamera.CameraPresentationMode.ResultArea
                    : PlayerCamera.CameraPresentationMode.Normal);
                localPlayer.GetComponent<BuddahPredictionCameraBridge>()?.ReportFollowTarget(
                    room != null && room.IsResultPhaseActive
                        ? PlayerCamera.CameraPresentationMode.ResultArea
                        : PlayerCamera.CameraPresentationMode.Normal,
                    localPlayer.transform,
                    FindBody(localPlayer),
                    "CinemachineFollower.NormalOrResult");
            }

            if (previousLocalPlayer != localPlayer || !_virtualCamera.enabled || _virtualCamera.Follow == null)
                localPlayer.SetLocalCamera(_virtualCamera);

            _virtualCamera.enabled = true;
        }
        else if (localPlayer == null && _virtualCamera.Follow != null)
        {
            // No local player found, disable the camera
            if (previousLocalPlayer != null)
            {
                previousLocalPlayer.ClearFollowTargetOverride();
                previousLocalPlayer.SetPresentationMode(PlayerCamera.CameraPresentationMode.Normal);
                previousLocalPlayer.GetComponent<BuddahPredictionCameraBridge>()?.ReportFollowTarget(
                    PlayerCamera.CameraPresentationMode.Normal,
                    null,
                    null,
                    "CinemachineFollower.Release");
            }

            _virtualCamera.Follow = null;
            _virtualCamera.enabled = false;
        }
    }

    // Unity objects must not go through ??: in the editor a missing GetComponent result is a fake null that ?? keeps.
    private static Rigidbody FindBody(Component target)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        return body != null ? body : target.GetComponentInParent<Rigidbody>();
    }

    private PlayerCamera FindLocalPlayer()
    {
        // The owner's camera stays the same for the whole race: search the scene again only once it is gone.
        if (_currentLocalPlayer != null && _currentLocalPlayer.gameObject.activeInHierarchy && _currentLocalPlayer.IsOwner)
            return _currentLocalPlayer;

        PlayerCamera[] allPlayers = FindObjectsOfType<PlayerCamera>();

        foreach (PlayerCamera player in allPlayers)
        {
            if (player.IsOwner)
                return player;
        }

        return null;
    }
}
