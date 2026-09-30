using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks movement components from Awake to OnDestroy, not OnEnable to OnDisable:
/// prediction can disable the legacy component while its player remains discoverable.
/// Queries exclude inactive GameObjects but include disabled components, matching
/// FindObjectsByType with FindObjectsInactive.Exclude. Callers retain their own
/// ownership/scene filters. No result is cached across ownership changes.
/// </summary>
public static class PlayerRegistry
{
    private static readonly List<BuddahMovement> Players = new List<BuddahMovement>();
    private static bool _seeded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Players.Clear();
        _seeded = false;
    }

    public static void Register(BuddahMovement player)
    {
        if (player != null && !Players.Contains(player))
            Players.Add(player);
    }

    public static void Unregister(BuddahMovement player)
    {
        Players.Remove(player);
    }

    // A first query can precede another component's Awake, and Enter Play Mode may
    // retain objects without another Awake. Seed once to preserve those cases.
    private static void EnsureSeeded()
    {
        if (_seeded)
            return;

        foreach (BuddahMovement player in Object.FindObjectsByType<BuddahMovement>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Register(player);

        _seeded = true;
    }

    public static void CopyActiveTo(List<BuddahMovement> destination)
    {
        EnsureSeeded();
        destination.Clear();
        for (int i = 0; i < Players.Count; i++)
        {
            BuddahMovement player = Players[i];
            if (player != null && player.gameObject.activeInHierarchy
                && (player.hideFlags & HideFlags.DontSave) == 0)
                destination.Add(player);
        }
    }
}