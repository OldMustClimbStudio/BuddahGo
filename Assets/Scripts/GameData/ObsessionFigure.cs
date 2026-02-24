using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class ObsessionFigure : NetworkBehaviour
{
    [Header("Obsession Settings")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float minValue = 0f;

    [Header("Runtime")]
    [SerializeField] private float completionGapToLeaderPercent = 0f;
    [SerializeField] private float extraRecoveryPerSecond = 0f;

    [Tooltip("Default drain per second (server authoritative).")]
    [SerializeField] private float drainPerSecond = 1f;

    [Header("Catch-up Recovery")]
    [Tooltip("Quadratic slope 'a' in R(d)=a*d^2, where d is completion gap in percent.")]
    [Min(0f)] [SerializeField] private float catchupRecoveryA = 0.01f;

    [Tooltip("Initial value when spawned.")]
    [SerializeField] private float initialValue = 0f;

    [Header("Backfire (Anti) Probability")]
    [Tooltip("Pmax in the sigmoid function. Treated as a percentage (0-100).")]
    [Range(0f, 100f)]
    [SerializeField] private float backfirePMaxPercent = 75f;

    [Tooltip("k in the sigmoid function (steepness). Larger = steeper curve.")]
    [Min(0.0001f)]
    [SerializeField] private float backfireK = 0.12f;

    [Tooltip("Midpoint of the sigmoid on the obsession axis. Matches your formula's 50 by default.")]
    [SerializeField] private float backfireMidpointX = 50f;

    [Tooltip("If obsession is at or above this value, backfire chance stays at Pmax.")]
    [SerializeField] private float backfireKeepPMaxAtOrAboveX = 100f;

    /// Current backfire probability in percent
    public float CurrentBackfireProbabilityPercent => GetBackfireProbabilityPercent(_current.Value);

    private readonly SyncVar<float> _current = new SyncVar<float>();

    public event Action<float, float> OnValueChanged; // (old, new)
    public event Action<float, float> OnCompletionGapChanged; // (old, new)

    public float Current => _current.Value;
    public float Max => maxValue;
    public float DrainPerSecond => drainPerSecond;
    public float CompletionGapToLeaderPercent => completionGapToLeaderPercent;
    public float ExtraRecoveryPerSecond => extraRecoveryPerSecond;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        // 客户端收到变化事件
        _current.OnChange += OnValueChangedSync;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        _current.OnChange -= OnValueChangedSync;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _current.Value = Mathf.Clamp(initialValue, minValue, maxValue);
    }

    private void Update()
    {
        if (IsServerInitialized)
        {
            float gapPercent = GetCompletionGapToLeaderPercent();
            float extraRecovery = catchupRecoveryA * gapPercent * gapPercent;
            extraRecoveryPerSecond = Mathf.Max(0f, extraRecovery);

            float totalRecoveryPerSecond = Mathf.Max(0f, drainPerSecond + extraRecoveryPerSecond);

            // 持续恢复（数值向 minValue 下降）
            if (_current.Value > minValue && totalRecoveryPerSecond > 0f)
            {
                _current.Value = Mathf.Clamp(_current.Value - totalRecoveryPerSecond * Time.deltaTime, minValue, maxValue);
            }
        }

        if (IsClientInitialized && IsOwner)
        {
            UpdateCompletionGapToLeaderPercent();
        }
        Debug.Log($"[ObsessionFigure][Update] current={_current.Value:0.###}, gap={completionGapToLeaderPercent:0.###}%, extraRecovery={extraRecoveryPerSecond:0.###}, backfireP={CurrentBackfireProbabilityPercent:0.###}%");
    }

    private void UpdateCompletionGapToLeaderPercent()
    {
        SetCompletionGapToLeaderPercent(GetCompletionGapToLeaderPercent());
    }

    private float GetCompletionGapToLeaderPercent()
    {
        if (LeaderboardManager.Instance == null)
        {
            return 0f;
        }

        var rankings = LeaderboardManager.Instance.Rankings;
        if (rankings.Count == 0)
        {
            return 0f;
        }

        float trackLen = (TrackSplineRef.Instance != null) ? TrackSplineRef.Instance.TrackLength : 0f;
        if (trackLen <= 1e-6f)
        {
            return 0f;
        }

        float leaderPercent = Mathf.Clamp01(rankings[0].DistanceOnTrack / trackLen) * 100f;
        bool foundSelf = false;
        float selfPercent = 0f;

        for (int i = 0; i < rankings.Count; i++)
        {
            RankEntry entry = rankings[i];
            if (entry.ClientId == OwnerId)
            {
                selfPercent = Mathf.Clamp01(entry.DistanceOnTrack / trackLen) * 100f;
                foundSelf = true;
                break;
            }
        }

        if (!foundSelf)
        {
            return 0f;
        }

        return Mathf.Max(0f, leaderPercent - selfPercent);
    }

    private void SetCompletionGapToLeaderPercent(float value)
    {
        value = Mathf.Max(0f, value);
        float oldValue = completionGapToLeaderPercent;
        if (Mathf.Approximately(oldValue, value))
            return;

        completionGapToLeaderPercent = value;
        OnCompletionGapChanged?.Invoke(oldValue, completionGapToLeaderPercent);
    }

    /// <summary>
    /// Server only: add obsession (skill use increases it).
    /// </summary>
    public void AddServer(float amount)
    {
        if (!IsServerInitialized) return;

        amount = Mathf.Max(0f, amount);
        if (amount <= 0f) return;

        _current.Value = Mathf.Clamp(_current.Value + amount, minValue, maxValue);
    }

    private void OnValueChangedSync(float oldValue, float newValue, bool asServer)
    {
        OnValueChanged?.Invoke(oldValue, newValue);
    }

    /// P(x) = Pmax / (1 + e^{-k(x - midpoint)})
    public float GetBackfireProbabilityPercent(float obsessionValue)
    {
        float obsessionForCalc = obsessionValue * 0.1f;
        float x = Mathf.Clamp(obsessionForCalc, minValue, maxValue);
        float pMax = Mathf.Clamp(backfirePMaxPercent, 0f, 100f);
        float k = Mathf.Max(0.0001f, backfireK);
        float keepPMaxAtOrAboveX = Mathf.Clamp(backfireKeepPMaxAtOrAboveX, minValue, maxValue);

        if (x >= keepPMaxAtOrAboveX)
            return pMax;

        float exp = Mathf.Exp(-k * (x - backfireMidpointX));
        float p = pMax / (1f + exp);
        return Mathf.Clamp(p, 0f, 100f);
    }
}
