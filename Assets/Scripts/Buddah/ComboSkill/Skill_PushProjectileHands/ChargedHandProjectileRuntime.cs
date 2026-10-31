using System.Collections.Generic;
using BuddahGo.VFX;
using FishNet;
using FishNet.Managing.Timing;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ChargedHandProjectileRuntime : MonoBehaviour
{
    private NetworkObject _attacker;
    private Vector3 _direction;
    private float _speed;
    private float _lockedY;
    private Vector3 _impulse;
    private float _buildUpDuration;
    private float _flightDuration;
    // One clock for every copy of a projectile (Docs/vfx/push-projectile-hands.md section 15): seconds since the server
    // spawned the hitbox, read from the network tick. Server hitbox and client visuals agree even across a frame hitch
    // (Time.time gains at most maximumDeltaTime per frame, the tick gains real time). Without a network (editor
    // previews) it falls back to Time.time.
    private PreciseTick _spawnTick;
    private PalmInkTimeline[] _inkTimelines;
    private bool _hasSpawnTick;
    private float _localSpawnTime;
    private float _hitTurnTorqueImpulse;
    private bool _applyHits;
    private bool _allowAttackerHit;
    private bool _armed;
    private Collider _hitbox;
    // Visual copies only: the layered skill effect (Docs/vfx), driven by the same timing as the hitbox.
    private SkillVfxPhaseController _skillVfx;
    private bool _dissipating;
    private Transform _followTarget;
    private Vector3 _followTargetStartPosition;
    private Vector3 _startPosition;
    private readonly HashSet<NetworkObject> _hitVictims = new();
    // Flight speed shape (ChargedProjectileFlight). Every caller passes the skill's curve; without one the palm stays
    // put (only an unconfigured client's empty fallback visual could get here).
    private float[] _flightDistanceFractions;
    private float _flightTravelled;

    private const float ProgressCompletionFraction = 0.8f;

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
        AnimationCurve speedCurve,
        PreciseTick? spawnTick = null)
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
        projectile.InitializeServer(attacker, attacker != null ? attacker.transform : null, startPos, direction, speed, buildUpDuration, flightLifetimeSeconds, impulse, hitTurnTorqueImpulse, allowAttackerHit, speedCurve, spawnTick);
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
        Transform followTarget = null,
        SkillVfxPhaseController.Context? skillVfxContext = null,
        AnimationCurve speedCurve = null,
        PreciseTick? spawnTick = null,
        Vector3? followTargetStart = null)
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

        projectile.InitializeVisual(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds, visualLocalEuler, visualScale, speedCurve, spawnTick, followTargetStart);
        projectile.StartSkillVfx(skillVfxContext, followTarget);
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
        AnimationCurve speedCurve,
        PreciseTick? spawnTick)
    {
        _attacker = attacker;
        _applyHits = true;
        _allowAttackerHit = allowAttackerHit;
        _impulse = impulse;
        _hitTurnTorqueImpulse = hitTurnTorqueImpulse;
        EnsureServerHitbox();
        ConfigureMotion(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds, speedCurve, spawnTick, null);
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
        AnimationCurve speedCurve,
        PreciseTick? spawnTick,
        Vector3? followTargetStart)
    {
        _applyHits = false;
        ConfigureMotion(startPos, followTarget, direction, speed, buildUpDuration, flightLifetimeSeconds, speedCurve, spawnTick, followTargetStart);
        if (_hitbox != null)
            _hitbox.enabled = false;

        transform.rotation *= Quaternion.Euler(visualLocalEuler);
        transform.localScale = Vector3.Scale(transform.localScale, visualScale);

        // Ink palm (VFX_Palm_Bullet / _Anti): its timeline plays the gather / flight / dissipate looks over this projectile's
        // timing; the moving is done here, not by the timeline. It draws in the skill-effect pass like the slot effects.
        _inkTimelines = GetComponentsInChildren<PalmInkTimeline>(true);
        if (_inkTimelines.Length > 0)
            SkillVfxPhaseController.MoveToCharacterLayer(transform);
        foreach (PalmInkTimeline timeline in _inkTimelines)
            timeline.ConfigureForProjectile(_buildUpDuration, _flightDuration, Elapsed());
    }

    private void StartSkillVfx(SkillVfxPhaseController.Context? context, Transform followTarget)
    {
        _skillVfx = GetComponent<SkillVfxPhaseController>();
        if (_skillVfx == null)
            return;

        SkillVfxPhaseController.Context resolved = context ?? new SkillVfxPhaseController.Context { caster = followTarget };
        _skillVfx.Configure(resolved);
        // what is left of the wind-up: a copy that starts late gets a shorter charge, then arms with the hitbox
        _skillVfx.BeginPhase(SkillVfxPhase.Chg, Mathf.Max(0.01f, _buildUpDuration - Elapsed()));
    }

    // spawnTick: the server's tick when it spawned the hitbox (the same value reaches the clients in the visual RPC).
    // followTargetStart: where the followed caster was at that moment, as the server saw it. A copy that spawns later
    // still follows the caster's movement since the server spawn, so it does not lag by what the caster drove meanwhile.
    private void ConfigureMotion(Vector3 startPos, Transform followTarget, Vector3 direction, float speed, float buildUpDuration, float flightLifetimeSeconds, AnimationCurve speedCurve, PreciseTick? spawnTick, Vector3? followTargetStart)
    {
        _flightDistanceFractions = speedCurve != null ? ChargedProjectileFlight.Cumulative(speedCurve) : null;
        _flightTravelled = 0f;
        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = transform.forward;

        _direction = planarDirection.normalized;
        _speed = Mathf.Max(0f, speed);
        _buildUpDuration = Mathf.Max(0f, buildUpDuration);
        _flightDuration = Mathf.Max(0.05f, flightLifetimeSeconds);
        _hasSpawnTick = spawnTick.HasValue && InstanceFinder.TimeManager != null;
        _spawnTick = spawnTick ?? default;
        _localSpawnTime = Time.time;
        _lockedY = startPos.y;
        _followTarget = followTarget;
        _followTargetStartPosition = followTargetStart ?? (followTarget != null ? followTarget.position : Vector3.zero);
        _startPosition = startPos;

        transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(_direction, Vector3.up));
    }

    private float Elapsed()
    {
        return _hasSpawnTick ? (float)InstanceFinder.TimeManager.TimePassed(_spawnTick) : Time.time - _localSpawnTime;
    }

    private void Update()
    {
        float elapsed = Elapsed();
        if (_inkTimelines != null)
            foreach (PalmInkTimeline timeline in _inkTimelines)
                timeline.time = Mathf.Min(elapsed, timeline.duration);   // the ink's look follows the same clock
        if (elapsed >= _buildUpDuration + _flightDuration)
        {
            if (_skillVfx != null && !_applyHits)
            {
                // The visual copy dissolves in place; the controller destroys it when every layer has finished.
                if (!_dissipating)
                {
                    _dissipating = true;
                    _skillVfx.BeginDissipation();
                }
                return;
            }

            Destroy(gameObject);
            return;
        }

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

        float moveDistance = GetFlightStep();
        if (moveDistance <= 0f)
            return;

        if (_applyHits)
            CheckVictimOverlaps();

        Vector3 nextPosition = transform.position + (_direction * moveDistance);
        nextPosition.y = _lockedY;
        transform.position = nextPosition;
        CheckVictimOverlaps();
    }

    // Distance to move this frame: where the speed shape's integral puts the palm now, minus where it is.
    private float GetFlightStep()
    {
        if (_flightDistanceFractions == null)
            return 0f;

        float flightElapsed = Mathf.Max(0f, Elapsed() - _buildUpDuration);
        float normalizedFlight = _flightDuration > 0f ? Mathf.Clamp01(flightElapsed / _flightDuration) : 1f;
        float target = _speed * _flightDuration * ChargedProjectileFlight.DistanceFraction(_flightDistanceFractions, normalizedFlight);
        float step = target - _flightTravelled;
        _flightTravelled = target;
        return step;
    }

    private void ArmProjectile()
    {
        _armed = true;
        if (_applyHits) BuddahGo.Match.SkillCombatEvents.Record("launched", "charged-projectile", _attacker, null, transform.position);
        SetProgress(1f);
        if (_skillVfx != null)
        {
            _skillVfx.EndPhase(SkillVfxPhase.Chg);
            _skillVfx.BeginPhase(SkillVfxPhase.Imp);
            _skillVfx.BeginPhase(SkillVfxPhase.Prj, _flightDuration);
        }
        FollowChargingTargetPosition();

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

    private void FollowChargingTargetPosition()
    {
        if (_followTarget == null)
            return;

        Vector3 translationDelta = _followTarget.position - _followTargetStartPosition;
        Vector3 nextPosition = _startPosition + translationDelta;
        nextPosition.y = _lockedY + translationDelta.y;
        transform.position = nextPosition;
    }

    // Charge progress for the slot effect (0..1, full at ProgressCompletionFraction of the wind-up).
    private void SetProgress(float value)
    {
        if (_skillVfx != null)
            _skillVfx.SetChargeProgress(value);
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

        // Phase 4b V3 — single dispatch entry (router internalizes V2 / PushTargetBox / Legacy fallback).
        bool routed = BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, BuddahPredictedImpulseSourceType.ChargedProjectile, _attacker);
        BuddahGo.Match.SkillCombatEvents.Record("hit", "charged-projectile", _attacker, victimNO, transform.position, routed);
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
