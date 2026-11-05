using System;
using System.Collections.Generic;

/// <summary>
/// Pure state board logic in ticks (no Unity, no network): live entries, activation at the window start,
/// expiry at the window end, early End and Clear. Every entry gets exactly one Ended, and Activated at most once.
/// One live entry per skill id: a recast of the same skill supersedes the previous entry (its Ended fires before
/// the new entry activates), so an underlying effect that a recast merges (Max of deadlines) is one "生效中" span
/// per execution and TryGetActive is never ambiguous.
/// </summary>
internal sealed class SkillStateBoardCore
{
    internal const string ExpiredReason = "expired";
    internal const string SupersededReason = "superseded";

    private readonly List<SkillStateEntry> _live = new List<SkillStateEntry>();
    private readonly List<SkillStateEntry> _scratch = new List<SkillStateEntry>();
    private int _lastEntryId;

    internal event Action<SkillStateEntry> Activated;
    internal event Action<SkillStateEntry> Ended;

    /// <summary>Entries that have not ended (pending and active), oldest first.</summary>
    internal IReadOnlyList<SkillStateEntry> LiveEntries => _live;

    /// <summary>
    /// Registers one execution. The window is [now + offset, now + offset + duration). Activated fires inside this
    /// call when the offset is zero, otherwise on the first <see cref="Tick"/> at or after the start tick.
    /// Returns null (and registers nothing) for an empty skill id or a zero duration.
    /// </summary>
    internal SkillStateEntry Begin(string skillId, int slot, uint nowTick, uint startOffsetTicks, uint durationTicks, ISkillRuntimeHandle handle)
    {
        if (string.IsNullOrWhiteSpace(skillId) || durationTicks == 0u)
            return null;

        string id = skillId.Trim();
        SkillStateEntry previous = FindLive(id);
        if (previous != null)
            EndEntry(previous, SupersededReason);

        uint startTick = nowTick + startOffsetTicks;
        var entry = new SkillStateEntry
        {
            EntryId = ++_lastEntryId,
            SkillId = id,
            Slot = slot,
            StartTick = startTick,
            UntilTick = startTick + durationTicks,
            Handle = handle,
        };
        _live.Add(entry);

        if (startOffsetTicks == 0u)
            ActivateEntry(entry);
        return entry;
    }

    /// <summary>Starts pending windows that are due, then ends windows that are over (an entry may do both in one tick).</summary>
    internal void Tick(uint nowTick)
    {
        if (_live.Count == 0)
            return;

        _scratch.Clear();
        _scratch.AddRange(_live);
        for (int i = 0; i < _scratch.Count; i++)
        {
            SkillStateEntry entry = _scratch[i];
            if (entry.IsEnded)
                continue;
            if (!entry.IsActivated && nowTick >= entry.StartTick)
                ActivateEntry(entry);
            if (!entry.IsEnded && entry.IsActivated && nowTick >= entry.UntilTick)
                EndEntry(entry, ExpiredReason);
        }
        _scratch.Clear();
    }

    /// <summary>Ends one live entry early. False when it is unknown or already ended.</summary>
    internal bool End(int entryId, string reason)
    {
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i].EntryId != entryId)
                continue;
            EndEntry(_live[i], reason);
            return true;
        }
        return false;
    }

    /// <summary>Ends every live entry (pending ones included; they never activate).</summary>
    internal void Clear(string reason)
    {
        if (_live.Count == 0)
            return;

        // Own copy: Clear can run from an Activated/Ended handler while Tick is iterating its scratch list.
        SkillStateEntry[] snapshot = _live.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (!snapshot[i].IsEnded)
                EndEntry(snapshot[i], reason);
        }
    }

    /// <summary>The live, activated entry of a skill whose window still contains <paramref name="nowTick"/>.</summary>
    internal bool TryGetActive(string skillId, uint nowTick, out SkillStateEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        SkillStateEntry live = FindLive(skillId.Trim());
        if (live == null || !live.IsActivated || nowTick >= live.UntilTick)
            return false;
        entry = live;
        return true;
    }

    private SkillStateEntry FindLive(string skillId)
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_live[i].SkillId, skillId, StringComparison.Ordinal))
                return _live[i];
        }
        return null;
    }

    private void ActivateEntry(SkillStateEntry entry)
    {
        entry.IsActivated = true;
        Activated?.Invoke(entry);
    }

    private void EndEntry(SkillStateEntry entry, string reason)
    {
        if (entry.IsEnded)
            return;
        entry.IsEnded = true;
        entry.EndReason = reason ?? string.Empty;
        _live.Remove(entry);
        Ended?.Invoke(entry);
    }
}
