using System.Collections.Generic;
using BuddahGo.Match;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

/// <summary>
/// Pure rule behind <see cref="BuddahBodyImpact"/> (unit-tested): an Arm opens the window [now, now + seconds) with a
/// fresh victim set; a contact hits only inside the window while the caster's motor burst is active, and each victim
/// at most once per Arm. The direction is the horizontal line from caster to victim (Q9: sideways scatter).
/// </summary>
internal sealed class BuddahBodyImpactRule
{
    private const float MinFlatSqrMagnitude = 0.0001f;

    private readonly HashSet<int> _hitVictims = new HashSet<int>();
    private float _armedUntil = float.NegativeInfinity;

    public float ImpulseMagnitude { get; private set; }

    public void Arm(float impulseMagnitude, float seconds, float now)
    {
        _hitVictims.Clear();
        ImpulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        _armedUntil = seconds > 0f ? now + seconds : float.NegativeInfinity;
    }

    public bool IsArmed(float now) => ImpulseMagnitude > 0f && now < _armedUntil;

    /// <summary>True exactly once per victim per Arm; a refused contact (window closed, no burst) does not use up the victim.</summary>
    public bool TryClaimHit(int victimKey, float now, bool casterBursting)
    {
        if (!casterBursting || !IsArmed(now))
            return false;
        return _hitVictims.Add(victimKey);
    }

    public static Vector3 ResolveImpulse(Vector3 casterPosition, Vector3 victimPosition, Vector3 casterForward, float magnitude)
    {
        Vector3 direction = victimPosition - casterPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < MinFlatSqrMagnitude)
        {
            // Victim stacked on the caster: push it along the caster's flat heading instead.
            direction = casterForward;
            direction.y = 0f;
            if (direction.sqrMagnitude < MinFlatSqrMagnitude)
                direction = Vector3.forward;
        }
        return direction.normalized * magnitude;
    }
}

/// <summary>
/// 金足破阵 (#4) body impact, server only: added at runtime to the caster by Yuanqi_GiantCharge and armed for the burst.
/// While armed and while the caster's motor burst is active, touching another racer (training dummies included)
/// routes one impulse to it through BuddahPredictionRouter: magnitude given by Arm, direction horizontal from the
/// caster to the victim, once per victim per Arm. Frozen victims stay put (the motor drops impulses while rooted).
/// Every hit is also presented on every peer through SkillExecutor.YuanqiImpactObserved (victim, point, impulse).
/// The component stays on the caster between bursts; contacts are ignored while it is not armed.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuddahBodyImpact : MonoBehaviour
{
    private const string EvidenceKind = "body-impact";

    private readonly BuddahBodyImpactRule _rule = new BuddahBodyImpactRule();
    private SkillExecutor _caster;
    private BuddahPredictedMotor _motor;
    // The 缘起 that armed this burst, reported with each impact (YuanqiImpactInfo.YuanqiId).
    private string _sourceYuanqiId = string.Empty;

    /// <param name="sourceYuanqiId">The arming 缘起 (presentation only), e.g. giant_charge.</param>
    public void Arm(float impulseMagnitude, float seconds, string sourceYuanqiId = null)
    {
        ResolveRefs();
        if (_caster == null || !_caster.IsServerInitialized)
            return;

        _rule.Arm(impulseMagnitude, seconds, Time.time);
        _sourceYuanqiId = sourceYuanqiId ?? string.Empty;
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][BodyImpact] '{name}' armed by '{_sourceYuanqiId}': impulse={_rule.ImpulseMagnitude:0.#} for {seconds:0.##}s");
    }

    private void OnCollisionEnter(Collision collision) => TryImpact(collision);

    // Stay covers a racer already in contact when the burst starts.
    private void OnCollisionStay(Collision collision) => TryImpact(collision);

    private void TryImpact(Collision collision)
    {
        float now = Time.time;
        if (collision == null || !_rule.IsArmed(now))
            return;
        if (_caster == null || !_caster.IsServerInitialized)
            return;

        Collider other = collision.collider;
        SkillExecutor victim = other != null ? other.GetComponentInParent<SkillExecutor>() : null;
        if (victim == null || victim == _caster)
            return;

        NetworkObject victimObject = victim.NetworkObject;
        if (victimObject == null || !victimObject.IsSpawned)
            return;

        if (!_rule.TryClaimHit(victimObject.ObjectId, now, IsCasterBursting()))
            return;

        Vector3 impulse = BuddahBodyImpactRule.ResolveImpulse(
            _caster.transform.position, victim.transform.position, _caster.transform.forward, _rule.ImpulseMagnitude);
        bool routed = BuddahPredictionRouter.RouteImpulse(victimObject, impulse, 0f, BuddahPredictedImpulseSourceType.BodyImpact, _caster.NetworkObject);
        Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : victim.transform.position;
        SkillCombatEvents.Record("hit", EvidenceKind, _caster.NetworkObject, victimObject, contactPoint, routed);
        // Presentation of every claimed hit, routed or not (the body did hit; a frozen victim just does not move).
        _caster.PlayYuanqiImpactObservers(_sourceYuanqiId, victimObject.ObjectId, contactPoint, impulse);
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][BodyImpact] '{name}' hit '{victim.name}' impulse={impulse} routed={routed}");
    }

    private bool IsCasterBursting()
    {
        ResolveRefs();
        return _motor != null && _motor.CurrentComputedStats.IsBurstActive;
    }

    private void ResolveRefs()
    {
        if (_caster == null) _caster = GetComponent<SkillExecutor>();
        if (_motor == null) _motor = GetComponent<BuddahPredictedMotor>();
    }
}
