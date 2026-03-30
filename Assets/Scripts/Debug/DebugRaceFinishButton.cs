using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.Debugging
{
    public class DebugRaceFinishButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RaceFinishManager raceFinishManager;
        [SerializeField] private bool showOnlyForLocalHost = true;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (raceFinishManager == null)
                raceFinishManager = FindFirstObjectByType<RaceFinishManager>();

            if (button != null)
                button.onClick.AddListener(HandleClicked);
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void Update()
        {
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        private void HandleClicked()
        {
            if (raceFinishManager == null)
                raceFinishManager = FindFirstObjectByType<RaceFinishManager>();

            raceFinishManager?.TriggerDebugFinishRaceFromLocalUi();
        }

        private void RefreshVisibility()
        {
            if (button == null)
                return;

            if (!showOnlyForLocalHost)
            {
                button.gameObject.SetActive(true);
                return;
            }

            bool isVisible = raceFinishManager != null && raceFinishManager.CanUseLocalDebugFinishButton();
            if (button.gameObject.activeSelf != isVisible)
                button.gameObject.SetActive(isVisible);
        }
    }
}
