using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamMultiplayer.Network.Results
{
    public static class MatchResultCache
    {
        private static readonly List<FinalMatchResultEntry> _results = new List<FinalMatchResultEntry>();
        private static readonly Dictionary<int, FinalMatchResultEntry> _resultsByClientId = new Dictionary<int, FinalMatchResultEntry>();

        public static bool HasResults => _results.Count > 0;
        public static IReadOnlyList<FinalMatchResultEntry> Results => _results;

        public static void SetResults(IEnumerable<FinalMatchResultEntry> results)
        {
            _results.Clear();
            _resultsByClientId.Clear();

            if (results == null)
                return;

            List<FinalMatchResultEntry> orderedResults = results
                .Where(entry => entry != null)
                .Select(CloneEntry)
                .OrderBy(entry => MathfSafePositive(entry.FinalRank))
                .ThenBy(entry => entry.IsFinished ? MathfSafePositive(entry.FinishOrder) : int.MaxValue)
                .ThenByDescending(entry => entry.FinalCompletionPercent)
                .ThenBy(entry => entry.ClientId)
                .ToList();

            for (int i = 0; i < orderedResults.Count; i++)
            {
                FinalMatchResultEntry entry = orderedResults[i];
                if (entry.FinalRank <= 0)
                    entry.FinalRank = i + 1;

                _results.Add(entry);
                _resultsByClientId[entry.ClientId] = entry;
            }
        }

        public static bool TryGetResultForClient(int clientId, out FinalMatchResultEntry entry)
        {
            return _resultsByClientId.TryGetValue(clientId, out entry);
        }

        public static bool TryGetPlacementIndexForClient(int clientId, out int placementIndex)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].ClientId != clientId)
                    continue;

                placementIndex = i;
                return true;
            }

            placementIndex = -1;
            return false;
        }

        public static void Clear()
        {
            _results.Clear();
            _resultsByClientId.Clear();
        }

        private static FinalMatchResultEntry CloneEntry(FinalMatchResultEntry source)
        {
            return new FinalMatchResultEntry
            {
                ClientId = source.ClientId,
                PlayerName = source.PlayerName ?? string.Empty,
                FinalRank = source.FinalRank,
                FinalCompletionPercent = source.FinalCompletionPercent,
                IsFinished = source.IsFinished,
                FinishOrder = source.FinishOrder,
                FinishServerTime = source.FinishServerTime,
                Lap = source.Lap,
                Checkpoints = source.Checkpoints,
                DistanceOnTrack = source.DistanceOnTrack,
                LapProgress01 = source.LapProgress01
            };
        }

        private static int MathfSafePositive(int value)
        {
            return value > 0 ? value : int.MaxValue;
        }
    }

    [Serializable]
    public class FinalMatchResultEntry
    {
        public int ClientId;
        public string PlayerName;
        public int FinalRank;
        public float FinalCompletionPercent;
        public bool IsFinished;
        public int FinishOrder;
        public double FinishServerTime;
        public int Lap;
        public int Checkpoints;
        public float DistanceOnTrack;
        public float LapProgress01;
    }
}
