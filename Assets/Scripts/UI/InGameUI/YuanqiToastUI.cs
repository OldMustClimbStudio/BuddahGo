using TMPro;
using UnityEngine;

/// <summary>
/// Caster-only 缘起 toast (A6): SkillExecutor.LocalYuanqiTriggered (owner) shows "缘起 &lt;name&gt;" for a few
/// seconds. Added at runtime to the RaceMap HUD canvas by <see cref="RaceMapUIBootstrap"/> (no prefab edit). The
/// name is SkillDatabase.GetYuanqiDisplayName, or the yuanqiId when the HUD font cannot render it.
/// </summary>
[DisallowMultipleComponent]
public class YuanqiToastUI : MonoBehaviour
{
    [SerializeField] private float bindRetryInterval = 0.5f;
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float fadeSeconds = 0.5f;
    [Tooltip("Anchored at the top centre of the HUD canvas.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, -170f);
    [SerializeField] private Vector2 size = new Vector2(760f, 64f);
    [SerializeField] private float fontSize = 44f;
    [SerializeField] private Color color = new Color(0.55f, 0.32f, 0f, 1f);
    [SerializeField] private float outlineWidth = 0.2f;
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.9f);

    private SkillExecutor _localExecutor;
    private float _bindCheckTime;
    private float _shownAt = float.NegativeInfinity;
    private TMP_FontAsset _font;
    private TextMeshProUGUI _label;

    private void Start()
    {
        EnsureLabel();
        BindExecutor();
    }

    private void Update()
    {
        _bindCheckTime += Time.deltaTime;
        if (_bindCheckTime >= bindRetryInterval)
        {
            _bindCheckTime = 0f;
            BindExecutor();
        }

        UpdateFade();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void EnsureLabel()
    {
        if (_label != null)
            return;
        var parent = transform as RectTransform;
        if (parent == null)
            return;
        var topCentre = new Vector2(0.5f, 1f);
        _font = HudRuntimeText.ResolveFont(this);
        _label = HudRuntimeText.Create(parent, "HudYuanqiToast", _font, fontSize, color,
            TextAlignmentOptions.Center, topCentre, topCentre, offset, size);
        HudRuntimeText.ApplyOutline(_label, outlineWidth, outlineColor);
        HudRuntimeText.SetText(_label, string.Empty);
    }

    private void BindExecutor()
    {
        if (_localExecutor != null && _localExecutor.IsOwner)
            return;
        Unbind();
        _localExecutor = FindOwned();
        if (_localExecutor != null)
            _localExecutor.LocalYuanqiTriggered += HandleYuanqiTriggered;
    }

    private void Unbind()
    {
        if (_localExecutor == null)
            return;
        _localExecutor.LocalYuanqiTriggered -= HandleYuanqiTriggered;
        _localExecutor = null;
    }

    private void HandleYuanqiTriggered(string yuanqiId)
    {
        EnsureLabel();
        if (_label == null)
            return;
        string name = HudSkillDatabase.YuanqiName(HudSkillDatabase.Resolve(), _font, yuanqiId);
        if (string.IsNullOrEmpty(name))
            name = yuanqiId ?? string.Empty;
        HudRuntimeText.SetText(_label, YuanqiHudText.Toast(HudGlyphCoverage.Resolve(_font, InGameHudLabels.YuanqiToast), name));
        _shownAt = Time.time;
        SetAlpha(1f);
    }

    private void UpdateFade()
    {
        if (_label == null || !_label.gameObject.activeSelf)
            return;
        float elapsed = Time.time - _shownAt;
        if (elapsed <= holdSeconds)
            return;
        float fade = fadeSeconds > 0f ? 1f - (elapsed - holdSeconds) / fadeSeconds : 0f;
        if (fade <= 0f)
        {
            HudRuntimeText.SetText(_label, string.Empty);
            return;
        }
        SetAlpha(fade);
    }

    private void SetAlpha(float alpha)
    {
        Color current = color;
        current.a = color.a * Mathf.Clamp01(alpha);
        _label.color = current;
    }

    private static SkillExecutor FindOwned()
    {
        SkillExecutor[] candidates = FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
            if (candidates[i] != null && candidates[i].IsOwner)
                return candidates[i];
        return null;
    }
}
