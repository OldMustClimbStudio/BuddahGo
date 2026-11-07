using BuddahGo.VFX;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/giant.md: how a caster-anchored layer follows the caster's body scale (巨大化 ×4, its anti ×0.3).
    public class SkillVfxScaleBlendTests
    {
        [Test]
        public void BlendRulesEaseTheAreaAndKeepTheDetailGrain()
        {
            EveryCase.Parts(
                ("a change is a real one only past the threshold", () =>
                {
                    Assert.That(SkillVfxRules.ScaleChanged(1f, 1.01f), Is.False);
                    Assert.That(SkillVfxRules.ScaleChanged(1f, 1.05f), Is.True);
                    Assert.That(SkillVfxRules.ScaleChanged(4f, 4.05f), Is.False);
                    Assert.That(SkillVfxRules.ScaleChanged(1f, 0.3f), Is.True);
                }),
                ("growing eases out, shrinking eases in and out", () =>
                {
                    Assert.That(SkillVfxRules.BlendProgress(0f, false), Is.EqualTo(0f));
                    Assert.That(SkillVfxRules.BlendProgress(1f, false), Is.EqualTo(1f));
                    Assert.That(SkillVfxRules.BlendProgress(0.5f, false), Is.EqualTo(0.875f).Within(1e-5f));
                    Assert.That(SkillVfxRules.BlendProgress(0.5f, true), Is.EqualTo(0.5f).Within(1e-5f));
                    Assert.That(SkillVfxRules.BlendProgress(2f, true), Is.EqualTo(1f));
                }),
                ("the area alpha dips halfway and is whole at both ends", () =>
                {
                    Assert.That(SkillVfxRules.BlendAlpha(0f, 0.3f), Is.EqualTo(1f));
                    Assert.That(SkillVfxRules.BlendAlpha(0.5f, 0.3f), Is.EqualTo(0.7f).Within(1e-5f));
                    Assert.That(SkillVfxRules.BlendAlpha(1f, 0.3f), Is.EqualTo(1f));
                }),
                ("details keep their grain and density for giant and anti", () =>
                {
                    Assert.That(SkillVfxRules.DetailSize(1f, 0.3f), Is.EqualTo(1f).Within(1e-5f));
                    Assert.That(SkillVfxRules.DetailSize(4f, 0.3f), Is.EqualTo(1.5157f).Within(1e-3f));
                    Assert.That(SkillVfxRules.DetailSize(0.3f, 0.3f), Is.EqualTo(0.6968f).Within(1e-3f));
                    Assert.That(SkillVfxRules.DetailEmission(1f, 6f), Is.EqualTo(1f));
                    Assert.That(SkillVfxRules.DetailEmission(2f, 6f), Is.EqualTo(4f).Within(1e-5f));
                    Assert.That(SkillVfxRules.DetailEmission(4f, 6f), Is.EqualTo(6f));
                    Assert.That(SkillVfxRules.DetailEmission(0.3f, 6f), Is.EqualTo(1f / 6f).Within(1e-5f));
                }),
                ("particles alive at the change fade out over the handoff", () =>
                {
                    Assert.That(SkillVfxRules.HandoffAlpha(0f, 0.8f), Is.EqualTo(1f));
                    Assert.That(SkillVfxRules.HandoffAlpha(0.4f, 0.8f), Is.EqualTo(0.5f).Within(1e-5f));
                    Assert.That(SkillVfxRules.HandoffAlpha(1f, 0.8f), Is.EqualTo(0f));
                    Assert.That(SkillVfxRules.HandoffAlpha(0f, 0f), Is.EqualTo(0f));
                }));
        }

        // A Blend layer (禅定缓流's ground zone): giant ×4 eases the area out over scaleUpSeconds, its anti ×0.3 (and
        // any shrink) eases it in and out over scaleDownSeconds with the detail emission paused; details are born at
        // their own grain; the surge leaves the layer. A Whole layer takes the caster's scale at once.
        // None (金足破阵's ink route): the layer keeps its authored size at any body scale, times the play's area scale
        // when it passes one (禅足冰封's ground: radius / R).
        [Test]
        public void NoneLayerKeepsItsAuthoredSize()
        {
            using (var rig = new SkillVfxTestRig())
            {
                SkillVfxLayer layer = rig.Layer(rig.Slot().transform, SkillVfxAnchor.CasterRelative);
                layer.casterScaling = SkillVfxCasterScaling.None;
                Assert.That(layer.FollowCasterScale(1f, 0f), Is.EqualTo(1f));
                Assert.That(layer.FollowCasterScale(4f, 0.5f), Is.EqualTo(1f));
                Assert.That(layer.FollowCasterScale(0.3f, 1f), Is.EqualTo(1f));
                layer.SetAreaScale(2f);
                Assert.That(layer.FollowCasterScale(1f, 1.5f), Is.EqualTo(2f), "the play's area scale, not the body");
                Assert.That(layer.FollowCasterScale(4f, 2f), Is.EqualTo(2f));
                layer.SetAreaScale(0f);
                Assert.That(layer.FollowCasterScale(4f, 2.5f), Is.EqualTo(1f), "0 or less means 1");
            }
        }

        [Test]
        public void BlendLayerEasesTheAreaBothWaysAndKeepsTheDetailGrain()
        {
            using (var rig = new SkillVfxTestRig())
            {
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterFollow);
                ParticleSystem area = rig.Particles(layer, "Area", 5f);
                ParticleSystem detail = rig.Particles(layer, "Detail", 5f);
                ParticleSystem surge = rig.Particles(layer, "Surge", 1.1f);
                ParticleSystem.MainModule detailMain = detail.main;
                detailMain.startSize = 2f;
                ParticleSystem.EmissionModule detailEmission = detail.emission;
                detailEmission.rateOverTime = 10f;
                layer.casterScaling = SkillVfxCasterScaling.Blend;
                layer.areaSystems = new[] { area };
                layer.scaleSurge = surge;
                layer.Initialize(1f, true);

                EveryCase.Parts(
                    ("the surge leaves the layer and the first placement takes the caster as it is", () =>
                    {
                        Assert.That(surge.transform.IsChildOf(layer.transform), Is.False);
                        Assert.That(layer.FollowCasterScale(1f, 0f), Is.EqualTo(1f));
                    }),
                    ("giant ×4 eases the area out", () =>
                    {
                        Assert.That(layer.FollowCasterScale(4f, 0.1f), Is.EqualTo(1f).Within(1e-4f));
                        Assert.That(layer.FollowCasterScale(4f, 0.6f), Is.EqualTo(1f + 3f * 0.875f).Within(1e-3f));
                        Assert.That(detail.main.startSize.constant, Is.EqualTo(2f * SkillVfxRules.DetailSize(3.625f, 0.3f)).Within(1e-3f));
                        Assert.That(detail.emission.rateOverTimeMultiplier, Is.EqualTo(60f).Within(1e-3f));
                        Assert.That(layer.FollowCasterScale(4f, 1.2f), Is.EqualTo(4f).Within(1e-4f));
                    }),
                    ("anti ×0.3 eases it back in with the detail emission paused", () =>
                    {
                        Assert.That(layer.FollowCasterScale(0.3f, 2f), Is.EqualTo(4f).Within(1e-4f));
                        Assert.That(layer.FollowCasterScale(0.3f, 2.4f), Is.EqualTo(4f + (0.3f - 4f) * 0.5f).Within(1e-3f));
                        Assert.That(detail.emission.rateOverTimeMultiplier, Is.EqualTo(0f));
                        Assert.That(layer.FollowCasterScale(0.3f, 2.9f), Is.EqualTo(0.3f).Within(1e-4f));
                        Assert.That(detail.emission.rateOverTimeMultiplier, Is.EqualTo(10f / 6f).Within(1e-3f));
                        Assert.That(detail.main.startSize.constant, Is.EqualTo(2f * SkillVfxRules.DetailSize(0.3f, 0.3f)).Within(1e-3f));
                    }),
                    ("a Whole layer follows at once", () =>
                    {
                        SkillVfxLayer whole = rig.Layer(controller.transform, SkillVfxAnchor.CasterFollow);
                        whole.Initialize(1f, true);
                        Assert.That(whole.FollowCasterScale(1f, 0f), Is.EqualTo(1f));
                        Assert.That(whole.FollowCasterScale(4f, 0.1f), Is.EqualTo(4f));
                    }));
            }
        }

        // The surge marks a change while the window runs; after it (the zone fading out) a change only eases the scale.
        [Test]
        public void SurgePlaysOnlyWhileTheWindowRuns()
        {
            using (var rig = new SkillVfxTestRig())
            {
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterFollow);
                ParticleSystem area = rig.Particles(layer, "Area", 5f);
                ParticleSystem surge = rig.Particles(layer, "Surge", 1.1f);
                layer.casterScaling = SkillVfxCasterScaling.Blend;
                layer.areaSystems = new[] { area };
                layer.scaleSurge = surge;
                layer.Initialize(1f, true);
                layer.FollowCasterScale(1f, 0f);

                layer.Apply(0f, 1f, 0f, 1f, null);
                layer.FollowCasterScale(4f, 1f);
                Assert.That(surge.isPlaying, Is.True, "in the window");

                surge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                layer.FollowCasterScale(4f, 3f);
                layer.Deactivate();
                layer.FollowCasterScale(1f, 4f);
                Assert.That(surge.isPlaying, Is.False, "after the window");
                Assert.That(layer.FollowCasterScale(1f, 4.4f), Is.EqualTo(2.5f).Within(1e-3f), "the scale still eases");
            }
        }

        // Through the controller: the layer's ground offset follows the body at once, its scale starts the blend.
        [Test]
        public void ControllerKeepsABlendLayerOnTheGroundWhileItsScaleCatchesUp()
        {
            using (var rig = new SkillVfxTestRig())
            {
                Transform caster = rig.New("Caster").transform;
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterFollow, new Vector3(0f, 0.62f, 0f));
                ParticleSystem area = rig.Particles(layer, "Area", 5f);
                layer.casterScaling = SkillVfxCasterScaling.Blend;
                layer.areaSystems = new[] { area };

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster });
                controller.BeginPhase(SkillVfxPhase.Imp, 6f);
                controller.LateUpdate();
                Assert.That(layer.transform.lossyScale.y, Is.EqualTo(1f).Within(1e-4f));

                caster.localScale = Vector3.one * 4f;
                controller.LateUpdate();
                Assert.That(layer.transform.position.y, Is.EqualTo(0.62f * 4f).Within(1e-3f));
                Assert.That(layer.transform.lossyScale.y, Is.EqualTo(1f).Within(1e-4f), "the blend starts from the old scale");
            }
        }
    }
}
