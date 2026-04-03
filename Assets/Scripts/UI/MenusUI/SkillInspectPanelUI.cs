using SteamMultiplayer.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public class SkillInspectPanelUI : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private string defaultHintText = "Esc to close";

        [Header("Scroll")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.18f;
        [SerializeField] private AnimationCurve fadeCurve;

        private Coroutine _fadeRoutine;

        private void Awake()
        {
            EnsureCurve();
            HideImmediate();
        }

        public void Show(SkillInspectableItem item)
        {
            if (item == null)
                return;

            SelectablePropertyOption option = item.OptionData;

            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(option.DisplayName) ? option.OptionId : option.DisplayName;

            if (descriptionText != null)
                descriptionText.text = option.Description ?? string.Empty;

            if (hintText != null)
                hintText.text = defaultHintText;

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 1f;
            }

            SetVisible(true, immediate: false);
        }

        public void HideImmediate()
        {
            SetVisible(false, immediate: true);
        }

        public void HideAnimated()
        {
            SetVisible(false, immediate: false);
        }

        private void SetVisible(bool visible, bool immediate)
        {
            bool shouldReactivateHost = visible && !gameObject.activeInHierarchy;
            if (shouldReactivateHost)
                gameObject.SetActive(true);

            if (panelRoot != null && visible)
                panelRoot.SetActive(true);

            if (canvasGroup == null)
            {
                if (panelRoot != null)
                    panelRoot.SetActive(visible);
                return;
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (immediate || fadeDuration <= 0f)
            {
                ApplyCanvasGroup(visible ? 1f : 0f);
                if (!visible && panelRoot != null)
                    panelRoot.SetActive(false);
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                ApplyCanvasGroup(visible ? 1f : 0f);
                if (!visible && panelRoot != null)
                    panelRoot.SetActive(false);
                return;
            }

            _fadeRoutine = StartCoroutine(FadeCanvasGroup(visible));
        }

        private System.Collections.IEnumerator FadeCanvasGroup(bool visible)
        {
            float start = canvasGroup.alpha;
            float target = visible ? 1f : 0f;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fadeDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = fadeCurve.Evaluate(t);
                ApplyCanvasGroup(Mathf.Lerp(start, target, eased));
                yield return null;
            }

            ApplyCanvasGroup(target);
            if (!visible && panelRoot != null)
                panelRoot.SetActive(false);

            _fadeRoutine = null;
        }

        private void ApplyCanvasGroup(float alpha)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = alpha > 0.99f;
            canvasGroup.interactable = alpha > 0.99f;
        }

        private void EnsureCurve()
        {
            if (fadeCurve == null || fadeCurve.length == 0)
                fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
}
