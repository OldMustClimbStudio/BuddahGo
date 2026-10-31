using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One skill slot of the owner HUD (A6 minimal): skill icon, per-slot cooldown fill and seconds, the slot key,
/// the same-skill surcharge window after the cooldown (next obsession gain multiplier and window seconds), and the
/// backfire / freeze lock that blocks every slot. The state lives in <see cref="SkillSlotHudTimeline"/>; the labels
/// are runtime TMP texts placed next to the icon (no prefab edits).
/// </summary>
public class SkillSlotUI : MonoBehaviour
{
    // Retired player combo tokens (the W/UP images of the slot prefabs). Player combos are gone (single-key casts);
    // the list is kept only so the images can be hidden.
    [Serializable]
    public class TokenUI
    {
        public Graphic black;
        public Graphic white;
    }

    [Header("Primary Visuals")]
    [SerializeField] private Image idleImage;
    [SerializeField] private Image activeImage;

    [Header("Rotate Visuals")]
    [SerializeField] private Image idleRotateImage;
    [SerializeField] private Image activeRotateImage;
    [SerializeField] private float rotateSpeed = 120f;

    [Header("Slot Binding")]
    [SerializeField] private int slotIndexOverride = -1;
    [SerializeField] private float bindRetryInterval = 0.5f;

    [Header("Retired Combo Tokens (hidden at runtime)")]
    [SerializeField] private List<TokenUI> tokens = new();

