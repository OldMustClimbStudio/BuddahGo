#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace BuddahGo.DesignTables
{
    public static class DesignTableMenu
    {
        [MenuItem("Tools/Design Tables/Import Changed", priority = 10)]
        public static void ImportChanged()
        {
            Show(DesignTableExporter.Run(new DesignTableExporter.Options()));
        }

        [MenuItem("Tools/Design Tables/Import All (Force)", priority = 11)]
        public static void ImportAll()
        {
            Show(DesignTableExporter.Run(new DesignTableExporter.Options { Force = true }));
        }

        [MenuItem("Tools/Design Tables/Validate Only", priority = 12)]
        public static void ValidateOnly()
        {
            DesignTableExporter.Run(new DesignTableExporter.Options { DryRun = true, Force = true });
        }

        [MenuItem("Tools/Design Tables/Import Tag/balance", priority = 20)] public static void TagBalance() => ImportTag("balance");
        [MenuItem("Tools/Design Tables/Import Tag/info", priority = 21)] public static void TagInfo() => ImportTag("info");
        [MenuItem("Tools/Design Tables/Import Tag/text", priority = 22)] public static void TagText() => ImportTag("text");
        [MenuItem("Tools/Design Tables/Import Tag/track", priority = 23)] public static void TagTrack() => ImportTag("track");
        [MenuItem("Tools/Design Tables/Import Tag/skill", priority = 30)] public static void TagSkill() => ImportTag("skill");
        [MenuItem("Tools/Design Tables/Import Tag/movement", priority = 31)] public static void TagMovement() => ImportTag("movement");
        [MenuItem("Tools/Design Tables/Import Tag/ai", priority = 32)] public static void TagAi() => ImportTag("ai");
        [MenuItem("Tools/Design Tables/Import Tag/match", priority = 33)] public static void TagMatch() => ImportTag("match");
        [MenuItem("Tools/Design Tables/Import Tag/ui", priority = 34)] public static void TagUi() => ImportTag("ui");
        [MenuItem("Tools/Design Tables/Import Tag/global", priority = 35)] public static void TagGlobal() => ImportTag("global");

        // Tag imports always re-map the tagged sheets (Force) so a designer can push one layer regardless of hashes.
        private static void ImportTag(string tag)
        {
            Show(DesignTableExporter.Run(new DesignTableExporter.Options { Force = true, Tags = new HashSet<string> { tag } }));
        }

        private static void Show(DesignTableExporter.Result r)
        {
            string body = r.NothingChanged
                ? "没有工作簿发生变化，无需导入。"
                : (r.Success ? "导入完成" : "导入失败，产物未写入") + "\n re-mapped " + r.SheetsExported + ", kept " + r.SheetsKept + ", records " + r.RecordsExported
                  + "\n changed: " + (r.ChangedWorkbooks.Count == 0 ? "(none)" : string.Join(", ", r.ChangedWorkbooks))
                  + "\n errors " + r.ErrorCount + ", warnings " + r.WarningCount;
            EditorUtility.DisplayDialog("Design Tables", body + "\n报告：" + DesignTableExporter.ReportPath, "OK");
        }

        [MenuItem("Tools/Design Tables/Open Report", priority = 50)]
        public static void OpenReport()
        {
            if (System.IO.File.Exists(DesignTableExporter.ReportPath))
                Process.Start(new ProcessStartInfo(System.IO.Path.GetFullPath(DesignTableExporter.ReportPath)) { UseShellExecute = true });
        }

        [MenuItem("Tools/Design Tables/Open Tables Folder", priority = 51)]
        public static void OpenFolder()
        {
            EditorUtility.RevealInFinder(DesignTableSet.TablesRoot);
        }

        [MenuItem("Tools/Design Tables/Auto Import On Save", priority = 70)]
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
