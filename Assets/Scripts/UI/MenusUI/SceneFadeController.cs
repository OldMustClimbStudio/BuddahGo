using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public class SceneFadeController : MonoBehaviour
    {
        private const string DefaultFadeInState = "CrossFade_In";
        private const string DefaultFadeOutState = "CrossFade_out";
        private const string AlternateFadeInState = "CrossFade_in";
        private const string AlternateFadeOutState = "CrossFade_Out";

        private static SceneFadeController _instance;
        private static bool _playFadeOutOnNextSceneLoad;
        private static bool _holdBlackUntilReleased;

        [Header("Auto Bind")]
        [SerializeField] private Animator crossFadeAnimator;
        [SerializeField] private CanvasGroup crossFadeCanvasGroup;
        [SerializeField] private Graphic crossFadeGraphic;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Animation States")]
        [SerializeField] private string fadeInStateName = DefaultFadeInState;
        [SerializeField] private string fadeOutStateName = DefaultFadeOutState;
        [SerializeField, Min(0f)] private float fallbackDurationSeconds = 1f;

        public static float PlayFadeInBeforeSceneLoad(float fallbackDurationSeconds = 1f)
        {
            SceneFadeController controller = EnsureInstance();
            _playFadeOutOnNextSceneLoad = true;
            return controller.PlayFadeInInternal(fallbackDurationSeconds);
        }

        public static void QueueFadeOutOnNextSceneLoad()
        {
            _playFadeOutOnNextSceneLoad = true;
            EnsureInstance();
        }

        public static void HoldBlackOnNextSceneLoad()
        {
            _holdBlackUntilReleased = true;
            EnsureInstance();
        }

        public static void ReleaseHeldBlackScreen()
        {
            SceneFadeController controller = EnsureInstance();
            _holdBlackUntilReleased = false;
            controller.ReleaseHeldBlackScreenInternal();
        }

        public static void RegisterPersistentFadeCarrier(Animator animator, CanvasGroup canvasGroup = null)
        {
            if (animator == null && canvasGroup == null)
                return;

            SceneFadeController controller = EnsureInstance();
            controller.RegisterPersistentFadeCarrierInternal(animator, canvasGroup);
        }

        private static SceneFadeController EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<SceneFadeController>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;

            GameObject runtimeLoader = new GameObject("SceneFadeController");
            _instance = runtimeLoader.AddComponent<SceneFadeController>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            TryBindAnimator();
            PreserveFadeHierarchyIfNeeded();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            if (_playFadeOutOnNextSceneLoad)
                StartCoroutine(PlayFadeOutAfterSceneLoadCoroutine());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindAnimator();

            if (_playFadeOutOnNextSceneLoad)
                StartCoroutine(PlayFadeOutAfterSceneLoadCoroutine());
        }

        private IEnumerator PlayFadeOutAfterSceneLoadCoroutine()
        {
            yield return null;
            yield return null;

            TryBindAnimator();
            if (_holdBlackUntilReleased)
            {
                _playFadeOutOnNextSceneLoad = false;
                yield break;
            }

            PlayState(fadeOutStateName, AlternateFadeOutState);
            _playFadeOutOnNextSceneLoad = false;
        }

        private float PlayFadeInInternal(float fallbackDuration)
        {
            TryBindAnimator();
            if (crossFadeCanvasGroup != null)
            {
                crossFadeCanvasGroup.alpha = 1f;
                crossFadeCanvasGroup.interactable = false;
                crossFadeCanvasGroup.blocksRaycasts = false;
            }

            if (crossFadeGraphic != null)
                crossFadeGraphic.gameObject.SetActive(true);

            PlayState(fadeInStateName, AlternateFadeInState);
            return Mathf.Max(0f, fallbackDurationSeconds > 0f ? fallbackDurationSeconds : fallbackDuration);
        }

        private void PlayState(string primaryStateName, string alternateStateName)
        {
            if (crossFadeAnimator == null || !HasAnyFadeState(crossFadeAnimator))
            {
                if (crossFadeCanvasGroup != null)
                {
                    bool fadeIn = string.Equals(primaryStateName, fadeInStateName, System.StringComparison.Ordinal)
                        || string.Equals(alternateStateName, AlternateFadeInState, System.StringComparison.Ordinal);
                    crossFadeCanvasGroup.alpha = fadeIn ? 1f : 0f;
                    crossFadeCanvasGroup.interactable = false;
                    crossFadeCanvasGroup.blocksRaycasts = false;
                    if (crossFadeGraphic != null && !fadeIn)
                        crossFadeGraphic.gameObject.SetActive(false);
                    return;
                }

                Debug.LogWarning("[SceneFadeController] No CrossFade Animator or CanvasGroup was found.");
                return;
            }

            crossFadeAnimator.gameObject.SetActive(true);
            crossFadeAnimator.Rebind();
            crossFadeAnimator.Update(0f);

            if (HasState(primaryStateName))
            {
                crossFadeAnimator.Play(primaryStateName, 0, 0f);
                return;
            }

            if (HasState(alternateStateName))
            {
                crossFadeAnimator.Play(alternateStateName, 0, 0f);
                return;
            }

            Debug.LogWarning($"[SceneFadeController] Animator '{crossFadeAnimator.name}' is missing fade states '{primaryStateName}'/'{alternateStateName}'.");
        }

        private bool HasState(string stateName)
        {
            return !string.IsNullOrWhiteSpace(stateName)
                && crossFadeAnimator != null
                && crossFadeAnimator.HasState(0, Animator.StringToHash(stateName));
        }

        private void TryBindAnimator()
        {
            if (HasAnyFadeState(crossFadeAnimator))
            {
                CacheFadeComponentsFromAnimator();
                return;
            }

            Animator[] animators = Resources.FindObjectsOfTypeAll<Animator>();
            Scene activeScene = SceneManager.GetActiveScene();
            Animator bestMatch = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                GameObject target = animator.gameObject;
                if (!target.scene.IsValid())
                    continue;

                int score = ScoreAnimator(animator);
                if (score <= 0)
                    continue;
                if (target.scene == activeScene)
                    score += 25;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestMatch = animator;
            }

            if (bestMatch != null)
            {
                crossFadeAnimator = bestMatch;
                CacheFadeComponentsFromAnimator();
                PreserveFadeHierarchyIfNeeded();
            }
        }

        private void ReleaseHeldBlackScreenInternal()
        {
            TryBindAnimator();
            if (crossFadeCanvasGroup != null)
            {
                crossFadeCanvasGroup.alpha = 0f;
                crossFadeCanvasGroup.interactable = false;
                crossFadeCanvasGroup.blocksRaycasts = false;
                if (crossFadeGraphic != null)
                    crossFadeGraphic.gameObject.SetActive(false);
                _playFadeOutOnNextSceneLoad = false;
                return;
            }

            PlayState(fadeOutStateName, AlternateFadeOutState);
            _playFadeOutOnNextSceneLoad = false;
        }

        private void RegisterPersistentFadeCarrierInternal(Animator animator, CanvasGroup canvasGroup)
        {
            if (animator != null)
                crossFadeAnimator = animator;

            if (canvasGroup == null && animator != null)
                canvasGroup = animator.GetComponent<CanvasGroup>();

            if (canvasGroup != null)
                crossFadeCanvasGroup = canvasGroup;

            if (crossFadeCanvasGroup != null)
            {
                crossFadeGraphic = crossFadeCanvasGroup.GetComponent<Graphic>();
                crossFadeCanvasGroup.alpha = 1f;
                crossFadeCanvasGroup.interactable = false;
                crossFadeCanvasGroup.blocksRaycasts = false;
            }

            if (crossFadeGraphic != null)
                crossFadeGraphic.gameObject.SetActive(true);

            PreserveFadeHierarchyIfNeeded();
        }

        private void PreserveFadeHierarchyIfNeeded()
        {
            if (!dontDestroyOnLoad)
                return;

            GameObject persistentRoot = crossFadeAnimator != null
                ? crossFadeAnimator.transform.root.gameObject
                : transform.root.gameObject;

            DontDestroyOnLoad(persistentRoot);
        }

        private static int ScoreAnimator(Animator animator)
        {
            int score = 0;
            string objectName = animator.gameObject.name;
            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : string.Empty;

            if (objectName.IndexOf("crossfade", System.StringComparison.OrdinalIgnoreCase) >= 0)
                score += 100;
            if (objectName.IndexOf("loader", System.StringComparison.OrdinalIgnoreCase) >= 0)
                score += 50;
            if (controllerName.IndexOf("crossfade", System.StringComparison.OrdinalIgnoreCase) >= 0)
                score += 100;
            if (animator.HasState(0, Animator.StringToHash(DefaultFadeInState)) || animator.HasState(0, Animator.StringToHash(AlternateFadeInState)))
                score += 200;
            if (animator.HasState(0, Animator.StringToHash(DefaultFadeOutState)) || animator.HasState(0, Animator.StringToHash(AlternateFadeOutState)))
                score += 200;

            return score;
        }

        private void CacheFadeComponentsFromAnimator()
        {
            if (crossFadeAnimator == null)
                return;

            if (crossFadeCanvasGroup == null)
                crossFadeCanvasGroup = crossFadeAnimator.GetComponent<CanvasGroup>();

            if (crossFadeGraphic == null)
                crossFadeGraphic = crossFadeAnimator.GetComponent<Graphic>();
        }

        private static bool HasAnyFadeState(Animator animator)
        {
            if (animator == null)
                return false;

            return animator.HasState(0, Animator.StringToHash(DefaultFadeInState))
                || animator.HasState(0, Animator.StringToHash(AlternateFadeInState))
                || animator.HasState(0, Animator.StringToHash(DefaultFadeOutState))
                || animator.HasState(0, Animator.StringToHash(AlternateFadeOutState));
        }
    }
}
