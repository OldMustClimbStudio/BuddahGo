#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BuddahGo.DesignTables
{
    public enum IssueSeverity { Info, Warning, Error }

    public sealed class Issue
    {
        public IssueSeverity Severity;
        public string Location = string.Empty;
        public string Message = string.Empty;
        public override string ToString() => Severity + " " + Location + ": " + Message;
    }

    /// <summary>
    /// Schema-level validation of every parsed sheet: header sanity, key uniqueness, type conversion,
    /// enum membership, foreign keys, ranges, asset paths. Runs on exported and presentation sheets alike.
    /// </summary>
    public static class DesignTableValidator
    {
        public static List<Issue> Validate(DesignTableSet set)
        {
            List<Issue> issues = new List<Issue>();
            foreach (string error in set.LoadErrors)
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = DesignTableSet.TablesRoot, Message = error });

            foreach (DesignSheet sheet in set.Sheets)
            {
                if (sheet.Name == DesignTableSet.EnumSheetName || sheet.Name == DesignTableSet.MetaSheetName)
                    continue;
                ValidateSheet(set, sheet, issues);
            }

            DesignTableDomainRules.Validate(set, issues);
            return issues;
        }

        private static void ValidateSheet(DesignTableSet set, DesignSheet sheet, List<Issue> issues)
        {
            string sheetLoc = sheet.Workbook + ".xlsx/" + sheet.Name;
            if (sheet.KeyColumns.Count == 0)
                issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = sheetLoc, Message = "no #key column declared" });

            HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (DataRow row in sheet.Rows)
            {
                if (sheet.KeyColumns.Count > 0)
                {
                    string composite = string.Empty;
                    bool keyMissing = false;
                    foreach (ColumnSpec k in sheet.KeyColumns)
                    {
                        string v = sheet.Cell(row, k).Trim();
                        // ruleSetId/modeTag/mapTag may stay empty (= default / all); other key columns must be filled.
                        if (string.IsNullOrEmpty(v) && k.Var != "ruleSetId" && k.Var != "modeTag" && k.Var != "mapTag")
                        {
                            issues.Add(new Issue { Severity = IssueSeverity.Error, Location = sheet.Location(row, k), Message = "key column '" + k.Var + "' is empty" });
                            keyMissing = true;
                        }
                        composite += v + "\u001f";
                    }
                    if (!keyMissing && !seenKeys.Add(composite))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = sheet.Location(row, sheet.KeyColumns[0]), Message = "duplicate key " + composite.Replace("\u001f", "+").TrimEnd('+') });
                }

                foreach (ColumnSpec column in sheet.Columns)
                {
                    if (!column.IsExported)
                        continue;
                    string raw = sheet.Cell(row, column);
                    ValidateCell(set, sheet, row, column, column.Type, raw, issues);
                }
            }
        }

        private static void ValidateCell(DesignTableSet set, DesignSheet sheet, DataRow row, ColumnSpec column, TypeSpec type, string raw, List<Issue> issues)
        {
            string value = raw == null ? string.Empty : raw.Trim();
            string loc = sheet.Location(row, column);
            switch (type.BaseType)
            {
                case ColumnBaseType.Int:
                    if (!string.IsNullOrEmpty(value) && !TryInt(value, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not an int" });
                    else if (type.HasRange && !string.IsNullOrEmpty(value) && TryInt(value, out int iv) && (iv < type.RangeMin || iv > type.RangeMax))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = value + " outside range " + type.RangeMin + ".." + type.RangeMax });
                    break;
                case ColumnBaseType.Float:
                    if (!string.IsNullOrEmpty(value) && !TryFloat(value, out float fv))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not a number" });
                    else if (type.HasRange && !string.IsNullOrEmpty(value) && TryFloat(value, out fv) && (fv < type.RangeMin || fv > type.RangeMax))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = value + " outside range " + type.RangeMin + ".." + type.RangeMax });
                    break;
                case ColumnBaseType.Bool:
                    if (!string.IsNullOrEmpty(value) && !ValueConverter.TryBool(value, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not a bool (true/false/1/0/是/否)" });
                    break;
                case ColumnBaseType.Enum:
                    ValidateEnum(set, loc, type, value, issues);
                    break;
                case ColumnBaseType.List:
                    if (!string.IsNullOrEmpty(value))
                    {
                        string[] items = value.Split(new[] { type.ListSeparator }, StringSplitOptions.None);
                        for (int i = 0; i < items.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(items[i]))
                                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "list has an empty element at position " + i });
                            else
                                ValidateCell(set, sheet, row, column, type.Element, items[i], issues);
                        }
                    }
                    break;
                case ColumnBaseType.Vector3:
                    if (!string.IsNullOrEmpty(value) && !ValueConverter.TryVector(value, 3, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not x,y,z" });
                    break;
                case ColumnBaseType.Vector2:
                    if (!string.IsNullOrEmpty(value) && !ValueConverter.TryVector(value, 2, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not x,y" });
                    break;
                case ColumnBaseType.Color:
                    if (!string.IsNullOrEmpty(value) && !ValueConverter.TryVector(value, 4, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not r,g,b,a" });
                    break;
                case ColumnBaseType.Curve:
                    if (!string.IsNullOrEmpty(value) && !ValueConverter.TryCurve(value, out _))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not time,value,inSlope,outSlope|…" });
                    break;
            }

            if (!string.IsNullOrEmpty(type.RefSheet) && !string.IsNullOrEmpty(value) && type.BaseType != ColumnBaseType.List)
            {
                DesignSheet target = set.Resolve(sheet.Workbook, type.RefWorkbook, type.RefSheet);
                if (target == null)
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "ref target sheet '" + (type.RefWorkbook ?? sheet.Workbook) + "/" + type.RefSheet + "' not found" });
                else if (target.FindColumn(type.RefField ?? (target.KeyColumns.Count > 0 ? target.KeyColumns[0].Var : string.Empty)) == null)
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "ref field '" + type.RefField + "' not found on " + type.RefSheet });
                else if (!set.KeyValues(target, type.RefField ?? target.KeyColumns[0].Var).Contains(value))
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' not found in " + type.RefSheet + "." + type.RefField });
            }

            if (type.IsPath && !string.IsNullOrEmpty(value) && !File.Exists(value) && !Directory.Exists(value))
                issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = loc, Message = "asset path '" + value + "' does not exist" });
        }

        private static void ValidateEnum(DesignTableSet set, string loc, TypeSpec type, string value, List<Issue> issues)
        {
            if (string.IsNullOrEmpty(value))
                return;
            if (!set.Enums.TryGetValue(type.EnumName, out HashSet<string> items))
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "enum '" + type.EnumName + "' is not defined in Global/__enums__" });
            else if (!items.Contains(value))
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = loc, Message = "'" + value + "' is not a member of enum " + type.EnumName });
        }

        private static bool TryInt(string s, out int v)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return true;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f == Math.Floor(f))
            {
                v = (int)f;
                return true;
            }
            v = 0;
            return false;
        }

        private static bool TryFloat(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
#endif
