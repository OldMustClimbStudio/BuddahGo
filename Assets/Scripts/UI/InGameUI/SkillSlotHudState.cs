using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>What one skill slot of the owner HUD shows. Later members win: a lock hides the cooldown label.</summary>
internal enum SkillSlotHudKind
{
    Ready,
    /// <summary>Cooldown over and a recast now pays the next same-skill surcharge stack (同招加价).</summary>
    SurchargeWindow,
    Cooldown,
    /// <summary>Backfire penalty: every slot is locked.</summary>
    BackfireLocked,
    /// <summary>Frost lock (freeze): every slot is locked.</summary>
    Frozen
}

/// <summary>One sample of a slot: the shown state plus the cooldown fill, which keeps draining under a lock.</summary>
internal readonly struct SkillSlotHudView
{
    public SkillSlotHudView(SkillSlotHudKind kind, float secondsLeft, bool isCoolingDown, float cooldownFill01,
        int nextSurchargeStacks, float nextGainMultiplier)
    {
        Kind = kind;
        SecondsLeft = secondsLeft;
        IsCoolingDown = isCoolingDown;
        CooldownFill01 = cooldownFill01;
        NextSurchargeStacks = nextSurchargeStacks;
        NextGainMultiplier = nextGainMultiplier;
    }

    public SkillSlotHudKind Kind { get; }
    /// <summary>Seconds left of the shown state (cooldown, window, penalty or freeze); 0 when Ready.</summary>
    public float SecondsLeft { get; }
    public bool IsCoolingDown { get; }
    /// <summary>1 at execution, 0 at cooldown end. There is no cast-lock hold any more (the global lock was retired).</summary>
    public float CooldownFill01 { get; }
    /// <summary>Stacks a recast inside the surcharge window pays; 0 outside it.</summary>
    public int NextSurchargeStacks { get; }
    /// <summary>repeatGainMultiplier ^ NextSurchargeStacks; 1 outside the window.</summary>
    public float NextGainMultiplier { get; }
    public bool IsLocked => Kind == SkillSlotHudKind.BackfireLocked || Kind == SkillSlotHudKind.Frozen;
}

/// <summary>
/// Owner-side timeline of one skill slot, fed by <see cref="SkillExecutor.LocalSkillUiEvent"/> (times in the local
/// clock; the caller passes the event time as the server saw it: arrival minus
/// <see cref="SkillExecutor.LocalSkillUiEvent.SecondsSinceServerEvent"/>, so a remote client keeps only the uplink
/// delay, on the safe side, like the tick-based backfire and freeze timers).
/// Mirrors the server rules: the cooldown starts at execution (a backfire still spends it); the same-skill
/// surcharge anchor is the cooldown end of that skill's last normal execution, a recast inside
/// [anchor, anchor + window] pays one stack more (SkillRepeatSurcharge), and a backfire clears every skill's stacks.
/// </summary>
internal sealed class SkillSlotHudTimeline
{
    private bool _hasCooldown;
    private float _cooldownStart;
    private float _cooldownEnd;

    private bool _hasSurcharge;
    private float _surchargeAnchor;
    private int _paidStacks;
    private float _windowSeconds;
    private float _gainMultiplier = 1f;

    /// <summary>
    /// One owner HUD event. <paramref name="isOwnSlot"/>: the event is for this slot (its cooldown starts).
    /// <paramref name="isSameSkill"/>: the event's skill is this slot's skill (a duplicate in another slot shares the
    /// server's per-skill stacks). <paramref name="surchargeStacks"/> &lt; 0 means a backfire cleared every stack.
    /// </summary>
    internal void OnSkillUiEvent(float now, bool isOwnSlot, bool isSameSkill, float cooldownSeconds, int surchargeStacks,
        float repeatWindowSeconds, float repeatGainMultiplier)
    {
        float cooldown = Mathf.Max(0f, cooldownSeconds);
        if (isOwnSlot)
        {
            _hasCooldown = cooldown > 0f;
            _cooldownStart = now;
            _cooldownEnd = now + cooldown;
        }

        if (surchargeStacks < 0)
        {
            ClearSurcharge();
            return;
        }

        if (!isOwnSlot && !isSameSkill)
            return;

        _hasSurcharge = repeatWindowSeconds > 0f;
        _surchargeAnchor = now + cooldown;
        _paidStacks = surchargeStacks;
        _windowSeconds = Mathf.Max(0f, repeatWindowSeconds);
        _gainMultiplier = Mathf.Max(1f, repeatGainMultiplier);
    }

    internal void ClearSurcharge()
    {
        _hasSurcharge = false;
        _paidStacks = 0;
    }

    internal void Reset()
    {
        _hasCooldown = false;
        _cooldownStart = _cooldownEnd = 0f;
        ClearSurcharge();
    }

