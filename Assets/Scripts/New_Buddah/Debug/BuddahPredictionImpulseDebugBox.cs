using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class BuddahPredictionImpulseDebugBox : MonoBehaviour
    {
        [SerializeField] private Vector3 impulse = new Vector3(0f, 0f, 16f);
        [SerializeField] private float turnTorqueImpulse = 8f;
        [SerializeField] private bool triggerOncePerVictim = true;
        [SerializeField] private bool fallbackToLegacy = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.45f, 0.1f, 0.25f);

        private readonly HashSet<NetworkObject> _triggeredVictims = new();
        private BoxCollider _boxCollider;

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider != null)
                _boxCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!InstanceFinder.IsServerStarted || other == null)
                return;

            NetworkObject victimNetworkObject = other.GetComponentInParent<NetworkObject>();
            if (victimNetworkObject == null)
                return;

            if (triggerOncePerVictim && _triggeredVictims.Contains(victimNetworkObject))
                return;

            if (triggerOncePerVictim)
                _triggeredVictims.Add(victimNetworkObject);

            // Phase 4b V3 — single dispatch entry (router internalizes V2 / PushTargetBox / Legacy fallback).
            // Note: 'fallbackToLegacy' serialized field is now ignored — router unconditionally falls back
            // to BuddahMovement RPC for Legacy buddah victims. Field kept per CLAUDE.md hard-stop on
            // serialized field removal; V4 cleanup will drop the field.
            BuddahPredictionRouter.RouteImpulse(
                victimNetworkObject,
                impulse,
                turnTorqueImpulse,
                BuddahPredictedImpulseSourceType.DebugBox,
                null);
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
