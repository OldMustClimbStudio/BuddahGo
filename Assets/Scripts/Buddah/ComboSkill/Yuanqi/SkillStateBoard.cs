using System;
using System.Collections.Generic;
using FishNet.Managing.Timing;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

/// <summary>
/// 技能状态板: which skills are "生效中" on this Buddah right now. Server only and never synced; added at runtime by
/// SkillExecutor.OnStartServer for every racer except training dummies (they never cast, but stay valid targets).
/// SkillExecutor writes one entry per normal execution (after CastObserversRpc, never for a backfire); expiry runs on
/// TimeManager.OnTick, early ends come from a 缘起 consuming the skill (YuanqiContext) and Clear from resets.
/// The board knows no pairs; YuanqiRegistry listens to Activated / Ended.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillStateBoard : MonoBehaviour
{
    private readonly SkillStateBoardCore _core = new SkillStateBoardCore();
    private TimeManager _timeManager;
    private Func<uint> _clock;
    private double _tickDelta;

    /// <summary>The window starts (immediately for offset 0, at StartTick otherwise).</summary>
    public event Action<SkillStateEntry> Activated
    {
        add => _core.Activated += value;
        remove => _core.Activated -= value;
    }

    /// <summary>Expiry, End(), a recast superseding the entry, or Clear(). Once per entry.</summary>
    public event Action<SkillStateEntry> Ended
    {
        add => _core.Ended += value;
        remove => _core.Ended -= value;
    }

    /// <summary>Live entries (pending and active), oldest first. Read-only view for diagnostics and readouts.</summary>
    public IReadOnlyList<SkillStateEntry> Entries => _core.LiveEntries;

    /// <summary>Server: ticks the board with the network clock (LocalTick on the server).</summary>
    internal void Bind(TimeManager timeManager)
    {
        Unbind();
        _timeManager = timeManager;
        if (_timeManager == null)
            return;
        _clock = () => _timeManager.LocalTick;
        _tickDelta = _timeManager.TickDelta;
        _timeManager.OnTick += OnTick;
    }

    /// <summary>Tests: a clock without a TimeManager (no OnTick; the core is ticked by its own tests).</summary>
    internal void BindClock(Func<uint> clock, double tickDelta)
    {
        Unbind();
        _clock = clock;
        _tickDelta = tickDelta;
    }

    internal void Unbind()
    {
        if (_timeManager != null)
            _timeManager.OnTick -= OnTick;
        _timeManager = null;
        _clock = null;
    }

    private uint Now => _clock != null ? _clock() : 0u;

    /// <summary>
    /// Registers one execution of <paramref name="skillId"/>. Returns null when the window has no duration or the
    /// board has no clock. A recast of the same skill ends its previous entry first (reason "superseded").
    /// </summary>
    public SkillStateEntry Begin(string skillId, int slot, SkillActiveWindow window, ISkillRuntimeHandle handle)
    {
        if (window.DurationSeconds <= 0f)
            return null;
        if (_clock == null)
        {
            Debug.LogWarning($"[Yuanqi][Board] {name}: no clock bound, '{skillId}' not registered.", this);
            return null;
        }

        float tickDelta = (float)_tickDelta;
        uint offsetTicks = BuddahTickMath.DurationToTicks(window.StartOffsetSeconds, tickDelta);
        uint durationTicks = BuddahTickMath.DurationToTicks(window.DurationSeconds, tickDelta);
        SkillStateEntry entry = _core.Begin(skillId, slot, Now, offsetTicks, durationTicks, handle);
        if (entry != null && NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[Yuanqi][Board] {name}: begin {entry} window {window} handle={(handle != null ? handle.GetType().Name : "none")}");
        return entry;
    }

    public void End(int entryId, string reason)
    {
        if (_core.End(entryId, reason) && NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[Yuanqi][Board] {name}: end #{entryId} ({reason})");
    }

    /// <summary>The live, started entry of a skill whose window contains the current tick.</summary>
    public bool TryGetActive(string skillId, out SkillStateEntry entry) => _core.TryGetActive(skillId, Now, out entry);

    /// <summary>Ends every entry (Ended once each). Respawn / server-controlled reset / server stop.</summary>
    public void Clear(string reason)
    {
        if (_core.LiveEntries.Count > 0 && NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[Yuanqi][Board] {name}: clear {_core.LiveEntries.Count} entr(y/ies) ({reason})");
        _core.Clear(reason);
    }

    private void OnTick()
    {
        if (_clock != null)
            _core.Tick(_clock());
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
