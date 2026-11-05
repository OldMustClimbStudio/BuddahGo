using System;
using System.Collections.Generic;
using BuddahGo.Match;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// 开局登记 + 触发: one per racer next to its SkillStateBoard, server only. When the loadout is set it instantiates a
/// per-racer copy of every enabled pair fully inside the loadout (YuanqiCatalog); combinations outside the loadout
/// have no instance and are never checked. Matching is event-driven on board.Activated:
///   for each registered pair containing the activated skill whose other skill is active on the board and whose
///   (EntryA, EntryB) has not triggered yet → OnTriggerServer(ctx) → consumeA / consumeB (idempotent) → Sustained:
///   remember until either entry ends → OnEndServer → presentation once (SkillExecutor.PlayYuanqiObservers)
///   → SkillCombatEvents "yuanqi-trigger".
/// The registry knows no pair's content; SkillExecutor knows no pair at all.
/// </summary>
[DisallowMultipleComponent]
public sealed class YuanqiRegistry : MonoBehaviour
{
    private const string CombatEventStage = "yuanqi-trigger";

    private readonly YuanqiPairTable<YuanqiAction, YuanqiContext> _table = new YuanqiPairTable<YuanqiAction, YuanqiContext>();
    private readonly List<YuanqiAction> _instances = new List<YuanqiAction>();
    private readonly List<int> _candidates = new List<int>();
    private SkillExecutor _caster;
    private SkillStateBoard _board;
    private SkillDatabase _database;
    private Func<string, int> _activeEntryIdOf;

    /// <summary>The per-racer pair instances for the current loadout, in database order.</summary>
    public IReadOnlyList<YuanqiAction> Registered => _instances;

    internal void Bind(SkillExecutor caster, SkillStateBoard board, SkillDatabase database)
    {
        Unbind();
        _caster = caster;
        _board = board;
        _database = database;
        _activeEntryIdOf = ActiveEntryIdOf;
        if (_board != null)
        {
            _board.Activated += OnActivated;
            _board.Ended += OnEnded;
        }
    }

    private void Unbind()
    {
        if (_board != null)
        {
            _board.Activated -= OnActivated;
            _board.Ended -= OnEnded;
        }
        _board = null;
    }

    /// <summary>Server: rebuilds the per-racer instances for these slots (running Sustained instances end first).</summary>
    public void RegisterForLoadout(IReadOnlyList<string> slotSkillIds)
    {
        var ended = new List<YuanqiContext>();
        _table.ClearRegistrations(ended);
        EndSustained(ended, "re-register");
        DestroyInstances();

        IReadOnlyList<YuanqiAction> all = _database != null ? _database.GetYuanqis() : null;
        List<YuanqiAction> matched = YuanqiCatalog.ForLoadout(all, slotSkillIds);
        for (int i = 0; i < matched.Count; i++)
        {
            YuanqiAction instance = Instantiate(matched[i]);
            instance.name = matched[i].name;
            _instances.Add(instance);
            _table.Register(instance, instance.skillA, instance.skillB, instance.mode == YuanqiMode.Sustained);
        }

        GameLog.Verbose($"[Yuanqi] {CasterName}: registered {_instances.Count} pair(s) for loadout [{Join(slotSkillIds)}]"
            + (_instances.Count > 0 ? ": " + JoinIds(_instances) : string.Empty));
    }

    /// <summary>Server: ends every Sustained instance and forgets which overlaps already triggered (respawn / reset).</summary>
    public void ResetServer(string reason)
    {
        var ended = new List<YuanqiContext>();
        _table.Reset(ended);
        EndSustained(ended, reason);
    }

