using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

public class HandPushProjectileRuntime : MonoBehaviour
{
    private NetworkObject _attacker;
    private Vector3 _direction;
    private float _speed;
    private float _expireTime;
    private float _lockedY;
    private Vector3 _impulse;
    private bool _applyHits;
    private bool _allowAttackerHit;
    private Collider _hitbox;
    private readonly HashSet<NetworkObject> _hitVictims = new();

    private void Awake()
    {
        _hitbox = GetComponent<Collider>();
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<Collider>(true);

        if (_hitbox != null)
            _hitbox.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public static HandPushProjectileRuntime SpawnServer(
        NetworkObject attacker,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float lifetimeSeconds,
        Vector3 impulse,
        GameObject projectilePrefab,
        Vector3 colliderSize,
        bool allowAttackerHit)
    {
        GameObject root = CreateProjectileRoot(projectilePrefab, startPos, direction, "Server");
        EnsureRuntimeBody(root);
        bool usingGeneratedCollider = EnsureProjectileCollider(root, projectilePrefab == null);
        SetAllRenderersEnabled(root, false);

        HandPushProjectileRuntime projectile = root.GetComponent<HandPushProjectileRuntime>();
        if (projectile == null)
            projectile = root.AddComponent<HandPushProjectileRuntime>();

        projectile.InitializeServer(attacker, startPos, direction, speed, lifetimeSeconds, impulse, colliderSize, usingGeneratedCollider, allowAttackerHit);
        return projectile;
    }

    public static HandPushProjectileRuntime SpawnVisual(
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float lifetimeSeconds,
        GameObject visualPrefab,
        Vector3 visualLocalEuler,
        Vector3 visualScale)
    {
        GameObject root = CreateProjectileRoot(visualPrefab, startPos, direction, "Visual");
        EnsureRuntimeBody(root);
        EnsureProjectileCollider(root, visualPrefab == null);

        HandPushProjectileRuntime projectile = root.GetComponent<HandPushProjectileRuntime>();
        if (projectile == null)
            projectile = root.AddComponent<HandPushProjectileRuntime>();
        projectile.InitializeVisual(startPos, direction, speed, lifetimeSeconds, visualPrefab, visualLocalEuler, visualScale);
        return projectile;
    }

    private void InitializeServer(
        NetworkObject attacker,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float lifetimeSeconds,
        Vector3 impulse,
        Vector3 colliderSize,
        bool usingGeneratedCollider,
        bool allowAttackerHit)
    {
        _attacker = attacker;
        _applyHits = true;
        _allowAttackerHit = allowAttackerHit;
        ConfigureMotion(startPos, direction, speed, lifetimeSeconds);
        _impulse = impulse;

        SetAllProjectileCollidersTrigger(true);

        if (usingGeneratedCollider && _hitbox is BoxCollider box)
            box.size = colliderSize;

        if (!_allowAttackerHit)
            IgnoreAttackerColliders();
    }

    private void InitializeVisual(
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float lifetimeSeconds,
        GameObject visualPrefab,
        Vector3 visualLocalEuler,
        Vector3 visualScale)
    {
        _applyHits = false;
        ConfigureMotion(startPos, direction, speed, lifetimeSeconds);

        SetAllProjectileCollidersTrigger(true);
        DisableAllProjectileColliders(gameObject);

        GameObject visualInstance = visualPrefab != null
            ? gameObject
            : CreatePlaceholderHandVisual();

        if (visualPrefab == null)
        {
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.Euler(visualLocalEuler);
            visualInstance.transform.localScale = Vector3.Scale(visualInstance.transform.localScale, visualScale);
        }
        else
        {
            transform.rotation *= Quaternion.Euler(visualLocalEuler);
            transform.localScale = Vector3.Scale(transform.localScale, visualScale);
        }

        DisableAllChildColliders(visualInstance);
    }

    private void ConfigureMotion(Vector3 startPos, Vector3 direction, float speed, float lifetimeSeconds)
    {
        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = transform.forward;

        _direction = planarDirection.normalized;
        _speed = Mathf.Max(0f, speed);
        _expireTime = Time.time + Mathf.Max(0.05f, lifetimeSeconds);
        _lockedY = startPos.y;

        transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(_direction, Vector3.up));
    }

