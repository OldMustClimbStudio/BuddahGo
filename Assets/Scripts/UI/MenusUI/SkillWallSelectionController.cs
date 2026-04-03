using System;
using System.Collections.Generic;
using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    public class SkillWallSelectionController : MonoBehaviour, ISkillSelectionFocusProvider
    {
        [Serializable]
        private class SpawnedSkillItem
        {
            public SelectablePropertyOption Option;
            public SkillWallItemView View;
            public int CurrentSlotIndex = -1;
        }

        [Header("Manager")]
        [SerializeField] private PropertiesSelectionManager selectionManager;
        [SerializeField] private bool autoFindManager = true;

        [Header("Scene Roots")]
        [SerializeField] private GameObject root;

        [Header("Wall")]
        [SerializeField] private Transform wallItemRoot;
        [SerializeField] private SkillWallItemView wallItemPrefab;
        [SerializeField] private Transform[] manualWallAnchors;
        [SerializeField] private int wallColumnCount = 4;
        [SerializeField] private float wallHorizontalSpacing = 1.2f;
        [SerializeField] private float wallVerticalSpacing = 0.8f;
        [SerializeField] private Vector3 wallLocalOrigin = Vector3.zero;

        [Header("Slots")]
        [SerializeField] private SkillSelectionSlotAnchor[] slotAnchors = new SkillSelectionSlotAnchor[SkillLoadout.SlotCount];

        [Header("Animation")]
        [SerializeField] private bool animateSelections = true;
        [SerializeField] private float moveDuration = 0.35f;
        [SerializeField] private float moveArcHeight = 0.45f;

        [Header("Submission")]
        [SerializeField] private Button confirmButton;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private readonly string[] _selectedSkillIds = new string[SkillLoadout.SlotCount];
        private readonly Dictionary<string, SpawnedSkillItem> _itemsBySkillId = new Dictionary<string, SpawnedSkillItem>();
        private readonly List<SpawnedSkillItem> _spawnedItems = new List<SpawnedSkillItem>();
        private readonly HashSet<int> _pendingSlotIndices = new HashSet<int>();
        private readonly Dictionary<string, Coroutine> _selectionAnimationsBySkillId = new Dictionary<string, Coroutine>();

        private PropertiesSelectionManager _subscribedManager;
        private string _lastStageKey = string.Empty;
        private string _lastOptionsSignature = string.Empty;
        private bool _initializedForSkillStage;
        private float _nextManagerDiagnosticTime;
        private bool _draftDirty;
        private bool _draftLocked;
        private bool _skillStageActive;
        private bool _selectionInputBlocked;
        private SkillWallItemView _focusedView;
        private SkillInspectController _inspectController;
        private InputSystem_Actions _inputActions;
        private InputAction _menuSelectAction;
        private int _lastMenuSelectFrame = -1;

        public IReadOnlyList<string> SelectedSkillIds => _selectedSkillIds;
        public bool IsSelectionInputBlocked => _selectionInputBlocked;

        private void Awake()
        {
            ClearLocalSelectionState();
            InitializeSlotControllers();
            EnsureInputActions();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        private void OnEnable()
        {
            EnableMenuInput();
            ResolveManager();
            SubscribeToManager();
            RefreshForCurrentState(forceRebuild: true);
        }

        private void Start()
        {
            ResolveManager();
            SubscribeToManager();
            RefreshForCurrentState(forceRebuild: true);
        }

        private void Update()
        {
            ResolveManager();
            SubscribeToManager();

            if (selectionManager == null)
            {
                EmitManagerDiagnosticIfNeeded();
                return;
            }

            string currentStageKey = selectionManager.CurrentStagePropertyKey;
            if (!string.Equals(currentStageKey, _lastStageKey, StringComparison.Ordinal))
                RefreshForCurrentState(forceRebuild: false);
        }

        private void OnDisable()
        {
            UnsubscribeFromManager();
            DisableMenuInput();
        }

        private void OnDestroy()
        {
            UnsubscribeFromManager();
            DisableMenuInput();
            ClearSpawnedWallItems();

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        public void RebuildFromManager()
        {
            RebuildWall();
        }

        public void BeginStage()
        {
            ResolveManager();
            InitializeSlotControllers();
            _skillStageActive = true;

            if (root != null)
                root.SetActive(true);

            RefreshActiveStageFromManager(forceRebuild: !_initializedForSkillStage || _spawnedItems.Count == 0);
        }

        public void EndStage()
        {
            if (!_skillStageActive && (root == null || !root.activeSelf) && _spawnedItems.Count == 0)
            {
                RefreshConfirmButtonState();
                return;
            }

            _skillStageActive = false;
            _initializedForSkillStage = false;
            _draftDirty = false;
            _draftLocked = false;
            _lastOptionsSignature = string.Empty;

            ResetView(clearSpawnedItems: true);

            if (root != null)
                root.SetActive(false);

            RefreshConfirmButtonState();
        }

        public void ResetView(bool clearSpawnedItems)
        {
            StopAllSelectionAnimations();
            ClearPendingReservations();
            ClearAllSlotOccupancy();

            for (int i = 0; i < _selectedSkillIds.Length; i++)
                _selectedSkillIds[i] = string.Empty;

            if (clearSpawnedItems)
            {
                for (int i = 0; i < _spawnedItems.Count; i++)
                {
                    if (_spawnedItems[i] != null && _spawnedItems[i].View != null)
                        Destroy(_spawnedItems[i].View.gameObject);
                }

                _spawnedItems.Clear();
                _itemsBySkillId.Clear();
            }
            else
            {
                for (int i = 0; i < _spawnedItems.Count; i++)
                {
                    SpawnedSkillItem item = _spawnedItems[i];
                    if (item == null || item.View == null)
                        continue;

                    item.CurrentSlotIndex = -1;
                    item.View.SetSelected(false);
                    item.View.SetInteractionLocked(false);
                    item.View.SnapToWallPose();
                }
            }
        }

        public void RebuildWall()
        {
            RefreshActiveStageFromManager(forceRebuild: true);
        }

        public bool RequestSelectSkill(string skillId)
        {
            if (!CanEditDraft())
                return false;

            skillId = (skillId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            if (!_itemsBySkillId.TryGetValue(skillId, out SpawnedSkillItem item))
            {
                LogDebug($"Ignored select request for unknown skill '{skillId}'.");
                return false;
            }

            if (FindSelectedSlotIndex(skillId) >= 0)
            {
                LogDebug($"Ignored select request because '{skillId}' is already selected.");
                return false;
            }

            SkillSelectionSlotAnchor emptySlot = FindFirstAvailableSlot();
            if (emptySlot == null)
            {
                LogDebug($"Ignored select request because all {SkillLoadout.SlotCount} slots are full.");
                PlayRejectedSelectionFeedback(item.View);
                return false;
            }

            BeginSelectionIntoSlot(item, emptySlot);
            return true;
        }

        public void HandleItemClicked(string skillId)
        {
            if (!CanEditDraft())
                return;

            int existingSlotIndex = FindSelectedSlotIndex(skillId);
            if (existingSlotIndex >= 0)
            {
                RemoveSkillFromSlot(existingSlotIndex);
                return;
            }

            RequestSelectSkill(skillId);
        }

        public void HandleItemInspectRequested(SkillInspectableItem item)
        {
            if (item == null)
                return;

            SkillInspectController inspectController = GetComponentInChildren<SkillInspectController>(true);
            if (inspectController != null)
                inspectController.RequestToggleInspect(item);
        }

        public void NotifyItemHoverEntered(SkillWallItemView itemView)
        {
            if (_selectionInputBlocked)
                return;

            _focusedView = itemView;
        }

        public void NotifyItemHoverExited(SkillWallItemView itemView)
        {
            if (_focusedView == itemView)
                _focusedView = null;
        }

        public SkillInspectableItem GetCurrentFocusedItem()
        {
            if (_focusedView == null || _selectionInputBlocked)
                return null;

            return _focusedView.InspectableItem;
        }

        public void SetSelectionInputBlocked(bool blocked)
        {
            _selectionInputBlocked = blocked;
            _focusedView = null;
            RefreshConfirmButtonState();
        }

        public bool WasMenuLeftClickThisFrame()
        {
            return _lastMenuSelectFrame == Time.frameCount;
        }

        public bool IsViewingInspectedItem(SkillWallItemView itemView)
        {
            if (itemView == null)
                return false;

            if (_inspectController == null)
                _inspectController = GetComponentInChildren<SkillInspectController>(true);

            return _inspectController != null
                && _inspectController.CurrentInspectedView == itemView;
        }

        public bool RemoveSkillFromSlot(int slotIndex)
        {
            if (!CanEditDraft() || !IsValidSlotIndex(slotIndex))
                return false;

            SkillSelectionSlotAnchor slot = GetSlotByIndex(slotIndex);
            if (slot == null || !slot.IsOccupied || slot.OccupyingItem == null)
                return false;

            SkillWallItemView itemView = slot.OccupyingItem;
            if (_itemsBySkillId.TryGetValue(itemView.SkillId, out SpawnedSkillItem spawnedItem))
            {
                StopSelectionAnimation(spawnedItem.Option.OptionId);
                spawnedItem.CurrentSlotIndex = -1;
                itemView.SetSelected(false);
                itemView.SetInteractionLocked(false);
                itemView.ReturnToWall(animateSelections);
            }

            slot.ClearOccupancy();
            RebuildSelectedSkillIdsFromSlots();
            _draftDirty = true;
            RefreshConfirmButtonState();
            return true;
        }

        public bool TryReplaceSlot(int slotIndex, string skillId)
        {
            if (!CanEditDraft() || !IsValidSlotIndex(slotIndex))
                return false;

            skillId = (skillId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            if (!_itemsBySkillId.TryGetValue(skillId, out SpawnedSkillItem item))
                return false;

            int existingSlotIndex = FindSelectedSlotIndex(skillId);
            if (existingSlotIndex >= 0 && existingSlotIndex != slotIndex)
                return false;

            if (existingSlotIndex == slotIndex)
                return true;

            RemoveSkillFromSlot(slotIndex);

            SkillSelectionSlotAnchor slot = GetSlotByIndex(slotIndex);
            if (slot == null)
                return false;

            BeginSelectionIntoSlot(item, slot);
            return true;
        }

        public void RebuildSelectedSkillIdsFromSlots()
        {
            for (int i = 0; i < _selectedSkillIds.Length; i++)
                _selectedSkillIds[i] = string.Empty;

            for (int i = 0; i < slotAnchors.Length; i++)
            {
                SkillSelectionSlotAnchor slot = slotAnchors[i];
                if (slot == null || !slot.IsOccupied || slot.OccupyingItem == null)
                    continue;

                int slotIndex = slot.SlotIndex;
                if (!IsValidSlotIndex(slotIndex))
                    continue;

                _selectedSkillIds[slotIndex] = slot.OccupyingItem.SkillId ?? string.Empty;
            }
        }

        private void HandleSelectionStateChanged()
        {
            RefreshForCurrentState(forceRebuild: false);
        }

        private void HandleConfirmClicked()
        {
            if (!CanConfirmSelection())
                return;

            SubmitCurrentSelection();
            _draftDirty = false;
            _draftLocked = true;
            RefreshConfirmButtonState();
        }

        private void RefreshForCurrentState(bool forceRebuild)
        {
            ResolveManager();

            if (selectionManager == null)
            {
                if (root != null)
                    root.SetActive(false);

                RefreshConfirmButtonState();
                return;
            }

            bool isSkillStage = IsSkillStageActive();
            string currentStageKey = selectionManager.CurrentStagePropertyKey;
            bool stageChanged = !string.Equals(currentStageKey, _lastStageKey, StringComparison.Ordinal);

            if (isSkillStage && (!_skillStageActive || stageChanged))
            {
                BeginStage();
                _lastStageKey = currentStageKey ?? string.Empty;
                RefreshConfirmButtonState();
                return;
            }

            if (!isSkillStage && _skillStageActive)
            {
                EndStage();
                _lastStageKey = currentStageKey ?? string.Empty;
                return;
            }

            if (root != null)
                root.SetActive(isSkillStage);

            if (!isSkillStage)
            {
                _lastStageKey = currentStageKey ?? string.Empty;
                RefreshConfirmButtonState();
                return;
            }

            _lastStageKey = currentStageKey ?? string.Empty;
            RefreshActiveStageFromManager(forceRebuild);
            RefreshConfirmButtonState();
        }

        private void RefreshActiveStageFromManager(bool forceRebuild)
        {
            ResolveManager();
            if (selectionManager == null)
            {
                RefreshConfirmButtonState();
                return;
            }

            List<SelectablePropertyOption> options = selectionManager.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey);
            string optionSignature = BuildOptionSignature(options);
            bool optionsChanged = !string.Equals(_lastOptionsSignature, optionSignature, StringComparison.Ordinal);
            bool shouldRebuild = forceRebuild || !_initializedForSkillStage || _spawnedItems.Count == 0 || optionsChanged;

            if (shouldRebuild)
            {
                BuildWall(options);
                _initializedForSkillStage = true;
                _lastOptionsSignature = optionSignature;
                SyncLocalSelectionFromManager();
                RefreshConfirmButtonState();
                return;
            }

            if (_draftLocked)
                SyncLocalSelectionFromManager();
            else if (!_draftDirty && !HasPendingLocalSelection())
                SyncLocalSelectionFromManager();

            RefreshConfirmButtonState();
        }

        private void ResolveManager()
        {
            if (selectionManager != null && (selectionManager.IsClientInitialized || selectionManager.IsServerInitialized))
                return;

            selectionManager = null;

            PropertiesSelectionManager singleton = PropertiesSelectionManager.Instance;
            if (singleton != null && (singleton.IsClientInitialized || singleton.IsServerInitialized))
            {
                selectionManager = singleton;
                return;
            }

            if (!autoFindManager)
                return;

            PropertiesSelectionManager sceneManager = FindFirstObjectByType<PropertiesSelectionManager>();
            if (sceneManager != null && (sceneManager.IsClientInitialized || sceneManager.IsServerInitialized))
                selectionManager = sceneManager;
        }

        private void EnsureInputActions()
        {
            if (_inputActions != null)
                return;

            _inputActions = new InputSystem_Actions();
            _menuSelectAction = _inputActions.Menu.Get().FindAction("MenuSelect");
        }

        private void EnableMenuInput()
        {
            EnsureInputActions();
            if (_menuSelectAction == null)
                return;

            _menuSelectAction.performed -= HandleMenuSelectPerformed;
            _menuSelectAction.performed += HandleMenuSelectPerformed;
            _inputActions.Menu.Enable();
        }

        private void DisableMenuInput()
        {
            if (_menuSelectAction != null)
                _menuSelectAction.performed -= HandleMenuSelectPerformed;

            if (_inputActions != null)
                _inputActions.Menu.Disable();
        }

        private void HandleMenuSelectPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            _lastMenuSelectFrame = Time.frameCount;
        }

        private void SubscribeToManager()
        {
            if (selectionManager == null || _subscribedManager == selectionManager)
                return;

            UnsubscribeFromManager();
            selectionManager.OnSelectionStateChanged += HandleSelectionStateChanged;
            _subscribedManager = selectionManager;
        }

        private void UnsubscribeFromManager()
        {
            if (_subscribedManager == null)
                return;

            _subscribedManager.OnSelectionStateChanged -= HandleSelectionStateChanged;
            _subscribedManager = null;
        }

        private void BuildWall(List<SelectablePropertyOption> options)
        {
            ClearSpawnedWallItems();
            ClearLocalSelectionState();
            ClearAllSlotOccupancy();
            ClearPendingReservations();
            _draftDirty = false;
            _draftLocked = false;

            Transform motionRoot = GetMotionRoot();
            if (motionRoot == null || wallItemPrefab == null || options == null)
            {
                LogDebug("Skipped wall build because required references are missing.");
                return;
            }

            int safeColumnCount = Mathf.Max(1, wallColumnCount);
            for (int i = 0; i < options.Count; i++)
            {
                SelectablePropertyOption option = options[i];
                if (string.IsNullOrWhiteSpace(option.OptionId))
                    continue;

                SkillWallItemView view = Instantiate(wallItemPrefab, motionRoot);
                GetWallSpawnPose(i, safeColumnCount, out Vector3 localPosition, out Quaternion localRotation);

                view.transform.localPosition = localPosition;
                view.transform.localRotation = localRotation;
                view.Initialize(this, option);
                view.SetWallPose(localPosition, localRotation);

                SpawnedSkillItem spawned = new SpawnedSkillItem
                {
                    Option = option,
                    View = view,
                    CurrentSlotIndex = -1
                };

                _spawnedItems.Add(spawned);
                _itemsBySkillId[option.OptionId] = spawned;
            }

            LogDebug($"Built skill wall with {_spawnedItems.Count} selectable items.");
        }

        private void SyncLocalSelectionFromManager()
        {
            StopAllSelectionAnimations();

            string[] managerSkillIds = null;
            selectionManager.TryGetLocalSkillLoadoutSelection(out managerSkillIds);

            ClearLocalSelectionState();
            ClearPendingReservations();
            ClearAllSlotOccupancy();

            for (int slotIndex = 0; slotIndex < SkillLoadout.SlotCount; slotIndex++)
            {
                string skillId = managerSkillIds != null && slotIndex < managerSkillIds.Length
                    ? (managerSkillIds[slotIndex] ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(skillId))
                    continue;

                if (!_itemsBySkillId.TryGetValue(skillId, out SpawnedSkillItem item))
                    continue;

                SkillSelectionSlotAnchor slot = GetSlotByIndex(slotIndex);
                if (slot == null)
                    continue;

                item.CurrentSlotIndex = slotIndex;
                item.View.SetSelected(true);
                if (!IsViewingInspectedItem(item.View))
                    item.View.SetInteractionLocked(true);
                slot.SetOccupied(item.View);
                if (!IsViewingInspectedItem(item.View))
                    item.View.MoveToWorldPose(slot.SnapAnchor.position, slot.SnapAnchor.rotation, 0f);
            }

            RebuildSelectedSkillIdsFromSlots();
            _draftDirty = false;
            _draftLocked = HasCompleteSelection();

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                SpawnedSkillItem item = _spawnedItems[i];
                if (item.CurrentSlotIndex >= 0)
                    continue;

                if (IsViewingInspectedItem(item.View))
                    continue;

                item.View.SetSelected(false);
                item.View.SetInteractionLocked(false);
                if (item.View.IsHovered)
                    item.View.RefreshHoverPose();
                else
                    item.View.SnapToWallPose();
            }
        }

        private void BeginSelectionIntoSlot(SpawnedSkillItem item, SkillSelectionSlotAnchor slot)
        {
            if (item == null || slot == null || !IsValidSlotIndex(slot.SlotIndex))
                return;

            _draftDirty = true;
            _pendingSlotIndices.Add(slot.SlotIndex);
            item.View.SetSelected(true);
            item.View.SetInteractionLocked(true);

            StopSelectionAnimation(item.Option.OptionId);
            Coroutine routine = StartCoroutine(AnimateSelectionIntoSlot(item, slot, animateSelections ? moveDuration : 0f));
            _selectionAnimationsBySkillId[item.Option.OptionId] = routine;
        }

        private System.Collections.IEnumerator AnimateSelectionIntoSlot(SpawnedSkillItem item, SkillSelectionSlotAnchor slot, float duration)
        {
            if (item == null || item.View == null || slot == null)
                yield break;

            Transform snapAnchor = slot.SnapAnchor;
            if (duration > 0f)
            {
                yield return item.View.AnimateFlightToWorldPose(
                    snapAnchor.position,
                    snapAnchor.rotation,
                    duration,
                    moveArcHeight);
            }
            else
            {
                item.View.MoveToWorldPose(snapAnchor.position, snapAnchor.rotation, 0f);
            }

            FinalizeItemIntoSlot(item, slot);
            _selectionAnimationsBySkillId.Remove(item.Option.OptionId);
        }

        private void FinalizeItemIntoSlot(SpawnedSkillItem item, SkillSelectionSlotAnchor slot)
        {
            if (item == null || item.View == null || slot == null)
                return;

            _pendingSlotIndices.Remove(slot.SlotIndex);
            slot.SetOccupied(item.View);
            item.CurrentSlotIndex = slot.SlotIndex;
            item.View.SetSelected(true);
            item.View.SetInteractionLocked(true);

            RebuildSelectedSkillIdsFromSlots();
            _draftDirty = true;
            RefreshConfirmButtonState();
        }

        private void SubmitCurrentSelection()
        {
            if (selectionManager == null)
                return;

            selectionManager.SubmitSkillLoadoutSelection(_selectedSkillIds);
        }

        private void ClearSpawnedWallItems()
        {
            StopAllSelectionAnimations();

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null && _spawnedItems[i].View != null)
                    Destroy(_spawnedItems[i].View.gameObject);
            }

            _spawnedItems.Clear();
            _itemsBySkillId.Clear();
        }

        private void ClearLocalSelectionState()
        {
            for (int i = 0; i < _selectedSkillIds.Length; i++)
                _selectedSkillIds[i] = string.Empty;

            for (int i = 0; i < _spawnedItems.Count; i++)
                _spawnedItems[i].CurrentSlotIndex = -1;
        }

        private void ClearAllSlotOccupancy()
        {
            for (int i = 0; i < slotAnchors.Length; i++)
            {
                if (slotAnchors[i] != null)
                    slotAnchors[i].ClearOccupancy();
            }
        }

        private void ClearPendingReservations()
        {
            _pendingSlotIndices.Clear();
        }

        private void InitializeSlotControllers()
        {
            for (int i = 0; i < slotAnchors.Length; i++)
            {
                if (slotAnchors[i] != null)
                    slotAnchors[i].Initialize(this);
            }
        }

        private Vector3 ComputeWallLocalPosition(int itemIndex, int columnCount)
        {
            int row = itemIndex / columnCount;
            int column = itemIndex % columnCount;

            float totalWidth = (columnCount - 1) * wallHorizontalSpacing;
            float x = column * wallHorizontalSpacing - totalWidth * 0.5f;
            float y = -(row * wallVerticalSpacing);

            return wallLocalOrigin + new Vector3(x, y, 0f);
        }

        private void GetWallSpawnPose(int itemIndex, int columnCount, out Vector3 localPosition, out Quaternion localRotation)
        {
            if (TryGetManualWallAnchorPose(itemIndex, out localPosition, out localRotation))
                return;

            Transform motionRoot = GetMotionRoot();
            if (wallItemRoot != null && motionRoot != null && motionRoot != wallItemRoot)
            {
                Vector3 wallLocalPosition = ComputeWallLocalPosition(itemIndex, columnCount);
                Vector3 worldPosition = wallItemRoot.TransformPoint(wallLocalPosition);
                localPosition = motionRoot.InverseTransformPoint(worldPosition);
                localRotation = Quaternion.Inverse(motionRoot.rotation) * wallItemRoot.rotation;
                return;
            }

            localPosition = ComputeWallLocalPosition(itemIndex, columnCount);
            localRotation = Quaternion.identity;
        }

        private bool TryGetManualWallAnchorPose(int itemIndex, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;

            if (manualWallAnchors == null || itemIndex < 0 || itemIndex >= manualWallAnchors.Length)
                return false;

            Transform anchor = manualWallAnchors[itemIndex];
            if (anchor == null)
                return false;

            Transform motionRoot = GetMotionRoot();
            if (motionRoot != null)
            {
                localPosition = motionRoot.InverseTransformPoint(anchor.position);
                localRotation = Quaternion.Inverse(motionRoot.rotation) * anchor.rotation;
            }
            else
            {
                localPosition = anchor.position;
                localRotation = anchor.rotation;
            }

            return true;
        }

        private Transform GetMotionRoot()
        {
            if (wallItemRoot == null)
                return null;

            return wallItemRoot.parent != null ? wallItemRoot.parent : wallItemRoot;
        }

        private static string BuildOptionSignature(List<SelectablePropertyOption> options)
        {
            if (options == null || options.Count == 0)
                return string.Empty;

            List<string> parts = new List<string>(options.Count);
            for (int i = 0; i < options.Count; i++)
                parts.Add(options[i].OptionId ?? string.Empty);

            return string.Join("|", parts);
        }

        private SkillSelectionSlotAnchor FindFirstAvailableSlot()
        {
            for (int i = 0; i < slotAnchors.Length; i++)
            {
                SkillSelectionSlotAnchor slot = slotAnchors[i];
                if (slot == null)
                    continue;

                if (!slot.IsOccupied && !_pendingSlotIndices.Contains(slot.SlotIndex))
                    return slot;
            }

            return null;
        }

        private int FindSelectedSlotIndex(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return -1;

            for (int i = 0; i < _selectedSkillIds.Length; i++)
            {
                if (string.Equals(_selectedSkillIds[i], skillId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private bool IsSkillStageActive()
        {
            return selectionManager != null
                && string.Equals(selectionManager.CurrentStagePropertyKey, PropertiesSelectionManager.SkillLoadoutStageKey, StringComparison.Ordinal);
        }

        private bool CanEdit()
        {
            return selectionManager != null
                && !_selectionInputBlocked
                && selectionManager.IsStageCountdownActive
                && !selectionManager.IsTransitioningToMatch
                && IsSkillStageActive();
        }

        private bool CanEditDraft()
        {
            return CanEdit() && !_draftLocked;
        }

        private bool CanConfirmSelection()
        {
            return CanEditDraft() && HasCompleteSelection();
        }

        private bool HasCompleteSelection()
        {
            for (int i = 0; i < _selectedSkillIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_selectedSkillIds[i]))
                    return false;
            }

            return true;
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SkillLoadout.SlotCount;
        }

        private SkillSelectionSlotAnchor GetSlotByIndex(int slotIndex)
        {
            for (int i = 0; i < slotAnchors.Length; i++)
            {
                SkillSelectionSlotAnchor slot = slotAnchors[i];
                if (slot != null && slot.SlotIndex == slotIndex)
                    return slot;
            }

            return null;
        }

        private void StopSelectionAnimation(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return;

            if (!_selectionAnimationsBySkillId.TryGetValue(skillId, out Coroutine routine) || routine == null)
                return;

            StopCoroutine(routine);
            _selectionAnimationsBySkillId.Remove(skillId);
        }

        private bool HasPendingLocalSelection()
        {
            return _pendingSlotIndices.Count > 0 || _selectionAnimationsBySkillId.Count > 0;
        }

        private void StopAllSelectionAnimations()
        {
            List<string> animatedSkillIds = new List<string>(_selectionAnimationsBySkillId.Keys);
            for (int i = 0; i < animatedSkillIds.Count; i++)
                StopSelectionAnimation(animatedSkillIds[i]);
        }

        private void PlayRejectedSelectionFeedback(SkillWallItemView itemView)
        {
            if (itemView == null)
                return;

            // Reserved for future bounce/shake feedback when all slots are full.
        }

        private void RefreshConfirmButtonState()
        {
            if (confirmButton == null)
                return;

            confirmButton.gameObject.SetActive(IsSkillStageActive() && !_draftLocked);
            confirmButton.interactable = CanConfirmSelection();
        }

        private void EmitManagerDiagnosticIfNeeded()
        {
            if (!enableDebugLogs || Time.unscaledTime < _nextManagerDiagnosticTime)
                return;

            _nextManagerDiagnosticTime = Time.unscaledTime + 2f;

            PropertiesSelectionManager sceneManager = FindFirstObjectByType<PropertiesSelectionManager>();
            bool hasSceneManager = sceneManager != null;
            bool isSpawned = hasSceneManager
                && sceneManager.NetworkObject != null
                && sceneManager.NetworkObject.IsSpawned;

            Debug.LogWarning(
                $"[SkillWallSelectionController] Waiting for network-ready PropertiesSelectionManager. " +
                $"sceneManagerFound={hasSceneManager}, spawned={isSpawned}, singletonNull={PropertiesSelectionManager.Instance == null}");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[SkillWallSelectionController] {message}");
        }
    }
}
