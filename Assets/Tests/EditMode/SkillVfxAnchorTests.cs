using BuddahGo.VFX;
using NUnit.Framework;
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
            using (var rig = new SkillVfxTestRig())
            {
                Transform caster = rig.New("Caster").transform;
                caster.SetPositionAndRotation(new Vector3(100f, 5f, -40f), Quaternion.Euler(0f, yaw, 0f));
                caster.localScale = Vector3.one * casterScale;
                Vector3 forward = caster.forward;

                SkillVfxPhaseController controller = rig.Slot();
                controller.transform.SetPositionAndRotation(
                    caster.position + (Vector3.up * 30f + forward * 7f) * casterScale,
                    Quaternion.LookRotation(forward, Vector3.up));
                Transform scaled = rig.New("Scaled", controller.transform).transform;
                SkillVfxTestRig.SetScaledRoot(controller, scaled);

                SkillVfxLayer layer = rig.Layer(scaled, SkillVfxAnchor.CasterRelative, new Vector3(0f, 2.93f, 7f));
                layer.transform.localPosition = new Vector3(0f, -27f, 0f);
                SkillVfxTestRig.Window(layer, SkillVfxWindowUnit.Seconds, 0f, 10f);

                controller.Configure(new SkillVfxPhaseController.Context
                {
                    colliderSize = new Vector3(60f, 50f, 20f),
                    caster = caster,
                });
                controller.BeginPhase(SkillVfxPhase.Imp);
                controller.LateUpdate();

                Vector3 expected = caster.TransformPoint(layer.casterOffset);
                Assert.That(Vector3.Distance(layer.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Vector3.Angle(layer.transform.forward, forward), Is.LessThan(0.01f));
                Assert.That(layer.transform.lossyScale.y, Is.EqualTo(casterScale).Within(1e-4f));
            }
        }

        // The caster's frame is the launch direction flattened to yaw: a palm launched up a slope or straight up does
        // not tilt the release effect off the caster's body.
        [Test]
        public void CasterRelativeFrameIgnoresTheLaunchPitch()
        {
            var caster = new Vector3(10f, 2f, -5f);
            var offset = new Vector3(1f, 3f, 7f);
            SkillVfxRules.CasterRelativePose(caster, 2f, Quaternion.Euler(0f, 60f, 0f), offset, new Vector3(0f, 0f, 90f),
                out Vector3 flat, out Quaternion flatRotation);
            SkillVfxRules.CasterRelativePose(caster, 2f, Quaternion.Euler(-35f, 60f, 0f), offset, new Vector3(0f, 0f, 90f),
                out Vector3 pitched, out Quaternion pitchedRotation);

            Assert.That(Vector3.Distance(flat, caster + Quaternion.Euler(0f, 60f, 0f) * (offset * 2f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(flatRotation, Quaternion.Euler(0f, 60f, 0f) * Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(pitched, flat), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(pitchedRotation, flatRotation), Is.LessThan(0.01f));

            // Straight up has no yaw: the offset stays in world axes.
            SkillVfxRules.CasterRelativePose(caster, 1f, Quaternion.Euler(-90f, 0f, 0f), offset, Vector3.zero,
                out Vector3 up, out _);
            Assert.That(Vector3.Distance(up, caster + offset), Is.LessThan(1e-4f));
        }

        // CasterAttached anchor (buff / trail / zone placeholders): the layer keeps to the caster while the window runs,
        // and a trail anywhere under the layer (a character trail copied into the effect) is switched on with it.
        [Test]
        public void CasterAttachedLayerFollowsTheCasterAndStartsChildTrails()
        {
            using (var rig = new SkillVfxTestRig())
            {
                Transform caster = rig.New("Caster").transform;
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterAttached, new Vector3(0f, 3f, -2f));
                SkillVfxTestRig.Window(layer, SkillVfxWindowUnit.Seconds, 0f, 10f);
                var trail = rig.New("Trail", layer.transform).AddComponent<TrailRenderer>();
                trail.enabled = false;
                trail.time = 0.5f;

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster });
                controller.BeginPhase(SkillVfxPhase.Imp);
                controller.LateUpdate();
                Assert.That(trail.enabled && trail.emitting, Is.True);
                Assert.That(trail.time, Is.EqualTo(0.5f));

                caster.SetPositionAndRotation(new Vector3(50f, 0f, 80f), Quaternion.Euler(0f, 120f, 0f));
                caster.localScale = Vector3.one * 2f;
                controller.LateUpdate();
                Vector3 expected = caster.TransformPoint(layer.casterOffset);
                Assert.That(Vector3.Distance(layer.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Vector3.Angle(layer.transform.forward, caster.forward), Is.LessThan(0.01f));
                Assert.That(layer.transform.lossyScale.y, Is.EqualTo(2f).Within(1e-4f));
            }
        }

        // CasterFollow anchor (禅定缓流's ground zone, 2026-10-30): the layer keeps to the caster's position and scale,
        // during and after its window, but keeps the facing the effect was played with when the caster turns.
        [Test]
        public void CasterFollowLayerFollowsThePositionButNotTheFacing()
        {
            using (var rig = new SkillVfxTestRig())
            {
                Transform caster = rig.New("Caster").transform;
                caster.rotation = Quaternion.Euler(0f, 30f, 0f);
                SkillVfxPhaseController controller = rig.Slot();
                controller.transform.rotation = caster.rotation;
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterFollow, new Vector3(0f, 3f, -2f));

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster });
                controller.BeginPhase(SkillVfxPhase.Imp, 5f);
                controller.LateUpdate();

                Quaternion started = controller.transform.rotation;
                caster.SetPositionAndRotation(new Vector3(50f, 0f, 80f), Quaternion.Euler(0f, 150f, 0f));
                caster.localScale = Vector3.one * 2f;
                controller.LateUpdate();
                Vector3 expected = caster.position + started * (layer.casterOffset * 2f);
                Assert.That(Vector3.Distance(layer.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Quaternion.Angle(layer.transform.rotation, started), Is.LessThan(0.01f));
                Assert.That(layer.transform.lossyScale.y, Is.EqualTo(2f).Within(1e-4f));

                // After the window: still on the caster, still not turned.
                controller.EndPhase(SkillVfxPhase.Imp);
                controller.LateUpdate();
                caster.SetPositionAndRotation(new Vector3(-30f, 0f, 200f), Quaternion.Euler(0f, 270f, 0f));
                controller.LateUpdate();
                expected = caster.position + started * (layer.casterOffset * 2f);
                Assert.That(Vector3.Distance(layer.transform.position, expected), Is.LessThan(1e-3f));
                Assert.That(Quaternion.Angle(layer.transform.rotation, started), Is.LessThan(0.01f));
            }
        }

        // After its window the layer stops emitting, but the particles still alive stay on the caster (禅定缓流 window
        // ends at 55 % of the slow; its smoke was left on the track).
        [Test]
        public void CasterAttachedLayerKeepsFollowingAfterItsWindow()
        {
            using (var rig = new SkillVfxTestRig())
            {
                Transform caster = rig.New("Caster").transform;
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform, SkillVfxAnchor.CasterAttached, new Vector3(0f, 3f, 0f));

                controller.Configure(new SkillVfxPhaseController.Context { caster = caster });
                controller.BeginPhase(SkillVfxPhase.Imp, 5f);
                controller.LateUpdate();
                controller.EndPhase(SkillVfxPhase.Imp);
                controller.LateUpdate();

                caster.position = new Vector3(-30f, 0f, 200f);
                controller.LateUpdate();
                Assert.That(Vector3.Distance(layer.transform.position, caster.TransformPoint(layer.casterOffset)), Is.LessThan(1e-3f));
            }
        }

        // Anti volley (palm doc): a burstShared layer (the one launch flash of the volley) shows on burst 0 only,
        // moved to the burst centre; the other copies hide it.
        [TestCase(0, true)]
        [TestCase(2, false)]
        public void BurstSharedLayerShowsOnceAtTheBurstCentre(int burstIndex, bool shown)
        {
            using (var rig = new SkillVfxTestRig())
            {
                SkillVfxPhaseController controller = rig.Slot();
                SkillVfxLayer layer = rig.Layer(controller.transform);
                layer.burstShared = true;
                layer.transform.position = new Vector3(10f, 0f, 0f);

                controller.Configure(new SkillVfxPhaseController.Context
                {
                    burstIndex = burstIndex,
                    burstCenterOffsetWorld = new Vector3(0f, 0f, 5f),
                });

                Assert.That(layer.gameObject.activeSelf, Is.EqualTo(shown));
                if (shown)
                    Assert.That(Vector3.Distance(layer.transform.position, new Vector3(10f, 0f, 5f)), Is.LessThan(1e-4f));
            }
        }
    }
}
