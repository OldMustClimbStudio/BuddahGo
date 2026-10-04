using BuddahGo.Match;
using FishNet.Object;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

public class RaceSpectatorTargetResolver : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs;

    public bool TryResolveSpectatorTarget(PlayerCamera localPlayer, out PlayerCamera target)
    {
        target = null;
        if (localPlayer == null)
            return false;

        RoomStateManager roomStateManager = RoomStateManager.Instance;
        MatchResultPresentationCoordinator coordinator = MatchResultPresentationCoordinator.Instance;
        if (roomStateManager == null
            || !roomStateManager.IsMatchPhaseActive
            || !roomStateManager.IsRaceStarted
            || (coordinator != null && coordinator.IsGlobalResultPresentationActive))
        {
            return false;
        }

        RaceFinishManager finishManager = RaceFinishManager.Instance;
        if (finishManager == null || finishManager.IsRaceForceEnded)
            return false;

        RaceCompletionTracker localCompletion = localPlayer.GetComponent<RaceCompletionTracker>();
        if (localCompletion == null)
            localCompletion = localPlayer.GetComponentInParent<RaceCompletionTracker>();
        if (localCompletion == null)
            localCompletion = localPlayer.GetComponentInChildren<RaceCompletionTracker>(true);
        if (localCompletion == null || !localCompletion.IsFinished)
            return false;

        if (LeaderboardManager.Instance == null || LeaderboardManager.Instance.Rankings.Count == 0)
            return false;

        PlayerCamera[] allCameras = FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < LeaderboardManager.Instance.Rankings.Count; i++)
        {
            RankEntry entry = LeaderboardManager.Instance.Rankings[i];
            if (RacerAuthority.Matches(localPlayer, entry.RacerId) || entry.IsFinished)
                continue;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                PlayerCamera candidate = allCameras[cameraIndex];
                if (candidate == null || !candidate.IsSpawned || !RacerAuthority.Matches(candidate, entry.RacerId))
                    continue;

                target = candidate;
                DebugLog($"Resolved spectator target clientId={entry.RacerId} rankIndex={i}");
                return true;
            }
        }

        return false;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            GameLog.Verbose($"[RaceSpectatorTargetResolver] {message}");
    }
}
