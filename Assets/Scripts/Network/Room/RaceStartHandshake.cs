using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    // Local bookkeeping only. Network protocol fields remain on RoomStateManager.
    internal sealed class RaceStartHandshake
    {
        internal readonly HashSet<int> _raceSceneManagersReadyClientIds = new HashSet<int>();
        internal readonly HashSet<int> _introAssignmentReadyClientIds = new HashSet<int>();
        internal readonly HashSet<int> _introVisualReadyClientIds = new HashSet<int>();
        internal readonly HashSet<int> _gameplayLiveClientIds = new HashSet<int>();
        internal readonly Dictionary<int, int> _introAssignmentReadySequenceByClientId = new Dictionary<int, int>();
        internal readonly Dictionary<int, int> _introVisualReadySequenceByClientId = new Dictionary<int, int>();
        internal readonly Dictionary<int, int> _gameplayLiveSequenceByClientId = new Dictionary<int, int>();

        internal void ResetAllReadiness()
        {
            _raceSceneManagersReadyClientIds.Clear();
            _introAssignmentReadyClientIds.Clear();
            _introVisualReadyClientIds.Clear();
            _gameplayLiveClientIds.Clear();
            _introAssignmentReadySequenceByClientId.Clear();
            _introVisualReadySequenceByClientId.Clear();
            _gameplayLiveSequenceByClientId.Clear();
        }

        internal void ResetSequenceReadiness()
        {
            _introAssignmentReadyClientIds.Clear();
            _introVisualReadyClientIds.Clear();
            _gameplayLiveClientIds.Clear();
            _introAssignmentReadySequenceByClientId.Clear();
            _introVisualReadySequenceByClientId.Clear();
            _gameplayLiveSequenceByClientId.Clear();
        }

        internal void RemovePlayer(int playerId)
        {
            _raceSceneManagersReadyClientIds.Remove(playerId);
            _introAssignmentReadyClientIds.Remove(playerId);
            _introVisualReadyClientIds.Remove(playerId);
            _gameplayLiveClientIds.Remove(playerId);
            _introAssignmentReadySequenceByClientId.Remove(playerId);
            _introVisualReadySequenceByClientId.Remove(playerId);
            _gameplayLiveSequenceByClientId.Remove(playerId);
        }

        internal static bool TryMarkLocalSequence(bool clientInitialized, int sequenceId, ref int reportedSequenceId)
        {
            if (!clientInitialized || sequenceId < 0 || reportedSequenceId == sequenceId)
                return false;

            reportedSequenceId = sequenceId;
            return true;
        }

        internal static bool AreAllReadyForSequence(bool serverInitialized, IList<RoomPlayerState> players,
            int sequenceId, Dictionary<int, int> readySequences)
        {
            if (!serverInitialized || sequenceId < 0)
                return false;

            for (int i = 0; i < players.Count; i++)
            {
                int playerId = players[i].PlayerId;
                if (!readySequences.TryGetValue(playerId, out int readySequenceId) || readySequenceId != sequenceId)
                    return false;
            }

            return true;
        }

        internal static void RecordReadySequence(int playerId, int sequenceId,
            HashSet<int> readyClientIds, Dictionary<int, int> readySequences)
        {
            readyClientIds.Add(playerId);
            readySequences[playerId] = sequenceId;
        }
    }
}
