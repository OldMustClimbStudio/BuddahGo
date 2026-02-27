using System.Text;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Text outputText;

    [Header("Display")]
    [SerializeField] private int maxRows = 8;

    private void OnEnable()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.Rankings.OnChange += HandleRankingsChanged;
        }

        RefreshText();
    }

    private void OnDisable()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.Rankings.OnChange -= HandleRankingsChanged;
        }
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
            float trackLen = (TrackSplineRef.Instance != null) ? TrackSplineRef.Instance.TrackLength : 0f;
            float lapBase = Mathf.Max(0, entry.Lap - 1);
            float lapProgress = (trackLen > 1e-6f) ? Mathf.Clamp01(entry.DistanceOnTrack / trackLen) : 0f;
            float totalPct = (lapBase + lapProgress) * 100f;
            sb.AppendLine($"{i + 1}. {entry.DisplayName} - Lap {entry.Lap} - {totalPct:0.0}%");
        }

        if (count == 0)
        {
            sb.AppendLine("(empty)");
        }

        outputText.text = sb.ToString();
    }
}
