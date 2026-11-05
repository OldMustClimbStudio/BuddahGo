using System;
using UnityEngine;

/// <summary>
/// What a pair gets when it triggers (and again when a Sustained pair ends). EntryA always matches the action's skillA.
/// Consuming goes through the skill's own handle and then ends its board entry; both are idempotent. What is consumed is
/// data (the action's consumeA / consumeB, table Skill/Yuanqi); a pair only picks WHEN through ConsumeConfigured
/// (before or after its effect), and the registry applies the same flags again after OnTriggerServer.
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

    /// <summary>
    /// Consumes the entries the action's consumeA / consumeB flags name (whatever the skillA / skillB order of the row),
    /// nothing else. Idempotent. Pairs call this instead of ConsumeA / ConsumeB so a table edit is never ignored.
    /// </summary>
    public void ConsumeConfigured(string reason)
    {
        if (Action == null)
            return;
        if (Action.consumeA) ConsumeA(reason);
        if (Action.consumeB) ConsumeB(reason);
    }

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
