using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(Rigidbody))]
public class ChargedHandProjectileRuntime : MonoBehaviour
{
    private NetworkObject _attacker;
    private Vector3 _direction;
    private float _speed;
    private float _expireTime;
    private float _lockedY;
    private Vector3 _impulse;
    private float _buildUpDuration;
    private float _flightDuration;
    private float _spawnTime;
    private float _armTime;
    private float _hitTurnTorqueImpulse;
    private string _progressProperty = "Progress";
    private bool _applyHits;
    private bool _allowAttackerHit;
    private bool _ignoreSolidWorld;
    private bool _armed;
    private Collider _hitbox;
    private VisualEffect[] _visualEffects;
    private Transform _followTarget;
    private Vector3 _followTargetStartPosition;
    private Vector3 _startPosition;
    private GameObject _launchEffectPrefab;
    private Vector3 _launchEffectWorldOffset;
    private readonly HashSet<NetworkObject> _hitVictims = new();

    private const float ProgressCompletionFraction = 0.8f;
    private const float FlightEndSpeedFraction = 0.2f;

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

    public static ChargedHandProjectileRuntime SpawnServer(
        NetworkObject attacker,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float buildUpDuration,
        float flightLifetimeSeconds,
        Vector3 impulse,
        Vector3 colliderSize,
        float hitTurnTorqueImpulse,
        bool allowAttackerHit,
        bool ignoreSolidWorld)
    {
        GameObject root = new GameObject("ChargedHandProjectile_Server");
        root.transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(direction, Vector3.up));

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = SanitizeColliderSize(colliderSize);
        box.enabled = false;

