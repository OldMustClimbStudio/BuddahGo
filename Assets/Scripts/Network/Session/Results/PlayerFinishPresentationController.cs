using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public class PlayerFinishPresentationController : MonoBehaviour
    {
        private const string DefaultDissolveProperty = "_DissolveAmount";

        public enum PresentationState
        {
            Racing,
            FinishedDissolvingOut,
            HiddenInResultAreaWaitingReveal,
            RevealingInResultArea,
            ResultAreaInteractive
        }

        [Header("Dissolve")]
        [SerializeField] private string dissolvePropertyName = DefaultDissolveProperty;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private bool enableDebugLogs;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<MaterialPropertyBlock> _propertyBlocks = new List<MaterialPropertyBlock>();
        private Collider[] _colliders;
        private Rigidbody _rigidbody;
        private BuddahMovement _movement;
        private NetworkObject _networkObject;
        private Coroutine _dissolveRoutine;
        private float _dissolveAmount;
        private PresentationState _pendingStateAfterDissolve = PresentationState.Racing;
        private bool _localLossOfControlCinematicPlayed;

        public PresentationState CurrentState { get; private set; }
        public bool BlocksMovementInput { get; private set; }
        public bool BlocksSkillInput { get; private set; }
        public bool BlocksRaceProgression { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            CurrentState = PresentationState.Racing;
            SetDissolveAmount(0f);
            SetSuppressedState(false, false);
        }

        public void ApplyFinishedPresentation(float dissolveDurationSeconds, bool forcedByGlobalEnd)
        {
            CurrentState = PresentationState.FinishedDissolvingOut;
            _pendingStateAfterDissolve = PresentationState.HiddenInResultAreaWaitingReveal;
            BlocksMovementInput = true;
            BlocksSkillInput = true;
            BlocksRaceProgression = true;
            ApplyPhysicsSuppression(false);
            SetCollidersEnabled(true);
            TryPlayLocalLossOfControlTimeline();
            PlayDissolveOut(dissolveDurationSeconds);
            DebugLog($"ApplyFinishedPresentation forcedByGlobalEnd={forcedByGlobalEnd}");
        }

        public void ApplyResultReveal(float dissolveDurationSeconds)
        {
            CurrentState = PresentationState.RevealingInResultArea;
            _pendingStateAfterDissolve = PresentationState.RevealingInResultArea;
            BlocksMovementInput = true;
            BlocksSkillInput = true;
            BlocksRaceProgression = true;
            ApplyPhysicsSuppression(false);
            SetCollidersEnabled(true);
            SetDissolveAmount(1f);
            PlayDissolveIn(dissolveDurationSeconds);
            DebugLog($"ApplyResultReveal duration={dissolveDurationSeconds:0.00} startAmount={_dissolveAmount:0.00}");
        }

        public void EnterResultInteractive(bool allowMovement, bool allowSkills)
        {
            CurrentState = PresentationState.ResultAreaInteractive;
            _pendingStateAfterDissolve = PresentationState.ResultAreaInteractive;
            BlocksMovementInput = !allowMovement;
            BlocksSkillInput = !allowSkills;
            BlocksRaceProgression = true;
            ApplyPhysicsSuppression(!allowMovement);
            SetCollidersEnabled(true);
            LocalFinishPresentationTimelineBridge.Instance?.HandleEnteredResultAreaInteractive();
            DebugLog($"EnterResultInteractive allowMovement={allowMovement} allowSkills={allowSkills}");
        }

        public void EnterHiddenInResultAreaWaitingReveal()
        {
            CurrentState = PresentationState.HiddenInResultAreaWaitingReveal;
            _pendingStateAfterDissolve = PresentationState.HiddenInResultAreaWaitingReveal;
            SetSuppressedState(true, true);
            SetDissolveAmount(1f);
            DebugLog($"EnterHiddenInResultAreaWaitingReveal amount={_dissolveAmount:0.00}");
        }

        public void ResetToRaceGameplay()
        {
            CurrentState = PresentationState.Racing;
            _pendingStateAfterDissolve = PresentationState.Racing;
            _localLossOfControlCinematicPlayed = false;
            SetSuppressedState(false, false);
            SetDissolveAmount(0f);
            LocalFinishPresentationTimelineBridge.Instance?.HandleRaceReset();
            DebugLog("ResetToRaceGameplay");
        }

        public void SetDissolveAmount(float value)
        {
            ResolveReferences();
            _dissolveAmount = Mathf.Clamp01(value);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                MaterialPropertyBlock block = _propertyBlocks[i];
                renderer.GetPropertyBlock(block);
                block.SetFloat(dissolvePropertyName, _dissolveAmount);
                renderer.SetPropertyBlock(block);
            }
        }

        public void PlayDissolveOut(float durationSeconds)
        {
            StartDissolveRoutine(1f, durationSeconds);
        }

        public void PlayDissolveIn(float durationSeconds)
        {
            StartDissolveRoutine(0f, durationSeconds);
        }

        private void StartDissolveRoutine(float targetValue, float durationSeconds)
        {
            if (_dissolveRoutine != null)
                StopCoroutine(_dissolveRoutine);

            _dissolveRoutine = StartCoroutine(DissolveRoutine(targetValue, durationSeconds));
        }

        private IEnumerator DissolveRoutine(float targetValue, float durationSeconds)
        {
            float startValue = _dissolveAmount;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, durationSeconds);

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                SetDissolveAmount(Mathf.Lerp(startValue, targetValue, t));
                yield return null;
            }

            SetDissolveAmount(targetValue);
            if (Mathf.Approximately(targetValue, 1f))
            {
                CurrentState = _pendingStateAfterDissolve;
            }
            else if (Mathf.Approximately(targetValue, 0f))
            {
                DebugLog("Dissolve-in completed at amount=0.00");
            }
            _dissolveRoutine = null;
        }

        private void ResolveReferences()
        {
            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>() ?? GetComponentInParent<NetworkObject>();

            if (_movement == null)
                _movement = GetComponent<BuddahMovement>() ?? GetComponentInParent<BuddahMovement>();

            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider>(includeInactiveRenderers);

            if (_renderers.Count == 0)
            {
                Renderer[] foundRenderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
                for (int i = 0; i < foundRenderers.Length; i++)
                {
                    Renderer renderer = foundRenderers[i];
                    if (renderer == null)
                        continue;

                    _renderers.Add(renderer);
                    _propertyBlocks.Add(new MaterialPropertyBlock());
                }
            }
        }

        private void SetSuppressedState(bool suppressMovement, bool suppressSkills)
        {
            BlocksMovementInput = suppressMovement;
            BlocksSkillInput = suppressSkills;
            BlocksRaceProgression = suppressMovement || suppressSkills;
            ApplyPhysicsSuppression(suppressMovement);
            SetCollidersEnabled(!suppressMovement);
        }

        private void ApplyPhysicsSuppression(bool suppressMovement)
        {
            ResolveReferences();

            if (_movement != null)
                _movement.SetExternalKinematicControlActive(suppressMovement);

            if (_rigidbody == null)
                return;

            if (suppressMovement)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            ResolveReferences();
            if (_colliders == null)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = enabled;
            }
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerFinishPresentationController] object='{name}' {message}");
        }

        private void TryPlayLocalLossOfControlTimeline()
        {
            ResolveReferences();
            if (_localLossOfControlCinematicPlayed)
                return;

            if (_networkObject == null || !_networkObject.IsOwner)
                return;

            LocalFinishPresentationTimelineBridge.Instance?.PlayLossOfControlTimeline();
            _localLossOfControlCinematicPlayed = true;
        }
    }
}
