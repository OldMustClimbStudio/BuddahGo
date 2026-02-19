using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class ObsessionFigure : NetworkBehaviour
{
    [Header("Obsession Settings")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float minValue = 0f;

    [Tooltip("Default drain per second (server authoritative).")]
    [SerializeField] private float drainPerSecond = 1f;

    [Tooltip("Initial value when spawned.")]
    [SerializeField] private float initialValue = 0f;

    private readonly SyncVar<float> _current = new SyncVar<float>();

    public event Action<float, float> OnValueChanged; // (old, new)

    public float Current => _current.Value;
    public float Max => maxValue;
    public float DrainPerSecond => drainPerSecond;

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
        if (!IsServerInitialized) return;

        // 持续减少
        if (_current.Value > minValue && drainPerSecond > 0f)
        {
            _current.Value = Mathf.Clamp(_current.Value - drainPerSecond * Time.deltaTime, minValue, maxValue);
        }
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
}
