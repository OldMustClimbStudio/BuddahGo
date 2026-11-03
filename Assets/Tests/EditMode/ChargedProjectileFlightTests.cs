using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/push-projectile-hands.md "飞行速度": the charged palm flies the speed shape tuned on the ink palm, its peak
    // speed comes from the Push sheet, and the range is peak x flight seconds x the shape's area.
    public class ChargedProjectileFlightTests
    {
        const string SkillAsset = "Assets/Skills_Assets/Skills/Skill_PushProjectileHands.asset";

        [Test]
        public void DistanceFractionIsMonotonicAndClamped()
        {
            EveryCase.Parts(
                ("distance grows from zero to the whole area", () =>
                {
                    float[] cumulative = ChargedProjectileFlight.Cumulative(ChargedProjectileFlight.DefaultSpeedCurve());
                    Assert.AreEqual(0f, ChargedProjectileFlight.DistanceFraction(cumulative, 0f), 1e-6f);
                    float previous = 0f;
                    for (int i = 1; i <= 50; i++)
                    {
                        float d = ChargedProjectileFlight.DistanceFraction(cumulative, i / 50f);
                        Assert.GreaterOrEqual(d, previous, "the palm never moves backwards");
                        previous = d;
                    }
                    Assert.AreEqual(cumulative[cumulative.Length - 1], ChargedProjectileFlight.DistanceFraction(cumulative, 1f), 1e-6f);
                    // most of the way is covered in the burst: past half the range by 40 % of the flight
                    Assert.Greater(ChargedProjectileFlight.DistanceFraction(cumulative, 0.4f), previous * 0.5f);
                }),
                // A shape tuned below zero only stops the palm; it never flies back toward the caster.
                ("a negative shape never moves the palm backwards", () =>
                {
                    var dip = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, -1f), new Keyframe(1f, 1f));
                    float[] cumulative = ChargedProjectileFlight.Cumulative(dip);
                    for (int i = 1; i < cumulative.Length; i++)
                        Assert.GreaterOrEqual(cumulative[i], cumulative[i - 1], "sample " + i);
                    Assert.Greater(cumulative[cumulative.Length - 1], 0f);
                }),
                // Before launch the palm is at the start; after the flight it stays at the full range.
                ("time outside the flight is clamped", () =>
                {
                    float[] cumulative = ChargedProjectileFlight.Cumulative(ChargedProjectileFlight.DefaultSpeedCurve());
                    Assert.AreEqual(0f, ChargedProjectileFlight.DistanceFraction(cumulative, -0.5f));
                    Assert.AreEqual(cumulative[cumulative.Length - 1], ChargedProjectileFlight.DistanceFraction(cumulative, 1.5f), 1e-6f);
                }));
        }

        // Range = peak x flight seconds x the shape's area: a flat shape covers peak x seconds, a linear ramp half of it.
        [Test]
        public void RangeIsPeakSpeedTimesFlightTimeTimesTheShapesArea()
        {
            Assert.AreEqual(300f, ChargedProjectileFlight.Range(AnimationCurve.Constant(0f, 1f, 1f), 100f, 3f), 1e-3f);
            Assert.AreEqual(100f, ChargedProjectileFlight.Range(AnimationCurve.Linear(0f, 0f, 1f, 1f), 100f, 2f), 1e-3f);
            Assert.AreEqual(0f, ChargedProjectileFlight.Range(null, 100f, 3f));

            var skill = AssetDatabase.LoadAssetAtPath<Skill_PushProjectileHands>(SkillAsset);
            Assert.IsNotNull(skill, SkillAsset);
            Assert.IsNotNull(skill.projectileSpeedCurve);
            Assert.GreaterOrEqual(skill.projectileSpeedCurve.length, 2, "projectileSpeedCurve has no shape");
        }
    }
}