    private void Update()
    {
        if (Time.time >= _expireTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 nextPosition = transform.position + (_direction * (_speed * Time.deltaTime));
        nextPosition.y = _lockedY;
        transform.position = nextPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyHit(other);
    }

    private void TryApplyHit(Collider other)
    {
        if (!_applyHits || !InstanceFinder.IsServerStarted || other == null)
            return;

        NetworkObject victimNO = other.GetComponentInParent<NetworkObject>();
        if (victimNO == null)
            return;

        if (!_allowAttackerHit && victimNO == _attacker)
            return;

        if (_hitVictims.Contains(victimNO))
            return;

        _hitVictims.Add(victimNO);

        BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
        if (victimMove == null)
            return;

        victimMove.ApplyPushImpulseTargetRpc(victimNO.Owner, _impulse);
    }

    private void IgnoreAttackerColliders()
    {
        if (_attacker == null || _hitbox == null)
            return;

        Collider[] attackerColliders = _attacker.GetComponentsInChildren<Collider>(true);
        Collider[] projectileColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < projectileColliders.Length; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (projectileCollider == null)
                continue;

            for (int j = 0; j < attackerColliders.Length; j++)
            {
                Collider attackerCollider = attackerColliders[j];
                if (attackerCollider == null)
                    continue;

                Physics.IgnoreCollision(projectileCollider, attackerCollider, true);
            }
        }
    }

    private static GameObject CreateProjectileRoot(GameObject prefab, Vector3 startPos, Vector3 direction, string suffix)
    {
        GameObject root;
        if (prefab != null)
        {
            root = Instantiate(prefab, startPos, Quaternion.LookRotation(direction, Vector3.up));
            root.name = $"{prefab.name}_{suffix}";
        }
        else
        {
            root = new GameObject($"HandPushProjectile_{suffix}");
            root.transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(direction, Vector3.up));
        }

        return root;
    }

    private static void EnsureRuntimeBody(GameObject root)
    {
        if (root.GetComponent<Rigidbody>() == null)
            root.AddComponent<Rigidbody>();
    }

    private static bool EnsureProjectileCollider(GameObject root, bool createIfMissing)
    {
        Collider collider = root.GetComponent<Collider>();
        if (collider == null)
            collider = root.GetComponentInChildren<Collider>(true);

        if (collider != null || !createIfMissing)
            return createIfMissing && collider == null;

        root.AddComponent<BoxCollider>();
        return true;
    }

    private static void DisableAllChildColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void DisableAllProjectileColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void SetAllProjectileCollidersTrigger(bool isTrigger)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].isTrigger = isTrigger;
        }
    }

    private static void SetAllRenderersEnabled(GameObject root, bool enabled)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private GameObject CreatePlaceholderHandVisual()
    {
        GameObject root = new("GeneratedHandVisual");
        root.transform.SetParent(transform, false);

        CreatePrimitiveChild(root.transform, "Palm", new Vector3(0f, 0f, 0f), new Vector3(0.75f, 0.25f, 0.9f));
        CreatePrimitiveChild(root.transform, "Finger_1", new Vector3(-0.24f, 0.18f, 0.45f), new Vector3(0.12f, 0.18f, 0.42f));
        CreatePrimitiveChild(root.transform, "Finger_2", new Vector3(-0.08f, 0.2f, 0.5f), new Vector3(0.12f, 0.2f, 0.48f));
        CreatePrimitiveChild(root.transform, "Finger_3", new Vector3(0.08f, 0.2f, 0.5f), new Vector3(0.12f, 0.2f, 0.48f));
        CreatePrimitiveChild(root.transform, "Finger_4", new Vector3(0.24f, 0.18f, 0.45f), new Vector3(0.12f, 0.18f, 0.42f));
        CreatePrimitiveChild(root.transform, "Thumb", new Vector3(0.42f, 0f, -0.1f), new Vector3(0.18f, 0.16f, 0.42f), new Vector3(0f, 32f, -45f));

        return root;
    }

    private static void CreatePrimitiveChild(
        Transform parent,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Vector3? localEuler = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
    }
}
