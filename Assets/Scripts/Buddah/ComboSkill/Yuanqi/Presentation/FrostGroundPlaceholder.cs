using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 占位，等禅足冰封正式特效: #2's frozen ground on every peer. A flat white frost disc of the pair's radius R on the
/// ground under the trigger centre: spreads out in <see cref="SpreadSeconds"/> (the Notion "瞬间生成"), holds while the
/// freeze lasts (N), then fades. Spawned by Yuanqi_FrostLock.PlayObservers; never touches game state. Flat: on a slope
/// part of it floats or sinks (the project has no URP decal feature yet).
/// </summary>
public sealed class FrostGroundPlaceholder : MonoBehaviour
{
    private const float SpreadSeconds = 0.12f;
    private const float FadeSeconds = 0.45f;
    private const float StartScale = 0.15f;
    private const float GroundLift = 0.06f;
    private const float GroundProbeUp = 4f;
    private const float GroundProbeDown = 30f;

    private static Mesh _mesh;

    private Material _material;
    private float _radius;
    private float _holdSeconds;
    private float _age;

    public static FrostGroundPlaceholder Spawn(Vector3 centre, float radius, float holdSeconds)
    {
        if (!FrostPlaceholder.Enabled || radius <= 0f)
            return null;
        Material material = FrostPlaceholder.NewMaterial("FrostGroundPlaceholder");
        if (material == null)
            return null;
        if (_mesh == null)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            FrostPlaceholder.BuildGroundDisc(vertices, colors, triangles);
            _mesh = FrostPlaceholder.ToMesh("FrostGroundPlaceholder", vertices, colors, triangles);
        }

        GameObject visual = FrostPlaceholder.NewVisual("FrostGroundPlaceholder (占位)", _mesh, material);
        visual.transform.position = new Vector3(centre.x, GroundHeight(centre) + GroundLift, centre.z);
        var placeholder = visual.AddComponent<FrostGroundPlaceholder>();
        placeholder._material = material;
        placeholder._radius = radius;
        placeholder._holdSeconds = Mathf.Max(0f, holdSeconds);
        placeholder.Apply();
        return placeholder;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _holdSeconds + FadeSeconds)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    private void Apply()
    {
        float spread = SpreadSeconds > 0f ? Mathf.Clamp01(_age / SpreadSeconds) : 1f;
        float scale = Mathf.Lerp(StartScale, 1f, 1f - (1f - spread) * (1f - spread)) * _radius;
        transform.localScale = new Vector3(scale, 1f, scale);
        float alpha = _age <= _holdSeconds ? 1f : 1f - Mathf.Clamp01((_age - _holdSeconds) / FadeSeconds);
        _material.color = new Color(1f, 1f, 1f, alpha);
    }

    private void OnDestroy()
    {
        if (_material == null)
            return;
        if (Application.isPlaying)
            Destroy(_material);
        else
            DestroyImmediate(_material);
    }

    // First solid, non-racer surface below the centre; the centre's own height without one.
    private static float GroundHeight(Vector3 centre)
    {
        RaycastHit[] hits = Physics.RaycastAll(centre + Vector3.up * GroundProbeUp, Vector3.down, GroundProbeUp + GroundProbeDown,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        float bestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<SkillExecutor>() != null)
                continue;
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit.point.y;
            }
        }
        return float.IsNegativeInfinity(best) ? centre.y : best;
    }
}
