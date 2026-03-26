using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class LobbyBrowserUI : MonoBehaviour
    {
        /* 此脚本用于处理大厅浏览界面，用户可以点击刷新按钮来获取最新的房间列表，并显示在界面上。
            每个房间项由LobbyBrowserListItemUI表示，用户可以点击加入按钮来尝试加入该房间。
            脚本会监听SteamLobbyManager的相关事件，以便在房间列表刷新、加入失败或流程状态变化时更新界面状态。
            同时提供返回按钮功能，允许用户返回主菜单界面。
            */
            
        [Header("References")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Transform listContentRoot;
        [SerializeField] private LobbyBrowserListItemUI listItemPrefab;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI emptyStateText;
        [SerializeField] private MainMenuUI mainMenuUI;

        private SteamLobbyManager _steamLobbyManager;
        private readonly List<LobbyBrowserListItemUI> _spawnedItems = new List<LobbyBrowserListItemUI>();
        private bool _subscribed;

        private void Awake()
        {
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshLobbyList);

            if (backButton != null)
                backButton.onClick.AddListener(HandleBack);
        }

        private void Start()
        {
            ResolveManager();
            Subscribe();
            RenderEmptyState("No rooms loaded.");
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
            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(RefreshLobbyList);

            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBack);

            Unsubscribe();
        }

        public void RefreshLobbyList()
        {
            ResolveManager();
            if (_steamLobbyManager == null)
            {
                SetStatus("SteamLobbyManager not found.");
                return;
            }

            SetStatus("Refreshing rooms...");
            _steamLobbyManager.RefreshLobbyList();
        }

        public void JoinLobby(ulong lobbyId)
        {
            ResolveManager();
            if (_steamLobbyManager == null)
            {
                SetStatus("SteamLobbyManager not found.");
                return;
            }

            SetStatus($"Joining room {lobbyId}...");
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

            _steamLobbyManager.OnLobbyListDataRefreshed += HandleLobbyListRefreshed;
            _steamLobbyManager.OnLobbyJoinFailed += HandleJoinFailed;
            _steamLobbyManager.OnFlowStateChanged += HandleFlowStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyListDataRefreshed -= HandleLobbyListRefreshed;
            _steamLobbyManager.OnLobbyJoinFailed -= HandleJoinFailed;
            _steamLobbyManager.OnFlowStateChanged -= HandleFlowStateChanged;
            _subscribed = false;
        }

        private void HandleLobbyListRefreshed(List<LobbyListItemData> lobbies)
        {
            ClearItems();

            if (lobbies == null || lobbies.Count == 0)
            {
                RenderEmptyState("No joinable rooms found.");
                return;
            }

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(false);

            Transform contentRoot = ResolveListContentRoot();

            foreach (LobbyListItemData lobby in lobbies)
            {
                if (contentRoot == null || listItemPrefab == null)
                    break;

                LobbyBrowserListItemUI item = Instantiate(listItemPrefab, contentRoot);
                item.Bind(lobby, this);
                _spawnedItems.Add(item);
            }

            SetStatus($"Found {lobbies.Count} rooms.");
        }

        private void HandleJoinFailed(string reason)
        {
            SetStatus($"Join failed: {reason}");
        }

        private void HandleFlowStateChanged(LobbyFlowState state, string message)
        {
            if (state == LobbyFlowState.RefreshingLobbyList
                || state == LobbyFlowState.JoiningLobby
                || state == LobbyFlowState.ConnectingToHost
                || state == LobbyFlowState.Failed
                || state == LobbyFlowState.LobbyListReady)
            {
                SetStatus(message);
            }
        }

        private void HandleBack()
        {
            if (mainMenuUI != null)
                mainMenuUI.GoBack();
        }

        private void RenderEmptyState(string message)
        {
            ClearItems();

            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(true);
                emptyStateText.text = message;
            }

            SetStatus(message);
        }

        private void ClearItems()
        {
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    Destroy(_spawnedItems[i].gameObject);
            }

            _spawnedItems.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private Transform ResolveListContentRoot()
        {
            if (listContentRoot == null)
                return null;

            ScrollRect scrollRect = listContentRoot.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
                return scrollRect.content;

            return listContentRoot;
        }
    }
}
