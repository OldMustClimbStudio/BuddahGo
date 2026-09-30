using UnityEngine;

namespace SteamMultiplayer.Network
{
    /// <summary>
    /// Centralised logging helper for the Steam Multiplayer framework.
    ///
    /// Log levels:
    ///   - Info  : development-only informational messages
    ///   - Warn  : always shown (recoverable issues)
    ///   - Error : always shown (failures)
    ///   - Dev   : only shown when DEVELOPMENT_BUILD or UNITY_EDITOR is defined
    ///
    /// Usage:
    ///   NetLog.Info("Server started");
    ///   NetLog.Dev("Tick delta: " + delta);
    /// </summary>
    public static class NetLog
    {
        private const string TAG = "[SteamMP]";

        /// <summary>Standard informational message.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            GameLog.Verbose($"{TAG} {message}");
        }

        /// <summary>Warning – something unexpected but recoverable.</summary>
        public static void Warn(string message)
        {
            Debug.LogWarning($"{TAG} {message}");
        }

        /// <summary>Error – something that prevents correct operation.</summary>
        public static void Error(string message)
        {
            Debug.LogError($"{TAG} {message}");
        }

        /// <summary>
        /// Development-only log. Stripped from release builds automatically
        /// via conditional compilation so there is zero overhead in shipping builds.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Dev(string message)
        {
            GameLog.Verbose($"{TAG}[DEV] {message}");
        }
    }
}
