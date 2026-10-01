using System.Text;
using FishNet.Object.Synchronizing;
using SteamMultiplayer.Network;
using TMPro;
using UnityEngine;

public class LeaderboardTMPUI : MonoBehaviour
{
    private readonly System.Collections.Generic.List<BuddahMovement> _playerQuery = new System.Collections.Generic.List<BuddahMovement>();

    [Header("References")]
    [SerializeField] private TextMeshProUGUI outputText;

    [Header("Display")]
    [SerializeField] private int maxRows = 8;
    [SerializeField] private float localRefreshIntervalSeconds = 0.1f;

    private bool _hasTextSnapshot;
    private string _lastRenderedText;
    private (int status, int countdown, bool progress, float spline, float completion, float dot,
        bool obsession, float current, float max, float backfire, float gap,
        LeaderboardManager leaderboard, string snapshot, int rows, System.Globalization.CultureInfo culture) _lastTextInputs;
    private readonly System.Collections.Generic.List<RankEntry> _displayedRankings = new System.Collections.Generic.List<RankEntry>();
    private bool _subscribedRankings;
    private ObsessionFigure _localObsession;
    private bool _subscribedObsession;
    private SplineProgressTracker _localTracker;
    private RaceCompletionTracker _localCompletionTracker;
    private float _nextLocalRefreshTime;

    private void OnEnable()
    {
        TrySubscribeRankings();
        TrySubscribeLocalObsession();
        RefreshText();
    }

    private void OnDisable()
    {
        UnsubscribeRankings();
        UnsubscribeLocalObsession();
    }

    private void Update()
    {
        if (!_subscribedRankings)
            TrySubscribeRankings();

        if (!_subscribedObsession)
            TrySubscribeLocalObsession();

        if (_localTracker == null || _localCompletionTracker == null)
            TryFindLocalProgressComponents();

        if (Time.time >= _nextLocalRefreshTime)
        {
            _nextLocalRefreshTime = Time.time + Mathf.Max(0.02f, localRefreshIntervalSeconds);
            RefreshText();
        }
    }

    private void TrySubscribeRankings()
    {
        if (_subscribedRankings) return;
        if (LeaderboardManager.Instance == null) return;

        LeaderboardManager.Instance.Rankings.OnChange += HandleRankingsChanged;
        _subscribedRankings = true;
        RefreshText();
    }

    private void UnsubscribeRankings()
    {
        if (!_subscribedRankings) return;

        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.Rankings.OnChange -= HandleRankingsChanged;

        _subscribedRankings = false;
    }

    private void HandleRankingsChanged(SyncListOperation op, int index, RankEntry oldItem, RankEntry newItem, bool asServer)
    {
        if (NetDebug.EnableVerboseLog)
        {
            int count = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Rankings.Count : -1;
            GameLog.Verbose($"[Leaderboard] SyncList updated count={count} op={op} asServer={asServer}");
        }
        RefreshText();
    }

