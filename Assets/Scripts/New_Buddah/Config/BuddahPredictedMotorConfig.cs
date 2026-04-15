using UnityEngine;

namespace NewBuddah.PredictionV2.Config
{
    [DisallowMultipleComponent]
    public class BuddahPredictedMotorConfig : MonoBehaviour
    {
        [Header("Stage 1 Locomotion")]
        [SerializeField] private float forwardForce = 50f;
        [SerializeField] private float turnTorque = 30f;
        [SerializeField] private float turnDecayPerSecond = 3f;
        [SerializeField] private float maxSpeed = 80f;
        [SerializeField] private float turnInputMultiplier = 1f;
        [SerializeField] private float pushGraceSeconds = 0.25f;
        [SerializeField] private float pushExtraMaxSpeed = 6f;

        public float ForwardForce => forwardForce;
        public float TurnTorque => turnTorque;
        public float TurnDecayPerSecond => turnDecayPerSecond;
        public float MaxSpeed => maxSpeed;
        public float TurnInputMultiplier => turnInputMultiplier;
        public float PushGraceSeconds => pushGraceSeconds;
        public float PushExtraMaxSpeed => pushExtraMaxSpeed;
    }
}
