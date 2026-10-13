using UnityEngine;

namespace NewBuddah.PredictionV2.Config
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // Apply table values before the motor caches base mass / reads the config.
    public class BuddahPredictedMotorConfig : MonoBehaviour
    {
        private void Awake()
        {
            // Movement/MovementBase, Movement/PushReaction (MeleePush row) and Movement/LaunchHandoff own these numbers.
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null)
                return;
            MovementBaseRecord movement = DesignTableOverlay.Single(db.movementBases, r => r.profileId == "default") ?? DesignTableOverlay.Single(db.movementBases, null);
            if (movement != null)
            {
                DesignTableOverlay.Apply(this, movement);
                Rigidbody body = GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.mass = movement.mass;
                    body.drag = movement.linearDrag;
                    body.angularDrag = movement.angularDrag;
                    body.maxAngularVelocity = movement.maxAngularVelocity;
                }
            }
            DesignTableOverlay.Apply(this, DesignTableOverlay.Single(db.pushReactions, r => r.impulseSource == "MeleePush"), "graceSeconds=pushGraceSeconds", "extraMaxSpeed=pushExtraMaxSpeed");
            DesignTableOverlay.Apply(this, DesignTableOverlay.Single(db.launchHandoffs, null), "groundSnapMaxDistance=handoffGroundSnapMaxDistance", "groundProbeHeight=handoffGroundProbeHeight");
        }

        [Header("Stage 1 Locomotion")]
        [SerializeField] private float forwardForce = 50f;
        [SerializeField] private float turnTorque = 30f;
        [SerializeField] private float turnDecayPerSecond = 3f;
        [SerializeField] private float maxSpeed = 80f;
        [SerializeField] private float turnInputMultiplier = 1f;
        [SerializeField] private float pushGraceSeconds = 0.25f;
        [SerializeField] private float pushExtraMaxSpeed = 6f;

        [Header("Launch Handoff")]
        [Tooltip("When the launch handoff is consumed the body is placed at the spline snapshot, which may sit below or above the track surface. Within this distance the body is moved vertically onto its collider rest height first, so PhysX depenetration does not eat the first ticks of forward motion. 0 disables.")]
        [SerializeField, Min(0f)] private float handoffGroundSnapMaxDistance = 1.5f;
        [Tooltip("Height above the lowest collider point from which the ground probe ray is cast downward. Clamped to the snap distance so out-of-range surfaces above the body cannot hide the ground.")]
        [SerializeField, Min(0.01f)] private float handoffGroundProbeHeight = 3f;

        public float ForwardForce => forwardForce;
        public float TurnTorque => turnTorque;
        public float TurnDecayPerSecond => turnDecayPerSecond;
        public float MaxSpeed => maxSpeed;
        public float TurnInputMultiplier => turnInputMultiplier;
        public float PushGraceSeconds => pushGraceSeconds;
        public float PushExtraMaxSpeed => pushExtraMaxSpeed;
        public float HandoffGroundSnapMaxDistance => handoffGroundSnapMaxDistance;
        public float HandoffGroundProbeHeight => handoffGroundProbeHeight;
    }
}
