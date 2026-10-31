using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/README.md section 4: the caster anchors of SkillVfxLayer (release / hit slots placed on the caster's
    // body, at any release angle and caster scale, independent of the gameplay collider).
    public class SkillVfxAnchorTests
    {
        // CasterRelative anchor (palm doc, Frontal burst): the layer lands on the same spot of the caster's body at any
        // release angle and caster scale, whatever the gameplay collider (anti 60 x 50 x 20) does to the scaled root.
        [TestCase(0f, 1f)]
        [TestCase(90f, 1f)]
        [TestCase(180f, 2f)]
        [TestCase(270f, 0.5f)]
        [TestCase(37f, 1.6f)]
        public void CasterRelativeLayerStaysOnTheCasterAtAnyAngleAndScale(float yaw, float casterScale)
        {
            var caster = new GameObject("Caster");
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Smoke_01");
            try
            {
                caster.transform.SetPositionAndRotation(new Vector3(100f, 5f, -40f), Quaternion.Euler(0f, yaw, 0f));
                caster.transform.localScale = Vector3.one * casterScale;
                Vector3 forward = caster.transform.forward;
                root.transform.SetPositionAndRotation(
                    caster.transform.position + (Vector3.up * 30f + forward * 7f) * casterScale,
                    Quaternion.LookRotation(forward, Vector3.up));

                var controller = root.AddComponent<SkillVfxPhaseController>();
                var scaled = new GameObject("Scaled").transform;
                scaled.SetParent(root.transform, false);
                var so = new SerializedObject(controller);
                so.FindProperty("scaledRoot").objectReferenceValue = scaled;
                so.ApplyModifiedPropertiesWithoutUndo();

                layerGo.transform.SetParent(scaled, false);
                layerGo.transform.localPosition = new Vector3(0f, -27f, 0f);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.anchor = SkillVfxAnchor.CasterRelative;
                layer.casterOffset = new Vector3(0f, 2.93f, 7f);
                layer.casterEuler = new Vector3(0f, 0f, 0f);
                layer.startUnit = SkillVfxWindowUnit.Seconds;
                layer.start = 0f;
                layer.endUnit = SkillVfxWindowUnit.Seconds;
                layer.end = 10f;

                controller.Configure(new SkillVfxPhaseController.Context
                {
                    colliderSize = new Vector3(60f, 50f, 20f),
                    caster = caster.transform,
                });
                controller.BeginPhase(SkillVfxPhase.Imp);
                typeof(SkillVfxPhaseController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null);

                Vector3 expected = caster.transform.TransformPoint(layer.casterOffset);
                Assert.That(Vector3.Distance(layerGo.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Vector3.Angle(layerGo.transform.forward, forward), Is.LessThan(0.01f));
                Assert.That(layerGo.transform.lossyScale.y, Is.EqualTo(casterScale).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(caster);
            }
        }

        // CasterAttached anchor (buff / trail / zone placeholders): the layer keeps to the caster while the window runs,
        // and a trail anywhere under the layer (a character trail copied into the effect) is switched on with it.
        [Test]
        public void CasterAttachedLayerFollowsTheCasterAndStartsChildTrails()
        {
            var caster = new GameObject("Caster");
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Flash_01");
            try
            {
                var controller = root.AddComponent<SkillVfxPhaseController>();
                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.anchor = SkillVfxAnchor.CasterAttached;
                layer.casterOffset = new Vector3(0f, 3f, -2f);
                layer.startUnit = SkillVfxWindowUnit.Seconds;
                layer.start = 0f;
                layer.endUnit = SkillVfxWindowUnit.Seconds;
                layer.end = 10f;
                var trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(layerGo.transform, false);
                var trail = trailGo.AddComponent<TrailRenderer>();
                trail.enabled = false;
                trail.time = 0.5f;

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster.transform });
                controller.BeginPhase(SkillVfxPhase.Imp);
                var lateUpdate = typeof(SkillVfxPhaseController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                lateUpdate.Invoke(controller, null);
                Assert.That(trail.enabled && trail.emitting, Is.True);
                Assert.That(trail.time, Is.EqualTo(0.5f));

                caster.transform.SetPositionAndRotation(new Vector3(50f, 0f, 80f), Quaternion.Euler(0f, 120f, 0f));
                caster.transform.localScale = Vector3.one * 2f;
                lateUpdate.Invoke(controller, null);
                Vector3 expected = caster.transform.TransformPoint(layer.casterOffset);
                Assert.That(Vector3.Distance(layerGo.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Vector3.Angle(layerGo.transform.forward, caster.transform.forward), Is.LessThan(0.01f));
                Assert.That(layerGo.transform.lossyScale.y, Is.EqualTo(2f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(caster);
            }
        }

        // CasterFollow anchor (禅定缓流's ground zone, 2026-10-30): the layer keeps to the caster's position and scale,
        // during and after its window, but keeps the facing the effect was played with when the caster turns.
        [Test]
        public void CasterFollowLayerFollowsThePositionButNotTheFacing()
        {
            var caster = new GameObject("Caster");
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Flash_01");
            try
            {
                caster.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                root.transform.rotation = caster.transform.rotation;
                var controller = root.AddComponent<SkillVfxPhaseController>();
                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.anchor = SkillVfxAnchor.CasterFollow;
                layer.casterOffset = new Vector3(0f, 3f, -2f);
                layer.end = -1f;

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster.transform });
                controller.BeginPhase(SkillVfxPhase.Imp, 5f);
                var lateUpdate = typeof(SkillVfxPhaseController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                lateUpdate.Invoke(controller, null);

                Quaternion started = root.transform.rotation;
                caster.transform.SetPositionAndRotation(new Vector3(50f, 0f, 80f), Quaternion.Euler(0f, 150f, 0f));
                caster.transform.localScale = Vector3.one * 2f;
                lateUpdate.Invoke(controller, null);
                Vector3 expected = caster.transform.position + started * (layer.casterOffset * 2f);
                Assert.That(Vector3.Distance(layerGo.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Quaternion.Angle(layerGo.transform.rotation, started), Is.LessThan(0.01f));
                Assert.That(layerGo.transform.lossyScale.y, Is.EqualTo(2f).Within(1e-4f));

                // After the window: still on the caster, still not turned.
                controller.EndPhase(SkillVfxPhase.Imp);
                lateUpdate.Invoke(controller, null);
                caster.transform.SetPositionAndRotation(new Vector3(-30f, 0f, 200f), Quaternion.Euler(0f, 270f, 0f));
                lateUpdate.Invoke(controller, null);
                expected = caster.transform.position + started * (layer.casterOffset * 2f);
                Assert.That(Vector3.Distance(layerGo.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Quaternion.Angle(layerGo.transform.rotation, started), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(caster);
            }
        }

        // After its window the layer stops emitting, but the particles still alive stay on the caster (禅定缓流 window
        // ends at 55 % of the slow; its smoke was left on the track).
        [Test]
        public void CasterAttachedLayerKeepsFollowingAfterItsWindow()
        {
            var caster = new GameObject("Caster");
            var root = new GameObject("Effect");
            var layerGo = new GameObject("FX_Test_Imp_C_Smoke_01");
            try
            {
                var controller = root.AddComponent<SkillVfxPhaseController>();
                layerGo.transform.SetParent(root.transform, false);
                var layer = layerGo.AddComponent<SkillVfxLayer>();
                layer.phase = SkillVfxPhase.Imp;
                layer.anchor = SkillVfxAnchor.CasterAttached;
                layer.casterOffset = new Vector3(0f, 3f, 0f);
                layer.end = -1f;

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster.transform });
                controller.BeginPhase(SkillVfxPhase.Imp, 5f);
                var lateUpdate = typeof(SkillVfxPhaseController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                lateUpdate.Invoke(controller, null);
                controller.EndPhase(SkillVfxPhase.Imp);
                lateUpdate.Invoke(controller, null);

                caster.transform.position = new Vector3(-30f, 0f, 200f);
                lateUpdate.Invoke(controller, null);
                Assert.That(Vector3.Distance(layerGo.transform.position, caster.transform.TransformPoint(layer.casterOffset)), Is.LessThan(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(layerGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(caster);
            }
        }
    }
}
