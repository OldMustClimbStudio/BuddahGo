#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BuddahGo.Match;
using UnityEngine;

namespace BuddahGo.AI
{
    // MechanicsPracticeHarness fixture only: parks the human while the harness runs. Practice auto-throttles the human,
    // and the predicted motor honours an ISteeringOverride for any racer with local control (the owner included), the
    // same way a training dummy parks itself. Added and removed at runtime by the harness; never on a prefab.
    [DisallowMultipleComponent]
    public sealed class MechanicsHarnessParkOverride : MonoBehaviour, ISteeringOverride
    {
        public bool TryGetOverride(out int steering, out bool drive)
        {
            steering = 0;
            drive = false;
            return true;
        }
    }
}
#endif
