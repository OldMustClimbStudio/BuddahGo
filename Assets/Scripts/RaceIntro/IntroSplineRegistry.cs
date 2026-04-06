using System.Collections.Generic;
using UnityEngine;

public class IntroSplineRegistry : MonoBehaviour
{
    [SerializeField] private SplineIntroPath[] splinePaths;

    private readonly Dictionary<string, SplineIntroPath> _pathById = new Dictionary<string, SplineIntroPath>();

    private void Awake()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    public SplineIntroPath GetPathById(string splineId)
    {
        if (string.IsNullOrWhiteSpace(splineId))
            return null;

        if (_pathById.Count == 0)
            RebuildCache();

        _pathById.TryGetValue(splineId, out SplineIntroPath path);
        return path;
    }

    private void RebuildCache()
    {
        _pathById.Clear();
        if (splinePaths == null || splinePaths.Length == 0)
            splinePaths = FindObjectsByType<SplineIntroPath>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < splinePaths.Length; i++)
        {
            SplineIntroPath path = splinePaths[i];
            if (path == null || string.IsNullOrWhiteSpace(path.SplineId))
                continue;

            _pathById[path.SplineId] = path;
        }
    }
}
