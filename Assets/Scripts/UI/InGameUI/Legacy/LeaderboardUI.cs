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
        int count = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.Rankings.Count : -1;
        Debug.Log($"[Leaderboard] SyncList updated count={count} op={op} asServer={asServer}");
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

        outputText.text = sb.ToString();
    }
}
