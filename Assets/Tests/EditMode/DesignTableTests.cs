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

        [Test]
        public void DesignTableSet_LoadsAllWorkbooksWithoutLoadErrors()
        {
            DesignTableSet set = DesignTableSet.Load();
            Assert.AreEqual(0, set.LoadErrors.Count, string.Join("\n", set.LoadErrors));
            Assert.IsNotNull(set.Find("Skill", "Skill"));
            Assert.AreEqual(12, set.Find("Skill", "Skill").Rows.Count);
            Assert.IsTrue(set.Enums.ContainsKey("SoloDifficulty"));
            Assert.IsTrue(set.Enums.ContainsKey("SkillId"), "SkillId registry");
            Assert.IsTrue(set.ExportTargets.ContainsKey("Skill/Skill"));
            Assert.IsFalse(set.ExportTargets.ContainsKey("Skill/SkillConstant"), "code constants are not exported");
            Assert.IsTrue(set.ExportTargets.ContainsKey("UI/Text"));
            Assert.IsFalse(set.ExportTargets.ContainsKey("Skill/SkillPresentation"), "presentation sheets are not exported");
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
                Assert.AreEqual(12, db.skills.Count);
                SkillRecord acceleration = db.skills.Find(r => r.skillId == "acceleration");
                Assert.AreEqual("skill.acceleration.name", acceleration.displayNameKey);
                Assert.AreEqual("神足通", db.localizedTexts.Find(t => t.key == acceleration.displayNameKey).zh, "display text lives in UI/Text");
                Assert.AreEqual(12f, db.skills.Find(r => r.skillId == "push_projectile_hands").cooldownSeconds, "effective config value, not the SO value");
                Assert.AreEqual(1f, acceleration.castConfirmDelaySeconds);
                Assert.AreEqual(10f, acceleration.effectDurationSeconds, "primary duration moved into Skill");
                Assert.AreEqual(0f, db.skills.Find(r => r.skillId == "blackcurtain_anti").obsessionGain, "anti rows carry no obsession gain (A6)");
                Assert.AreEqual("blackcurtain_anti", db.skills.Find(r => r.skillId == "blackcurtain").antiSkillId, "renamed anti id (A5)");
                Assert.AreEqual(2, db.skillEffectGiants.Count);
                Assert.AreEqual(4f, db.skillEffectGiants.Find(r => r.skillId == "giant").scaleMultiplier);
                Assert.AreEqual(3, db.pushes.Count);
                Assert.AreEqual(600f, db.pushes.Find(r => r.pushId == "charged").speed);
                Assert.AreEqual(6, db.comboInputs[0].maxBuffer);
                Assert.AreEqual(3, db.drivingTiers.Count);
                Assert.AreEqual(90f, db.drivingTiers.Find(r => r.difficulty == "Hard").MaxThrustAngleDegrees);
                Assert.AreEqual(5, db.personalities.Count);
                Assert.AreEqual(3, db.comboBindings.Count);
                Assert.AreEqual(4, db.comboBindings[2].sequence.Count);
                Assert.AreEqual(1000f, db.obsessions[0].maxValue);
                Assert.AreEqual(3, db.modeRules.Count);
                Assert.AreEqual(6, db.trackSpawns.Count);
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
            Assert.IsFalse(uiOnly.Contains("Movement/MovementBase"), "MovementBase does not depend on UI");
            Assert.IsFalse(uiOnly.Contains("AI/DrivingTier"));

            HashSet<string> aiBalance = DesignTableExporter.ComputeRemapSet(set, null, new HashSet<string> { "ai" });
            Assert.IsTrue(aiBalance.Contains("AI/DrivingTier"));
            Assert.IsFalse(aiBalance.Contains("Skill/Skill"));

            HashSet<string> trackChanged = DesignTableExporter.ComputeRemapSet(set, new HashSet<string> { "Match" }, new HashSet<string> { "track" });
            Assert.IsTrue(trackChanged.Contains("Match/TrackSpawn"));
            Assert.IsTrue(trackChanged.Contains("Movement/LapRule"), "LapRule references Match/Track and carries the track tag");
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
