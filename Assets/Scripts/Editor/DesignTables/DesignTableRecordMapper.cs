#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Fills record instances from sheet rows by matching ##var names to public fields (reflection).
    /// Text keys: a column "xxxKey" referencing UI/Text fills field "xxxKey" when it exists, otherwise
    /// the resolved zh text into field "xxx" (for hand-written records that store the text itself).
    /// </summary>
    public static class DesignTableRecordMapper
    {
        public static IList MapSheet(DesignTableSet set, DesignSheet sheet, Type recordType, Dictionary<string, string> texts, List<Issue> issues)
        {
            Type listType = typeof(List<>).MakeGenericType(recordType);
            IList list = (IList)Activator.CreateInstance(listType);
            Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo f in recordType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                fields[f.Name] = f;

            List<(ColumnSpec column, FieldInfo field, bool resolveText)> bindings = new List<(ColumnSpec, FieldInfo, bool)>();
            foreach (ColumnSpec column in sheet.Columns)
            {
                if (!column.IsExported)
                    continue;
                if (fields.TryGetValue(column.Var, out FieldInfo direct))
                {
                    bindings.Add((column, direct, false));
                    continue;
                }
                bool isTextRef = column.Type.RefSheet == "Text" && column.Var.EndsWith("Key", StringComparison.Ordinal);
                if (isTextRef && fields.TryGetValue(column.Var.Substring(0, column.Var.Length - 3), out FieldInfo resolved) && resolved.FieldType == typeof(string))
                {
                    bindings.Add((column, resolved, true));
                    continue;
                }
                issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = sheet.Workbook + ".xlsx/" + sheet.Name + "!" + XlsxReader.ColumnName(column.Index) + "1",
                    Message = "column '" + column.Var + "' has no field on " + recordType.Name + " (ignored; regenerate records with gen_records.py?)" });
            }

            foreach (DataRow row in sheet.Rows)
            {
                object record = Activator.CreateInstance(recordType);
                foreach ((ColumnSpec column, FieldInfo field, bool resolveText) in bindings)
                {
                    string raw = sheet.Cell(row, column).Trim();
                    try
                    {
                        if (resolveText)
                        {
                            if (string.IsNullOrEmpty(raw))
                                field.SetValue(record, string.Empty);
                            else if (texts != null && texts.TryGetValue(raw, out string text))
                                field.SetValue(record, text);
                            else
                            {
                                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = sheet.Location(row, column), Message = "text key '" + raw + "' not found in UI/Text" });
                                field.SetValue(record, raw);
                            }
                        }
                        else
                            field.SetValue(record, Convert(column.Type, field.FieldType, raw));
                    }
                    catch (Exception e)
                    {
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = sheet.Location(row, column), Message = "cannot convert '" + raw + "' to " + field.FieldType.Name + ": " + e.Message });
                    }
                }
                list.Add(record);
            }
            return list;
        }

        public static object Convert(TypeSpec type, Type fieldType, string raw)
        {
            if (fieldType == typeof(string))
                return raw ?? string.Empty;
            if (fieldType == typeof(int))
                return ValueConverter.ToInt(raw);
            if (fieldType == typeof(float))
                return ValueConverter.ToFloat(raw);
            if (fieldType == typeof(bool))
                return ValueConverter.TryBool(raw, out bool b) && b;
            if (fieldType == typeof(Vector3))
                return ValueConverter.ToVector3(raw);
            if (fieldType == typeof(Vector2))
                return ValueConverter.ToVector2(raw);
            if (fieldType == typeof(Color))
                return ValueConverter.ToColor(raw);
            if (fieldType == typeof(AnimationCurve))
                return ValueConverter.TryCurve(raw, out AnimationCurve c) ? c : new AnimationCurve();
            if (fieldType.IsEnum)
            {
                if (string.IsNullOrEmpty(raw))
                    return Activator.CreateInstance(fieldType);
                return int.TryParse(raw, out int iv) ? Enum.ToObject(fieldType, iv) : Enum.Parse(fieldType, raw, true);
            }
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elem = fieldType.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(fieldType);
                if (!string.IsNullOrEmpty(raw))
                {
                    string sep = type.BaseType == ColumnBaseType.List ? type.ListSeparator : "|";
                    foreach (string item in raw.Split(new[] { sep }, StringSplitOptions.None))
                        list.Add(Convert(type.Element ?? type, elem, item.Trim()));
                }
                return list;
            }
            if (fieldType.IsArray)
            {
                Type elem = fieldType.GetElementType();
                string[] items = string.IsNullOrEmpty(raw) ? new string[0] : raw.Split(new[] { type.ListSeparator }, StringSplitOptions.None);
                Array arr = Array.CreateInstance(elem, items.Length);
                for (int i = 0; i < items.Length; i++)
                    arr.SetValue(Convert(type.Element ?? type, elem, items[i].Trim()), i);
                return arr;
            }
            throw new NotSupportedException("unsupported field type " + fieldType.Name);
        }
    }
}
#endif
