using UnityEngine;

public class RaceMapUIBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject uiPrefab;

    private static GameObject _spawnedUi;
    private static bool _spawnedFromBootstrap;

    private void Start()
    {
        if (_spawnedUi != null)
        {
            EnsureRuntimeHud(_spawnedUi);
            return;
        }

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
            EnsureRuntimeHud(_spawnedUi);
            return;
        }

        _spawnedUi = Instantiate(uiPrefab);
        _spawnedUi.name = uiPrefab.name;
        _spawnedFromBootstrap = true;
        EnsureRuntimeHud(_spawnedUi);
    }

    // HUD parts that exist only at runtime (A6, no prefab edit): the caster's 缘起 toast on the HUD canvas, and the
    // placeholder mouse-aim ground marker (it draws its own world-space object, not under the canvas).
    private static void EnsureRuntimeHud(GameObject hud)
    {
        if (hud == null)
            return;
        if (hud.GetComponent<YuanqiToastUI>() == null)
            hud.AddComponent<YuanqiToastUI>();
        if (hud.GetComponent<AimGroundIndicator>() == null)
            hud.AddComponent<AimGroundIndicator>();
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
