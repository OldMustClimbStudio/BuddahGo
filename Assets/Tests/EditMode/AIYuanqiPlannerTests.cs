using System;
using System.Collections.Generic;
using System.Linq;
using BuddahGo.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// A7 缘起 decisions (建议（实现采用）): which pairs a loadout can trigger, which skill goes first, when the second
// skill is a follow-up and when the first is a setup. Pairs are the shipped rows of Skill/Yuanqi (#1, #2, #4).
public class AIYuanqiPlannerTests
{
    private const float Delta = 1f / 60f, Confirm = 1f, Late = .6f;
    private static readonly string[] Keeper = { "slowtrap", "acceleration", "giant" };
    private static readonly string[] Hunter = { "push_projectile_hands", "acceleration", "reverseturn" };
    private static readonly string[] Brawler = { "giant", "push_projectile_hands", "slowtrap" };
    private static readonly Dictionary<string, float> Durations = new Dictionary<string, float>
        { { "acceleration", 3f }, { "slowtrap", 6f }, { "giant", 6f }, { "push_projectile_hands", 10f }, { "reverseturn", 10f }, { "blackcurtain", 10f } };
    private static float Duration(string id) => Durations.TryGetValue(id, out float d) ? d : 0f;
    private readonly List<YuanqiAction> _pairs = new List<YuanqiAction>();

    private YuanqiAction Pair<T>(string id, string a, string b, bool consumeA, bool consumeB, YuanqiMode mode) where T : YuanqiAction
    {
        var pair = ScriptableObject.CreateInstance<T>();
        pair.yuanqiId = id; pair.skillA = a; pair.skillB = b; pair.consumeA = consumeA; pair.consumeB = consumeB; pair.mode = mode;
        _pairs.Add(pair);
        return pair;
    }
    [SetUp] public void SetUp()
    {
        Pair<Yuanqi_GiantPalm>("giant_palm", "giant", "push_projectile_hands", false, false, YuanqiMode.Sustained);
        Pair<Yuanqi_FrostLock>("frost_lock", "acceleration", "slowtrap", true, true, YuanqiMode.Instant);
        Pair<Yuanqi_GiantCharge>("giant_charge", "acceleration", "giant", true, false, YuanqiMode.Instant);
    }
    [TearDown] public void TearDown() { foreach (var pair in _pairs) Object.DestroyImmediate(pair); _pairs.Clear(); }
    private AIYuanqiPlanner Build(string[] loadout) => AIYuanqiPlanner.Build(loadout, _pairs, Duration);
    private static SkillStateEntry Active(string skillId, int slot, uint untilTick, bool activated = true, bool ended = false)
        => new SkillStateEntry { EntryId = 1, SkillId = skillId, Slot = slot, StartTick = 0, UntilTick = untilTick, IsActivated = activated, IsEnded = ended };

