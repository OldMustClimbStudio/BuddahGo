using System;
using System.Collections.Generic;

namespace BuddahGo.AI
{
    // One 缘起 pair of the AI's own loadout, mapped to slots. Pairs come from the same SkillDatabase.GetYuanqis() +
    // YuanqiCatalog.ForLoadout the server registry uses; nothing here invents a pair or its content.
    public sealed class AIYuanqiPair
    {
        public string YuanqiId;
        public string SkillA, SkillB;
        public int SlotA, SlotB;
        public bool ConsumeA, ConsumeB;
        public bool Sustained;
        // Cast order (README §5.1, 已定: the skill the pair consumes goes first). FirstSlot/SecondSlot are slot indices.
        public int FirstSlot, SecondSlot;
        public string FirstSkillId => FirstSlot == SlotA ? SkillA : SkillB;
        public string SecondSkillId => SecondSlot == SlotA ? SkillA : SkillB;
        public bool Contains(int slot) => slot == SlotA || slot == SlotB;
        public int Other(int slot) => slot == SlotA ? SlotB : SlotA;
        public string SkillOf(int slot) => slot == SlotA ? SkillA : SkillB;
        public bool Consumes(int slot) => slot == SlotA ? ConsumeA : ConsumeB;
    }

    // A follow-up candidate: the other skill of Pair is active on the state board; casting Slot within ValidSeconds
    // lands the second execution inside the first skill's window and triggers the pair.
    public readonly struct AIYuanqiFollowUp
    {
        public readonly AIYuanqiPair Pair;
        public readonly int Slot;
        public readonly float ValidSeconds, RemainingSeconds;
        public AIYuanqiFollowUp(AIYuanqiPair pair, int slot, float validSeconds, float remainingSeconds)
        { Pair = pair; Slot = slot; ValidSeconds = validSeconds; RemainingSeconds = remainingSeconds; }
    }

    // 缘起 decisions for one AI loadout (建议（实现采用）, A7). Pure logic: no components, no scene state, so the
    // EditMode tests drive it with hand-made pairs and board entries. AISkillCaster.Decide asks it per slot:
    // a follow-up candidate gets the follow-up score/validity/multiplier, a setup candidate the setup multiplier.
    public sealed class AIYuanqiPlanner
    {
        private readonly List<AIYuanqiPair> _pairs = new List<AIYuanqiPair>();
        public IReadOnlyList<AIYuanqiPair> Pairs => _pairs;

        // Order rule: only one skill consumed → it goes first (enjoy its effect, then let the other land); both or
        // neither consumed → the longer main effect first (Sustained overlaps longest; #2 keeps the second cast's
        // confirm inside the first window); equal durations → skillA first.
        public static AIYuanqiPlanner Build(IReadOnlyList<string> loadoutSkillIds, IEnumerable<YuanqiAction> yuanqis, Func<string, float> effectDurationSeconds)
        {
            var planner = new AIYuanqiPlanner();
            if (loadoutSkillIds == null || yuanqis == null) return planner;
            foreach (YuanqiAction action in YuanqiCatalog.ForLoadout(yuanqis, loadoutSkillIds))
            {
                string a = action.skillA.Trim(), b = action.skillB.Trim();
                int slotA = IndexOf(loadoutSkillIds, a), slotB = IndexOf(loadoutSkillIds, b);
                if (slotA < 0 || slotB < 0 || slotA == slotB) continue;
                var pair = new AIYuanqiPair
                {
                    YuanqiId = action.yuanqiId.Trim(), SkillA = a, SkillB = b, SlotA = slotA, SlotB = slotB,
                    ConsumeA = action.consumeA, ConsumeB = action.consumeB, Sustained = action.mode == YuanqiMode.Sustained
                };
                bool aFirst;
                if (pair.ConsumeA != pair.ConsumeB) aFirst = pair.ConsumeA;
                else
                {
                    float durationA = effectDurationSeconds != null ? effectDurationSeconds(a) : 0f;
                    float durationB = effectDurationSeconds != null ? effectDurationSeconds(b) : 0f;
                    aFirst = durationA >= durationB;
                }
                pair.FirstSlot = aFirst ? slotA : slotB; pair.SecondSlot = aFirst ? slotB : slotA;
                planner._pairs.Add(pair);
            }
            return planner;
        }

