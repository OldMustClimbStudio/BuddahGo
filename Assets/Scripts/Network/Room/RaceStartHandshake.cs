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
    }
}
