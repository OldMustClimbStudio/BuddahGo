using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using Scene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace SteamMultiplayer.Network
{
    internal static class RoomDiagnostics
    {
        internal static void LogClientRaceSceneDiagnostics(RoomStateManager owner, string stageLabel)
        {
            GameLog.Verbose(
                $"[SceneDiag][Client] Race probe {stageLabel} loaded={UnitySceneManager.GetSceneByName(owner.DiagnosticRaceSceneName).isLoaded} " +
                $"matchPhase={owner.CurrentMatchSessionPhase} raceStarted={owner.IsRaceStarted} clientId={owner.GetLocalClientId()}");
            LogSceneObjectProbe("IntroSequenceManager", UnityEngine.Object.FindFirstObjectByType<IntroSequenceManager>(FindObjectsInactive.Include));
            LogSceneObjectProbe("LeaderboardManager", UnityEngine.Object.FindFirstObjectByType<LeaderboardManager>(FindObjectsInactive.Include));
            LogSceneObjectProbe("RaceFinishManager", UnityEngine.Object.FindFirstObjectByType<RaceFinishManager>(FindObjectsInactive.Include));
            LogSceneObjectProbe("ResultDecisionManager", UnityEngine.Object.FindFirstObjectByType<ResultDecisionManager>(FindObjectsInactive.Include));
            LogSceneObjectProbe("MatchResultPresentationCoordinator", UnityEngine.Object.FindFirstObjectByType<MatchResultPresentationCoordinator>(FindObjectsInactive.Include));
            LogSceneObjectProbe("RaceResultAreaManager", UnityEngine.Object.FindFirstObjectByType<RaceResultAreaManager>(FindObjectsInactive.Include));
            GameLog.Verbose($"[SceneDiag][Client] Local readiness summary {BuildLocalRaceSceneStatusReport(owner)}");
        }

        internal static void LogSceneObjectProbe(string label, NetworkBehaviour behaviour)
        {
            if (behaviour == null)
            {
                GameLog.Verbose($"[SceneDiag][Client] {label}: missing");
                return;
            }

            NetworkObject nob = behaviour.NetworkObject;
            bool isSpawned = nob != null && nob.IsSpawned;
            bool isSceneObject = nob != null && nob.IsSceneObject;
            string sceneName = behaviour.gameObject.scene.name;
            GameLog.Verbose($"[SceneDiag][Client] {label}: present scene='{sceneName}' spawned={isSpawned} isSceneObject={isSceneObject} active={behaviour.gameObject.activeInHierarchy}");
        }

        internal static string BuildLocalRaceSceneStatusReport(RoomStateManager owner)
        {
            int localClientId = owner.GetLocalClientId();
            bool raceSceneLoaded = !string.IsNullOrWhiteSpace(owner.DiagnosticRaceSceneName) && UnitySceneManager.GetSceneByName(owner.DiagnosticRaceSceneName).isLoaded;
            bool introSequenceReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<IntroSequenceManager>(FindObjectsInactive.Include));
            bool leaderboardReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<LeaderboardManager>(FindObjectsInactive.Include));
            bool raceFinishReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<RaceFinishManager>(FindObjectsInactive.Include));
            bool resultDecisionReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<ResultDecisionManager>(FindObjectsInactive.Include));
            bool resultPresentationReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<MatchResultPresentationCoordinator>(FindObjectsInactive.Include));
            bool resultAreaReady = RoomStateManager.IsSceneObjectReady(UnityEngine.Object.FindFirstObjectByType<RaceResultAreaManager>(FindObjectsInactive.Include));
            bool localPlayerListed = owner.TryGetLocalPlayer(out RoomPlayerState localPlayer);
            bool localRaceBodyReady = false;
            int localRaceBodyObjectId = -1;

            RaceBodyIntroStateController[] introBodies = UnityEngine.Object.FindObjectsByType<RaceBodyIntroStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < introBodies.Length; i++)
            {
                RaceBodyIntroStateController body = introBodies[i];
                if (body == null || body.OwnerId != localClientId)
                    continue;

                localRaceBodyReady = body.NetworkObject != null && body.NetworkObject.IsSpawned;
                localRaceBodyObjectId = body.NetworkObject != null ? body.NetworkObject.ObjectId : -1;
                break;
            }

            return
                $"clientId={localClientId} raceSceneLoaded={raceSceneLoaded} localPlayerListed={localPlayerListed} " +
                $"localPlayerReady={(localPlayerListed && localPlayer.IsReady)} localManagersReported={owner.DiagnosticManagersReported} " +
                $"introAssignmentSeq={owner.DiagnosticAssignmentSequence} introVisualSeq={owner.DiagnosticVisualSequence} gameplayLiveSeq={owner.DiagnosticGameplaySequence} " +
                $"localRaceBodyReady={localRaceBodyReady} localRaceBodyObj={localRaceBodyObjectId} " +
                $"managers[intro={introSequenceReady},leaderboard={leaderboardReady},finish={raceFinishReady},decision={resultDecisionReady},presentation={resultPresentationReady},resultArea={resultAreaReady}]";
        }

        internal static string BuildServerRaceReadinessSummary(RoomStateManager owner, RaceStartHandshake handshake)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(
                $"players={owner.Players.Count} managersReadyCount={handshake.SceneManagersReadyCount} " +
                $"introAssignmentReadyCount={handshake.ReadyCount(RaceStartHandshake.Stage.Assignment)} introVisualReadyCount={handshake.ReadyCount(RaceStartHandshake.Stage.Visual)} " +
                $"gameplayLiveCount={handshake.ReadyCount(RaceStartHandshake.Stage.Gameplay)} goIssued={owner.IsAuthoritativeGoIssued} movementUnlocked={owner.IsGameplayMovementUnlocked}");

            UnityEngine.SceneManagement.Scene raceScene = string.IsNullOrWhiteSpace(owner.DiagnosticRaceSceneName)
                ? default
                : UnitySceneManager.GetSceneByName(owner.DiagnosticRaceSceneName);
            HashSet<NetworkConnection> sceneConnections = null;
            if (raceScene.IsValid() && raceScene.isLoaded && InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.SceneConnections.TryGetValue(raceScene, out sceneConnections);

            for (int i = 0; i < owner.Players.Count; i++)
            {
                RoomPlayerState playerState = owner.Players[i];
                NetworkConnection conn = null;
                bool hasConn = false;
                if (InstanceFinder.ServerManager != null)
                {
                    hasConn = InstanceFinder.ServerManager.Clients.TryGetValue(playerState.PlayerId, out conn) && conn != null;
                }
                bool authenticated = hasConn && conn.IsAuthenticated;
                bool inRaceScene = hasConn && sceneConnections != null && sceneConnections.Contains(conn);
                bool hasRacePlayer = hasConn && owner.HasOwnedRacePlayer(conn);
                bool managersReady = handshake.AreSceneManagersReady(playerState.PlayerId);
                bool introAssignmentReady = handshake.HasReported(playerState.PlayerId, RaceStartHandshake.Stage.Assignment);
                bool introVisualReady = handshake.HasReported(playerState.PlayerId, RaceStartHandshake.Stage.Visual);
                bool gameplayLive = handshake.HasReported(playerState.PlayerId, RaceStartHandshake.Stage.Gameplay);
                handshake.TryGetReportedSequence(playerState.PlayerId, RaceStartHandshake.Stage.Assignment, out int introAssignmentSeq);
                handshake.TryGetReportedSequence(playerState.PlayerId, RaceStartHandshake.Stage.Visual, out int introVisualSeq);
                handshake.TryGetReportedSequence(playerState.PlayerId, RaceStartHandshake.Stage.Gameplay, out int gameplayLiveSeq);
                sb.Append(
                    $" | p{playerState.PlayerId}:{playerState.PlayerName} host={playerState.IsHost} roomReady={playerState.IsReady} " +
                    $"conn={hasConn} auth={authenticated} scene={inRaceScene} racePlayer={hasRacePlayer} " +
                    $"sceneReady={managersReady} introReady={introAssignmentReady}(seq={introAssignmentSeq}) " +
                    $"visualReady={introVisualReady}(seq={introVisualSeq}) gameplayLive={gameplayLive}(seq={gameplayLiveSeq})");
            }

            return sb.ToString();
        }

        internal static string FormatSceneNames(Scene[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
                return "<none>";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < scenes.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                sb.Append(scenes[i].name);
            }

            return sb.ToString();
        }
    }
}