    [Test] public void KeeperGetsFrostLockAndGiantChargeMappedToSlots()
    {
        var planner = Build(Keeper);
        Assert.That(planner.Pairs.Select(p => p.YuanqiId), Is.EquivalentTo(new[] { "frost_lock", "giant_charge" }));
        var frost = planner.Pairs.Single(p => p.YuanqiId == "frost_lock");
        Assert.That(frost.SlotA, Is.EqualTo(1)); Assert.That(frost.SlotB, Is.EqualTo(0));
        Assert.That(frost.ConsumeA, Is.True); Assert.That(frost.ConsumeB, Is.True); Assert.That(frost.Sustained, Is.False);
        var charge = planner.Pairs.Single(p => p.YuanqiId == "giant_charge");
        Assert.That(charge.SlotA, Is.EqualTo(1)); Assert.That(charge.SlotB, Is.EqualTo(2));
        Assert.That(charge.ConsumeA, Is.True); Assert.That(charge.ConsumeB, Is.False);
    }
    [Test] public void HunterHasNoPairAndBrawlerGetsGiantPalm()
    {
        Assert.That(Build(Hunter).Pairs, Is.Empty);
        var brawler = Build(Brawler);
        Assert.That(brawler.Pairs.Select(p => p.YuanqiId), Is.EqualTo(new[] { "giant_palm" }));
        Assert.That(brawler.Pairs[0].Sustained, Is.True);
        Assert.That(brawler.Pairs[0].SlotA, Is.EqualTo(0)); Assert.That(brawler.Pairs[0].SlotB, Is.EqualTo(1));
    }
    [Test] public void ShippedDatabaseGivesTheSamePairsForTheKeeper()
    {
        var database = AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Skills_Assets/Skill Database.asset");
        Assert.That(database, Is.Not.Null);
        var planner = AIYuanqiPlanner.Build(Keeper, database.GetYuanqis(), Duration);
        Assert.That(planner.Pairs.Select(p => p.YuanqiId), Is.EquivalentTo(new[] { "frost_lock", "giant_charge" }));
        Assert.That(AIYuanqiPlanner.Build(Hunter, database.GetYuanqis(), Duration).Pairs, Is.Empty);
    }
    [Test] public void ConsumedSkillGoesFirstWhenOnlyOneIsConsumed()
    {
        var charge = Build(Keeper).Pairs.Single(p => p.YuanqiId == "giant_charge");
        Assert.That(charge.FirstSlot, Is.EqualTo(1), "神足通 is consumed, so it is cast first and enjoyed before 法相金身 lands.");
        Assert.That(charge.SecondSlot, Is.EqualTo(2));
        Assert.That(charge.FirstSkillId, Is.EqualTo("acceleration")); Assert.That(charge.SecondSkillId, Is.EqualTo("giant"));
    }
    [Test] public void LongerEffectGoesFirstWhenBothOrNeitherAreConsumed()
    {
        var frost = Build(Keeper).Pairs.Single(p => p.YuanqiId == "frost_lock");
        Assert.That(frost.FirstSlot, Is.EqualTo(0), "Both consumed: 缓流 (6 s) first so 神足通's confirm lands inside its window.");
        Assert.That(frost.SecondSlot, Is.EqualTo(1));
        var palm = Build(Brawler).Pairs[0];
        Assert.That(palm.FirstSlot, Is.EqualTo(1), "Neither consumed: the 10 s hands buff first gives the longest overlap.");
        Assert.That(palm.SecondSlot, Is.EqualTo(0));
    }
    [Test] public void EqualEffectsPutSkillAFirst()
    {
        var planner = AIYuanqiPlanner.Build(Brawler, _pairs, id => 6f);
        var palm = planner.Pairs.Single(p => p.YuanqiId == "giant_palm");
        Assert.That(palm.FirstSlot, Is.EqualTo(0)); Assert.That(palm.SecondSlot, Is.EqualTo(1));
    }
    [Test] public void ConsumedFirstSkillIsFollowedUpLate()
    {
        var planner = Build(Keeper);
        // 神足通 (slot 1) active, 3 s left: too early, let it run first.
        var board = new[] { Active("acceleration", 1, 100 + 180) };
        Assert.That(planner.TryGetFollowUp(0, board, 100, Delta, Confirm, Late, out _), Is.False);
        // 1.5 s left: inside the late window (confirm 1 s + 0.6 s), valid for 0.5 s.
        Assert.That(planner.TryGetFollowUp(0, board, 190, Delta, Confirm, Late, out var f), Is.True);
        Assert.That(f.Pair.YuanqiId, Is.EqualTo("frost_lock")); Assert.That(f.Slot, Is.EqualTo(0));
        Assert.That(f.ValidSeconds, Is.EqualTo(.5f).Within(.001f));
        // 金身 (slot 2) is the other half of #4 and follows the same late rule.
        Assert.That(planner.TryGetFollowUp(2, board, 100, Delta, Confirm, Late, out _), Is.False);
        Assert.That(planner.TryGetFollowUp(2, board, 190, Delta, Confirm, Late, out f), Is.True);
        Assert.That(f.Pair.YuanqiId, Is.EqualTo("giant_charge"));
        // Exactly the confirm time left: the second cast could not land inside the window any more.
        Assert.That(planner.TryGetFollowUp(0, board, 220, Delta, Confirm, Late, out _), Is.False);
    }
    [Test] public void UnconsumedFirstSkillIsFollowedUpImmediately()
    {
        var brawler = Build(Brawler);
        var board = new[] { Active("giant", 0, 100 + 354) };
        Assert.That(brawler.TryGetFollowUp(1, board, 100, Delta, Confirm, Late, out var f), Is.True, "#1 consumes nothing: overlap as early as possible.");
        Assert.That(f.ValidSeconds, Is.EqualTo(5.9f - Confirm).Within(.001f));
        Assert.That(brawler.TryGetFollowUp(2, board, 100, Delta, Confirm, Late, out _), Is.False, "缓流 is not in #1.");
        // #4 with 金身 already active (not consumed): 神足通 is a follow-up at once.
        var keeper = Build(Keeper);
        Assert.That(keeper.TryGetFollowUp(1, board, 100, Delta, Confirm, Late, out f), Is.True);
        Assert.That(f.Pair.YuanqiId, Is.EqualTo("giant_charge"));
    }
    [Test] public void FollowUpIgnoresPendingEndedAndForeignEntries()
    {
        var planner = Build(Brawler);
        Assert.That(planner.TryGetFollowUp(1, new[] { Active("giant", 0, 400, activated: false) }, 100, Delta, Confirm, Late, out _), Is.False);
        Assert.That(planner.TryGetFollowUp(1, new[] { Active("giant", 0, 400, ended: true) }, 100, Delta, Confirm, Late, out _), Is.False);
        Assert.That(planner.TryGetFollowUp(1, new[] { Active("slowtrap", 2, 400) }, 100, Delta, Confirm, Late, out _), Is.False);
        Assert.That(planner.TryGetFollowUp(1, Array.Empty<SkillStateEntry>(), 100, Delta, Confirm, Late, out _), Is.False);
        Assert.That(Build(Hunter).TryGetFollowUp(0, new[] { Active("acceleration", 1, 400) }, 100, Delta, Confirm, Late, out _), Is.False);
    }
    [Test] public void SetupNeedsTheSecondSlotReadyBeforeTheFirstEffectEnds()
    {
        var planner = Build(Keeper);
        // 神足通 first for #4: 金身 ready in 2.5 s < 3 s effect → setup.
        Assert.That(planner.IsSetup(1, slot => slot == 2 ? 2.5f : 0f, Duration, Confirm, out var pair), Is.True);
        Assert.That(pair.YuanqiId, Is.EqualTo("giant_charge"));
        // 金身 ready only after 神足通 ended → the second cast cannot land in time.
        Assert.That(planner.IsSetup(1, slot => slot == 2 ? 3.5f : 0f, Duration, Confirm, out _), Is.False);
        // 缓流 first for #2: 神足通 ready at once → setup; ready in 7 s (> 6 s window) → not.
        Assert.That(planner.IsSetup(0, slot => 0f, Duration, Confirm, out pair), Is.True);
        Assert.That(pair.YuanqiId, Is.EqualTo("frost_lock"));
        Assert.That(planner.IsSetup(0, slot => slot == 1 ? 7f : 0f, Duration, Confirm, out _), Is.False);
        // 金身 is never the first skill of a keeper pair.
        Assert.That(planner.IsSetup(2, slot => 0f, Duration, Confirm, out _), Is.False);
        Assert.That(Build(Hunter).IsSetup(1, slot => 0f, Duration, Confirm, out _), Is.False);
    }
    [Test] public void SetupBonusIsRefusedWhenTheSecondCastWouldStopOnRisk()
    {
        var charge = Build(Keeper).Pairs.Single(p => p.YuanqiId == "giant_charge");
        // Second cast risk: the first skill's gain (no surcharge) is added to the current obsession before the curve.
        float p2 = AIYuanqiPlanner.SecondBackfireProbability(charge, 300f, id => id == "acceleration" ? 300f : 200f, obsession => obsession >= 600f ? 66f : 16f);
        Assert.That(p2, Is.EqualTo(.66f).Within(.0001f));
        Assert.That(AIYuanqiPlanner.SetupBonusAllowed(p2, .45f), Is.False, "Hard stops at 0.45.");
        Assert.That(AIYuanqiPlanner.SetupBonusAllowed(p2, .7f), Is.True, "Normal stops at 0.7.");
        Assert.That(AIYuanqiPlanner.SetupBonusAllowed(.7f, .7f), Is.False, "At the threshold the second cast would be refused.");
    }
}
