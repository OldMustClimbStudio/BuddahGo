using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owner obsession HUD: the bar and Buddha heads, plus (A6) runtime labels next to the bar: the current backfire
/// probability as the primary number, and the obsession value with the backfire penalty time while it lasts.
/// </summary>
public class ObsessionUI : MonoBehaviour
{
    [Header("平滑设置")]
    public float smoothSpeed = 8f;
    public float colorSmoothSpeed = 5f;

    [Header("图片淡入淡出速度")]
    public float fadeSpeed = 3f;

    [Header("UI抖动设置")]
    public float maxShakeStrength = 3f;
    public float shakeSpeed = 15f;

    [Header("进度条闪烁设置 (>333生效)")]
    public Color flashColor = Color.white;
    public Color normalColor = Color.gray;

    [Header("UI引用")]
    public Image ObsessionData;
    public RectTransform icon;
    public Color targetIconColor = Color.black;
    public RectTransform uiRoot;

    [Header("佛头图片")]
    public Image buddha0_333;
    public Image buddha334_666;
    public Image buddha667_1000;

    [Header("反噬概率与惩罚 (runtime labels, anchored at the bar's right end)")]
    [SerializeField] private Vector2 probabilityOffset = new Vector2(36f, 4f);
    [SerializeField] private Vector2 probabilitySize = new Vector2(260f, 40f);
    [SerializeField] private float probabilityFontSize = 32f;
    [SerializeField] private Vector2 statusOffset = new Vector2(36f, -26f);
    [SerializeField] private Vector2 statusSize = new Vector2(360f, 30f);
    [SerializeField] private float statusFontSize = 22f;
    [SerializeField] private Color labelColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color penaltyColor = new Color(0.75f, 0.1f, 0.1f, 1f);
    [SerializeField] private float labelOutlineWidth = 0.2f;
    [SerializeField] private Color labelOutlineColor = new Color(1f, 1f, 1f, 0.85f);

    private ObsessionFigure _localObsessionFigure;
    private Image iconImage;
    private Color originalIconColor;
    private float currentObsession;

    private TMP_FontAsset _labelFont;
    private TextMeshProUGUI _probabilityLabel;
    private TextMeshProUGUI _statusLabel;
    private string _lastProbabilityText;
    private string _lastStatusText;
    private int _lastLabelKey;

    float MaxObsession => _localObsessionFigure != null ? Mathf.Max(1f, _localObsessionFigure.Max) : 1000f;
    float ThresholdLow => MaxObsession / 3f;
    float ThresholdHigh => ThresholdLow * 2f;

    private void Start()
    {
        currentObsession = 0;

        if (icon != null)
        {
            iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
                originalIconColor = iconImage.color;
        }

        FindLocalObsessionFigure();
        EnsureRuntimeLabels();
    }

    private void Update()
    {
        if (_localObsessionFigure == null || !_localObsessionFigure.IsOwner)
            FindLocalObsessionFigure();

        if (_localObsessionFigure != null)
        {
            currentObsession = Mathf.Lerp(
                currentObsession,
                _localObsessionFigure.Current,
                Time.deltaTime * smoothSpeed
            );
        }

        UpdateFillAmount();
        UpdateIconPosition();
        UpdateIconColorByThreshold();
        UpdateBuddhaImageFade();
        UpdateUIShake();
        UpdateBarFlash(); // 🔥 进度条闪烁
        UpdateBackfireLabels();
    }

    // Labels sit at the right end of the bar (children of the bar, so they follow its layout and the UI shake).
    private void EnsureRuntimeLabels()
    {
        if (_probabilityLabel != null)
            return;
        RectTransform parent = ObsessionData != null ? ObsessionData.rectTransform : uiRoot;
        if (parent == null)
            return;

        _labelFont = HudRuntimeText.ResolveFont(this);
        var rightMiddle = new Vector2(1f, 0.5f);
        var leftMiddle = new Vector2(0f, 0.5f);
        _probabilityLabel = HudRuntimeText.Create(parent, "HudBackfireChance", _labelFont, probabilityFontSize, labelColor,
            TextAlignmentOptions.MidlineLeft, rightMiddle, leftMiddle, probabilityOffset, probabilitySize);
        _statusLabel = HudRuntimeText.Create(parent, "HudObsessionStatus", _labelFont, statusFontSize, labelColor,
            TextAlignmentOptions.MidlineLeft, rightMiddle, leftMiddle, statusOffset, statusSize);
        HudRuntimeText.ApplyOutline(_probabilityLabel, labelOutlineWidth, labelOutlineColor);
        HudRuntimeText.ApplyOutline(_statusLabel, labelOutlineWidth, labelOutlineColor);
    }

