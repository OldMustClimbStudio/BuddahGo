using System;
using System.Text;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public sealed class SoloResultsView : MonoBehaviour
    {
        private GameObject _view;
        private TMP_Text _times;
        private TMP_Text _status;
        private Button _rematch;
        private Button _return;
        private bool _defaultFocused;

        public void Initialize(TMP_FontAsset font)
        {
            _view = SoloUIFactory.Canvas(transform, "SoloResultsCanvas", 250).gameObject;
            var card = SoloUIFactory.Card(_view.transform, "SoloResults", 570f);
            SoloUIFactory.Label(card, "Title", "Practice 完成", new Vector2(0f, 225f),
                new Vector2(600f, 55f), font, 34f);
            _times = SoloUIFactory.Label(card, "RaceTimes", string.Empty, new Vector2(0f, 40f),
                new Vector2(590f, 300f), font, 27f);
            _times.enableAutoSizing = true;
            _times.fontSizeMin = 18f;
            _times.fontSizeMax = 27f;
            _status = SoloUIFactory.Label(card, "DecisionStatus", string.Empty, new Vector2(0f, -155f),
                new Vector2(600f, 50f), font, 21f);
            _rematch = SoloUIFactory.Button(card, "Rematch", "再来一局", new Vector2(-145f, -225f),
                new Vector2(260f, 60f), font);
            _return = SoloUIFactory.Button(card, "ReturnHome", "返回", new Vector2(145f, -225f),
                new Vector2(260f, 60f), font);
            _rematch.onClick.AddListener(() => Submit(ResultPlayerChoice.StartNewGame));
            _return.onClick.AddListener(() => Submit(ResultPlayerChoice.ReturnToRoom));
            SoloUIFactory.Navigate(_rematch, _return);
            _view.SetActive(false);
        }

        private void Update()
        {
            if (_view == null) return;
            var manager = ResultDecisionManager.Instance;
            var presentation = MatchResultPresentationCoordinator.Instance;
            bool show = !MatchRules.Current.VoteOnResults &&
                ((presentation != null && presentation.CurrentStage == MatchResultPresentationStage.ResultInteractive)
                 || (presentation == null && manager != null && manager.IsDecisionActive));
            bool justOpened = show && !_view.activeSelf;
            _view.SetActive(show);
            if (!show) { _defaultFocused = false; return; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            bool active = manager != null && manager.IsDecisionActive && manager.FinalDecision == ResultFinalDecision.None;
            _rematch.interactable = active;
            // If the decision manager is missing, returning can still safely close the session.
            _return.interactable = active || manager == null;
            _status.text = manager == null ? "暂时无法再来一局，可以返回主菜单。" :
                manager.FinalDecision == ResultFinalDecision.StartNewGame ? "正在进入选择…" :
                manager.FinalDecision == ResultFinalDecision.ReturnToRoom ? "正在返回主菜单…" : string.Empty;
            RefreshTimes();
            if ((justOpened || !_defaultFocused) && (active || manager == null))
            {
                SoloUIFactory.Focus(active ? _rematch : _return);
                _defaultFocused = true;
            }
        }

        private void RefreshTimes()
        {
            var connection = GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection;
            int id = PlayerIdentity.GetLocalClientId(connection);
            if (id < 0 || id >= 10000 || MatchServices.Timing == null ||
                !MatchServices.Timing.TryGetResult(RacerId.FromClient(id), out var result))
            {
                _times.text = "计时数据暂不可用";
                return;
            }
            var text = new StringBuilder();
            text.AppendLine(result.Finished ? "总用时  " + FormatTime(result.TotalSeconds) : "完整圈速数据不可用");
            if (result.LapSeconds != null)
                for (int i = 0; i < result.LapSeconds.Length; i++)
                    text.AppendLine($"第 {i + 1} 圈  {FormatTime(result.LapSeconds[i])}");
            _times.text = text.ToString();
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) return "--:--.---";
            var duration = TimeSpan.FromSeconds(seconds);
            return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}";
        }

        private static void Submit(ResultPlayerChoice choice)
        {
            var manager = ResultDecisionManager.Instance;
            if (manager != null) manager.SubmitChoiceServerRpc(choice);
            else if (choice == ResultPlayerChoice.ReturnToRoom) SessionControl.Current?.RequestStopSession();
        }
    }
}
