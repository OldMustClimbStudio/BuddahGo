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
        public void DefaultShapeReproducesTheInkPalmRange()
        {
            // PalmInkTimeline in VFX_Palm_Bullet: travelDistance 1070 over 4 s, derived peak 889.77, 1 s of it gathering.
            float range = ChargedProjectileFlight.Range(ChargedProjectileFlight.DefaultSpeedCurve(), 889.77f, 3f);
            Assert.AreEqual(1070f, range, 1070f * 0.01f);
        }

        [Test]
        public void DistanceGrowsFromZeroToTheWholeArea()
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
        }

        [Test]
        public void PalmSkillCarriesTheShapeAndTheTableSpeed()
        {
            var skill = AssetDatabase.LoadAssetAtPath<Skill_PushProjectileHands>(SkillAsset);
            Assert.IsNotNull(skill, SkillAsset);
            Assert.IsNotNull(skill.projectileSpeedCurve);
            Assert.GreaterOrEqual(skill.projectileSpeedCurve.length, 2, "projectileSpeedCurve has no shape");

            ProjectConfigDatabase db = DesignTableOverlay.Db;
            Assert.IsNotNull(db);
            PushRecord charged = db.pushes.Find(r => r.pushId == "charged");
            Assert.IsNotNull(charged, "Push charged row");
            Assert.AreEqual(charged.speed, skill.projectileSpeed, 1e-3f, "SO fallback speed matches the Push sheet");
            float range = ChargedProjectileFlight.Range(skill.projectileSpeedCurve, charged.speed, charged.lifetimeSeconds);
            Assert.That(range, Is.InRange(100f, 5000f), "range " + range);
        }
    }
}
