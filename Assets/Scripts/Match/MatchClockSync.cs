using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace BuddahGo.Match
{
    public sealed class MatchClockSync : NetworkBehaviour, IMatchClock
    {
        // S1 reserves the synchronized pause offset; it remains zero until S7.
        private readonly SyncVar<uint> _pausedTicks = new SyncVar<uint>();
        private MatchClock _clock;
        public double Now => _clock != null ? _clock.Now : 0d;
        public bool IsPaused => false;
        private void Register()
        {
            _clock = new MatchClock(() => TimeManager.Tick - _pausedTicks.Value, TimeManager.TickDelta);
            MatchServices.Clock = this;
        }
        public override void OnStartServer() { base.OnStartServer(); _pausedTicks.Value = 0; Register(); }
        public override void OnStartClient() { base.OnStartClient(); Register(); }
        public override void OnStopNetwork()
        {
            if (ReferenceEquals(MatchServices.Clock, this)) MatchServices.Clock = null;
            _clock = null;
            base.OnStopNetwork();
        }
    }
}
