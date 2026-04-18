using FishNet;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class BuddahPredictionPushTargetBox : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private bool resetVelocityBeforeApply;
        [SerializeField] private float linearDamping = 0.35f;
        [SerializeField] private float angularDamping = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private Color gizmoColor = new Color(0.15f, 0.8f, 1f, 0.2f);

        private BoxCollider _boxCollider;

        public bool TryApplyServerImpulse(
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            int sourceObjectId)
        {
            if (!InstanceFinder.IsServerStarted || targetRigidbody == null)
                return false;

            if (resetVelocityBeforeApply)
            {
                targetRigidbody.velocity = Vector3.zero;
                targetRigidbody.angularVelocity = Vector3.zero;
            }

            targetRigidbody.WakeUp();
            targetRigidbody.AddForce(impulse, ForceMode.Impulse);
            if (Mathf.Abs(turnTorqueImpulse) > 0.001f)
                targetRigidbody.AddTorque(Vector3.up * turnTorqueImpulse, ForceMode.Impulse);

            if (verboseLogs)
            {
                Debug.Log(
                    $"{Bootstrap.BuddahPredictionBootstrap.LogPrefix} push-target hit name={name} source={sourceType} " +
                    $"sourceId={sourceObjectId} impulse={impulse} torque={turnTorqueImpulse:0.00}",
                    this);
            }

            return true;
        }

        private void Awake()
        {
            if (targetRigidbody == null)
                targetRigidbody = GetComponent<Rigidbody>();

            _boxCollider = GetComponent<BoxCollider>();

            if (targetRigidbody != null)
            {
                targetRigidbody.isKinematic = false;
                targetRigidbody.useGravity = false;
                targetRigidbody.drag = linearDamping;
                targetRigidbody.angularDrag = angularDamping;
                targetRigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (_boxCollider != null)
                _boxCollider.isTrigger = false;
        }

        private void OnDrawGizmos()
        {
            BoxCollider boxCollider = _boxCollider != null ? _boxCollider : GetComponent<BoxCollider>();
            if (boxCollider == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
