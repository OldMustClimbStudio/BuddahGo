#if BUDDAH_PREDICTION_PERF_PROBE
using System;
using NewBuddah.PredictionV2.Core;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace NewBuddah.PredictionV2.Debugging
{
    // Phase 4 V13 perf-budget probe — observation only, gated behind
    // BUDDAH_PREDICTION_PERF_PROBE. Two measurement paths:
    //   1. rep-*-ms — per-frame Stopwatch ticks accumulated inside
    //      BuddahPredictedMotor.RunInputs (forward + replay). Probe reads +
    //      zeros the static accumulator each Update. Phase 8 Entry 7 M2:
    //      replaces the earlier ProfilerMarker/ProfilerRecorder path, which
    //      required an active Profiler recording session to sample custom
    //      markers (standalone Player.log runs read 0). Stopwatch is
    //      Profiler-independent, stdlib-only, release-safe.
    //   2. gc-alloc-*-b — ProfilerRecorder on the built-in "GC.Alloc" marker
    //      under ProfilerCategory.Memory. Always captured by Unity's runtime;
    //      unchanged by Entry 7.
    //
    // Samples every Update; writes per-frame values into a pre-allocated ring
    // buffer; emits [D-PERF HEARTBEAT] every _heartbeatFrames frames with
    // avg / p99 / max aggregates. All sort / aggregation buffers are
    // pre-allocated at Awake -> no per-frame alloc.
    //
    // The motor's PerfProbeScope + s_runInputsTicksThisFrame live inside
    // BuddahPredictedMotor.cs behind the SAME #if BUDDAH_PREDICTION_PERF_PROBE
    // guard. When the define is undefined, both the motor wrap and this probe
    // compile out -> real path is byte-identical. Reviewer G1 bind.
    //
    // IMPORTANT — singleton discipline (reviewer Clarification 1):
    // The motor's static accumulator sums EVERY motor instance's RunInputs
    // time per frame. Attaching this probe per-Buddah would produce duplicate
    // heartbeat lines with the same sum value and no added information. This
    // probe MUST be scene-singleton. The Awake guard below aborts any
    // secondary instance with an error log so misplacement surfaces loudly
    // in the first playmode session after placement.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionPerfProbe : MonoBehaviour
    {
        private const string GcAllocMarkerName = "GC.Alloc";

        // Entry 7 M2: cache ns-per-Stopwatch-tick once. Stopwatch.Frequency is
        // platform-dependent (10 MHz Windows, 1 GHz Linux/Mac typical) so the
        // divisor cannot be hard-coded. Computed once at type-init.
        private static readonly double s_nsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

        // Runtime auto-instantiate (scene-singleton). Unity invokes this static
        // hook on every scene load in editor + standalone playmode. No manual
        // placement in scene/prefab required; no committed scene state change.
        // Gated inside the existing #if BUDDAH_PREDICTION_PERF_PROBE top-level
        // guard — compiles out when the define is undefined.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstantiate()
        {
            if (FindObjectOfType<BuddahPredictionPerfProbe>() != null)
                return;

            var go = new GameObject("[Auto] BuddahPredictionPerfProbe");
            go.AddComponent<BuddahPredictionPerfProbe>();
            DontDestroyOnLoad(go);
        }

        [Header("Capture cadence")]
        [Tooltip("Frames per heartbeat emit. Also the ring-buffer window for p99 computation.")]
        [SerializeField, Min(16)] private int _heartbeatFrames = 60;

        [Header("Logging")]
        [Tooltip("Prefix for emitted lines. Keep as [D-PERF HEARTBEAT] so existing digest tooling can grep alongside [D-LOC] / [D-VIS].")]
        [SerializeField] private string _logPrefix = "[D-PERF HEARTBEAT]";

        private ProfilerRecorder _gcAllocRecorder;

        // Pre-allocated ring + sort buffers. Replicate ticks are nanoseconds
        // (long, converted from Stopwatch ticks); GC.Alloc are bytes (long).
        private long[] _repNsBuffer;
        private long[] _gcBytesBuffer;
        private long[] _sortBuffer;
        private int _bufferIndex;
        private int _samplesCollected;
        private int _framesSinceHeartbeat;

        private void Awake()
        {
            // Reviewer Clarification 1 singleton guard. ProfilerRecorder with
            // SumAllSamplesInFrame returns the per-frame sum of EVERY motor
            // instance's marker time — per-Buddah placement produces duplicate
            // heartbeat lines with identical values and zero added information.
            // FindObjectsOfType includes self, so ">1" detects duplicates.
            var existing = FindObjectsOfType<BuddahPredictionPerfProbe>();
            if (existing.Length > 1)
            {
                Debug.LogError(
                    $"[D-PERF] Multiple BuddahPredictionPerfProbe instances found (count={existing.Length}) — must be scene-singleton. " +
                    "Disabling this instance; remove extras from the scene before baseline capture.",
                    this);
                enabled = false;
                return;
            }

            int window = Mathf.Max(16, _heartbeatFrames);
            _repNsBuffer = new long[window];
            _gcBytesBuffer = new long[window];
            _sortBuffer = new long[window];
            _bufferIndex = 0;
            _samplesCollected = 0;
            _framesSinceHeartbeat = 0;
        }

        private void OnEnable()
        {
            // If Awake's singleton guard tripped, buffers are null; skip recorder
            // start so we don't run on the disabled-duplicate instance.
            if (_repNsBuffer == null || _gcBytesBuffer == null)
                return;

            // Entry 7 M2: zero the motor's per-frame accumulator so the first
            // heartbeat doesn't read any ticks that happened between domain
            // reload and probe enable.
            BuddahPredictedMotor.s_runInputsTicksThisFrame = 0L;

            _gcAllocRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                GcAllocMarkerName,
                _gcBytesBuffer.Length,
                ProfilerRecorderOptions.SumAllSamplesInFrame);
        }

        private void OnDisable()
        {
            if (_gcAllocRecorder.Valid)
                _gcAllocRecorder.Dispose();
        }

        private void Update()
        {
            // Defensive: singleton guard may have left buffers null.
            if (_repNsBuffer == null || _gcBytesBuffer == null)
                return;

            // Entry 7 M2: read + zero the motor's per-frame Stopwatch-ticks
            // accumulator. Sums every RunInputs body (forward + reconcile
            // replays) since the previous Update read. Same-thread (main) so
            // no atomics needed — see main-thread invariant on the motor's
            // s_runInputsTicksThisFrame field.
            long repTicks = BuddahPredictedMotor.s_runInputsTicksThisFrame;
            BuddahPredictedMotor.s_runInputsTicksThisFrame = 0L;
            _repNsBuffer[_bufferIndex] = (long)(repTicks * s_nsPerTick);

            if (_gcAllocRecorder.Valid)
                _gcBytesBuffer[_bufferIndex] = _gcAllocRecorder.LastValue;
            else
                _gcBytesBuffer[_bufferIndex] = 0L;

            int window = _repNsBuffer.Length;
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
            int window = _repNsBuffer.Length;

            // Replicate tick time aggregates (ns -> ms)
            Array.Copy(_repNsBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            long repMaxNs = _sortBuffer[window - 1];
            long repP99Ns = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            double repAvgNs = Avg(_repNsBuffer);

            // GC.Alloc aggregates (bytes)
            Array.Copy(_gcBytesBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            long gcMaxBytes = _sortBuffer[window - 1];
            long gcP99Bytes = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            double gcAvgBytes = Avg(_gcBytesBuffer);

            const double nsToMs = 1e-6;
            Debug.Log(
                $"{_logPrefix} frame={Time.frameCount} window={window}\n" +
                $"  rep-avg-ms={repAvgNs * nsToMs:F4} rep-p99-ms={repP99Ns * nsToMs:F4} rep-max-ms={repMaxNs * nsToMs:F4}\n" +
                $"  gc-alloc-avg-b={gcAvgBytes:F1} gc-alloc-p99-b={gcP99Bytes} gc-alloc-max-b={gcMaxBytes}",
                this);
        }

        private static double Avg(long[] buffer)
        {
            int n = buffer.Length;
            if (n == 0) return 0d;
            double sum = 0d;
            for (int i = 0; i < n; i++) sum += buffer[i];
            return sum / n;
        }
    }
}
#endif