        ChargedHandProjectileRuntime projectile = root.AddComponent<ChargedHandProjectileRuntime>();
        projectile.InitializeServer(attacker, attacker != null ? attacker.transform : null, startPos, direction, speed, buildUpDuration, flightLifetimeSeconds, impulse, hitTurnTorqueImpulse, allowAttackerHit, ignoreSolidWorld);
        return projectile;
    }

    public static ChargedHandProjectileRuntime SpawnVisual(
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float buildUpDuration,
        float flightLifetimeSeconds,
        GameObject visualPrefab,
        Vector3 visualLocalEuler,
        Vector3 visualScale,
        string progressProperty,
        Transform followTarget = null,
        GameObject launchEffectPrefab = null,
        Vector3? launchEffectWorldOffset = null)
    {
        GameObject root;
        if (visualPrefab != null)
        {
            root = Instantiate(visualPrefab, startPos, Quaternion.LookRotation(direction, Vector3.up));
            root.name = "ChargedHandProjectile_Visual";
        }
        else
        {
            root = new GameObject("ChargedHandProjectile_Visual");
            root.transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(direction, Vector3.up));
        }

        EnsureRuntimeBody(root);
        DisableAllColliders(root);

        ChargedHandProjectileRuntime projectile = root.GetComponent<ChargedHandProjectileRuntime>();
        if (projectile == null)
            projectile = root.AddComponent<ChargedHandProjectileRuntime>();

        projectile.InitializeVisual(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds, visualLocalEuler, visualScale, progressProperty, launchEffectPrefab, launchEffectWorldOffset ?? Vector3.zero);
        return projectile;
    }

    private void InitializeServer(
        NetworkObject attacker,
        Transform followTarget,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        float buildUpDuration,
        float flightLifetimeSeconds,
        Vector3 impulse,
        float hitTurnTorqueImpulse,
        bool allowAttackerHit,
        bool ignoreSolidWorld)
    {
        _attacker = attacker;
        _applyHits = true;
        _allowAttackerHit = allowAttackerHit;
        _ignoreSolidWorld = ignoreSolidWorld;
        _impulse = impulse;
        _hitTurnTorqueImpulse = hitTurnTorqueImpulse;
        EnsureServerHitbox();
        ConfigureMotion(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds);
        SetProgress(0f);

        if (!_allowAttackerHit)
            IgnoreAttackerColliders();
    }

    private void InitializeVisual(
        Vector3 startPos,
        Transform followTarget,
        Vector3 direction,
        float speed,
        float buildUpDuration,
        float flightLifetimeSeconds,
        Vector3 visualLocalEuler,
        Vector3 visualScale,
        string progressProperty,
        GameObject launchEffectPrefab,
        Vector3 launchEffectWorldOffset)
    {
        _applyHits = false;
        _progressProperty = string.IsNullOrWhiteSpace(progressProperty) ? "Progress" : progressProperty;
        ConfigureMotion(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds);
        _launchEffectPrefab = launchEffectPrefab;
        _launchEffectWorldOffset = launchEffectWorldOffset;
        if (_hitbox != null)
            _hitbox.enabled = false;

        transform.rotation *= Quaternion.Euler(visualLocalEuler);
        transform.localScale = Vector3.Scale(transform.localScale, visualScale);

        _visualEffects = GetComponentsInChildren<VisualEffect>(true);
        SetProgress(0f);
        PlayAllVisualEffects();
    }

    private void ConfigureMotion(Vector3 startPos, Transform followTarget, Vector3 direction, float speed, float buildUpDuration, float flightLifetimeSeconds)
    {
        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = transform.forward;

        _direction = planarDirection.normalized;
        _speed = Mathf.Max(0f, speed);
        _buildUpDuration = Mathf.Max(0f, buildUpDuration);
        _flightDuration = Mathf.Max(0.05f, flightLifetimeSeconds);
        _spawnTime = Time.time;
        _armTime = _spawnTime + _buildUpDuration;
        _expireTime = _armTime + _flightDuration;
        _lockedY = startPos.y;
        _followTarget = followTarget;
        _followTargetStartPosition = followTarget != null ? followTarget.position : Vector3.zero;
        _startPosition = startPos;

        transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(_direction, Vector3.up));
    }

    private void Update()
    {
        if (Time.time >= _expireTime)
        {
            Destroy(gameObject);
            return;
        }

        float elapsed = Time.time - _spawnTime;
        float progressDuration = _buildUpDuration * ProgressCompletionFraction;
        float buildProgress = progressDuration > 0f ? Mathf.Clamp01(elapsed / progressDuration) : 1f;
        SetProgress(buildProgress);

        if (!_armed)
        {
            FollowChargingTargetPosition();

            if (elapsed < _buildUpDuration)
                return;

            ArmProjectile();
        }

        float currentSpeed = GetCurrentFlightSpeed();
        float moveDistance = currentSpeed * Time.deltaTime;
        if (moveDistance <= 0f)
            return;

        if (_applyHits)
            CheckVictimOverlaps();

        Vector3 nextPosition = transform.position + (_direction * moveDistance);
        nextPosition.y = _lockedY;
        transform.position = nextPosition;
        CheckVictimOverlaps();
    }

    private float GetCurrentFlightSpeed()
    {
        if (_speed <= 0f)
            return 0f;

        float flightElapsed = Mathf.Max(0f, Time.time - _armTime);
        float normalizedFlight = _flightDuration > 0f ? Mathf.Clamp01(flightElapsed / _flightDuration) : 1f;
        float speedFraction = Mathf.Lerp(1f, FlightEndSpeedFraction, normalizedFlight);
        return _speed * speedFraction;
    }

    private void ArmProjectile()
    {
        _armed = true;
        SetProgress(1f);
        FollowChargingTargetPosition();
        SpawnLaunchEffect();

        if (_hitbox != null)
            _hitbox.enabled = _applyHits;

        Physics.SyncTransforms();
        CheckVictimOverlaps();
    }

    private void EnsureServerHitbox()
    {
        if (!_applyHits)
            return;

        if (_hitbox == null)
            _hitbox = GetComponent<Collider>();
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<Collider>(true);

        if (_hitbox == null)
        {
            BoxCollider createdBox = gameObject.AddComponent<BoxCollider>();
            createdBox.isTrigger = true;
            _hitbox = createdBox;
        }

        _hitbox.isTrigger = true;
        _hitbox.enabled = false;
    }

    private void SpawnLaunchEffect()
    {
        if (_launchEffectPrefab == null)
            return;

        Vector3 effectPosition = transform.position + _launchEffectWorldOffset;
        Instantiate(_launchEffectPrefab, effectPosition, Quaternion.LookRotation(_direction, Vector3.up));
    }

    private void FollowChargingTargetPosition()
    {
        if (_followTarget == null)
            return;

        Vector3 translationDelta = _followTarget.position - _followTargetStartPosition;
        Vector3 nextPosition = _startPosition + translationDelta;
        nextPosition.y = _lockedY + translationDelta.y;
        transform.position = nextPosition;
    }

    private void SetProgress(float value)
    {
        if (_visualEffects == null || _visualEffects.Length == 0)
            return;

        for (int i = 0; i < _visualEffects.Length; i++)
        {
            VisualEffect visualEffect = _visualEffects[i];
            if (visualEffect == null || !visualEffect.HasFloat(_progressProperty))
                continue;

            visualEffect.SetFloat(_progressProperty, value);
        }
    }

    private void PlayAllVisualEffects()
    {
        if (_visualEffects == null)
            return;

        for (int i = 0; i < _visualEffects.Length; i++)
        {
            if (_visualEffects[i] == null)
                continue;

            _visualEffects[i].Reinit();
            _visualEffects[i].Play();
        }
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
        if (!_armed || !_applyHits || !InstanceFinder.IsServerStarted || other == null)
            return;

        NetworkObject victimNO = other.GetComponentInParent<NetworkObject>();
        if (victimNO == null)
            return;

        if (!_allowAttackerHit && victimNO == _attacker)
            return;

        if (_hitVictims.Contains(victimNO))
            return;

        _hitVictims.Add(victimNO);

        if (BuddahPredictionCombatRouting.TryRouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, BuddahPredictedImpulseSourceType.ChargedProjectile, _attacker))
            return;

        BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
        if (victimMove != null)
            victimMove.ApplyPushImpulseAndTorqueTargetRpc(victimNO.Owner, _impulse, _hitTurnTorqueImpulse);
    }

    private bool TryStopAtSolidWorld(float moveDistance)
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            return false;

        RaycastHit[] hits = body.SweepTestAll(_direction, moveDistance, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        float nearestDistance = float.MaxValue;
        bool foundBlockingHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (ShouldIgnoreSolidHit(hitCollider))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hits[i].distance);
            foundBlockingHit = true;
        }

        if (!foundBlockingHit)
            return false;

        Vector3 nextPosition = transform.position + (_direction * Mathf.Max(0f, nearestDistance - 0.01f));
        nextPosition.y = _lockedY;
        transform.position = nextPosition;
        Destroy(gameObject);
        return true;
    }

    private void CheckVictimOverlaps()
    {
        if (!_armed || !_applyHits || !InstanceFinder.IsServerStarted || _hitbox == null)
            return;

        if (_hitbox is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, box.transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
                TryApplyHit(overlaps[i]);
            return;
        }

        Collider[] fallbackOverlaps = Physics.OverlapSphere(_hitbox.bounds.center, _hitbox.bounds.extents.magnitude, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < fallbackOverlaps.Length; i++)
            TryApplyHit(fallbackOverlaps[i]);
    }

    private bool ShouldIgnoreSolidHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        if (_hitbox != null && hitCollider == _hitbox)
            return true;

        if (hitCollider.isTrigger)
            return true;

        NetworkObject hitNetworkObject = hitCollider.GetComponentInParent<NetworkObject>();
        if (hitNetworkObject != null)
            return true;

        return false;
    }

    private void IgnoreAttackerColliders()
    {
        if (_attacker == null || _hitbox == null)
            return;

        Collider[] attackerColliders = _attacker.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < attackerColliders.Length; i++)
        {
            Collider attackerCollider = attackerColliders[i];
            if (attackerCollider != null)
                Physics.IgnoreCollision(_hitbox, attackerCollider, true);
        }
    }

    private static void EnsureRuntimeBody(GameObject root)
    {
        if (root.GetComponent<Rigidbody>() == null)
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private static void DisableAllColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private static Vector3 SanitizeColliderSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.05f, Mathf.Abs(size.x)),
            Mathf.Max(0.05f, Mathf.Abs(size.y)),
            Mathf.Max(0.05f, Mathf.Abs(size.z)));
    }
}
