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
            XlsxReader.Sheet balance = sheets.Find(s => s.Name == "SkillBalance");
            Assert.IsNotNull(balance, "SkillBalance sheet");
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
            TypeSpec key = TypeSpec.Parse("string#key#ref=SkillDefinition.skillId");
            Assert.AreEqual(ColumnBaseType.String, key.BaseType);
            Assert.IsTrue(key.IsKey);
            Assert.AreEqual("SkillDefinition", key.RefSheet);
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
            Assert.IsNotNull(set.Find("Skill", "SkillBalance"));
            Assert.AreEqual(12, set.Find("Skill", "SkillBalance").Rows.Count);
            Assert.IsTrue(set.Enums.ContainsKey("SoloDifficulty"));
            Assert.IsTrue(set.ExportTargets.ContainsKey("Skill/SkillDefinition"));
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
                Assert.AreEqual(12, db.skillDefinitions.Count);
                Assert.AreEqual("神足通", db.skillDefinitions.Find(r => r.skillId == "acceleration").displayName, "text key resolved into displayName");
                Assert.AreEqual(12f, db.skillBalances.Find(r => r.skillId == "push_projectile_hands").cooldownSeconds, "effective config value, not the SO value");
                Assert.AreEqual(1f, db.skillBalances[0].castConfirmDelaySeconds);
                Assert.Greater(db.skillEffectParams.Count, 40);
                Assert.AreEqual(3, db.drivingTiers.Count);
                Assert.AreEqual(90f, db.drivingTiers.Find(r => r.difficulty == "Hard").MaxThrustAngleDegrees);
                Assert.AreEqual(5, db.personalities.Count);
                Assert.AreEqual(3, db.comboBindings.Count);
                Assert.AreEqual(4, db.comboBindings[2].sequence.Count);
                Assert.AreEqual(1000f, db.obsessions[0].maxValue);
                Assert.AreEqual(3, db.modeRules.Count);
                Assert.AreEqual(6, db.trackSpawns.Count);
                Assert.Greater(db.localizedTexts.Count, 200);
                Assert.IsTrue(db.globalRules.Exists(r => r.ruleKey == "combo_input_window_seconds"));
            }
            finally
            {
                if (result.Database != null)
                    Object.DestroyImmediate(result.Database);
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
