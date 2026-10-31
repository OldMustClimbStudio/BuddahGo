using System;

namespace BuddahGo.AI
{
    public enum AISkillCommitmentPhase { Idle, Decided, WaitingConfirm, Resolved }

    // Tick-only action state. A7: no combo keys. The caster decides a slot (Decided), requests it on the next tick
    // like a Human Player pressing the key (WaitingConfirm) and the shared SkillExecutor, not this class, decides
    // whether the cast is accepted, executed, cancelled or backfires.
    public sealed class AISkillCommitment
    {
        public AISkillCommitmentPhase Phase { get; private set; }
        public int Slot { get; private set; } = -1;
        public uint Deadline { get; private set; }
        public bool Active => Phase != AISkillCommitmentPhase.Idle && Phase != AISkillCommitmentPhase.Resolved;
        public bool Waiting => Phase == AISkillCommitmentPhase.WaitingConfirm;
        private uint _beginTick;

        public void Begin(int slot, uint tick, uint deadline)
        {
            if (Active) throw new InvalidOperationException("Cannot replace an active skill commitment.");
            Slot = slot; Deadline = deadline; _beginTick = tick; Phase = AISkillCommitmentPhase.Decided;
        }

        // True once, on a tick after Begin and before the deadline: the caster then calls CastSlotServer(Slot).
        // At the deadline the commitment cancels itself (the caster records "opportunity-expired").
        public bool ShouldRequest(uint tick)
        {
            if (Phase != AISkillCommitmentPhase.Decided) return false;
            if (tick >= Deadline) { Cancel(); return false; }
            if (tick <= _beginTick) return false;
            Phase = AISkillCommitmentPhase.WaitingConfirm;
            return true;
        }

        public void Accepted() { if (Active) Phase = AISkillCommitmentPhase.WaitingConfirm; }
        public void Resolve() { Phase = AISkillCommitmentPhase.Resolved; }
        public void Cancel() { Phase = AISkillCommitmentPhase.Idle; Slot = -1; }
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