    void UpdateBackfireLabels()
    {
        if (_probabilityLabel == null || _statusLabel == null)
            return;

        if (_localObsessionFigure == null)
        {
            _lastLabelKey = 0;
            SetLabel(_probabilityLabel, ref _lastProbabilityText, string.Empty);
            SetLabel(_statusLabel, ref _lastStatusText, string.Empty);
            return;
        }

        bool penaltyActive = _localObsessionFigure.IsBackfireActive;
        float percent = _localObsessionFigure.CurrentBackfireProbabilityPercent;
        float obsession = _localObsessionFigure.Current;
        float penaltyLeft = _localObsessionFigure.BackfireSecondsLeft;
        // Rebuild the strings only when something visible changes (display precision, or a new font fallback).
        int key = System.HashCode.Combine(ObsessionHudFormat.ChangeKey(percent, obsession, penaltyActive, penaltyLeft),
            HudGlyphCoverage.GlobalFallbackCount());
        if (key == _lastLabelKey && _lastProbabilityText != null)
            return;
        _lastLabelKey = key;

        string probability = ObsessionHudFormat.Probability(
            HudGlyphCoverage.Resolve(_labelFont, InGameHudLabels.BackfireChance), percent);
        string status = ObsessionHudFormat.Status(
            HudGlyphCoverage.Resolve(_labelFont, InGameHudLabels.ObsessionValue), obsession,
            HudGlyphCoverage.Resolve(_labelFont, InGameHudLabels.BackfirePenalty), penaltyActive, penaltyLeft);

        _probabilityLabel.color = penaltyActive ? penaltyColor : labelColor;
        _statusLabel.color = penaltyActive ? penaltyColor : labelColor;
        SetLabel(_probabilityLabel, ref _lastProbabilityText, probability);
        SetLabel(_statusLabel, ref _lastStatusText, status);
    }

    private static void SetLabel(TextMeshProUGUI label, ref string last, string value)
    {
        if (string.Equals(last, value, System.StringComparison.Ordinal))
            return;
        last = value;
        HudRuntimeText.SetText(label, value);
    }

    void FindLocalObsessionFigure()
    {
        ObsessionFigure[] all = FindObjectsByType<ObsessionFigure>(FindObjectsSortMode.None);
        foreach (var obs in all)
        {
            if (obs != null && obs.IsOwner)
            {
                _localObsessionFigure = obs;
                break;
            }
        }
    }

    void UpdateFillAmount()
    {
        if (ObsessionData == null) return;
        float fill = currentObsession / MaxObsession;
        fill = Mathf.Clamp01(fill);
        ObsessionData.fillAmount = fill;
    }

    void UpdateIconPosition()
    {
        if (icon == null || ObsessionData == null) return;
        float w = ObsessionData.rectTransform.rect.width;
        float tx = ObsessionData.fillAmount * w;
        icon.anchoredPosition = Vector2.Lerp(icon.anchoredPosition, new Vector2(tx, icon.anchoredPosition.y), Time.deltaTime * smoothSpeed * 2);
    }

    void UpdateIconColorByThreshold()
    {
        if (iconImage == null) return;
        Color c = currentObsession > ThresholdHigh ? targetIconColor : originalIconColor;
        iconImage.color = Color.Lerp(iconImage.color, c, Time.deltaTime * colorSmoothSpeed);
    }

    void UpdateBuddhaImageFade()
    {
        if (buddha0_333 == null || buddha334_666 == null || buddha667_1000 == null) return;

        buddha0_333.gameObject.SetActive(true);
        buddha334_666.gameObject.SetActive(true);
        buddha667_1000.gameObject.SetActive(true);

        float a0 = 0, a1 = 0, a2 = 0;
        if (currentObsession <= ThresholdLow) a0 = 1;
        else if (currentObsession <= ThresholdHigh) a1 = 1;
        else a2 = 1;

        buddha0_333.color = new Color(1, 1, 1, Mathf.Lerp(buddha0_333.color.a, a0, Time.deltaTime * fadeSpeed));
        buddha334_666.color = new Color(1, 1, 1, Mathf.Lerp(buddha334_666.color.a, a1, Time.deltaTime * fadeSpeed));
        buddha667_1000.color = new Color(1, 1, 1, Mathf.Lerp(buddha667_1000.color.a, a2, Time.deltaTime * fadeSpeed));
    }

    void UpdateUIShake()
    {
        if (uiRoot == null) return;
        uiRoot.anchoredPosition = Vector2.zero;
        if (currentObsession <= ThresholdLow) return;

        float t = Mathf.InverseLerp(ThresholdLow, MaxObsession, currentObsession);
        float s = t * maxShakeStrength;
        float x = Mathf.PerlinNoise(Time.time * shakeSpeed, 0) * s;
        float y = Mathf.PerlinNoise(0, Time.time * shakeSpeed) * s;
        uiRoot.anchoredPosition = new Vector2(x, y);
    }

    // ======================================================================
    // 🔥 核心：进度条闪烁（>333开始，越高越快）
    // ======================================================================
    void UpdateBarFlash()
    {
        if (ObsessionData == null) return;

        // 低于低阈值 → 不闪，保持正常颜色
        if (currentObsession <= ThresholdLow)
        {
            ObsessionData.color = normalColor;
            return;
        }

        // 计算强度：越高闪得越快
        float t = Mathf.InverseLerp(ThresholdLow, MaxObsession, currentObsession);
        float freq = Mathf.Lerp(2f, 12f, t); // 慢 → 快
        float blink = Mathf.PingPong(Time.time * freq, 1f);

        // 柔和渐变
        ObsessionData.color = Color.Lerp(normalColor, flashColor, blink);
    }
}
