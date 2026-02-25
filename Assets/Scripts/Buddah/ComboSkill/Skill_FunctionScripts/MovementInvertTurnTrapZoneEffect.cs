using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side trap zone that applies turn-input inversion to touched targets.
/// </summary>
public class MovementInvertTurnTrapZoneEffect : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private float _zoneTimeLeft = 5f;
    [SerializeField] private float _invertDurationSeconds = 1.25f;
    [SerializeField] private float _perTargetReapplyCooldown = 0.75f;
    [SerializeField] private bool _affectCaster = false;
    [SerializeField] private LayerMask _targetLayers = ~0;

    private SkillExecutor _caster;
    private bool _initialized;
    private readonly Dictionary<SkillExecutor, float> _nextApplyAt = new();

    private GameObject _triggerInstance;
    private GameObject _triggerPrefabSource;

    public void ApplyOrRefresh(
        SkillExecutor caster,
        float zoneDurationSeconds,
        float invertDurationSeconds,
        float perTargetReapplyCooldown,
        bool affectCaster,
        LayerMask targetLayers,
        GameObject triggerColliderPrefab)
    {
        float newZoneDuration = Mathf.Max(0.05f, zoneDurationSeconds);

        _caster = caster;
        float newInvertDuration = Mathf.Max(0.05f, invertDurationSeconds);
        float newReapplyCooldown = Mathf.Max(0f, perTargetReapplyCooldown);

        if (!_initialized)
        {
            _zoneTimeLeft = newZoneDuration;
            _invertDurationSeconds = newInvertDuration;
            _perTargetReapplyCooldown = newReapplyCooldown;
            _affectCaster = affectCaster;
            _targetLayers = targetLayers;
            _initialized = true;
        }
        else
        {
            _zoneTimeLeft = Mathf.Max(_zoneTimeLeft, newZoneDuration);
            _invertDurationSeconds = newInvertDuration;
            _perTargetReapplyCooldown = newReapplyCooldown;
            _affectCaster = affectCaster;
            _targetLayers = targetLayers;
        }

        EnsureTriggerInstance(triggerColliderPrefab);
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

        target.ApplyInvertTurnInputToOwner(_invertDurationSeconds);
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
            Debug.LogWarning("[MovementInvertTurnTrapZoneEffect] Missing trigger collider prefab.");
            return;
        }

        _triggerInstance = Instantiate(triggerColliderPrefab, transform);
        _triggerInstance.name = $"{triggerColliderPrefab.name}_InvertTurnTrapRuntime";
        _triggerInstance.transform.localPosition = Vector3.zero;
        _triggerInstance.transform.localRotation = Quaternion.identity;
        _triggerInstance.transform.localScale = triggerColliderPrefab.transform.localScale;

        BindRelaysAndValidateColliders(_triggerInstance);
    }

    private void BindRelaysAndValidateColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            Debug.LogWarning("[MovementInvertTurnTrapZoneEffect] Trigger prefab has no Collider.");
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;

            c.isTrigger = true;

            var relay = c.GetComponent<InvertTurnTrapTriggerRelay>();
            if (relay == null)
                relay = c.gameObject.AddComponent<InvertTurnTrapTriggerRelay>();

            relay.Initialize(this);
        }
    }

    private void OnDisable()
    {
        _nextApplyAt.Clear();

        if (_triggerInstance != null)
            Destroy(_triggerInstance);

        _triggerInstance = null;
        _triggerPrefabSource = null;
    }
}
