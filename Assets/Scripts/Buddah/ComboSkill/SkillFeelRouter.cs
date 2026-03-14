using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

public class SkillFeelRouter : MonoBehaviour
{
    [System.Serializable]
    private class FeelEntry
    {
        public string eventId;
        public MonoBehaviour feedbackPlayer;
    }

    [Header("Configured Feedback Players")]
    [SerializeField] private FeelEntry[] entries;

    private readonly Dictionary<string, MonoBehaviour> _playersByEventId = new();
    private readonly HashSet<MonoBehaviour> _initializedPlayers = new();
    private const BindingFlags InstanceAnyVisibility = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private void Awake()
    {
        RebuildCache();
    }

    public void Play(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        if (_playersByEventId.Count == 0)
            RebuildCache();

        if (!_playersByEventId.TryGetValue(eventId, out MonoBehaviour player) || player == null)
        {
            Debug.LogWarning($"[SkillFeelRouter] No feedback player configured for event '{eventId}'.", this);
            return;
        }

        if (!IsSceneBacked(player))
        {
            Debug.LogError(
                $"[SkillFeelRouter] Event '{eventId}' is mapped to a feedback player that is not a scene instance. " +
                $"Player='{DescribePlayer(player)}'. Make sure the router references the MMF_Player on the character instance/prefab hierarchy, not a prefab asset.",
                player);
            return;
        }

        EnsureInitialized(player);

        if (!TryInvoke(player, "PlayFeedbacks") &&
            !TryInvoke(player, "Play") &&
            !TryInvoke(player, "PlayFeedbacks", true))
        {
            Debug.LogWarning($"[SkillFeelRouter] Feedback player '{player.GetType().Name}' does not expose a supported play method for event '{eventId}'.", player);
        }
    }

    private void RebuildCache()
    {
        _playersByEventId.Clear();
        _initializedPlayers.Clear();

        if (entries == null)
            return;

        foreach (FeelEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.eventId) || entry.feedbackPlayer == null)
                continue;

            _playersByEventId[entry.eventId] = entry.feedbackPlayer;
        }
    }

    private void EnsureInitialized(MonoBehaviour player)
    {
        if (player == null || _initializedPlayers.Contains(player))
            return;

        bool initialized =
            TryInvoke(player, "Initialization") ||
            TryInvoke(player, "Initialize") ||
            TryInvoke(player, "Initializaton");

        if (initialized)
            _initializedPlayers.Add(player);
    }

    private static bool IsSceneBacked(MonoBehaviour player)
    {
        if (player == null)
            return false;

        return player.gameObject.scene.IsValid() && player.gameObject.scene.isLoaded;
    }

    private static string DescribePlayer(MonoBehaviour player)
    {
        if (player == null)
            return "null";

        var builder = new StringBuilder();
        builder.Append(player.GetType().Name);
        builder.Append(" on ");
        builder.Append(GetHierarchyPath(player.transform));
        builder.Append(", sceneValid=");
        builder.Append(player.gameObject.scene.IsValid());
        builder.Append(", sceneLoaded=");
        builder.Append(player.gameObject.scene.isLoaded);
        builder.Append(", sceneName='");
        builder.Append(player.gameObject.scene.name);
        builder.Append("'");
        return builder.ToString();
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "<no-transform>";

        var names = new List<string>();
        Transform current = target;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static bool TryInvoke(MonoBehaviour player, string methodName, params object[] args)
    {
        MethodInfo method = player.GetType().GetMethod(methodName, InstanceAnyVisibility, null, GetArgumentTypes(args), null);
        if (method == null)
            return false;

        try
        {
            method.Invoke(player, args);
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogError($"[SkillFeelRouter] Failed invoking '{methodName}' on {DescribePlayer(player)}.\n{ex.InnerException}", player);
            return false;
        }

        return true;
    }

    private static System.Type[] GetArgumentTypes(object[] args)
    {
        if (args == null || args.Length == 0)
            return System.Type.EmptyTypes;

        var types = new System.Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            types[i] = args[i]?.GetType() ?? typeof(object);

        return types;
    }
}
