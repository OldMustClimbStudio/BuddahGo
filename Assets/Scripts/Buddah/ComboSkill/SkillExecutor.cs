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
using System.Reflection;

public class SkillExecutor : NetworkBehaviour
{
    public readonly struct LocalSkillUiEvent
    {
        /// <summary>SurchargeStacks value of a backfire: it cleared every skill's stacks.</summary>
        public const int StacksClearedByBackfire = -1;

        public LocalSkillUiEvent(int slotIndex, float cooldownSeconds,
            int surchargeStacks = 0, float repeatWindowSeconds = 0f, float repeatGainMultiplier = 1f,
            float secondsSinceServerEvent = 0f)
        {
            SlotIndex = slotIndex;
            CooldownSeconds = cooldownSeconds;
            SurchargeStacks = surchargeStacks;
            RepeatWindowSeconds = repeatWindowSeconds;
            RepeatGainMultiplier = repeatGainMultiplier;
            SecondsSinceServerEvent = Mathf.Max(0f, secondsSinceServerEvent);
        }

        /// <summary>
        /// Seconds between the server's execution (or backfire) and this event on the owner: the RPC's server tick
        /// against this peer's estimate of the server tick (0 on the host and when unknown). The HUD anchors the
        /// cooldown and the surcharge window at Time.time minus this, i.e. on the server's clock like the backfire and
        /// freeze timers, instead of at the RPC's arrival (which put both a full round trip late on remote clients).
        /// </summary>
        public float SecondsSinceServerEvent { get; }

        /// <summary>
        /// Age of a server tick on this peer: (estimated server tick − event tick) × tick delta; 0 when the event tick is
        /// unknown (0) or the estimate has not reached it yet.
        /// </summary>
        internal static float SecondsSinceServerTick(uint eventTick, uint estimatedServerTick, double tickDelta)
        {
            if (eventTick == 0u || estimatedServerTick <= eventTick || tickDelta <= 0d)
                return 0f;
            return (float)((estimatedServerTick - eventTick) * tickDelta);
        }

        public int SlotIndex { get; }
        public float CooldownSeconds { get; }
        // No CastLockSeconds: the global cast lock was retired (A3) and its HUD hold with it (A6).
        /// <summary>
        /// Same-skill surcharge stacks this execution paid (gain × RepeatGainMultiplier^stacks; 0 = base gain).
        /// <see cref="StacksClearedByBackfire"/> (−1): the cast backfired and every skill's stacks were cleared.
        /// A recast within RepeatWindowSeconds after this slot's cooldown ends pays stacks + 1.
        /// </summary>
        public int SurchargeStacks { get; }
        /// <summary>ObsessionFigure.repeatWindowSeconds: seconds after the cooldown end during which a recast stacks.</summary>
        public float RepeatWindowSeconds { get; }
        /// <summary>ObsessionFigure.repeatGainMultiplier: obsession gain multiplier per stack, compounding.</summary>
        public float RepeatGainMultiplier { get; }
    }

    public event Action<LocalSkillUiEvent> LocalSkillUiTriggered;

    /// <summary>
    /// Every client (host included): a 缘起 of this racer triggered (yuanqiId, payload, localIsCaster). Raised from the
    /// 缘起 ObserversRpc after YuanqiAction.PlayObservers, also when this client's SkillDatabase lacks the id. The
    /// presentation hook for artists: the payload carries the centre, radius, seconds, direction and target ids.
    /// </summary>
    public event Action<string, YuanqiPresentationPayload, bool> YuanqiObserved;

    /// <summary>Every client (host included): this racer's 缘起 body hit a victim (金足破阵 impact), one per victim per burst.</summary>
    public event Action<YuanqiImpactInfo> YuanqiImpactObserved;

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
    /// <summary>Owner-side: one of this racer's 缘起 triggered (yuanqiId), for the HUD toast.</summary>
    public event Action<string> LocalYuanqiTriggered;
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
    // Which skill owns the shared FOV pulse slot (null = untagged); a 缘起 consume settles only its own boost.
    private string _cameraFovPulseTag;
    // Per-slot cooldown end in server Time.time. There is no global lock between skills.
    private float[] _nextReadyTime;
    private readonly SkillRepeatSurcharge _repeatSurcharge = new SkillRepeatSurcharge();
    private ObsessionFigure _obs;
    private NewBuddah.PredictionV2.Core.BuddahPredictedMotor _motor;
    private OwnerMovementEffectApplier _movementEffects;
    private OwnerMovementEffectApplier MovementEffects => _movementEffects ??= new OwnerMovementEffectApplier(this);
    internal BuddahPredictionSkillMovementBridge PredictionMovementBridge => predictionSkillMovementBridge;

