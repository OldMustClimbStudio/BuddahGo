#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Saving a workbook in Excel makes Unity re-import the .xlsx under Assets/Design/Tables; that import
    /// schedules a debounced import. Excel writes through temp files and fires several events per save,
    /// and leaves a ~$ lock file, so both are filtered.
    /// </summary>
    public sealed class DesignTablePostprocessor : AssetPostprocessor
    {
        private const string AutoImportPref = "BuddahGo.DesignTables.AutoImport";
        private const double DebounceSeconds = 0.5;
        private static double _requestedAt = -1;

        public static bool AutoImport
        {
            get => EditorPrefs.GetBool(AutoImportPref, true);
            set => EditorPrefs.SetBool(AutoImportPref, value);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!AutoImport || Application.isBatchMode)
                return;
            bool hit = false;
            foreach (string path in importedAssets)
            {
                if (!path.StartsWith(DesignTableSet.TablesRoot + "/", StringComparison.Ordinal) || !path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    continue;
                string name = System.IO.Path.GetFileName(path);
                if (name.StartsWith("~$", StringComparison.Ordinal))
                    continue;
                hit = true;
            }
            if (!hit)
                return;

            if (_requestedAt < 0)
                EditorApplication.update += Pump;
            _requestedAt = EditorApplication.timeSinceStartup;
        }

        private static void Pump()
        {
            if (EditorApplication.timeSinceStartup - _requestedAt < DebounceSeconds)
                return;
            EditorApplication.update -= Pump;
            _requestedAt = -1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                // Try again once the editor settles.
                _requestedAt = EditorApplication.timeSinceStartup;
                EditorApplication.update += Pump;
                return;
            }
            try
            {
                DesignTableExporter.Run(dryRun: false);
            }
            catch (Exception e)
            {
                Debug.LogError("[DesignTables] auto import failed: " + e);
            }
        }
    }
}
#endif
