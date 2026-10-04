using BuddahGo.Match;

namespace BuddahGo.AI
{
    // FishNet may run several catch-up ticks in one rendered frame. Limit actual frame work,
    // not just tick phases, and keep snapshot capture separate from candidate evaluation.
    public sealed class AISkillDecisionSchedule
    {
        private int _snapshotFrame = -1, _decisionFrame = -1;
        public void SnapshotCaptured(int frame) => _snapshotFrame = frame;
        public bool TryClaim(uint tick, int racerIndex, int frame)
        {
            if (frame == _snapshotFrame || frame == _decisionFrame || tick % RacerId.MaxAI != (uint)racerIndex) return false;
            _decisionFrame = frame;
            return true;
        }
    }
}
