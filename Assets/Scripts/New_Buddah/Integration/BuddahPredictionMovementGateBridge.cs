using SteamMultiplayer.Network.Results;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    public sealed class BuddahPredictionMovementGateBridge
    {
        public bool IsMovementAllowed(GameObject owner)
        {
            if (owner == null)
                return false;

            return ResultAreaInteractionGate.ShouldAllowMovementInput(owner);
        }
    }
}
