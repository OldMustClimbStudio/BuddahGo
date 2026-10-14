#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Batch entry points for pre-commit hooks and CI:
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ImportAndValidate -quit [-designForce] [-designTags skill,ai]
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ValidateOnly -quit
    /// Without -designForce only workbooks whose bytes changed since the last successful import are re-mapped.
    /// Exit code 0 on success, 1 when any Error was reported.
    /// </summary>
    public static class DesignTableCli
    {
        public static void ImportAndValidate()
        {
            Exit(DesignTableExporter.Run(ParseOptions(dryRun: false)));
        }

        public static void ValidateOnly()
        {
            DesignTableExporter.Options options = ParseOptions(dryRun: true);
            options.Force = true;
            Exit(DesignTableExporter.Run(options));
        }

        /// <summary>
        /// Probe of the "save in Excel → Unity re-imports the xlsx → auto import" path without an interactive editor:
        /// re-import Skill.xlsx through the AssetDatabase so the AssetPostprocessor callback queues it, then run the
        /// queued import. Exit code 0 when the callback fired and the import re-mapped the Skill sheets and wrote outputs.
        /// </summary>
        public static void ProbeAutoImport()
        {
            DesignTablePostprocessor.AllowInBatchMode = true;
            string path = DesignTableSet.TablesRoot + "/Skill/Skill.xlsx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            bool scheduled = DesignTablePostprocessor.PendingWorkbooks.Contains("Skill");
            DesignTableExporter.Result result = scheduled ? DesignTablePostprocessor.RunPendingNow() : null;
            bool ok = scheduled && result != null && result.Success && result.OutputsWritten && result.RemappedSheets.Contains("Skill/Skill");
            Debug.Log("[DesignTables] auto-import probe: callback=" + scheduled + " remapped=" + (result != null ? result.SheetsExported : 0)
                + " kept=" + (result != null ? result.SheetsKept : 0) + " written=" + (result != null && result.OutputsWritten) + " -> " + (ok ? "OK" : "FAILED"));
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        private static DesignTableExporter.Options ParseOptions(bool dryRun)
        {
            DesignTableExporter.Options options = new DesignTableExporter.Options { DryRun = dryRun };
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-designForce", StringComparison.OrdinalIgnoreCase))
                    options.Force = true;
                else if (string.Equals(args[i], "-designTags", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    options.Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string tag in args[i + 1].Split(','))
                        if (!string.IsNullOrWhiteSpace(tag)) options.Tags.Add(tag.Trim());
                    options.Force = true;
                }
            }
            return options;
        }

        private static void Exit(DesignTableExporter.Result result)
        {
            Debug.Log("[DesignTables] CLI finished: " + (result.Success ? "OK" : "FAILED") + (result.NothingChanged ? " (nothing changed)" : string.Empty)
                + " errors=" + result.ErrorCount + " warnings=" + result.WarningCount + " report=" + DesignTableExporter.ReportPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(result.Success ? 0 : 1);
        }
    }
}
#endif
