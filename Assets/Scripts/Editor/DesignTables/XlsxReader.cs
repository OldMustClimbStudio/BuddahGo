#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Minimal .xlsx reader built on the zip + XML structure of the file. No third-party DLLs.
    /// Reads every sheet into a dense string grid (cached values for formulas), which is all the
    /// design-table pipeline needs. Shared strings, inline strings, booleans and numbers are supported.
    /// </summary>
    public static class XlsxReader
    {
        public sealed class Sheet
        {
            public string Name;
            /// <summary>Row-major grid; rows and columns are 0-based, missing cells are empty strings.</summary>
            public List<string[]> Rows = new List<string[]>();
            public int ColumnCount;
        }

        public static List<Sheet> Read(string path)
        {
            // FileShare.ReadWrite so an open Excel instance does not block the import.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(zip);
                Dictionary<string, string> rels = ReadWorkbookRels(zip);
                List<Sheet> sheets = new List<Sheet>();
                ZipArchiveEntry workbook = zip.GetEntry("xl/workbook.xml");
                if (workbook == null)
                    throw new InvalidDataException("xl/workbook.xml missing: " + path);

                using (XmlReader reader = XmlReader.Create(workbook.Open(), XmlSettings()))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet")
                            continue;

                        string name = reader.GetAttribute("name");
                        string rid = reader.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                        if (string.IsNullOrEmpty(rid) || !rels.TryGetValue(rid, out string target))
                            continue;

                        string entryPath = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
                        ZipArchiveEntry entry = zip.GetEntry(entryPath);
                        if (entry == null)
                            continue;

                        Sheet sheet = new Sheet { Name = name };
                        ReadSheet(entry, sharedStrings, sheet);
                        sheets.Add(sheet);
                    }
                }

                return sheets;
            }
        }

        private static XmlReaderSettings XmlSettings()
        {
            return new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true, DtdProcessing = DtdProcessing.Prohibit };
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            List<string> result = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return result;

            using (XmlReader reader = XmlReader.Create(entry.Open(), XmlSettings()))
            {
                StringBuilder current = null;
                bool advance = true;
                while (!advance || reader.Read())
                {
                    advance = true;
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                    {
                        current = new StringBuilder();
                        if (reader.IsEmptyElement)
                        {
                            result.Add(string.Empty);
                            current = null;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && current != null)
                    {
                        // ReadElementContentAsString leaves the reader on the node after </t>; do not Read() again.
                        current.Append(reader.ReadElementContentAsString());
                        advance = false;
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si" && current != null)
                    {
                        result.Add(current.ToString());
                        current = null;
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, string> ReadWorkbookRels(ZipArchive zip)
        {
            Dictionary<string, string> rels = new Dictionary<string, string>(StringComparer.Ordinal);
            ZipArchiveEntry entry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (entry == null)
                return rels;

            using (XmlReader reader = XmlReader.Create(entry.Open(), XmlSettings()))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
                        rels[reader.GetAttribute("Id")] = reader.GetAttribute("Target");
                }
            }

            return rels;
        }

        private static void ReadSheet(ZipArchiveEntry entry, List<string> sharedStrings, Sheet sheet)
        {
            List<Dictionary<int, string>> sparseRows = new List<Dictionary<int, string>>();
            int maxColumn = -1;
            using (XmlReader reader = XmlReader.Create(entry.Open(), XmlSettings()))
            {
                Dictionary<int, string> row = null;
                int rowIndex = -1;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                    {
                        string r = reader.GetAttribute("r");
                        rowIndex = r != null ? int.Parse(r, CultureInfo.InvariantCulture) - 1 : rowIndex + 1;
                        while (sparseRows.Count <= rowIndex)
                            sparseRows.Add(null);
                        row = new Dictionary<int, string>();
                        sparseRows[rowIndex] = row;
                        if (reader.IsEmptyElement)
                            row = null;
                    }
                    else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c" && row != null)
                    {
                        string cellRef = reader.GetAttribute("r");
                        string type = reader.GetAttribute("t");
                        int column = ColumnIndex(cellRef);
                        string value = ReadCellValue(reader, type, sharedStrings);
                        if (column >= 0)
                        {
                            row[column] = value;
                            if (column > maxColumn)
                                maxColumn = column;
                        }
                    }
                }
            }

            sheet.ColumnCount = maxColumn + 1;
            for (int i = 0; i < sparseRows.Count; i++)
            {
                string[] dense = new string[sheet.ColumnCount];
                for (int c = 0; c < dense.Length; c++)
                    dense[c] = string.Empty;
                if (sparseRows[i] != null)
                {
                    foreach (KeyValuePair<int, string> kv in sparseRows[i])
                        dense[kv.Key] = kv.Value ?? string.Empty;
                }
                sheet.Rows.Add(dense);
            }
        }

        private static string ReadCellValue(XmlReader reader, string type, List<string> sharedStrings)
        {
            if (reader.IsEmptyElement)
                return string.Empty;

            string v = null;
            string inline = null;
            int depth = reader.Depth;
            if (!reader.Read())
                return string.Empty;
            // Stop on </c>; ReadElementContentAsString already advances past </v> or </t>, so only Read() otherwise.
            while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "v")
                    v = reader.ReadElementContentAsString();
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
                    inline = (inline ?? string.Empty) + reader.ReadElementContentAsString();
                else if (!reader.Read())
                    break;
            }

            switch (type)
            {
                case "s":
                    if (v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < sharedStrings.Count)
                        return sharedStrings[index];
                    return string.Empty;
                case "inlineStr":
                    return inline ?? string.Empty;
                case "b":
                    return v == "1" ? "true" : "false";
                case "e":
                    return string.Empty;
                default:
                    return v ?? inline ?? string.Empty;
            }
        }

        /// <summary>"B7" → 1, "AA3" → 26.</summary>
        public static int ColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return -1;
            int index = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char ch = cellRef[i];
                if (ch >= 'A' && ch <= 'Z')
                    index = index * 26 + (ch - 'A' + 1);
                else
                    break;
            }
            return index - 1;
        }

        /// <summary>0 → "A", 26 → "AA". Used for error locations.</summary>
        public static string ColumnName(int index)
        {
            StringBuilder sb = new StringBuilder();
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                index = (index - 1) / 26;
            }
            return sb.ToString();
        }
    }
}
#endif
