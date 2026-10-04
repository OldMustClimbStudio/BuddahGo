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
        Assert.That(float.IsNegativeInfinity(AISkillDecision.Utility(AISkillKind.SlowTrap, opportunity, AISkillSituation.Pursued,
            _catalog.Personalities[0], _catalog.ForDifficulty(SoloDifficulty.Hard), _catalog.Tuning, .5f, 2, 0)), Is.False);
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
