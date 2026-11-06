using System.Collections.Generic;
using NewBuddah.PredictionV2.Visual;
using UnityEngine;

/// <summary>
/// 占位，等禅足冰封正式特效: the ice block on a frozen racer (players, AI, training dummies), every peer. Shown the
/// moment <see cref="RacerSkillPresentationState.FrozenChanged"/> reports true, around the racer's rendered body
/// (its body collider's bounds taken then, so a giant gets a bigger block), and melts away in <see cref="MeltSeconds"/> on the thaw.
/// Follows the visual root. Added next to the presentation state by SkillExecutor; never touches game state.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrozenIceShellPlaceholder : MonoBehaviour
{
    private const float MeltSeconds = 0.25f;
    private const float Padding = 1.06f;

    private static Mesh _mesh;

    private RacerSkillPresentationState _state;
    private GameObject _shell;
    private Material _material;
    private float _meltLeft;

    internal static void Attach(GameObject racer, RacerSkillPresentationState state)
    {
        if (racer == null || state == null)
            return;
        if (!racer.TryGetComponent(out FrozenIceShellPlaceholder shell))
            shell = racer.AddComponent<FrozenIceShellPlaceholder>();
        shell.Bind(state);
    }

    private void Bind(RacerSkillPresentationState state)
    {
        if (_state == state)
            return;
        if (_state != null)
            _state.FrozenChanged -= OnFrozenChanged;
        _state = state;
        if (isActiveAndEnabled)
            _state.FrozenChanged += OnFrozenChanged;
    }

    private void OnEnable()
    {
        if (_state != null)
        {
            _state.FrozenChanged -= OnFrozenChanged;
            _state.FrozenChanged += OnFrozenChanged;
        }
    }

    private void OnDisable()
    {
        if (_state != null)
            _state.FrozenChanged -= OnFrozenChanged;
        Hide();
    }

    private void OnDestroy()
    {
        if (_shell != null)
            Destroy(_shell);
        if (_material != null)
            Destroy(_material);
    }

    private void OnFrozenChanged(bool frozen)
    {
        if (frozen)
            Show();
        else if (_shell != null && _shell.activeSelf)
            _meltLeft = MeltSeconds;
    }

    private void Update()
    {
        if (_meltLeft <= 0f || _shell == null)
            return;
        _meltLeft -= Time.deltaTime;
        if (_meltLeft <= 0f)
        {
            Hide();
            return;
        }
        _material.color = new Color(1f, 1f, 1f, _meltLeft / MeltSeconds);
    }

    private void Show()
    {
        if (!FrostPlaceholder.Enabled || !TryGetBodyBounds(out Bounds bounds, out Transform anchor))
            return;
        if (_shell == null && !Build())
            return;
        _meltLeft = 0f;
        _material.color = Color.white;
        _shell.transform.SetParent(null, false);
        _shell.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        _shell.transform.localScale = bounds.size * Padding;
        _shell.transform.SetParent(anchor, true);
        _shell.SetActive(true);
    }

    private void Hide()
    {
        _meltLeft = 0f;
        if (_shell != null)
            _shell.SetActive(false);
    }

    private bool Build()
    {
        _material = FrostPlaceholder.NewMaterial("FrozenIceShellPlaceholder");
        if (_material == null)
            return false;
        if (_mesh == null)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            FrostPlaceholder.BuildIceBlock(vertices, colors, triangles);
            _mesh = FrostPlaceholder.ToMesh("FrozenIceShellPlaceholder", vertices, colors, triangles);
        }
        _shell = FrostPlaceholder.NewVisual("FrozenIceShellPlaceholder (占位)", _mesh, _material);
        _shell.SetActive(false);
        return true;
    }

    // The racer's body: its solid (non-trigger) collider, which scales with the giant; the rendered meshes without one.
    // The skinned model's bounds also cover the hands' reach, which made the block swallow the neighbours.
    private bool TryGetBodyBounds(out Bounds bounds, out Transform anchor)
    {
        var bridge = GetComponent<BuddahPredictionVisualRootBridge>();
        anchor = bridge != null && bridge.GetVisualRoot() != null ? bridge.GetVisualRoot() : transform;
        foreach (Collider body in GetComponents<Collider>())
        {
            if (body.enabled && !body.isTrigger)
            {
                bounds = body.bounds;
                return true;
            }
        }
        bounds = default;
        bool any = false;
        foreach (Renderer r in anchor.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || (_shell != null && r.gameObject == _shell) || !(r is SkinnedMeshRenderer || r is MeshRenderer))
                continue;
            if (any)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            any = true;
        }
        return any;
    }
}
