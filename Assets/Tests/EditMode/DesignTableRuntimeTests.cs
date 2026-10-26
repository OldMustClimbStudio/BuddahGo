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

        /// <summary>skillId of every Skill/Skill row, split by unlockState: hidden rows are never offered to players.</summary>
        private static void SkillRows(DesignTableSet set, out HashSet<string> offered, out HashSet<string> hidden)
        {
            DesignSheet s = set.Find("Skill", "Skill");
            Assert.IsNotNull(s, "Skill/Skill missing");
            ColumnSpec id = s.FindColumn("skillId"), unlock = s.FindColumn("unlockState");
            Assert.IsNotNull(id, "Skill.skillId missing");
            Assert.IsNotNull(unlock, "Skill.unlockState missing");
            offered = new HashSet<string>();
            hidden = new HashSet<string>();
            foreach (DataRow row in s.Rows)
            {
                string skillId = s.Cell(row, id).Trim();
                if (skillId.Length == 0) continue;
                bool isHidden = string.Equals(s.Cell(row, unlock).Trim(), "hidden", StringComparison.OrdinalIgnoreCase);
                (isHidden ? hidden : offered).Add(skillId);
            }
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
            AssertMap(T("MatchSpawnManager"), typeof(ModeRuleRecord), SteamMultiplayer.Network.Match.MatchSpawnManager.TrainingDummyModeRuleMap);
            AssertMap(typeof(YuanqiAction), typeof(YuanqiRecord), SkillDatabase.YuanqiRowMap);

            // Every YuanqiEffect<Pair> list of the database has a Yuanqi_* class that owns all of its columns (same-name Apply).
            int families = 0;
            foreach (FieldInfo list in typeof(ProjectConfigDatabase).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!list.FieldType.IsGenericType || list.FieldType.GetGenericTypeDefinition() != typeof(List<>))
                    continue;
                Type record = list.FieldType.GetGenericArguments()[0];
                if (!record.Name.StartsWith("YuanqiEffect", StringComparison.Ordinal))
                    continue;
                Type action = SkillDatabase.YuanqiFamilyActionType(record);
                Assert.IsNotNull(action, record.Name + " has no Yuanqi_* class in SkillDatabase.YuanqiFamilyActionType");
                Assert.IsTrue(typeof(YuanqiAction).IsAssignableFrom(action), action.Name);
                AssertMap(action, record, Fields(record));
                families++;
            }
            Assert.GreaterOrEqual(families, 2, "YuanqiEffectFrostLock and YuanqiEffectGiantCharge");
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
                Assert.IsTrue(skills.TryGetObsessionGain(row.skillId, out float gain), row.skillId);
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", row.skillId, "obsessionGain"), gain, row.skillId + ".obsessionGain");
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
                foreach (SkillAction action in source.skills) { if (action == null) continue; SkillAction c = UnityEngine.Object.Instantiate(action); temp.Add(c); scratch.skills.Add(c); copies.Add(c); }
                // 缘起 copies start from values no table cell holds, so every mapped field must come from the tables.
                List<YuanqiAction> pairs = new List<YuanqiAction>();
                foreach (YuanqiAction pair in source.yuanqis)
                {
                    if (pair == null) continue;
                    YuanqiAction c = UnityEngine.Object.Instantiate(pair);
                    c.skillA = c.skillB = "probe";
                    c.consumeA = c.consumeB = true;
                    c.isEnabled = false;
                    c.mode = c.mode == YuanqiMode.Instant ? YuanqiMode.Sustained : YuanqiMode.Instant;
                    if (c is Yuanqi_FrostLock frostCopy) { frostCopy.freezeSeconds = 999f; frostCopy.radiusMeters = 999f; frostCopy.affectsTrainingDummies = false; }
                    if (c is Yuanqi_GiantCharge chargeCopy) { chargeCopy.burstExtraMaxSpeed = 999f; chargeCopy.burstSeconds = 999f; chargeCopy.impactImpulseMultiplier = 999f; }
                    temp.Add(c); scratch.yuanqis.Add(c); pairs.Add(c);
                }
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
                Assert.GreaterOrEqual(checked_, 6, "primary durations applied (one per normal skill)");

                Skill_Giant giant = (Skill_Giant)copies.Find(c => c.skillId == "giant");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectGiant", "skillId", "giant", "scaleMultiplier"), giant.scaleMultiplier, "family sheet reaches the SO");
                Skill_Acceleration acceleration = (Skill_Acceleration)copies.Find(c => c.skillId == "acceleration");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectAcceleration", "skillId", "acceleration", "extraForwardForce"), acceleration.extraForwardForce);
                Skill_PushProjectileHands hands = (Skill_PushProjectileHands)copies.Find(c => c.skillId == "push_projectile_hands");
                Assert.AreEqual(Cell(set, "Skill", "Push", "pushId", "charged", "speed"), hands.projectileSpeed, "Push charged row reaches the SO");
                Assert.AreEqual(Cell(set, "Skill", "Push", "pushId", "charged", "windupSeconds"), hands.buildUpSeconds);

                DesignSheet yuanqiSheet = set.Find("Skill", "Yuanqi");
                Assert.IsNotNull(yuanqiSheet, "Skill/Yuanqi missing");
                Assert.AreEqual(yuanqiSheet.Rows.Count, pairs.Count, "one 缘起 asset per Skill/Yuanqi row");
                foreach (YuanqiAction pair in pairs)
                {
                    string id = pair.yuanqiId;
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "skillA"), pair.skillA, id + ".skillA");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "skillB"), pair.skillB, id + ".skillB");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "consumeA") == "true", pair.consumeA, id + ".consumeA");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "consumeB") == "true", pair.consumeB, id + ".consumeB");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "mode"), pair.mode.ToString(), id + ".mode");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "retriggerPolicy"), pair.retriggerPolicy.ToString(), id + ".retriggerPolicy");
                    Assert.AreEqual(Text(set, "Skill", "Yuanqi", "yuanqiId", id, "isEnabled") == "true", pair.isEnabled, id + ".isEnabled");
                }
                Yuanqi_FrostLock frost = (Yuanqi_FrostLock)pairs.Find(p => p.yuanqiId == "frost_lock");
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectFrostLock", "yuanqiId", "frost_lock", "freezeSeconds"), frost.freezeSeconds, "family sheet reaches the 缘起 SO");
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectFrostLock", "yuanqiId", "frost_lock", "radiusMeters"), frost.radiusMeters);
                Assert.AreEqual(Text(set, "Skill", "YuanqiEffectFrostLock", "yuanqiId", "frost_lock", "affectsTrainingDummies") == "true", frost.affectsTrainingDummies);
                Yuanqi_GiantCharge charge = (Yuanqi_GiantCharge)pairs.Find(p => p.yuanqiId == "giant_charge");
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectGiantCharge", "yuanqiId", "giant_charge", "burstExtraMaxSpeed"), charge.burstExtraMaxSpeed);
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectGiantCharge", "yuanqiId", "giant_charge", "burstSeconds"), charge.burstSeconds);
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectGiantCharge", "yuanqiId", "giant_charge", "impactImpulseMultiplier"), charge.impactImpulseMultiplier);

                // Text: the row's displayNameKey through UI/Text.
                Assert.AreEqual(Text(set, "UI", "Text", "key", "yuanqi.frost_lock.name", "zh"), scratch.GetYuanqiDisplayName("frost_lock"), "缘起 name from UI/Text");
                Assert.AreEqual(Text(set, "UI", "Text", "key", "yuanqi.frost_lock.desc", "zh"), scratch.GetYuanqiDescription("frost_lock"), "缘起 description from UI/Text");
            }
            finally
            {
                foreach (UnityEngine.Object o in temp)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        /// <summary>Raw (trimmed) cell text of the row whose key column equals keyValue.</summary>
        private static string Text(DesignTableSet set, string workbook, string sheet, string keyVar, string keyValue, string var)
        {
            DesignSheet s = set.Find(workbook, sheet);
            Assert.IsNotNull(s, workbook + "/" + sheet + " missing");
            ColumnSpec key = s.FindColumn(keyVar), column = s.FindColumn(var);
            Assert.IsNotNull(column, sheet + "." + var + " missing");
            foreach (DataRow row in s.Rows)
                if (s.Cell(row, key).Trim() == keyValue)
                    return s.Cell(row, column).Trim();
            Assert.Fail("row '" + keyValue + "' not found in " + workbook + "/" + sheet);
            return string.Empty;
        }

        [Test]
        public void EveryYuanqiRowHasAnAssetOfItsFamily()
        {
            // The table rule cannot see assets: each Skill/Yuanqi row needs a 缘起 asset in the Skill Database, and every
            // YuanqiEffect<Pair> row must target an asset of the class SkillDatabase.YuanqiFamilyActionType names.
            DesignTableSet set = DesignTableSet.Load();
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);
            Dictionary<string, YuanqiAction> assets = new Dictionary<string, YuanqiAction>();
            foreach (YuanqiAction pair in database.yuanqis)
                if (pair != null) assets[pair.yuanqiId] = pair;

            DesignSheet yuanqi = set.Find("Skill", "Yuanqi");
            Assert.IsNotNull(yuanqi, "Skill/Yuanqi missing");
            ColumnSpec id = yuanqi.FindColumn("yuanqiId");
            foreach (DataRow row in yuanqi.Rows)
                Assert.IsTrue(assets.ContainsKey(yuanqi.Cell(row, id).Trim()), "Skill/Yuanqi row '" + yuanqi.Cell(row, id) + "' has no asset in " + SkillDatabaseAsset);

            int checkedRows = 0;
            foreach (KeyValuePair<string, DesignTableSet.ExportTarget> target in set.ExportTargets)
            {
                if (target.Value.Workbook != "Skill" || !target.Value.Sheet.StartsWith("YuanqiEffect", StringComparison.Ordinal))
                    continue;
                Type expected = SkillDatabase.YuanqiFamilyActionType(T(target.Value.RecordType));
                Assert.IsNotNull(expected, target.Key + " has no Yuanqi_* class");
                DesignSheet family = set.Find("Skill", target.Value.Sheet);
                ColumnSpec familyId = family.FindColumn("yuanqiId");
                foreach (DataRow row in family.Rows)
                {
                    string rowId = family.Cell(row, familyId).Trim();
                    Assert.IsTrue(assets.TryGetValue(rowId, out YuanqiAction asset), target.Key + " row '" + rowId + "' has no asset");
                    Assert.IsInstanceOf(expected, asset, target.Key + " row '" + rowId + "' targets " + asset.GetType().Name);
                    checkedRows++;
                }
            }
            Assert.GreaterOrEqual(checkedRows, 2, "frost_lock and giant_charge family rows");
        }

        [Test]
        public void YuanqiTextFallsBackToTheAssetName()
        {
            SkillDatabase scratch = ScriptableObject.CreateInstance<SkillDatabase>();
            Yuanqi_GiantPalm probe = ScriptableObject.CreateInstance<Yuanqi_GiantPalm>();
            try
            {
                probe.yuanqiId = "probe_pair_without_row";
                probe.displayName = "Probe Pair";
                scratch.yuanqis.Add(probe);
                scratch.ApplyDesignTablesNow();
                Assert.AreEqual("Probe Pair", scratch.GetYuanqiDisplayName(" probe_pair_without_row "), "no row and no text key: the SO displayName");
                Assert.AreEqual("unknown_pair", scratch.GetYuanqiDisplayName("unknown_pair"), "no asset either: the id");
                Assert.AreEqual(string.Empty, scratch.GetYuanqiDisplayName(null));
                Assert.AreEqual("fallback", scratch.GetYuanqiDescription("probe_pair_without_row", "fallback"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
                UnityEngine.Object.DestroyImmediate(scratch);
            }
        }

        [Test]
        public void SkillDatabaseHoldsOnlyTheNormalSkills()
        {
            // A4: a backfire now fails the requested cast, so the six *_anti SkillActions are retired, and phase B deleted their
            // rows. Any hidden row added later must not resolve to an action and must never be offered for selection.
            ProjectConfigDatabase main = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            Assert.IsNotNull(main, MainAsset);
            Assert.IsTrue(ProjectConfigRuntime.EnsureInitialized(main));
            SkillRows(DesignTableSet.Load(), out HashSet<string> offered, out HashSet<string> hidden);
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);

            foreach (string id in offered)
                Assert.IsTrue(database.TryGet(id, out SkillAction action) && action != null, "Skill row '" + id + "' has no SkillAction");
            foreach (string id in hidden)
                Assert.IsFalse(database.TryGet(id, out _), "hidden Skill row '" + id + "' still resolves to a SkillAction");
            CollectionAssert.AreEquivalent(offered, database.GetSelectableNormalSkills().ConvertAll(s => s.skillId), "selection offers exactly the non-hidden Skill rows");

            Assert.IsNull(typeof(SkillAction).GetField("antiSkillId"), "SkillAction.antiSkillId is retired");
            Assert.IsNull(typeof(SkillAction).GetField("castLockSeconds"), "SkillAction.castLockSeconds is retired (no global cast lock)");
            Assert.IsNull(typeof(SkillDatabase).GetField("antiSkills"), "SkillDatabase.antiSkills is retired");
        }

        [Test]
        public void SkillCrossValidatorIgnoresHiddenRowsAndResolvesNoBackfireVariant()
        {
            ProjectConfigDatabase main = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            Assert.IsNotNull(main, MainAsset);
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);
            using (var serializedDatabase = new SerializedObject(database))
                Assert.AreSame(main, serializedDatabase.FindProperty("_projectConfigDatabase").objectReferenceValue, "the validator must compare this skill database");
            SkillRows(DesignTableSet.Load(), out _, out HashSet<string> hidden);

            foreach (ProjectConfigValidationMessage message in SkillConfigCrossValidator.Validate(main))
            {
                StringAssert.DoesNotStartWith("Anti resolution", message.message, message.context);
                if (message.severity != ProjectConfigValidationSeverity.Warning)
                    continue;
                foreach (string id in hidden)
                    StringAssert.DoesNotContain("'" + id + "'", message.message, "warning about hidden Skill row '" + id + "'");
            }
        }

        [Test]
        public void SkillTablesApplyWhenTheLookupWasBuiltBeforePlayMode()
        {
            // Editor Play Mode: the SkillDatabase OnEnable (domain reload) runs while Application.isPlaying is still false,
            // and the first gameplay lookup happens once it is true. The durations must still come from the table.
            ProjectConfigDatabase main = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            Assert.IsTrue(ProjectConfigRuntime.EnsureInitialized(main));
            DesignTableSet set = DesignTableSet.Load();
            SkillDatabase source = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Skill_Acceleration original = (Skill_Acceleration)source.normalSkills.Find(s => s != null && s.skillId == "acceleration");
            Assert.IsNotNull(original);
            Skill_Acceleration copy = UnityEngine.Object.Instantiate(original);
            copy.durationSeconds = 999f;
            SkillDatabase scratch = ScriptableObject.CreateInstance<SkillDatabase>();
            Func<bool> previous = SkillDatabase.IsPlaying;
            try
            {
                SkillDatabase.IsPlaying = () => false;
                scratch.normalSkills.Add(copy);
                // What the domain reload does on entering Play Mode: OnEnable → RebuildLookup while isPlaying is false.
                typeof(SkillDatabase).GetMethod("RebuildLookup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scratch, null);
                Assert.IsTrue(scratch.TryGet("acceleration", out _), "lookup built in edit state");
                Assert.AreEqual(999f, copy.durationSeconds, "nothing is applied outside play");
                SkillDatabase.IsPlaying = () => true;
                Assert.IsTrue(scratch.TryGet("acceleration", out SkillAction resolved));
                Assert.AreSame(copy, resolved);
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", "acceleration", "effectDurationSeconds"), copy.durationSeconds,
                    "Skill.effectDurationSeconds must reach the SO on the first lookup in play");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectAcceleration", "skillId", "acceleration", "extraForwardForce"), copy.extraForwardForce);
            }
            finally
            {
                SkillDatabase.IsPlaying = previous;
                UnityEngine.Object.DestroyImmediate(scratch);
                UnityEngine.Object.DestroyImmediate(copy);
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
