using System;
using BuddahGo.Match;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
            JoinById,
            SoloSetup
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
        private InputSystem_Actions _inputActions;
        private InputAction _menuCancelAction;

        public MenuPanel CurrentPanel => _currentPanel;
        public GameObject HomePanel => homePanel;
        private bool _started;
        private GameObject _soloSetupPanel;
        private GameObject _homeDefaultSelection;

        public void RegisterSoloSetupPanel(GameObject panel, GameObject homeDefaultSelection)
        {
            _soloSetupPanel = panel;
            _homeDefaultSelection = homeDefaultSelection;
            SetPanelActive(panel, _currentPanel == MenuPanel.SoloSetup);
            TryRestoreFailedSoloSetup();
        }

        public void ShowSoloSetup() => ShowPanel(MenuPanel.SoloSetup, true);

        private void TryRestoreFailedSoloSetup()
        {
            // Start and bootstrap registration may run in either order. Consume once both are ready.
            if (!_started || _soloSetupPanel == null) return;
            var settings = SessionControl.Current?.TakeFailedSoloSettings();
            if (settings == null) return;
            _soloSetupPanel.GetComponent<SoloSetupPanel>().RestoreSettings(settings);
            ShowSoloSetup();
        }

        private void Awake()
        {
            if (menuPanelsRoot == null && homePanel != null && homePanel.transform.parent != null)
                menuPanelsRoot = homePanel.transform.parent.gameObject;
        }

        private void Start()
        {
            EnsureInputActions();
            ResolveManager();
            Subscribe();
            ShowHome();
            _started = true;
            TryRestoreFailedSoloSetup();
            RefreshRootVisibility();
        }

        private void OnEnable()
        {
            EnsureInputActions();
            EnableMenuInput();
            ResolveManager();
            Subscribe();
            RefreshRootVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DisableMenuInput();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DisableMenuInput();
        }

        public void ShowHome()
        {
            _history.Clear();
            ShowPanel(MenuPanel.Home, false);
            if (_homeDefaultSelection != null && _homeDefaultSelection.activeInHierarchy
                && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_homeDefaultSelection);
        }

        public void ShowCreateRoom()
        {
            if (SessionControl.Current == null || !SessionControl.Current.IsOnlineAvailable) return;
            ShowPanel(MenuPanel.CreateRoom, true);
        }

        public void ShowBrowseRooms()
        {
            if (SessionControl.Current == null || !SessionControl.Current.IsOnlineAvailable) return;
            ShowPanel(MenuPanel.BrowseRooms, true);
        }

        public void ShowJoinById()
        {
            if (SessionControl.Current == null || !SessionControl.Current.IsOnlineAvailable) return;
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
            if (previous == MenuPanel.Home) ShowHome();
            else ShowPanel(previous, false);
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
            SetPanelActive(_soloSetupPanel, panel == MenuPanel.SoloSetup);
            RefreshRootVisibility();
        }

        private void ResolveManager()
        {
            if (_steamLobbyManager == null)
                _steamLobbyManager = SteamLobbyManager.Instance;
        }

        private void EnsureInputActions()
        {
            if (_inputActions != null)
                return;

            _inputActions = new InputSystem_Actions();
            _menuCancelAction = _inputActions.Menu.Get().FindAction("MenuCancel");
        }

        private void EnableMenuInput()
        {
            if (_menuCancelAction == null)
                return;

            _menuCancelAction.performed -= HandleMenuCancelPerformed;
            _menuCancelAction.performed += HandleMenuCancelPerformed;
            _inputActions.Menu.Enable();
        }

        private void DisableMenuInput()
        {
            if (_menuCancelAction != null)
                _menuCancelAction.performed -= HandleMenuCancelPerformed;

            if (_inputActions != null)
                _inputActions.Menu.Disable();
        }

        private void HandleMenuCancelPerformed(InputAction.CallbackContext context)
        {
            if (!allowEscapeBack || !context.performed || LocalInputBlock.IsBlocked
                || (_currentPanel == MenuPanel.SoloSetup && SessionControl.Current != null
                    && (SessionControl.Current.IsStarting || MatchRules.Current.AutoStartRoom)))
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
