#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Excel → ProjectConfigDatabase. Load all workbooks, validate, map exported sheets onto the database
    /// lists, and (unless dry run) write the asset, the JSON fallback, the CSV mirror and the report.
    /// Outputs are only written when there is no Error, so a broken table never reaches the game.
    /// </summary>
    public static class DesignTableExporter
    {
        public const string DatabaseAssetPath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";
        public const string JsonPath = "Assets/Resources/Config/ProjectConfigDatabase.json";
        public const string ReportPath = DesignTableSet.ReportsRoot + "/validation.md";

        public sealed class Result
        {
            public List<Issue> Issues = new List<Issue>();
            public int ErrorCount;
            public int WarningCount;
            public int SheetsExported;
            public int RecordsExported;
            public bool OutputsWritten;
            public ProjectConfigDatabase Database;
            public bool Success => ErrorCount == 0;
        }

        public static Result Run(bool dryRun, bool log = true)
        {
            Result result = new Result();
            DesignTableSet set = DesignTableSet.Load();
            result.Issues.AddRange(DesignTableValidator.Validate(set));

            // Build into a scratch instance first; copy onto the asset only when everything validates.
            ProjectConfigDatabase scratch = ScriptableObject.CreateInstance<ProjectConfigDatabase>();
            Dictionary<string, string> texts = CollectTexts(set);
            foreach (KeyValuePair<string, DesignTableSet.ExportTarget> kv in set.ExportTargets)
            {
                DesignTableSet.ExportTarget target = kv.Value;
                DesignSheet sheet = set.Find(target.Workbook, target.Sheet);
                if (sheet == null)
                {
                    result.Issues.Add(new Issue { Severity = IssueSeverity.Error, Location = kv.Key, Message = "exported sheet missing from workbook" });
                    continue;
                }
                Type recordType = FindType(target.RecordType);
                FieldInfo listField = typeof(ProjectConfigDatabase).GetField(target.ListField, BindingFlags.Public | BindingFlags.Instance);
                if (recordType == null || listField == null)
                {
                    result.Issues.Add(new Issue { Severity = IssueSeverity.Error, Location = kv.Key, Message = "record type '" + target.RecordType + "' or list '" + target.ListField + "' missing (run gen_records.py and recompile)" });
                    continue;
                }
                IList list = DesignTableRecordMapper.MapSheet(set, sheet, recordType, texts, result.Issues);
                listField.SetValue(scratch, list);
                result.SheetsExported++;
                result.RecordsExported += list.Count;
            }

            foreach (ProjectConfigValidationMessage m in ProjectConfigValidator.Validate(scratch))
            {
                result.Issues.Add(new Issue
                {
                    Severity = m.severity == ProjectConfigValidationSeverity.Error ? IssueSeverity.Error : m.severity == ProjectConfigValidationSeverity.Warning ? IssueSeverity.Warning : IssueSeverity.Info,
                    Location = "ProjectConfigValidator/" + m.context,
                    Message = m.message
                });
            }

            foreach (Issue issue in result.Issues)
            {
                if (issue.Severity == IssueSeverity.Error) result.ErrorCount++;
                else if (issue.Severity == IssueSeverity.Warning) result.WarningCount++;
            }
            result.Database = scratch;

            if (log)
                LogIssues(result);

            WriteReport(result, dryRun);
            if (!dryRun && result.Success)
            {
                WriteOutputs(set, scratch);
                result.OutputsWritten = true;
            }
            if (!dryRun)
                UnityEngine.Object.DestroyImmediate(scratch);
            return result;
        }

        private static Dictionary<string, string> CollectTexts(DesignTableSet set)
        {
            Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.Ordinal);
            DesignSheet sheet = set.Find("UI", "Text");
            if (sheet == null)
                return texts;
            ColumnSpec key = sheet.FindColumn("key"), zh = sheet.FindColumn("zh");
            if (key == null || zh == null)
                return texts;
            foreach (DataRow row in sheet.Rows)
                texts[sheet.Cell(row, key).Trim()] = sheet.Cell(row, zh);
            return texts;
        }

        private static Type FindType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(name, false);
                if (t != null)
                    return t;
            }
            return null;
        }

        private static void WriteOutputs(DesignTableSet set, ProjectConfigDatabase scratch)
        {
            ProjectConfigDatabase asset = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabaseAssetPath);
            if (asset == null)
                throw new InvalidOperationException("Missing " + DatabaseAssetPath);

            // Copy every list field (hand-written and generated) from scratch to the asset; keep the asset's own defaults.
            foreach (FieldInfo f in typeof(ProjectConfigDatabase).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(IList).IsAssignableFrom(f.FieldType))
                    f.SetValue(asset, f.GetValue(scratch));
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            string json = JsonUtility.ToJson(asset, true);
            File.WriteAllText(JsonPath, json + "\n", new UTF8Encoding(false));

            WriteCsvMirror(set);
            AssetDatabase.ImportAsset(JsonPath);
            AssetDatabase.Refresh();
        }

        /// <summary>Regenerate _Export/<Workbook>/<Sheet>.csv from the workbooks so git diffs show cell changes.</summary>
        public static void WriteCsvMirror(DesignTableSet set)
        {
            foreach (DesignSheet sheet in set.Sheets)
            {
                string dir = Path.Combine(DesignTableSet.ExportRoot, sheet.Workbook);
                Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                AppendRow(sb, "##var", sheet.Columns, c => c.Var);
                AppendRow(sb, "##type", sheet.Columns, c => c.Type.Raw);
                AppendRow(sb, "##group", sheet.Columns, c => c.Group);
                AppendRow(sb, "##comment", sheet.Columns, c => c.Comment);
                foreach (DataRow row in sheet.Rows)
                    AppendRow(sb, string.Empty, sheet.Columns, c => sheet.Cell(row, c));
                File.WriteAllText(Path.Combine(dir, sheet.Name + ".csv"), sb.ToString(), new UTF8Encoding(false));
            }
        }

        private static void AppendRow(StringBuilder sb, string first, List<ColumnSpec> columns, Func<ColumnSpec, string> get)
        {
            sb.Append(Csv(first));
            foreach (ColumnSpec c in columns)
                sb.Append(',').Append(Csv(get(c)));
            sb.Append("\r\n");
        }

        private static string Csv(string s)
        {
            s = s ?? string.Empty;
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        private static void WriteReport(Result result, bool dryRun)
        {
            Directory.CreateDirectory(DesignTableSet.ReportsRoot);
            StringBuilder sb = new StringBuilder();
            sb.Append("# Design tables validation\n\n");
            sb.Append("- time: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            sb.Append("- mode: ").Append(dryRun ? "validate only" : "import").Append('\n');
            sb.Append("- sheets exported: ").Append(result.SheetsExported).Append(", records: ").Append(result.RecordsExported).Append('\n');
            sb.Append("- errors: ").Append(result.ErrorCount).Append(", warnings: ").Append(result.WarningCount).Append('\n');
            sb.Append("- outputs written: ").Append(result.Success && !dryRun ? "yes" : "no").Append("\n\n");
            if (result.Issues.Count > 0)
            {
                sb.Append("| severity | location | message |\n|---|---|---|\n");
                foreach (Issue issue in result.Issues)
                    sb.Append("| ").Append(issue.Severity).Append(" | ").Append(issue.Location.Replace("|", "\\|")).Append(" | ").Append(issue.Message.Replace("|", "\\|")).Append(" |\n");
            }
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static void LogIssues(Result result)
        {
            foreach (Issue issue in result.Issues)
            {
                string line = "[DesignTables] " + issue.Location + " :: " + issue.Message;
                if (issue.Severity == IssueSeverity.Error) Debug.LogError(line);
                else if (issue.Severity == IssueSeverity.Warning) Debug.LogWarning(line);
                else Debug.Log(line);
            }
            Debug.Log("[DesignTables] sheets=" + result.SheetsExported + " records=" + result.RecordsExported + " errors=" + result.ErrorCount + " warnings=" + result.WarningCount);
        }
    }
}
#endif
