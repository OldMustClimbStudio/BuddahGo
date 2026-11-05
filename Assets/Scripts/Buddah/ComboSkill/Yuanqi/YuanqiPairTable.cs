using System;
using System.Collections.Generic;

/// <summary>
/// Pure 缘起 bookkeeping for one racer (no Unity, no network): the registered pairs, which pairs a newly activated
/// skill can complete, "once per overlap" (a pair never triggers twice for the same two entry ids) and the running
/// Sustained instances keyed by their two entry ids. <typeparamref name="TPair"/> is the per-racer pair instance,
/// <typeparamref name="TState"/> whatever the caller needs to end a Sustained instance (the YuanqiContext at runtime).
/// </summary>
internal sealed class YuanqiPairTable<TPair, TState> where TPair : class
{
    private readonly struct Registration
    {
        public Registration(TPair pair, string skillA, string skillB, bool sustained)
        {
            Pair = pair; SkillA = skillA; SkillB = skillB; Sustained = sustained;
        }

        public readonly TPair Pair;
        public readonly string SkillA;
        public readonly string SkillB;
        public readonly bool Sustained;
    }

    private readonly struct Running
    {
        public Running(int pairIndex, int entryIdA, int entryIdB, TState state)
        {
            PairIndex = pairIndex; EntryIdA = entryIdA; EntryIdB = entryIdB; State = state;
        }

        public readonly int PairIndex;
        public readonly int EntryIdA;
        public readonly int EntryIdB;
        public readonly TState State;
    }

    private readonly List<Registration> _pairs = new List<Registration>();
    private readonly HashSet<(int pairIndex, int entryIdA, int entryIdB)> _triggered = new HashSet<(int, int, int)>();
    private readonly List<Running> _sustained = new List<Running>();

    internal int Count => _pairs.Count;
    internal int SustainedCount => _sustained.Count;
    internal TPair PairAt(int pairIndex) => _pairs[pairIndex].Pair;
    internal bool IsSustained(int pairIndex) => _pairs[pairIndex].Sustained;

    internal void Register(TPair pair, string skillA, string skillB, bool sustained)
    {
        _pairs.Add(new Registration(pair, Normalize(skillA), Normalize(skillB), sustained));
    }

    /// <summary>Drops every registration and overlap memory; running Sustained states are handed back to be ended.</summary>
    internal void ClearRegistrations(List<TState> endedSustained)
    {
        Reset(endedSustained);
        _pairs.Clear();
    }

    /// <summary>Indices of the registered pairs that contain <paramref name="skillId"/>, in registration order.</summary>
    internal void CollectCandidates(string skillId, List<int> results)
    {
        string id = Normalize(skillId);
        if (id.Length == 0)
            return;
        for (int i = 0; i < _pairs.Count; i++)
        {
            if (string.Equals(_pairs[i].SkillA, id, StringComparison.Ordinal) || string.Equals(_pairs[i].SkillB, id, StringComparison.Ordinal))
                results.Add(i);
        }
    }

    /// <summary>
    /// The activated entry completes pair <paramref name="pairIndex"/> when the pair's other skill has an active entry
    /// (<paramref name="activeEntryIdOf"/> returns its id, or 0 or less when none) and these two entries have not
    /// triggered the pair yet. On success the overlap is claimed and the ids come back ordered as (skillA, skillB).
    /// </summary>
    internal bool TryMatch(int pairIndex, string activatedSkillId, int activatedEntryId, Func<string, int> activeEntryIdOf,
        out int entryIdA, out int entryIdB)
    {
        entryIdA = 0;
        entryIdB = 0;
        if (pairIndex < 0 || pairIndex >= _pairs.Count || activeEntryIdOf == null)
            return false;

        Registration pair = _pairs[pairIndex];
        string id = Normalize(activatedSkillId);
        bool activatedIsA = string.Equals(pair.SkillA, id, StringComparison.Ordinal);
        bool activatedIsB = string.Equals(pair.SkillB, id, StringComparison.Ordinal);
        if (!activatedIsA && !activatedIsB)
            return false;

        int otherEntryId = activeEntryIdOf(activatedIsA ? pair.SkillB : pair.SkillA);
        if (otherEntryId <= 0 || otherEntryId == activatedEntryId)
            return false;

        int a = activatedIsA ? activatedEntryId : otherEntryId;
        int b = activatedIsA ? otherEntryId : activatedEntryId;
        if (!_triggered.Add((pairIndex, a, b)))
            return false;

        entryIdA = a;
        entryIdB = b;
        return true;
    }

    internal void BeginSustained(int pairIndex, int entryIdA, int entryIdB, TState state)
    {
        _sustained.Add(new Running(pairIndex, entryIdA, entryIdB, state));
    }

    /// <summary>Removes and returns the Sustained instances that used <paramref name="endedEntryId"/>.</summary>
    internal void TakeSustainedEndedBy(int endedEntryId, List<TState> results)
    {
        int i = 0;
        while (i < _sustained.Count)
        {
            Running running = _sustained[i];
            if (running.EntryIdA != endedEntryId && running.EntryIdB != endedEntryId)
            {
                i++;
                continue;
            }
            _sustained.RemoveAt(i);
            results.Add(running.State); // oldest first, as they began
        }
    }

    /// <summary>Removes and returns every Sustained instance and forgets which overlaps already triggered.</summary>
    internal void Reset(List<TState> endedSustained)
    {
        for (int i = 0; i < _sustained.Count; i++)
            endedSustained.Add(_sustained[i].State);
        _sustained.Clear();
        _triggered.Clear();
    }

    private static string Normalize(string skillId) => (skillId ?? string.Empty).Trim();
}
