using System;
using System.Collections;
using Cinemachine;
using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.Playables;

namespace SteamMultiplayer.Network.Results
{
    public class ResultPresentationTimelineBridge : MonoBehaviour
    {
        public static ResultPresentationTimelineBridge Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayableDirector playableDirector;
        [SerializeField] private CinemachineVirtualCamera sharedPresentationCamera;

        [Header("Timing")]
        [SerializeField] private bool useFallbackTimelineDelays = false;
        [SerializeField, Min(0f)] private float blackScreenStartDelaySeconds = 0f;
        [SerializeField, Min(0f)] private float blackScreenFullyCoveredDelaySeconds = 1f;
        [SerializeField, Min(0f)] private float blackScreenEndDelaySeconds = 3f;
        [SerializeField, Min(0f)] private float timelineFinishedDelaySeconds = 3f;

        [Header("Camera")]
        [SerializeField] private bool keepSharedCameraDuringResultArea = true;
        [SerializeField] private int activeCameraPriority = 500;
        [SerializeField] private int inactiveCameraPriority = 0;
        [SerializeField] private bool enableDebugLogs = true;

        private Coroutine _fallbackRoutine;
        private bool _blackScreenStartedInvoked;
        private bool _blackScreenReachedInvoked;
        private bool _blackScreenFinishedInvoked;
        private bool _presentationCompleteInvoked;
        private bool _presentationCameraLocked;

        public float BlackScreenStartDelaySeconds => blackScreenStartDelaySeconds;
        public float BlackScreenFullyCoveredDelaySeconds => Mathf.Max(blackScreenStartDelaySeconds, blackScreenFullyCoveredDelaySeconds);
        public float BlackScreenEndDelaySeconds => Mathf.Max(BlackScreenFullyCoveredDelaySeconds, blackScreenEndDelaySeconds);
        public float TimelineFinishedDelaySeconds => Mathf.Max(BlackScreenEndDelaySeconds, timelineFinishedDelaySeconds);
        public bool UseFallbackTimelineDelays => useFallbackTimelineDelays;
        public bool IsSharedPresentationCameraActive { get; private set; }
        public bool IsPresentationCameraLocked => _presentationCameraLocked;
        public bool HasBlackScreenStarted => _blackScreenStartedInvoked;
        public bool HasBlackScreenFullyCovered => _blackScreenReachedInvoked;
        public bool HasBlackScreenEnded => _blackScreenFinishedInvoked;
        public bool HasTimelineFinished => _presentationCompleteInvoked;

        public event Action BlackScreenStarted;
        public event Action BlackScreenFullyCovered;
        public event Action BlackScreenEnded;
        public event Action TimelineFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetSharedPresentationCameraActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayResultTransitionTimeline()
        {
            ResetSignalFlags();
            _presentationCameraLocked = false;
            SetSharedPresentationCameraActive(false);

            if (playableDirector != null)
            {
                playableDirector.time = 0d;
                playableDirector.Evaluate();
                playableDirector.Play();
            }

            if (_fallbackRoutine != null)
                StopCoroutine(_fallbackRoutine);

            if (useFallbackTimelineDelays)
                _fallbackRoutine = StartCoroutine(FallbackSignalRoutine());
            else
                _fallbackRoutine = null;
            DebugLog("PlayResultTransitionTimeline");
        }

        public void OnBlackScreenStart()
        {
            if (_blackScreenStartedInvoked)
                return;

            _blackScreenStartedInvoked = true;
            BlackScreenStarted?.Invoke();
            DebugLog("OnBlackScreenStart");
        }

        public void OnBlackScreenFullyCovered()
        {
            if (_blackScreenReachedInvoked)
                return;

            _blackScreenReachedInvoked = true;
            _presentationCameraLocked = true;
            SetSharedPresentationCameraActive(true);
            BlackScreenFullyCovered?.Invoke();
            if (MatchResultPresentationCoordinator.Instance != null
                && MatchResultPresentationCoordinator.Instance.IsServerInitialized)
            {
                MatchResultPresentationCoordinator.Instance.HandleTimelineBlackScreenFullyCoveredServer();
            }
            DebugLog("OnBlackScreenFullyCovered");
        }

        public void OnBlackScreenEnd()
        {
            if (_blackScreenFinishedInvoked)
                return;

            _blackScreenFinishedInvoked = true;
            BlackScreenEnded?.Invoke();
            DebugLog("OnBlackScreenEnd");
        }

        public void OnTimelineFinished()
        {
            if (_presentationCompleteInvoked)
                return;

            _presentationCompleteInvoked = true;
            if (playableDirector != null)
                playableDirector.Stop();
            TimelineFinished?.Invoke();
            if (keepSharedCameraDuringResultArea && _presentationCameraLocked)
                SetSharedPresentationCameraActive(true);
            if (MatchResultPresentationCoordinator.Instance != null
                && MatchResultPresentationCoordinator.Instance.IsServerInitialized)
            {
                MatchResultPresentationCoordinator.Instance.HandleTimelineFinishedServer();
            }
            DebugLog("OnTimelineFinished");
        }

        public void ReleaseSharedPresentationCamera()
        {
            _presentationCameraLocked = false;
            SetSharedPresentationCameraActive(false);
            DebugLog("ReleaseSharedPresentationCamera");
        }

        public void HandleTimelineBlackScreenReachedSignal()
        {
            OnBlackScreenFullyCovered();
        }

        public void HandleTimelineBlackScreenStartSignal()
        {
            OnBlackScreenStart();
        }

        public void HandleTimelineBlackScreenFinishedSignal()
        {
            OnBlackScreenEnd();
        }

        public void HandleResultPresentationCompleteSignal()
        {
            OnTimelineFinished();
        }

        private IEnumerator FallbackSignalRoutine()
        {
            if (BlackScreenStartDelaySeconds > 0f)
                yield return new WaitForSeconds(BlackScreenStartDelaySeconds);
            OnBlackScreenStart();

            float toFullyCovered = Mathf.Max(0f, BlackScreenFullyCoveredDelaySeconds - BlackScreenStartDelaySeconds);
            if (toFullyCovered > 0f)
                yield return new WaitForSeconds(toFullyCovered);
            OnBlackScreenFullyCovered();

            float toBlackEnd = Mathf.Max(0f, BlackScreenEndDelaySeconds - BlackScreenFullyCoveredDelaySeconds);
            if (toBlackEnd > 0f)
                yield return new WaitForSeconds(toBlackEnd);
            OnBlackScreenEnd();

            float toPresentationComplete = Mathf.Max(0f, TimelineFinishedDelaySeconds - BlackScreenEndDelaySeconds);
            if (toPresentationComplete > 0f)
                yield return new WaitForSeconds(toPresentationComplete);

            OnTimelineFinished();
            _fallbackRoutine = null;
        }

        private void ResetSignalFlags()
        {
            _blackScreenStartedInvoked = false;
            _blackScreenReachedInvoked = false;
            _blackScreenFinishedInvoked = false;
            _presentationCompleteInvoked = false;
        }

        private void SetSharedPresentationCameraActive(bool active)
        {
            IsSharedPresentationCameraActive = active;
            if (sharedPresentationCamera == null)
                return;

            sharedPresentationCamera.Priority = active ? activeCameraPriority : inactiveCameraPriority;
            sharedPresentationCamera.gameObject.SetActive(true);
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[ResultPresentationTimelineBridge] {message}");
        }
    }
}
