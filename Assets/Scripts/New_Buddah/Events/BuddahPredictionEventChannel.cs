namespace NewBuddah.PredictionV2.Events
{
    // Fixed-size ring buffer per effect family. See Docs/prediction-refactor-plan/05-event-channel.md.
    // Phase 2 scope: owner-side allocation + append + clear + count accessors. Motor does NOT consume
    // yet (per Phase 2 contract in 12-migration-sequence.md and lessons-log.md L6); Phase 3 adds the
    // consume / reconcile-replay path together with the simulation steps.
    public sealed class BuddahPredictionEventChannel<T>
    {
        public struct Entry
        {
            public uint Id;
            public T Payload;
        }

        public const int DefaultCapacity = 64;

        private readonly Entry[] _ring;
        private uint _nextSequence;
        private uint _lastConsumedId;
        private int _count;
        private int _tail;

        public BuddahPredictionEventChannel() : this(DefaultCapacity) { }

        public BuddahPredictionEventChannel(int capacity)
        {
            if (capacity <= 0)
                capacity = DefaultCapacity;
            _ring = new Entry[capacity];
            _nextSequence = 1u;
        }

        public int Capacity => _ring.Length;
        public int Count => _count;
        public uint NextSequence => _nextSequence;
        public uint LastConsumedId => _lastConsumedId;

        public bool TryEnqueue(in T payload, out uint id)
        {
            if (_count >= _ring.Length)
            {
                id = 0u;
                return false;
            }

            id = _nextSequence++;
            _ring[_tail] = new Entry { Id = id, Payload = payload };
            _tail = (_tail + 1) % _ring.Length;
            _count++;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _ring.Length; i++)
                _ring[i] = default;
            _tail = 0;
            _count = 0;
        }
    }
}
