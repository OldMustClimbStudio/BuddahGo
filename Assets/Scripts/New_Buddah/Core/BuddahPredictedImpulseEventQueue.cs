using System.Collections.Generic;
using System.Text;

namespace NewBuddah.PredictionV2.Core
{
    public sealed class BuddahPredictedImpulseEventQueue
    {
        private readonly List<BuddahPredictedImpulseEventData> _pending = new();
        private readonly HashSet<uint> _recentEventIds = new();
        private readonly Queue<uint> _recentEventOrder = new();

        private const int MaxRecentIds = 64;

        public int PendingCount => _pending.Count;

        public bool TryEnqueue(BuddahPredictedImpulseEventData eventData)
        {
            if (_recentEventIds.Contains(eventData.EventId))
                return false;

            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].EventId == eventData.EventId)
                    return false;
            }

            _pending.Add(eventData);
            return true;
        }

        public int ConsumeReady(uint currentTick, System.Func<BuddahPredictedImpulseEventData, bool> consume)
        {
            int consumedCount = 0;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                BuddahPredictedImpulseEventData eventData = _pending[i];
                if (eventData.EventTick > currentTick)
                    continue;

                if (!consume(eventData))
                    continue;

                eventData.Consumed = true;
                _pending.RemoveAt(i);
                RememberEventId(eventData.EventId);
                consumedCount++;
            }

            return consumedCount;
        }

        public string BuildPendingSummary()
        {
            if (_pending.Count == 0)
                return "none";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _pending.Count; i++)
            {
                BuddahPredictedImpulseEventData eventData = _pending[i];
                if (i > 0)
                    sb.Append(" | ");

                sb.Append('#');
                sb.Append(eventData.EventId);
                sb.Append(' ');
                sb.Append(eventData.SourceType);
                sb.Append(" @");
                sb.Append(eventData.EventTick);
            }

            return sb.ToString();
        }

        public void Clear()
        {
            _pending.Clear();
            _recentEventIds.Clear();
            _recentEventOrder.Clear();
        }

        private void RememberEventId(uint eventId)
        {
            if (!_recentEventIds.Add(eventId))
                return;

            _recentEventOrder.Enqueue(eventId);
            while (_recentEventOrder.Count > MaxRecentIds)
            {
                uint expired = _recentEventOrder.Dequeue();
                _recentEventIds.Remove(expired);
            }
        }
    }
}
