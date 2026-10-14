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
    /// Excel → ProjectConfigDatabase. Load all workbooks, validate everything, then re-map only the layers that
    /// need it: sheets in workbooks whose bytes changed since the last successful import (plus sheets that
    /// reference them through #ref), optionally narrowed by a tag filter. Unchanged lists are kept from the
    /// current asset. Outputs are only written when there is no Error, so a broken table never reaches the game.
    /// </summary>
    public static class DesignTableExporter
    {
        public const string DatabaseAssetPath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";
        public const string JsonPath = "Assets/Resources/Config/ProjectConfigDatabase.json";
        public const string ReportPath = DesignTableSet.ReportsRoot + "/validation.md";

        public sealed class Options
        {
            /// <summary>Validate and build only; never write outputs or the import state.</summary>
            public bool DryRun;
            /// <summary>Re-map every exported sheet regardless of file hashes.</summary>
            public bool Force;
            /// <summary>Only sheets carrying at least one of these tags are re-mapped (null/empty = all).</summary>
            public HashSet<string> Tags;
            /// <summary>Workbooks reported as changed by the caller (e.g. the asset postprocessor); merged with hash detection.</summary>
            public HashSet<string> ChangedWorkbooks;
            public bool Log = true;
        }

        public sealed class Result
        {
            public List<Issue> Issues = new List<Issue>();
            public int ErrorCount;
            public int WarningCount;
            public int SheetsExported;
            public int SheetsKept;
            public int RecordsExported;
            public bool OutputsWritten;
            public bool NothingChanged;
            public HashSet<string> ChangedWorkbooks = new HashSet<string>(StringComparer.Ordinal);
            public List<string> RemappedSheets = new List<string>();
            public ProjectConfigDatabase Database;
            public bool Success => ErrorCount == 0;
        }

        public static Result Run(bool dryRun, bool log = true)
        {
            return Run(new Options { DryRun = dryRun, Log = log, Force = true });
        }

        public static Result Run(Options options)
        {
            options = options ?? new Options();
            Result result = new Result();
            DesignTableSet set = DesignTableSet.Load();
            result.Issues.AddRange(DesignTableValidator.Validate(set));

            DesignTableImportState state = DesignTableImportState.Load();
            HashSet<string> changed = state.ChangedWorkbooks(set, out Dictionary<string, string> currentHashes);
            if (options.ChangedWorkbooks != null)
                changed.UnionWith(options.ChangedWorkbooks);
            result.ChangedWorkbooks = changed;

            ProjectConfigDatabase asset = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabaseAssetPath);
            bool outputsMissing = asset == null || !File.Exists(JsonPath);
            HashSet<string> remap = ComputeRemapSet(set, options.Force || outputsMissing ? null : changed, options.Tags);
            if (remap.Count == 0 && !options.Force && !outputsMissing)
            {
                result.NothingChanged = true;
                CountIssues(result);
                if (options.Log)
                    Debug.Log("[DesignTables] no workbook changed since the last import; nothing to do (use Force to re-import everything).");
                WriteReport(result, options);
                return result;
            }

            // Build into a scratch instance: re-mapped sheets from Excel, every other list copied from the current asset.
            ProjectConfigDatabase scratch = ScriptableObject.CreateInstance<ProjectConfigDatabase>();
            if (asset != null)
                CopyLists(asset, scratch);
            Dictionary<string, string> texts = CollectTexts(set);
            foreach (KeyValuePair<string, DesignTableSet.ExportTarget> kv in set.ExportTargets)
            {
                DesignTableSet.ExportTarget target = kv.Value;
                if (!remap.Contains(kv.Key))
                {
                    result.SheetsKept++;
                    continue;
                }
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
                result.RemappedSheets.Add(kv.Key);
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

            CountIssues(result);
            result.Database = scratch;
            if (options.Log)
                LogIssues(result);

            if (!options.DryRun && result.Success)
            {
                WriteOutputs(set, scratch, asset, remap);
                ProjectConfigRuntime.Invalidate();
                // A tag-filtered import only certifies workbooks whose exported sheets were all re-mapped.
                foreach (KeyValuePair<string, string> kv in currentHashes)
                    if (options.Tags == null || options.Tags.Count == 0 || AllSheetsOfWorkbookRemapped(set, kv.Key, remap)) state.Hashes[kv.Key] = kv.Value;
                state.Save();
                result.OutputsWritten = true;
            }
            WriteReport(result, options);
            if (!options.DryRun)
                UnityEngine.Object.DestroyImmediate(scratch);
            return result;
        }

        /// <summary>
        /// Sheets to re-map: those in <paramref name="changedWorkbooks"/> (null = all) or depending on them via #ref,
        /// filtered by <paramref name="tags"/> (null/empty = no filter). Pure function, covered by tests.
        /// </summary>
        public static HashSet<string> ComputeRemapSet(DesignTableSet set, HashSet<string> changedWorkbooks, HashSet<string> tags)
        {
            HashSet<string> remap = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, DesignTableSet.ExportTarget> kv in set.ExportTargets)
            {
                DesignTableSet.ExportTarget target = kv.Value;
                if (tags != null && tags.Count > 0 && !target.Tags.Overlaps(tags))
                    continue;
                if (changedWorkbooks == null)
                {
                    remap.Add(kv.Key);
                    continue;
                }
                DesignSheet sheet = set.Find(target.Workbook, target.Sheet);
                HashSet<string> deps = sheet != null ? set.DependencyWorkbooks(sheet) : new HashSet<string> { target.Workbook };
                if (deps.Overlaps(changedWorkbooks))
                    remap.Add(kv.Key);
            }
            return remap;
        }

        private static bool AllSheetsOfWorkbookRemapped(DesignTableSet set, string workbook, HashSet<string> remap)
        {
            foreach (KeyValuePair<string, DesignTableSet.ExportTarget> kv in set.ExportTargets)
                if (kv.Value.Workbook == workbook && !remap.Contains(kv.Key))
                    return false;
            return true;
        }

        private static void CountIssues(Result result)
        {
            result.ErrorCount = 0; result.WarningCount = 0;
            foreach (Issue issue in result.Issues)
            {
                if (issue.Severity == IssueSeverity.Error) result.ErrorCount++;
                else if (issue.Severity == IssueSeverity.Warning) result.WarningCount++;
            }
        }

        private static void CopyLists(ProjectConfigDatabase from, ProjectConfigDatabase to)
        {
            foreach (FieldInfo f in typeof(ProjectConfigDatabase).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(IList).IsAssignableFrom(f.FieldType))
                    continue;
                IList source = (IList)f.GetValue(from);
                IList copy = (IList)Activator.CreateInstance(f.FieldType);
                if (source != null)
                    foreach (object item in source) copy.Add(item);
                f.SetValue(to, copy);
            }
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

        private static void WriteOutputs(DesignTableSet set, ProjectConfigDatabase scratch, ProjectConfigDatabase asset, HashSet<string> remap)
        {
            if (asset == null)
                throw new InvalidOperationException("Missing " + DatabaseAssetPath);

            CopyLists(scratch, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            string json = JsonUtility.ToJson(asset, true);
            File.WriteAllText(JsonPath, json + "\n", new UTF8Encoding(false));

            WriteCsvMirror(set, remap);
            AssetDatabase.ImportAsset(JsonPath);
            AssetDatabase.Refresh();
        }

        /// <summary>Regenerate _Export/<Workbook>/<Sheet>.csv for the re-mapped sheets (and every presentation sheet of a changed workbook).</summary>
        public static void WriteCsvMirror(DesignTableSet set, HashSet<string> remap = null)
        {
            HashSet<string> workbooks = new HashSet<string>(StringComparer.Ordinal);
            if (remap != null)
                foreach (string key in remap) workbooks.Add(key.Split('/')[0]);
            foreach (DesignSheet sheet in set.Sheets)
            {
                if (remap != null && !workbooks.Contains(sheet.Workbook))
                    continue;
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

        private static void WriteReport(Result result, Options options)
        {
            Directory.CreateDirectory(DesignTableSet.ReportsRoot);
            StringBuilder sb = new StringBuilder();
            sb.Append("# Design tables validation\n\n");
            sb.Append("- time: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            sb.Append("- mode: ").Append(options.DryRun ? "validate only" : options.Force ? "import (force)" : "import (changed)").Append('\n');
            if (options.Tags != null && options.Tags.Count > 0)
                sb.Append("- tags: ").Append(string.Join(",", options.Tags)).Append('\n');
            sb.Append("- changed workbooks: ").Append(result.ChangedWorkbooks.Count == 0 ? "(none)" : string.Join(", ", result.ChangedWorkbooks)).Append('\n');
            sb.Append("- sheets re-mapped: ").Append(result.SheetsExported).Append(", kept: ").Append(result.SheetsKept).Append(", records: ").Append(result.RecordsExported).Append('\n');
            sb.Append("- errors: ").Append(result.ErrorCount).Append(", warnings: ").Append(result.WarningCount).Append('\n');
            sb.Append("- outputs written: ").Append(result.OutputsWritten ? "yes" : result.NothingChanged ? "no (nothing changed)" : "no").Append("\n\n");
            if (result.RemappedSheets.Count > 0)
                sb.Append("Re-mapped: ").Append(string.Join(", ", result.RemappedSheets)).Append("\n\n");
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
            Debug.Log("[DesignTables] changed=" + string.Join(",", result.ChangedWorkbooks) + " remapped=" + result.SheetsExported + " kept=" + result.SheetsKept + " records=" + result.RecordsExported + " errors=" + result.ErrorCount + " warnings=" + result.WarningCount);
        }
    }
}
#endif
