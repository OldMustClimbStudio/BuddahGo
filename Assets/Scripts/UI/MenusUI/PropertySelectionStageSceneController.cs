using System;
using System.Collections.Generic;
using Cinemachine;
using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class PropertySelectionStageSceneController : MonoBehaviour
    {
        private enum CameraControlMode
        {
            PrioritySwitch = 0,
            SmoothRigTransition = 1
        }

        [Header("Manager")]
        [SerializeField] private PropertiesSelectionManager selectionManager;
        [SerializeField] private bool autoFindManager = true;

        [Header("Camera Control")]
        [SerializeField] private CameraControlMode cameraControlMode = CameraControlMode.SmoothRigTransition;

        [Header("Stage Cameras")]
        [SerializeField] private CinemachineVirtualCamera mapStageCamera;
        [SerializeField] private CinemachineVirtualCamera skillStageCamera;
        [SerializeField] private CinemachineVirtualCamera thirdStageCamera;

        [Header("Smooth Camera Rig")]
        [SerializeField] private Transform transitionCameraRig;
        [SerializeField] private CinemachineVirtualCamera transitionVirtualCamera;
        [SerializeField] private Transform mapStageAnchor;
        [SerializeField] private Transform skillStageAnchor;
        [SerializeField] private Transform thirdStageAnchor;
        [SerializeField] private Transform mapCurveHandle;
        [SerializeField] private Transform skillCurveHandle;
        [SerializeField] private Transform thirdCurveHandle;
        [SerializeField] private float transitionDuration = 1.1f;
        [SerializeField] private AnimationCurve transitionCurve = null;
        [SerializeField] private bool snapToStageOnStart = true;

        [Header("Stage Roots")]
        [SerializeField] private bool controlStageRoots = false;
        [SerializeField] private GameObject mapStageRoot;
        [SerializeField] private GameObject skillStageRoot;
        [SerializeField] private GameObject thirdStageRoot;
        [SerializeField] private SkillWallSelectionController skillWallSelectionController;

        [Header("Camera Priority")]
        [SerializeField] private int activeCameraPriority = 100;
        [SerializeField] private int inactiveCameraPriority = 0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private PropertiesSelectionManager _subscribedManager;
        private string _lastAppliedStageKey = string.Empty;
        private StageViewTarget _lastAppliedTarget = StageViewTarget.Map;
        private bool _warnedMissingManager;
        private bool _hasAppliedInitialStage;
        private Coroutine _cameraTransitionRoutine;

        private void Awake()
        {
            EnsureTransitionCurve();
            ResolveManager();
        }

        private void OnEnable()
        {
            ResolveManager();
            SubscribeToManager();
            RefreshStageState(logIfChanged: false);
        }

        private void Start()
        {
            ResolveManager();
            SubscribeToManager();
            RefreshStageState(logIfChanged: false);
        }

        private void Update()
        {
            if (!HasValidManager())
            {
                ResolveManager();
                SubscribeToManager();
            }

            if (HasValidManager())
                RefreshStageState(logIfChanged: false);
        }

        private void OnDisable()
        {
            StopCameraTransition();

            if (skillWallSelectionController != null && _lastAppliedTarget == StageViewTarget.Skill)
                skillWallSelectionController.EndStage();

            UnsubscribeFromManager();
        }

        private void OnDestroy()
        {
            StopCameraTransition();

            if (skillWallSelectionController != null && _lastAppliedTarget == StageViewTarget.Skill)
                skillWallSelectionController.EndStage();

            UnsubscribeFromManager();
        }

        private void HandleSelectionStateChanged()
        {
            RefreshStageState(logIfChanged: true);
        }

        private void ResolveManager()
        {
            if (HasValidManager())
                return;

            if (selectionManager != null && IsManagerReady(selectionManager))
            {
                _warnedMissingManager = false;
                return;
            }

            selectionManager = null;

            PropertiesSelectionManager singleton = PropertiesSelectionManager.Instance;
            if (singleton != null && IsManagerReady(singleton))
            {
                selectionManager = singleton;
                _warnedMissingManager = false;
                return;
            }

            if (autoFindManager)
            {
                PropertiesSelectionManager sceneManager = FindFirstObjectByType<PropertiesSelectionManager>();
                if (sceneManager != null && IsManagerReady(sceneManager))
                {
                    selectionManager = sceneManager;
                    _warnedMissingManager = false;
                    return;
                }
            }

            if (!_warnedMissingManager && enableDebugLogs)
            {
                Debug.Log("[PropertySelectionStageSceneController] Waiting for PropertiesSelectionManager...");
                _warnedMissingManager = true;
            }
        }

        private void SubscribeToManager()
        {
            if (!HasValidManager())
                return;

            if (_subscribedManager == selectionManager)
                return;

            UnsubscribeFromManager();

            selectionManager.OnSelectionStateChanged += HandleSelectionStateChanged;
            _subscribedManager = selectionManager;

            if (enableDebugLogs)
                Debug.Log("[PropertySelectionStageSceneController] Subscribed to PropertiesSelectionManager.");
        }

        private void UnsubscribeFromManager()
        {
            if (_subscribedManager == null)
                return;

            _subscribedManager.OnSelectionStateChanged -= HandleSelectionStateChanged;
            _subscribedManager = null;
        }

        private void RefreshStageState(bool logIfChanged)
        {
            if (!HasValidManager())
                return;

            string currentStageKey = PropertiesSelectionManager.Instance != null
                ? PropertiesSelectionManager.Instance.CurrentStagePropertyKey
                : selectionManager.CurrentStagePropertyKey;

            StageViewTarget target = ResolveStageTarget(currentStageKey);

            bool changed = !string.Equals(_lastAppliedStageKey, currentStageKey, StringComparison.Ordinal)
                || _lastAppliedTarget != target;

            ApplyVisualState(target, changed);

            if (changed)
            {
                HandleStageLifecycle(_lastAppliedTarget, target);
                _lastAppliedStageKey = currentStageKey ?? string.Empty;
                _lastAppliedTarget = target;
                _hasAppliedInitialStage = true;

                if (logIfChanged && enableDebugLogs)
                {
                    Debug.Log(
                        $"[PropertySelectionStageSceneController] Stage switched to '{_lastAppliedStageKey}' -> {target}.");
                }
            }
        }

        private void HandleStageLifecycle(StageViewTarget previousTarget, StageViewTarget newTarget)
        {
            if (skillWallSelectionController == null)
                return;

            if (previousTarget == StageViewTarget.Skill && newTarget != StageViewTarget.Skill)
                skillWallSelectionController.EndStage();

            if (newTarget == StageViewTarget.Skill)
                skillWallSelectionController.BeginStage();
        }

        private void ApplyVisualState(StageViewTarget target, bool changed)
        {
            if (cameraControlMode == CameraControlMode.SmoothRigTransition && CanUseSmoothRig())
            {
                ApplySmoothRigState(target, changed);
                return;
            }

            ApplyExclusiveStageRoot(target);
            ApplyPriorityCameraState(target);
        }

        private void ApplySmoothRigState(StageViewTarget target, bool changed)
        {
            SetCameraPriority(transitionVirtualCamera, activeCameraPriority);
            SetCameraPriority(mapStageCamera, inactiveCameraPriority);
            SetCameraPriority(skillStageCamera, inactiveCameraPriority);
            SetCameraPriority(thirdStageCamera, inactiveCameraPriority);

            if (!_hasAppliedInitialStage || !changed || snapToStageOnStart)
            {
                if (!_hasAppliedInitialStage)
                {
                    StopCameraTransition();
                    SnapRigToTarget(target);
                    ApplyExclusiveStageRoot(target);
                    return;
                }

                if (!changed)
                    return;
            }

            if (changed)
            {
                StopCameraTransition();
                ApplyStageRootsDuringTransition(_lastAppliedTarget, target);
                _cameraTransitionRoutine = StartCoroutine(AnimateRigToTarget(target));
            }
        }

        private void ApplyExclusiveStageRoot(StageViewTarget target)
        {
            if (!controlStageRoots)
                return;

            Dictionary<GameObject, bool> desiredStates = new Dictionary<GameObject, bool>();
            RegisterDesiredRootState(desiredStates, mapStageRoot, target == StageViewTarget.Map);
            RegisterDesiredRootState(desiredStates, skillStageRoot, target == StageViewTarget.Skill);
            RegisterDesiredRootState(desiredStates, thirdStageRoot, target == StageViewTarget.Third);
            ApplyDesiredRootStates(desiredStates);
        }

        private void ApplyStageRootsDuringTransition(StageViewTarget fromTarget, StageViewTarget toTarget)
        {
            if (!controlStageRoots)
                return;

            Dictionary<GameObject, bool> desiredStates = new Dictionary<GameObject, bool>();
            RegisterDesiredRootState(desiredStates, mapStageRoot, fromTarget == StageViewTarget.Map || toTarget == StageViewTarget.Map);
            RegisterDesiredRootState(desiredStates, skillStageRoot, fromTarget == StageViewTarget.Skill || toTarget == StageViewTarget.Skill);
            RegisterDesiredRootState(desiredStates, thirdStageRoot, fromTarget == StageViewTarget.Third || toTarget == StageViewTarget.Third);
            ApplyDesiredRootStates(desiredStates);
        }

        private void ApplyPriorityCameraState(StageViewTarget target)
        {
            SetCameraPriority(mapStageCamera, target == StageViewTarget.Map ? activeCameraPriority : inactiveCameraPriority);
            SetCameraPriority(skillStageCamera, target == StageViewTarget.Skill ? activeCameraPriority : inactiveCameraPriority);
            SetCameraPriority(thirdStageCamera, target == StageViewTarget.Third ? activeCameraPriority : inactiveCameraPriority);
            SetCameraPriority(transitionVirtualCamera, inactiveCameraPriority);
        }

        private StageViewTarget ResolveStageTarget(string stageKey)
        {
            if (string.Equals(stageKey, PropertiesSelectionManager.MapStageKey, StringComparison.Ordinal))
                return StageViewTarget.Map;

            if (string.Equals(stageKey, PropertiesSelectionManager.SkillLoadoutStageKey, StringComparison.Ordinal))
                return StageViewTarget.Skill;

            if (string.Equals(stageKey, PropertiesSelectionManager.SkinStageKey, StringComparison.Ordinal))
                return StageViewTarget.Third;

            if (selectionManager != null && selectionManager.CurrentStageIndex >= 2)
                return StageViewTarget.Third;

            return StageViewTarget.Map;
        }

        private bool HasValidManager()
        {
            return selectionManager != null && IsManagerReady(selectionManager);
        }

        private bool CanUseSmoothRig()
        {
            return transitionCameraRig != null
                && transitionVirtualCamera != null
                && mapStageAnchor != null
                && skillStageAnchor != null
                && thirdStageAnchor != null;
        }

        private void EnsureTransitionCurve()
        {
            if (transitionCurve == null || transitionCurve.length == 0)
                transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private void SnapRigToTarget(StageViewTarget target)
        {
            Transform anchor = GetAnchor(target);
            if (transitionCameraRig == null || anchor == null)
                return;

            transitionCameraRig.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private System.Collections.IEnumerator AnimateRigToTarget(StageViewTarget target)
        {
            Transform anchor = GetAnchor(target);
            if (transitionCameraRig == null || anchor == null)
            {
                ApplyExclusiveStageRoot(target);
                yield break;
            }

            EnsureTransitionCurve();

            Vector3 startPosition = transitionCameraRig.position;
            Quaternion startRotation = transitionCameraRig.rotation;
            Vector3 endPosition = anchor.position;
            Quaternion endRotation = anchor.rotation;

            Vector3 controlPointA = GetOutgoingControlPoint(_lastAppliedTarget, startPosition, endPosition);
            Vector3 controlPointB = GetIncomingControlPoint(target, startPosition, endPosition);

            float duration = Mathf.Max(0.01f, transitionDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedT = transitionCurve.Evaluate(normalizedTime);

                transitionCameraRig.position = EvaluateCubicBezier(startPosition, controlPointA, controlPointB, endPosition, curvedT);
                transitionCameraRig.rotation = Quaternion.Slerp(startRotation, endRotation, curvedT);

                yield return null;
            }

            transitionCameraRig.SetPositionAndRotation(endPosition, endRotation);
            ApplyExclusiveStageRoot(target);
            _cameraTransitionRoutine = null;
        }

        private void StopCameraTransition()
        {
            if (_cameraTransitionRoutine == null)
                return;

            StopCoroutine(_cameraTransitionRoutine);
            _cameraTransitionRoutine = null;
        }

        private Transform GetAnchor(StageViewTarget target)
        {
            switch (target)
            {
                case StageViewTarget.Map:
                    return mapStageAnchor;
                case StageViewTarget.Skill:
                    return skillStageAnchor;
                case StageViewTarget.Third:
                    return thirdStageAnchor;
                default:
                    return mapStageAnchor;
            }
        }

        private Transform GetCurveHandle(StageViewTarget target)
        {
            switch (target)
            {
                case StageViewTarget.Map:
                    return mapCurveHandle;
                case StageViewTarget.Skill:
                    return skillCurveHandle;
                case StageViewTarget.Third:
                    return thirdCurveHandle;
                default:
                    return null;
            }
        }

        private Vector3 GetOutgoingControlPoint(StageViewTarget fromTarget, Vector3 startPosition, Vector3 endPosition)
        {
            Transform handle = GetCurveHandle(fromTarget);
            if (handle != null)
                return handle.position;

            Vector3 direction = (endPosition - startPosition).normalized;
            float distance = Vector3.Distance(startPosition, endPosition);
            return startPosition + direction * distance * 0.35f;
        }

        private Vector3 GetIncomingControlPoint(StageViewTarget toTarget, Vector3 startPosition, Vector3 endPosition)
        {
            Transform handle = GetCurveHandle(toTarget);
            if (handle != null)
                return handle.position;

            Vector3 direction = (startPosition - endPosition).normalized;
            float distance = Vector3.Distance(startPosition, endPosition);
            return endPosition + direction * distance * 0.35f;
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0
                + 3f * oneMinusT * oneMinusT * t * p1
                + 3f * oneMinusT * t * t * p2
                + t * t * t * p3;
        }

        private static bool IsManagerReady(PropertiesSelectionManager manager)
        {
            return manager != null && (manager.IsClientInitialized || manager.IsServerInitialized);
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private static void RegisterDesiredRootState(Dictionary<GameObject, bool> desiredStates, GameObject root, bool shouldBeActive)
        {
            if (root == null)
                return;

            if (desiredStates.TryGetValue(root, out bool currentState))
            {
                desiredStates[root] = currentState || shouldBeActive;
                return;
            }

            desiredStates.Add(root, shouldBeActive);
        }

        private static void ApplyDesiredRootStates(Dictionary<GameObject, bool> desiredStates)
        {
            foreach (KeyValuePair<GameObject, bool> pair in desiredStates)
                SetActiveSafe(pair.Key, pair.Value);
        }

        private static void SetCameraPriority(CinemachineVirtualCamera virtualCamera, int priority)
        {
            if (virtualCamera != null && virtualCamera.Priority != priority)
                virtualCamera.Priority = priority;
        }

        private enum StageViewTarget
        {
            Map = 0,
            Skill = 1,
            Third = 2
        }
    }
}
