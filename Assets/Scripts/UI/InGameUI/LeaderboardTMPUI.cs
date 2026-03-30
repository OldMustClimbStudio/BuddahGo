using System.Text;
using FishNet.Object.Synchronizing;
using SteamMultiplayer.Network;
using TMPro;
using UnityEngine;

public class LeaderboardTMPUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI outputText;

    [Header("Display")]
    [SerializeField] private int maxRows = 8;
    [SerializeField] private float localRefreshIntervalSeconds = 0.1f;

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
        int count = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Rankings.Count : -1;
        Debug.Log($"[Leaderboard] SyncList updated count={count} op={op} asServer={asServer}");
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
        var movers = FindObjectsByType<BuddahMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
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

        StringBuilder sb = new StringBuilder();
        if (RoomStateManager.Instance != null && RoomStateManager.Instance.IsRaceSceneLoadedLocally)
        {
            if (RoomStateManager.Instance.IsWaitingForRacePlayers)
                sb.AppendLine("Race Status: Waiting for players...");
            else if (RoomStateManager.Instance.IsRaceCountdownActive)
                sb.AppendLine($"Race Status: Starting in {RoomStateManager.Instance.RaceCountdownSecondsRemaining}...");
            else if (RoomStateManager.Instance.IsRaceStarted)
                sb.AppendLine("Race Status: Started");

            sb.AppendLine();
        }

        sb.AppendLine("Leaderboard");

        if (TryGetLocalProgress(out float splineProgress01, out float finalCompletionPercent, out float dot))
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

        sb.AppendLine();

        if (LeaderboardManager.Instance == null)
        {
            sb.AppendLine("(no data)");
            outputText.text = sb.ToString();
            return;
        }

        int count = Mathf.Min(maxRows, LeaderboardManager.Instance.Rankings.Count);
        for (int i = 0; i < count; i++)
        {
            RankEntry entry = LeaderboardManager.Instance.Rankings[i];
            string finishSuffix = entry.IsFinished ? $" - Finished #{entry.FinishOrder}" : string.Empty;
            sb.AppendLine($"{i + 1}. {entry.DisplayName} - Lap {entry.Lap} - {entry.FinalCompletionPercent:0.0}%{finishSuffix}");
        }

        if (count == 0)
            sb.AppendLine("(empty)");

        outputText.text = sb.ToString();
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
