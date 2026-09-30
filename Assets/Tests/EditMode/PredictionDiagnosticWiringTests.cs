using System.Reflection;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Debugging;
using NewBuddah.PredictionV2.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class PredictionDiagnosticWiringTests
    {
        [Test]
        public void HealthOnlyRefreshesForItsLogConsumerAndAtSampleIntervals()
        {
            var go = new GameObject("diagnostic sampling test");
            go.SetActive(false); // Avoid gameplay Awake/auto-added bridges.
            bool previousVerbose = NetDebug.EnableVerboseLog;
            try
            {
                NetDebug.EnableVerboseLog = false;
                var bootstrap = go.AddComponent<BuddahPredictionBootstrap>();
                go.AddComponent<BuddahPredictionDebugOverlay>(); // Mirror alone consumes no health strings.
                go.AddComponent<BuddahPredictionCompatibilityRegistry>();
                var health = go.AddComponent<BuddahPredictionRuntimeHealthReport>();
                bootstrap.DebugState.healthSummary = "sentinel";
                health.RefreshHealthReport();
                Assert.That(bootstrap.DebugState.healthSummary, Is.EqualTo("sentinel"));

                bootstrap.DebugSettings.enableVerboseLogs = true;
                health.RefreshHealthReport();
                Assert.That(bootstrap.DebugState.healthSummary, Is.EqualTo("Legacy main track active"));
                bootstrap.DebugState.healthSummary = "not due";
                health.RefreshHealthReport();
                Assert.That(bootstrap.DebugState.healthSummary, Is.EqualTo("not due"));

                typeof(BuddahPredictionRuntimeHealthReport).GetField("_lastHealthSampleTime",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, float.NegativeInfinity);
                health.RefreshHealthReport();
                Assert.That(bootstrap.DebugState.healthSummary, Is.EqualTo("Legacy main track active"));
            }
            finally
            {
                NetDebug.EnableVerboseLog = previousVerbose;
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DiagnosticComponentsCannotDrawEvenWithOldSerializedFlags()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(BuddahPredictionDebugOverlay).GetMethod("OnGUI", flags), Is.Null);
            Assert.That(typeof(ComboSkillInput).GetMethod("OnGUI", flags), Is.Null);
        }

        [Test]
        public void PrefabDisablesScreenFlagsAndKeepsConsoleMirror()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<BuddahPredictionBootstrap>().DebugSettings.enableOnScreenDebug, Is.False);
            var combo = new SerializedObject(prefab.GetComponent<ComboSkillInput>());
            Assert.That(combo.FindProperty("debugHud").boolValue, Is.False);
            var overlay = new SerializedObject(prefab.GetComponent<BuddahPredictionDebugOverlay>());
            Assert.That(overlay.FindProperty("mirrorSummaryToConsole").boolValue, Is.True);
        }
    }
}
