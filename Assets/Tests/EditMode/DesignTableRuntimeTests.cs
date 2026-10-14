using System;
using System.Collections.Generic;
using System.Reflection;
using BuddahGo.DesignTables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    /// <summary>
    /// The runtime half of the design-table channel: every explicit field mapping resolves on its component, the imported
    /// database reaches the repositories and the skill assets, and the save-in-Excel callback queues the right workbook.
    /// Values are always compared with the sheet cell, never with literals, so balance edits cannot break these tests.
    /// </summary>
    public class DesignTableRuntimeTests
    {
        private const string MainAsset = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";
        private const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";

        private static float Cell(DesignTableSet set, string workbook, string sheet, string keyVar, string keyValue, string var)
        {
            DesignSheet s = set.Find(workbook, sheet);
            Assert.IsNotNull(s, workbook + "/" + sheet + " missing");
            ColumnSpec key = s.FindColumn(keyVar), column = s.FindColumn(var);
            Assert.IsNotNull(column, sheet + "." + var + " missing");
            foreach (DataRow row in s.Rows)
                if (s.Cell(row, key).Trim() == keyValue)
                    return ValueConverter.ToFloat(s.Cell(row, column));
            Assert.Fail("row '" + keyValue + "' not found in " + workbook + "/" + sheet);
            return 0f;
        }

        private static Type T(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                foreach (Type t in asm.GetTypes())
                    if (t.Name == name)
                        return t;
            Assert.Fail("type " + name + " not found");
            return null;
        }

        private static string[] Fields(Type record, params string[] except)
        {
            List<string> names = new List<string>();
            foreach (FieldInfo f in record.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (Array.IndexOf(except, f.Name) < 0)
                    names.Add(f.Name);
            return names.ToArray();
        }

        private static void AssertMap(Type target, Type record, params string[] map)
        {
            foreach (string entry in map)
            {
                int eq = entry.IndexOf('=');
                string source = eq > 0 ? entry.Substring(0, eq) : entry;
                string destination = eq > 0 ? entry.Substring(eq + 1) : entry;
                Assert.IsNotNull(record.GetField(source, BindingFlags.Public | BindingFlags.Instance), record.Name + " has no column field " + source);
                Assert.IsNotNull(DesignTableOverlay.FindField(target, destination), target.Name + " has no field '" + destination + "' for " + record.Name + "." + source);
            }
        }

        [Test]
        public void EveryMappedColumnHasAFieldOnItsConsumer()
        {
            AssertMap(T("BuddahHandControl"), typeof(PushRecord), BuddahHandControl.PushBasicMap);
            AssertMap(T("Skill_PushProjectileHands"), typeof(PushRecord), SkillDatabase.PushChargedMap);
            AssertMap(T("Skill_PushProjectileHands_Anti"), typeof(PushRecord), SkillDatabase.PushChargedAntiMap);
            AssertMap(T("BuddahMovement"), typeof(MovementRecord), "launchInheritSeconds=launchInheritTime", "launchBlendSeconds=launchBlendTime");
            AssertMap(T("RaceBodyIntroStateController"), typeof(IntroRecord), "handoffLeadSeconds=defaultHandoffLeadTime");
            AssertMap(T("RaceBodyIntroStateController"), typeof(MovementRecord), "velocitySampleDeltaSeconds");
            AssertMap(T("BuddahPredictedMotorConfig"), typeof(MovementRecord), "forwardForce", "maxSpeed", "turnTorque", "turnDecayPerSecond", "turnInputMultiplier",
                "pushGraceSeconds", "pushExtraMaxSpeed", "handoffGroundSnapMaxDistance", "handoffGroundProbeHeight");
            AssertMap(T("LapProgress"), typeof(TrackRecord), "crossCooldownSeconds=minimumCrossingCooldownSeconds", "maxProgressJumpWithoutCheckpoint01=maximumProgressJumpWithoutCheckpoint01",
                "minForwardDot", "armDistanceMeters=minimumWorldDistanceFromStartToArmLap", "useSequentialCheckpoints", "autoAdvanceSplineCheckpoints", "requiredCheckpointCount",
                "fallbackCheckpointProgresses01", "startPointTag");
            AssertMap(T("BuddahRespawn"), typeof(RespawnRecord), "contactSeconds=continuousContactRespawnSeconds", "cooldownSeconds=postRespawnCooldownSeconds", "heightOffset=respawnHeightOffset",
                "clearanceRadius=respawnClearanceCheckRadius", "liftStep=additionalRespawnLiftStep", "maxLift=maxAdditionalRespawnLift", "floorTag=respawnFloorTag");
            AssertMap(T("ObsessionFigure"), typeof(ObsessionRecord), Fields(typeof(ObsessionRecord), "profileId"));
            AssertMap(T("RaceCompletionTracker"), typeof(TrackWrongWayRecord), Fields(typeof(TrackWrongWayRecord), "trackId"));
            AssertMap(T("IntroSequenceManager"), typeof(IntroRecord), "fallbackIntroDurationSeconds=fallbackIntroDuration", "introSpeedMetersPerSecond", "handoffLeadSeconds",
                "introLeadInSeconds", "randomizeSlots", "deterministicShuffleSeed");
            AssertMap(T("MiniMapController"), typeof(TrackRecord), "minimapWorldMinX=worldMinX", "minimapWorldMaxX=worldMaxX", "minimapWorldMinZ=worldMinZ", "minimapWorldMaxZ=worldMaxZ");
            AssertMap(T("RaceResultAreaManager"), typeof(TrackRecord), "resultOverflowOffset=overflowOffset", "missingPlayerOffset");
            AssertMap(T("TrackSplineRef"), typeof(TrackRecord), "trackSplineSamples=samples");
            AssertMap(T("AIDifficultyProfile"), typeof(DrivingTierRecord), Fields(typeof(DrivingTierRecord), "difficulty", "rolloutTail"));
            AssertMap(T("AISkillDifficulty"), typeof(SkillDifficultyRecord), Fields(typeof(SkillDifficultyRecord)));
            AssertMap(T("AISkillTuning"), typeof(SkillTuningRecord), Fields(typeof(SkillTuningRecord), "profileId"));
            AssertMap(T("ComboSkillInput"), typeof(ComboInputRecord), "inputWindowSeconds=stepWindowSeconds", "maxBuffer");
        }

        [Test]
        public void ImportedDatabaseReachesRepositoriesAndSkillAssets()
        {
            ProjectConfigDatabase main = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            Assert.IsNotNull(main, MainAsset);
            Assert.IsTrue(ProjectConfigRuntime.EnsureInitialized(main));
            DesignTableSet set = DesignTableSet.Load();

            Assert.IsTrue(ProjectConfigRuntime.TryGetSkillConfigRepository(out SkillConfigRepository skills));
            foreach (SkillRecord row in main.skills)
            {
                Assert.IsTrue(skills.TryGetCooldown(row.skillId, out float cooldown), row.skillId);
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", row.skillId, "cooldownSeconds"), cooldown, row.skillId + ".cooldownSeconds");
                Assert.IsTrue(skills.TryGetCastLock(row.skillId, out float castLock));
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", row.skillId, "castLockSeconds"), castLock, row.skillId + ".castLockSeconds");
            }
            Assert.AreEqual(Cell(set, "Skill", "ComboInput", "profileId", "default", "inputWindowSeconds"), DesignTableOverlay.Single(main.comboInputs, null).inputWindowSeconds);

            // Skill SOs: apply onto copies so the assets on disk stay untouched.
            SkillDatabase source = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(source, SkillDatabaseAsset);
            List<UnityEngine.Object> temp = new List<UnityEngine.Object>();
            SkillDatabase scratch = ScriptableObject.CreateInstance<SkillDatabase>();
            temp.Add(scratch);
            try
            {
                List<SkillAction> copies = new List<SkillAction>();
                foreach (SkillAction action in source.normalSkills) { if (action == null) continue; SkillAction c = UnityEngine.Object.Instantiate(action); temp.Add(c); scratch.normalSkills.Add(c); copies.Add(c); }
                foreach (SkillAction action in source.antiSkills) { if (action == null) continue; SkillAction c = UnityEngine.Object.Instantiate(action); temp.Add(c); scratch.antiSkills.Add(c); copies.Add(c); }
                foreach (SkillAction action in source.skills) { if (action == null) continue; SkillAction c = UnityEngine.Object.Instantiate(action); temp.Add(c); scratch.skills.Add(c); copies.Add(c); }
                scratch.ApplyDesignTablesNow();

                int checked_ = 0;
                foreach (SkillAction copy in copies)
                {
                    string field = SkillDatabase.EffectDurationField(copy);
                    if (field == null) continue;
                    float expected = Cell(set, "Skill", "Skill", "skillId", copy.skillId, "effectDurationSeconds");
                    if (expected <= 0f) continue;
                    FieldInfo f = DesignTableOverlay.FindField(copy.GetType(), field);
                    Assert.IsNotNull(f, copy.GetType().Name + "." + field);
                    Assert.AreEqual(expected, (float)f.GetValue(copy), copy.skillId + "." + field + " did not receive Skill.effectDurationSeconds");
                    checked_++;
                }
                Assert.GreaterOrEqual(checked_, 10, "primary durations applied");

                Skill_Giant giant = (Skill_Giant)copies.Find(c => c.skillId == "giant");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectGiant", "skillId", "giant", "scaleMultiplier"), giant.scaleMultiplier, "family sheet reaches the SO");
                Skill_Acceleration acceleration = (Skill_Acceleration)copies.Find(c => c.skillId == "acceleration");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectAcceleration", "skillId", "acceleration", "extraForwardForce"), acceleration.extraForwardForce);
                Skill_PushProjectileHands hands = (Skill_PushProjectileHands)copies.Find(c => c.skillId == "push_projectile_hands");
                Assert.AreEqual(Cell(set, "Skill", "Push", "pushId", "charged", "speed"), hands.projectileSpeed, "Push charged row reaches the SO");
                Assert.AreEqual(Cell(set, "Skill", "Push", "pushId", "charged", "windupSeconds"), hands.buildUpSeconds);
                Skill_PushProjectileHands_Anti burst = (Skill_PushProjectileHands_Anti)copies.Find(c => c.skillId == "push_projectile_hands_anti");
                Assert.AreEqual((int)Cell(set, "Skill", "Push", "pushId", "charged_anti", "projectileCount"), burst.projectileCount);
            }
            finally
            {
                foreach (UnityEngine.Object o in temp)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        [Test]
        public void SavingAWorkbookQueuesItAndTheImportRemapsOnlyDependentSheets()
        {
            DesignTablePostprocessor.AllowInBatchMode = true;
            try
            {
                Assert.IsFalse(DesignTablePostprocessor.Schedule(new[] { DesignTableSet.TablesRoot + "/Skill/~$Skill.xlsx", "Assets/Other.xlsx" }), "lock files and foreign xlsx are ignored");
                Assert.IsTrue(DesignTablePostprocessor.Schedule(new[] { DesignTableSet.TablesRoot + "/Skill/Skill.xlsx" }));
                Assert.IsTrue(DesignTablePostprocessor.PendingWorkbooks.Contains("Skill"));
                DesignTableExporter.Result result = DesignTablePostprocessor.RunPendingNow(dryRun: true);
                try
                {
                    Assert.IsTrue(result.Success, string.Join("\n", result.Issues.ConvertAll(i => i.ToString())));
                    Assert.IsTrue(result.ChangedWorkbooks.Contains("Skill"), "the saved workbook counts as changed even when its hash is current");
                    Assert.IsTrue(result.RemappedSheets.Contains("Skill/Skill"));
                    Assert.IsTrue(result.RemappedSheets.Contains("AI/Personality"), "Personality references Skill.skillId");
                    // Hash detection may add workbooks edited while Unity was closed; the remap set must equal the dependency closure of those.
                    HashSet<string> expected = DesignTableExporter.ComputeRemapSet(DesignTableSet.Load(), result.ChangedWorkbooks, null);
                    CollectionAssert.AreEquivalent(expected, result.RemappedSheets);
                    if (!result.ChangedWorkbooks.Contains("Movement"))
                        Assert.IsFalse(result.RemappedSheets.Contains("Movement/Movement"), "unrelated workbooks are kept");
                    Assert.AreEqual(0, DesignTablePostprocessor.PendingWorkbooks.Count);
                }
                finally
                {
                    if (result.Database != null) UnityEngine.Object.DestroyImmediate(result.Database);
                }
            }
            finally
            {
                DesignTablePostprocessor.AllowInBatchMode = false;
            }
        }
    }
}
