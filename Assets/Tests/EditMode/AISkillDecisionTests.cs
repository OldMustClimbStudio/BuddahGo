using System;
using System.Linq;
using BuddahGo.AI;
using BuddahGo.Match;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class AISkillDecisionTests
{
    [Serializable] private struct RecordedStats { public NewBuddah.PredictionV2.Core.BuddahPredictedMotorComputedStats Stats; }
    [Test] public void EvidenceIncludesActualMotorEffectState()
    {
        var row = new RecordedStats { Stats = new NewBuddah.PredictionV2.Core.BuddahPredictedMotorComputedStats
            { IsRooted = true, FinalSteeringSign = -1f, ScaleMultiplier = .3f } };
        var copy = JsonUtility.FromJson<RecordedStats>(JsonUtility.ToJson(row));
        Assert.That(copy.Stats.IsRooted, Is.True);
        Assert.That(copy.Stats.FinalSteeringSign, Is.EqualTo(-1f));
        Assert.That(copy.Stats.ScaleMultiplier, Is.EqualTo(.3f));
    }
    private AISkillCatalog _catalog;
    private AISkillSnapshot _frame;
    [SetUp] public void SetUp()
    {
        _catalog = JsonUtility.FromJson<AISkillCatalog>(Resources.Load<TextAsset>("AI/SkillPersonalities").text);
        _frame = new AISkillSnapshot { Count = 2, Tick = 100, TickDelta = 1f / 60f };
        _frame.Racers[0] = Racer(10000, 100f);
        _frame.Racers[1] = Racer(0, 150f);
    }
    private static AISkillRacerSnapshot Racer(int id, float progress) => new AISkillRacerSnapshot
    {
        RacerId = id, Available = true, Progress = progress, TrackDistance = progress,
        Position = Vector3.forward * progress, Forward = Vector3.forward, Velocity = Vector3.forward * 60f,
        Speed = 60f, AlongSpeed = 60f, PreviousSpeed = 60f, Scale = 1f,
        StraightFraction = .8f, StraightSeconds = 8f, CornerFraction = .5f, CornerSeconds = 4f
    };
    private AISkillOpportunity Evaluate(AISkillKind kind, AITargetPreference preference = AITargetPreference.Ahead)
        => AISkillDecision.Evaluate(kind, _frame, 0, _catalog.Tuning, preference, 0f, 180f);

    [Test] public void ProductCatalogUsesSelectablePoolAndFiveDistinctCompleteLoadouts()
    {
        Assert.DoesNotThrow(() => _catalog.Validate(id => AISkillCatalog.SkillIds.Contains(id)));
        Assert.That(AISkillCatalog.Current.Personalities.Length, Is.EqualTo(5), "Runtime repository validation must also succeed.");
        Assert.That(_catalog.Personalities.SelectMany(p => p.Loadout).Distinct().Count(), Is.EqualTo(6));
        foreach (var p in _catalog.Personalities)
            Assert.That(p.Loadout.Distinct().Count(), Is.EqualTo(3));
    }
    [Test] public void CatalogRejectsUnknownDuplicateAndEmptyLoadouts()
    {
        _catalog.Personalities[0].Loadout[0] = "not-selectable";
        Assert.Throws<ArgumentException>(() => _catalog.Validate(id => AISkillCatalog.SkillIds.Contains(id)));
        _catalog.Personalities[0].Loadout[0] = _catalog.Personalities[0].Loadout[1];
        Assert.Throws<ArgumentException>(() => _catalog.Validate(id => AISkillCatalog.SkillIds.Contains(id)));
        _catalog.Personalities[0].Loadout[0] = "";
        Assert.Throws<ArgumentException>(() => _catalog.Validate(id => AISkillCatalog.SkillIds.Contains(id)));
    }
    [Test] public void AccelerationNeedsUsableStraightAndNoExistingBoost()
    {
        Assert.That(Evaluate(AISkillKind.Acceleration).Score, Is.GreaterThan(0f));
        _frame.Racers[0].Accelerating = true;
        Assert.That(Evaluate(AISkillKind.Acceleration).Score, Is.Zero);
        _frame.Racers[0].Accelerating = false; _frame.Racers[0].StraightSeconds = 2.9f;
        Assert.That(Evaluate(AISkillKind.Acceleration).Score, Is.Zero);
    }
    [Test] public void TrapNeedsApproachingSameLaneOpponentBehind()
    {
        Assert.That(Evaluate(AISkillKind.SlowTrap).Score, Is.Zero);
        _frame.Racers[1] = Racer(0, 60f); _frame.Racers[1].AlongSpeed = 70f;
        var opportunity = Evaluate(AISkillKind.SlowTrap);
        Assert.That(opportunity.Score, Is.GreaterThan(0)); Assert.That(opportunity.ValidSeconds, Is.EqualTo(4f));
        _frame.Racers[1].Lateral = 30f;
        Assert.That(Evaluate(AISkillKind.SlowTrap).Score, Is.Zero);
        _frame.Racers[1].Lateral = 0; _frame.Racers[1].AlongSpeed = 50;
        Assert.That(Evaluate(AISkillKind.SlowTrap).Score, Is.Zero);
    }
    [Test] public void GiantMeasuresRealNearbyPackAndNotDistantRank()
    {
        Assert.That(Evaluate(AISkillKind.Giant).Score, Is.Zero);
        _frame.Racers[1] = Racer(0, 115f);
        Assert.That(Evaluate(AISkillKind.Giant).Score, Is.EqualTo(.5f));
        _frame.Count = 3; _frame.Racers[2] = Racer(10001, 85f);
        Assert.That(Evaluate(AISkillKind.Giant).Score, Is.EqualTo(1f));
        _frame.Racers[2].Available = false;
        Assert.That(Evaluate(AISkillKind.Giant).Score, Is.EqualTo(.5f));
    }
    [Test] public void HandsRejectRearUnreachableAndProtectedTargets()
    {
        Assert.That(Evaluate(AISkillKind.Hands).Score, Is.GreaterThan(0));
        _frame.Racers[1].PushProtected = true;
        Assert.That(Evaluate(AISkillKind.Hands).Score, Is.Zero);
        _frame.Racers[1] = Racer(0, 90);
        Assert.That(Evaluate(AISkillKind.Hands).Score, Is.Zero);
        _frame.Racers[1] = Racer(0, 401);
        Assert.That(Evaluate(AISkillKind.Hands).Score, Is.Zero);
    }
    [TestCase(AISkillKind.Reverse)] [TestCase(AISkillKind.Curtain)]
    public void VisibleGlobalEffectSuppressesDuplicateOpportunity(AISkillKind kind)
    {
        Assert.That(Evaluate(kind).Score, Is.GreaterThan(0));
        _frame.ReverseSeconds = 3; _frame.CurtainSeconds = 3;
        Assert.That(Evaluate(kind).Score, Is.Zero);
    }
    [TestCase(AISkillKind.Hands)] [TestCase(AISkillKind.Reverse)] [TestCase(AISkillKind.Curtain)]
    public void TargetIdentityDoesNotChangeOpportunity(AISkillKind kind)
    {
        var human = Evaluate(kind); _frame.Racers[1].RacerId = 10004;
        var ai = Evaluate(kind);
        Assert.That(ai.Score, Is.EqualTo(human.Score)); Assert.That(ai.ValidSeconds, Is.EqualTo(human.ValidSeconds));
        Assert.That(ai.TargetId, Is.EqualTo(10004));
    }
    [TestCase(AISkillKind.Acceleration)] [TestCase(AISkillKind.Hands)] [TestCase(AISkillKind.Giant)]
    public void RootAndInactiveRaceGateAllOpportunities(AISkillKind kind)
    {
        _frame.Racers[0].Rooted = true; Assert.That(Evaluate(kind).Score, Is.Zero);
        _frame.Racers[0].Rooted = false; _frame.Racers[0].Available = false; Assert.That(Evaluate(kind).Score, Is.Zero);
    }
    [Test] public void ShortOpportunityCannotOutliveReactionAndCombo()
    {
        var p = _catalog.Personalities[1]; var d = _catalog.ForDifficulty(SoloDifficulty.Easy);
        float value = AISkillDecision.Utility(AISkillKind.Hands, new AISkillOpportunity(1, 2), AISkillSituation.Chasing,
            p, d, _catalog.Tuning, 0, 1.9f, .6f);
        Assert.That(float.IsNegativeInfinity(value), Is.True);
    }
    [Test] public void EasyIgnoresRiskButHardStopsBeforeDangerousCast()
    {
        var p = _catalog.Personalities[1]; var opportunity = new AISkillOpportunity(1, 6);
        float easyLow = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p,
            _catalog.ForDifficulty(SoloDifficulty.Easy), _catalog.Tuning, 0, 2, 0);
        float easyHigh = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p,
            _catalog.ForDifficulty(SoloDifficulty.Easy), _catalog.Tuning, .95f, 2, 0);
        Assert.That(easyHigh, Is.EqualTo(easyLow));
        Assert.That(float.IsNegativeInfinity(AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p,
            _catalog.ForDifficulty(SoloDifficulty.Hard), _catalog.Tuning, .45f, 2, 0)), Is.True);
        // A7: the backfire penalty is the same for every skill, so one stop threshold per difficulty applies to all of them.
        Assert.That(float.IsNegativeInfinity(AISkillDecision.Utility(AISkillKind.SlowTrap, opportunity, AISkillSituation.Pursued,
            _catalog.Personalities[0], _catalog.ForDifficulty(SoloDifficulty.Hard), _catalog.Tuning, .5f, 2, 0)), Is.True);
        Assert.That(float.IsNegativeInfinity(AISkillDecision.Utility(AISkillKind.SlowTrap, opportunity, AISkillSituation.Pursued,
            _catalog.Personalities[0], _catalog.ForDifficulty(SoloDifficulty.Normal), _catalog.Tuning, .5f, 2, 0)), Is.False);
        Assert.That(_catalog.ForDifficulty(SoloDifficulty.Hard).StopProbability, Is.LessThan(_catalog.ForDifficulty(SoloDifficulty.Normal).StopProbability));
    }
    [Test] public void RiskTermIsUnifiedBackfireProbabilityTimesCost()
    {
        var p = _catalog.Personalities[1]; var d = _catalog.ForDifficulty(SoloDifficulty.Hard); var opportunity = new AISkillOpportunity(1, 6);
        _catalog.Tuning.BackfireCost = 2f;
        float safe = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p, d, _catalog.Tuning, 0f, 2, 0);
        float risky = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p, d, _catalog.Tuning, .2f, 2, 0);
        Assert.That(safe - risky, Is.EqualTo(d.RiskWeight * .2f * 2f).Within(.0001f));
        float trap = AISkillDecision.Utility(AISkillKind.SlowTrap, opportunity, AISkillSituation.Chasing, _catalog.Personalities[0], d, _catalog.Tuning, .2f, 2, 0);
        float trapSafe = AISkillDecision.Utility(AISkillKind.SlowTrap, opportunity, AISkillSituation.Chasing, _catalog.Personalities[0], d, _catalog.Tuning, 0f, 2, 0);
        Assert.That(trapSafe - trap, Is.EqualTo(safe - risky).Within(.0001f), "Every skill pays the same risk.");
    }
    [Test] public void LateYuanqiFollowUpPassesTheValidityGateAsDecideBuildsIt()
    {
        // A7 缘起 follow-up (建议（实现采用）): the planner already subtracts the confirm from the window, and Utility
        // subtracts required = confirm + 2 ticks itself, so Decide must hand it the time the first skill is still
        // active at execution (RemainingSeconds). #2 frost_lock, 神足通 active with 1.5 s left (late window), confirm 1 s.
        const float delta = 1f / 60f, confirm = 1f, age = .1f, required = confirm + delta * 2f;
        var frost = ScriptableObject.CreateInstance<Yuanqi_FrostLock>();
        try
        {
            frost.yuanqiId = "frost_lock"; frost.skillA = "acceleration"; frost.skillB = "slowtrap"; frost.consumeA = frost.consumeB = true; frost.mode = YuanqiMode.Instant;
            var planner = AIYuanqiPlanner.Build(new[] { "slowtrap", "acceleration", "giant" }, new[] { frost }, id => id == "slowtrap" ? 6f : 3f);
            var board = new[] { new SkillStateEntry { EntryId = 1, SkillId = "acceleration", Slot = 1, StartTick = 0, UntilTick = 190 + 90, IsActivated = true, IsEnded = false } };
            Assert.That(planner.TryGetFollowUp(0, board, 190, delta, confirm, .6f, out var followUp), Is.True);
            Assert.That(followUp.ValidSeconds, Is.EqualTo(.5f).Within(.001f)); Assert.That(followUp.RemainingSeconds, Is.EqualTo(1.5f).Within(.001f));
            var p = _catalog.Personalities[0]; var d = _catalog.ForDifficulty(SoloDifficulty.Normal);
            var candidate = new AISkillOpportunity(Mathf.Max(0f, _catalog.Tuning.YuanqiFollowUpScore), followUp.RemainingSeconds + age);
            float utility = AISkillDecision.Utility(AISkillKind.SlowTrap, candidate, AISkillSituation.Pursued, p, d, _catalog.Tuning, 0f, required, age);
            Assert.That(float.IsNegativeInfinity(utility), Is.False, "a late follow-up must be castable, not rejected on validity");
            Assert.That(utility, Is.GreaterThan(0f));
            // The request deadline alone (remaining − confirm) would subtract the confirm twice and never pass the gate.
            var deadlineOnly = new AISkillOpportunity(candidate.Score, followUp.ValidSeconds + age);
            Assert.That(float.IsNegativeInfinity(AISkillDecision.Utility(AISkillKind.SlowTrap, deadlineOnly, AISkillSituation.Pursued, p, d, _catalog.Tuning, 0f, required, age)), Is.True);
        }
        finally { Object.DestroyImmediate(frost); }
    }
    [Test] public void YuanqiValueMultiplierScalesTheValueButNotTheRisk()
    {
        var p = _catalog.Personalities[1]; var d = _catalog.ForDifficulty(SoloDifficulty.Hard); var opportunity = new AISkillOpportunity(1, 6);
        float plain = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p, d, _catalog.Tuning, .2f, 2, 0);
        float boosted = AISkillDecision.Utility(AISkillKind.Acceleration, opportunity, AISkillSituation.Chasing, p, d, _catalog.Tuning, .2f, 2, 0, 1f, 1.5f);
        float risk = d.RiskWeight * .2f * _catalog.Tuning.BackfireCost;
        Assert.That(boosted, Is.EqualTo((plain + risk) * 1.5f - risk).Within(.0001f));
    }
    private static float ClassifyImpaired(NewBuddah.PredictionV2.Core.BuddahPredictedModifierState m, uint tick, out bool accelerating, out bool slowed)
    {
        AISkillWorld.Classify(in m, tick, 1f / 60f, out accelerating, out slowed, out float impaired);
        return impaired;
    }
    [Test] public void ClassifyTreatsTheBackfireSpeedPenaltyAsSlowedAndImpaired()
    {
        var m = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = 160, MaxSpeedPenaltyMultiplier = .85f };
        float impaired = ClassifyImpaired(m, 100, out bool accelerating, out bool slowed);
        Assert.That(slowed, Is.True); Assert.That(accelerating, Is.False); Assert.That(impaired, Is.EqualTo(1f).Within(.0001f));
        Assert.That(ClassifyImpaired(m, 160, out _, out slowed), Is.Zero); Assert.That(slowed, Is.False);
    }
    [Test] public void ClassifyTreatsTheBurstAsAccelerating()
    {
        var m = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { BurstUntilTick = 148, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f };
        Assert.That(ClassifyImpaired(m, 100, out bool accelerating, out bool slowed), Is.Zero);
        Assert.That(accelerating, Is.True); Assert.That(slowed, Is.False);
        m.BurstUntilTick = 100; ClassifyImpaired(m, 100, out accelerating, out _); Assert.That(accelerating, Is.False);
        var boost = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { AccelUntilTick = 280, AccelExtraForwardForce = 50f, AccelExtraMaxSpeed = 40f };
        ClassifyImpaired(boost, 100, out accelerating, out slowed); Assert.That(accelerating, Is.True); Assert.That(slowed, Is.False);
    }
    [Test] public void ClassifyKeepsRootFreezeInvertAndSlowButNotShrinking()
    {
        var root = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { RootUntilTick = 130, FreezeUntilTick = 190, InvertTurnUntilTick = 160 };
        Assert.That(ClassifyImpaired(root, 100, out _, out _), Is.EqualTo(1.5f).Within(.0001f));
        var slow = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { AccelUntilTick = 220, AccelExtraForwardForce = -20f };
        Assert.That(ClassifyImpaired(slow, 100, out bool accelerating, out bool slowed), Is.EqualTo(2f).Within(.0001f));
        Assert.That(slowed, Is.True); Assert.That(accelerating, Is.False);
        // 缩小 has no source after the unified backfire: a small scale is not an impairment any more.
        var shrunk = new NewBuddah.PredictionV2.Core.BuddahPredictedModifierState { ScaleUntilTick = 400, ScaleMultiplier = .3f };
        Assert.That(ClassifyImpaired(shrunk, 100, out _, out slowed), Is.Zero); Assert.That(slowed, Is.False);
        var racer = Racer(0, 100f); racer.Scale = .3f;
        Assert.That(racer.IsImpaired(.2f), Is.False);
        racer.Slowed = true; Assert.That(racer.IsImpaired(.2f), Is.True);
    }
    [Test] public void OpportunistOnlyGetsBonusWhileStateWillStillExistAtExecution()
    {
        var p = _catalog.Personalities[4]; var d = _catalog.ForDifficulty(SoloDifficulty.Easy);
        float ordinary = AISkillDecision.Utility(AISkillKind.Hands, new AISkillOpportunity(1, 6), AISkillSituation.Chasing, p, d, _catalog.Tuning, 0, 2, .4f);
        float shortState = AISkillDecision.Utility(AISkillKind.Hands, new AISkillOpportunity(1, 6, 0, true, 1), AISkillSituation.Chasing, p, d, _catalog.Tuning, 0, 2, .4f);
        float longState = AISkillDecision.Utility(AISkillKind.Hands, new AISkillOpportunity(1, 6, 0, true, 4), AISkillSituation.Chasing, p, d, _catalog.Tuning, 0, 2, .4f);
        Assert.That(shortState, Is.EqualTo(ordinary)); Assert.That(longState, Is.EqualTo(ordinary * 1.6f).Within(.0001f));
    }
    [Test] public void ExponentialSamplingHasSameProbabilityAcrossDecisionRates()
    {
        float one = AISkillDecision.CastProbability(.8f, .2f, 1f, 1.5f);
        float quarter = AISkillDecision.CastProbability(.8f, .2f, .25f, 1.5f);
        Assert.That(1f - Mathf.Pow(1f - quarter, 4), Is.EqualTo(one).Within(.00001));
        Assert.That(AISkillDecision.CastProbability(.1f, .2f, 1, 1), Is.Zero);
    }
    [Test] public void PackAndPursuitDependOnProgressAndClosingSpeed()
    {
        Assert.That(AISkillDecision.Situation(_frame, 0, _catalog.Tuning), Is.EqualTo(AISkillSituation.Chasing));
        _frame.Racers[1] = Racer(0, 60); _frame.Racers[1].AlongSpeed = 70;
        Assert.That(AISkillDecision.Situation(_frame, 0, _catalog.Tuning), Is.EqualTo(AISkillSituation.Pursued));
        _frame.Count = 3; _frame.Racers[2] = Racer(10001, 110);
        Assert.That(AISkillDecision.Situation(_frame, 0, _catalog.Tuning), Is.EqualTo(AISkillSituation.Pack));
    }
    [Test] public void ImpairedDriverProfileDoesNotMutateAssetOrPerRacerBase()
    {
        var body = new GameObject("profile-test"); var driver = body.AddComponent<AIRacerDriver>();
        var profile = Object.Instantiate(AIDifficultyProfiles.Load(SoloDifficulty.Normal));
        try
        {
            driver.AdoptProfile(profile);
            string before = JsonUtility.ToJson(profile);
            driver.ConfigureSkillPerception(_catalog.ForDifficulty(SoloDifficulty.Normal), body.AddComponent<SkillPerceptionState>());
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(body); }
    }
}