    // 缘起 (server only; training dummies never get them): which skills are 生效中, and the pairs this loadout can trigger.
    // This class only writes board entries and resets; it knows no pair.
    private SkillStateBoard _stateBoard;
    private YuanqiRegistry _yuanqiRegistry;
    /// <summary>Server: this racer's skill state board (null on clients and training dummies).</summary>
    public SkillStateBoard StateBoard => _stateBoard;
    /// <summary>Server: this racer's 缘起 registry (null on clients and training dummies).</summary>
    public YuanqiRegistry YuanqiRegistry => _yuanqiRegistry;

    // Every peer, every racer: freeze / burst presentation events and the frozen Animator hold (plain MonoBehaviour).
    private RacerSkillPresentationState _presentationState;
    /// <summary>Every peer: this racer's freeze / burst presentation state (null before the network starts).</summary>
    public RacerSkillPresentationState PresentationState => _presentationState;

    // Owner-only skill keys (Q/E/R and 1/2/3). Combos are retired for everyone (A7): server AI calls CastSlotServer
    // directly, the same authority pipeline the owner RPC reaches.
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

    // Once per peer (the host gets one call): attaches the presentation state on the server, the owner and observers.
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        AttachPresentationState();
    }

    public override void OnStopNetwork()
    {
        if (_presentationState != null)
            _presentationState.Unbind();
        base.OnStopNetwork();
    }

    // Idempotent: a re-spawned object (or a component a HUD added first) is bound again, never duplicated.
    private void AttachPresentationState()
    {
        _presentationState = RacerSkillPresentationState.GetOrAdd(gameObject);
        ResolveMotor();
        ResolveCharacterAnimator();
        _presentationState.Bind(_motor, characterAnimator);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveObsessionFigure();
        if (loadout == null) loadout = GetComponent<SkillLoadout>();
        SetUpYuanqiServer();
    }

    public override void OnStopServer()
    {
        DropPendingCastServer(SkillPendingCancelSource.Teardown);
        TearDownYuanqiServer();
        base.OnStopServer();
    }

    // Every racer except a training dummy (it never casts, but stays a valid 缘起 target). Plain MonoBehaviours added
    // at runtime: no new NetworkBehaviour on the prefab.
    private void SetUpYuanqiServer()
    {
        if (TryGetComponent(out RacerIdentity identity) && identity.IsTrainingDummy)
            return;

        if (_stateBoard == null && !TryGetComponent(out _stateBoard))
            _stateBoard = gameObject.AddComponent<SkillStateBoard>();
        _stateBoard.Bind(TimeManager);

        if (_yuanqiRegistry == null && !TryGetComponent(out _yuanqiRegistry))
            _yuanqiRegistry = gameObject.AddComponent<YuanqiRegistry>();
        _yuanqiRegistry.Bind(this, _stateBoard, database);

        if (loadout != null)
        {
            loadout.ServerSlotsChanged -= OnLoadoutSlotsChangedServer;
            loadout.ServerSlotsChanged += OnLoadoutSlotsChangedServer;
        }
        // The loadout may have been set before this subscription (SkillLoadout.OnStartServer runs in the same Spawn).
        RegisterYuanqiForLoadoutServer();
    }

    private void TearDownYuanqiServer()
    {
        if (loadout != null)
            loadout.ServerSlotsChanged -= OnLoadoutSlotsChangedServer;
        ResetYuanqiStateServer("server stop");
        if (_stateBoard != null)
            _stateBoard.Unbind();
    }

    private void OnLoadoutSlotsChangedServer() => RegisterYuanqiForLoadoutServer();

    private void RegisterYuanqiForLoadoutServer()
    {
        if (_yuanqiRegistry == null || loadout == null)
            return;
        // A loadout change mid-race (diagnostic harnesses) starts from a clean board.
        if (_stateBoard != null)
            _stateBoard.Clear("loadout changed");
        _yuanqiRegistry.RegisterForLoadout(loadout.CopySlotSkillIds());
    }

    /// <summary>
    /// Server: ends every board entry and running 缘起 and forgets which overlaps already triggered. Called by every
    /// server-side reset: the server-controlled reset, ResetActiveSkillEffectsServer for a Human Player, the motor when a
    /// Human Player's modifier-resetting teleport (respawn) is consumed on the server, and server stop.
    /// Running skill effects are not touched here.
    /// </summary>
    internal void ResetYuanqiStateServer(string reason)
    {
        if (_stateBoard != null)
            _stateBoard.Clear(reason);
        if (_yuanqiRegistry != null)
            _yuanqiRegistry.ResetServer(reason);
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
        // The 缘起 board is server-only, so the owner TargetRpc below cannot clear it.
        ResetYuanqiStateServer("reset");
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
        ResetYuanqiStateServer("server-controlled reset");
        CancelPendingCastServer();
        GetComponent<BuddahHandControl>()?.ResetSkillInputAndEffects();
        GetComponent<SkillPerceptionState>()?.Clear();
        if (resetMovementModifiers)
        {
            ResolveMotor();
            // Keeps the backfire max-speed penalty (motor rule), like the respawn teleport reset.
            _motor?.ResetActiveSkillModifiers();
        }
        // Consume, not a bare Destroy: the zone VFX is unparented (it follows the racer to its respawn point), so only
        // the zone's own stop RPC removes it on every peer. The board was already cleared above.
        var trap = GetComponent<MovementSlowTrapZoneEffect>();
        if (trap != null) trap.Consume("server-controlled reset");
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
        _cameraFovPulseTag = null;

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
    /// Server: which gate would block a cast of this slot right now (None = it would be accepted). The same gate
    /// TryResolveCast applies, exposed so the AI can log why a request was refused; it changes no state. An invalid
    /// slot or a non-server peer reports Pending (never Cooldown, which would suggest a readiness time).
    /// </summary>
    internal SkillCastBlock ResolveCastBlockServer(int slotIndex)
    {
        if (!IsServerInitialized || slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount) return SkillCastBlock.Pending;
        return EvaluateCastGate(slotIndex, Time.time);
    }

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
        // A penalty that ended this frame gets its x0.8 before the gate opens, so the backfire roll that follows in
        // CastSlotServer never sees the pre-penalty obsession.
        _obs?.SettleBackfireServer();
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
        CastObserversRpc(cast.Slot, cast.SkillId, cast.SurchargeStacks, ServerTickForHud());
        // After CastObserversRpc: a 缘起 may trigger and consume right here, and its end presentation must reach the
        // clients after the start presentation (trail, FOV) it ends.
        BeginSkillStateServer(cast.Slot, cast.SkillId, cast.Skill);
        ServerCastChanged?.Invoke(new CastEvent(CastStage.Executed, cast.Slot, cast.SkillId));
    }

    // One generic board write per normal execution (a backfire never gets here). No per-skill branches: the window
    // and the handle come from the SkillAction.
    private void BeginSkillStateServer(int slotIndex, string skillId, SkillAction skill)
    {
        if (_stateBoard == null || skill == null)
            return;
        try
        {
            SkillActiveWindow window = skill.GetActiveWindow(this);
            if (window.DurationSeconds <= 0f)
                return;
            _stateBoard.Begin(skillId, slotIndex, window, skill.CreateRuntimeHandle(this));
        }
        catch (Exception exception)
        {
            // Never skip the Executed event (AI commitments wait for it) because a 缘起 failed.
            Debug.LogException(exception, this);
        }
    }

    /// <summary>Table Skill.effectDurationSeconds; without a table row the SO primary duration field; else 0.</summary>
    public float ResolveEffectDurationSeconds(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return 0f;

        if (ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository skills)
            && skills.TryGet(skillId, out SkillRecord record) && record != null && record.effectDurationSeconds > 0f)
            return record.effectDurationSeconds;

        if (database == null || !database.TryGet(skillId, out SkillAction skill) || skill == null)
            return 0f;
        string fieldName = SkillDatabase.EffectDurationField(skill);
        FieldInfo field = fieldName != null ? skill.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;
        return field != null && field.FieldType == typeof(float) ? Mathf.Max(0f, (float)field.GetValue(skill)) : 0f;
    }

    /// <summary>Server: the slot of the skill's live board entry, else its first loadout slot, else -1.</summary>
    internal int ResolveActiveSlot(string skillId)
    {
        if (_stateBoard != null && _stateBoard.TryGetActive(skillId, out SkillStateEntry entry))
            return entry.Slot;
        if (loadout != null)
            for (int i = 0; i < SkillLoadout.SlotCount; i++)
                if (string.Equals(loadout.GetSkillId(i), skillId, StringComparison.Ordinal))
                    return i;
        return -1;
    }

    /// <summary>Server: plays a consumed skill's early-end presentation on every peer (SkillAction.ExecuteConsumedObservers).</summary>
    public void PlaySkillConsumedObservers(string skillId, int slot)
    {
        if (!IsServerInitialized || string.IsNullOrWhiteSpace(skillId)) return;
        SkillConsumedObserversRpc(skillId, slot);
    }

    /// <summary>
    /// Server: plays a 缘起 trigger on every peer (YuanqiAction.PlayObservers, then YuanqiObserved); the owner also
    /// raises LocalYuanqiTriggered.
    /// </summary>
    public void PlayYuanqiObservers(string yuanqiId, YuanqiPresentationPayload payload)
    {
        if (!IsServerInitialized || string.IsNullOrWhiteSpace(yuanqiId)) return;
        if (payload.TargetObjectIds == null)
            payload.TargetObjectIds = Array.Empty<int>();
        YuanqiObserversRpc(yuanqiId, payload);
    }

    /// <summary>Server: one 缘起 body impact on <paramref name="victimObjectId"/>; every peer raises YuanqiImpactObserved.</summary>
    public void PlayYuanqiImpactObservers(string yuanqiId, int victimObjectId, Vector3 point, Vector3 impulse)
    {
        if (!IsServerInitialized) return;
        YuanqiImpactObserversRpc(yuanqiId ?? string.Empty, victimObjectId, point, impulse);
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
        RunBackfireVariantServer(cast);

        // CastObserversRpc is skipped, so the owner HUD learns the spent cooldown here.
        NetworkConnection conn = Owner;
        if (conn != null && conn.IsValid)
            BackfireSkillUiTargetRpc(conn, cast.Slot, cast.CooldownSeconds, ServerTickForHud());

        ServerCastChanged?.Invoke(new CastEvent(CastStage.Executed, cast.Slot, cast.SkillId, backlash: true));
    }

    // User rule 2026-10-31: a backfire applies the unified penalty and always runs the requested skill's own _anti variant
    // (restored as on dev). The variant runs once on the server, right after the penalty starts; it never goes on the 缘起
    // state board, adds no obsession and has no cooldown of its own (the slot keeps the requested skill's cooldown).
    private void RunBackfireVariantServer(QueuedCast cast)
    {
        string antiSkillId = ResolveAntiSkillId(cast.SkillId, cast.Skill);
        if (string.IsNullOrWhiteSpace(antiSkillId) || database == null
            || !database.TryGet(antiSkillId, out SkillAction anti) || anti == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Backfire of '{cast.SkillId}' has no _anti variant ('{antiSkillId}'); only the unified penalty applies.", this);
            return;
        }

        // A failing variant must not skip the Executed event (AI commitments wait for it); the penalty already started.
        try { anti.ExecuteServer(this, cast.Slot); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        BackfireVariantObserversRpc(cast.Slot, antiSkillId);
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Server] BACKFIRE VARIANT '{antiSkillId}' (slot {cast.Slot}) for '{cast.SkillId}'.");
    }

    // Config Skill.antiSkillId, else SkillAction.antiSkillId, else "<id>_anti" if that action exists (as on dev).
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

    // surchargeStacks: the same-skill surcharge stacks this execution paid (owner HUD only).
    // serverTick: the server tick of the execution, so the owner HUD anchors the cooldown on the server's clock.
    [ObserversRpc]
    private void CastObserversRpc(int slotIndex, string skillId, int surchargeStacks, uint serverTick)
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
                LocalSkillUiTriggered?.Invoke(CreateSkillUiEvent(slotIndex, resolvedCooldownSeconds, surchargeStacks, serverTick));

                if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Owner] ExecuteLocal '{skillId}' (slot {slotIndex})");
                skill.ExecuteLocal(this, slotIndex);
            }
        }
    }

    // Owner HUD event; the surcharge window and multiplier come from this peer's ObsessionFigure (same table values).
    // Its age is the server tick of the event against this peer's estimate of the server tick (TimeManager.Tick).
    private LocalSkillUiEvent CreateSkillUiEvent(int slotIndex, float cooldownSeconds, int surchargeStacks, uint serverTick)
    {
        ResolveObsessionFigure();
        float repeatWindowSeconds = _obs != null ? _obs.RepeatWindowSeconds : 0f;
        float repeatGainMultiplier = _obs != null ? _obs.RepeatGainMultiplier : 1f;
        float age = TimeManager != null
            ? LocalSkillUiEvent.SecondsSinceServerTick(serverTick, TimeManager.Tick, TimeManager.TickDelta)
            : 0f;
        return new LocalSkillUiEvent(slotIndex, cooldownSeconds, surchargeStacks, repeatWindowSeconds, repeatGainMultiplier, age);
    }

    // Server: the tick an owner HUD event happened on (0 = unknown, the HUD then anchors at the RPC's arrival).
    private uint ServerTickForHud() => TimeManager != null ? TimeManager.Tick : 0u;

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
    /// slot's cooldown is refunded (only this freeze path refunds), and pending hand input is dropped.
    /// A confirming backfire survives the freeze (SkillCastRules.DropsPendingCast, user rule 2026-10-26): its cooldown
    /// stays spent, nothing is refunded, and the penalty starts when its confirm window ends.
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
        GetComponent<BuddahHandControl>()?.CancelPendingServerInput();
    }

    /// <summary>
    /// Server: ends the acceleration buff now. Prediction path: EndModifier(Acceleration) clears only a positive buff, so
    /// a slow-trap debuff in the shared slot (negative extras, last writer wins) keeps running to its deadline.
    /// Legacy fallback (prediction off): MovementAccelerationEffect.CancelAndRestore drops whatever is merged, slows included.
    /// </summary>
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

    /// <param name="slotTag">Optional owner of the shared FOV pulse slot (the skill id); see SettleCameraFovBoostEarlyLocal.</param>
    public void PlayCameraFovBoostLocal(float fovOffset, float rampInSeconds, float durationSeconds, float settleSeconds, AnimationCurve rampInCurve = null, AnimationCurve settleCurve = null, string slotTag = null)
    {
        ResolvePlayerCamera();

        if (playerCamera == null)
            return;

        if (_cameraFovPulseRoutine != null)
            StopCoroutine(_cameraFovPulseRoutine);

        _cameraFovPulseTag = slotTag;
        _cameraFovPulseRoutine = StartCoroutine(PlayCameraFovBoostRoutine(fovOffset, rampInSeconds, durationSeconds, settleSeconds, rampInCurve, settleCurve));
    }

    /// <summary>
    /// Owner: a 缘起 consumed the skill tagged <paramref name="slotTag"/>. Its FOV boost leaves the hold now and settles
    /// from the current offset, but only while that skill still owns the pulse slot (a later skill's boost is kept).
    /// </summary>
    public void SettleCameraFovBoostEarlyLocal(string slotTag, float settleSeconds, AnimationCurve settleCurve = null)
    {
        if (_cameraFovPulseRoutine == null || string.IsNullOrEmpty(slotTag) || !string.Equals(_cameraFovPulseTag, slotTag, StringComparison.Ordinal))
            return;

        ResolvePlayerCamera();
        StopCoroutine(_cameraFovPulseRoutine);
        _cameraFovPulseRoutine = null;
        _cameraFovPulseTag = null;
        if (playerCamera == null)
            return;

        if (settleSeconds <= 0f)
        {
            playerCamera.SetRuntimeFieldOfViewOffset(0f);
            return;
        }

        float fromOffset = playerCamera.RuntimeFieldOfViewOffset;
        _cameraFovPulseRoutine = StartCoroutine(SkillCameraFov.RunSettle(() => playerCamera, ClearCameraFovPulse, fromOffset, settleSeconds, settleCurve));
    }

    private void ClearCameraFovPulse()
    {
        _cameraFovPulseRoutine = null;
        _cameraFovPulseTag = null;
    }

    /// <summary>Every peer: the acceleration trail stops emitting now and fades out (a consumed 神足通).</summary>
    public void EndAccelerationTrailEarlyLocal()
    {
        ResolveAccelerationTrailController();
        if (accelerationTrailController != null)
            accelerationTrailController.EndEarly();
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
        return SkillCameraFov.Run(() => playerCamera, ClearCameraFovPulse,
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

    // A backfired cast skips CastObserversRpc; the owner HUD still shows the spent slot cooldown. The backfire cleared
    // every skill's surcharge stacks (SurchargeStacks = -1).
    [TargetRpc]
    private void BackfireSkillUiTargetRpc(NetworkConnection conn, int slotIndex, float cooldownSeconds, uint serverTick)
    {
        LocalSkillUiTriggered?.Invoke(CreateSkillUiEvent(slotIndex, cooldownSeconds, LocalSkillUiEvent.StacksClearedByBackfire, serverTick));
    }

    // 缘起 consumed a skill: its early-end presentation, resolved from this client's SkillDatabase like CastObserversRpc.
    [ObserversRpc]
    private void SkillConsumedObserversRpc(string skillId, int slotIndex)
    {
        if (database == null) return;
        if (!database.TryGet(skillId, out SkillAction skill) || skill == null) return;
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Observers] '{skillId}' consumed (slot {slotIndex}), IsOwner={IsOwner}");
        skill.ExecuteConsumedObservers(this, slotIndex, IsOwner);
    }

    // 缘起 triggered: the pair's presentation, resolved from this client's SkillDatabase by id. Owner: HUD toast event.
    [ObserversRpc]
    private void YuanqiObserversRpc(string yuanqiId, YuanqiPresentationPayload payload)
    {
        if (database != null && database.TryGetYuanqi(yuanqiId, out YuanqiAction action) && action != null)
        {
            GameLog.Verbose($"[Yuanqi][Observers] '{yuanqiId}' on {name}, IsOwner={IsOwner}, targets={(payload.TargetObjectIds != null ? payload.TargetObjectIds.Length : 0)}");
            // A failing visual must not swallow the owner's HUD event below.
            try { action.PlayObservers(this, payload, IsOwner); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        else
        {
            Debug.LogWarning($"[Yuanqi][Observers] Unknown yuanqiId '{yuanqiId}' in the client SkillDatabase.");
        }

        // The presentation interface (artists' VFX); a failing listener must not swallow the owner's HUD event.
        try { YuanqiObserved?.Invoke(yuanqiId, payload, IsOwner); }
        catch (Exception exception) { Debug.LogException(exception, this); }

        if (IsOwner)
            LocalYuanqiTriggered?.Invoke(yuanqiId);
    }

    // 缘起 body impact (BuddahBodyImpact, server): presentation only, every peer.
    [ObserversRpc]
    private void YuanqiImpactObserversRpc(string yuanqiId, int victimObjectId, Vector3 point, Vector3 impulse)
    {
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][Observers] '{yuanqiId}' impact by {name} on object {victimObjectId}, impulse={impulse}");
        YuanqiImpactObserved?.Invoke(new YuanqiImpactInfo
        {
            YuanqiId = yuanqiId,
            VictimObjectId = victimObjectId,
            Point = point,
            Impulse = impulse,
        });
    }

    // The anti palm burst (Skill_PushProjectileHands_Anti), restored 2026-10-31; appended last to keep the RPC order.
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

    // A backfire's _anti variant on every peer (presentation and owner feel), resolved from this client's SkillDatabase like
    // CastObserversRpc. Appended last to keep the RPC order (PredictionProtocol v2, unreleased).
    [ObserversRpc]
    private void BackfireVariantObserversRpc(int slotIndex, string antiSkillId)
    {
        if (database == null || !database.TryGet(antiSkillId, out SkillAction anti) || anti == null)
            return;
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillExecutor][Observers] backfire variant '{antiSkillId}' (slot {slotIndex}), IsOwner={IsOwner}");
        anti.ExecuteObservers(this, slotIndex, IsOwner);
        if (IsOwner)
            anti.ExecuteLocal(this, slotIndex);
    }
}
