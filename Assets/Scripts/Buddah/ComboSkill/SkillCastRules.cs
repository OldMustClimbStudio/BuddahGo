using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a slot cannot start a cast right now. <see cref="None"/> means the gate is open.</summary>
internal enum SkillCastBlock { None, Pending, BackfireLocked, Frozen, Cooldown }

/// <summary>What a confirming cast does when its confirm window ends.</summary>
internal enum SkillConfirmOutcome { Execute, Backfire, Cancel }

/// <summary>Who asks to drop a confirming cast before its window ends.</summary>
internal enum SkillPendingCancelSource
{
    /// <summary>An ordinary cancel: AI commitment cancels, server-controlled respawn/teleport resets.</summary>
    Request,
    /// <summary>A freeze (frost lock) interrupt, which also refunds the slot cooldown.</summary>
    Freeze,
    /// <summary>The executor stops (server stop, component disabled): its coroutine cannot run any more.</summary>
    Teardown
}

/// <summary>Pure cast-rhythm rules shared by <see cref="SkillExecutor"/> request handling and AI readiness.</summary>
internal static class SkillCastRules
{
    /// <summary>Backfire percentages at or below this never roll (keeps a zeroed curve deterministic).</summary>
    internal const float MinRollableBackfirePercent = 0.0001f;

    /// <summary>
    /// One gate for TryResolveCast and IsSlotReadyServer. There is no global lock between skills:
    /// casts are serialized (one confirming cast at a time), a backfire or a freeze locks every slot,
    /// otherwise only the slot's own cooldown applies.
    /// </summary>
    internal static SkillCastBlock EvaluateGate(bool hasPendingCast, bool backfireActive, bool frozen, float now, float slotReadyTime)
    {
        if (hasPendingCast) return SkillCastBlock.Pending;
        if (backfireActive) return SkillCastBlock.BackfireLocked;
        if (frozen) return SkillCastBlock.Frozen;
        if (now < slotReadyTime) return SkillCastBlock.Cooldown;
        return SkillCastBlock.None;
    }

    /// <summary>The request-time backfire draw. <paramref name="roll01"/> is a uniform value in [0, 1).</summary>
    internal static bool RollsBackfire(float backfirePercent, float roll01)
        => backfirePercent > MinRollableBackfirePercent && roll01 * 100f < backfirePercent;

    /// <summary>
    /// The confirm-time decision. A server AI drops its cast once the race no longer runs for it (nothing left to
    /// execute or penalise). A backfire is resolved before the AI continuation recheck: that recheck only avoids a
    /// duplicate global effect, and a backfire runs no skill, so an AI cannot escape the penalty a Human Player
    /// always takes. <paramref name="aiInRace"/> and <paramref name="aiCanContinue"/> are ignored for anyone else.
    /// </summary>
    internal static SkillConfirmOutcome ResolveConfirmOutcome(bool isBackfire, bool isServerAI, bool aiInRace, bool aiCanContinue)
    {
        if (isServerAI && !aiInRace) return SkillConfirmOutcome.Cancel;
        if (isBackfire) return SkillConfirmOutcome.Backfire;
        if (isServerAI && !aiCanContinue) return SkillConfirmOutcome.Cancel;
        return SkillConfirmOutcome.Execute;
    }

    /// <summary>
    /// Whether a confirming cast is dropped. A rolled backfire survives every ordinary cancel and a freeze interrupt
    /// (user rule 2026-10-26: being frozen never lets a caster escape a backfire already rolled at the key press; its
    /// cooldown stays spent and the penalty starts when the window ends). Only executor teardown drops it.
    /// </summary>
    internal static bool DropsPendingCast(bool isBackfire, SkillPendingCancelSource source)
        => !isBackfire || source == SkillPendingCancelSource.Teardown;
}

/// <summary>
/// Same-skill surcharge (同招加价), keyed by skill id. The anchor is the cooldown end of that skill's last
/// successful execution; a request inside [anchor, anchor + window] stacks once more, any other request starts
/// over at zero. Stacks are resolved at request time and committed only when the cast executes normally, so
/// cancelled and backfired casts never count. A backfire clears every skill's stacks.
/// </summary>
internal sealed class SkillRepeatSurcharge
{
    private struct Entry
    {
        public float AnchorTime;
        public int Stacks;
    }

    private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

    internal int ResolveStacks(string skillId, float requestTime, float windowSeconds)
    {
        if (string.IsNullOrEmpty(skillId) || !_entries.TryGetValue(skillId, out Entry entry))
            return 0;
        bool inWindow = requestTime >= entry.AnchorTime && requestTime <= entry.AnchorTime + Mathf.Max(0f, windowSeconds);
        return inWindow ? entry.Stacks + 1 : 0;
    }

    internal void CommitExecution(string skillId, int stacks, float cooldownEndTime)
    {
        if (string.IsNullOrEmpty(skillId))
            return;
        _entries[skillId] = new Entry { AnchorTime = cooldownEndTime, Stacks = Mathf.Max(0, stacks) };
    }

    internal int CommittedStacks(string skillId)
        => !string.IsNullOrEmpty(skillId) && _entries.TryGetValue(skillId, out Entry entry) ? entry.Stacks : 0;

    internal void Clear() => _entries.Clear();

    /// <summary>gain = base × multiplier^stacks (×1.15 → ×1.3225 → ×1.520875 with the default multiplier).</summary>
    internal static float ResolveGain(float baseGain, float gainMultiplier, int stacks)
    {
        float gain = Mathf.Max(0f, baseGain);
        if (stacks <= 0)
            return gain;
        return gain * Mathf.Pow(Mathf.Max(1f, gainMultiplier), stacks);
    }
}
