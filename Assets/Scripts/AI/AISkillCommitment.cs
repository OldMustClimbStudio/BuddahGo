using System;

namespace BuddahGo.AI
{
    public enum AISkillCommitmentPhase { Idle, Decided, Combo, WaitingConfirm, Resolved }

    // Tick-only action state. The shared input recognizer and executor, not this class, decide casts.
    public sealed class AISkillCommitment
    {
        public AISkillCommitmentPhase Phase { get; private set; }
        public int Slot { get; private set; } = -1;
        public int KeyIndex { get; private set; }
        public int Retries { get; private set; }
        public uint Deadline { get; private set; }
        public bool Active => Phase != AISkillCommitmentPhase.Idle && Phase != AISkillCommitmentPhase.Resolved;
        public bool Waiting => Phase == AISkillCommitmentPhase.WaitingConfirm;
        public int FailedCombos { get; private set; }
        private uint _nextKey;
        private bool _delayed;
        private ComboSkillInput.Token[] _sequence;

        public void Begin(int slot, ComboSkillInput.Token[] sequence, uint tick, uint deadline)
        {
            if (Active || sequence == null || sequence.Length == 0) throw new InvalidOperationException("Cannot replace an active skill commitment.");
            Slot = slot; _sequence = sequence; Deadline = deadline; KeyIndex = 0; Retries = 0;
            _delayed = false; _nextKey = tick + 1; Phase = AISkillCommitmentPhase.Decided;
        }

        public bool NextKey(uint tick, float delta, float minInterval, float maxInterval, float inputWindow,
            float mistakeProbability, Random random, out ComboSkillInput.Token token)
        {
            token = default;
            if (!Active || Waiting || tick < _nextKey || tick >= Deadline) return false;
            Phase = AISkillCommitmentPhase.Combo;
            if (!_delayed && KeyIndex > 0 && random.NextDouble() < mistakeProbability)
            {
                FailedCombos++;
                if (Retries >= 1) { Cancel(); return false; }
                Retries++; KeyIndex = 0; _delayed = true;
                _nextKey = tick + SecondsToTicks(inputWindow + delta, delta);
                return false;
            }
            _delayed = false;
            token = _sequence[KeyIndex++];
            _nextKey = tick + SecondsToTicks(minInterval + (float)random.NextDouble() * (maxInterval - minInterval), delta);
            if (KeyIndex == _sequence.Length) Phase = AISkillCommitmentPhase.WaitingConfirm;
            return true;
        }

        public void Accepted() { if (Active) Phase = AISkillCommitmentPhase.WaitingConfirm; }
        public void Resolve() { Phase = AISkillCommitmentPhase.Resolved; _sequence = null; }
        public void Cancel() { Phase = AISkillCommitmentPhase.Idle; Slot = -1; KeyIndex = 0; _sequence = null; }
        public void ReleaseResolved() { if (Phase == AISkillCommitmentPhase.Resolved) Cancel(); }
        public static uint SecondsToTicks(float seconds, float delta) => (uint)Math.Max(1, Math.Ceiling(seconds / delta));
    }

    public sealed class AISteeringPerception
    {
        public float Sign { get; private set; } = 1f;
        private float _observed = 1f;
        private uint _adaptAt;
        public float Observe(float actualSign, uint tick, float delta, float adaptSeconds, float readaptSeconds)
        {
            actualSign = actualSign < 0f ? -1f : 1f;
            if (_observed != actualSign)
            {
                _observed = actualSign;
                _adaptAt = tick + AISkillCommitment.SecondsToTicks(actualSign < 0f ? adaptSeconds : readaptSeconds, delta);
            }
            if (tick >= _adaptAt) Sign = _observed;
            return Sign;
        }
        public void Reset() { Sign = _observed = 1f; _adaptAt = 0; }
    }
}
