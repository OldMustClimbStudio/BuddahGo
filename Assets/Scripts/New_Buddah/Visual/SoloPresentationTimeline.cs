using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Visual
{
    // Pose times are completed Unity physics times, never network callback times.
    // One fixed-step delay is selected at sequence start and remains unchanged at GO.
    internal sealed class SoloPresentationTimeline
    {
        private struct Sample
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly Sample[] _samples;
        private int _first;
        public int Count { get; private set; }
        public double Delay { get; }
        public double LatestTime => Count == 0 ? double.NaN : At(Count - 1).Time;

        public SoloPresentationTimeline(double delay, int capacity = 64)
        {
            if (delay <= 0 || double.IsInfinity(delay) || double.IsNaN(delay))
                throw new ArgumentOutOfRangeException(nameof(delay));
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            Delay = delay;
            _samples = new Sample[capacity];
        }

        public void Clear() { _first = 0; Count = 0; }
        private Sample At(int index) => _samples[(_first + index) % _samples.Length];

        public bool Record(double time, Vector3 position, Quaternion rotation)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || (Count > 0 && time < LatestTime))
                return false;
            var sample = new Sample { Time = time, Position = position, Rotation = rotation };
            // Two observations of the SAME simulation instant are not two steps.
            // Identical poses at DIFFERENT times remain separate, including real holds.
            if (Count > 0 && time == LatestTime)
            {
                _samples[(_first + Count - 1) % _samples.Length] = sample;
                return true;
            }
            if (Count == _samples.Length) { _first = (_first + 1) % _samples.Length; Count--; }
            _samples[(_first + Count++) % _samples.Length] = sample;
            return true;
        }

        public bool TrySample(double renderTime, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (Count == 0) return false;
            double time = renderTime - Delay;
            Sample a = At(0);
            for (int i = 1; i < Count && time > a.Time; i++)
            {
                Sample b = At(i);
                if (time <= b.Time)
                {
                    float alpha = (float)((time - a.Time) / (b.Time - a.Time));
                    position = Vector3.Lerp(a.Position, b.Position, alpha);
                    rotation = Quaternion.Slerp(a.Rotation, b.Rotation, alpha);
                    return true;
                }
                a = b;
            }
            // Underflow/startup holds the nearest real sample; never invent movement.
            position = a.Position;
            rotation = a.Rotation;
            return true;
        }

        internal static double AlignGoToPhysics(double localNow, double networkNow, double scheduledGo,
            double lastFixedTime, double fixedDelta)
        {
            double localGo = localNow + scheduledGo - networkNow;
            return lastFixedTime + Math.Ceiling((localGo - lastFixedTime) / fixedDelta) * fixedDelta;
        }
    }
}
