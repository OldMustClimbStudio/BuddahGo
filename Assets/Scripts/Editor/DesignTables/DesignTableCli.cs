#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BuddahGo.DesignTables
{
    /// <summary>
    /// Batch entry points for pre-commit hooks and CI:
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ImportAndValidate -quit
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ValidateOnly -quit
    /// Exit code 0 on success, 1 when any Error was reported.
    /// </summary>
    public static class DesignTableCli
    {
        public static void ImportAndValidate()
        {
            Exit(DesignTableExporter.Run(dryRun: false));
        }

        public static void ValidateOnly()
        {
            Exit(DesignTableExporter.Run(dryRun: true));
        }

        private static void Exit(DesignTableExporter.Result result)
        {
            Debug.Log("[DesignTables] CLI finished: " + (result.Success ? "OK" : "FAILED") + " errors=" + result.ErrorCount + " warnings=" + result.WarningCount + " report=" + DesignTableExporter.ReportPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(result.Success ? 0 : 1);
        }
    }
}
#endif
