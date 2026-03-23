using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkillExecutor : NetworkBehaviour
{
    private const float CastConfirmDelaySeconds = 1f;
    private const float AntiCastConfirmDelaySeconds = 1f;
    private const string SkillReflectionSharedFeelEventId = "Skill_Reflection_Shared";
    private const string SkillReflectionLocalFeelEventId = "Skill_Reflection_Local";
    private const string AntiReflectionSharedFeelEventId = "Anti_Reflection_Shared";
    private const string AntiReflectionLocalFeelEventId = "Anti_Reflection_Local";
    private const string AntiAnimatorTriggerName = "IsAnti";

    [Header("Refs")]
    [SerializeField] private ComboSkillInput comboInput;
    [SerializeField] private SkillLoadout loadout;
    [SerializeField] private SkillDatabase database;
    [SerializeField] private SkillFeelRouter feelRouter;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private PlayerAccelerationTrail accelerationTrailController;
    [SerializeField] private PlayerCamera playerCamera;
    private float _castLockedUntil;
    private readonly Dictionary<string, int> _feelStopTokens = new();
    private Coroutine _cameraFovPulseRoutine;

    private float[] _nextReadyTime; // server cooldown tracking

    /// <summary>Cached reference.</summary>
    private ObsessionFigure _obs;

    private static readonly int AntiAnimatorTriggerHash = Animator.StringToHash(AntiAnimatorTriggerName);

    private void Awake()
    {
        _nextReadyTime = new float[SkillLoadout.SlotCount];
        ResolveObsessionFigure();
        ResolveFeelRouter();
        ResolveCharacterAnimator();
        ResolvePlayerCamera();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveObsessionFigure();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        if (comboInput == null) comboInput = GetComponent<ComboSkillInput>();
        if (loadout == null) loadout = GetComponent<SkillLoadout>();
        ResolveFeelRouter();

        if (comboInput != null)
            comboInput.OnSkillSlotTriggered += OnSlotTriggeredByCombo;
        else
            Debug.LogWarning("[SkillExecutor] Missing ComboSkillInput reference.");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (comboInput != null)
            comboInput.OnSkillSlotTriggered -= OnSlotTriggeredByCombo;
    }

    private void OnSlotTriggeredByCombo(int slotIndex, string comboName)
    {
        // Runs only on the owning client.
        Debug.Log($"[SkillExecutor][Owner] Combo '{comboName}' triggered slot {slotIndex}, requesting cast...");
        RequestCast(slotIndex);
    }

    public void RequestCast(int slotIndex)
    {
        if (!IsOwner) return;
        CastSlotServerRpc(slotIndex);
    }

    [ServerRpc(RequireOwnership = true)]
    private void CastSlotServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount) return;
        if (loadout == null || database == null)
        {
            Debug.LogWarning("[SkillExecutor][Server] Missing loadout/database.");
            return;
        }

        string skillId = loadout.GetSkillId(slotIndex);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.Log($"[SkillExecutor][Server] Slot {slotIndex} is empty, cast ignored.");
            return;
        }

        if (!database.TryGet(skillId, out SkillAction skill) || skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Unknown skillId '{skillId}' (slot {slotIndex}).");
            return;
        }

        float now = (float)Time.time;
        if (now < _castLockedUntil) return;

        if (now < _nextReadyTime[slotIndex])
        {
            Debug.Log($"[SkillExecutor][Server] Skill '{skillId}' on cooldown. Ready in {(_nextReadyTime[slotIndex] - now):0.00}s");
            return;
        }

        ResolveObsessionFigure();
        float obsessionNow = (_obs != null) ? _obs.Current : 0f;
        float backfirePercent = (_obs != null) ? _obs.GetBackfireProbabilityPercent(obsessionNow) : 0f;
        string resolvedAntiSkillId = ResolveAntiSkillId(skillId, skill);
        bool hasResolvableAnti = !string.IsNullOrWhiteSpace(resolvedAntiSkillId);

        bool isAnti = false;
        SkillAction executedSkill = skill;
        string executedSkillId = skillId;

        if (hasResolvableAnti && backfirePercent > 0.0001f)
        {
            float roll = Random.value * 100f;
            isAnti = roll < backfirePercent;

            if (isAnti)
            {
                if (database.TryGet(resolvedAntiSkillId, out SkillAction antiSkill) && antiSkill != null)
                {
                    executedSkill = antiSkill;
                    executedSkillId = resolvedAntiSkillId;
                }
                else
                {
                    Debug.LogWarning($"[SkillExecutor][Server] Anti skillId '{resolvedAntiSkillId}' not found for '{skillId}'. Fallback normal.");
                    isAnti = false;
                }
            }

            Debug.Log($"[SkillExecutor][Server] Backfire roll: skill='{skillId}', anti='{resolvedAntiSkillId}', obsession={obsessionNow:0.###}, p={backfirePercent:0.###}%, roll={roll:0.###} -> anti={(isAnti ? "YES" : "NO")}");
        }

        float castDelaySeconds = isAnti ? AntiCastConfirmDelaySeconds : CastConfirmDelaySeconds;
        float triggerAt = now + castDelaySeconds;

        // Cooldown and cast lock start when the delayed cast actually fires.
        _castLockedUntil = triggerAt + executedSkill.castLockSeconds;
        _nextReadyTime[slotIndex] = triggerAt + executedSkill.cooldownSeconds;

        float obsessionGain = Mathf.Max(0f, skill.ObsessionGain);
        Debug.Log($"[SkillExecutor][Server] QUEUE '{executedSkillId}' (slot {slotIndex}) delay={castDelaySeconds:0.##}s cooldown={executedSkill.cooldownSeconds:0.##} lock={executedSkill.castLockSeconds:0.##} anti={isAnti}");
        PlayQueuedCastFeedbackObserversRpc(executedSkillId, isAnti);
        StartCoroutine(ExecuteQueuedCastAfterDelay(slotIndex, executedSkill, executedSkillId, obsessionGain, isAnti, castDelaySeconds));
    }

    private IEnumerator ExecuteQueuedCastAfterDelay(int slotIndex, SkillAction executedSkill, string executedSkillId, float obsessionGain, bool isAnti, float castDelaySeconds)
    {
        yield return new WaitForSeconds(castDelaySeconds);

        if (executedSkill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Delayed cast '{executedSkillId}' lost its SkillAction reference.");
            yield break;
        }

        Debug.Log($"[SkillExecutor][Server] CAST '{executedSkillId}' (slot {slotIndex}) after {castDelaySeconds:0.##}s delay.");

        executedSkill.ExecuteServer(this, slotIndex);

        // Obsession gain follows the original skill, not the anti variant.
        _obs?.AddServer(obsessionGain);

        CastObserversRpc(slotIndex, executedSkillId, isAnti);
    }

    [ObserversRpc]
    private void PlayQueuedCastFeedbackObserversRpc(string executedSkillId, bool isAnti)
    {
        if (isAnti)
        {
            TriggerAntiAnimationLocal();
            PlayFeelLocal(AntiReflectionSharedFeelEventId);
        }
        else
        {
            PlayFeelLocal(SkillReflectionSharedFeelEventId);
        }

        Debug.Log($"[SkillExecutor][PreCastFeedback] '{executedSkillId}' anti={isAnti}, IsOwner={IsOwner}");

        if (!IsOwner)
            return;

        PlayFeelLocal(isAnti ? AntiReflectionLocalFeelEventId : SkillReflectionLocalFeelEventId);
    }

    [ObserversRpc]
    private void CastObserversRpc(int slotIndex, string executedSkillId, bool isAnti)
    {
        if (database == null) return;

        string ownerClientId = Owner != null ? Owner.ClientId.ToString() : "null";
        string localClientId = LocalConnection != null ? LocalConnection.ClientId.ToString() : "null";
        int objectId = NetworkObject != null ? NetworkObject.ObjectId : 0;
        Debug.Log($"[SkillExecutor][ObserversRpc] object='{name}', objectId={objectId}, slot={slotIndex}, skill='{executedSkillId}', anti={isAnti}, IsOwner={IsOwner}, IsClientInitialized={IsClientInitialized}, ownerClientId={ownerClientId}, localClientId={localClientId}");

        if (database.TryGet(executedSkillId, out SkillAction skill) && skill != null)
        {
            Debug.Log($"[SkillExecutor][Observers] '{executedSkillId}' played (slot {slotIndex}) [anti={isAnti}]");
            skill.ExecuteObservers(this, slotIndex, isAnti, IsOwner);
        }
    }

    public void ApplyAccelerationToOwner(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyAccelerationTargetRpc(conn, extraForwardForce, extraMaxSpeed, durationSeconds);
    }

    [TargetRpc]
    private void ApplyAccelerationTargetRpc(NetworkConnection conn, float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        // Runs only on the owning client.
        var move = GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement.");
            return;
        }

        if (move.IsSkillRooted)
        {
            Debug.Log($"[SkillExecutor][Target] Accel ignored because rooted. ({extraForwardForce}, {extraMaxSpeed}, {durationSeconds}s)");
            return;
        }

        var effect = GetComponent<MovementAccelerationEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<MovementAccelerationEffect>();

        effect.ApplyOrRefresh(move, extraForwardForce, extraMaxSpeed, durationSeconds);
        PlayFeelLocal("acceleration_local");

        if (extraForwardForce > 0f || extraMaxSpeed > 0f)
            ShowAccelerationTrail(durationSeconds);

        Debug.Log($"[SkillExecutor][Target] Accel: +{extraForwardForce} forwardForce, +{extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }

    public void ApplyRootThenAccelerationToOwner(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyRootThenAccelerationTargetRpc(conn, rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);
    }

    [TargetRpc]
    private void ApplyRootThenAccelerationTargetRpc(NetworkConnection conn, float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        var move = GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement for root-then-accel.");
            return;
        }

        var effect = GetComponent<MovementRootThenAccelerationEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<MovementRootThenAccelerationEffect>();

        effect.ApplyOrRestart(move, rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);

        Debug.Log($"[SkillExecutor][Target] RootThenAccel: root={rootDurationSeconds}s, accel=({extraForwardForce},{extraMaxSpeed}) for {accelDurationSeconds}s");
    }

    public void ApplyInvertTurnInputToOwner(float durationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyInvertTurnInputTargetRpc(conn, durationSeconds);
    }

    [TargetRpc]
    private void ApplyInvertTurnInputTargetRpc(NetworkConnection conn, float durationSeconds)
    {
        var move = GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement for invert-turn.");
            return;
        }

        var effect = GetComponent<MovementInvertTurnInputEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<MovementInvertTurnInputEffect>();

        effect.ApplyOrRefresh(move, durationSeconds);

        Debug.Log($"[SkillExecutor][Target] InvertTurnInput for {durationSeconds}s");
    }

    public void ApplyScaleToOwner(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyScaleTargetRpc(conn, scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds);
    }

    [TargetRpc]
    private void ApplyScaleTargetRpc(NetworkConnection conn, float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds)
    {
        var effect = GetComponent<PlayerScaleEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<PlayerScaleEffect>();

        effect.ApplyOrRefresh(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds);

        Debug.Log($"[SkillExecutor][Target] Scale x{scaleMultiplier:0.##} for {durationSeconds}s (enter={enterDurationSeconds:0.##}, restore={restoreDurationSeconds:0.##})");
    }

    private void ResolveObsessionFigure()
    {
        if (_obs != null)
            return;

        _obs = GetComponent<ObsessionFigure>();
        if (_obs == null)
            _obs = GetComponentInParent<ObsessionFigure>();
        if (_obs == null)
            _obs = GetComponentInChildren<ObsessionFigure>(true);
    }

    public void PlayFeelLocal(string eventId)
    {
        ResolveFeelRouter();

        if (feelRouter == null)
            return;

        feelRouter.Play(eventId);
    }

    private void ResolveFeelRouter()
    {
        if (feelRouter != null)
            return;

        feelRouter = GetComponent<SkillFeelRouter>();
        if (feelRouter != null)
            return;

        feelRouter = GetComponentInChildren<SkillFeelRouter>(true);
        if (feelRouter != null)
            return;

        feelRouter = GetComponentInParent<SkillFeelRouter>();
    }

    private void TriggerAntiAnimationLocal()
    {
        ResolveCharacterAnimator();

        if (characterAnimator == null)
            return;

        characterAnimator.ResetTrigger(AntiAnimatorTriggerHash);
        characterAnimator.SetTrigger(AntiAnimatorTriggerHash);
    }

    private void ResolveCharacterAnimator()
    {
        if (characterAnimator != null)
            return;

        characterAnimator = GetComponent<Animator>();
        if (characterAnimator != null)
            return;

        characterAnimator = GetComponentInChildren<Animator>(true);
        if (characterAnimator != null)
            return;

        characterAnimator = GetComponentInParent<Animator>();
    }

    private void ShowAccelerationTrail(float durationSeconds)
    {
        ResolveAccelerationTrailController();

        if (accelerationTrailController == null)
            return;

        accelerationTrailController.ShowForDuration(durationSeconds);
    }

    public void ShowAccelerationTrailLocal(float durationSeconds)
    {
        if (durationSeconds <= 0f)
            return;

        ShowAccelerationTrail(durationSeconds);
    }

    private void ResolveAccelerationTrailController()
    {
        if (accelerationTrailController != null)
            return;

        accelerationTrailController = GetComponent<PlayerAccelerationTrail>();
        if (accelerationTrailController != null)
            return;

        accelerationTrailController = GetComponentInChildren<PlayerAccelerationTrail>(true);
        if (accelerationTrailController != null)
            return;

        accelerationTrailController = GetComponentInParent<PlayerAccelerationTrail>();
    }

    public void PlayFeelLocalTimed(string startEventId, string stopEventId, float durationSeconds, string scheduleKey = null)
    {
        PlayFeelLocal(startEventId);

        if (string.IsNullOrWhiteSpace(stopEventId) || durationSeconds <= 0f)
            return;

        string key = string.IsNullOrWhiteSpace(scheduleKey) ? stopEventId : scheduleKey;
        int nextToken = 1;
        if (_feelStopTokens.TryGetValue(key, out int currentToken))
            nextToken = currentToken + 1;

        _feelStopTokens[key] = nextToken;
        StartCoroutine(PlayFeelStopAfterDelay(stopEventId, durationSeconds, key, nextToken));
    }

    public void PlayCameraFovBoostLocal(float fovOffset, float rampInSeconds, float durationSeconds, float settleSeconds, AnimationCurve rampInCurve = null, AnimationCurve settleCurve = null)
    {
        ResolvePlayerCamera();

        if (playerCamera == null)
            return;

        if (_cameraFovPulseRoutine != null)
            StopCoroutine(_cameraFovPulseRoutine);

        _cameraFovPulseRoutine = StartCoroutine(PlayCameraFovBoostRoutine(fovOffset, rampInSeconds, durationSeconds, settleSeconds, rampInCurve, settleCurve));
    }

    private IEnumerator PlayFeelStopAfterDelay(string stopEventId, float durationSeconds, string scheduleKey, int token)
    {
        yield return new WaitForSeconds(durationSeconds);

        if (!_feelStopTokens.TryGetValue(scheduleKey, out int currentToken) || currentToken != token)
            yield break;

        PlayFeelLocal(stopEventId);
    }

    private IEnumerator PlayCameraFovBoostRoutine(float fovOffset, float rampInSeconds, float durationSeconds, float settleSeconds, AnimationCurve rampInCurve, AnimationCurve settleCurve)
    {
        if (playerCamera == null)
            yield break;

        AnimationCurve resolvedRampInCurve = rampInCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AnimationCurve resolvedSettleCurve = settleCurve ?? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        if (rampInSeconds <= 0f)
        {
            playerCamera.SetRuntimeFieldOfViewOffset(fovOffset);
        }
        else
        {
            float safeRampInSeconds = Mathf.Max(0.001f, rampInSeconds);
            float rampInElapsed = 0f;
            while (rampInElapsed < safeRampInSeconds)
            {
                if (playerCamera == null)
                    yield break;

                float normalizedTime = Mathf.Clamp01(rampInElapsed / safeRampInSeconds);
                float curveValue = Mathf.Clamp01(resolvedRampInCurve.Evaluate(normalizedTime));
                playerCamera.SetRuntimeFieldOfViewOffset(fovOffset * curveValue);
                rampInElapsed += Time.deltaTime;
                yield return null;
            }

            if (playerCamera != null)
                playerCamera.SetRuntimeFieldOfViewOffset(fovOffset);
        }

        if (durationSeconds > 0f)
            yield return new WaitForSeconds(durationSeconds);

        if (settleSeconds <= 0f)
        {
            if (playerCamera != null)
                playerCamera.SetRuntimeFieldOfViewOffset(0f);
        }
        else
        {
            float safeSettleSeconds = Mathf.Max(0.001f, settleSeconds);
            float elapsed = 0f;
            while (elapsed < safeSettleSeconds)
            {
                if (playerCamera == null)
                    yield break;

                float normalizedTime = Mathf.Clamp01(elapsed / safeSettleSeconds);
                float curveValue = Mathf.Clamp01(resolvedSettleCurve.Evaluate(normalizedTime));
                playerCamera.SetRuntimeFieldOfViewOffset(fovOffset * curveValue);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (playerCamera != null)
            playerCamera.SetRuntimeFieldOfViewOffset(0f);

        _cameraFovPulseRoutine = null;
    }

    private string ResolveAntiSkillId(string skillId, SkillAction skill)
    {
        if (skill != null && !string.IsNullOrWhiteSpace(skill.antiSkillId))
            return skill.antiSkillId.Trim();

        if (string.IsNullOrWhiteSpace(skillId) || database == null)
            return string.Empty;

        string inferredAntiSkillId = $"{skillId}_anti";
        if (database.TryGet(inferredAntiSkillId, out SkillAction inferredAntiSkill) && inferredAntiSkill != null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Skill '{skillId}' has no configured antiSkillId. Falling back to inferred anti '{inferredAntiSkillId}'.");
            return inferredAntiSkillId;
        }

        return string.Empty;
    }

    private void ResolvePlayerCamera()
    {
        if (playerCamera != null)
            return;

        playerCamera = GetComponent<PlayerCamera>();
        if (playerCamera != null)
            return;

        playerCamera = GetComponentInChildren<PlayerCamera>(true);
        if (playerCamera != null)
            return;

        playerCamera = GetComponentInParent<PlayerCamera>();
    }
}
