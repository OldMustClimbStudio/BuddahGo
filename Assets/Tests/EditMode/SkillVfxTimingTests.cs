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
    }
}
