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

    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
        RefreshText();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribed)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (_subscribed)
            return;

        if (LeaderboardManager.Instance == null)
            return;

        LeaderboardManager.Instance.Rankings.OnChange += HandleRankingsChanged;
        _subscribed = true;
        RefreshText();
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.Rankings.OnChange -= HandleRankingsChanged;
        }

        _subscribed = false;
    }

    private void HandleRankingsChanged(SyncListOperation op, int index, RankEntry oldItem, RankEntry newItem, bool asServer)
    {
        RefreshText();
    }

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