    private void TrySubscribeLocalObsession()
    {
        if (_subscribedObsession) return;

        var all = FindObjectsByType<ObsessionFigure>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsOwner)
            {
                _localObsession = all[i];
                break;
            }
        }

        if (_localObsession == null) return;

        _localObsession.OnValueChanged += HandleObsessionChanged;
        _localObsession.OnCompletionGapChanged += HandleCompletionGapChanged;
        _subscribedObsession = true;
        RefreshText();
    }

    private void TryFindLocalProgressComponents()
    {
        PlayerRegistry.CopyActiveTo(_playerQuery);
        var movers = _playerQuery;
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null || !movers[i].IsOwner)
                continue;

            _localTracker = movers[i].GetComponent<SplineProgressTracker>();
            _localCompletionTracker = movers[i].GetComponent<RaceCompletionTracker>();
            if (_localTracker != null || _localCompletionTracker != null)
                return;
        }
    }

    private void UnsubscribeLocalObsession()
    {
        if (!_subscribedObsession) return;

        if (_localObsession != null)
        {
            _localObsession.OnValueChanged -= HandleObsessionChanged;
            _localObsession.OnCompletionGapChanged -= HandleCompletionGapChanged;
        }

        _localObsession = null;
        _subscribedObsession = false;
    }

    private void HandleObsessionChanged(float oldValue, float newValue)
    {
        RefreshText();
    }

    private void HandleCompletionGapChanged(float oldValue, float newValue)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (outputText == null)
            return;

        RoomStateManager room = RoomStateManager.Instance;
        int status = room == null || !room.IsRaceSceneLoadedLocally ? -1
            : room.IsWaitingForRacePlayers ? 1 : room.IsRaceCountdownActive ? 2
            : room.IsResultPhaseActive ? 3 : room.IsRaceStarted ? 4 : 0;
        bool hasProgress = TryGetLocalProgress(out float splineProgress01, out float finalCompletionPercent, out float dot);
        var inputs = (status, status == 2 ? room.RaceCountdownSecondsRemaining : 0,
            hasProgress, splineProgress01, finalCompletionPercent, dot, _localObsession != null,
            _localObsession != null ? _localObsession.Current : 0f,
            _localObsession != null ? _localObsession.Max : 0f,
            _localObsession != null ? _localObsession.CurrentBackfireProbabilityPercent : 0f,
            _localObsession != null ? _localObsession.CompletionGapToLeaderPercent : 0f,
            LeaderboardManager.Instance,
            LeaderboardManager.Instance != null ? LeaderboardManager.Instance.LeaderboardSnapshotText : null,
            maxRows, System.Globalization.CultureInfo.CurrentCulture);
        if (_hasTextSnapshot && _lastTextInputs.Equals(inputs) && !RankingsTextChanged()
            && outputText.text == _lastRenderedText)
            return;
        _lastTextInputs = inputs;
        _hasTextSnapshot = true;
        RememberRankingsText();
        StringBuilder sb = new StringBuilder();
        if (RoomStateManager.Instance != null && RoomStateManager.Instance.IsRaceSceneLoadedLocally)
        {
            if (RoomStateManager.Instance.IsWaitingForRacePlayers)
                sb.AppendLine("Race Status: Waiting for players...");
            else if (RoomStateManager.Instance.IsRaceCountdownActive)
                sb.AppendLine($"Race Status: Starting in {RoomStateManager.Instance.RaceCountdownSecondsRemaining}...");
            else if (RoomStateManager.Instance.IsResultPhaseActive)
                sb.AppendLine("Race Status: Result Area");
            else if (RoomStateManager.Instance.IsRaceStarted)
                sb.AppendLine("Race Status: Started");

            sb.AppendLine();
        }

        sb.AppendLine("Leaderboard");

        if (hasProgress)
            sb.AppendLine($"Your Progress: {finalCompletionPercent:0.0}%   LapSpline: {(splineProgress01 * 100f):0.0}%   WrongWayDot: {dot:0.00}");
        else
            sb.AppendLine("Your Progress: (local player not found)");

        if (_localObsession != null)
        {
            sb.AppendLine($"Your Obsession: {_localObsession.Current:0.0}/{_localObsession.Max:0.0}");
            sb.AppendLine($"Backfire Chance: {_localObsession.CurrentBackfireProbabilityPercent:0.0}%");
            sb.AppendLine($"Gap To Leader: {_localObsession.CompletionGapToLeaderPercent:0.0}%");
        }
        else
        {
            sb.AppendLine("Your Obsession: (local obsession not found)");
            sb.AppendLine("Backfire Chance: (local obsession not found)");
            sb.AppendLine("Gap To Leader: (local obsession not found)");
        }

        // This legacy diagnostic view is log-only; the normal race HUD owns presentation.

        if (LeaderboardManager.Instance == null)
        {
            sb.AppendLine("(no data)");
            if (NetDebug.EnableVerboseLog) GameLog.Verbose("[RaceHUD] " + sb.ToString());
            outputText.text = _lastRenderedText = string.Empty;
            return;
        }

        string snapshotText = LeaderboardManager.Instance.LeaderboardSnapshotText;
        if (!string.IsNullOrWhiteSpace(snapshotText))
        {
            string[] lines = snapshotText.Split('\n');
            int linesToShow = Mathf.Min(maxRows, lines.Length);
            for (int i = 0; i < linesToShow; i++)
                sb.AppendLine(lines[i].TrimEnd('\r'));
        }
        else
        {
            int count = Mathf.Min(maxRows, LeaderboardManager.Instance.Rankings.Count);
            for (int i = 0; i < count; i++)
            {
                RankEntry entry = LeaderboardManager.Instance.Rankings[i];
                string finishSuffix = entry.IsFinished ? $" - Finished #{entry.FinishOrder}" : string.Empty;
                sb.AppendLine($"{i + 1}. {entry.DisplayName} - Lap {entry.Lap} - {entry.FinalCompletionPercent:0.0}%{finishSuffix}");
            }

            if (count == 0)
                sb.AppendLine("(empty)");
        }

        if (NetDebug.EnableVerboseLog) GameLog.Verbose("[RaceHUD] " + sb.ToString());
        outputText.text = _lastRenderedText = string.Empty;
    }

    private bool RankingsTextChanged()
    {
        LeaderboardManager leaderboard = LeaderboardManager.Instance;
        int count = leaderboard != null && string.IsNullOrWhiteSpace(leaderboard.LeaderboardSnapshotText)
            ? Mathf.Max(0, Mathf.Min(maxRows, leaderboard.Rankings.Count)) : 0;
        if (_displayedRankings.Count != count)
            return true;
        for (int i = 0; i < count; i++)
        {
            RankEntry current = leaderboard.Rankings[i];
            RankEntry previous = _displayedRankings[i];
            // RankEntry.Equals uses approximate floats; text cache needs exact display inputs.
            if (current.DisplayName != previous.DisplayName || current.Lap != previous.Lap
                || !current.FinalCompletionPercent.Equals(previous.FinalCompletionPercent)
                || current.IsFinished != previous.IsFinished || current.FinishOrder != previous.FinishOrder)
                return true;
        }
        return false;
    }

    private void RememberRankingsText()
    {
        _displayedRankings.Clear();
        LeaderboardManager leaderboard = LeaderboardManager.Instance;
        if (leaderboard == null || !string.IsNullOrWhiteSpace(leaderboard.LeaderboardSnapshotText))
            return;
        int count = Mathf.Min(maxRows, leaderboard.Rankings.Count);
        for (int i = 0; i < count; i++)
            _displayedRankings.Add(leaderboard.Rankings[i]);
    }
    private bool TryGetLocalProgress(out float splineProgress01, out float finalCompletionPercent, out float dot)
    {
        splineProgress01 = 0f;
        finalCompletionPercent = 0f;
        dot = 0f;

        if (_localTracker == null || _localCompletionTracker == null)
            TryFindLocalProgressComponents();

        if (_localTracker != null)
        {
            splineProgress01 = _localTracker.progress01;
            dot = _localTracker.forwardDot;
        }

        if (_localCompletionTracker != null)
        {
            finalCompletionPercent = _localCompletionTracker.FinalCompletionPercent;
            return true;
        }

        return _localTracker != null;
    }
}