    [Header("Runtime Labels (positions relative to the icon, in the icon parent's space)")]
    [SerializeField] private Vector2 keyLabelOffset = new Vector2(-67f, -45.5f);
    [SerializeField] private Vector2 keyLabelSize = new Vector2(72f, 30f);
    [SerializeField] private Vector2 statusLabelOffset = new Vector2(9f, -45.5f);
    [SerializeField] private Vector2 statusLabelSize = new Vector2(240f, 30f);
    [SerializeField] private float labelFontSize = 22f;
    [SerializeField] private Color keyLabelColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color surchargeColor = new Color(0.78f, 0.36f, 0f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.75f, 0.1f, 0.1f, 1f);
    [SerializeField] private float labelOutlineWidth = 0.2f;
    [SerializeField] private Color labelOutlineColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("Multiplied into the icon colours while every slot is locked (backfire penalty or freeze).")]
    [SerializeField] private Color lockedIconTint = new Color(0.45f, 0.45f, 0.45f, 0.8f);

    private static string[] _bindingKeyLabels;

    private bool _isActive;
    private int _slotIndex = -1;
    private float _bindCheckTime;
    private string _boundSkillId = string.Empty;
    private Quaternion _idleRotation = Quaternion.identity;

    private readonly SkillSlotHudTimeline _timeline = new SkillSlotHudTimeline();
    private int _lastStatusKey = int.MinValue;
    private bool _lockTintApplied;
    private Color _idleBaseColor = Color.white;
    private Color _activeBaseColor = Color.white;

    private SkillLoadout _localLoadout;
    private SkillExecutor _localExecutor;
    private SkillExecutor _timelineExecutor;
    private ObsessionFigure _localObsession;
    private BuddahPredictedMotor _localMotor;
    private SkillUiSpriteLibrary _spriteLibrary;

    private TMP_FontAsset _font;
    private TextMeshProUGUI _keyLabel;
    private TextMeshProUGUI _statusLabel;

    private void Start()
    {
        CacheIdleRotation();
        CacheIconColors();
        ResolveSlotIndex();
        HideRetiredTokens();
        EnsureRuntimeLabels();
        ApplyReadyState();
        BindRuntimeSources(forceRefresh: true);
        RefreshSkillPresentation(force: true);
    }

    private void Update()
    {
        _bindCheckTime += Time.deltaTime;
        if (_bindCheckTime >= bindRetryInterval)
        {
            _bindCheckTime = 0f;
            BindRuntimeSources(forceRefresh: false);
            RefreshSkillPresentation(force: false);
        }

        UpdateSlotState();
        UpdateRotateVisuals();
    }

    private void OnDisable()
    {
        UnsubscribeExecutor();
    }

    private void ResolveSlotIndex()
    {
        if (slotIndexOverride >= 0)
        {
            _slotIndex = slotIndexOverride;
            return;
        }

        if (_slotIndex >= 0)
            return;

        Transform current = transform;
        while (current != null)
        {
            if (TryParseSlotIndex(current.name, out int parsed))
            {
                _slotIndex = parsed;
                return;
            }

            current = current.parent;
        }
    }

    private void BindRuntimeSources(bool forceRefresh)
    {
        ResolveSlotIndex();

        if (_localLoadout == null || !_localLoadout.IsOwner)
            _localLoadout = FindOwnedComponent<SkillLoadout>();

        SkillExecutor ownedExecutor = _localExecutor;
        if (ownedExecutor == null || !ownedExecutor.IsOwner)
            ownedExecutor = FindOwnedComponent<SkillExecutor>();

        if (!ReferenceEquals(ownedExecutor, _localExecutor))
        {
            UnsubscribeExecutor();

            // A different owned racer (new match): nothing of the old timeline applies. Re-binding the same racer
            // after OnDisable keeps it.
            if (ownedExecutor != null && !ReferenceEquals(ownedExecutor, _timelineExecutor))
            {
                _timeline.Reset();
                _timelineExecutor = ownedExecutor;
            }
            _localExecutor = ownedExecutor;
            _localMotor = null;
            if (_localExecutor != null)
            {
                _localExecutor.LocalSkillUiTriggered += HandleSkillTriggered;
                _localMotor = _localExecutor.GetComponent<BuddahPredictedMotor>();
            }
        }

        if (_localObsession == null || !_localObsession.IsOwner)
            _localObsession = FindOwnedComponent<ObsessionFigure>();

        if (_spriteLibrary == null)
            _spriteLibrary = GetComponentInParent<SkillUiSpriteLibrary>(true);

        if (forceRefresh)
            RefreshSkillPresentation(force: true);
    }

    private void RefreshSkillPresentation(bool force)
    {
        if (_localLoadout == null || _slotIndex < 0)
            return;

        string skillId = _localLoadout.GetSkillId(_slotIndex) ?? string.Empty;
        if (!force && string.Equals(_boundSkillId, skillId, StringComparison.Ordinal))
            return;

        if (!string.Equals(_boundSkillId, skillId, StringComparison.Ordinal))
            _timeline.Reset();
        _boundSkillId = skillId;

        if (_spriteLibrary != null && _spriteLibrary.TryGetSprites(_boundSkillId, out SkillUiSpriteLibrary.SkillSpriteEntry entry))
        {
            ApplyMappedSprite(idleImage, entry.blackSprite);
            ApplyMappedSprite(activeImage, entry.whiteSprite);
        }

        if (_isActive)
            ApplyActiveState();
        else
            ApplyReadyState();
    }

    // Every slot hears every owner event: its own (cooldown), a duplicate of its skill in another slot (shared
    // surcharge stacks) and any backfire (SurchargeStacks -1 clears every skill's stacks).
    private void HandleSkillTriggered(SkillExecutor.LocalSkillUiEvent skillEvent)
    {
        bool isOwnSlot = skillEvent.SlotIndex == _slotIndex;
        string eventSkillId = _localLoadout != null ? _localLoadout.GetSkillId(skillEvent.SlotIndex) : string.Empty;
        bool isSameSkill = !string.IsNullOrEmpty(_boundSkillId) && string.Equals(eventSkillId, _boundSkillId, StringComparison.Ordinal);

        // Anchored at the server's execution, not at the RPC's arrival (a remote client would show both the cooldown
        // end and the surcharge window a round trip late).
        float eventTime = Time.time - skillEvent.SecondsSinceServerEvent;
        _timeline.OnSkillUiEvent(eventTime, isOwnSlot, isSameSkill, skillEvent.CooldownSeconds, skillEvent.SurchargeStacks,
            skillEvent.RepeatWindowSeconds, skillEvent.RepeatGainMultiplier);
        _lastStatusKey = int.MinValue;
    }

    private void UpdateSlotState()
    {
        float backfireLeft = _localObsession != null ? _localObsession.BackfireSecondsLeft : 0f;
        float frozenLeft = _localMotor != null ? _localMotor.FreezeSecondsLeft : 0f;
        SkillSlotHudView view = _timeline.Sample(Time.time, backfireLeft, frozenLeft);

        if (view.IsCoolingDown != _isActive)
        {
            _isActive = view.IsCoolingDown;
            if (_isActive) ApplyActiveState();
            else ApplyReadyState();
        }
        if (_isActive)
            SetActiveFill(view.CooldownFill01);

        ApplyLockTint(view.IsLocked);
        UpdateStatusLabel(view);
    }

    private void UpdateStatusLabel(in SkillSlotHudView view)
    {
        if (_statusLabel == null)
            return;
        int key = SkillSlotHudFormat.ChangeKey(view);
        if (key == _lastStatusKey)
            return;
        _lastStatusKey = key;

        var labels = new SkillSlotHudLabels(
            HudGlyphCoverage.Resolve(_font, InGameHudLabels.SlotBackfireLock),
            HudGlyphCoverage.Resolve(_font, InGameHudLabels.SlotFrozen),
            HudGlyphCoverage.Resolve(_font, InGameHudLabels.SlotSurcharge));
        _statusLabel.color = view.IsLocked ? lockedColor
            : view.Kind == SkillSlotHudKind.SurchargeWindow ? surchargeColor : cooldownColor;
        HudRuntimeText.SetText(_statusLabel, SkillSlotHudFormat.Status(view, labels));
    }

    private void EnsureRuntimeLabels()
    {
        if (_keyLabel != null)
            return;

        RectTransform anchorRect = idleImage != null ? idleImage.rectTransform : transform as RectTransform;
        RectTransform parent = anchorRect != null ? anchorRect.parent as RectTransform : null;
        if (parent == null)
            parent = transform as RectTransform;
        if (parent == null)
            return;

        Vector2 anchor = anchorRect != null ? anchorRect.anchorMin : new Vector2(0.5f, 0.5f);
        Vector2 origin = anchorRect != null && anchorRect != parent ? anchorRect.anchoredPosition : Vector2.zero;
        var leftMiddle = new Vector2(0f, 0.5f);

        _font = HudRuntimeText.ResolveFont(this);
        _keyLabel = HudRuntimeText.Create(parent, "HudKeyLabel", _font, labelFontSize, keyLabelColor,
            TextAlignmentOptions.MidlineLeft, anchor, leftMiddle, origin + keyLabelOffset, keyLabelSize);
        _statusLabel = HudRuntimeText.Create(parent, "HudStatusLabel", _font, labelFontSize, cooldownColor,
            TextAlignmentOptions.MidlineLeft, anchor, leftMiddle, origin + statusLabelOffset, statusLabelSize);
        HudRuntimeText.ApplyOutline(_keyLabel, labelOutlineWidth, labelOutlineColor);
        HudRuntimeText.ApplyOutline(_statusLabel, labelOutlineWidth, labelOutlineColor);
        HudRuntimeText.SetText(_keyLabel, ResolveKeyLabel(_slotIndex));
        HudRuntimeText.SetText(_statusLabel, string.Empty);
    }

    // Read once from the input actions (Q/E/R and 1/2/3 today); the contract defaults when they cannot be read.
    private static string ResolveKeyLabel(int slotIndex)
    {
        if (_bindingKeyLabels == null)
        {
            _bindingKeyLabels = new string[SkillLoadout.SlotCount];
            InputSystem_Actions actions = null;
            try
            {
                actions = new InputSystem_Actions();
                InputAction[] slots = { actions.Player.SkillSlot1, actions.Player.SkillSlot2, actions.Player.SkillSlot3 };
                for (int i = 0; i < slots.Length && i < _bindingKeyLabels.Length; i++)
                {
                    var paths = new List<string>();
                    foreach (InputBinding binding in slots[i].bindings)
                        if (!binding.isComposite && !binding.isPartOfComposite)
                            paths.Add(binding.effectivePath);
                    _bindingKeyLabels[i] = SkillSlotHudFormat.KeyLabelFromBindingPaths(paths);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (actions != null)
                    UnityEngine.Object.Destroy(actions.asset);
            }
        }

        string label = slotIndex >= 0 && slotIndex < _bindingKeyLabels.Length ? _bindingKeyLabels[slotIndex] : null;
        return string.IsNullOrEmpty(label) ? SkillSlotHudFormat.DefaultKeyLabel(slotIndex) : label;
    }

    private void HideRetiredTokens()
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == null)
                continue;
            SetGraphicVisible(tokens[i].black, false);
            SetGraphicVisible(tokens[i].white, false);
        }
    }

    private void CacheIconColors()
    {
        if (idleImage != null) _idleBaseColor = idleImage.color;
        if (activeImage != null) _activeBaseColor = activeImage.color;
    }

    private void ApplyLockTint(bool locked)
    {
        if (locked == _lockTintApplied)
            return;
        _lockTintApplied = locked;
        if (idleImage != null) idleImage.color = locked ? _idleBaseColor * lockedIconTint : _idleBaseColor;
        if (activeImage != null) activeImage.color = locked ? _activeBaseColor * lockedIconTint : _activeBaseColor;
    }

    private void ApplyReadyState()
    {
        if (idleImage != null)
        {
            idleImage.enabled = true;
            idleImage.gameObject.SetActive(true);
            idleImage.fillAmount = 1f;
        }

        if (activeImage != null)
        {
            activeImage.enabled = false;
            activeImage.gameObject.SetActive(false);
            activeImage.fillAmount = 0f;
        }
    }

    private void ApplyActiveState()
    {
        if (idleImage != null)
        {
            idleImage.enabled = false;
            idleImage.gameObject.SetActive(false);
        }

        if (activeImage != null)
        {
            activeImage.enabled = true;
            activeImage.gameObject.SetActive(true);
            activeImage.fillAmount = 1f;
        }
    }

    private void SetActiveFill(float fill)
    {
        if (activeImage == null)
            return;

        activeImage.fillAmount = Mathf.Clamp01(fill);
    }

    private void UpdateRotateVisuals()
    {
        if (idleRotateImage == null || activeRotateImage == null)
            return;

        if (_isActive)
        {
            idleRotateImage.gameObject.SetActive(false);
            activeRotateImage.gameObject.SetActive(true);
            activeRotateImage.rectTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
        else
        {
            idleRotateImage.gameObject.SetActive(true);
            activeRotateImage.gameObject.SetActive(false);
            idleRotateImage.rectTransform.rotation = _idleRotation;
        }
    }

    private void CacheIdleRotation()
    {
        if (idleRotateImage != null)
            _idleRotation = idleRotateImage.rectTransform.rotation;
    }

    private void UnsubscribeExecutor()
    {
        if (_localExecutor == null)
            return;

        _localExecutor.LocalSkillUiTriggered -= HandleSkillTriggered;
        _localExecutor = null;
        _localMotor = null;
    }

    private static void SetGraphicVisible(Graphic graphic, bool visible)
    {
        if (graphic == null)
            return;

        graphic.enabled = visible;
        graphic.gameObject.SetActive(visible);
    }

    private static void ApplyMappedSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.overrideSprite = sprite;
    }

    private static bool TryParseSlotIndex(string nodeName, out int slotIndex)
    {
        slotIndex = -1;
        if (string.IsNullOrWhiteSpace(nodeName) || !nodeName.StartsWith("SkillSlot_"))
            return false;

        string suffix = nodeName.Substring("SkillSlot_".Length);
        if (!int.TryParse(suffix, out int parsed))
            return false;

        slotIndex = parsed - 1;
        return slotIndex >= 0 && slotIndex < SkillLoadout.SlotCount;
    }

    private static T FindOwnedComponent<T>() where T : NetworkBehaviour
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && candidates[i].IsOwner)
                return candidates[i];
        }

        return null;
    }
}
