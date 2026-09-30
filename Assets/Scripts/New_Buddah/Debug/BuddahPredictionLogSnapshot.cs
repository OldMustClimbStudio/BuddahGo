using System.Diagnostics;

namespace NewBuddah.PredictionV2.Debugging
{
    // Diagnostic-only copy of the latest event, independent of the sampling clock.
    // In particular, reconcile speed cannot be read from DebugState.planarSpeed later:
    // another replicate may already have overwritten it before the log is sampled.
    internal struct BuddahPredictionLogSnapshot
    {
        private bool _hasReplicate, _hasReconcile;
        private uint _replicateTick, _reconcileTick;
        private float _steering, _throttle, _speed, _positionDelta, _velocityDelta, _planarDelta, _verticalDelta;
        private string _status, _writerReason;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void CaptureReplicate(uint tick, float steering, float throttle, string status, string writerReason = null)
        {
            _hasReplicate = true;
            _replicateTick = tick;
            _steering = steering;
            _throttle = throttle;
            _status = status;
            _writerReason = writerReason;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void CaptureReconcile(uint tick, float speed, float positionDelta, float velocityDelta, float planarDelta, float verticalDelta)
        {
            _hasReconcile = true;
            _reconcileTick = tick;
            _speed = speed;
            _positionDelta = positionDelta;
            _velocityDelta = velocityDelta;
            _planarDelta = planarDelta;
            _verticalDelta = verticalDelta;
        }

        public string BuildReplicateSummary() => !_hasReplicate ? "n/a" :
            $"tick={_replicateTick} steer={_steering:0.00} throttle={_throttle:0.00} {_status}"
            + (_writerReason == null ? string.Empty : ":" + _writerReason);

        public string BuildReconcileSummary() => !_hasReconcile ? "n/a" :
            $"tick={_reconcileTick} speed={_speed:0.00} posDelta={_positionDelta:0.000} velDelta={_velocityDelta:0.000} " +
            $"planarVelDelta={_planarDelta:0.000} verticalVelDelta={_verticalDelta:0.000}";
    }
}
