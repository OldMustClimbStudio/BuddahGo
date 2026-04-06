using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Header("Auto Bind")]
        [SerializeField] private Animator crossFadeAnimator;
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
            PlayState(fadeOutStateName, AlternateFadeOutState);
            _playFadeOutOnNextSceneLoad = false;
        }

        private float PlayFadeInInternal(float fallbackDuration)
        {
            TryBindAnimator();
            PlayState(fadeInStateName, AlternateFadeInState);
            return Mathf.Max(0f, fallbackDurationSeconds > 0f ? fallbackDurationSeconds : fallbackDuration);
        }

        private void PlayState(string primaryStateName, string alternateStateName)
        {
            if (crossFadeAnimator == null)
            {
                Debug.LogWarning("[SceneFadeController] No CrossFade Animator was found.");
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
            if (crossFadeAnimator != null)
                return;

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
                PreserveFadeHierarchyIfNeeded();
            }
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
    }
}
