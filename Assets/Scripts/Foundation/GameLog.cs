using System.Diagnostics;
using UnityEngine;

/// <summary>Informational logs and their arguments are omitted from non-development players.</summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Verbose(object message) => UnityEngine.Debug.Log(message);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Verbose(object message, Object context) => UnityEngine.Debug.Log(message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Verbose(string tag, string message) => UnityEngine.Debug.Log($"[{tag}] {message}");
}