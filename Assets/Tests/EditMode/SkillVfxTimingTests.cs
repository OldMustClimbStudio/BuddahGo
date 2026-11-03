using BuddahGo.VFX;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/README.md section 4 "时长缩放": an effect tuned at a reference phase length follows the length gameplay
    // passes (a design table change) by its layers' time scaling.
    public class SkillVfxTimingTests
    {
        [Test]
        public void TimeScaleIsTheLiveWindowOverTheReferenceWindow()
        {
            Assert.That(SkillVfxRules.TimeScale(0f, 20f, 0f, 10f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(SkillVfxRules.TimeScale(1f, 4f, 2f, 8f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SkillVfxRules.TimeScale(0f, 5f, 0f, 0f), Is.EqualTo(1f));
            Assert.That(SkillVfxRules.TimeScale(0f, 1000f, 0f, 1f), Is.EqualTo(SkillVfxRules.MaxTimeScale));
        }

        // README 3.4: a PhaseFraction window moves with the gameplay length, a Seconds window does not, and a negative
        // end runs to the end of the phase.
        [Test]
        public void LayerWindowMovesWithThePhaseOnlyInPhaseFraction()
        {
            using (var rig = new SkillVfxTestRig())
            {
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform);

                SkillVfxTestRig.Window(layer, SkillVfxWindowUnit.PhaseFraction, 0.25f, 0.75f);
                AssertWindow(controller, layer, 8f, 2f, 6f);
                AssertWindow(controller, layer, 4f, 1f, 3f);

                SkillVfxTestRig.Window(layer, SkillVfxWindowUnit.Seconds, 0.5f, 2f);
                AssertWindow(controller, layer, 8f, 0.5f, 2f);
                AssertWindow(controller, layer, 4f, 0.5f, 2f);

                layer.end = -1f;
                AssertWindow(controller, layer, 8f, 0.5f, 8f);
            }
        }

        // k is 1 wherever the content must not be rescaled: Fixed layers, a phase with no reference length and a
        // window that does not move with the phase.
        [Test]
        public void LayerTimeScaleOnlyRescalesMovingWindowsWithAReference()
        {
            var cases = new[]
            {
                (scaling: SkillVfxTimeScaling.Stretch, phase: SkillVfxPhase.Imp, unit: SkillVfxWindowUnit.PhaseFraction, expected: 2f),
                (scaling: SkillVfxTimeScaling.Rhythm, phase: SkillVfxPhase.Imp, unit: SkillVfxWindowUnit.PhaseFraction, expected: 2f),
                (scaling: SkillVfxTimeScaling.Fixed, phase: SkillVfxPhase.Imp, unit: SkillVfxWindowUnit.PhaseFraction, expected: 1f),
                (scaling: SkillVfxTimeScaling.Stretch, phase: SkillVfxPhase.Dis, unit: SkillVfxWindowUnit.PhaseFraction, expected: 1f),
                (scaling: SkillVfxTimeScaling.Stretch, phase: SkillVfxPhase.Imp, unit: SkillVfxWindowUnit.Seconds, expected: 1f),
            };
            EveryCase.Check(cases, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot(10f);
                    SkillVfxLayer layer = rig.Layer(controller.transform);
                    layer.phase = c.phase;
                    layer.timeScaling = c.scaling;
                    SkillVfxTestRig.Window(layer, c.unit, 0f, c.unit == SkillVfxWindowUnit.Seconds ? 5f : -1f);

                    Assert.That(controller.LayerTimeScale(layer, 20f), Is.EqualTo(c.expected).Within(1e-5f));
                }
            });
        }

        [Test]
        public void LayerContentFollowsTheGameplayLength()
        {
            var cases = new[]
            {
                // Tuned at Imp 10 s, played at 20 s: k = 2 for a whole-phase window.
                (scaling: SkillVfxTimeScaling.Fixed, unit: SkillVfxWindowUnit.PhaseFraction, simulationSpeed: 1f, startDelay: 1f, burstTime: 1f, duration: 2f),
                (scaling: SkillVfxTimeScaling.Stretch, unit: SkillVfxWindowUnit.PhaseFraction, simulationSpeed: 0.5f, startDelay: 1f, burstTime: 1f, duration: 2f),
                (scaling: SkillVfxTimeScaling.Rhythm, unit: SkillVfxWindowUnit.PhaseFraction, simulationSpeed: 1f, startDelay: 2f, burstTime: 2f, duration: 4f),
                // A Seconds window does not move with the phase, so nothing rescales.
                (scaling: SkillVfxTimeScaling.Stretch, unit: SkillVfxWindowUnit.Seconds, simulationSpeed: 1f, startDelay: 1f, burstTime: 1f, duration: 2f),
            };
            EveryCase.Check(cases, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot(10f);
                    SkillVfxLayer layer = rig.Layer(controller.transform);
                    layer.timeScaling = c.scaling;
                    SkillVfxTestRig.Window(layer, c.unit, 0f, c.unit == SkillVfxWindowUnit.Seconds ? 10f : -1f);

                    ParticleSystem ps = rig.Particles(layer, "Smoke", 5f);
                    ParticleSystem.MainModule main = ps.main;
                    main.duration = 2f;
                    main.startDelay = 1f;
                    ParticleSystem.EmissionModule emission = ps.emission;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(1f, 5) });

                    controller.Configure(new SkillVfxPhaseController.Context());
                    controller.BeginPhase(SkillVfxPhase.Imp, 20f);
                    controller.LateUpdate();

                    main = ps.main;
                    Assert.That(main.simulationSpeed, Is.EqualTo(c.simulationSpeed).Within(1e-4f), "simulation speed");
                    Assert.That(main.startDelay.constant, Is.EqualTo(c.startDelay).Within(1e-4f), "start delay");
                    Assert.That(ps.emission.GetBurst(0).time, Is.EqualTo(c.burstTime).Within(1e-4f), "burst time");
                    Assert.That(main.duration, Is.EqualTo(c.duration).Within(1e-4f), "emission length");
                }
            });
        }

        // 禅定缓流 (2026-10-30): at the window end the particles only stop emitting; the slot waits for the last one to
        // die of old age (lifetime 5 s, stretched ×2 at a 12 s zone tuned at 6 s) instead of cutting them after the
        // fixed 1.5 s tail. Without the switch the fixed tail (×k) still applies.
        [Test]
        public void ParticlesDieNaturallyKeepsTheSlotUntilTheLastParticleDies()
        {
            EveryCase.Check(new[] { (dieNaturally: true, expectedSeconds: 12f + 10f), (dieNaturally: false, expectedSeconds: 12f + 1.5f * 2f) }, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot(6f);
                    SkillVfxLayer layer = rig.Layer(controller.transform);
                    layer.timeScaling = SkillVfxTimeScaling.Stretch;
                    layer.particlesDieNaturally = c.dieNaturally;
                    ParticleSystem.MainModule main = rig.Particles(layer, "Ground", 5f).main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);

                    controller.Configure(new SkillVfxPhaseController.Context());
                    controller.BeginPhase(SkillVfxPhase.Imp, 12f);
                    Assert.That(controller.DestroyWhenFinished(1.5f), Is.EqualTo(c.expectedSeconds).Within(0.05f));
                }
            });
        }

        // 转向反转 (2026-10-30): a long-lived system listed in endWithWindow is gone by the window end (at most
        // endWithWindowSeconds after it, real time), so the slot waits only for the rest (5 s smoke ×2 stretched)
        // instead of its 12 s lifetime ×2.
        [Test]
        public void EndWithWindowCapsTheLongLivedSystemsTail()
        {
            EveryCase.Check(new[] { (listed: true, expectedSeconds: 12f + 5f * 2f), (listed: false, expectedSeconds: 12f + 12f * 2f) }, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot(6f);
                    SkillVfxLayer layer = rig.Layer(controller.transform);
                    layer.timeScaling = SkillVfxTimeScaling.Stretch;
                    layer.particlesDieNaturally = true;

                    rig.Particles(layer, "Smoke", 5f);
                    ParticleSystem symbol = rig.Particles(layer, "Symbol", 12f);
                    layer.endWithWindow = c.listed ? new[] { symbol } : new ParticleSystem[0];
                    layer.endWithWindowSeconds = 6f;

                    controller.Configure(new SkillVfxPhaseController.Context());
                    controller.BeginPhase(SkillVfxPhase.Imp, 12f);
                    Assert.That(controller.DestroyWhenFinished(1.5f), Is.EqualTo(c.expectedSeconds).Within(0.05f));
                }
            });
        }

        // A Seconds window may outlast the gameplay length (a 3 s hit with an 8 s ground mark): the slot lives until
        // the window ends, then the tail.
        [Test]
        public void SlotLivesUntilItsLongestWindowEnds()
        {
            EveryCase.Check(new[] { (windowEnd: 8f, expectedSeconds: 8f + 1.5f), (windowEnd: 2f, expectedSeconds: 3f + 1.5f) }, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot();
                    SkillVfxLayer layer = rig.Layer(controller.transform);
                    SkillVfxTestRig.Window(layer, SkillVfxWindowUnit.Seconds, 0f, c.windowEnd);

                    controller.Configure(new SkillVfxPhaseController.Context());
                    controller.BeginPhase(SkillVfxPhase.Imp, 3f);
                    Assert.That(controller.DestroyWhenFinished(1.5f), Is.EqualTo(c.expectedSeconds).Within(0.05f));
                }
            });
        }

        // Expiry (README 4): dissipation lasts the VFX table's dis_seconds (0.4 s default) or the longest Dis layer,
        // whichever is longer, plus the longest tail fade of a phase cut short.
        [Test]
        public void DissipationWaitsForTheLongestDisLayerAndTheTailFade()
        {
            EveryCase.Check(new[] { (disLayerSeconds: 0.9f, expectedSeconds: 0.9f + 0.3f), (disLayerSeconds: 0.2f, expectedSeconds: 0.4f + 0.3f) }, c =>
            {
                using (var rig = new SkillVfxTestRig())
                {
                    SkillVfxPhaseController controller = rig.Slot();
                    SkillVfxLayer body = rig.Layer(controller.transform);
                    body.tailFadeSeconds = 0.3f;
                    SkillVfxLayer dissolve = rig.Layer(controller.transform);
                    dissolve.phase = SkillVfxPhase.Dis;
                    SkillVfxTestRig.Window(dissolve, SkillVfxWindowUnit.Seconds, 0f, c.disLayerSeconds);

                    controller.Configure(new SkillVfxPhaseController.Context());
                    controller.BeginPhase(SkillVfxPhase.Imp, 5f);
                    Assert.That(controller.BeginDissipation(), Is.EqualTo(c.expectedSeconds).Within(1e-4f));
                }
            });
        }

        // The yin-yang's alpha: fade in to 0.1, hold, fade out from 0.9. A particle keeps its alpha when its ending
        // starts: one holding at full alpha starts at the top of the fade-out, one still fading in (alpha 0.48 at
        // 0.05) starts where the fade-out is at 0.48, one already fading out is unchanged.
        [Test]
        public void EndingAgeKeepsTheParticlesAlpha()
        {
            var cases = new[]
            {
                (age: 0.5f, expected: 0.9f + 0.1f * (1f - 0.98f)),
                (age: 0.05f, expected: 0.9f + 0.1f * (1f - 0.48f)),
                (age: 0.95f, expected: 0.95f),
            };
            EveryCase.Check(cases, c =>
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.96f, 0.1f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
                Assert.That(SkillVfxRules.EndingAge(gradient, c.age), Is.EqualTo(c.expected).Within(0.002f));
            });
        }

        static void AssertWindow(SkillVfxPhaseController controller, SkillVfxLayer layer, float phaseSeconds,
            float expectedStart, float expectedEnd)
        {
            controller.LayerWindow(layer, phaseSeconds, out float start, out float end);
            Assert.That(start, Is.EqualTo(expectedStart).Within(1e-5f), "start at a " + phaseSeconds + " s phase");
            Assert.That(end, Is.EqualTo(expectedEnd).Within(1e-5f), "end at a " + phaseSeconds + " s phase");
        }
    }
}
