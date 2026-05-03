#if UNITY_EDITOR && BUDDAH_PREDICTION_FRAMETIME_PROBE
using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    // Phase 7 Stage 4 extension probe (per cowork-reviewer disposition + Yonezawa
    // disclosure 2026-05-03): the V5 CLIENT log was confounded by Editor stutter
    // on the client machine, so apparent jitter could not be cleanly separated
    // from frame-time variability. This probe measures Time.unscaledDeltaTime per
    // rendered frame (machine-global, NOT per-Buddah) so the analyzer can flag
    // STUTTER-CONFOUNDED runs and downgrade their data to upper-bound only.
    //
    // Scope: this probe is for Path C (host-only 90s active play, NEW path
    // amended into the contract by Yonezawa pre-Stage-5 disclosure). Manual
    // AddComponent on a Buddah prefab instance after PlayMode entry; no
    // BuddahPredictionBootstrap runtime-attach (per alpha discipline; runtime-
    // attach plumbing deferred to Phase 7.6).
    //
    // Gating: when BUDDAH_PREDICTION_FRAMETIME_PROBE undefined, the entire file
    // compiles out -> real path byte-identical. UNITY_EDITOR-gated for the same
    // reason as ReconcileSnapProbe -- production builds should never include
    // diagnostic probes.
    //
    // L15 normalize NOT applicable (no quaternion math). G1 zero-allocation
    // hot path: one Time.unscaledDeltaTime read + one ring-buffer slot write per
    // LateUpdate; one Array.Copy + one Array.Sort per heartbeat.
    //
    // Output format (parallel to [D-VIS HEARTBEAT] and [D-REC HEARTBEAT]):
    //   [D-FT HEARTBEAT] frame=<N> dt-min=<F3> dt-p99=<F3> dt-max=<F3> dt-avg=<F3>
    // Units: milliseconds (Time.unscaledDeltaTime is in seconds; multiplied by
    // 1000 at sample time so emitted values are directly comparable to a stutter
    // threshold expressed in ms).
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionFrameTimeProbe : MonoBehaviour
    {
        [Header("Capture cadence")]
        [Tooltip("Frames per heartbeat emit. Also the ring-buffer window size for p99 computation. Aligned with VisualShakeProbe default 60.")]
        [SerializeField, Min(16)] private int _heartbeatFrames = 60;

        [Header("Logging")]
        [Tooltip("Prefix for emitted lines. Mirrors [D-VIS HEARTBEAT] / [D-REC HEARTBEAT] / [D-LOC HEARTBEAT] convention.")]
        [SerializeField] private string _logPrefix = "[D-FT HEARTBEAT]";

        // Ring buffer of per-frame Time.unscaledDeltaTime in milliseconds.
        // Sized at Awake; never re-allocated.
        private float[] _deltaMsBuffer;
        private float[] _sortBuffer;
        private int _bufferIndex;
        private int _samplesCollected;
        private int _framesSinceHeartbeat;

        private void Awake()
        {
            int window = Mathf.Max(16, _heartbeatFrames);
            _deltaMsBuffer = new float[window];
            _sortBuffer = new float[window];
            _bufferIndex = 0;
            _samplesCollected = 0;
            _framesSinceHeartbeat = 0;
        }

        private void LateUpdate()
        {
            if (_deltaMsBuffer == null)
                return;

            float dtMs = Time.unscaledDeltaTime * 1000f;

            int window = _deltaMsBuffer.Length;
            _deltaMsBuffer[_bufferIndex] = dtMs;
            _bufferIndex = (_bufferIndex + 1) % window;
            if (_samplesCollected < window)
                _samplesCollected++;

            _framesSinceHeartbeat++;
            if (_framesSinceHeartbeat < _heartbeatFrames || _samplesCollected < window)
                return;

            EmitHeartbeat();
            _framesSinceHeartbeat = 0;
        }

        private void EmitHeartbeat()
        {
            int window = _deltaMsBuffer.Length;

            Array.Copy(_deltaMsBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            float dtMin = _sortBuffer[0];
            float dtP99 = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            float dtMax = _sortBuffer[window - 1];
            float dtAvg = Avg(_deltaMsBuffer);

            Debug.Log(
                $"{_logPrefix} frame={Time.frameCount} dt-min={dtMin:F3} dt-p99={dtP99:F3} dt-max={dtMax:F3} dt-avg={dtAvg:F3}",
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
