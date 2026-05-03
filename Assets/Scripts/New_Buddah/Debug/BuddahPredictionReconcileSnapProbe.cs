#if UNITY_EDITOR && BUDDAH_PREDICTION_RECONCILE_PROBE
using System;
using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    // Phase 7 Q4 sibling probe (mirrors BuddahPredictionVisualShakeProbe shape) -
    // observation only, gated behind BUDDAH_PREDICTION_RECONCILE_PROBE + UNITY_EDITOR.
    // Subscribes to BuddahPredictedMotor.OnReconcileSampled; receives
    // (preReconcilePosition, postReconcilePosition) per [Reconcile] callback;
    // records magnitude of (post - pre) into a pre-allocated ring buffer keyed
    // on EVENTS (not frames - rec-cb cadence is event-driven, not frame-rate-
    // driven). Emits [D-REC HEARTBEAT] every _eventsPerHeartbeat events with
    // rec-snap-max / rec-snap-p99 / rec-snap-avg + events-in-window + owner.
    //
    // Gating: when BUDDAH_PREDICTION_RECONCILE_PROBE undefined, the entire file
    // compiles out -> real path byte-identical. When defined but UNITY_EDITOR
    // is not (e.g., Standalone build), file also compiles out (motor's event
    // declaration is also UNITY_EDITOR-gated; cannot subscribe to a non-existent
    // event).
    //
    // L15 normalize NOT applicable: this probe records Vector3 magnitudes only,
    // no quaternion arithmetic. G1 zero-allocation hot-path: one
    // (post - pre).magnitude + one ring-buffer slot write per event; one
    // Array.Copy + one Array.Sort per heartbeat.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionReconcileSnapProbe : MonoBehaviour
    {
        [Header("Pinned references")]
        [SerializeField] private NetworkObject _networkObject;

        [Header("Capture cadence (event-keyed, NOT frame-keyed)")]
        [Tooltip("Reconcile events per heartbeat emit. Also the ring-buffer window size for p99 computation.")]
        [SerializeField, Min(16)] private int _eventsPerHeartbeat = 60;

        [Header("Logging")]
        [Tooltip("Prefix for emitted lines. Mirrors [D-VIS HEARTBEAT] / [D-LOC HEARTBEAT] convention.")]
        [SerializeField] private string _logPrefix = "[D-REC HEARTBEAT]";

        // Ring buffers. Sized at Awake; never re-allocated per event.
        private float[] _snapDistanceBuffer;
        private float[] _sortBuffer;
        private int _bufferIndex;
        private int _samplesCollected;

        private int _eventsSinceHeartbeat;

        private void Awake()
        {
            int window = Mathf.Max(16, _eventsPerHeartbeat);
            _snapDistanceBuffer = new float[window];
            _sortBuffer = new float[window];
            _bufferIndex = 0;
            _samplesCollected = 0;
            _eventsSinceHeartbeat = 0;

            // Runtime fallback resolution (mirrors VisualShakeProbe pattern).
            // Buddah's NetworkObject lives on the same GameObject as the motor
            // (RequireComponent on motor); GetComponentInParent always resolves.
            // Inspector pin still wins if present.
            if (_networkObject == null)
                _networkObject = GetComponentInParent<NetworkObject>();
        }

        private void OnEnable()
        {
            BuddahPredictedMotor.OnReconcileSampled += HandleReconcileSampled;
        }

        private void OnDisable()
        {
            BuddahPredictedMotor.OnReconcileSampled -= HandleReconcileSampled;
        }

        private void HandleReconcileSampled(Vector3 prePos, Vector3 postPos)
        {
            if (_snapDistanceBuffer == null)
                return;

            float snapDistance = (postPos - prePos).magnitude;

            int window = _snapDistanceBuffer.Length;
            _snapDistanceBuffer[_bufferIndex] = snapDistance;
            _bufferIndex = (_bufferIndex + 1) % window;
            if (_samplesCollected < window)
                _samplesCollected++;

            _eventsSinceHeartbeat++;
            if (_eventsSinceHeartbeat < _eventsPerHeartbeat || _samplesCollected < window)
                return;

            EmitHeartbeat();
            _eventsSinceHeartbeat = 0;
        }

        private void EmitHeartbeat()
        {
            int window = _snapDistanceBuffer.Length;
            bool isOwner = _networkObject != null && _networkObject.IsOwner;

            Array.Copy(_snapDistanceBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            float snapMax = _sortBuffer[window - 1];
            float snapP99 = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            float snapAvg = Avg(_snapDistanceBuffer);

            Debug.Log(
                $"{_logPrefix} frame={Time.frameCount} events-in-window={window} " +
                $"rec-snap-max={snapMax:F5} rec-snap-p99={snapP99:F5} rec-snap-avg={snapAvg:F5} owner={isOwner}",
                this);
        }

        private static float Avg(float[] buffer)
        {
            int n = buffer.Length;
            if (n == 0) return 0f;
            double sum = 0d;
            for (int i = 0; i < n; i++) sum += buffer[i];
            return (float)(sum / n);
        }
    }
}
#endif
