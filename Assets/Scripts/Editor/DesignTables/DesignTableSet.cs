#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>All workbooks under Assets/Design/Tables plus the scope and enum definitions.</summary>
    public sealed class DesignTableSet
    {
        public const string TablesRoot = "Assets/Design/Tables";
        public const string ExportRoot = TablesRoot + "/_Export";
        public const string ReportsRoot = TablesRoot + "/_Reports";
        public const string EnumSheetName = "__enums__";
        public const string MetaSheetName = "__meta__";

        public readonly List<DesignSheet> Sheets = new List<DesignSheet>();
        /// <summary>enumName → allowed names (plus integer values as strings).</summary>
        public readonly Dictionary<string, HashSet<string>> Enums = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        public readonly Dictionary<string, ExportTarget> ExportTargets = new Dictionary<string, ExportTarget>(StringComparer.Ordinal);
        public readonly List<string> LoadErrors = new List<string>();

        public sealed class ExportTarget
        {
            public string Workbook;
            public string Sheet;
            public string RecordType;
            public string ListField;
        }

        public static DesignTableSet Load(string projectRoot = null)
        {
            string root = projectRoot ?? Directory.GetCurrentDirectory();
            DesignTableSet set = new DesignTableSet();
            string tablesDir = Path.Combine(root, TablesRoot);
            if (!Directory.Exists(tablesDir))
            {
                set.LoadErrors.Add("Missing directory " + TablesRoot);
                return set;
            }

            foreach (string workbookPath in Directory.GetFiles(tablesDir, "*.xlsx", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(workbookPath);
                if (fileName.StartsWith("~$", StringComparison.Ordinal))
                    continue;
                string workbook = Path.GetFileNameWithoutExtension(workbookPath);
                string relative = workbookPath.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                try
                {
                    foreach (XlsxReader.Sheet raw in XlsxReader.Read(workbookPath))
                    {
                        DesignSheet sheet = DesignSheet.Parse(workbook, relative, raw);
                        if (sheet != null)
                            set.Sheets.Add(sheet);
                    }
                }
                catch (Exception e)
                {
                    set.LoadErrors.Add(relative + ": " + e.Message);
                }
            }

            set.LoadEnums();
            set.LoadExportMap(root);
            return set;
        }

        private void LoadEnums()
        {
            DesignSheet enums = Find("Global", EnumSheetName);
            if (enums == null)
                return;
            ColumnSpec nameCol = enums.FindColumn("enumName");
            ColumnSpec itemCol = enums.FindColumn("name");
            ColumnSpec valueCol = enums.FindColumn("value");
            ColumnSpec aliasCol = enums.FindColumn("alias");
            if (nameCol == null || itemCol == null)
                return;
            foreach (DataRow row in enums.Rows)
            {
                string enumName = enums.Cell(row, nameCol).Trim();
                if (!Enums.TryGetValue(enumName, out HashSet<string> items))
                {
                    items = new HashSet<string>(StringComparer.Ordinal);
                    Enums[enumName] = items;
                }
                items.Add(enums.Cell(row, itemCol).Trim());
                if (valueCol != null && !string.IsNullOrWhiteSpace(enums.Cell(row, valueCol)))
                    items.Add(enums.Cell(row, valueCol).Trim());
                if (aliasCol != null && !string.IsNullOrWhiteSpace(enums.Cell(row, aliasCol)))
                    items.Add(enums.Cell(row, aliasCol).Trim());
            }
        }

        private void LoadExportMap(string root)
        {
            string mapPath = Path.Combine(root, TablesRoot, "export-map.json");
            if (!File.Exists(mapPath))
            {
                LoadErrors.Add("Missing " + TablesRoot + "/export-map.json (run Tools/design-tables/gen_records.py)");
                return;
            }
            // Tiny hand parser: {"Workbook/Sheet": {"record": "...", "list": "..."}, ...}
            string json = File.ReadAllText(mapPath);
            int pos = 0;
            while (true)
            {
                int keyStart = json.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
                pos = keyEnd + 1;
                if (!key.Contains("/"))
                    continue;
                int objEnd = json.IndexOf('}', pos);
                string obj = json.Substring(pos, objEnd - pos);
                string record = Extract(obj, "record");
                string list = Extract(obj, "list");
                string[] parts = key.Split('/');
                ExportTargets[key] = new ExportTarget { Workbook = parts[0], Sheet = parts[1], RecordType = record, ListField = list };
                pos = objEnd + 1;
            }
        }

        private static string Extract(string obj, string field)
        {
            int i = obj.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int q1 = obj.IndexOf('"', obj.IndexOf(':', i) + 1);
            int q2 = obj.IndexOf('"', q1 + 1);
            return obj.Substring(q1 + 1, q2 - q1 - 1);
        }

        public DesignSheet Find(string workbook, string sheet)
        {
            for (int i = 0; i < Sheets.Count; i++)
            {
                if (Sheets[i].Workbook == workbook && Sheets[i].Name == sheet)
                    return Sheets[i];
            }
            return null;
        }

        /// <summary>Resolve "Sheet" or "Workbook/Sheet" relative to a workbook.</summary>
        public DesignSheet Resolve(string currentWorkbook, string refWorkbook, string refSheet)
        {
            return Find(string.IsNullOrEmpty(refWorkbook) ? currentWorkbook : refWorkbook, refSheet);
        }

        public bool IsExported(DesignSheet sheet)
        {
            return ExportTargets.ContainsKey(sheet.Workbook + "/" + sheet.Name);
        }

        public HashSet<string> KeyValues(DesignSheet sheet, string field)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            ColumnSpec column = sheet.FindColumn(field);
            if (column == null)
                return values;
            foreach (DataRow row in sheet.Rows)
                values.Add(sheet.Cell(row, column).Trim());
            return values;
        }
    }
}
#endif
