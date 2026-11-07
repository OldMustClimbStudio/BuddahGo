using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    /// <summary>
    /// Pure rules of the confirmed 缘起 pairs: #2 frost lock target selection (horizontal radius × palm multiplier, exclusions) and
    /// #4 giant charge body impact (armed window, burst gate, once per victim per Arm, sideways scatter direction).
    /// </summary>
    public class YuanqiPairRulesTests
    {
        private static readonly Vector3 Center = new Vector3(10f, 2f, -5f);

        private static YuanqiTargetCandidate Other(Vector3 position, bool dummy = false, bool initialized = true)
            => new YuanqiTargetCandidate(position, isCaster: false, isServerInitialized: initialized, isTrainingDummy: dummy);

        // ---- #2 targeting -------------------------------------------------------------------------------------

        // #2's radius at R = 12 m for a caster at this body scale of a scale row (full body scale, palm multiplier there).
        private static float FrostRadius(float bodyScale, float bodyScaleAtFull, float palmAtFull)
            => Yuanqi_FrostLock.RadiusFor(12f, PalmSizeRule.Resolve(bodyScale, bodyScaleAtFull, palmAtFull));

        [Test]
        public void TargetingUsesAHorizontalInclusiveRadiusAndItsExclusions()
        {
            EveryCase.Check(new[]
            {
                // The radius is horizontal and inclusive.
                (name: "exactly R is inside", candidate: Other(Center + new Vector3(12f, 0f, 0f)), radius: 12f, dummies: true, eligible: true),
                (name: "just inside R", candidate: Other(Center + new Vector3(0f, 0f, -11.99f)), radius: 12f, dummies: true, eligible: true),
                (name: "just outside R", candidate: Other(Center + new Vector3(12.01f, 0f, 0f)), radius: 12f, dummies: true, eligible: false),
                (name: "diagonal 12.7 m is outside", candidate: Other(Center + new Vector3(9f, 0f, 9f)), radius: 12f, dummies: true, eligible: false),
                (name: "height is ignored", candidate: Other(Center + new Vector3(3f, 40f, 4f)), radius: 12f, dummies: true, eligible: true),
                // The caster and uninitialized racers are excluded.
                (name: "the caster is never a target", candidate: new YuanqiTargetCandidate(Center, isCaster: true, isServerInitialized: true, isTrainingDummy: false), radius: 12f, dummies: true, eligible: false),
                (name: "not server-initialized", candidate: Other(Center, initialized: false), radius: 12f, dummies: true, eligible: false),
                (name: "another racer on the caster's spot", candidate: Other(Center), radius: 12f, dummies: true, eligible: true),
                // Training dummies follow the flag.
                (name: "dummy with includeTrainingDummies", candidate: Other(Center + Vector3.right, dummy: true), radius: 12f, dummies: true, eligible: true),
                (name: "dummy without includeTrainingDummies", candidate: Other(Center + Vector3.right, dummy: true), radius: 12f, dummies: false, eligible: false),
                (name: "racers stay targets without includeTrainingDummies", candidate: Other(Center + Vector3.right), radius: 12f, dummies: false, eligible: true),
                // The radius follows the caster's palm multiplier (已定 2026-11-07): R × PalmSizeRule, shipped rows giant
                // ×2 at body ×4 and giant_anti ×0.5 at body ×0.3, linear while the body changes.
                (name: "palm ×1 without 金身: 12 m", candidate: Other(Center + new Vector3(12f, 0f, 0f)), radius: FrostRadius(1f, 4f, 2f), dummies: true, eligible: true),
                (name: "palm ×1 without 金身: 12.01 m outside", candidate: Other(Center + new Vector3(12.01f, 0f, 0f)), radius: FrostRadius(1f, 4f, 2f), dummies: true, eligible: false),
                (name: "palm ×2 at full 金身: 20 m inside 24 m", candidate: Other(Center + new Vector3(0f, 0f, 20f)), radius: FrostRadius(4f, 4f, 2f), dummies: true, eligible: true),
                (name: "palm ×2 at full 金身: 24.01 m outside", candidate: Other(Center + new Vector3(24.01f, 0f, 0f)), radius: FrostRadius(4f, 4f, 2f), dummies: true, eligible: false),
                (name: "palm ×1.5 halfway (body ×2.5): 17.9 m inside 18 m", candidate: Other(Center + new Vector3(17.9f, 0f, 0f)), radius: FrostRadius(2.5f, 4f, 2f), dummies: true, eligible: true),
                (name: "palm ×0.5 at full 金身反噬: 5.9 m inside 6 m", candidate: Other(Center + new Vector3(5.9f, 0f, 0f)), radius: FrostRadius(0.3f, 0.3f, 0.5f), dummies: true, eligible: true),
                (name: "palm ×0.5 at full 金身反噬: 7 m outside", candidate: Other(Center + new Vector3(0f, 0f, -7f)), radius: FrostRadius(0.3f, 0.3f, 0.5f), dummies: true, eligible: false),
                // A non-positive or invalid radius selects nobody.
                (name: "radius 0", candidate: Other(Center), radius: 0f, dummies: true, eligible: false),
                (name: "radius -1", candidate: Other(Center), radius: -1f, dummies: true, eligible: false),
                (name: "radius NaN", candidate: Other(Center), radius: float.NaN, dummies: true, eligible: false),
            }, c => Assert.AreEqual(c.eligible, YuanqiTargeting.IsEligible(c.candidate, Center, c.radius, c.dummies)), c => c.name);
        }

        // ---- #4 body impact -----------------------------------------------------------------------------------

        [Test]
        public void BodyImpactHitsOncePerVictimInsideTheWindowAndScattersSideways()
        {
            EveryCase.Parts(
                ("once per victim per Arm", () =>
                {
                    var rule = new BuddahBodyImpactRule();
                    rule.Arm(300f, 0.8f, now: 5f);
                    Assert.IsTrue(rule.TryClaimHit(victimKey: 7, now: 5.1f, casterBursting: true), "first contact hits");
                    Assert.IsFalse(rule.TryClaimHit(7, 5.2f, true), "same victim, same burst: no second hit");
                    Assert.IsTrue(rule.TryClaimHit(8, 5.2f, true), "another victim still hits");

                    rule.Arm(300f, 0.8f, now: 9f);
                    Assert.IsTrue(rule.TryClaimHit(7, 9.1f, true), "a new Arm forgets the earlier victims");
                }),
                ("only inside the window and while bursting", () =>
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
                }),
                ("zero magnitude or duration never arms", () =>
                {
                    var rule = new BuddahBodyImpactRule();
                    rule.Arm(0f, 0.8f, 5f);
                    Assert.IsFalse(rule.TryClaimHit(1, 5.1f, true));
                    rule.Arm(300f, 0f, 5f);
                    Assert.IsFalse(rule.TryClaimHit(1, 5f, true));
                    rule.Arm(-300f, 0.8f, 5f);
                    Assert.IsFalse(rule.IsArmed(5f));
                }),
                ("direction is horizontal from caster to victim", () =>
                {
                    Vector3 impulse = BuddahBodyImpactRule.ResolveImpulse(new Vector3(0f, 1f, 0f), new Vector3(3f, 9f, 4f), Vector3.forward, 300f);
                    Assert.AreEqual(0f, impulse.y, 1e-5f, "no vertical component");
                    Assert.AreEqual(300f, impulse.magnitude, 1e-3f);
                    Assert.AreEqual(180f, impulse.x, 1e-3f, "3/5 of 300: sideways scatter, not the caster's heading");
                    Assert.AreEqual(240f, impulse.z, 1e-3f);
                }),
                ("degenerate direction falls back to caster forward", () =>
                {
                    Vector3 stacked = BuddahBodyImpactRule.ResolveImpulse(Vector3.zero, new Vector3(0f, 5f, 0f), new Vector3(0f, 0.5f, -2f), 300f);
                    AssertVector(new Vector3(0f, 0f, -300f), stacked, "victim straight above: flat caster forward");

                    Vector3 straightUp = BuddahBodyImpactRule.ResolveImpulse(Vector3.zero, Vector3.zero, Vector3.up, 300f);
                    AssertVector(new Vector3(0f, 0f, 300f), straightUp, "no usable forward: world forward");
                }));
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, message + " (y)");
            Assert.AreEqual(expected.z, actual.z, 1e-3f, message + " (z)");
        }
    }
}
