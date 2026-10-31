using BuddahGo.Match;
using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class ObsessionFigure : NetworkBehaviour
{
    [Header("Obsession Settings")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float minValue = 0f;

    [Header("Runtime")]
    [SerializeField] private float completionGapToLeaderPercent = 0f;
    [SerializeField] private float extraRecoveryPerSecond = 0f;

    [Tooltip("Default drain per second (server authoritative).")]
    [SerializeField] private float drainPerSecond = 1f;

    [Header("Catch-up Recovery")]
    [Tooltip("Quadratic slope 'a' in R(d)=a*d^2, where d is completion gap in percent.")]
    [Min(0f)] [SerializeField] private float catchupRecoveryA = 0.01f;

    [Tooltip("Initial value when spawned.")]
    [SerializeField] private float initialValue = 0f;

    [Header("Backfire (Anti) Probability")]
    [Tooltip("Pmax in the sigmoid function. Treated as a percentage (0-100).")]
    [Range(0f, 100f)]
    [SerializeField] private float backfirePMaxPercent = 75f;

    [Tooltip("k in the sigmoid function (steepness). Larger = steeper curve. For a 0-1000 range, a smaller value (e.g. 0.012) is typical.")]
    [Min(0.0001f)]
    [SerializeField] private float backfireK = 0.012f;

    [Tooltip("Midpoint of the sigmoid on the obsession axis. For a 0-1000 obsession range, 500 is a typical midpoint.")]
    [SerializeField] private float backfireMidpointX = 500f;

    [Tooltip("If obsession is at or above this value, backfire chance stays at Pmax.")]
    [SerializeField] private float backfireKeepPMaxAtOrAboveX = 1000f;

    // Phase A defaults; the names are the planned Skill/Obsession columns (phase B), so the overlay maps them as-is.
    [Header("Same-skill Surcharge")]
    [Tooltip("Seconds after a skill's own cooldown ends during which recasting it compounds its obsession gain.")]
    [Min(0f)] [SerializeField] private float repeatWindowSeconds = 5f;

    [Tooltip("Obsession gain multiplier per surcharge stack, compounding (x1.15 -> x1.3225 -> x1.52).")]
    [Min(1f)] [SerializeField] private float repeatGainMultiplier = 1.15f;

    [Header("Backfire Penalty")]
    [Tooltip("Seconds after the backfired cast's confirm window during which every skill is locked and the obsession decay is paused.")]
    [Min(0f)] [SerializeField] private float backfireLockSeconds = 10f;

    [Tooltip("Max speed multiplier for the caster while the backfire penalty lasts (predicted modifier).")]
    [Range(0.1f, 1f)] [SerializeField] private float backfireMaxSpeedMultiplier = 0.85f;

    [Tooltip("Share of the obsession kept when the backfire penalty ends.")]
    [Range(0f, 1f)] [SerializeField] private float backfireEndKeepRatio = 0.8f;

    public float CurrentBackfireProbabilityPercent => GetBackfireProbabilityPercent(_current.Value);

    private readonly SyncVar<float> _current = new SyncVar<float>();
    // Server tick at which the backfire penalty ends; 0 = inactive. Replicated so the HUD can show the time left.
    private readonly SyncVar<uint> _backfireEndTick = new SyncVar<uint>();

    public event Action<float, float> OnValueChanged;
    public event Action<float, float> OnCompletionGapChanged;

    /// <summary>
    /// Backfire penalty start (true) and end (false), raised once per change on the server and on every client (the
    /// host raises it once). Driven by the replicated end tick: the SyncVar change starts it, and each peer ends it
    /// when its own clock passes the end tick (a client does not wait for the server's 0). A late joiner gets one
    /// start if it joins mid-penalty. When the object stops, a running penalty reports its end.
    /// </summary>
    public event Action<bool> BackfireStateChanged;

    private readonly PresentationFlag _backfireState = new PresentationFlag();

    public float Current => _current.Value;
    public float Max => maxValue;
    public float DrainPerSecond => drainPerSecond;
    public float CompletionGapToLeaderPercent => completionGapToLeaderPercent;
    public float ExtraRecoveryPerSecond => extraRecoveryPerSecond;

    public float RepeatWindowSeconds => Mathf.Max(0f, repeatWindowSeconds);
    public float RepeatGainMultiplier => Mathf.Max(1f, repeatGainMultiplier);
    public float BackfireLockSeconds => Mathf.Max(0f, backfireLockSeconds);
    public float BackfireMaxSpeedMultiplier => Mathf.Clamp(backfireMaxSpeedMultiplier, 0.1f, 1f);

    /// <summary>Seconds left of the backfire penalty in the server clock (the client's estimate on clients); 0 when inactive.</summary>
    public float BackfireSecondsLeft => TimeManager == null
        ? 0f
        : ObsessionBackfireRules.SecondsLeft(_backfireEndTick.Value, TimeManager.Tick, TimeManager.TickDelta);

    /// <summary>True while the backfire penalty locks every skill. Valid on the server and on clients.</summary>
    public bool IsBackfireActive => BackfireSecondsLeft > 0f;

    private void Awake()
    {
        // Skill/Obsession owns the obsession curve; prefab values stay as fallback.
        ProjectConfigDatabase db = DesignTableOverlay.Db;
        if (db != null)
            DesignTableOverlay.Apply(this, DesignTableOverlay.DefaultRow(db.obsessions, r => r.profileId));
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _current.OnChange += OnValueChangedSync;
        _backfireEndTick.OnChange += OnBackfireEndTickChangedSync;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        _current.OnChange -= OnValueChangedSync;
        _backfireEndTick.OnChange -= OnBackfireEndTickChangedSync;
        if (_backfireState.Reset())
            RaiseBackfireStateChanged(false);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _current.Value = Mathf.Clamp(initialValue, minValue, maxValue);
        // A fresh spawn starts clean. Respawn is a teleport, not a spawn, so the penalty survives it.
        _backfireEndTick.Value = 0u;
    }

    private void Update()
    {
        if (IsServerInitialized)
        {
            SettleBackfireServer();

            float gapPercent = GetCompletionGapToLeaderPercent();
            float extraRecovery = catchupRecoveryA * gapPercent * gapPercent;
            extraRecoveryPerSecond = Mathf.Max(0f, extraRecovery);

            float totalRecoveryPerSecond = Mathf.Max(0f, drainPerSecond + extraRecoveryPerSecond);
            // The backfire penalty freezes the bar: no drain and no catch-up while it lasts.
            float drained = ObsessionBackfireRules.Drain(_current.Value, minValue, maxValue, totalRecoveryPerSecond,
                Time.deltaTime, paused: _backfireEndTick.Value != 0u);
            if (drained != _current.Value)
                _current.Value = drained;
        }

        if (IsClientInitialized && IsOwner)
        {
            UpdateCompletionGapToLeaderPercent();
        }

        // Every peer: the client-side end check (this peer's clock passed the end tick) and late joiners.
        RefreshBackfireState();
    }

    private void UpdateCompletionGapToLeaderPercent()
    {
        SetCompletionGapToLeaderPercent(GetCompletionGapToLeaderPercent());
    }

    private float GetCompletionGapToLeaderPercent()
    {
        if (LeaderboardManager.Instance == null)
            return 0f;

        var rankings = LeaderboardManager.Instance.Rankings;
        if (rankings.Count == 0)
            return 0f;

        float leaderPercent = rankings[0].FinalCompletionPercent;
        bool foundSelf = false;
        float selfPercent = 0f;

        for (int i = 0; i < rankings.Count; i++)
        {
            RankEntry entry = rankings[i];
            if (RacerAuthority.Matches(this, entry.RacerId))
            {
                selfPercent = entry.FinalCompletionPercent;
                foundSelf = true;
                break;
            }
        }

        if (!foundSelf)
            return 0f;

        return Mathf.Max(0f, leaderPercent - selfPercent);
    }

    private void SetCompletionGapToLeaderPercent(float value)
    {
        value = Mathf.Max(0f, value);
        float oldValue = completionGapToLeaderPercent;
        if (Mathf.Approximately(oldValue, value))
            return;

        completionGapToLeaderPercent = value;
        OnCompletionGapChanged?.Invoke(oldValue, completionGapToLeaderPercent);
    }

    public void AddServer(float amount)
    {
        if (!IsServerInitialized)
            return;

        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        _current.Value = Mathf.Clamp(_current.Value + amount, minValue, maxValue);
    }

    /// <summary>Server: current × ratio (ratio below 0 counts as 0), clamped to the range.</summary>
    public void MultiplyServer(float ratio)
    {
        if (!IsServerInitialized)
            return;

        _current.Value = ObsessionBackfireRules.Scale(_current.Value, ratio, minValue, maxValue);
    }

    /// <summary>
    /// Server: starts the backfire penalty (SkillExecutor calls it when the backfired cast's confirm window ends).
    /// For backfireLockSeconds every skill is locked and the decay is paused; at the end the obsession is
    /// multiplied by backfireEndKeepRatio. No reset path clears it, so it persists through respawn.
    /// Re-entering while active keeps the later end and applies the end ratio only once.
    /// </summary>
    public void BeginBackfireServer()
    {
        if (!IsServerInitialized || TimeManager == null)
            return;

        uint endTick = ObsessionBackfireRules.EndTick(TimeManager.Tick, BackfireLockSeconds, TimeManager.TickDelta);
        if (endTick == 0u)
        {
            // A zero lock has no penalty window: apply the end immediately.
            MultiplyServer(backfireEndKeepRatio);
            return;
        }

        if (_backfireEndTick.Value != 0u && NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[ObsessionFigure][Server] Backfire re-entered while active on '{name}'; keeping the later end.");

        _backfireEndTick.Value = ObsessionBackfireRules.ExtendEndTick(_backfireEndTick.Value, endTick);
        // The SyncVar callback raises it too; the edge makes the second call a no-op.
        RefreshBackfireState();
        if (NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[ObsessionFigure][Server] Backfire penalty on '{name}' for {BackfireLockSeconds:0.##}s at obsession {_current.Value:0.#}.");
    }

    /// <summary>
    /// Server: ends a penalty whose end tick has been reached (x backfireEndKeepRatio) right now instead of waiting for
    /// Update. The cast gate calls it before reading IsBackfireActive and Current, so a request processed in the frame
    /// the penalty ends rolls against the kept value (README 6.2: obsession at unlock = value at backfire x 0.8).
    /// Idempotent; does nothing on clients or while the penalty runs.
    /// </summary>
    public void SettleBackfireServer()
    {
        if (!IsServerInitialized || TimeManager == null)
            return;
        if (ObsessionBackfireRules.IsEndDue(_backfireEndTick.Value, TimeManager.Tick))
            EndBackfireServer();
    }

    private void EndBackfireServer()
    {
        // Multiply before clearing the end tick: BackfireStateChanged(false) listeners already read the kept value.
        MultiplyServer(backfireEndKeepRatio);
        _backfireEndTick.Value = 0u;
        RefreshBackfireState();
        if (NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[ObsessionFigure][Server] Backfire penalty ended on '{name}'; obsession now {_current.Value:0.#}.");
    }

    private void OnValueChangedSync(float oldValue, float newValue, bool asServer)
    {
        OnValueChanged?.Invoke(oldValue, newValue);
    }

    private void OnBackfireEndTickChangedSync(uint oldTick, uint newTick, bool asServer)
    {
        RefreshBackfireState();
    }

    // Edge of IsBackfireActive; the same flag on server and client, so a host raises each change once.
    private void RefreshBackfireState()
    {
        if (!IsServerInitialized && !IsClientInitialized)
            return;
        if (_backfireState.Update(IsBackfireActive))
            RaiseBackfireStateChanged(_backfireState.Reported);
    }

    private void RaiseBackfireStateChanged(bool active)
    {
        if (NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[ObsessionFigure] Backfire penalty {(active ? "started" : "ended")} on '{name}' (server={IsServerInitialized}, owner={IsOwner}, left={BackfireSecondsLeft:0.##}s).");
        if (BackfireStateChanged == null)
            return;
        // A failing presentation listener must not break the obsession update loop.
        try { BackfireStateChanged(active); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    public float GetBackfireProbabilityPercent(float obsessionValue)
    {
        return ObsessionBackfireRules.ProbabilityPercent(obsessionValue, minValue, maxValue,
            backfirePMaxPercent, backfireK, backfireMidpointX, backfireKeepPMaxAtOrAboveX);
    }
}
