using System;
using UnityEngine;

/// <summary>
/// What a pair gets when it triggers (and again when a Sustained pair ends). EntryA always matches the action's skillA.
/// Consuming goes through the skill's own handle and then ends its board entry; both are idempotent, so the registry can
/// apply consumeA / consumeB after the pair already consumed in its own order.
/// </summary>
public sealed class YuanqiContext
{
    private readonly SkillStateBoard _board;

    internal YuanqiContext(SkillExecutor caster, YuanqiAction action, SkillStateEntry entryA, SkillStateEntry entryB, uint tick, SkillStateBoard board)
    {
        Caster = caster;
        Action = action;
        EntryA = entryA;
        EntryB = entryB;
        Tick = tick;
        _board = board;
    }

    public SkillExecutor Caster { get; }
    public YuanqiAction Action { get; }
    public SkillStateEntry EntryA { get; }
    public SkillStateEntry EntryB { get; }
    /// <summary>Server LocalTick of the trigger.</summary>
    public uint Tick { get; }

    /// <summary>Set by OnTriggerServer; the registry sends it to observers once per trigger.</summary>
    public YuanqiPresentationPayload Presentation;

    /// <summary>Ends skill A's remaining effect (handle.Consume) and its board entry. Idempotent.</summary>
    public void ConsumeA(string reason) => Consume(EntryA, reason);

    /// <summary>Ends skill B's remaining effect (handle.Consume) and its board entry. Idempotent.</summary>
    public void ConsumeB(string reason) => Consume(EntryB, reason);

    private void Consume(SkillStateEntry entry, string reason)
    {
        if (entry == null || entry.IsEnded)
            return;

        if (entry.Handle != null)
        {
            // A skill failing to end its effect must not leave the board claiming the skill still runs.
            try { entry.Handle.Consume(reason); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        if (_board != null)
            _board.End(entry.EntryId, reason);
        GameLog.Verbose($"[Yuanqi] Consumed {entry.SkillId}#{entry.EntryId} ({reason}) handle={(entry.Handle != null ? "yes" : "none")}");
    }
}