    /// <summary>Priority: Frozen, BackfireLocked, Cooldown, SurchargeWindow, Ready.</summary>
    internal SkillSlotHudView Sample(float now, float backfireSecondsLeft, float frozenSecondsLeft)
    {
        bool cooling = _hasCooldown && now < _cooldownEnd;
        float cooldownLeft = cooling ? _cooldownEnd - now : 0f;
        float duration = _cooldownEnd - _cooldownStart;
        float fill = cooling && duration > 0f ? Mathf.Clamp01(cooldownLeft / duration) : 0f;

        bool inWindow = _hasSurcharge && !cooling && now >= _surchargeAnchor && now <= _surchargeAnchor + _windowSeconds;
        int nextStacks = inWindow ? _paidStacks + 1 : 0;
        float nextMultiplier = inWindow ? Mathf.Pow(_gainMultiplier, nextStacks) : 1f;

        SkillSlotHudKind kind;
        float secondsLeft;
        if (frozenSecondsLeft > 0f) { kind = SkillSlotHudKind.Frozen; secondsLeft = frozenSecondsLeft; }
        else if (backfireSecondsLeft > 0f) { kind = SkillSlotHudKind.BackfireLocked; secondsLeft = backfireSecondsLeft; }
        else if (cooling) { kind = SkillSlotHudKind.Cooldown; secondsLeft = cooldownLeft; }
        else if (inWindow) { kind = SkillSlotHudKind.SurchargeWindow; secondsLeft = Mathf.Max(0f, _surchargeAnchor + _windowSeconds - now); }
        else { kind = SkillSlotHudKind.Ready; secondsLeft = 0f; }

        return new SkillSlotHudView(kind, secondsLeft, cooling, fill, nextStacks, nextMultiplier);
    }
}

/// <summary>Label prefixes for the slot status line (already resolved for the font; see InGameHudLabels).</summary>
internal readonly struct SkillSlotHudLabels
{
    public SkillSlotHudLabels(string backfire, string frozen, string surcharge)
    {
        Backfire = backfire ?? string.Empty;
        Frozen = frozen ?? string.Empty;
        Surcharge = surcharge ?? string.Empty;
    }

    public string Backfire { get; }
    public string Frozen { get; }
    public string Surcharge { get; }
}

/// <summary>Text of the slot HUD: key label and status line. ASCII digits and punctuation only, besides the labels.</summary>
internal static class SkillSlotHudFormat
{
    private static readonly string[] DefaultKeyLabels = { "Q/1", "E/2", "R/3" };
    // Absorbs float error so an exact tenth (4.2 s) does not round up to the next one.
    private const float TenthRoundingEpsilon = 1e-3f;
    private const float TenthsPerSecond = 10f;

    /// <summary>The bindings of the contract (Q/E/R and 1/2/3); used when the input actions cannot be read.</summary>
    internal static string DefaultKeyLabel(int slotIndex)
        => slotIndex >= 0 && slotIndex < DefaultKeyLabels.Length ? DefaultKeyLabels[slotIndex] : string.Empty;

    /// <summary>"&lt;Keyboard&gt;/q", "&lt;Keyboard&gt;/1" → "Q/1": control names in binding order, upper-cased, distinct.</summary>
    internal static string KeyLabelFromBindingPaths(IEnumerable<string> bindingPaths)
    {
        if (bindingPaths == null)
            return string.Empty;
        var keys = new List<string>();
        foreach (string path in bindingPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            string trimmed = path.Trim();
            int slash = trimmed.LastIndexOf('/');
            string control = (slash >= 0 ? trimmed.Substring(slash + 1) : trimmed).Trim().ToUpperInvariant();
            if (control.Length > 0 && !keys.Contains(control))
                keys.Add(control);
        }
        return string.Join("/", keys);
    }

    /// <summary>Seconds rounded up to the next tenth, so an active state never reads 0.0: "4.2s".</summary>
    internal static string Seconds(float seconds)
    {
        float tenths = Mathf.Max(0f, Mathf.Ceil(seconds * TenthsPerSecond - TenthRoundingEpsilon));
        return (tenths / TenthsPerSecond).ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }

    /// <summary>"x1.15", "x1.32", "x1.52".</summary>
    internal static string Multiplier(float multiplier)
        => "x" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);

    internal static string Status(in SkillSlotHudView view, in SkillSlotHudLabels labels)
    {
        switch (view.Kind)
        {
            case SkillSlotHudKind.Frozen: return Prefixed(labels.Frozen, Seconds(view.SecondsLeft));
            case SkillSlotHudKind.BackfireLocked: return Prefixed(labels.Backfire, Seconds(view.SecondsLeft));
            case SkillSlotHudKind.Cooldown: return Seconds(view.SecondsLeft);
            case SkillSlotHudKind.SurchargeWindow:
                return labels.Surcharge + Multiplier(view.NextGainMultiplier) + " " + Seconds(view.SecondsLeft);
            default: return string.Empty;
        }
    }

    /// <summary>Change key of a view at label precision: the HUD rewrites its text only when this changes.</summary>
    internal static int ChangeKey(in SkillSlotHudView view)
    {
        int tenths = Mathf.CeilToInt(view.SecondsLeft * TenthsPerSecond - TenthRoundingEpsilon);
        return HashCode.Combine((int)view.Kind, tenths, view.NextSurchargeStacks);
    }

    private static string Prefixed(string label, string value)
        => string.IsNullOrEmpty(label) ? value : label + " " + value;
}
