#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Per-machine record of the workbook contents last imported successfully (Library/DesignTables/import-state.txt,
    /// one "Workbook<TAB>sha1" line per workbook). Lets an import skip workbooks whose bytes did not change.
    /// </summary>
    public sealed class DesignTableImportState
    {
        public const string StatePath = "Library/DesignTables/import-state.txt";
        public readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        public static DesignTableImportState Load()
        {
            DesignTableImportState state = new DesignTableImportState();
            if (!File.Exists(StatePath))
                return state;
            foreach (string line in File.ReadAllLines(StatePath))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0)
                    state.Hashes[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
            }
            return state;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in Hashes)
                sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            File.WriteAllText(StatePath, sb.ToString(), new UTF8Encoding(false));
        }

        public static string HashFile(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA1 sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>Workbooks whose current file hash differs from the last successful import (or were never imported).</summary>
        public HashSet<string> ChangedWorkbooks(DesignTableSet set, out Dictionary<string, string> currentHashes)
        {
            HashSet<string> changed = new HashSet<string>(StringComparer.Ordinal);
            currentHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kv in set.WorkbookPaths)
            {
                string hash = File.Exists(kv.Value) ? HashFile(kv.Value) : string.Empty;
                currentHashes[kv.Key] = hash;
                if (!Hashes.TryGetValue(kv.Key, out string previous) || previous != hash)
                    changed.Add(kv.Key);
            }
            return changed;
        }
    }
}
#endif
