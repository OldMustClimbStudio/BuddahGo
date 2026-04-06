using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public class PropertySelectionMatchTransitionTimeline : MonoBehaviour
    {
        [SerializeField] private PlayableDirector playableDirector;
        [SerializeField] private RectTransform transitionRoot;
        [SerializeField] private CanvasGroup targetCanvasGroup;
        [SerializeField, Min(0f)] private float fallbackDurationSeconds = 1f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private TimelineAsset _runtimeTimeline;
        private TrackAsset _runtimeTrack;

        public bool IsConfigured
        {
            get
            {
                if (playableDirector != null && playableDirector.playableAsset != null)
                    return true;

                return targetCanvasGroup != null;
            }
        }

        private void Awake()
        {
            if (playableDirector == null)
                playableDirector = GetComponent<PlayableDirector>();

            if (playableDirector == null)
                playableDirector = gameObject.AddComponent<PlayableDirector>();
        }

        public float GetDurationSeconds()
        {
            if (!IsConfigured)
                return 0f;

            if (playableDirector != null && playableDirector.playableAsset != null)
            {
                double duration = playableDirector.duration;
                if (duration <= 0d)
                    duration = fallbackDurationSeconds;

                return Mathf.Max(0f, (float)duration);
            }

            return Mathf.Max(0f, fallbackDurationSeconds);
        }

        public bool PlayTransition()
        {
            if (!IsConfigured)
                return false;

            if (transitionRoot != null)
                transitionRoot.localScale = Vector3.one;

            if (targetCanvasGroup != null)
                targetCanvasGroup.alpha = 0f;

            EnsurePlayableAsset();

            playableDirector.Stop();
            playableDirector.time = 0d;
            playableDirector.Evaluate();
            playableDirector.Play();
            return true;
        }

        private void EnsurePlayableAsset()
        {
            if (playableDirector == null)
                return;

            if (playableDirector.playableAsset != null)
                return;

            if (targetCanvasGroup == null)
                return;

            if (_runtimeTimeline == null)
            {
                _runtimeTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
                _runtimeTrack = _runtimeTimeline.CreateTrack<AnimationTrack>(null, "TransitionFadeTrack");

                TimelineClip timelineClip = _runtimeTrack.CreateDefaultClip();
                timelineClip.duration = Mathf.Max(0.01f, fallbackDurationSeconds);

                AnimationPlayableAsset animationPlayableAsset = timelineClip.asset as AnimationPlayableAsset;
                if (animationPlayableAsset != null)
                {
                    AnimationClip clip = new AnimationClip
                    {
                        name = "PropertySelectionToMatchFade",
                        legacy = false
                    };

                    clip.SetCurve(string.Empty, typeof(CanvasGroup), "m_Alpha", BuildAlphaCurve());
                    animationPlayableAsset.clip = clip;
                    animationPlayableAsset.removeStartOffset = false;
                }
            }

            playableDirector.playableAsset = _runtimeTimeline;
            playableDirector.SetGenericBinding(_runtimeTrack, targetCanvasGroup);
        }

        private AnimationCurve BuildAlphaCurve()
        {
            if (alphaCurve != null && alphaCurve.length > 0)
                return new AnimationCurve(alphaCurve.keys);

            return AnimationCurve.EaseInOut(0f, 0f, Mathf.Max(0.01f, fallbackDurationSeconds), 1f);
        }
    }
}
