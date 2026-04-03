using System.Collections;
using SteamMultiplayer.Network;
using TMPro;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Collider-based 3D wall item.
    /// Uses OnMouseEnter/Exit/Down so it can work with simple world-space objects
    /// without depending on the legacy UI drag stack.
    /// </summary>
    public class SkillWallItemView : MonoBehaviour
    {
        [Header("Optional Visuals")]
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private TextMeshPro labelText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.75f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.7f, 1f, 0.7f, 1f);

        [Header("Hover Motion")]
        [SerializeField] private float hoverForwardDistance = 0.08f;
        [SerializeField] private float hoverMoveDuration = 0.12f;
        [SerializeField] private AnimationCurve hoverCurve;

        [Header("Flight Motion")]
        [SerializeField] private float defaultFlightDuration = 0.35f;
        [SerializeField] private AnimationCurve flightCurve;

        private SkillWallSelectionController _controller;
        private SkillInspectableItem _inspectableItem;
        private SelectablePropertyOption _optionData;
        private string _skillId = string.Empty;
        private string _displayName = string.Empty;
        private string _description = string.Empty;
        private string _previewIconKey = string.Empty;

        private Vector3 _wallLocalPosition;
        private Quaternion _wallLocalRotation = Quaternion.identity;

        private bool _isHovered;
        private bool _isSelected;
        private bool _isInFlight;
        private bool _interactionLocked;

        private Coroutine _hoverRoutine;
        private Coroutine _moveRoutine;

        public string SkillId => _skillId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public string PreviewIconKey => _previewIconKey;
        public bool IsSelected => _isSelected;
        public bool IsInFlight => _isInFlight;
        public bool IsHovered => _isHovered;
        public bool IsInteractionLocked => _interactionLocked;
        public Vector3 WallLocalPosition => _wallLocalPosition;
        public Quaternion WallLocalRotation => _wallLocalRotation;
        public SelectablePropertyOption OptionData => _optionData;
        public SkillInspectableItem InspectableItem => _inspectableItem;

        private void Awake()
        {
            EnsureCurves();
            _inspectableItem = GetComponent<SkillInspectableItem>();
        }

        public void Initialize(SkillWallSelectionController controller, SelectablePropertyOption option)
        {
            _controller = controller;
            _optionData = option;
            _skillId = (option.OptionId ?? string.Empty).Trim();
            _displayName = string.IsNullOrWhiteSpace(option.DisplayName) ? _skillId : option.DisplayName;
            _description = option.Description ?? string.Empty;
            _previewIconKey = option.PreviewIconKey ?? string.Empty;

            gameObject.name = $"SkillWallItem_{_skillId}";
            RecordCurrentWallPose();

            if (labelText != null)
                labelText.text = _displayName;

            _isHovered = false;
            _isSelected = false;
            _isInFlight = false;
            _interactionLocked = false;

            _inspectableItem = _inspectableItem != null ? _inspectableItem : GetComponent<SkillInspectableItem>();
            if (_inspectableItem != null)
                _inspectableItem.Initialize(option, this);

            EnsureInteractionRelays();

            ApplyVisualState();
            SnapToWallPose();
        }

        public void RecordCurrentWallPose()
        {
            _wallLocalPosition = transform.localPosition;
            _wallLocalRotation = transform.localRotation;
        }

        public void SetWallPose(Vector3 localPosition, Quaternion localRotation)
        {
            _wallLocalPosition = localPosition;
            _wallLocalRotation = localRotation;
        }

        public Vector3 GetWallWorldPosition()
        {
            return transform.parent != null
                ? transform.parent.TransformPoint(_wallLocalPosition)
                : _wallLocalPosition;
        }

        public Quaternion GetWallWorldRotation()
        {
            return transform.parent != null
                ? transform.parent.rotation * _wallLocalRotation
                : _wallLocalRotation;
        }

        public void SnapToWallPose()
        {
            StopAllMotion();
            transform.localPosition = _wallLocalPosition;
            transform.localRotation = _wallLocalRotation;
            _isInFlight = false;
            _isHovered = false;
            ApplyVisualState();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selected)
            {
                _isHovered = false;
                StopHoverRoutine();
            }
            ApplyVisualState();
        }

        public void SetInteractionLocked(bool locked)
        {
            _interactionLocked = locked;
            if (locked)
            {
                _isHovered = false;
                StopHoverRoutine();
            }
            ApplyVisualState();
        }

        public void ReturnToWall(bool animate = true)
        {
            _isSelected = false;
            _isHovered = false;
            _interactionLocked = false;

            if (animate)
            {
                MoveToLocalPose(_wallLocalPosition, _wallLocalRotation, defaultFlightDuration);
            }
            else
            {
                SnapToWallPose();
            }
        }

        public void MoveToWorldPose(Vector3 worldPosition, Quaternion worldRotation, float? durationOverride = null)
        {
            StopAllMotion();
            float duration = durationOverride ?? defaultFlightDuration;
            if (duration <= 0f)
            {
                transform.SetPositionAndRotation(worldPosition, worldRotation);
                _isInFlight = false;
                ApplyVisualState();
                return;
            }

            _moveRoutine = StartCoroutine(AnimateWorldMove(worldPosition, worldRotation, duration));
        }

        public IEnumerator AnimateFlightToWorldPose(Vector3 worldPosition, Quaternion worldRotation, float duration, float arcHeight)
        {
            StopAllMotion();
            _isInFlight = true;
            _isHovered = false;
            ApplyVisualState();

            EnsureCurves();

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            Vector3 controlOffset = Vector3.up * Mathf.Max(0f, arcHeight);
            Vector3 midpoint = (startPosition + worldPosition) * 0.5f + controlOffset;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float easedT = flightCurve.Evaluate(t);

                Vector3 a = Vector3.Lerp(startPosition, midpoint, easedT);
                Vector3 b = Vector3.Lerp(midpoint, worldPosition, easedT);
                transform.position = Vector3.Lerp(a, b, easedT);
                transform.rotation = Quaternion.Slerp(startRotation, worldRotation, easedT);
                yield return null;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            _isInFlight = false;
            _moveRoutine = null;
            ApplyVisualState();
        }

        public void MoveToLocalPose(Vector3 localPosition, Quaternion localRotation, float? durationOverride = null)
        {
            StopAllMotion();
            float duration = durationOverride ?? defaultFlightDuration;
            if (duration <= 0f)
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                _isInFlight = false;
                ApplyVisualState();
                return;
            }

            _moveRoutine = StartCoroutine(AnimateLocalMove(localPosition, localRotation, duration, isFlightMotion: true));
        }

        public void CancelMotionAndSnapBackToWall()
        {
            StopAllMotion();
            SnapToWallPose();
        }

        public void HandlePointerEnter()
        {
            if (!CanInteract())
                return;

            _isHovered = true;
            ApplyVisualState();
            StartHoverMotion();
            _controller.NotifyItemHoverEntered(this);
        }

        public void HandlePointerExit()
        {
            if (_isSelected || _isInFlight)
                return;

            _isHovered = false;
            ApplyVisualState();
            StartHoverMotion();
            _controller.NotifyItemHoverExited(this);
        }

        public void HandlePrimaryPointerDown()
        {
            if (_controller == null || string.IsNullOrWhiteSpace(_skillId))
                return;

            if (_isInFlight || !_controller.WasMenuLeftClickThisFrame())
                return;

            _controller.HandleItemClicked(_skillId);
        }

        public void RefreshHoverPose()
        {
            if (_isSelected || _isInFlight)
                return;

            StartHoverMotion();
        }

        private void OnMouseEnter()
        {
            HandlePointerEnter();
        }

        private void OnMouseExit()
        {
            HandlePointerExit();
        }

        private void OnMouseDown()
        {
            HandlePrimaryPointerDown();
        }

        private void StartHoverMotion()
        {
            StopHoverRoutine();

            Vector3 targetWorldPosition = GetWallWorldPosition();
            Quaternion targetWorldRotation = GetWallWorldRotation();

            if (_isHovered)
                targetWorldPosition += GetWallWorldRotation() * Vector3.forward * hoverForwardDistance;

            _hoverRoutine = StartCoroutine(AnimateWorldHover(targetWorldPosition, targetWorldRotation, hoverMoveDuration));
        }

        private bool CanInteract()
        {
            return _controller != null
                && !_controller.IsSelectionInputBlocked
                && !_interactionLocked
                && !_isSelected
                && !_isInFlight
                && !string.IsNullOrWhiteSpace(_skillId);
        }

        private IEnumerator AnimateWorldMove(Vector3 worldPosition, Quaternion worldRotation, float duration)
        {
            _isInFlight = true;
            ApplyVisualState();

            EnsureCurves();

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float easedT = flightCurve.Evaluate(t);

                transform.position = Vector3.Lerp(startPosition, worldPosition, easedT);
                transform.rotation = Quaternion.Slerp(startRotation, worldRotation, easedT);
                yield return null;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            _isInFlight = false;
            _moveRoutine = null;
            ApplyVisualState();
        }

        private IEnumerator AnimateWorldHover(Vector3 worldPosition, Quaternion worldRotation, float duration)
        {
            EnsureCurves();

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float easedT = hoverCurve.Evaluate(t);

                transform.position = Vector3.Lerp(startPosition, worldPosition, easedT);
                transform.rotation = Quaternion.Slerp(startRotation, worldRotation, easedT);
                yield return null;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            _hoverRoutine = null;
        }

        private IEnumerator AnimateLocalMove(Vector3 localPosition, Quaternion localRotation, float duration, bool isFlightMotion)
        {
            if (isFlightMotion)
            {
                _isInFlight = true;
                ApplyVisualState();
            }

            EnsureCurves();

            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            AnimationCurve curve = isFlightMotion ? flightCurve : hoverCurve;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float easedT = curve.Evaluate(t);

                transform.localPosition = Vector3.Lerp(startPosition, localPosition, easedT);
                transform.localRotation = Quaternion.Slerp(startRotation, localRotation, easedT);
                yield return null;
            }

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            if (isFlightMotion)
            {
                _isInFlight = false;
                _moveRoutine = null;
                ApplyVisualState();
            }
            else
            {
                _hoverRoutine = null;
            }
        }

        private void ApplyVisualState()
        {
            Color targetColor = normalColor;
            if (_isSelected)
                targetColor = selectedColor;
            else if (_isHovered && !_isInFlight)
                targetColor = hoverColor;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || targetRenderer.material == null)
                    continue;

                targetRenderer.material.color = targetColor;
            }
        }

        private void StopAllMotion()
        {
            StopHoverRoutine();
            StopMoveRoutine();
        }

        private void StopHoverRoutine()
        {
            if (_hoverRoutine == null)
                return;

            StopCoroutine(_hoverRoutine);
            _hoverRoutine = null;
        }

        private void StopMoveRoutine()
        {
            if (_moveRoutine == null)
                return;

            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _isInFlight = false;
        }

        private void EnsureCurves()
        {
            if (hoverCurve == null || hoverCurve.length == 0)
                hoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (flightCurve == null || flightCurve.length == 0)
                flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private void EnsureInteractionRelays()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];
                if (targetCollider == null)
                    continue;

                SkillWallItemInteractionRelay relay = targetCollider.GetComponent<SkillWallItemInteractionRelay>();
                if (relay == null)
                    relay = targetCollider.gameObject.AddComponent<SkillWallItemInteractionRelay>();

                relay.Initialize(this);
            }
        }
    }
}
