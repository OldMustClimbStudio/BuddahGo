using System.Collections;
using MoreMountains.Feedbacks;
using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    public class SkillInspectController : MonoBehaviour
    {
        public enum SkillInspectState
        {
            Inactive,
            Entering,
            Viewing,
            Exiting
        }

        [Header("Dependencies")]
        [SerializeField] private PropertiesSelectionManager selectionManager;
        [SerializeField] private SkillWallSelectionController selectionController;
        [SerializeField] private MonoBehaviour focusProviderBehaviour;
        [SerializeField] private Camera inspectCamera;
        [SerializeField] private Transform inspectAnchor;
        [SerializeField] private SkillInspectPanelUI inspectPanelUI;

        [Header("Motion")]
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.25f;
        [SerializeField] private float flightArcHeight = 0.2f;
        [SerializeField] private AnimationCurve moveCurve;
        [SerializeField] private bool enableIdleMotion = true;
        [SerializeField] private float idleAmplitude = 0.02f;
        [SerializeField] private float idleFrequency = 0.75f;
        [SerializeField] private float idleYawDegrees = 2f;

        [Header("Feedbacks")]
        [SerializeField] private MMF_Player enterInspectFeedback;
        [SerializeField] private MMF_Player exitInspectFeedback;

        public SkillInspectState State { get; private set; }
        public SkillInspectableItem CurrentItem => _currentItem;
        public SkillWallItemView CurrentInspectedView => _inspectedView;

        private ISkillSelectionFocusProvider _focusProvider;
        private SkillInspectableItem _currentItem;
        private SkillWallItemView _inspectedView;
        private Coroutine _transitionRoutine;
        private Coroutine _idleRoutine;
        private Vector3 _returnPosition;
        private Quaternion _returnRotation;
        private InputSystem_Actions _inputActions;
        private InputAction _menuCancelAction;
        private InputAction _menuInspectAction;

        private void Awake()
        {
            EnsureCurve();
            ResolveDependencies();
            SetSelectionInputBlocked(false);
            if (inspectPanelUI != null)
                inspectPanelUI.HideImmediate();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            EnableMenuInput();
        }

        private void Update()
        {
            ResolveDependencies();

            if (!IsSkillStageActive())
            {
                if (State != SkillInspectState.Inactive)
                    ForceExitImmediate();
                return;
            }

            if (State != SkillInspectState.Inactive && _currentItem == null)
            {
                ForceExitImmediate();
                return;
            }

            if (State == SkillInspectState.Entering || State == SkillInspectState.Exiting)
                return;
        }

        private void OnDisable()
        {
            DisableMenuInput();
            ForceExitImmediate();
        }

        public bool RequestToggleInspect(SkillInspectableItem item)
        {
            if (item == null || !IsSkillStageActive())
                return false;

            if (State == SkillInspectState.Entering || State == SkillInspectState.Exiting)
                return false;

            if (State == SkillInspectState.Viewing)
            {
                if (item == _currentItem)
                {
                    BeginExit();
                    return true;
                }

                return false;
            }

            return BeginEnter(item);
        }

        public void ForceExitImmediate()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            StopIdleMotion();
            if (_inspectedView != null)
            {
                _inspectedView.CancelMotionAndSnapBackToWall();
                _inspectedView.SetInteractionLocked(false);
            }

            _inspectedView = null;
            _currentItem = null;
            State = SkillInspectState.Inactive;
            SetSelectionInputBlocked(false);

            if (inspectPanelUI != null)
                inspectPanelUI.HideImmediate();
        }

        private bool BeginEnter(SkillInspectableItem item)
        {
            if (item == null || !CanInspectItem(item))
                return false;

            if (inspectAnchor == null || inspectCamera == null)
                return false;

            SkillWallItemView ownerView = item.OwnerView;
            if (ownerView == null)
                return false;

            _currentItem = item;
            _inspectedView = ownerView;
            _returnPosition = item.GetInspectStartPosition();
            _returnRotation = item.GetInspectStartRotation();
            State = SkillInspectState.Entering;
            SetSelectionInputBlocked(true);
            ownerView.CancelMotionAndSnapBackToWall();
            ownerView.SetInteractionLocked(true);

            Transform targetTransform = ownerView.transform;
            targetTransform.SetPositionAndRotation(_returnPosition, _returnRotation);

            if (inspectPanelUI != null)
                inspectPanelUI.Show(item);

            PlayFeedback(enterInspectFeedback);
            _transitionRoutine = StartCoroutine(AnimateInspectTransition(
                targetTransform,
                _returnPosition,
                _returnRotation,
                inspectAnchor.position,
                inspectAnchor.rotation * item.InspectRotation,
                Mathf.Max(0.01f, enterDuration),
                completeState: SkillInspectState.Viewing,
                restoreToWallOnComplete: false));
            return true;
        }

        private void BeginExit()
        {
            if (State != SkillInspectState.Viewing || _inspectedView == null)
                return;

            State = SkillInspectState.Exiting;
            StopIdleMotion();

            if (inspectPanelUI != null)
                inspectPanelUI.HideAnimated();

            PlayFeedback(exitInspectFeedback);
            _transitionRoutine = StartCoroutine(AnimateInspectTransition(
                _inspectedView.transform,
                _inspectedView.transform.position,
                _inspectedView.transform.rotation,
                _returnPosition,
                _returnRotation,
                Mathf.Max(0.01f, exitDuration),
                completeState: SkillInspectState.Inactive,
                restoreToWallOnComplete: true));
        }

        private IEnumerator AnimateInspectTransition(
            Transform target,
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 endPosition,
            Quaternion endRotation,
            float duration,
            SkillInspectState completeState,
            bool restoreToWallOnComplete)
        {
            float elapsed = 0f;
            Vector3 midpoint = (startPosition + endPosition) * 0.5f + Vector3.up * Mathf.Max(0f, flightArcHeight);

            while (elapsed < duration)
            {
                if (target == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = moveCurve.Evaluate(t);

                Vector3 a = Vector3.Lerp(startPosition, midpoint, easedT);
                Vector3 b = Vector3.Lerp(midpoint, endPosition, easedT);
                target.position = Vector3.Lerp(a, b, easedT);
                target.rotation = Quaternion.Slerp(startRotation, endRotation, easedT);
                yield return null;
            }

            if (target != null)
                target.SetPositionAndRotation(endPosition, endRotation);

            _transitionRoutine = null;
            State = completeState;

            if (completeState == SkillInspectState.Viewing)
            {
                StartIdleMotion();
                yield break;
            }

            if (restoreToWallOnComplete && _inspectedView != null)
            {
                _inspectedView.CancelMotionAndSnapBackToWall();
                _inspectedView.SetInteractionLocked(false);
            }

            _inspectedView = null;
            _currentItem = null;
            SetSelectionInputBlocked(false);
        }

        private void StartIdleMotion()
        {
            if (!enableIdleMotion || _inspectedView == null)
                return;

            StopIdleMotion();
            _idleRoutine = StartCoroutine(IdleMotionRoutine(_inspectedView.transform));
        }

        private void StopIdleMotion()
        {
            if (_idleRoutine == null)
                return;

            StopCoroutine(_idleRoutine);
            _idleRoutine = null;
        }

        private IEnumerator IdleMotionRoutine(Transform target)
        {
            Vector3 basePosition = target.position;
            Quaternion baseRotation = target.rotation;
            float time = 0f;

            while (State == SkillInspectState.Viewing && target != null)
            {
                time += Time.unscaledDeltaTime * idleFrequency;
                float offset = Mathf.Sin(time * Mathf.PI * 2f) * idleAmplitude;
                float yaw = Mathf.Sin(time * Mathf.PI * 2f * 0.5f) * idleYawDegrees;
                target.position = basePosition + inspectAnchor.up * offset;
                target.rotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);
                yield return null;
            }

            _idleRoutine = null;
        }

        private bool CanInspectItem(SkillInspectableItem item)
        {
            return item != null
                && State == SkillInspectState.Inactive
                && IsSkillStageActive()
                && item.CanInspect();
        }

        private bool IsSkillStageActive()
        {
            ResolveDependencies();
            return selectionManager != null
                && selectionManager.IsStageCountdownActive
                && !selectionManager.IsTransitioningToMatch
                && string.Equals(selectionManager.CurrentStagePropertyKey, PropertiesSelectionManager.SkillLoadoutStageKey, System.StringComparison.Ordinal);
        }

        private void ResolveDependencies()
        {
            if (selectionManager == null)
                selectionManager = PropertiesSelectionManager.Instance != null ? PropertiesSelectionManager.Instance : FindFirstObjectByType<PropertiesSelectionManager>();

            if (selectionController == null)
                selectionController = GetComponentInParent<SkillWallSelectionController>();

            if (inspectCamera == null)
                inspectCamera = Camera.main;

            if (focusProviderBehaviour == null && selectionController != null)
                focusProviderBehaviour = selectionController;

            if (_focusProvider == null && focusProviderBehaviour is ISkillSelectionFocusProvider focusProvider)
                _focusProvider = focusProvider;
        }

        private void EnsureInputActions()
        {
            if (_inputActions != null)
                return;

            _inputActions = new InputSystem_Actions();
            InputActionMap menuMap = _inputActions.Menu.Get();
            _menuCancelAction = menuMap.FindAction("MenuCancel");
            _menuInspectAction = menuMap.FindAction("MenuInspect");
        }

        private void EnableMenuInput()
        {
            EnsureInputActions();
            if (_menuCancelAction == null || _menuInspectAction == null)
                return;

            _menuCancelAction.performed -= HandleMenuCancelPerformed;
            _menuCancelAction.performed += HandleMenuCancelPerformed;
            _menuInspectAction.performed -= HandleMenuInspectPerformed;
            _menuInspectAction.performed += HandleMenuInspectPerformed;
            _inputActions.Menu.Enable();
        }

        private void DisableMenuInput()
        {
            if (_menuCancelAction != null)
                _menuCancelAction.performed -= HandleMenuCancelPerformed;

            if (_menuInspectAction != null)
                _menuInspectAction.performed -= HandleMenuInspectPerformed;

            if (_inputActions != null)
                _inputActions.Menu.Disable();
        }

        private void HandleMenuCancelPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || !IsSkillStageActive())
                return;

            if (State == SkillInspectState.Entering || State == SkillInspectState.Exiting)
                return;

            if (State == SkillInspectState.Viewing)
                BeginExit();
        }

        private void HandleMenuInspectPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || !IsSkillStageActive())
                return;

            if (State == SkillInspectState.Entering || State == SkillInspectState.Exiting)
                return;

            if (State == SkillInspectState.Viewing)
            {
                BeginExit();
                return;
            }

            SkillInspectableItem focused = GetFocusedOrHoveredItemUnderPointer();
            if (focused != null)
                BeginEnter(focused);
        }

        private SkillInspectableItem GetFocusedOrHoveredItemUnderPointer()
        {
            if (_focusProvider != null)
            {
                SkillInspectableItem focusedItem = _focusProvider.GetCurrentFocusedItem();
                if (focusedItem != null)
                    return focusedItem;
            }

            if (inspectCamera == null || Mouse.current == null)
                return null;

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            Ray ray = inspectCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hitInfo, inspectCamera.farClipPlane))
                return null;

            if (hitInfo.collider == null)
                return null;

            SkillInspectableItem hoveredItem = hitInfo.collider.GetComponentInParent<SkillInspectableItem>();
            if (hoveredItem == null || !hoveredItem.CanInspect())
                return null;

            return hoveredItem;
        }

        private void SetSelectionInputBlocked(bool blocked)
        {
            if (selectionController != null)
                selectionController.SetSelectionInputBlocked(blocked);
        }

        private static void PlayFeedback(MMF_Player feedbackPlayer)
        {
            if (feedbackPlayer == null)
                return;

            feedbackPlayer.Initialization();
            feedbackPlayer.PlayFeedbacks();
        }

        private void EnsureCurve()
        {
            if (moveCurve == null || moveCurve.length == 0)
                moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
}
