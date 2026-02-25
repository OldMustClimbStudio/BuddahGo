using System.Text;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

public class LeaderboardTMPUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI outputText;

    [Header("Display")]
    [SerializeField] private int maxRows = 8;

    private bool _subscribedRankings;
    private ObsessionFigure _localObsession;
    private bool _subscribedObsession;

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
    }

    // ---------------- Rankings ----------------
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
        RefreshText();
    }

    // ---------------- Obsession (local) ----------------
    private void TrySubscribeLocalObsession()
    {
        if (_subscribedObsession) return;

        // 找本地玩家的 ObsessionFigure（owner）
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

    // ---------------- UI ----------------
    private void RefreshText()
    {
        if (outputText == null)
            return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Leaderboard");

        // 本地完成度
        if (TryGetLocalProgress(out float p01, out float dot))
            sb.AppendLine($"Your Progress: {(p01 * 100f):0.0}%   WrongWayDot: {dot:0.00}");
        else
            sb.AppendLine("Your Progress: (local player not found)");

        // ✅ 本地执着值
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

        // 显示排行榜前 N 名
        int count = Mathf.Min(maxRows, LeaderboardManager.Instance.Rankings.Count);
        for (int i = 0; i < count; i++)
        {
            RankEntry entry = LeaderboardManager.Instance.Rankings[i];
            float trackLen = (TrackSplineRef.Instance != null) ? TrackSplineRef.Instance.TrackLength : 0f;
            float pct = (trackLen > 1e-6f) ? (entry.DistanceOnTrack / trackLen * 100f) : 0f;
            sb.AppendLine($"{i + 1}. {entry.DisplayName} - {pct:0.0}%");
        }

        if (count == 0)
            sb.AppendLine("(empty)");

        outputText.text = sb.ToString();
    }

    private bool TryGetLocalProgress(out float p01, out float dot)
    {
        p01 = 0f;
        dot = 0f;

        var movers = FindObjectsByType<BuddahMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
        {
            if (movers[i] != null && movers[i].IsOwner)
            {
                var tracker = movers[i].GetComponent<SplineProgressTracker>();
                if (tracker != null)
                {
                    p01 = tracker.progress01;
                    dot = tracker.forwardDot;
                    return true;
                }
            }
        }
        return false;
    }
}
