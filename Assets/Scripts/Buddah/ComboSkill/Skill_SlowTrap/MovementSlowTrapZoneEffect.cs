using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side trap zone driven by trigger colliders parented to the caster.
/// The trigger object is supplied by a prefab (made by designer) and attached at runtime.
/// </summary>
public class MovementSlowTrapZoneEffect : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private float _zoneTimeLeft = 5f;
    [SerializeField] private float _slowForwardForce = 6f;
    [SerializeField] private float _slowMaxSpeed = 2f;
    [SerializeField] private float _slowDurationSeconds = 1.25f;
    [SerializeField] private float _perTargetReapplyCooldown = 0.75f;
    [SerializeField] private bool _affectCaster = false;
    [SerializeField] private LayerMask _targetLayers = ~0;

    private SkillExecutor _caster;
    private bool _initialized;
    private readonly Dictionary<SkillExecutor, float> _nextApplyAt = new();

    private GameObject _triggerInstance;
    private GameObject _triggerPrefabSource;
    private GameObject _debugReferenceInstance;
    private GameObject _debugReferencePrefabSource;

    public void ApplyOrRefresh(
        SkillExecutor caster,
        float zoneDurationSeconds,
        float slowForwardForce,
        float slowMaxSpeed,
        float slowDurationSeconds,
        float perTargetReapplyCooldown,
        bool affectCaster,
        LayerMask targetLayers,
        GameObject triggerColliderPrefab,
        GameObject debugReferencePrefab,
        Vector3 debugReferenceLocalOffset,
        Vector3 debugReferenceLocalEuler,
        Vector3 debugReferenceLocalScale)
    {
        float newZoneDuration = Mathf.Max(0.05f, zoneDurationSeconds);

        _caster = caster;
        float newSlowForwardForce = Mathf.Abs(slowForwardForce);
        float newSlowMaxSpeed = Mathf.Abs(slowMaxSpeed);
        float newSlowDuration = Mathf.Max(0.05f, slowDurationSeconds);
        float newReapplyCooldown = Mathf.Max(0f, perTargetReapplyCooldown);

        if (!_initialized)
        {
            _zoneTimeLeft = newZoneDuration;
            _slowForwardForce = newSlowForwardForce;
            _slowMaxSpeed = newSlowMaxSpeed;
            _slowDurationSeconds = newSlowDuration;
            _perTargetReapplyCooldown = newReapplyCooldown;
            _affectCaster = affectCaster;
            _targetLayers = targetLayers;
            _initialized = true;
        }
        else
        {
            _zoneTimeLeft = Mathf.Max(_zoneTimeLeft, newZoneDuration);
            _slowForwardForce = newSlowForwardForce;
            _slowMaxSpeed = newSlowMaxSpeed;
            _slowDurationSeconds = newSlowDuration;
            _perTargetReapplyCooldown = newReapplyCooldown;
            _affectCaster = affectCaster;
            _targetLayers = targetLayers;
        }

        EnsureTriggerInstance(triggerColliderPrefab);
        EnsureDebugReferenceInstance(debugReferencePrefab, debugReferenceLocalOffset, debugReferenceLocalEuler, debugReferenceLocalScale);
    }

    public void HandleTriggerTouch(Collider other)
    {
        if (_caster == null || !_caster.IsServerInitialized || other == null)
            return;

        if (((1 << other.gameObject.layer) & _targetLayers.value) == 0)
            return;

        SkillExecutor target = other.GetComponentInParent<SkillExecutor>();
        if (target == null || !target.IsServerInitialized)
            return;

        if (!_affectCaster && target == _caster)
            return;

        float now = Time.time;
        if (_nextApplyAt.TryGetValue(target, out float nextAt) && now < nextAt)
            return;

        float extraForwardForce = -Mathf.Abs(_slowForwardForce);
        float extraMaxSpeed = -Mathf.Abs(_slowMaxSpeed);
        target.ApplyAccelerationToOwner(extraForwardForce, extraMaxSpeed, _slowDurationSeconds);

        _nextApplyAt[target] = now + _perTargetReapplyCooldown;
    }

    private void Update()
    {
        if (_caster == null || !_caster.IsServerInitialized)
        {
            Destroy(this);
            return;
        }

        _zoneTimeLeft -= Time.deltaTime;
        if (_zoneTimeLeft <= 0f)
            Destroy(this);
    }

    private void EnsureTriggerInstance(GameObject triggerColliderPrefab)
    {
        if (_triggerInstance != null && _triggerPrefabSource == triggerColliderPrefab)
            return;

        if (_triggerInstance != null)
            Destroy(_triggerInstance);

        _triggerPrefabSource = triggerColliderPrefab;

        if (triggerColliderPrefab == null)
        {
            Debug.LogWarning("[MovementSlowTrapZoneEffect] Missing trigger collider prefab.");
            return;
        }

        _triggerInstance = Instantiate(triggerColliderPrefab, transform);
        _triggerInstance.name = $"{triggerColliderPrefab.name}_SlowTrapRuntime";
        _triggerInstance.transform.localPosition = Vector3.zero;
        _triggerInstance.transform.localRotation = Quaternion.identity;
        // Preserve prefab-authored size so the runtime trigger matches the designer's intended dimensions.
        _triggerInstance.transform.localScale = triggerColliderPrefab.transform.localScale;

        BindRelaysAndValidateColliders(_triggerInstance);
    }

    private void BindRelaysAndValidateColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            Debug.LogWarning("[MovementSlowTrapZoneEffect] Trigger prefab has no Collider.");
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;

            c.isTrigger = true;

            var relay = c.GetComponent<SlowTrapTriggerRelay>();
            if (relay == null)
                relay = c.gameObject.AddComponent<SlowTrapTriggerRelay>();

            relay.Initialize(this);
        }
    }

    private void EnsureDebugReferenceInstance(GameObject debugReferencePrefab, Vector3 localOffset, Vector3 localEuler, Vector3 localScale)
    {
        if (_debugReferenceInstance != null && _debugReferencePrefabSource == debugReferencePrefab)
        {
            _debugReferenceInstance.transform.localPosition = localOffset;
            _debugReferenceInstance.transform.localRotation = Quaternion.Euler(localEuler);
            _debugReferenceInstance.transform.localScale = GetDebugReferenceScaledSize(debugReferencePrefab, localScale);
            return;
        }

        if (_debugReferenceInstance != null)
            Destroy(_debugReferenceInstance);

        _debugReferencePrefabSource = debugReferencePrefab;

        if (debugReferencePrefab == null)
            return;

        _debugReferenceInstance = Instantiate(debugReferencePrefab, transform);
        _debugReferenceInstance.name = $"{debugReferencePrefab.name}_SlowTrapDebugRef";
        _debugReferenceInstance.transform.localPosition = localOffset;
        _debugReferenceInstance.transform.localRotation = Quaternion.Euler(localEuler);
        _debugReferenceInstance.transform.localScale = GetDebugReferenceScaledSize(debugReferencePrefab, localScale);
    }

    private static Vector3 GetDebugReferenceScaledSize(GameObject debugReferencePrefab, Vector3 scaleMultiplier)
    {
        Vector3 baseScale = (debugReferencePrefab != null) ? debugReferencePrefab.transform.localScale : Vector3.one;
        return Vector3.Scale(baseScale, scaleMultiplier);
    }

    private void OnDisable()
    {
        _nextApplyAt.Clear();

        if (_triggerInstance != null)
            Destroy(_triggerInstance);
        if (_debugReferenceInstance != null)
            Destroy(_debugReferenceInstance);

        _triggerInstance = null;
        _triggerPrefabSource = null;
        _debugReferenceInstance = null;
        _debugReferencePrefabSource = null;
    }
}
