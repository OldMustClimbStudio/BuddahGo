using System;
using System.Collections.Generic;

namespace NewBuddah.PredictionV2.Events
{
    // Tick-stamped event channel per effect family. See Docs/prediction-refactor-plan/05-event-channel.md.
    //
    // Phase 4b V2b Step 0 redesign — mirrors Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs
    // (the OLD-path canonical model). Storage is a List<Entry>; entries carry server-stamped EventTick;
    // ConsumeReady drains entries with EventTick <= currentTick via a callback-driven pattern; consumed
    // event IDs are remembered in _recentEventIds (size 64) for retransmit dedupe. Replay-safe by
    // construction: events removed from _pending only when callback returns true on a forward pass.
    //
    // Why this design (vs the V2a-era ring + TryDequeue): see lessons-log L16 (single-consumer FIFO +
    // FishNet reconcile replay = structural blindness) and L17 (cross-phase observation drain = bidirectional
    // phase-skew false positives).
    public sealed class BuddahPredictionEventChannel<T>
    {
        public struct Entry
        {
            public uint Id;
            public uint EventTick;
            public T Payload;
        }

        public delegate bool ConsumeCallback(in Entry entry);

        public const int DefaultCapacity = 64;
        private const int MaxRecentIds = 64;

        private readonly List<Entry> _pending;
        private readonly HashSet<uint> _recentEventIds = new();
        private readonly Queue<uint> _recentEventOrder = new();
        private readonly int _capacity;
        private uint _nextSequence;
        private uint _lastConsumedId;

        public BuddahPredictionEventChannel() : this(DefaultCapacity) { }

        public BuddahPredictionEventChannel(int capacity)
        {
            if (capacity <= 0)
                capacity = DefaultCapacity;
            _capacity = capacity;
            _pending = new List<Entry>(capacity);
            _nextSequence = 1u;
        }

        public int Capacity => _capacity;
        public int Count => _pending.Count;
        public uint NextSequence => _nextSequence;
        public uint LastConsumedId => _lastConsumedId;

        // V2b Step 0 — tick-stamped enqueue. eventTick is the server-canonical clock for this event
        // (set by adapter at server-side cmd construction; transported through TargetRpc; client uses
        // cmd-supplied tick verbatim). See Q2 in design Q&A archive.
        public bool TryEnqueue(in T payload, uint eventTick, out uint id)
        {
            if (_pending.Count >= _capacity)
            {
                id = 0u;
                return false;
            }

            id = _nextSequence++;
            // Belt-and-braces: id is monotonic per-channel so collision is impossible in practice;
            // recent-IDs check is here in case future code reuses IDs across channels.
            if (_recentEventIds.Contains(id))
                return false;

            _pending.Add(new Entry { Id = id, EventTick = eventTick, Payload = payload });
            return true;
        }

        // V2b Step 0 — replay-safe drain. Mirrors BuddahPredictedImpulseEventQueue.ConsumeReady.
        // Entries with EventTick <= currentTick are presented to the callback. Returning true consumes
        // the entry (RemoveAt + RememberEventId). Returning false leaves the entry pending for the next
        // tick. Iteration is forward (FIFO order) — V2b Step 1 may introduce conditional callback
        // returns for filter-driven apply ordering.
        public int ConsumeReady(uint currentTick, ConsumeCallback callback)
        {
            int consumed = 0;
            for (int i = 0; i < _pending.Count; )
            {
                Entry entry = _pending[i];
                if (entry.EventTick > currentTick)
                {
                    i++;
                    continue;
                }
                if (!callback(in entry))
                {
                    i++;
                    continue;
                }

                _pending.RemoveAt(i);
                RememberEventId(entry.Id);
                _lastConsumedId = entry.Id;
                consumed++;
                // do not increment i: List shifted left
            }
            return consumed;
        }

        // V2a era. Replay-unsafe (drains exactly once and entries vanish). Retained as a transitional
        // shim; no callsite remains in V2b Step 0 (motor's InvertedShadow drain rewritten to ConsumeReady).
        // Will be deleted at V4 when CombatRouting + legacy fallback is removed.
        [Obsolete("Use ConsumeReady. TryDequeue is replay-unsafe — see lessons-log L16.")]
        public bool TryDequeue(out Entry entry)
        {
            if (_pending.Count == 0)
            {
                entry = default;
                return false;
            }

            entry = _pending[0];
            _pending.RemoveAt(0);
            _lastConsumedId = entry.Id;
            RememberEventId(entry.Id);
            return true;
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
