using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Pure helper for the launch-handoff ground snap: given where the body root would be placed and how far
    // its lowest collider point hangs below the root, decide the root height that rests the colliders on the
    // probed ground. No Unity physics calls here so the decision is unit-testable and identical on server
    // and owner.
    public static class BuddahHandoffGroundSnap
    {
        private const float MinimumSnapDistance = 0.0005f;

        /// <summary>
        /// Returns true and the resting root height when the vertical move needed is within
        /// <paramref name="maxSnapDistance"/>. Returns false (and the unchanged root height) when the
        /// clearance is unknown, snapping is disabled, the move would exceed the limit, or no move is needed.
        /// </summary>
        public static bool TryResolveRestY(float rootY, float clearanceBelowRoot, float groundY, float maxSnapDistance, out float restY)
        {
            restY = rootY;
            if (clearanceBelowRoot < 0f || maxSnapDistance <= 0f)
                return false;

            float desired = groundY + clearanceBelowRoot;
            float delta = desired - rootY;
            if (Mathf.Abs(delta) > maxSnapDistance || Mathf.Abs(delta) < MinimumSnapDistance)
                return false;

            restY = desired;
            return true;
        }
    }
}
