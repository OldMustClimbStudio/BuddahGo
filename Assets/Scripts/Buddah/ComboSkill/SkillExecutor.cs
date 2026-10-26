using FishNet.Connection;
using BuddahGo.Match;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Integration;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
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
        // Always 0 since the global cast lock was retired (A3); the HUD lock display is reworked in A6.
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
        // A backfired cast ends with Executed + IsBacklash: the skill did not run and the backfire penalty began.
        public readonly bool IsBacklash;
        public CastEvent(CastStage stage, int slot, string skillId, bool backlash = false)
        { Stage = stage; Slot = slot; SkillId = skillId; IsBacklash = backlash; }
    }
    public event Action<CastEvent> ServerCastChanged;
    public event Action ActiveEffectsReset;
    public bool HasPendingCast => _pendingCast != null;
    /// <summary>Longest Skill.castConfirmDelaySeconds in the tables (AI timing estimate); the constant without tables.</summary>
    public float ConfirmationSeconds => ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository skills) && skills.MaxCastConfirmDelaySeconds > 0f
        ? skills.MaxCastConfirmDelaySeconds : CastConfirmDelaySeconds;

    // Everything one confirming cast needs after its delay. The coroutine reads only this, never the _pending* fields.
    private readonly struct QueuedCast
    {
        public QueuedCast(int id, int slot, string skillId, SkillAction skill, float obsessionGain, int surchargeStacks,
            float cooldownEndTime, float cooldownSeconds, bool isBackfire, float delaySeconds)
        {
            Id = id; Slot = slot; SkillId = skillId; Skill = skill; ObsessionGain = obsessionGain; SurchargeStacks = surchargeStacks;
            CooldownEndTime = cooldownEndTime; CooldownSeconds = cooldownSeconds; IsBackfire = isBackfire; DelaySeconds = delaySeconds;
        }

        public readonly int Id;
        public readonly int Slot;
        public readonly string SkillId;
        public readonly SkillAction Skill;
        public readonly float ObsessionGain;
        public readonly int SurchargeStacks;
        public readonly float CooldownEndTime;
        public readonly float CooldownSeconds;
        public readonly bool IsBackfire;
        public readonly float DelaySeconds;
    }

    private Coroutine _pendingCast;
    private int _pendingSlot = -1;
    private string _pendingSkillId;
    // A confirming backfire is not dropped by ordinary cancels (SkillCastRules.DropsPendingCast).
    private bool _pendingIsBackfire;
    private int _pendingCastId;
    private int _castSequence;
    private ISkillCastContinuation _castContinuation;

    private const float CastConfirmDelaySeconds = 1f;
    private const string SkillReflectionSharedFeelEventId = "Skill_Reflection_Shared";
    private const string SkillReflectionLocalFeelEventId = "Skill_Reflection_Local";
    // The former anti feedback is now the unified backfire feedback (confirm window of a backfired cast).
    private const string BackfireReflectionSharedFeelEventId = "Anti_Reflection_Shared";
    private const string BackfireReflectionLocalFeelEventId = "Anti_Reflection_Local";
    private const string BackfireAnimatorTriggerName = "IsAnti";

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

    private readonly Dictionary<string, int> _feelStopTokens = new();
    private Coroutine _cameraFovPulseRoutine;
    // Per-slot cooldown end in server Time.time. There is no global lock between skills.
    private float[] _nextReadyTime;
    private readonly SkillRepeatSurcharge _repeatSurcharge = new SkillRepeatSurcharge();
    private ObsessionFigure _obs;
    private NewBuddah.PredictionV2.Core.BuddahPredictedMotor _motor;
    private OwnerMovementEffectApplier _movementEffects;
    private OwnerMovementEffectApplier MovementEffects => _movementEffects ??= new OwnerMovementEffectApplier(this);
    internal BuddahPredictionSkillMovementBridge PredictionMovementBridge => predictionSkillMovementBridge;

    // Owner-only skill keys (Q/E/R and 1/2/3). Player combos are retired; AI still casts through ComboSkillInput.
    private InputSystem_Actions _skillKeyInput;
    private bool _skillKeysBound;

    private static readonly int BackfireAnimatorTriggerHash = Animator.StringToHash(BackfireAnimatorTriggerName);

    private void Awake()
    {
        _castContinuation = GetComponent<ISkillCastContinuation>();
        _nextReadyTime = new float[SkillLoadout.SlotCount];
        ResolveObsessionFigure();
        ResolveMotor();
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
        // The only AI cast route until A7: AISkillCaster injects combo keys, the recognizer raises the slot here.
        if (RacerAuthority.IsServerAI(this) && comboInput != null)
            comboInput.OnSkillSlotTriggered += OnSlotTriggeredByCombo;
    }

    public override void OnStopServer()
    {
        if (comboInput != null) comboInput.OnSkillSlotTriggered -= OnSlotTriggeredByCombo;
        DropPendingCastServer(SkillPendingCancelSource.Teardown);
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        if (loadout == null) loadout = GetComponent<SkillLoadout>();
        ResolveFeelRouter();
        BindSkillKeys();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        UnbindSkillKeys();
    }

    private void OnDisable()
    {
        UnbindSkillKeys();
        // Coroutines die with the component; never leave a dead pending cast (backfire included) blocking every later request.
        if (_pendingCast != null) DropPendingCastServer(SkillPendingCancelSource.Teardown);
    }

    private void OnDestroy()
    {
        UnbindSkillKeys();
        _skillKeyInput?.Dispose();
        _skillKeyInput = null;
    }

    private void BindSkillKeys()
    {
        if (_skillKeysBound) return;
        _skillKeyInput ??= new InputSystem_Actions();
        _skillKeyInput.Player.SkillSlot1.performed += OnSkillSlot1Performed;
        _skillKeyInput.Player.SkillSlot2.performed += OnSkillSlot2Performed;
        _skillKeyInput.Player.SkillSlot3.performed += OnSkillSlot3Performed;
        _skillKeyInput.Player.Enable();
        _skillKeysBound = true;
    }

    // The generated wrapper's finaliser asserts its maps were disabled, so every exit path disables them.
    private void UnbindSkillKeys()
    {
        if (_skillKeyInput == null) return;
        if (_skillKeysBound)
        {
            _skillKeyInput.Player.SkillSlot1.performed -= OnSkillSlot1Performed;
            _skillKeyInput.Player.SkillSlot2.performed -= OnSkillSlot2Performed;
            _skillKeyInput.Player.SkillSlot3.performed -= OnSkillSlot3Performed;
            _skillKeysBound = false;
        }
        _skillKeyInput.Disable();
    }

    private void OnSkillSlot1Performed(InputAction.CallbackContext ctx) => OnSkillKeyPressed(0);
    private void OnSkillSlot2Performed(InputAction.CallbackContext ctx) => OnSkillKeyPressed(1);
    private void OnSkillSlot3Performed(InputAction.CallbackContext ctx) => OnSkillKeyPressed(2);

    private void OnSkillKeyPressed(int slotIndex)
    {
        if (LocalInputBlock.IsBlocked) return;
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Owner] Skill key for slot {slotIndex}, requesting cast...");
        RequestCast(slotIndex);
    }

    // Server AI only. Player combos are retired: a human's recognizer is no longer subscribed.
    private void OnSlotTriggeredByCombo(int slotIndex, string comboName)
    {
        if (RacerAuthority.IsServerAI(this)) { CastSlotServer(slotIndex); return; }
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor] Combo '{comboName}' (slot {slotIndex}) ignored: player combos are retired.");
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

        // Server-controlled racers (AI, training dummies) have no owner connection, so their server-side skill
        // state is cleared here. Human Players keep the original owner-targeted reset; their skill rules are unchanged.
        if (RacerAuthority.IsServerControlled(this)) { ResetActiveSkillEffectsForOwner(); return; }
        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid)
            return;

        ResetActiveSkillEffectsTargetRpc(conn);
    }

    // Server-controlled cleanup (AI, training dummies): pending input and cast, hands buff, perception, modifiers
    // and the trap zone. A Human Player's respawn never enters here; its owner-side reset below is unchanged.
    // The backfire penalty is not a skill effect and survives every reset (lock, obsession freeze, x0.85). A confirming
    // backfire is kept too: like a Human Player's, it still ends in the penalty when its confirm window ends.
    private void ResetServerControlledSkillState(bool resetMovementModifiers)
    {
        CancelPendingCastServer();
        comboInput?.ClearCombo();
        GetComponent<BuddahHandControl>()?.ResetSkillInputAndEffects();
        GetComponent<SkillPerceptionState>()?.Clear();
        if (resetMovementModifiers)
        {
            ResolveMotor();
            // Keeps the backfire max-speed penalty (motor rule), like the respawn teleport reset.
            _motor?.ResetActiveSkillModifiers();
        }
        var trap = GetComponent<MovementSlowTrapZoneEffect>();
        if (trap != null) { trap.enabled = false; Destroy(trap); }
        ActiveEffectsReset?.Invoke();
    }

    public void ResetActiveSkillEffectsForOwner(bool resetMovementModifiers = true)
    {
        if (IsServerInitialized && RacerAuthority.IsServerControlled(this)) ResetServerControlledSkillState(resetMovementModifiers);
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
            characterAnimator.ResetTrigger(BackfireAnimatorTriggerHash);
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
        // Server AI: refuse at entry when a same-kind global effect is already visible, so no cooldown is
        // spent and no pre-cast feedback plays. The confirmation-time recheck below remains as the fallback.
        if (RacerAuthority.IsServerAI(this) && _castContinuation != null && !_castContinuation.CanContinueCast(slotIndex))
        {
            ServerCastChanged?.Invoke(new CastEvent(CastStage.Rejected, slotIndex, requestedId));
            return false;
        }
        bool isBackfire = ResolveCastVariant(skillId);
        QueueCast(slotIndex, skillId, skill, now, isBackfire);
        return true;
    }

    public bool IsSlotReadyServer(int slotIndex) => IsServerInitialized
        && slotIndex >= 0 && slotIndex < SkillLoadout.SlotCount
        && EvaluateCastGate(slotIndex, Time.time) == SkillCastBlock.None;

    /// <summary>
    /// Server: cancels the confirming cast (emits Cancelled; the spent cooldown is not refunded). A confirming backfire
    /// is not cancelled: it ends in the penalty when its window ends, exactly like a Human Player's.
    /// Returns true when a cast was cancelled.
    /// </summary>
    public bool CancelPendingCastServer() => DropPendingCastServer(SkillPendingCancelSource.Request);

    private bool DropPendingCastServer(SkillPendingCancelSource source)
    {
        if (_pendingCast == null || !SkillCastRules.DropsPendingCast(_pendingIsBackfire, source)) return false;
        StopCoroutine(_pendingCast);
        int slotIndex = _pendingSlot;
        string skillId = _pendingSkillId;
        ClearPendingCast();
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Cancelled, slotIndex, skillId));
        return true;
    }

    private void ClearPendingCast()
    {
        _pendingCast = null;
        _pendingSlot = -1;
        _pendingSkillId = null;
        _pendingIsBackfire = false;
        _pendingCastId = 0;
    }

    // The one cast gate shared by TryResolveCast (every request) and IsSlotReadyServer (AI readiness).
    private SkillCastBlock EvaluateCastGate(int slotIndex, float now)
    {
        ResolveObsessionFigure();
        ResolveMotor();
        return SkillCastRules.EvaluateGate(
            HasPendingCast,
            _obs != null && _obs.IsBackfireActive,
            _motor != null && _motor.IsFrozen,
            now,
            _nextReadyTime[slotIndex]);
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
            if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] Slot {slotIndex} is empty, cast ignored.");
            return false;
        }

        if (!database.TryGet(skillId, out skill) || skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Unknown skillId '{skillId}' (slot {slotIndex}).");
            return false;
        }

        now = (float)Time.time;
        SkillCastBlock block = EvaluateCastGate(slotIndex, now);
        if (block != SkillCastBlock.None)
        {
            if (NetDebug.EnableVerboseLog)
            {
                string detail = block == SkillCastBlock.Cooldown ? $" Ready in {(_nextReadyTime[slotIndex] - now):0.00}s" : string.Empty;
                GameLog.Verbose($"[SkillExecutor][Server] Skill '{skillId}' (slot {slotIndex}) rejected: {block}.{detail}");
            }
            return false;
        }

        return true;
    }

    // Unified backfire: the roll happens at request time from the current obsession; the requested skill is never
    // swapped for another action. A backfired cast still confirms, spends its cooldown and then fails.
    private bool ResolveCastVariant(string skillId)
    {
        ResolveObsessionFigure();
        float obsessionNow = (_obs != null) ? _obs.Current : 0f;
        float backfirePercent = (_obs != null) ? _obs.GetBackfireProbabilityPercent(obsessionNow) : 0f;
        if (backfirePercent <= SkillCastRules.MinRollableBackfirePercent)
            return false;

        float roll01 = UnityEngine.Random.value;
        bool isBackfire = SkillCastRules.RollsBackfire(backfirePercent, roll01);
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] Backfire roll: skill='{skillId}', obsession={obsessionNow:0.###}, p={backfirePercent:0.###}%, roll={roll01 * 100f:0.###} -> backfire={(isBackfire ? "YES" : "NO")}");
        return isBackfire;
    }

    // Skill.castConfirmDelaySeconds of the requested row; the constant only applies without a row.
    private static float ResolveCastConfirmDelay(string skillId)
    {
        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository skills)
            && skills.TryGetCastConfirmDelay(skillId, out float seconds))
            return seconds;
        return CastConfirmDelaySeconds;
    }

    private void QueueCast(int slotIndex, string skillId, SkillAction skill, float now, bool isBackfire)
    {
        float castDelaySeconds = ResolveCastConfirmDelay(skillId);
        float triggerAt = now + castDelaySeconds;

        // Cooldown always comes from the requested skill, backfire or not.
        float resolvedCooldownSeconds = database != null ? database.GetCooldownSeconds(skillId, skill) : skill.cooldownSeconds;
        float cooldownEndTime = triggerAt + resolvedCooldownSeconds;
        _nextReadyTime[slotIndex] = cooldownEndTime;

        // Same-skill surcharge: resolved now, committed only if the cast executes normally.
        float baseObsessionGain = database != null ? Mathf.Max(0f, database.GetObsessionGain(skillId, skill)) : Mathf.Max(0f, skill.ObsessionGain);
        float repeatWindowSeconds = _obs != null ? _obs.RepeatWindowSeconds : 0f;
        float repeatGainMultiplier = _obs != null ? _obs.RepeatGainMultiplier : 1f;
        int surchargeStacks = _repeatSurcharge.ResolveStacks(skillId, now, repeatWindowSeconds);
        float obsessionGain = SkillRepeatSurcharge.ResolveGain(baseObsessionGain, repeatGainMultiplier, surchargeStacks);

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] QUEUE '{skillId}' (slot {slotIndex}) delay={castDelaySeconds:0.##}s cooldown={resolvedCooldownSeconds:0.##} gain={obsessionGain:0.#} (x{repeatGainMultiplier:0.###}^{surchargeStacks}) backfire={isBackfire}");
        PlayQueuedCastFeedbackObserversRpc(skillId, isBackfire);

        var cast = new QueuedCast(unchecked(++_castSequence), slotIndex, skillId, skill, obsessionGain, surchargeStacks,
            cooldownEndTime, resolvedCooldownSeconds, isBackfire, castDelaySeconds);
        _pendingSlot = slotIndex;
        _pendingSkillId = skillId;
        _pendingIsBackfire = isBackfire;
        _pendingCastId = cast.Id;
        _pendingCast = StartCoroutine(ExecuteQueuedCastAfterDelay(cast));
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Accepted, slotIndex, skillId));
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestResetActiveSkillEffectsServerRpc()
    {
        ResetActiveSkillEffectsServer();
    }

    private IEnumerator ExecuteQueuedCastAfterDelay(QueuedCast cast)
    {
        yield return new WaitForSeconds(cast.DelaySeconds);

        // Clear only this cast's pending state; from here on the locals in `cast` identify it.
        if (_pendingCastId == cast.Id) ClearPendingCast();

        // Server AI rechecks only public, already-visible global effects here; a backfire is resolved first (it runs
        // no skill, so it cannot duplicate an effect), so an AI never escapes the penalty a Human Player takes.
        bool isServerAI = RacerAuthority.IsServerAI(this);
        bool aiInRace = isServerAI && ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject);
        bool aiCanContinue = isServerAI && (_castContinuation == null || _castContinuation.CanContinueCast(cast.Slot));
        switch (SkillCastRules.ResolveConfirmOutcome(cast.IsBackfire, isServerAI, aiInRace, aiCanContinue))
        {
            case SkillConfirmOutcome.Cancel:
                ServerCastChanged?.Invoke(new CastEvent(CastStage.Cancelled, cast.Slot, cast.SkillId));
                yield break;
            case SkillConfirmOutcome.Backfire:
                ExecuteBackfireServer(cast);
                yield break;
        }

        if (cast.Skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Delayed cast '{cast.SkillId}' lost its SkillAction reference.");
            ServerCastChanged?.Invoke(new CastEvent(CastStage.Cancelled, cast.Slot, cast.SkillId));
            yield break;
        }

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] CAST '{cast.SkillId}' (slot {cast.Slot}) after {cast.DelaySeconds:0.##}s delay.");

        cast.Skill.ExecuteServer(this, cast.Slot);
        _obs?.AddServer(cast.ObsessionGain);
        _repeatSurcharge.CommitExecution(cast.SkillId, cast.SurchargeStacks, cast.CooldownEndTime);
        CastObserversRpc(cast.Slot, cast.SkillId);
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Executed, cast.Slot, cast.SkillId));
    }

    // The backfired cast fails: no ExecuteServer, no obsession gain, no surcharge stack, no skill presentation.
    // Its cooldown stays spent. The penalty (all skills locked, obsession frozen, max speed x0.85, x0.8 at the end)
    // starts now, when the confirm window ends. Running buffs and 缘起 are not interrupted.
    private void ExecuteBackfireServer(QueuedCast cast)
    {
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] BACKFIRE '{cast.SkillId}' (slot {cast.Slot}): skill fails, penalty begins.");

        ResolveObsessionFigure();
        if (_obs != null)
        {
            _obs.BeginBackfireServer();
            ApplyMaxSpeedMultiplierToOwner(_obs.BackfireMaxSpeedMultiplier, _obs.BackfireLockSeconds);
        }
        _repeatSurcharge.Clear();

        // CastObserversRpc is skipped, so the owner HUD learns the spent cooldown here.
        NetworkConnection conn = Owner;
        if (conn != null && conn.IsValid)
            BackfireSkillUiTargetRpc(conn, cast.Slot, cast.CooldownSeconds);

        ServerCastChanged?.Invoke(new CastEvent(CastStage.Executed, cast.Slot, cast.SkillId, backlash: true));
    }

    [ObserversRpc]
    private void PlayQueuedCastFeedbackObserversRpc(string skillId, bool isBackfire)
    {
        if (isBackfire)
        {
            TriggerBackfireAnimationLocal();
            PlayFeelLocal(BackfireReflectionSharedFeelEventId);
        }
        else
        {
            PlayFeelLocal(SkillReflectionSharedFeelEventId);
        }

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][PreCastFeedback] '{skillId}' backfire={isBackfire}, IsOwner={IsOwner}");

        if (!IsOwner)
            return;

        PlayFeelLocal(isBackfire ? BackfireReflectionLocalFeelEventId : SkillReflectionLocalFeelEventId);
    }

    [ObserversRpc]
    private void CastObserversRpc(int slotIndex, string skillId)
    {
        if (database == null) return;

        string ownerClientId = Owner != null ? Owner.ClientId.ToString() : "null";
        string localClientId = LocalConnection != null ? LocalConnection.ClientId.ToString() : "null";
        int objectId = NetworkObject != null ? NetworkObject.ObjectId : 0;
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][ObserversRpc] object='{name}', objectId={objectId}, slot={slotIndex}, skill='{skillId}', IsOwner={IsOwner}, IsClientInitialized={IsClientInitialized}, ownerClientId={ownerClientId}, localClientId={localClientId}");

        if (database.TryGet(skillId, out SkillAction skill) && skill != null)
        {
            if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Observers] '{skillId}' played (slot {slotIndex})");
            skill.ExecuteObservers(this, slotIndex, IsOwner);

            if (IsOwner)
            {
                float resolvedCooldownSeconds = database.GetCooldownSeconds(skillId, skill);
                LocalSkillUiTriggered?.Invoke(new LocalSkillUiEvent(slotIndex, resolvedCooldownSeconds, 0f));

                if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Owner] ExecuteLocal '{skillId}' (slot {slotIndex})");
                skill.ExecuteLocal(this, slotIndex);
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

    /// <summary>
    /// Server: freezes this racer (frost lock). Also interrupts its confirming cast: the cast is cancelled and that
    /// slot's cooldown is refunded (only this freeze path refunds), and pending AI combo keys and hand input are dropped.
    /// A confirming backfire is dropped the same way (no penalty) until the user rules on freeze vs. backfire.
    /// </summary>
    public void ApplyFreezeToOwner(float durationSeconds)
    {
        if (!IsServerInitialized) return;

        InterruptCastForFreezeServer();
        MovementEffects.ApplyFreezeServer(durationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyFreezeTargetRpc(conn, durationSeconds);
    }

    private void InterruptCastForFreezeServer()
    {
        int interruptedSlot = _pendingSlot;
        bool interrupted = DropPendingCastServer(SkillPendingCancelSource.Freeze);
        if (interrupted && interruptedSlot >= 0 && interruptedSlot < _nextReadyTime.Length)
            _nextReadyTime[interruptedSlot] = Time.time;
        if (RacerAuthority.IsServerAI(this)) comboInput?.ClearCombo();
        GetComponent<BuddahHandControl>()?.CancelPendingServerInput();
    }

    /// <summary>Server: ends the acceleration modifier now (shared slot: also ends a slow-trap debuff).</summary>
    public void EndAccelerationForOwner()
    {
        if (!IsServerInitialized) return;

        MovementEffects.EndAccelerationServer();

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        EndAccelerationTargetRpc(conn);
    }

    /// <summary>Server: ends the motor scale modifier now. Visual scale is presentation and ends separately.</summary>
    public void EndScaleForOwner()
    {
        if (!IsServerInitialized) return;

        MovementEffects.EndScaleServer();

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        EndScaleTargetRpc(conn);
    }

    /// <summary>Server: instant extra max speed that decays to zero over the duration (giant charge).</summary>
    public void ApplyBurstToOwner(float extraMaxSpeed, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyBurstServer(extraMaxSpeed, durationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyBurstTargetRpc(conn, extraMaxSpeed, durationSeconds);
    }

    /// <summary>Server: multiplies the final max speed for the duration (backfire penalty x0.85).</summary>
    public void ApplyMaxSpeedMultiplierToOwner(float multiplier, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        MovementEffects.ApplyMaxSpeedMultiplierServer(multiplier, durationSeconds);

        NetworkConnection conn = Owner;
        if (conn == null || !conn.IsValid) return;

        ApplyMaxSpeedMultiplierTargetRpc(conn, multiplier, durationSeconds);
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

    private void ResolveMotor()
    {
        if (_motor != null)
            return;

        _motor = GetComponent<NewBuddah.PredictionV2.Core.BuddahPredictedMotor>();
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

    private void TriggerBackfireAnimationLocal()
    {
        ResolveCharacterAnimator();

        if (characterAnimator == null)
            return;

        characterAnimator.ResetTrigger(BackfireAnimatorTriggerHash);
        characterAnimator.SetTrigger(BackfireAnimatorTriggerHash);
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

    private void ResolvePlayerCamera()
    {
        if (playerCamera != null)
            return;

        playerCamera = ResolveInHierarchy<PlayerCamera>(false);
    }

    // New RPCs are appended below, after the existing RPC declarations, to preserve their relative order.

    [TargetRpc]
    private void ApplyFreezeTargetRpc(NetworkConnection conn, float durationSeconds)
    {
        MovementEffects.ApplyFreezeOwner(durationSeconds);
    }

    [TargetRpc]
    private void EndAccelerationTargetRpc(NetworkConnection conn)
    {
        MovementEffects.EndAccelerationOwner();
    }

    [TargetRpc]
    private void EndScaleTargetRpc(NetworkConnection conn)
    {
        MovementEffects.EndScaleOwner();
    }

    [TargetRpc]
    private void ApplyBurstTargetRpc(NetworkConnection conn, float extraMaxSpeed, float durationSeconds)
    {
        MovementEffects.ApplyBurstOwner(extraMaxSpeed, durationSeconds);
    }

    [TargetRpc]
    private void ApplyMaxSpeedMultiplierTargetRpc(NetworkConnection conn, float multiplier, float durationSeconds)
    {
        MovementEffects.ApplyMaxSpeedMultiplierOwner(multiplier, durationSeconds);
    }

    // A backfired cast skips CastObserversRpc; the owner HUD still shows the spent slot cooldown.
    [TargetRpc]
    private void BackfireSkillUiTargetRpc(NetworkConnection conn, int slotIndex, float cooldownSeconds)
    {
        LocalSkillUiTriggered?.Invoke(new LocalSkillUiEvent(slotIndex, cooldownSeconds, 0f));
    }
}
