using System.Collections.Generic;
using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace BuddahGo.Tests
{
    // Docs/vfx/push-projectile-hands.md section 15: the ink palm's 3D fluid is shared by every palm on this client.
    // At most simulationBudget volumes simulate, the ones nearest the camera; without compute shaders (batch mode
    // -nographics, a headless server) the fluid stays off quietly. None of these tests touches the GPU.
    public class InkFluidVolumeTests
    {
        const string ComputeAsset = "Assets/Rendering/Shaders/FX/InkFluid3D.compute";

        readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            InkFluidVolume.ForceNoCompute = false;
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void EveryVolumeSimulatesWhileTheyFitTheBudget()
        {
            var volumes = new List<InkFluidVolume> { Volume(0f, 3), Volume(500f, 3), Volume(900f, 3) };
            Assert.That(InkFluidVolume.PickSimulating(volumes, true, Vector3.zero), Is.EqualTo(3));
            Assert.That(volumes.TrueForAll(v => v.InBudget), Is.True);
        }

        [Test]
        public void OverTheBudgetTheNearestSimulate()
        {
            var volumes = new List<InkFluidVolume> { Volume(100f, 2), Volume(10f, 2), Volume(50f, 2), Volume(300f, 2) };
            InkFluidVolume.PickSimulating(volumes, true, Vector3.zero);
            Assert.That(InBudget(volumes), Is.EqualTo(new[] { false, true, true, false }));
        }

        // Every palm carries its own simulationBudget; the strictest one applies to all.
        [Test]
        public void TheSmallestBudgetApplies()
        {
            var volumes = new List<InkFluidVolume> { Volume(100f, 4), Volume(10f, 4), Volume(50f, 1) };
            Assert.That(InkFluidVolume.PickSimulating(volumes, true, Vector3.zero), Is.EqualTo(1));
            Assert.That(InBudget(volumes), Is.EqualTo(new[] { false, true, false }));
        }

        // No main camera (a server, a loading frame): the palms spawned first keep simulating.
        [Test]
        public void WithoutACameraTheOldestSimulate()
        {
            var volumes = new List<InkFluidVolume> { Volume(900f, 2), Volume(500f, 2), Volume(10f, 2) };
            InkFluidVolume.PickSimulating(volumes, false, Vector3.zero);
            Assert.That(InBudget(volumes), Is.EqualTo(new[] { true, true, false }));
        }

        // A palm that already simulates keeps its grids against a newcomer less than 20 % nearer, so two palms at the
        // edge of the budget do not swap every frame.
        [TestCase(85f, true)]
        [TestCase(75f, false)]
        public void ASimulatingVolumeKeepsItsGridsUntilANewcomerIsClearlyNearer(float newcomerDistance, bool keeps)
        {
            float simulating = InkFluidVolume.RankScore(100f * 100f, true);
            float newcomer = InkFluidVolume.RankScore(newcomerDistance * newcomerDistance, false);
            Assert.That(simulating < newcomer, Is.EqualTo(keeps));
        }

        // Without compute shaders a fully wired palm holds no grids, takes nothing from the pool and logs nothing.
        [Test]
        public void WithoutComputeAWiredVolumeHoldsNoGridsAndLogsNothing()
        {
            InkFluidVolume.ForceNoCompute = true;
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeAsset);
            Assert.That(compute, Is.Not.Null, ComputeAsset);
            int pooled = InkFluidVolume.PooledCount;

            InkFluidVolume volume = Volume(0f, 8);
            volume.simulateInEditMode = false;
            volume.compute = compute;
            volume.hand = volume.transform;
            volume.sdf = Track(new Texture3D(4, 4, 4, TextureFormat.RGBA32, false));
            volume.material = Track(new Material(Shader.Find("Hidden/Internal-Colored")));

            volume.Tick(1f / 30f);
            volume.SeedGather(12f, 60f, 0.18f);

            Assert.That(volume.IsSimulating, Is.False);
            Assert.That(InkFluidVolume.PooledCount, Is.EqualTo(pooled));
            LogAssert.NoUnexpectedReceived();
        }

        InkFluidVolume Volume(float distance, int budget)
        {
            var go = Track(new GameObject("Ink"));
            go.transform.position = new Vector3(0f, 0f, distance);
            var volume = go.AddComponent<InkFluidVolume>();
            volume.simulationBudget = budget;
            return volume;
        }

        T Track<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        static bool[] InBudget(List<InkFluidVolume> volumes)
        {
            return volumes.ConvertAll(v => v.InBudget).ToArray();
        }
    }
}
