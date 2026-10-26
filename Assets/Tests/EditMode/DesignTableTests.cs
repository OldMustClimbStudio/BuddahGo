using System.Collections.Generic;
using System.IO;
using BuddahGo.DesignTables;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    /// <summary>Design-table pipeline: xlsx reading, header grammar, validation and the dry-run export of the real workbooks.</summary>
    public class DesignTableTests
    {
        private const string SkillWorkbook = DesignTableSet.TablesRoot + "/Skill/Skill.xlsx";

        /// <summary>Numeric cell of the row whose key column equals keyValue; keeps the tests free of design numbers.</summary>
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

        [Test]
        public void XlsxReader_ReadsSheetsAndHeaderMarkers()
        {
            Assert.IsTrue(File.Exists(SkillWorkbook), SkillWorkbook + " missing");
            List<XlsxReader.Sheet> sheets = XlsxReader.Read(SkillWorkbook);
            XlsxReader.Sheet balance = sheets.Find(s => s.Name == "Skill");
            Assert.IsNotNull(balance, "Skill sheet");
            Assert.AreEqual("##var", balance.Rows[0][0]);
            Assert.AreEqual("##type", balance.Rows[1][0]);
            Assert.AreEqual("skillId", balance.Rows[0][1]);
            Assert.AreEqual(1, XlsxReader.ColumnIndex("B7"));
            Assert.AreEqual(26, XlsxReader.ColumnIndex("AA3"));
            Assert.AreEqual("AA", XlsxReader.ColumnName(26));
        }

        [Test]
        public void TypeSpec_ParsesTagsAndLists()
        {
            TypeSpec key = TypeSpec.Parse("string#key#ref=Skill.skillId");
            Assert.AreEqual(ColumnBaseType.String, key.BaseType);
            Assert.IsTrue(key.IsKey);
            Assert.AreEqual("Skill", key.RefSheet);
            Assert.AreEqual("skillId", key.RefField);

            TypeSpec cross = TypeSpec.Parse("string#ref=UI/Text.key");
            Assert.AreEqual("UI", cross.RefWorkbook);
            Assert.AreEqual("Text", cross.RefSheet);

            TypeSpec list = TypeSpec.Parse("list,enum:ComboToken#sep=,");
            Assert.AreEqual(ColumnBaseType.List, list.BaseType);
            Assert.AreEqual(",", list.ListSeparator);
            Assert.AreEqual(ColumnBaseType.Enum, list.Element.BaseType);
            Assert.AreEqual("ComboToken", list.Element.EnumName);

            TypeSpec range = TypeSpec.Parse("float#range=0,1.5");
            Assert.IsTrue(range.HasRange);
            Assert.AreEqual(1.5f, range.RangeMax);
        }

        [Test]
        public void ValueConverter_RoundTripsCurvesAndVectors()
        {
            Assert.IsTrue(ValueConverter.TryCurve("0,0,2,2|1,1,0,0", out AnimationCurve curve));
            Assert.AreEqual(2, curve.length);
            Assert.AreEqual(2f, curve[0].outTangent);
            Assert.AreEqual("0,0,2,2|1,1,0,0", ValueConverter.CurveToString(curve));
            Assert.AreEqual(new Vector3(1, -2.5f, 3), ValueConverter.ToVector3("1,-2.5,3"));
            Assert.IsTrue(ValueConverter.TryBool("是", out bool yes) && yes);
            Assert.IsFalse(ValueConverter.TryBool("maybe", out _));
        }

        /// <summary>skillId-like key cells of a sheet (first key column), trimmed.</summary>
        private static List<string> Keys(DesignTableSet set, string workbook, string sheet, string keyVar)
        {
            DesignSheet s = set.Find(workbook, sheet);
            Assert.IsNotNull(s, workbook + "/" + sheet + " missing");
            ColumnSpec key = s.FindColumn(keyVar);
            Assert.IsNotNull(key, sheet + "." + keyVar + " missing");
            List<string> keys = new List<string>();
            foreach (DataRow row in s.Rows)
                keys.Add(s.Cell(row, key).Trim());
            return keys;
        }

        [Test]
        public void DesignTableSet_LoadsAllWorkbooksWithoutLoadErrors()
        {
            DesignTableSet set = DesignTableSet.Load();
            Assert.AreEqual(0, set.LoadErrors.Count, string.Join("\n", set.LoadErrors));
            Assert.IsNotNull(set.Find("Skill", "Skill"));
            Assert.Greater(set.Find("Skill", "Skill").Rows.Count, 0);
            Assert.IsTrue(set.Enums.ContainsKey("SoloDifficulty"));
            Assert.IsTrue(set.Enums.ContainsKey("SkillId"), "SkillId registry");
            Assert.IsTrue(set.ExportTargets.ContainsKey("Skill/Skill"));
            Assert.IsFalse(set.ExportTargets.ContainsKey("Skill/SkillConstant"), "code constants are not exported");
            Assert.IsTrue(set.ExportTargets.ContainsKey("UI/Text"));
            Assert.IsFalse(set.ExportTargets.ContainsKey("Skill/SkillPresentation"), "presentation sheets are not exported");

            // Phase B (mechanics update): 缘起 sheets and enums, unified backfire (no *_anti rows, no cast lock / anti id columns).
            foreach (string sheet in new[] { "Yuanqi", "YuanqiEffectFrostLock", "YuanqiEffectGiantCharge" })
                Assert.IsTrue(set.ExportTargets.ContainsKey("Skill/" + sheet), "Skill/" + sheet + " is exported");
            foreach (string enumName in new[] { "YuanqiId", "YuanqiMode", "YuanqiRetrigger" })
                Assert.IsTrue(set.Enums.ContainsKey(enumName), enumName + " registry");
            foreach (string command in new[] { "Freeze", "EndModifier", "Burst", "MaxSpeedMultiplier" })
                Assert.IsTrue(set.Enums["ModifierCommandType"].Contains(command), "ModifierCommandType." + command);
            Assert.IsNull(set.Find("Skill", "Skill").FindColumn("antiSkillId"), "antiSkillId retired");
            Assert.IsNull(set.Find("Skill", "Skill").FindColumn("castLockSeconds"), "castLockSeconds retired");
            foreach (string[] sheet in new[] { new[] { "Skill", "skillId" }, new[] { "SkillEffectAcceleration", "skillId" }, new[] { "SkillEffectSlowTrap", "skillId" },
                         new[] { "SkillEffectBlackCurtain", "skillId" }, new[] { "SkillEffectGiant", "skillId" }, new[] { "SkillPresentation", "skillId" },
                         new[] { "SkillSelectionRule", "skillId" }, new[] { "SkillConstant", "ownerId" }, new[] { "Push", "pushId" } })
                foreach (string id in Keys(set, "Skill", sheet[0], sheet[1]))
                    Assert.IsFalse(id.EndsWith("_anti"), "retired anti row '" + id + "' in Skill/" + sheet[0]);
            foreach (string column in new[] { "repeatWindowSeconds", "repeatGainMultiplier", "backfireLockSeconds", "backfireMaxSpeedMultiplier", "backfireEndKeepRatio" })
                Assert.IsNotNull(set.Find("Skill", "Obsession").FindColumn(column), "Obsession." + column);
            Assert.IsNotNull(set.Find("Match", "ModeRule").FindColumn("trainingDummyCount"), "training dummies live on ModeRule");
        }

        // ---- Domain rules on in-memory sheets -------------------------------------------------------------------------

        private static string[] Row(string marker, string[] cells)
        {
            string[] row = new string[cells.Length + 1];
            row[0] = marker;
            for (int i = 0; i < cells.Length; i++)
                row[i + 1] = cells[i] ?? string.Empty;
            return row;
        }

        /// <summary>A parsed sheet from header vars/types and data rows (data starts at Excel row 5, column B is the first var).</summary>
        private static DesignSheet Probe(string workbook, string name, string[] vars, string[] types, params string[][] rows)
        {
            XlsxReader.Sheet raw = new XlsxReader.Sheet { Name = name, ColumnCount = vars.Length + 1 };
            raw.Rows.Add(Row("##var", vars));
            raw.Rows.Add(Row("##type", types));
            raw.Rows.Add(Row("##group", new string[vars.Length]));
            raw.Rows.Add(Row("##comment", new string[vars.Length]));
            foreach (string[] r in rows)
                raw.Rows.Add(Row(string.Empty, r));
            return DesignSheet.Parse(workbook, "memory", raw);
        }

        private static List<Issue> DomainIssues(params DesignSheet[] sheets)
        {
            DesignTableSet set = new DesignTableSet();
            set.Sheets.AddRange(sheets);
            List<Issue> issues = new List<Issue>();
            DesignTableDomainRules.Validate(set, issues);
            return issues;
        }

        private static bool HasIssue(List<Issue> issues, IssueSeverity severity, string location)
        {
            return issues.Exists(i => i.Severity == severity && i.Location == location);
        }

        private static readonly string[] YuanqiVars = { "yuanqiId", "skillA", "skillB", "consumeA", "consumeB", "mode", "retriggerPolicy", "isEnabled" };
        private static readonly string[] YuanqiTypes = { "enum:YuanqiId#key", "enum:SkillId#ref=Skill.skillId", "enum:SkillId#ref=Skill.skillId", "bool", "bool", "enum:YuanqiMode", "enum:YuanqiRetrigger", "bool" };

        [Test]
        public void DomainRules_RejectInvalidYuanqiRows()
        {
            DesignSheet yuanqi = Probe("Skill", "Yuanqi", YuanqiVars, YuanqiTypes,
                new[] { "ok", "acceleration", "slowtrap", "true", "true", "Instant", "OncePerOverlap", "true" },
                new[] { "self", "giant", "giant", "false", "false", "Instant", "OncePerOverlap", "true" },
                new[] { "dup", "slowtrap", "acceleration", "false", "false", "Instant", "OncePerOverlap", "true" },
                new[] { "held", "giant", "push_projectile_hands", "true", "false", "Sustained", "OncePerOverlap", "true" },
                new[] { "blank", "blackcurtain", "reverseturn", "", "false", "", "", "" },
                new[] { "noskilla", "", "giant", "false", "false", "Instant", "OncePerOverlap", "true" },
                new[] { "noskillb", "reverseturn", "", "false", "false", "Instant", "OncePerOverlap", "true" },
                new[] { "numeric", "slowtrap", "giant", "true", "false", "1", "OncePerOverlap", "true" },
                new[] { "numericok", "blackcurtain", "giant", "true", "false", "0", "OncePerOverlap", "true" });
            List<Issue> issues = DomainIssues(yuanqi);
            string all = string.Join("\n", issues);

            Assert.IsFalse(issues.Exists(i => i.Location.StartsWith("Skill.xlsx/Yuanqi!") && i.Location.EndsWith("5")), "valid row has no issue:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!D6"), "skillB equals skillA:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!B7"), "same unordered pair twice:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!E8"), "Sustained pair consumes skillA:\n" + all);
            Assert.IsFalse(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!F8"), "consumeB false is fine:\n" + all);
            foreach (string cell in new[] { "E9", "G9", "H9", "I9" })
                Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!" + cell), "blank " + cell + " (bool reads false, enum keeps the SO value):\n" + all);
            Assert.IsFalse(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!F9"), "filled consumeB:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!C10"), "blank skillA drops the pair silently:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!D11"), "blank skillB drops the pair silently:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Yuanqi!E12"), "mode 1 imports as Sustained, which cannot consume:\n" + all);
            Assert.IsFalse(issues.Exists(i => i.Location.StartsWith("Skill.xlsx/Yuanqi!") && i.Location.EndsWith("13")), "mode 0 is Instant, which may consume:\n" + all);
        }

        [Test]
        public void DomainRules_WarnWhenThreeSkillsTriggerPairwise()
        {
            string[] ab = { "ab", "acceleration", "slowtrap", "false", "false", "Instant", "OncePerOverlap", "true" };
            string[] bc = { "bc", "slowtrap", "giant", "false", "false", "Instant", "OncePerOverlap", "true" };
            string[] caOn = { "ca", "giant", "acceleration", "false", "false", "Instant", "OncePerOverlap", "true" };
            string[] caOff = { "ca", "giant", "acceleration", "false", "false", "Instant", "OncePerOverlap", "false" };

            List<Issue> triangle = DomainIssues(Probe("Skill", "Yuanqi", YuanqiVars, YuanqiTypes, ab, bc, caOn));
            Issue warning = triangle.Find(i => i.Severity == IssueSeverity.Warning && i.Location.StartsWith("Skill.xlsx/Yuanqi"));
            Assert.IsNotNull(warning, string.Join("\n", triangle));
            StringAssert.Contains("acceleration", warning.Message);
            StringAssert.Contains("giant", warning.Message);
            StringAssert.Contains("slowtrap", warning.Message);
            Assert.IsFalse(triangle.Exists(i => i.Severity == IssueSeverity.Error), string.Join("\n", triangle));

            List<Issue> disabled = DomainIssues(Probe("Skill", "Yuanqi", YuanqiVars, YuanqiTypes, ab, bc, caOff));
            Assert.IsFalse(disabled.Exists(i => i.Location.StartsWith("Skill.xlsx/Yuanqi")), "a disabled pair breaks the triangle: " + string.Join("\n", disabled));
        }

        [Test]
        public void DomainRules_RequireTheBackfireAndSurchargeCells()
        {
            DesignSheet obsession = Probe("Skill", "Obsession",
                new[] { "profileId", "repeatWindowSeconds", "repeatGainMultiplier", "backfireLockSeconds", "backfireMaxSpeedMultiplier", "backfireEndKeepRatio" },
                new[] { "string#key", "float", "float", "float", "float", "float" },
                new[] { "default", "5", "", "10", "0.85", "" });
            List<Issue> issues = DomainIssues(obsession);
            string all = string.Join("\n", issues);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Obsession!D5"), "blank repeatGainMultiplier:\n" + all);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Skill.xlsx/Obsession!G5"), "blank backfireEndKeepRatio:\n" + all);
            Assert.AreEqual(2, issues.FindAll(i => i.Location.StartsWith("Skill.xlsx/Obsession")).Count, all);
        }

        [Test]
        public void DomainRules_RequireTheTrainingDummyCellsWhenDummiesSpawn()
        {
            DesignSheet mode = Probe("Match", "ModeRule",
                new[] { "modeId", "trainingDummyCount", "trainingDummySpacingMeters", "trainingDummyStartOffsetMeters", "trainingDummyLateralOffsetMeters", "trainingDummyResetDelaySeconds" },
                new[] { "enum:MatchMode#key", "int", "float", "float", "float", "float" },
                new[] { "Online", "0", "", "", "", "" },
                new[] { "Practice", "3", "15", "", "0", "3" });
            List<Issue> issues = DomainIssues(mode);
            string all = string.Join("\n", issues);
            Assert.IsTrue(HasIssue(issues, IssueSeverity.Error, "Match.xlsx/ModeRule!E6"), "blank start offset with dummies:\n" + all);
            Assert.AreEqual(1, issues.FindAll(i => i.Location.StartsWith("Match.xlsx/ModeRule")).Count, "count 0 needs nothing; an explicit 0 lateral offset is fine:\n" + all);
        }

        [Test]
        public void Validator_RealWorkbooksHaveNoErrors()
        {
            DesignTableSet set = DesignTableSet.Load();
            List<Issue> issues = DesignTableValidator.Validate(set);
            List<string> errors = issues.FindAll(i => i.Severity == IssueSeverity.Error).ConvertAll(i => i.ToString());
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void Validator_ReportsBadCellsAndDuplicateKeys()
        {
            XlsxReader.Sheet raw = new XlsxReader.Sheet { Name = "Probe", ColumnCount = 4 };
            raw.Rows.Add(new[] { "##var", "id", "count", "mode" });
            raw.Rows.Add(new[] { "##type", "string#key", "int#range=0,5", "enum:SoloDifficulty" });
            raw.Rows.Add(new[] { "##group", "", "", "" });
            raw.Rows.Add(new[] { "##comment", "", "", "" });
            raw.Rows.Add(new[] { "", "a", "3", "Easy" });
            raw.Rows.Add(new[] { "", "a", "x", "Impossible" });
            raw.Rows.Add(new[] { "##", "ignored", "9", "Nope" });
            raw.Rows.Add(new[] { "", "b", "7", "" });

            DesignSheet sheet = DesignSheet.Parse("Probe", "memory", raw);
            Assert.AreEqual(3, sheet.Rows.Count, "commented row skipped");
            DesignTableSet set = new DesignTableSet();
            set.Sheets.Add(sheet);
            set.Enums["SoloDifficulty"] = new HashSet<string> { "Easy", "Normal", "Hard" };
            List<Issue> issues = DesignTableValidator.Validate(set);
            Assert.IsTrue(issues.Exists(i => i.Message.Contains("duplicate key")), "duplicate key");
            Assert.IsTrue(issues.Exists(i => i.Message.Contains("not an int")), "bad int");
            Assert.IsTrue(issues.Exists(i => i.Message.Contains("not a member of enum")), "bad enum");
            Assert.IsTrue(issues.Exists(i => i.Message.Contains("outside range")), "range");
            Assert.IsTrue(issues.Exists(i => i.Location.EndsWith("!C6")), "location points at the Excel cell: " + string.Join("\n", issues));
        }

        [Test]
        public void Exporter_DryRunBuildsDatabaseFromRealWorkbooks()
        {
            DesignTableExporter.Result result = DesignTableExporter.Run(dryRun: true, log: false);
            try
            {
                List<string> errors = result.Issues.FindAll(i => i.Severity == IssueSeverity.Error).ConvertAll(i => i.ToString());
                Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
                Assert.IsFalse(result.OutputsWritten);
                ProjectConfigDatabase db = result.Database;
                DesignTableSet set = DesignTableSet.Load();
                Assert.AreEqual(set.Find("Skill", "Skill").Rows.Count, db.skills.Count);
                Assert.IsFalse(db.skills.Exists(r => r.skillId.EndsWith("_anti")), "the *_anti rows are retired (unified backfire)");
                Assert.IsNull(typeof(SkillRecord).GetField("antiSkillId"), "SkillRecord.antiSkillId retired with its column");
                Assert.IsNull(typeof(SkillRecord).GetField("castLockSeconds"), "SkillRecord.castLockSeconds retired with its column");
                SkillRecord acceleration = db.skills.Find(r => r.skillId == "acceleration");
                Assert.AreEqual("skill.acceleration.name", acceleration.displayNameKey);
                Assert.AreEqual("神足通", db.localizedTexts.Find(t => t.key == acceleration.displayNameKey).zh, "display text lives in UI/Text");
                // Numeric checks compare the database with the sheet cell, never with a literal: designers change these numbers.
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", "push_projectile_hands", "cooldownSeconds"), db.skills.Find(r => r.skillId == "push_projectile_hands").cooldownSeconds, "Skill row value, not the SO value");
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", "acceleration", "castConfirmDelaySeconds"), acceleration.castConfirmDelaySeconds);
                Assert.AreEqual(Cell(set, "Skill", "Skill", "skillId", "acceleration", "effectDurationSeconds"), acceleration.effectDurationSeconds, "primary duration lives in Skill");
                Assert.Greater(acceleration.effectDurationSeconds, 0f);
                Assert.IsFalse(db.skillEffectGiants.Exists(r => r.skillId.EndsWith("_anti")), "family tables keep one row per normal skill");
                Assert.AreEqual(Cell(set, "Skill", "SkillEffectGiant", "skillId", "giant", "scaleMultiplier"), db.skillEffectGiants.Find(r => r.skillId == "giant").scaleMultiplier);
                CollectionAssert.AreEquivalent(new[] { "basic", "charged" }, db.pushes.ConvertAll(p => p.pushId), "charged_anti retired");
                Assert.AreEqual(Cell(set, "Skill", "Push", "pushId", "charged", "speed"), db.pushes.Find(r => r.pushId == "charged").speed);
                Assert.AreEqual((int)Cell(set, "Skill", "ComboInput", "profileId", "default", "maxBuffer"), db.comboInputs[0].maxBuffer);
                Assert.AreEqual(3, db.drivingTiers.Count);
                Assert.AreEqual(Cell(set, "AI", "DrivingTier", "difficulty", "Hard", "maxThrustAngleDegrees"), db.drivingTiers.Find(r => r.difficulty == "Hard").maxThrustAngleDegrees, "camelCase headers");
                Assert.AreEqual(Cell(set, "AI", "DrivingTuning", "profileId", "default", "targetSpeedMax"), db.drivingTunings[0].targetSpeedMax, "DrivingGlobal read keys became DrivingTuning");
                Assert.AreEqual(5, db.personalities.Count);
                Assert.AreEqual(Cell(set, "AI", "Personality", "personalityId", "keeper", "slot0Weight"), db.personalities.Find(p => p.personalityId == "keeper").slot0Weight, "weights live on the loadout slots");
                Assert.AreEqual(Cell(set, "AI", "SkillTuning", "profileId", "default", "keyMaxSeconds"), db.skillTunings[0].keyMaxSeconds, "SkillGlobal became a one-row SkillTuning");
                Assert.AreEqual((int)Cell(set, "AI", "SkillDifficulty", "difficulty", "Normal", "impairedDrivingReactionTicks"), db.skillDifficulties.Find(d => d.difficulty == "Normal").impairedDrivingReactionTicks, "driving impairment split from skill impairment");
                Assert.AreEqual(3, db.comboBindings.Count);
                Assert.AreEqual(4, db.comboBindings[2].sequence.Count);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "maxValue"), db.obsessions[0].maxValue);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "repeatWindowSeconds"), db.obsessions[0].repeatWindowSeconds);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "repeatGainMultiplier"), db.obsessions[0].repeatGainMultiplier);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "backfireLockSeconds"), db.obsessions[0].backfireLockSeconds);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "backfireMaxSpeedMultiplier"), db.obsessions[0].backfireMaxSpeedMultiplier);
                Assert.AreEqual(Cell(set, "Skill", "Obsession", "profileId", "default", "backfireEndKeepRatio"), db.obsessions[0].backfireEndKeepRatio);
                CollectionAssert.AreEquivalent(Keys(set, "Skill", "Yuanqi", "yuanqiId"), db.yuanqis.ConvertAll(y => y.yuanqiId), "one YuanqiRecord per Skill/Yuanqi row");
                YuanqiRecord frostLock = db.yuanqis.Find(y => y.yuanqiId == "frost_lock");
                Assert.IsNotNull(frostLock, "frost_lock row");
                Assert.AreEqual("yuanqi.frost_lock.name", frostLock.displayNameKey);
                Assert.IsNotNull(db.localizedTexts.Find(t => t.key == frostLock.displayNameKey), "缘起 names live in UI/Text");
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectFrostLock", "yuanqiId", "frost_lock", "freezeSeconds"), db.yuanqiEffectFrostLocks.Find(r => r.yuanqiId == "frost_lock").freezeSeconds);
                Assert.AreEqual(Cell(set, "Skill", "YuanqiEffectGiantCharge", "yuanqiId", "giant_charge", "impactImpulseMultiplier"), db.yuanqiEffectGiantCharges.Find(r => r.yuanqiId == "giant_charge").impactImpulseMultiplier);
                Assert.AreEqual(3, db.modeRules.Count);
                Assert.AreEqual((int)Cell(set, "Match", "ModeRule", "modeId", "Practice", "trainingDummyCount"), db.modeRules.Find(m => m.modeId == "Practice").trainingDummyCount, "training dummies live on the Practice row");
                Assert.AreEqual("Normal", db.modeRules.Find(m => m.modeId == "Solo").defaultDifficulty, "RacerNaming.defaultDifficulty moved onto ModeRule");
                Assert.AreEqual(1, db.selectionStages.Count, "map / skin placeholder stages are commented out with ##");
                Assert.AreEqual(0, db.selectionOptions.Count, "placeholder options are commented out with ##");
                Assert.AreEqual(6, db.trackSpawns.Count);
                Assert.AreEqual(1, db.movements.Count);
                Assert.AreEqual(Cell(set, "Movement", "Movement", "profileId", "default", "pushGraceSeconds"), db.movements[0].pushGraceSeconds, "PushReaction merged into Movement");
                Assert.AreEqual(Cell(set, "Movement", "Movement", "profileId", "default", "launchInheritSeconds"), db.movements[0].launchInheritSeconds, "LaunchHandoff merged into Movement");
                Assert.AreEqual(1, db.respawns.Count);
                Assert.AreEqual((int)Cell(set, "Match", "Track", "trackId", "racemap", "requiredCheckpointCount"), db.tracks[0].requiredCheckpointCount, "LapRule merged into Track");
                Assert.AreEqual(1, db.trackWrongWays.Count);
                Assert.Greater(db.localizedTexts.Count, 200);
                Assert.IsTrue(db.globalRules.Exists(r => r.ruleKey == "target_frame_rate"));
                Assert.IsFalse(db.globalRules.Exists(r => r.ruleKey == "combo_input_window_seconds"), "combo keys moved to Skill/ComboInput");
            }
            finally
            {
                if (result.Database != null)
                    Object.DestroyImmediate(result.Database);
            }
        }

        [Test]
        public void RemapSet_FollowsChangedWorkbooksAndReferences()
        {
            DesignTableSet set = DesignTableSet.Load();
            HashSet<string> all = DesignTableExporter.ComputeRemapSet(set, null, null);
            Assert.AreEqual(set.ExportTargets.Count, all.Count, "null = every exported sheet");

            HashSet<string> uiOnly = DesignTableExporter.ComputeRemapSet(set, new HashSet<string> { "UI" }, null);
            Assert.IsTrue(uiOnly.Contains("UI/Text"));
            Assert.IsTrue(uiOnly.Contains("Skill/Skill"), "Skill references UI/Text, so a UI change re-maps it");
            Assert.IsTrue(uiOnly.Contains("Match/ModeRule"), "ModeRule references UI/Text");
            Assert.IsFalse(uiOnly.Contains("Movement/Movement"), "Movement does not depend on UI");
            Assert.IsFalse(uiOnly.Contains("AI/DrivingTier"));

            HashSet<string> aiBalance = DesignTableExporter.ComputeRemapSet(set, null, new HashSet<string> { "ai" });
            Assert.IsTrue(aiBalance.Contains("AI/DrivingTier"));
            Assert.IsFalse(aiBalance.Contains("Skill/Skill"));

            HashSet<string> trackChanged = DesignTableExporter.ComputeRemapSet(set, new HashSet<string> { "Match" }, new HashSet<string> { "track" });
            Assert.IsTrue(trackChanged.Contains("Match/TrackSpawn"));
            Assert.IsTrue(trackChanged.Contains("Match/TrackWrongWay"), "TrackWrongWay carries the track tag");
            Assert.IsTrue(trackChanged.Contains("Match/Track"), "Track carries the lap rules and the track tag");
            Assert.IsFalse(trackChanged.Contains("Match/Lobby"), "Lobby lacks the track tag");
        }

        [Test]
        public void ImportState_DetectsUnchangedAndChangedWorkbooks()
        {
            DesignTableSet set = DesignTableSet.Load();
            DesignTableImportState state = new DesignTableImportState();
            HashSet<string> first = state.ChangedWorkbooks(set, out Dictionary<string, string> hashes);
            Assert.AreEqual(set.WorkbookPaths.Count, first.Count, "nothing recorded yet → everything changed");
            foreach (KeyValuePair<string, string> kv in hashes)
                state.Hashes[kv.Key] = kv.Value;
            HashSet<string> second = state.ChangedWorkbooks(set, out _);
            Assert.AreEqual(0, second.Count, "same bytes → no change");
            state.Hashes["Skill"] = "stale";
            HashSet<string> third = state.ChangedWorkbooks(set, out _);
            Assert.AreEqual(1, third.Count);
            Assert.IsTrue(third.Contains("Skill"));
        }

        [Test]
        public void Exporter_NothingChangedSkipsWhenHashesMatch()
        {
            // Dry run with Force so the result carries a database; then verify the no-change path reports NothingChanged.
            DesignTableSet set = DesignTableSet.Load();
            DesignTableImportState state = DesignTableImportState.Load();
            Dictionary<string, string> saved = new Dictionary<string, string>(state.Hashes);
            try
            {
                state.ChangedWorkbooks(set, out Dictionary<string, string> hashes);
                state.Hashes.Clear();
                foreach (KeyValuePair<string, string> kv in hashes) state.Hashes[kv.Key] = kv.Value;
                state.Save();
                DesignTableExporter.Result result = DesignTableExporter.Run(new DesignTableExporter.Options { DryRun = true, Log = false });
                Assert.IsTrue(result.NothingChanged || !File.Exists(DesignTableExporter.JsonPath), "all hashes recorded → nothing to re-map");
                if (result.Database != null) Object.DestroyImmediate(result.Database);
            }
            finally
            {
                state.Hashes.Clear();
                foreach (KeyValuePair<string, string> kv in saved) state.Hashes[kv.Key] = kv.Value;
                state.Save();
            }
        }

        [Test]
        public void LocalizedTextRepository_FallsBackToKey()
        {
            ProjectConfigDatabase db = ScriptableObject.CreateInstance<ProjectConfigDatabase>();
            try
            {
                db.localizedTexts.Add(new LocalizedTextRecord { key = "skill.giant.name", zh = "法相金身" });
                LocalizedTextRepository repo = new LocalizedTextRepository(db);
                Assert.AreEqual("法相金身", repo.Get("skill.giant.name"));
                Assert.AreEqual("missing.key", repo.Get("missing.key"));
            }
            finally
            {
                Object.DestroyImmediate(db);
            }
        }
    }
}
