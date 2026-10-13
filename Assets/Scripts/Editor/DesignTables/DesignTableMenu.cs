#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;

namespace BuddahGo.DesignTables
{
    public static class DesignTableMenu
    {
        [MenuItem("Tools/Design Tables/Import All", priority = 10)]
        public static void ImportAll()
        {
            DesignTableExporter.Result r = DesignTableExporter.Run(dryRun: false);
            EditorUtility.DisplayDialog("Design Tables",
                (r.Success ? "导入完成" : "导入失败，产物未写入") + "\n sheets " + r.SheetsExported + ", records " + r.RecordsExported + "\n errors " + r.ErrorCount + ", warnings " + r.WarningCount + "\n报告：" + DesignTableExporter.ReportPath, "OK");
        }

        [MenuItem("Tools/Design Tables/Validate Only", priority = 11)]
        public static void ValidateOnly()
        {
            DesignTableExporter.Run(dryRun: true);
        }

        [MenuItem("Tools/Design Tables/Open Report", priority = 12)]
        public static void OpenReport()
        {
            if (System.IO.File.Exists(DesignTableExporter.ReportPath))
                Process.Start(new ProcessStartInfo(System.IO.Path.GetFullPath(DesignTableExporter.ReportPath)) { UseShellExecute = true });
        }

        [MenuItem("Tools/Design Tables/Open Tables Folder", priority = 30)]
        public static void OpenFolder()
        {
            EditorUtility.RevealInFinder(DesignTableSet.TablesRoot);
        }

        [MenuItem("Tools/Design Tables/Auto Import On Save", priority = 50)]
        public static void ToggleAutoImport()
        {
            DesignTablePostprocessor.AutoImport = !DesignTablePostprocessor.AutoImport;
        }

        [MenuItem("Tools/Design Tables/Auto Import On Save", true)]
        public static bool ToggleAutoImportValidate()
        {
            Menu.SetChecked("Tools/Design Tables/Auto Import On Save", DesignTablePostprocessor.AutoImport);
            return true;
        }
    }
}
#endif
