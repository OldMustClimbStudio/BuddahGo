using System.Collections.Generic;
using UnityEngine;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        public enum MenuPanel
        {
            Home,
            CreateRoom,
            BrowseRooms,
            JoinById
        }

        [Header("Panels")]
        [SerializeField] private GameObject menuPanelsRoot;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private GameObject browseRoomsPanel;
        [SerializeField] private GameObject joinByIdPanel;
        [SerializeField] private GameObject roomPanelRoot;

        [Header("Behavior")]
        [SerializeField] private bool allowEscapeBack = true;
        [SerializeField] private bool escapeFromHomeQuits = false;

        private readonly Stack<MenuPanel> _history = new Stack<MenuPanel>();
        private MenuPanel _currentPanel = MenuPanel.Home;
        private SteamLobbyManager _steamLobbyManager;
        private bool _subscribed;

        public MenuPanel CurrentPanel => _currentPanel;

        private void Awake()
        {
            if (menuPanelsRoot == null && homePanel != null && homePanel.transform.parent != null)
                menuPanelsRoot = homePanel.transform.parent.gameObject;
        }

        private void Start()
        {
            ResolveManager();
            Subscribe();
            ShowHome();
            RefreshRootVisibility();
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            RefreshRootVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!allowEscapeBack || !Input.GetKeyDown(KeyCode.Escape))
                return;

            if (_steamLobbyManager != null && _steamLobbyManager.IsInLobby)
                return;

            if (_currentPanel == MenuPanel.Home)
            {
                if (escapeFromHomeQuits)
                    Application.Quit();

                return;
            }

            GoBack();
        }

        public void ShowHome()
        {
            _history.Clear();
            ShowPanel(MenuPanel.Home, false);
        }

        public void ShowCreateRoom()
        {
            ShowPanel(MenuPanel.CreateRoom, true);
        }

        public void ShowBrowseRooms()
        {
            ShowPanel(MenuPanel.BrowseRooms, true);
        }

        public void ShowJoinById()
        {
            ShowPanel(MenuPanel.JoinById, true);
        }

        public void GoBack()
        {
            if (_history.Count == 0)
            {
                ShowHome();
                return;
            }

            MenuPanel previous = _history.Pop();
            ShowPanel(previous, false);
        }

        private void ShowPanel(MenuPanel panel, bool pushCurrent)
        {
            if (pushCurrent && _currentPanel != panel)
                _history.Push(_currentPanel);

            _currentPanel = panel;

            SetPanelActive(homePanel, panel == MenuPanel.Home);
            SetPanelActive(createRoomPanel, panel == MenuPanel.CreateRoom);
            SetPanelActive(browseRoomsPanel, panel == MenuPanel.BrowseRooms);
            SetPanelActive(joinByIdPanel, panel == MenuPanel.JoinById);
            RefreshRootVisibility();
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

            _steamLobbyManager.OnLobbyCreated += HandleEnteredLobby;
            _steamLobbyManager.OnLobbyJoined += HandleEnteredLobby;
            _steamLobbyManager.OnLobbyLeft += HandleLobbyLeft;
            _steamLobbyManager.OnHostLeft += HandleHostLeft;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyCreated -= HandleEnteredLobby;
            _steamLobbyManager.OnLobbyJoined -= HandleEnteredLobby;
            _steamLobbyManager.OnLobbyLeft -= HandleLobbyLeft;
            _steamLobbyManager.OnHostLeft -= HandleHostLeft;
            _subscribed = false;
        }

        private void HandleEnteredLobby(Steamworks.Data.Lobby lobby)
        {
            RefreshRootVisibility();
        }

        private void HandleLobbyLeft()
        {
            ShowHome();
        }

        private void HandleHostLeft()
        {
            ShowHome();
        }

        private void RefreshRootVisibility()
        {
            bool isInLobby = _steamLobbyManager != null && _steamLobbyManager.IsInLobby;

            if (menuPanelsRoot != null)
                menuPanelsRoot.SetActive(!isInLobby);

            if (roomPanelRoot != null)
                roomPanelRoot.SetActive(isInLobby);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
    }
}