        // Slot is a follow-up when the other skill of one of its pairs is active on the board (IsActivated && !IsEnded)
        // with at least confirmSeconds + delta left; the candidate is valid for remaining − confirmSeconds. When the
        // active skill is the one this pair consumes (#2, #4: 神足通), the follow-up waits until at most
        // confirmSeconds + lateWindowSeconds remain so its effect is enjoyed first ("晚打"); otherwise (#1) it is a
        // candidate at once so the overlap is as long as possible. Several pairs → the most urgent window wins.
        public bool TryGetFollowUp(int slot, IReadOnlyList<SkillStateEntry> boardEntries, uint tick, float delta,
            float confirmSeconds, float lateWindowSeconds, out AIYuanqiFollowUp followUp)
        {
            followUp = default;
            if (boardEntries == null || boardEntries.Count == 0) return false;
            bool found = false;
            for (int p = 0; p < _pairs.Count; p++)
            {
                AIYuanqiPair pair = _pairs[p];
                if (!pair.Contains(slot)) continue;
                int other = pair.Other(slot);
                string otherSkill = pair.SkillOf(other);
                for (int i = 0; i < boardEntries.Count; i++)
                {
                    SkillStateEntry entry = boardEntries[i];
                    if (entry == null || !entry.IsActivated || entry.IsEnded || entry.UntilTick <= tick) continue;
                    if (!string.Equals(entry.SkillId, otherSkill, StringComparison.Ordinal)) continue;
                    float remaining = (entry.UntilTick - tick) * delta;
                    if (remaining < confirmSeconds + delta) continue;
                    if (pair.Consumes(other) && remaining > confirmSeconds + lateWindowSeconds) continue;
                    float valid = remaining - confirmSeconds;
                    if (found && valid >= followUp.ValidSeconds) continue;
                    followUp = new AIYuanqiFollowUp(pair, slot, valid, remaining); found = true;
                }
            }
            return found;
        }

        // Slot is a setup when it is the FirstSlot of a pair whose SecondSlot will be off cooldown at least
        // confirmSeconds before the first effect ends. The first effect runs from now + confirm for its duration, and
        // the second cast must be requested confirm before that end, so the second slot must be ready within the
        // first skill's effect duration.
        public bool IsSetup(int slot, Func<int, float> slotReadyInSeconds, Func<string, float> effectDurationSeconds, float confirmSeconds, out AIYuanqiPair pair)
        {
            pair = null; float bestDuration = 0f;
            for (int p = 0; p < _pairs.Count; p++)
            {
                AIYuanqiPair candidate = _pairs[p];
                if (candidate.FirstSlot != slot) continue;
                float duration = effectDurationSeconds != null ? effectDurationSeconds(candidate.FirstSkillId) : 0f;
                float readyIn = slotReadyInSeconds != null ? slotReadyInSeconds(candidate.SecondSlot) : float.PositiveInfinity;
                // Second request time ≤ first effect end − confirm  ⇔  readyIn + confirm ≤ confirm + duration.
                if (duration <= 0f || readyIn + confirmSeconds > confirmSeconds + duration) continue;
                if (pair == null || duration > bestDuration) { pair = candidate; bestDuration = duration; }
            }
            return pair != null;
        }

        // Risk of the whole pair (已定: both gains are estimated together): the second cast rolls against the
        // obsession after the first skill's gain (SkillDatabase.GetObsessionGain, no same-skill surcharge).
        public static float SecondBackfireProbability(AIYuanqiPair pair, float currentObsession, Func<string, float> obsessionGain, Func<float, float> backfirePercent)
        {
            if (pair == null || backfirePercent == null) return 0f;
            float gain = obsessionGain != null ? Math.Max(0f, obsessionGain(pair.FirstSkillId)) : 0f;
            return Math.Max(0f, Math.Min(1f, backfirePercent(currentObsession + gain) * .01f));
        }

        // The setup bonus only applies when the second cast would itself pass the stop threshold.
        public static bool SetupBonusAllowed(float secondBackfireProbability, float stopProbability) => secondBackfireProbability < stopProbability;

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != null && string.Equals(ids[i].Trim(), id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
