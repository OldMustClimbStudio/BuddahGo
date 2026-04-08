using FishNet.Object.Synchronizing;
using SteamMultiplayer.Network.Results;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public class ResultDecisionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI playerChoicesText;
        [SerializeField] private Button startNewGameButton;
        [SerializeField] private Button returnToRoomButton;

        private ResultDecisionManager _decisionManager;
        private bool _subscribed;

        private void Awake()
        {
            if (startNewGameButton != null)
                startNewGameButton.onClick.AddListener(HandleStartNewGameClicked);

            if (returnToRoomButton != null)
                returnToRoomButton.onClick.AddListener(HandleReturnToRoomClicked);
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            RefreshView();
        }

        private void Update()
        {
            ResolveManager();
            Subscribe();
            RefreshView();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (startNewGameButton != null)
                startNewGameButton.onClick.RemoveListener(HandleStartNewGameClicked);

            if (returnToRoomButton != null)
                returnToRoomButton.onClick.RemoveListener(HandleReturnToRoomClicked);

            Unsubscribe();
        }

        private void ResolveManager()
        {
            if (_decisionManager == null)
                _decisionManager = ResultDecisionManager.Instance;
        }

        private void Subscribe()
        {
            if (_subscribed || _decisionManager == null)
                return;

            _decisionManager.PlayerDecisions.OnChange += HandlePlayerDecisionsChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _decisionManager == null)
                return;

            _decisionManager.PlayerDecisions.OnChange -= HandlePlayerDecisionsChanged;
            _subscribed = false;
        }

        private void HandlePlayerDecisionsChanged(SyncListOperation op, int index, ResultPlayerDecision oldItem, ResultPlayerDecision newItem, bool asServer)
        {
            RefreshView();
        }

        private void HandleStartNewGameClicked()
        {
            _decisionManager?.SubmitChoiceServerRpc(ResultPlayerChoice.StartNewGame);
        }

        private void HandleReturnToRoomClicked()
        {
            _decisionManager?.SubmitChoiceServerRpc(ResultPlayerChoice.ReturnToRoom);
        }

        private void RefreshView()
        {
            if (_decisionManager == null)
            {
                SetText(countdownText, "Countdown: --");
                SetText(statusText, "Waiting for ResultDecisionManager...");
                SetText(playerChoicesText, string.Empty);
                SetButtonsInteractable(false);
                return;
            }

            SetText(countdownText, $"Next Decision In: {_decisionManager.RemainingSeconds}s");

            string status = _decisionManager.FinalDecision switch
            {
                ResultFinalDecision.StartNewGame => "All players agreed. Starting a new game...",
                ResultFinalDecision.ReturnToRoom => "Returning everyone to the room...",
                _ => BuildPendingStatus()
            };
            SetText(statusText, status);

            SetText(playerChoicesText, _decisionManager.BuildDecisionSummary());
            SetButtonsInteractable(_decisionManager.IsDecisionActive && _decisionManager.FinalDecision == ResultFinalDecision.None);
        }

        private string BuildPendingStatus()
        {
            if (_decisionManager == null)
                return string.Empty;

            if (_decisionManager.TryGetLocalDecision(out ResultPlayerDecision localDecision))
            {
                if (!localDecision.HasResponded)
                    return "Choose whether to start a new game or return to the room.";

                return $"Your Choice: {localDecision.Choice}";
            }

            return "Waiting for your decision state...";
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (startNewGameButton != null)
                startNewGameButton.interactable = interactable;

            if (returnToRoomButton != null)
                returnToRoomButton.interactable = interactable;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
