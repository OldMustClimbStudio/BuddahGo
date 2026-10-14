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
        private bool _optionsInitialized;
        private string[] _maxPlayersValues;
        private string[] _visibilityValues;

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

            // The table row's value, not the label: labels are display text and may be localized.
            string selected = SelectedValue(maxPlayersDropdown, _maxPlayersValues) ?? maxPlayersDropdown.options[maxPlayersDropdown.value].text;
            return int.TryParse(selected, out int parsed) ? Mathf.Clamp(parsed, 2, 6) : 4;
        }

        private LobbyVisibility GetSelectedVisibility()
        {
            if (visibilityDropdown == null)
                return LobbyVisibility.Public;

            if (System.Enum.TryParse(SelectedValue(visibilityDropdown, _visibilityValues), true, out LobbyVisibility configured))
                return configured;

            return visibilityDropdown.value switch
            {
                1 => LobbyVisibility.FriendsOnly,
                2 => LobbyVisibility.Private,
                _ => LobbyVisibility.Public
            };
        }

        private void EnsureDefaultOptions()
        {
            // Once per panel: re-enabling the panel keeps the player's current choice.
            if (_optionsInitialized)
                return;
            _optionsInitialized = true;

            // UI/MenuOption replaces the options serialized in the scene; without table rows the scene options stay.
            _maxPlayersValues = FillFromMenuOptionTable(maxPlayersDropdown, "max_players");
            if (maxPlayersDropdown != null && _maxPlayersValues == null && maxPlayersDropdown.options.Count == 0)
            {
                maxPlayersDropdown.AddOptions(new System.Collections.Generic.List<string> { "4", "5", "6" });
                maxPlayersDropdown.value = 2;
                maxPlayersDropdown.RefreshShownValue();
            }

            _visibilityValues = FillFromMenuOptionTable(visibilityDropdown, "visibility");
            if (visibilityDropdown != null && _visibilityValues == null && visibilityDropdown.options.Count == 0)
            {
                visibilityDropdown.AddOptions(new System.Collections.Generic.List<string> { "Public", "Friends Only", "Private" });
                visibilityDropdown.value = 0;
                visibilityDropdown.RefreshShownValue();
            }
        }

        private static string SelectedValue(TMP_Dropdown dropdown, string[] values)
        {
            return values != null && dropdown.value >= 0 && dropdown.value < values.Length ? values[dropdown.value] : null;
        }

        // UI/MenuOption owns the dropdown option lists (ordered by index) and defaults. Returns the row values in
        // option order, or null when the table has no rows for the group (the dropdown is left untouched).
        private static string[] FillFromMenuOptionTable(TMP_Dropdown dropdown, string optionGroup)
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (dropdown == null || db == null || db.menuOptions == null)
                return null;
            System.Collections.Generic.List<MenuOptionRecord> rows = db.menuOptions.FindAll(o => o != null && o.optionGroup == optionGroup);
            if (rows.Count == 0)
                return null;
            rows.Sort((a, b) => a.index.CompareTo(b.index));
            System.Collections.Generic.List<string> labels = new System.Collections.Generic.List<string>(rows.Count);
            string[] values = new string[rows.Count];
            int defaultIndex = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].isDefault)
                    defaultIndex = i;
                values[i] = rows[i].value;
                labels.Add(DesignTableOverlay.Text(rows[i].labelKey, rows[i].value));
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            dropdown.SetValueWithoutNotify(defaultIndex);
            dropdown.RefreshShownValue();
            return values;
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
