using FishNet.Connection;
using BuddahGo.Match;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Integration;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkillExecutor : NetworkBehaviour
{
    public readonly struct LocalSkillUiEvent
    {
        public LocalSkillUiEvent(int slotIndex, float cooldownSeconds, float castLockSeconds)
        {
            SlotIndex = slotIndex;
            CooldownSeconds = cooldownSeconds;
            CastLockSeconds = castLockSeconds;
        }

        public int SlotIndex { get; }
        public float CooldownSeconds { get; }
        public float CastLockSeconds { get; }
    }

    public event Action<LocalSkillUiEvent> LocalSkillUiTriggered;

    public enum CastStage { Requested, Rejected, Accepted, Executed, Cancelled }
    public readonly struct CastEvent
    {
        public readonly CastStage Stage;
        public readonly int Slot;
        public readonly string SkillId;
        // Only populated after execution; decisions cannot inspect the pending backlash draw.
        public readonly bool IsBacklash;
        public CastEvent(CastStage stage, int slot, string skillId, bool backlash = false)
        { Stage = stage; Slot = slot; SkillId = skillId; IsBacklash = backlash; }
    }
    public event Action<CastEvent> ServerCastChanged;
    public event Action ActiveEffectsReset;
    public bool HasPendingCast => _pendingCast != null;
    public float ConfirmationSeconds => CastConfirmDelaySeconds;
    private Coroutine _pendingCast;
    private int _pendingSlot = -1;
    private string _pendingSkillId;
    private ISkillCastContinuation _castContinuation;

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
    [SerializeField] private BuddahPredictionBootstrap predictionBootstrap;
    [SerializeField] private BuddahPredictionSkillMovementBridge predictionSkillMovementBridge;

    private float _castLockedUntil;
    private readonly Dictionary<string, int> _feelStopTokens = new();
    private Coroutine _cameraFovPulseRoutine;
    private float[] _nextReadyTime;
    private ObsessionFigure _obs;
    private OwnerMovementEffectApplier _movementEffects;
    private OwnerMovementEffectApplier MovementEffects => _movementEffects ??= new OwnerMovementEffectApplier(this);
    internal BuddahPredictionSkillMovementBridge PredictionMovementBridge => predictionSkillMovementBridge;

    private static readonly int AntiAnimatorTriggerHash = Animator.StringToHash(AntiAnimatorTriggerName);

    private void Awake()
    {
        _castContinuation = GetComponent<ISkillCastContinuation>();
        _nextReadyTime = new float[SkillLoadout.SlotCount];
        ResolveObsessionFigure();
        ResolveFeelRouter();
        ResolveCharacterAnimator();
        ResolvePlayerCamera();
        if (predictionBootstrap == null) predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionSkillMovementBridge == null) predictionSkillMovementBridge = GetComponent<BuddahPredictionSkillMovementBridge>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveObsessionFigure();
        if (comboInput == null) comboInput = GetComponent<ComboSkillInput>();
        if (loadout == null) loadout = GetComponent<SkillLoadout>();
        if (RacerAuthority.IsServerAI(this) && comboInput != null)
            comboInput.OnSkillSlotTriggered += OnSlotTriggeredByCombo;
    }

    public override void OnStopServer()
    {
        if (comboInput != null) comboInput.OnSkillSlotTriggered -= OnSlotTriggeredByCombo;
        CancelPendingCastServer();
        base.OnStopServer();
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
        if (RacerAuthority.IsServerAI(this)) { CastSlotServer(slotIndex); return; }
        GameLog.Verbose($"[SkillExecutor][Owner] Combo '{comboName}' triggered slot {slotIndex}, requesting cast...");
        RequestCast(slotIndex);
    }

    public void RequestCast(int slotIndex)
    {
        if (!IsOwner || IsRaceGameplayBlocked()) return;
        CastSlotServerRpc(slotIndex);
    }

    public void RequestResetActiveSkillEffectsForRespawn()
    {
        if (!IsOwner)
            return;

        if (IsServerInitialized)
        {
            ResetActiveSkillEffectsServer();
            return;
        }

        ResetActiveSkillEffectsForOwner();
        RequestResetActiveSkillEffectsServerRpc();
    }

    public void ResetActiveSkillEffectsServer()
    {
        if (!IsServerInitialized)
            return;

        ResetActiveSkillEffectsForOwner();
        NetworkConnection conn = Owner;
        if (conn != null && conn.IsValid && !IsOwner) ResetActiveSkillEffectsTargetRpc(conn);
    }

    public void ResetActiveSkillEffectsForOwner(bool resetMovementModifiers = true)
    {
        if (IsServerInitialized) CancelPendingCastServer();
        comboInput?.ClearCombo();
        GetComponent<BuddahHandControl>()?.ResetSkillInputAndEffects();
        GetComponent<SkillPerceptionState>()?.Clear();
        if (resetMovementModifiers) GetComponent<NewBuddah.PredictionV2.Core.BuddahPredictedMotor>()?.ResetActiveSkillModifiers();
        var trap = GetComponent<MovementSlowTrapZoneEffect>();
        if (trap != null) { trap.enabled = false; Destroy(trap); }
        ActiveEffectsReset?.Invoke();
        ResolveCharacterAnimator();
        ResolveAccelerationTrailController();
        ResolvePlayerCamera();

        if (_cameraFovPulseRoutine != null)
        {
            StopCoroutine(_cameraFovPulseRoutine);
            _cameraFovPulseRoutine = null;
        }

        _feelStopTokens.Clear();

        var accelerationEffect = GetComponent<MovementAccelerationEffect>();
        if (accelerationEffect != null)
            accelerationEffect.CancelAndRestore();

        var rootThenAccelerationEffect = GetComponent<MovementRootThenAccelerationEffect>();
        if (rootThenAccelerationEffect != null)
            rootThenAccelerationEffect.CancelAndRestore();

        var invertTurnEffect = GetComponent<MovementInvertTurnInputEffect>();
        if (invertTurnEffect != null)
            invertTurnEffect.CancelAndRestore();

        var scaleEffect = GetComponent<PlayerScaleEffect>();
        if (scaleEffect != null)
            scaleEffect.CancelAndRestore();

        accelerationTrailController?.ClearTrail();
        playerCamera?.ResetRuntimeEffects();

        if (characterAnimator != null)
            characterAnimator.ResetTrigger(AntiAnimatorTriggerHash);
    }

    [ServerRpc(RequireOwnership = true)]
    private void CastSlotServerRpc(int slotIndex)
    {
        CastSlotServer(slotIndex);
    }

    // The owner RPC retains RequireOwnership; server AI uses this identical authority pipeline.
    public bool CastSlotServer(int slotIndex)
    {
        if (!IsServerInitialized) return false;
        string requestedId = loadout != null ? loadout.GetSkillId(slotIndex) : string.Empty;
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Requested, slotIndex, requestedId));
        if (!TryResolveCast(slotIndex, out string skillId, out SkillAction skill, out float now))
        {
            ServerCastChanged?.Invoke(new CastEvent(CastStage.Rejected, slotIndex, requestedId));
            return false;
        }
        ResolveCastVariant(skillId, skill, out bool isAnti, out SkillAction executedSkill, out string executedSkillId);
        QueueCast(slotIndex, skillId, skill, now, isAnti, executedSkill, executedSkillId);
        return true;
    }

    public bool IsSlotReadyServer(int slotIndex) => IsServerInitialized
        && slotIndex >= 0 && slotIndex < SkillLoadout.SlotCount && !HasPendingCast
        && Time.time >= _castLockedUntil && Time.time >= _nextReadyTime[slotIndex];

    public void CancelPendingCastServer()
    {
        if (_pendingCast == null) return;
        StopCoroutine(_pendingCast);
        _pendingCast = null;
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Cancelled, _pendingSlot, _pendingSkillId));
        _pendingSlot = -1;
    }

    private bool TryResolveCast(int slotIndex, out string skillId, out SkillAction skill, out float now)
    {
        skillId = string.Empty;
        skill = null;
        now = 0f;
        if (!ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject))
            return false;

        if (slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount) return false;
        if (loadout == null || database == null)
        {
            Debug.LogWarning("[SkillExecutor][Server] Missing loadout/database.");
            return false;
        }

        skillId = loadout.GetSkillId(slotIndex);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            GameLog.Verbose($"[SkillExecutor][Server] Slot {slotIndex} is empty, cast ignored.");
            return false;
        }

        if (!database.TryGet(skillId, out skill) || skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Unknown skillId '{skillId}' (slot {slotIndex}).");
            return false;
        }

        now = (float)Time.time;
        if (now < _castLockedUntil) return false;

        if (now < _nextReadyTime[slotIndex])
        {
            GameLog.Verbose($"[SkillExecutor][Server] Skill '{skillId}' on cooldown. Ready in {(_nextReadyTime[slotIndex] - now):0.00}s");
            return false;
        }

        return true;
    }

    private void ResolveCastVariant(string skillId, SkillAction skill, out bool isAnti,
        out SkillAction executedSkill, out string executedSkillId)
    {
        ResolveObsessionFigure();
        float obsessionNow = (_obs != null) ? _obs.Current : 0f;
        float backfirePercent = (_obs != null) ? _obs.GetBackfireProbabilityPercent(obsessionNow) : 0f;
        string resolvedAntiSkillId = ResolveAntiSkillId(skillId, skill);
        bool hasResolvableAnti = !string.IsNullOrWhiteSpace(resolvedAntiSkillId);

        isAnti = false;
        executedSkill = skill;
        executedSkillId = skillId;

        if (hasResolvableAnti && backfirePercent > 0.0001f)
        {
            float roll = UnityEngine.Random.value * 100f;
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

            GameLog.Verbose($"[SkillExecutor][Server] Backfire roll: skill='{skillId}', anti='{resolvedAntiSkillId}', obsession={obsessionNow:0.###}, p={backfirePercent:0.###}%, roll={roll:0.###} -> anti={(isAnti ? "YES" : "NO")}");
        }
    }

    private void QueueCast(int slotIndex, string skillId, SkillAction skill, float now,
        bool isAnti, SkillAction executedSkill, string executedSkillId)
    {
        float castDelaySeconds = isAnti ? AntiCastConfirmDelaySeconds : CastConfirmDelaySeconds;
        float triggerAt = now + castDelaySeconds;

        float resolvedCastLockSeconds = database != null ? database.GetCastLockSeconds(executedSkillId, executedSkill) : executedSkill.castLockSeconds;
        float resolvedCooldownSeconds = database != null ? database.GetCooldownSeconds(executedSkillId, executedSkill) : executedSkill.cooldownSeconds;

        _castLockedUntil = triggerAt + resolvedCastLockSeconds;
        _nextReadyTime[slotIndex] = triggerAt + resolvedCooldownSeconds;

        float obsessionGain = database != null ? Mathf.Max(0f, database.GetObsessionGain(skillId, skill)) : Mathf.Max(0f, skill.ObsessionGain);
        GameLog.Verbose($"[SkillExecutor][Server] QUEUE '{executedSkillId}' (slot {slotIndex}) delay={castDelaySeconds:0.##}s cooldown={resolvedCooldownSeconds:0.##} lock={resolvedCastLockSeconds:0.##} anti={isAnti}");
        PlayQueuedCastFeedbackObserversRpc(executedSkillId, isAnti);
        _pendingSlot = slotIndex;
        _pendingSkillId = skillId;
        _pendingCast = StartCoroutine(ExecuteQueuedCastAfterDelay(slotIndex, executedSkill, executedSkillId, obsessionGain, isAnti, castDelaySeconds));
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Accepted, slotIndex, skillId));
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestResetActiveSkillEffectsServerRpc()
    {
        ResetActiveSkillEffectsServer();
    }

    private IEnumerator ExecuteQueuedCastAfterDelay(int slotIndex, SkillAction executedSkill, string executedSkillId, float obsessionGain, bool isAnti, float castDelaySeconds)
    {
        yield return new WaitForSeconds(castDelaySeconds);

        _pendingCast = null;
        if (RacerAuthority.IsServerAI(this) && (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject)
            || _castContinuation != null && !_castContinuation.CanContinueCast(slotIndex)))
        {
            ServerCastChanged?.Invoke(new CastEvent(CastStage.Cancelled, slotIndex, _pendingSkillId));
            yield break;
        }

        if (executedSkill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Delayed cast '{executedSkillId}' lost its SkillAction reference.");
            yield break;
        }

        GameLog.Verbose($"[SkillExecutor][Server] CAST '{executedSkillId}' (slot {slotIndex}) after {castDelaySeconds:0.##}s delay.");

        executedSkill.ExecuteServer(this, slotIndex);
        _obs?.AddServer(obsessionGain);
        CastObserversRpc(slotIndex, executedSkillId, isAnti);
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Executed, slotIndex, _pendingSkillId, isAnti));
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

        GameLog.Verbose($"[SkillExecutor][PreCastFeedback] '{executedSkillId}' anti={isAnti}, IsOwner={IsOwner}");

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
        GameLog.Verbose($"[SkillExecutor][ObserversRpc] object='{name}', objectId={objectId}, slot={slotIndex}, skill='{executedSkillId}', anti={isAnti}, IsOwner={IsOwner}, IsClientInitialized={IsClientInitialized}, ownerClientId={ownerClientId}, localClientId={localClientId}");

        if (database.TryGet(executedSkillId, out SkillAction skill) && skill != null)
        {
            GameLog.Verbose($"[SkillExecutor][Observers] '{executedSkillId}' played (slot {slotIndex}) [anti={isAnti}]");
            skill.ExecuteObservers(this, slotIndex, isAnti, IsOwner);

            if (IsOwner)
            {
                float resolvedCooldownSeconds = database != null ? database.GetCooldownSeconds(executedSkillId, skill) : skill.cooldownSeconds;
                float resolvedCastLockSeconds = database != null ? database.GetCastLockSeconds(executedSkillId, skill) : skill.castLockSeconds;
                LocalSkillUiTriggered?.Invoke(new LocalSkillUiEvent(slotIndex, resolvedCooldownSeconds, resolvedCastLockSeconds));

                GameLog.Verbose($"[SkillExecutor][Owner] ExecuteLocal '{executedSkillId}' (slot {slotIndex}) [anti={isAnti}]");
                skill.ExecuteLocal(this, slotIndex, isAnti);
            }
        }
    }

    public void ApplyAccelerationToOwner(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyAccelerationServer(extraForwardForce, extraMaxSpeed, durationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyAccelerationTargetRpc(conn, extraForwardForce, extraMaxSpeed, durationSeconds);
    }

    [TargetRpc]
    private void ApplyAccelerationTargetRpc(NetworkConnection conn, float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        MovementEffects.ApplyAccelerationOwner(extraForwardForce, extraMaxSpeed, durationSeconds);
    }

    public void ApplyRootThenAccelerationToOwner(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyRootThenAccelerationServer(rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyRootThenAccelerationTargetRpc(conn, rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);
    }

    [TargetRpc]
    private void ApplyRootThenAccelerationTargetRpc(NetworkConnection conn, float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        MovementEffects.ApplyRootThenAccelerationOwner(rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds);
    }

    public void ApplyInvertTurnInputToOwner(float durationSeconds)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyInvertTurnInputServer(durationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyInvertTurnInputTargetRpc(conn, durationSeconds);
    }

    [TargetRpc]
    private void ApplyInvertTurnInputTargetRpc(NetworkConnection conn, float durationSeconds)
    {
        MovementEffects.ApplyInvertTurnInputOwner(durationSeconds);
    }

    public void ApplyScaleToOwner(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds, float massMultiplier, float forwardForceMultiplier)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyScaleServer(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds, massMultiplier, forwardForceMultiplier);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyScaleTargetRpc(conn, scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds, massMultiplier, forwardForceMultiplier);
    }

    [TargetRpc]
    private void ApplyScaleTargetRpc(NetworkConnection conn, float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds, float massMultiplier, float forwardForceMultiplier)
    {
        MovementEffects.ApplyScaleOwner(scaleMultiplier, durationSeconds, enterDurationSeconds, restoreDurationSeconds, massMultiplier, forwardForceMultiplier);
    }

    internal bool UsePredictionMovementBridge()
    {
        if (predictionBootstrap == null) predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionSkillMovementBridge == null) predictionSkillMovementBridge = GetComponent<BuddahPredictionSkillMovementBridge>();

        return predictionBootstrap != null
               && predictionBootstrap.IsPredictionModeActive()
               && predictionSkillMovementBridge != null
               && predictionSkillMovementBridge.IsPredictionMovementActive();
    }

    private T ResolveInHierarchy<T>(bool parentsBeforeChildren) where T : Component
    {
        T result = GetComponent<T>();
        if (result != null)
            return result;

        if (parentsBeforeChildren)
        {
            result = GetComponentInParent<T>();
            if (result == null)
                result = GetComponentInChildren<T>(true);
        }
        else
        {
            result = GetComponentInChildren<T>(true);
            if (result == null)
                result = GetComponentInParent<T>();
        }
        return result;
    }

    private void ResolveObsessionFigure()
    {
        if (_obs != null)
            return;

        _obs = ResolveInHierarchy<ObsessionFigure>(true);
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

        feelRouter = ResolveInHierarchy<SkillFeelRouter>(false);
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

        characterAnimator = ResolveInHierarchy<Animator>(false);
    }

    internal void ShowAccelerationTrail(float durationSeconds)
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

        accelerationTrailController = ResolveInHierarchy<PlayerAccelerationTrail>(false);
    }

    private bool IsRaceGameplayBlocked()
    {
        return !ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject);
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

    [TargetRpc]
    private void ResetActiveSkillEffectsTargetRpc(NetworkConnection conn)
    {
        ResetActiveSkillEffectsForOwner();
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
        return SkillCameraFov.Run(() => playerCamera, () => _cameraFovPulseRoutine = null,
            fovOffset, rampInSeconds, durationSeconds, settleSeconds, rampInCurve, settleCurve);
    }

    private string ResolveAntiSkillId(string skillId, SkillAction skill)
    {
        if (database != null)
        {
            string configuredAntiSkillId = database.GetAntiSkillId(skillId, skill);
            if (!string.IsNullOrWhiteSpace(configuredAntiSkillId))
                return configuredAntiSkillId;
        }

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

        playerCamera = ResolveInHierarchy<PlayerCamera>(false);
    }

    public void PlayChargedBurstVisualsServer(string skillId, Vector3[] starts, Vector3 direction,
        float speed, float buildUpSeconds, float lifetimeSeconds, Vector3 localEuler, Vector3 visualSize)
    {
        if (!IsServerInitialized)
            return;
        PlayChargedBurstVisualsObserversRpc(skillId, starts, direction, speed, buildUpSeconds,
            lifetimeSeconds, localEuler, visualSize);
    }

    // Appended after the existing RPC declarations to preserve their relative order.
    [ObserversRpc]
    private void PlayChargedBurstVisualsObserversRpc(string skillId, Vector3[] starts, Vector3 direction,
        float speed, float buildUpSeconds, float lifetimeSeconds, Vector3 localEuler, Vector3 visualSize)
    {
        if (database == null || !database.TryGet(skillId, out SkillAction action)
            || !(action is Skill_PushProjectileHands_Anti skill) || starts == null)
            return;

        BuddahHandControl handControl = GetComponent<BuddahHandControl>();
        if (handControl == null)
            return;
        handControl.SpawnChargedBurstVisualsAtPositions(starts, direction, speed, buildUpSeconds,
            lifetimeSeconds, localEuler, visualSize, skill.ChargedProjectileVfxPrefab,
            skill.ChargedProjectileProgressProperty);
    }

}
