using System;
using System.Collections.Generic;
using BuddahGo.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>Preplace once on an always-active root in each supported scene.</summary>
    [DisallowMultipleComponent]
    public sealed class SoloUIBootstrap : MonoBehaviour
    {
        [Tooltip("Assign a bundled TMP font with Chinese glyphs for the Solo UI.")]
        [SerializeField] private TMP_FontAsset font;
        private readonly List<Button> _onlineButtons = new List<Button>();
        private TMP_Text _onlineStatus;

        private void Start()
        {
            var menu = FindObjectOfType<MainMenuUI>();
            if (font == null && menu != null)
            {
                var inherited = menu.GetComponentInChildren<TMP_Text>(true);
                if (inherited != null) font = inherited.font;
            }
            if (font == null) font = TMP_Settings.defaultFontAsset;
            // Existing selection/leaderboard text also renders the localized offline player name.
            // Keep one shared fallback for the application lifetime, without replacing their styling.
            if (font != null && !TMP_Settings.fallbackFontAssets.Contains(font))
                TMP_Settings.fallbackFontAssets.Add(font);

            string scene = gameObject.scene.name;
            if (scene == SceneNames.MainMenu && menu != null) BuildMenu(menu);
            if (scene == SceneNames.PropertySelection || scene == SceneNames.RaceMap)
            {
                var quit = gameObject.AddComponent<QuitConfirmDialog>();
                quit.Initialize(font);
            }
            if (scene == SceneNames.RaceMap)
                gameObject.AddComponent<SoloResultsView>().Initialize(font);
        }

        private void BuildMenu(MainMenuUI menu)
        {
            var home = menu.HomePanel;
            if (home == null) return;
            var existing = home.GetComponentsInChildren<Button>(true);
            Array.Sort(existing, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            foreach (var button in existing)
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    string method = button.onClick.GetPersistentMethodName(i);
                    if (button.onClick.GetPersistentTarget(i) == menu &&
                        (method == nameof(MainMenuUI.ShowCreateRoom) || method == nameof(MainMenuUI.ShowBrowseRooms)
                         || method == nameof(MainMenuUI.ShowJoinById)))
                    {
                        _onlineButtons.Add(button);
                        break;
                    }
                }
            }

            var setupCanvas = SoloUIFactory.Canvas(transform, "SoloSetupCanvas", 200);
            setupCanvas.gameObject.SetActive(false);
            setupCanvas.gameObject.AddComponent<SoloSetupPanel>().Initialize(menu, font);
            var solo = SoloUIFactory.Button(home.transform, "SoloMatchButton", "单人游戏",
                new Vector2(0f, 180f), new Vector2(448f, 70f), font);
            solo.GetComponentInChildren<TMP_Text>().fontSize = 42f;
            solo.onClick.AddListener(menu.ShowSoloSetup);
            menu.RegisterSoloSetupPanel(setupCanvas.gameObject, solo.gameObject);
            for (int i = 0; i < existing.Length; i++)
            {
                var rect = existing[i].transform as RectTransform;
                if (rect != null) rect.anchoredPosition = new Vector2(0f, 90f - i * 90f);
            }
            _onlineStatus = SoloUIFactory.Label(home.transform, "OnlineAvailability", string.Empty,
                new Vector2(0f, -255f), new Vector2(900f, 58f), font, 20f);
            RefreshOnlineAvailability();
            if (menu.CurrentPanel == MainMenuUI.MenuPanel.Home) SoloUIFactory.Focus(solo);
        }

        private void Update() => RefreshOnlineAvailability();

        private void RefreshOnlineAvailability()
        {
            if (_onlineStatus == null) return;
            var session = SessionControl.Current;
            bool available = session != null && session.IsOnlineAvailable;
            foreach (var button in _onlineButtons)
                if (button != null) button.interactable = available;
            string message = session == null ? "正在初始化…" :
                available ? string.Empty : "Steam 不可用，请启动 Steam 后重开游戏";
            if (session != null && !string.IsNullOrEmpty(session.LastError)) message = session.LastError;
            if (_onlineStatus.text != message) _onlineStatus.text = message;
        }
    }
}
