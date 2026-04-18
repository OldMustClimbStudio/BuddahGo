using FishNet.Object.Prediction;

namespace NewBuddah.PredictionV2.Core
{
    public struct BuddahPredictedInputData : IReplicateData
    {
        private uint _tick;

        public float Steering;
        public float Throttle;
        public bool MovementAllowed;
        public bool OwnerInputLive;

        public BuddahPredictedInputData(float steering, float throttle, bool movementAllowed, bool ownerInputLive) : this()
        {
            Steering = steering;
            Throttle = throttle;
            MovementAllowed = movementAllowed;
            OwnerInputLive = ownerInputLive;
        }

        public uint GetTick()
        {
            return _tick;
        }

        public void SetTick(uint value)
        {
            _tick = value;
        }

        public void Dispose()
        {
        }
    }
}
