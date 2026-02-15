using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// Server-only short-lived trigger hitbox. When it overlaps a victim player,
/// the server tells the victim's OWNER client to apply an impulse (since physics is client-authoritative).
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PushHitbox : MonoBehaviour
{
    private NetworkObject _attacker;
    private Vector3 _impulse;
    private float _expireTime;

    private Collider _myCollider;
    private readonly HashSet<NetworkObject> _hit = new();

    private void Awake()
    {
        _myCollider = GetComponent<Collider>();

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
        _impulse = impulse;
        _expireTime = Time.time + Mathf.Max(0.01f, lifetimeSeconds);

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
    }

    private void Update()
    {
        // This object should only matter on server; if created elsewhere, destroy it.
        if (!InstanceFinder.IsServer)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= _expireTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InstanceFinder.IsServer)
            return;

        NetworkObject victimNO = other.GetComponentInParent<NetworkObject>();
        if (victimNO == null)
            return;

        if (victimNO == _attacker)
            return;

        if (_hit.Contains(victimNO))
            return;

        _hit.Add(victimNO);

        Debug.Log($"[PushHitbox] Hit victim={victimNO.name} owner={victimNO.OwnerId} attacker={_attacker?.name} impulse={_impulse}");

        BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
        if (victimMove == null)
            return;

        // Tell the victim's owner client to apply the impulse.
        victimMove.ApplyPushImpulseTargetRpc(victimNO.Owner, _impulse);
    }
}
