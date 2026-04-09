using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillSlotUI : MonoBehaviour
{
    [Serializable]
    public class TokenUI
    {
        public ComboSkillInput.Token tokenType;
        public Graphic black;
        public Graphic white;
    }

    private enum TokenVisualState
    {
        Idle,
        InputPreview,
        SuccessHold,
        CooldownHidden
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

    [Header("Token Visuals")]
    [SerializeField] private List<TokenUI> tokens = new();
    [SerializeField] private float successHoldSeconds = 2f;
    [SerializeField] private float fallbackCastLockSeconds = 2f;
    [SerializeField] private float previewTimeoutSeconds = 0.5f;

    private bool _isActive;
    private int _slotIndex = -1;
    private float _bindCheckTime;
    private string _boundSkillId = string.Empty;
    private Quaternion _idleRotation = Quaternion.identity;

    private float _elapsed;
    private float _castLockDuration;
    private float _cooldownDuration;
    private float _fillDrainDuration;
    private float _successHoldRemaining;
    private float _previewTimeoutRemaining;

    private TokenVisualState _tokenState = TokenVisualState.Idle;
    private int _previewMatchedCount;
    private ComboSkillInput.Token[] _slotSequence;

    private SkillLoadout _localLoadout;
    private SkillExecutor _localExecutor;
    private SkillUiSpriteLibrary _spriteLibrary;
    private ComboSkillInput _localComboInput;

    private void Start()
    {
        CacheIdleRotation();
        ResolveSlotIndex();
        ApplyReadyState();
        ApplyTokenIdleState();
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

        UpdateCooldown();
        UpdateTokenVisuals();
        UpdateRotateVisuals();
    }

    private void OnDisable()
    {
        UnsubscribeExecutor();
        UnsubscribeComboInput();
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

        if (_localLoadout == null)
            _localLoadout = FindOwnedComponent<SkillLoadout>();

        SkillExecutor ownedExecutor = _localExecutor;
        if (ownedExecutor == null || !ownedExecutor.IsOwner)
            ownedExecutor = FindOwnedComponent<SkillExecutor>();

        if (!ReferenceEquals(ownedExecutor, _localExecutor))
        {
            UnsubscribeExecutor();

            _localExecutor = ownedExecutor;
            if (_localExecutor != null)
                _localExecutor.LocalSkillUiTriggered += HandleSkillTriggered;
        }

        if (_spriteLibrary == null)
            _spriteLibrary = GetComponentInParent<SkillUiSpriteLibrary>(true);

        ComboSkillInput ownedComboInput = _localComboInput;
        if (ownedComboInput == null || !ownedComboInput.IsOwner)
            ownedComboInput = FindOwnedComponent<ComboSkillInput>();

        if (!ReferenceEquals(ownedComboInput, _localComboInput))
        {
            UnsubscribeComboInput();

            _localComboInput = ownedComboInput;
            if (_localComboInput != null)
            {
                _localComboInput.OnComboProgress += HandleComboProgress;
                _localComboInput.TryGetSequenceForSlot(_slotIndex, out _slotSequence);
            }
        }

        if (forceRefresh)
            RefreshSkillPresentation(force: true);
    }

    private void RefreshSkillPresentation(bool force)
    {
        if (_localLoadout == null || _slotIndex < 0)
            return;

        string skillId = _localLoadout.GetSkillId(_slotIndex);
        if (!force && string.Equals(_boundSkillId, skillId, System.StringComparison.Ordinal))
            return;

        _boundSkillId = skillId ?? string.Empty;

        if (_spriteLibrary == null)
            return;

        if (_spriteLibrary.TryGetSprites(_boundSkillId, out SkillUiSpriteLibrary.SkillSpriteEntry entry))
        {
            ApplyMappedSprite(idleImage, entry.blackSprite);
            ApplyMappedSprite(activeImage, entry.whiteSprite);
        }

        if (_isActive)
            ApplyActiveState();
        else
            ApplyReadyState();
    }

    private void HandleSkillTriggered(SkillExecutor.LocalSkillUiEvent skillEvent)
    {
        if (skillEvent.SlotIndex != _slotIndex)
            return;

        PlayCooldown(skillEvent.CooldownSeconds, skillEvent.CastLockSeconds);
    }

    private void HandleComboProgress(ComboSkillInput.ComboProgressEvent progressEvent)
    {
        if (progressEvent.slotIndex != _slotIndex)
            return;

        if (_tokenState == TokenVisualState.SuccessHold || _tokenState == TokenVisualState.CooldownHidden)
            return;

        if (_isActive)
            return;

        if (!progressEvent.isValidPrefix || progressEvent.matchedStepCount <= 0)
        {
            ApplyTokenIdleState();
            return;
        }

        _previewMatchedCount = progressEvent.matchedStepCount;
        _tokenState = TokenVisualState.InputPreview;
        _previewTimeoutRemaining = previewTimeoutSeconds;
        ApplyTokenPreviewState(_previewMatchedCount);
    }

    private void PlayCooldown(float cooldownSeconds, float castLockSeconds)
    {
        _cooldownDuration = Mathf.Max(0f, cooldownSeconds);
        _castLockDuration = castLockSeconds > 0f ? castLockSeconds : fallbackCastLockSeconds;
        _castLockDuration = Mathf.Clamp(_castLockDuration, 0f, _cooldownDuration);
        _fillDrainDuration = Mathf.Max(0f, _cooldownDuration - _castLockDuration);
        _elapsed = 0f;

        _isActive = _cooldownDuration > 0f;
        _successHoldRemaining = successHoldSeconds;
        _previewTimeoutRemaining = 0f;

        ApplyTokenSuccessHoldState();

        if (_isActive)
            ApplyActiveState();
        else
            ApplyReadyState();
    }

    private void UpdateCooldown()
    {
        if (!_isActive)
            return;

        _elapsed += Time.deltaTime;

        if (_elapsed < _castLockDuration)
        {
            SetActiveFill(1f);
        }
        else
        {
            float fill = _fillDrainDuration > 0f
                ? 1f - ((_elapsed - _castLockDuration) / _fillDrainDuration)
                : 0f;
            SetActiveFill(fill);
        }

        if (_elapsed >= _cooldownDuration)
        {
            _isActive = false;
            SetActiveFill(0f);
            ApplyReadyState();
            ApplyTokenIdleState();
        }
    }

    private void UpdateTokenVisuals()
    {
        if (_tokenState == TokenVisualState.SuccessHold)
        {
            _successHoldRemaining -= Time.deltaTime;
            if (_successHoldRemaining <= 0f)
                ApplyTokenCooldownHiddenState();

            return;
        }

        if (_tokenState == TokenVisualState.InputPreview)
        {
            _previewTimeoutRemaining -= Time.deltaTime;
            if (_previewTimeoutRemaining <= 0f)
                ApplyTokenIdleState();
        }
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
    }

    private void UnsubscribeComboInput()
    {
        if (_localComboInput == null)
            return;

        _localComboInput.OnComboProgress -= HandleComboProgress;
        _localComboInput = null;
    }

    private void ApplyTokenIdleState()
    {
        _tokenState = TokenVisualState.Idle;
        _previewMatchedCount = 0;
        _previewTimeoutRemaining = 0f;

        for (int i = 0; i < tokens.Count; i++)
            SetTokenVisual(tokens[i], showBlack: true, showWhite: false);
    }

    private void ApplyTokenPreviewState(int matchedCount)
    {
        _tokenState = TokenVisualState.InputPreview;

        int maxPreviewCount = _slotSequence != null && _slotSequence.Length > 0
            ? _slotSequence.Length
            : tokens.Count;
        int clampedCount = Mathf.Clamp(matchedCount, 0, maxPreviewCount);

        for (int i = 0; i < tokens.Count; i++)
        {
            bool highlight = i < clampedCount;
            SetTokenVisual(tokens[i], showBlack: !highlight, showWhite: highlight);
        }
    }

    private void ApplyTokenSuccessHoldState()
    {
        _tokenState = TokenVisualState.SuccessHold;
        _previewTimeoutRemaining = 0f;

        for (int i = 0; i < tokens.Count; i++)
            SetTokenVisual(tokens[i], showBlack: false, showWhite: true);
    }

    private void ApplyTokenCooldownHiddenState()
    {
        _tokenState = TokenVisualState.CooldownHidden;

        for (int i = 0; i < tokens.Count; i++)
            SetTokenVisual(tokens[i], showBlack: false, showWhite: false);
    }

    private static void SetTokenVisual(TokenUI tokenUi, bool showBlack, bool showWhite)
    {
        if (tokenUi == null)
            return;

        SetGraphicVisible(tokenUi.black, showBlack);
        SetGraphicVisible(tokenUi.white, showWhite);
    }

    private static void SetGraphicVisible(Graphic graphic, bool visible)
    {
        if (graphic == null)
            return;

        graphic.enabled = visible;
        if (graphic is Behaviour behaviour)
            behaviour.gameObject.SetActive(visible);
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
