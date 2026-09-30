using System.Collections.Generic;

namespace SteamMultiplayer.Network
{
    // Local bookkeeping only. Network protocol fields remain on RoomStateManager.
    internal sealed class RaceStartHandshake
    {
        private readonly HashSet<int> _raceSceneManagersReadyClientIds = new HashSet<int>();
        private readonly HashSet<int> _introAssignmentReadyClientIds = new HashSet<int>();
        private readonly HashSet<int> _introVisualReadyClientIds = new HashSet<int>();
        private readonly HashSet<int> _gameplayLiveClientIds = new HashSet<int>();
        private readonly Dictionary<int, int> _introAssignmentReadySequenceByClientId = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _introVisualReadySequenceByClientId = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _gameplayLiveSequenceByClientId = new Dictionary<int, int>();

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

        internal enum Stage { Assignment, Visual, Gameplay }

        internal void MarkSceneManagersReady(int playerId) => _raceSceneManagersReadyClientIds.Add(playerId);
        internal bool AreSceneManagersReady(int playerId) => _raceSceneManagersReadyClientIds.Contains(playerId);

        private (HashSet<int> ids, Dictionary<int, int> sequences) Readiness(Stage stage)
        {
            switch (stage)
            {
                case Stage.Assignment: return (_introAssignmentReadyClientIds, _introAssignmentReadySequenceByClientId);
                case Stage.Visual: return (_introVisualReadyClientIds, _introVisualReadySequenceByClientId);
                case Stage.Gameplay: return (_gameplayLiveClientIds, _gameplayLiveSequenceByClientId);
                default: throw new System.ArgumentOutOfRangeException(nameof(stage));
            }
        }

        internal int SceneManagersReadyCount => _raceSceneManagersReadyClientIds.Count;
        internal int ReadyCount(Stage stage) => Readiness(stage).ids.Count;
        internal bool HasReported(int playerId, Stage stage) => Readiness(stage).ids.Contains(playerId);
        internal bool TryGetReportedSequence(int playerId, Stage stage, out int sequenceId)
            => Readiness(stage).sequences.TryGetValue(playerId, out sequenceId);

        internal bool IsReadyForSequence(int playerId, int sequenceId, Stage stage)
        {
            var readiness = Readiness(stage);
            return readiness.ids.Contains(playerId)
                && readiness.sequences.TryGetValue(playerId, out int recorded) && recorded == sequenceId;
        }

        internal bool AreAllReadyForSequence(bool serverInitialized, IList<RoomPlayerState> players,
            int sequenceId, Stage stage)
        {
            if (!serverInitialized || sequenceId < 0)
                return false;

            var readySequences = Readiness(stage).sequences;
            for (int i = 0; i < players.Count; i++)
            {
                int playerId = players[i].PlayerId;
                if (!readySequences.TryGetValue(playerId, out int readySequenceId) || readySequenceId != sequenceId)
                    return false;
            }

            return true;
        }

        internal void RecordReadySequence(int playerId, int sequenceId, Stage stage)
        {
            var readiness = Readiness(stage);
            readiness.ids.Add(playerId);
            readiness.sequences[playerId] = sequenceId;
        }
    }
}
