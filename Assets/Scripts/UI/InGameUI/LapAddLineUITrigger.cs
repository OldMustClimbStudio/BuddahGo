using UnityEngine;
using MoreMountains.Feedbacks;

public class LapAddLineUITrigger : MonoBehaviour
{
    [Header("Lap Source")]
    [SerializeField] private LapProgress lapProgress;
    [SerializeField] private bool autoFindLocalLapProgress = true;
    [SerializeField, Min(0.1f)] private float rebindIntervalSeconds = 1f;

    [Header("Animator")]
    [SerializeField] private string triggerName = "LapAdd";
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Feel")]
    [SerializeField] private MMF_Player lapAddFeelPlayer;
    [SerializeField] private bool autoFindFeelInChildren = true;

    [Header("Read Only")]
    [SerializeField] private string debugLapSourceName;
    [SerializeField] private int debugCurrentLap;
    [SerializeField] private int debugLastLap;
    [SerializeField] private int debugAnimatorCount;
    [SerializeField] private string debugFeelName;

    private Animator[] _animators;
    private int _triggerHash;
    private bool _hasLapSnapshot;
    private float _nextRebindTime;

    private void Awake()
    {
        RefreshAnimators();
        TryResolveFeelPlayer();
        _triggerHash = Animator.StringToHash(string.IsNullOrWhiteSpace(triggerName) ? "LapAdd" : triggerName);
    }

    private void OnEnable()
    {
        RefreshAnimators();
        TryResolveFeelPlayer();
        TryResolveLapProgress(force: true);
        TakeLapSnapshot();
    }

    private void Update()
    {
        if ((lapProgress == null || !lapProgress.isActiveAndEnabled) && autoFindLocalLapProgress && Time.time >= _nextRebindTime)
        {
            TryResolveLapProgress(force: false);
        }

        if (lapProgress == null)
            return;

        int currentLap = lapProgress.CurrentLap;
        debugCurrentLap = currentLap;

        if (!_hasLapSnapshot)
        {
            debugLastLap = currentLap;
            _hasLapSnapshot = true;
            return;
        }

        if (currentLap > debugLastLap)
            TriggerLapAddAnimation();

        debugLastLap = currentLap;
    }

    private void TriggerLapAddAnimation()
    {
        if (_animators == null || _animators.Length == 0)
            RefreshAnimators();

        TryResolveFeelPlayer();

        for (int i = 0; i < _animators.Length; i++)
        {
            Animator animator = _animators[i];
            if (animator == null)
                continue;

            animator.SetTrigger(_triggerHash);
        }

        if (lapAddFeelPlayer != null)
        {
            lapAddFeelPlayer.Initialization();
            lapAddFeelPlayer.PlayFeedbacks();
        }
    }

    private void RefreshAnimators()
    {
        _animators = GetComponentsInChildren<Animator>(includeInactiveChildren);
        debugAnimatorCount = _animators != null ? _animators.Length : 0;
    }

    private void TakeLapSnapshot()
    {
        if (lapProgress == null)
        {
            _hasLapSnapshot = false;
            debugLastLap = 0;
            debugCurrentLap = 0;
            return;
        }

        int lap = lapProgress.CurrentLap;
        debugLastLap = lap;
        debugCurrentLap = lap;
        _hasLapSnapshot = true;
    }

    private void TryResolveLapProgress(bool force)
    {
        if (!force && lapProgress != null && lapProgress.isActiveAndEnabled)
            return;

        _nextRebindTime = Time.time + Mathf.Max(0.1f, rebindIntervalSeconds);

        LapProgress found = FindOwnedLapProgressOn<BuddahMovement>();
        if (found == null)
            found = FindOwnedLapProgressOn<PlayerCamera>();
        if (found == null)
            found = FindOwnedLapProgressDirect();

        if (found == lapProgress)
            return;

        lapProgress = found;
        debugLapSourceName = lapProgress != null ? lapProgress.gameObject.name : "(none)";
        TakeLapSnapshot();
    }

    private static LapProgress FindOwnedLapProgressOn<T>() where T : MonoBehaviour
    {
        T[] all = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T item = all[i];
            if (item == null)
                continue;

            if (item is BuddahMovement movement && !movement.IsOwner)
                continue;

            if (item is PlayerCamera camera && !camera.IsOwner)
                continue;

            LapProgress lp = item.GetComponent<LapProgress>();
            if (lp != null && lp.IsOwner)
                return lp;
        }

        return null;
    }

    private static LapProgress FindOwnedLapProgressDirect()
    {
        LapProgress[] all = FindObjectsByType<LapProgress>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            LapProgress lp = all[i];
            if (lp != null && lp.IsOwner)
                return lp;
        }

        return null;
    }

    private void TryResolveFeelPlayer()
    {
        if (lapAddFeelPlayer == null && autoFindFeelInChildren)
            lapAddFeelPlayer = GetComponentInChildren<MMF_Player>(includeInactiveChildren);

        debugFeelName = lapAddFeelPlayer != null ? lapAddFeelPlayer.gameObject.name : "(none)";
    }
}