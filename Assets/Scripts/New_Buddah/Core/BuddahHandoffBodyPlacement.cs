using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Places the predicted body at a launch-handoff snapshot. Sleep/WakeUp drops PhysX contact and sleep state after
    // the pose jump, but putting a body to sleep also zeroes its velocities, so the snapshot velocity is written after
    // the wake. Writing it before (as the online consume path once did) launched the racer from rest: with
    // Movement.launchInheritSeconds / launchBlendSeconds at 0 nothing restores the snapshot velocity later.
    public static class BuddahHandoffBodyPlacement
    {
        public static void Place(Rigidbody body, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            if (body == null)
                return;

            body.position = position;
            body.rotation = rotation;
            body.Sleep();
            body.WakeUp();
            if (body.isKinematic)
                return;

            body.velocity = velocity;
            body.angularVelocity = angularVelocity;
        }
    }
}
