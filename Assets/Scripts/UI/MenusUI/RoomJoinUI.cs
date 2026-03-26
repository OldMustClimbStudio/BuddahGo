using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class RoomJoinUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_InputField lobbyIdInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private MainMenuUI mainMenuUI;

        private SteamLobbyManager _steamLobbyManager;
        private bool _subscribed;

        private void Awake()
        {
            if (joinButton != null)
                joinButton.onClick.AddListener(JoinByLobbyId);

            if (backButton != null)
                backButton.onClick.AddListener(HandleBack);
        }

        private void Start()
        {
            ResolveManager();
            Subscribe();
            SetError(string.Empty);
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (joinButton != null)
                joinButton.onClick.RemoveListener(JoinByLobbyId);

            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBack);

            Unsubscribe();
        }

        public void JoinByLobbyId()
        {
            ResolveManager();
            if (_steamLobbyManager == null)
            {
                SetError("SteamLobbyManager not found.");
                return;
            }

            string input = lobbyIdInput != null ? lobbyIdInput.text.Trim() : string.Empty;
            if (!ulong.TryParse(input, out ulong lobbyId))
            {
                SetError("Please enter a valid Lobby ID.");
                return;
            }

            SetError("Joining room...");
            _steamLobbyManager.JoinLobbyById(lobbyId);
        }

        private void ResolveManager()
        {
            if (_steamLobbyManager == null)
                _steamLobbyManager = SteamLobbyManager.Instance;
        }

        private void Subscribe()
        {
            if (_subscribed || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyJoinFailed += HandleJoinFailed;
            _steamLobbyManager.OnFlowStateChanged += HandleFlowStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyJoinFailed -= HandleJoinFailed;
            _steamLobbyManager.OnFlowStateChanged -= HandleFlowStateChanged;
            _subscribed = false;
        }

        private void HandleJoinFailed(string reason)
        {
            SetError($"Join failed: {reason}");
        }

        private void HandleFlowStateChanged(LobbyFlowState state, string message)
        {
            if (state == LobbyFlowState.JoiningLobby
                || state == LobbyFlowState.ConnectingToHost
                || state == LobbyFlowState.Failed)
            {
                SetError(message);
            }
        }

        private void HandleBack()
        {
            if (mainMenuUI != null)
                mainMenuUI.GoBack();
        }

        private void SetError(string message)
        {
            if (errorText != null)
                errorText.text = message;
        }
    }
}
