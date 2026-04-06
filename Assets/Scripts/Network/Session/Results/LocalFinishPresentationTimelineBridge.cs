using UnityEngine;
using UnityEngine.Playables;

namespace SteamMultiplayer.Network.Results
{
    public class LocalFinishPresentationTimelineBridge : MonoBehaviour
    {
        public static LocalFinishPresentationTimelineBridge Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayableDirector playableDirector;

        [Header("Behavior")]
        [SerializeField] private bool rewindBeforePlay = true;
        [SerializeField] private bool stopOnResultAreaInteractive = false;
        [SerializeField] private bool stopOnRaceReset = true;
        [SerializeField] private bool enableDebugLogs;

        public bool IsPlayingLossOfControlTimeline { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayLossOfControlTimeline()
        {
            if (playableDirector == null)
            {
                DebugLog("PlayLossOfControlTimeline skipped because PlayableDirector is missing.");
                return;
            }

            if (rewindBeforePlay)
            {
                playableDirector.time = 0d;
                playableDirector.Evaluate();
            }

            playableDirector.Play();
            IsPlayingLossOfControlTimeline = true;
            DebugLog("PlayLossOfControlTimeline");
        }

        public void StopLossOfControlTimeline()
        {
            if (playableDirector != null)
                playableDirector.Stop();

            IsPlayingLossOfControlTimeline = false;
            DebugLog("StopLossOfControlTimeline");
        }

        public void HandleLossOfControlPresentationFinishedSignal()
        {
            IsPlayingLossOfControlTimeline = false;
            DebugLog("HandleLossOfControlPresentationFinishedSignal");
        }

        public void HandleEnteredResultAreaInteractive()
        {
            if (stopOnResultAreaInteractive)
                StopLossOfControlTimeline();
        }

        public void HandleRaceReset()
        {
            if (stopOnRaceReset)
                StopLossOfControlTimeline();
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[LocalFinishPresentationTimelineBridge] {message}");
        }
    }
}
