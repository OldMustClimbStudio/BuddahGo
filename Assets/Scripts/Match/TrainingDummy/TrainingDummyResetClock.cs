namespace BuddahGo.Match
{
    // Pure reset timing for a training dummy: once it is displaced from its home pose, it is teleported back
    // `delaySeconds` after the last disturbance (skill effect, impulse, collision with another body or motion).
    // Resting contact with another body is not a disturbance (see IsDisturbingContact).
    internal struct TrainingDummyResetClock
    {
        // A body contact disturbs the dummy only while the two bodies really move relative to each other. Resting
        // against another body (another parked dummy, an idle racer or prop) is not a new collision: counting it
        // would refresh the clock on every physics step and the dummy would never go home.
        public static bool IsDisturbingContact(float relativeSpeed, float minRelativeSpeed) => relativeSpeed > minRelativeSpeed;

        private float _lastDisturbanceTime;

        // Starts (or restarts after a reset) the wait at `now`, so a just-queued teleport is not requested again.
        public void Restart(float now) => _lastDisturbanceTime = now;

        public void MarkDisturbed(float now)
        {
            if (now > _lastDisturbanceTime)
                _lastDisturbanceTime = now;
        }

        public bool ShouldReset(float now, bool displaced, float delaySeconds) => displaced && now - _lastDisturbanceTime >= delaySeconds;

        // Seconds left before the reset, or -1 when none is pending (the dummy is at home).
        public float SecondsUntilReset(float now, bool displaced, float delaySeconds)
        {
            if (!displaced)
                return -1f;
            float left = _lastDisturbanceTime + delaySeconds - now;
            return left > 0f ? left : 0f;
        }
    }
}
