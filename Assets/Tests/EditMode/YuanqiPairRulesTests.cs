using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    /// <summary>
    /// Pure rules of the confirmed 缘起 pairs: #2 frost lock target selection (horizontal radius, exclusions) and
    /// #4 giant charge body impact (armed window, burst gate, once per victim per Arm, sideways scatter direction).
    /// </summary>
    public class YuanqiPairRulesTests
    {
        private static readonly Vector3 Center = new Vector3(10f, 2f, -5f);

        private static YuanqiTargetCandidate Other(Vector3 position, bool dummy = false, bool initialized = true)
            => new YuanqiTargetCandidate(position, isCaster: false, isServerInitialized: initialized, isTrainingDummy: dummy);

        // ---- #2 targeting -------------------------------------------------------------------------------------

        [Test]
        public void Targeting_RadiusIsHorizontalAndInclusive()
        {
            Assert.IsTrue(YuanqiTargeting.IsEligible(Other(Center + new Vector3(12f, 0f, 0f)), Center, 12f, true), "exactly R is inside");
            Assert.IsTrue(YuanqiTargeting.IsEligible(Other(Center + new Vector3(0f, 0f, -11.99f)), Center, 12f, true));
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center + new Vector3(12.01f, 0f, 0f)), Center, 12f, true), "just outside R");
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center + new Vector3(9f, 0f, 9f)), Center, 12f, true), "diagonal 12.7 m is outside");
            Assert.IsTrue(YuanqiTargeting.IsEligible(Other(Center + new Vector3(3f, 40f, 4f)), Center, 12f, true), "height is ignored");
        }

        [Test]
        public void Targeting_ExcludesCasterAndUninitialized()
        {
            var caster = new YuanqiTargetCandidate(Center, isCaster: true, isServerInitialized: true, isTrainingDummy: false);
            Assert.IsFalse(YuanqiTargeting.IsEligible(caster, Center, 12f, true), "the caster is never a target");
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center, initialized: false), Center, 12f, true), "not server-initialized");
            Assert.IsTrue(YuanqiTargeting.IsEligible(Other(Center), Center, 12f, true), "another racer on the caster's spot");
        }

        [Test]
        public void Targeting_TrainingDummiesFollowTheFlag()
        {
            YuanqiTargetCandidate dummy = Other(Center + Vector3.right, dummy: true);
            Assert.IsTrue(YuanqiTargeting.IsEligible(dummy, Center, 12f, includeTrainingDummies: true));
            Assert.IsFalse(YuanqiTargeting.IsEligible(dummy, Center, 12f, includeTrainingDummies: false));
            Assert.IsTrue(YuanqiTargeting.IsEligible(Other(Center + Vector3.right), Center, 12f, includeTrainingDummies: false), "racers stay targets");
        }

        [Test]
        public void Targeting_NonPositiveOrInvalidRadiusSelectsNobody()
        {
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center), Center, 0f, true));
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center), Center, -1f, true));
            Assert.IsFalse(YuanqiTargeting.IsEligible(Other(Center), Center, float.NaN, true));
        }

        // ---- #4 body impact -----------------------------------------------------------------------------------

        [Test]
        public void BodyImpact_OncePerVictimPerArm()
        {
            var rule = new BuddahBodyImpactRule();
            rule.Arm(300f, 0.8f, now: 5f);
            Assert.IsTrue(rule.TryClaimHit(victimKey: 7, now: 5.1f, casterBursting: true), "first contact hits");
            Assert.IsFalse(rule.TryClaimHit(7, 5.2f, true), "same victim, same burst: no second hit");
            Assert.IsTrue(rule.TryClaimHit(8, 5.2f, true), "another victim still hits");

            rule.Arm(300f, 0.8f, now: 9f);
            Assert.IsTrue(rule.TryClaimHit(7, 9.1f, true), "a new Arm forgets the earlier victims");
        }

        [Test]
        public void BodyImpact_OnlyInsideTheWindowAndWhileBursting()
        {
            var rule = new BuddahBodyImpactRule();
            Assert.IsFalse(rule.IsArmed(0f), "never armed");
            Assert.IsFalse(rule.TryClaimHit(1, 0f, true));

            rule.Arm(300f, 0.8f, now: 5f);
            Assert.IsTrue(rule.IsArmed(5f));
            Assert.IsFalse(rule.TryClaimHit(1, 5.1f, casterBursting: false), "the motor burst is over (or the caster is frozen)");
            Assert.IsTrue(rule.TryClaimHit(1, 5.2f, casterBursting: true), "a refused contact does not use up the victim");
            Assert.IsFalse(rule.TryClaimHit(2, 5.9f, true), "the window [now, now + seconds) has closed");
            Assert.IsFalse(rule.IsArmed(5.9f));
        }

        [Test]
        public void BodyImpact_ZeroMagnitudeOrDurationNeverArms()
        {
            var rule = new BuddahBodyImpactRule();
            rule.Arm(0f, 0.8f, 5f);
            Assert.IsFalse(rule.TryClaimHit(1, 5.1f, true));
            rule.Arm(300f, 0f, 5f);
            Assert.IsFalse(rule.TryClaimHit(1, 5f, true));
            rule.Arm(-300f, 0.8f, 5f);
            Assert.IsFalse(rule.IsArmed(5f));
        }

        [Test]
        public void BodyImpact_DirectionIsHorizontalFromCasterToVictim()
        {
            Vector3 impulse = BuddahBodyImpactRule.ResolveImpulse(new Vector3(0f, 1f, 0f), new Vector3(3f, 9f, 4f), Vector3.forward, 300f);
            Assert.AreEqual(0f, impulse.y, 1e-5f, "no vertical component");
            Assert.AreEqual(300f, impulse.magnitude, 1e-3f);
            Assert.AreEqual(180f, impulse.x, 1e-3f, "3/5 of 300: sideways scatter, not the caster's heading");
            Assert.AreEqual(240f, impulse.z, 1e-3f);
        }

        [Test]
        public void BodyImpact_DegenerateDirectionFallsBackToCasterForward()
        {
            Vector3 stacked = BuddahBodyImpactRule.ResolveImpulse(Vector3.zero, new Vector3(0f, 5f, 0f), new Vector3(0f, 0.5f, -2f), 300f);
            AssertVector(new Vector3(0f, 0f, -300f), stacked, "victim straight above: flat caster forward");

            Vector3 straightUp = BuddahBodyImpactRule.ResolveImpulse(Vector3.zero, Vector3.zero, Vector3.up, 300f);
            AssertVector(new Vector3(0f, 0f, 300f), straightUp, "no usable forward: world forward");
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, message + " (y)");
            Assert.AreEqual(expected.z, actual.z, 1e-3f, message + " (z)");
        }
    }
}
