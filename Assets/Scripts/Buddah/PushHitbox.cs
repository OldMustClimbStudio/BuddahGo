using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

/// <summary>
/// Server-only short-lived trigger hitbox. When it overlaps a victim player,
/// the server tells the victim's OWNER client to apply an impulse (since physics is client-authoritative).
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PushHitbox : MonoBehaviour
{
    private const float OverlapPadding = 0.02f;

    private NetworkObject _attacker;
    private Transform _attackerTransform;
    private Vector3 _impulse;
    private float _expireTime;
    private Vector3 _localOffsetFromAttacker;
    private Quaternion _localRotationFromAttacker;

    private Collider _myCollider;
    private readonly HashSet<NetworkObject> _hit = new();

    private void Awake()
    {
        _myCollider = GetComponent<Collider>();
        if (_myCollider == null)
            _myCollider = GetComponentInChildren<Collider>(true);

        // Safety: ensure trigger + kinematic rb.
        if (_myCollider != null)
            _myCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void Init(NetworkObject attacker, Vector3 impulse, float lifetimeSeconds)
    {
        _attacker = attacker;
        _attackerTransform = attacker != null ? attacker.transform : null;
        _impulse = impulse;
        _expireTime = Time.time + Mathf.Max(0.01f, lifetimeSeconds);

        CacheAttackerRelativePose();

        // Ignore attacker colliders to avoid self-hit.
        if (_attacker != null && _myCollider != null)
        {
            Collider[] cols = _attacker.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c != null)
                    Physics.IgnoreCollision(_myCollider, c, true);
            }
        }

        Debug.Log($"[PushHitbox] Spawn attacker={_attacker?.name} pos={transform.position} rot={transform.rotation.eulerAngles} impulse={_impulse} lifetime={lifetimeSeconds:0.00}");
    }

    public void ApplyScaleMultiplier(float scaleMultiplier)
    {
        float multiplier = Mathf.Max(0.01f, scaleMultiplier);
        if (Mathf.Approximately(multiplier, 1f))
            return;

        if (_myCollider == null)
            _myCollider = GetComponent<Collider>();

        switch (_myCollider)
        {
            case BoxCollider box:
                box.size *= multiplier;
                break;
            case SphereCollider sphere:
                sphere.radius *= multiplier;
                break;
            case CapsuleCollider capsule:
                capsule.radius *= multiplier;
                capsule.height *= multiplier;
                break;
        }
    }

    private void Update()
    {
        // This object should only matter on server; if created elsewhere, destroy it.
        if (!InstanceFinder.IsServerStarted)
        {
            Destroy(gameObject);
            return;
        }

        FollowAttackerPose();
        CheckVictimOverlaps();

        if (Time.time >= _expireTime)
            Destroy(gameObject);
    }

    private void CacheAttackerRelativePose()
    {
        if (_attackerTransform == null)
            return;

        _localOffsetFromAttacker = _attackerTransform.InverseTransformPoint(transform.position);
        _localRotationFromAttacker = Quaternion.Inverse(_attackerTransform.rotation) * transform.rotation;
    }

    private void FollowAttackerPose()
    {
        if (_attackerTransform == null)
            return;

        transform.SetPositionAndRotation(
            _attackerTransform.TransformPoint(_localOffsetFromAttacker),
            _attackerTransform.rotation * _localRotationFromAttacker);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        TryApplyHit(other, "enter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        TryApplyHit(other, "stay");
    }

    private void CheckVictimOverlaps()
    {
        if (_myCollider == null)
            return;

        if (_myCollider is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale) + Vector3.one * OverlapPadding;
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, box.transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
                TryApplyHit(overlaps[i], "overlap-box");
            return;
        }

        if (_myCollider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float radius = sphere.radius * Mathf.Max(
                sphere.transform.lossyScale.x,
                Mathf.Max(sphere.transform.lossyScale.y, sphere.transform.lossyScale.z)) + OverlapPadding;
            Collider[] overlaps = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
                TryApplyHit(overlaps[i], "overlap-sphere");
            return;
        }

        Collider[] fallbackOverlaps = Physics.OverlapBox(
            _myCollider.bounds.center,
            _myCollider.bounds.extents + Vector3.one * OverlapPadding,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < fallbackOverlaps.Length; i++)
            TryApplyHit(fallbackOverlaps[i], "overlap-fallback");
    }

    private void TryApplyHit(Collider other, string detectionMode)
    {
        if (other == null)
            return;

        NetworkObject victimNO = other.GetComponentInParent<NetworkObject>();
        if (victimNO == null)
            return;

        if (victimNO == _attacker)
            return;

        if (_hit.Contains(victimNO))
            return;

        _hit.Add(victimNO);

        Debug.Log($"[PushHitbox] Hit mode={detectionMode} victim={victimNO.name} owner={victimNO.OwnerId} attacker={_attacker?.name} impulse={_impulse}");

        // Phase 4b V3 — single dispatch entry. Router internally handles V2-prediction Buddah,
        // PushTargetBox debug, and Legacy Buddah BuddahMovement RPC fallback. Replaces the
        // pre-V3 two-tier dispatch (CombatRouting + per-callsite BuddahMovement.ApplyPushImpulseTargetRpc).
        BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, 0f, BuddahPredictedImpulseSourceType.MeleePush, _attacker);
    }
}
