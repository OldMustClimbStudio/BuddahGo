using UnityEngine;

public class RaceMapUIBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject uiPrefab;

    private static GameObject _spawnedUi;
    private static bool _spawnedFromBootstrap;

    private void Start()
    {
        if (_spawnedUi != null)
            return;

        if (uiPrefab == null)
        {
            Debug.LogWarning("[RaceMapUIBootstrap] UI prefab is not assigned.");
            return;
        }

        GameObject existingUi = FindExistingUiInScene();
        if (existingUi != null)
        {
            _spawnedUi = existingUi;
            _spawnedFromBootstrap = false;
            return;
        }

        _spawnedUi = Instantiate(uiPrefab);
        _spawnedUi.name = uiPrefab.name;
        _spawnedFromBootstrap = true;
    }

    private void OnDestroy()
    {
        if (_spawnedUi == null)
            return;

        if (_spawnedFromBootstrap && _spawnedUi.scene == gameObject.scene)
            _spawnedUi = null;
    }

    private GameObject FindExistingUiInScene()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
                continue;

            GameObject candidate = canvas.gameObject;
            if (candidate == null)
                continue;

            if (candidate.scene != gameObject.scene)
                continue;

            if (candidate == gameObject)
                continue;

            if (candidate.name == uiPrefab.name || candidate.GetComponent<SkillUiSpriteLibrary>() != null)
                return candidate;
        }

        return null;
    }
}