    private void OnActivated(SkillStateEntry activated)
    {
        if (activated == null || _table.Count == 0)
            return;

        _candidates.Clear();
        _table.CollectCandidates(activated.SkillId, _candidates);
        if (_candidates.Count == 0)
            return;

        // Own copy: a trigger can consume the activated entry or re-enter through nested board events.
        int[] candidates = _candidates.ToArray();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (activated.IsEnded)
                break; // consumed by an earlier pair this tick
            int pairIndex = candidates[i];
            if (!_table.TryMatch(pairIndex, activated.SkillId, activated.EntryId, _activeEntryIdOf, out int entryIdA, out int entryIdB))
                continue;
            SkillStateEntry entryA = activated.EntryId == entryIdA ? activated : FindActive(entryIdA);
            SkillStateEntry entryB = activated.EntryId == entryIdB ? activated : FindActive(entryIdB);
            if (entryA == null || entryB == null)
                continue;
            Trigger(pairIndex, entryA, entryB);
        }
    }

    private void Trigger(int pairIndex, SkillStateEntry entryA, SkillStateEntry entryB)
    {
        YuanqiAction action = _table.PairAt(pairIndex);
        uint tick = _caster != null && _caster.TimeManager != null ? _caster.TimeManager.LocalTick : 0u;
        var ctx = new YuanqiContext(_caster, action, entryA, entryB, tick, _board);
        string reason = "yuanqi:" + action.yuanqiId;

        GameLog.Verbose($"[Yuanqi] TRIGGER '{action.yuanqiId}' on {CasterName}: {entryA.SkillId}#{entryA.EntryId} + {entryB.SkillId}#{entryB.EntryId} tick={tick} mode={action.mode} consume=({action.consumeA},{action.consumeB})");

        try { action.OnTriggerServer(ctx); }
        catch (Exception exception) { Debug.LogException(exception, this); }

        // The table's consume flags, also when the pair did not consume (idempotent if it already did).
        ctx.ConsumeConfigured(reason);

        if (action.mode == YuanqiMode.Sustained)
        {
            if (entryA.IsEnded || entryB.IsEnded)
                EndSustained(new List<YuanqiContext> { ctx }, "entry ended during trigger");
            else
                _table.BeginSustained(pairIndex, entryA.EntryId, entryB.EntryId, ctx);
        }

        if (_caster != null)
            _caster.PlayYuanqiObservers(action.yuanqiId, ctx.Presentation);
        RecordCombatEvents(action, ctx);
    }

    private void OnEnded(SkillStateEntry ended)
    {
        if (ended == null || _table.SustainedCount == 0)
            return;
        var endedSustained = new List<YuanqiContext>();
        _table.TakeSustainedEndedBy(ended.EntryId, endedSustained);
        EndSustained(endedSustained, $"{ended.SkillId}#{ended.EntryId} {ended.EndReason}");
    }

    private void EndSustained(List<YuanqiContext> ended, string reason)
    {
        for (int i = 0; i < ended.Count; i++)
        {
            YuanqiContext ctx = ended[i];
            if (ctx == null || ctx.Action == null)
                continue;
            GameLog.Verbose($"[Yuanqi] END '{ctx.Action.yuanqiId}' on {CasterName} ({reason})");
            try { ctx.Action.OnEndServer(ctx); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private int ActiveEntryIdOf(string skillId)
        => _board != null && _board.TryGetActive(skillId, out SkillStateEntry entry) ? entry.EntryId : 0;

    private SkillStateEntry FindActive(int entryId)
    {
        if (_board == null)
            return null;
        IReadOnlyList<SkillStateEntry> entries = _board.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].EntryId == entryId && entries[i].IsActivated && !entries[i].IsEnded)
                return entries[i];
        }
        return null;
    }

    // Evidence hook: one record per affected target, or one without a target when the pair names none.
    private void RecordCombatEvents(YuanqiAction action, YuanqiContext ctx)
    {
        if (_caster == null)
            return;
        NetworkObject source = _caster.NetworkObject;
        Vector3 position = _caster.transform.position;
        int[] targets = ctx.Presentation.TargetObjectIds;
        if (targets == null || targets.Length == 0)
        {
            SkillCombatEvents.Record(CombatEventStage, action.yuanqiId, source, null, position, true);
            return;
        }

        var spawned = _caster.NetworkManager != null && _caster.NetworkManager.ServerManager != null
            ? _caster.NetworkManager.ServerManager.Objects.Spawned : null;
        for (int i = 0; i < targets.Length; i++)
        {
            NetworkObject target = null;
            if (spawned != null)
                spawned.TryGetValue(targets[i], out target);
            SkillCombatEvents.Record(CombatEventStage, action.yuanqiId, source, target, target != null ? target.transform.position : position, true);
        }
    }

    private void DestroyInstances()
    {
        for (int i = 0; i < _instances.Count; i++)
        {
            if (_instances[i] != null)
                Destroy(_instances[i]);
        }
        _instances.Clear();
    }

    // Teardown only: SkillExecutor.OnStopServer already ended everything gracefully; the racer's components may be
    // gone by now, so running Sustained instances are dropped without OnEndServer.
    private void OnDestroy()
    {
        _table.ClearRegistrations(new List<YuanqiContext>());
        Unbind();
        DestroyInstances();
    }

    private string CasterName => _caster != null ? _caster.name : name;

    private static string Join(IReadOnlyList<string> ids)
    {
        if (ids == null)
            return string.Empty;
        var parts = new string[ids.Count];
        for (int i = 0; i < ids.Count; i++)
            parts[i] = ids[i] ?? string.Empty;
        return string.Join(", ", parts);
    }

    private static string JoinIds(List<YuanqiAction> actions)
    {
        var parts = new string[actions.Count];
        for (int i = 0; i < actions.Count; i++)
            parts[i] = actions[i].yuanqiId;
        return string.Join(", ", parts);
    }
}
