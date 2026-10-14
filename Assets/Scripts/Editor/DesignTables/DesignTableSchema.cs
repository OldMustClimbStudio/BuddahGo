#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace BuddahGo.DesignTables
{
    public enum ColumnBaseType
    {
        Int,
        Float,
        Bool,
        String,
        Text,
        Enum,
        List,
        Vector3,
        Vector2,
        Color,
        Curve
    }

    /// <summary>Parsed form of one ##type cell, e.g. "string#key#ref=SkillDefinition.skillId" or "list,enum:ComboToken#sep=,".</summary>
    public sealed class TypeSpec
    {
        public string Raw = string.Empty;
        public ColumnBaseType BaseType;
        /// <summary>Element type for lists.</summary>
        public TypeSpec Element;
        public string EnumName;
        public bool IsKey;
        public string RefWorkbook;
        public string RefSheet;
        public string RefField;
        public bool HasRange;
        public float RangeMin;
        public float RangeMax;
        public string ListSeparator = "|";
        public bool IsPath;

        public static TypeSpec Parse(string raw)
        {
            TypeSpec spec = new TypeSpec { Raw = raw ?? string.Empty };
            string[] parts = spec.Raw.Split('#');
            string head = parts[0].Trim();
            for (int i = 1; i < parts.Length; i++)
            {
                string tag = parts[i].Trim();
                if (tag == "key")
                    spec.IsKey = true;
                else if (tag == "path")
                    spec.IsPath = true;
                else if (tag.StartsWith("ref=", StringComparison.Ordinal))
                    ParseRef(tag.Substring(4), spec);
                else if (tag.StartsWith("range=", StringComparison.Ordinal))
                {
                    string[] r = tag.Substring(6).Split(',');
                    if (r.Length == 2 && float.TryParse(r[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spec.RangeMin)
                        && float.TryParse(r[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spec.RangeMax))
                        spec.HasRange = true;
                }
                else if (tag.StartsWith("sep=", StringComparison.Ordinal))
                    spec.ListSeparator = tag.Substring(4);
            }

            if (head.StartsWith("list,", StringComparison.Ordinal))
            {
                spec.BaseType = ColumnBaseType.List;
                spec.Element = Parse(head.Substring(5));
                return spec;
            }
            if (head.StartsWith("enum:", StringComparison.Ordinal))
            {
                spec.BaseType = ColumnBaseType.Enum;
                spec.EnumName = head.Substring(5);
                return spec;
            }
            switch (head)
            {
                case "int": spec.BaseType = ColumnBaseType.Int; break;
                case "float": spec.BaseType = ColumnBaseType.Float; break;
                case "bool": spec.BaseType = ColumnBaseType.Bool; break;
                case "text": spec.BaseType = ColumnBaseType.Text; break;
                case "vector3": spec.BaseType = ColumnBaseType.Vector3; break;
                case "vector2": spec.BaseType = ColumnBaseType.Vector2; break;
                case "color": spec.BaseType = ColumnBaseType.Color; break;
                case "curve": spec.BaseType = ColumnBaseType.Curve; break;
                default: spec.BaseType = ColumnBaseType.String; break;
            }
            return spec;
        }

        private static void ParseRef(string target, TypeSpec spec)
        {
            // "Sheet.field" or "Workbook/Sheet.field"
            int slash = target.IndexOf('/');
            if (slash >= 0)
            {
                spec.RefWorkbook = target.Substring(0, slash);
                target = target.Substring(slash + 1);
            }
            int dot = target.LastIndexOf('.');
            if (dot > 0)
            {
                spec.RefSheet = target.Substring(0, dot);
                spec.RefField = target.Substring(dot + 1);
            }
            else
                spec.RefSheet = target;
        }
    }

    public sealed class ColumnSpec
    {
        public int Index;
        public string Var = string.Empty;
        public TypeSpec Type;
        public string Group = string.Empty;
        public string Comment = string.Empty;
        public bool IsDoc => Group == "doc";
        public bool IsExported => !IsDoc && !string.IsNullOrEmpty(Var) && !Var.StartsWith("#", StringComparison.Ordinal);
    }

    /// <summary>One data sheet: header specs plus data rows (header rows stripped, commented rows removed).</summary>
    public sealed class DesignSheet
    {
        public string Workbook = string.Empty;
        public string Name = string.Empty;
        public string SourcePath = string.Empty;
        public List<ColumnSpec> Columns = new List<ColumnSpec>();
        /// <summary>Data rows; each row keeps the original 1-based Excel row number for error locations.</summary>
        public List<DataRow> Rows = new List<DataRow>();
        public List<ColumnSpec> KeyColumns = new List<ColumnSpec>();

        public ColumnSpec FindColumn(string var)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Var == var)
                    return Columns[i];
            }
            return null;
        }

        public string Cell(DataRow row, ColumnSpec column)
        {
            return column.Index < row.Cells.Length ? row.Cells[column.Index] : string.Empty;
        }

        public string Location(DataRow row, ColumnSpec column)
        {
            return Workbook + ".xlsx/" + Name + "!" + XlsxReader.ColumnName(column.Index) + row.ExcelRow;
        }

        /// <summary>Parse the ##var/##type/##group/##comment rows. Returns null when A1 does not start with "##" (sheet ignored).</summary>
        public static DesignSheet Parse(string workbook, string sourcePath, XlsxReader.Sheet raw)
        {
            if (raw.Rows.Count == 0 || raw.Rows[0].Length == 0 || !raw.Rows[0][0].StartsWith("##", StringComparison.Ordinal))
                return null;

            DesignSheet sheet = new DesignSheet { Workbook = workbook, Name = raw.Name, SourcePath = sourcePath };
            string[] varRow = null, typeRow = null, groupRow = null, commentRow = null;
            int firstDataRow = 0;
            for (int r = 0; r < raw.Rows.Count; r++)
            {
                string marker = raw.Rows[r].Length > 0 ? raw.Rows[r][0] : string.Empty;
                if (!marker.StartsWith("##", StringComparison.Ordinal))
                {
                    firstDataRow = r;
                    break;
                }
                if (marker == "##var") varRow = raw.Rows[r];
                else if (marker == "##type") typeRow = raw.Rows[r];
                else if (marker == "##group") groupRow = raw.Rows[r];
                else if (marker == "##comment" || (marker == "##" && commentRow == null)) commentRow = raw.Rows[r];
                firstDataRow = r + 1;
            }

            if (varRow == null || typeRow == null)
                throw new InvalidOperationException(workbook + ".xlsx/" + raw.Name + ": header rows ##var and ##type are required");

            for (int c = 1; c < varRow.Length; c++)
            {
                string var = varRow[c].Trim();
                if (string.IsNullOrEmpty(var))
                    continue;
                ColumnSpec column = new ColumnSpec
                {
                    Index = c,
                    Var = var,
                    Type = TypeSpec.Parse(c < typeRow.Length ? typeRow[c] : string.Empty),
                    Group = groupRow != null && c < groupRow.Length ? groupRow[c].Trim() : string.Empty,
                    Comment = commentRow != null && c < commentRow.Length ? commentRow[c] : string.Empty
                };
                sheet.Columns.Add(column);
                if (column.Type.IsKey)
                    sheet.KeyColumns.Add(column);
            }

            for (int r = firstDataRow; r < raw.Rows.Count; r++)
            {
                string[] cells = raw.Rows[r];
                if (cells.Length > 0 && cells[0].StartsWith("##", StringComparison.Ordinal))
                    continue;
                bool empty = true;
                for (int i = 0; i < sheet.Columns.Count && empty; i++)
                {
                    int idx = sheet.Columns[i].Index;
                    if (idx < cells.Length && !string.IsNullOrWhiteSpace(cells[idx]))
                        empty = false;
                }
                if (empty)
                    continue;
                sheet.Rows.Add(new DataRow { ExcelRow = r + 1, Cells = cells });
            }

            return sheet;
        }
    }

    public sealed class DataRow
    {
        public int ExcelRow;
        public string[] Cells;
    }
}
#endif
