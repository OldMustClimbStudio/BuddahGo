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
            sb.AppendLine($"{i + 1}. {entry.DisplayName} - {entry.Checkpoints}");
        }

        if (count == 0)
        {
            sb.AppendLine("(empty)");
        }

        outputText.text = sb.ToString();
    }
}
