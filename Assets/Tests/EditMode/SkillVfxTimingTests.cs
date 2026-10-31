using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
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

        // Tuned at Imp 10 s, played at 20 s: k = 2 for a whole-phase window.
        [TestCase(SkillVfxTimeScaling.Fixed, SkillVfxWindowUnit.PhaseFraction, 1f, 1f, 1f, 2f)]
        [TestCase(SkillVfxTimeScaling.Stretch, SkillVfxWindowUnit.PhaseFraction, 0.5f, 1f, 1f, 2f)]
        [TestCase(SkillVfxTimeScaling.Rhythm, SkillVfxWindowUnit.PhaseFraction, 1f, 2f, 2f, 4f)]
        // A Seconds window does not move with the phase, so nothing rescales.
        [TestCase(SkillVfxTimeScaling.Stretch, SkillVfxWindowUnit.Seconds, 1f, 1f, 1f, 2f)]
        public void LayerContentFollowsTheGameplayLength(SkillVfxTimeScaling scaling, SkillVfxWindowUnit unit,
            float simulationSpeed, float startDelay, float burstTime, float duration)
        {
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Smoke_01");
            try
            {
                var controller = root.AddComponent<SkillVfxPhaseController>();
                var so = new SerializedObject(controller);
                SerializedProperty references = so.FindProperty("referenceSeconds");
                references.arraySize = 1;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = 10f;
                so.ApplyModifiedPropertiesWithoutUndo();

                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.timeScaling = scaling;
                layer.startUnit = unit;
                layer.start = 0f;
                layer.endUnit = unit;
                layer.end = unit == SkillVfxWindowUnit.Seconds ? 10f : -1f;

                var psGo = new GameObject("Smoke");
                psGo.transform.SetParent(layerGo.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.duration = 2f;
                main.startDelay = 1f;
                main.simulationSpeed = 1f;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(1f, 5) });

                controller.Configure(new SkillVfxPhaseController.Context());
                controller.BeginPhase(SkillVfxPhase.Imp, 20f);
                typeof(SkillVfxPhaseController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null);

                main = ps.main;
                Assert.That(main.simulationSpeed, Is.EqualTo(simulationSpeed).Within(1e-4f), "simulation speed");
                Assert.That(main.startDelay.constant, Is.EqualTo(startDelay).Within(1e-4f), "start delay");
                Assert.That(ps.emission.GetBurst(0).time, Is.EqualTo(burstTime).Within(1e-4f), "burst time");
                Assert.That(main.duration, Is.EqualTo(duration).Within(1e-4f), "emission length");
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
            }
        }

        // 禅定缓流 (2026-10-30): at the window end the particles only stop emitting; the slot waits for the last one to
        // die of old age (lifetime 5 s, stretched ×2 at a 12 s zone tuned at 6 s) instead of cutting them after the
        // fixed 1.5 s tail. Without the switch the fixed tail (×k) still applies.
        [TestCase(true, 12f + 10f)]
        [TestCase(false, 12f + 1.5f * 2f)]
        public void ParticlesDieNaturallyKeepsTheSlotUntilTheLastParticleDies(bool dieNaturally, float expectedSeconds)
        {
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Flash_01");
            try
            {
                var controller = root.AddComponent<SkillVfxPhaseController>();
                var so = new SerializedObject(controller);
                SerializedProperty references = so.FindProperty("referenceSeconds");
                references.arraySize = 1;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = 6f;
                so.ApplyModifiedPropertiesWithoutUndo();

                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.timeScaling = SkillVfxTimeScaling.Stretch;
                layer.particlesDieNaturally = dieNaturally;

                var psGo = new GameObject("Ground");
                psGo.transform.SetParent(layerGo.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.simulationSpeed = 1f;

                controller.Configure(new SkillVfxPhaseController.Context());
                float before = Time.time;
                controller.BeginPhase(SkillVfxPhase.Imp, 12f);
                controller.DestroyWhenFinished(1.5f);

                float destroyAt = (float)typeof(SkillVfxPhaseController)
                    .GetField("_destroyAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(controller);
                Assert.That(destroyAt - before, Is.EqualTo(expectedSeconds).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
            }
        }

        // 转向反转 (2026-10-30): a long-lived system listed in endWithWindow is gone by the window end (at most
        // endWithWindowSeconds after it, real time), so the slot waits only for the rest (5 s smoke ×2 stretched)
        // instead of its 12 s lifetime ×2.
        [TestCase(true, 12f + 5f * 2f)]
        [TestCase(false, 12f + 12f * 2f)]
        public void EndWithWindowCapsTheLongLivedSystemsTail(bool listed, float expectedSeconds)
        {
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Smoke_01");
            try
            {
                var controller = root.AddComponent<SkillVfxPhaseController>();
                var so = new SerializedObject(controller);
                SerializedProperty references = so.FindProperty("referenceSeconds");
                references.arraySize = 1;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
                references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = 6f;
                so.ApplyModifiedPropertiesWithoutUndo();

                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.timeScaling = SkillVfxTimeScaling.Stretch;
                layer.particlesDieNaturally = true;

                AddParticles(layerGo, "Smoke", 5f);
                ParticleSystem symbol = AddParticles(layerGo, "Symbol", 12f);
                layer.endWithWindow = listed ? new[] { symbol } : new ParticleSystem[0];
                layer.endWithWindowSeconds = 6f;

                controller.Configure(new SkillVfxPhaseController.Context());
                float before = Time.time;
                controller.BeginPhase(SkillVfxPhase.Imp, 12f);
                controller.DestroyWhenFinished(1.5f);

                float destroyAt = (float)typeof(SkillVfxPhaseController)
                    .GetField("_destroyAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(controller);
                Assert.That(destroyAt - before, Is.EqualTo(expectedSeconds).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
            }
        }

        // The yin-yang's alpha: fade in to 0.1, hold, fade out from 0.9. A particle keeps its alpha when its ending
        // starts: one holding at full alpha starts at the top of the fade-out, one still fading in (alpha 0.48 at
        // 0.05) starts where the fade-out is at 0.48, one already fading out is unchanged.
        [TestCase(0.5f, 0.9f + 0.1f * (1f - 0.98f))]
        [TestCase(0.05f, 0.9f + 0.1f * (1f - 0.48f))]
        [TestCase(0.95f, 0.95f)]
        public void EndingAgeKeepsTheParticlesAlpha(float age, float expected)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.96f, 0.1f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
            Assert.That(SkillVfxRules.EndingAge(gradient, age), Is.EqualTo(expected).Within(0.002f));
        }

        static ParticleSystem AddParticles(GameObject parent, string name, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = lifetime;
            main.simulationSpeed = 1f;
            return ps;
        }
    }
}
