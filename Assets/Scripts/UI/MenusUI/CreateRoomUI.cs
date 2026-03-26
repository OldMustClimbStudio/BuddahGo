using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class CreateRoomUI : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_Dropdown maxPlayersDropdown;
        [SerializeField] private TMP_Dropdown visibilityDropdown;

        [Header("Buttons")]
        [SerializeField] private Button createButton;
        [SerializeField] private Button backButton;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private MainMenuUI mainMenuUI;

        private SteamLobbyManager _steamLobbyManager;
        private bool _subscribed;

        private void Awake()
        {
            if (createButton != null)
                createButton.onClick.AddListener(CreateRoom);

            if (backButton != null)
                backButton.onClick.AddListener(HandleBack);
        }

        private void Start()
        {
            ResolveManager();
            Subscribe();
            EnsureDefaultOptions();
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            EnsureDefaultOptions();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (createButton != null)
                createButton.onClick.RemoveListener(CreateRoom);

            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBack);

            Unsubscribe();
        }

        public void CreateRoom()
        {
            ResolveManager();
            if (_steamLobbyManager == null)
            {
                SetStatus("SteamLobbyManager not found.");
                return;
            }

            string roomName = roomNameInput != null ? roomNameInput.text.Trim() : string.Empty;
            int maxPlayers = GetSelectedMaxPlayers();
            LobbyVisibility visibility = GetSelectedVisibility();

            SetStatus("Creating room...");
            _steamLobbyManager.CreateLobby(roomName, visibility, maxPlayers);
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

            _steamLobbyManager.OnLobbyCreated += HandleLobbyCreated;
            _steamLobbyManager.OnLobbyCreateFailed += HandleCreateFailed;
            _steamLobbyManager.OnFlowStateChanged += HandleFlowStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyCreated -= HandleLobbyCreated;
            _steamLobbyManager.OnLobbyCreateFailed -= HandleCreateFailed;
            _steamLobbyManager.OnFlowStateChanged -= HandleFlowStateChanged;
            _subscribed = false;
        }

        private void HandleLobbyCreated(Steamworks.Data.Lobby lobby)
        {
            SetStatus($"Room created: {lobby.Id.Value}");
        }

        private void HandleCreateFailed(string reason)
        {
            SetStatus($"Create failed: {reason}");
        }

        private void HandleFlowStateChanged(LobbyFlowState state, string message)
        {
            if (state == LobbyFlowState.CreatingLobby
                || state == LobbyFlowState.LobbyCreated
                || state == LobbyFlowState.StartingHost
                || state == LobbyFlowState.Failed)
            {
                SetStatus(message);
            }
        }

        private int GetSelectedMaxPlayers()
        {
            if (maxPlayersDropdown == null || maxPlayersDropdown.options.Count == 0)
                return 4;

            string selected = maxPlayersDropdown.options[maxPlayersDropdown.value].text;
            return int.TryParse(selected, out int parsed) ? Mathf.Clamp(parsed, 4, 6) : 4;
        }

        private LobbyVisibility GetSelectedVisibility()
        {
            if (visibilityDropdown == null)
                return LobbyVisibility.Public;

            return visibilityDropdown.value switch
            {
                1 => LobbyVisibility.FriendsOnly,
                2 => LobbyVisibility.Private,
                _ => LobbyVisibility.Public
            };
        }

        private void EnsureDefaultOptions()
        {
            if (maxPlayersDropdown != null && maxPlayersDropdown.options.Count == 0)
            {
                maxPlayersDropdown.AddOptions(new System.Collections.Generic.List<string> { "4", "5", "6" });
                maxPlayersDropdown.value = 2;
                maxPlayersDropdown.RefreshShownValue();
            }

            if (visibilityDropdown != null && visibilityDropdown.options.Count == 0)
            {
                visibilityDropdown.AddOptions(new System.Collections.Generic.List<string> { "Public", "Friends Only", "Private" });
                visibilityDropdown.value = 0;
                visibilityDropdown.RefreshShownValue();
            }
        }

        private void HandleBack()
        {
            if (mainMenuUI != null)
                mainMenuUI.GoBack();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
