#if BUDDAH_PREDICTION_PERF_PROBE
using System;
using Unity.Profiling;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    // Phase 4 V13 perf-budget probe — observation only, gated behind
    // BUDDAH_PREDICTION_PERF_PROBE. Attaches two ProfilerRecorders:
    //   1. ProfilerCategory.Scripts, marker "BuddahPredictedMotor.RunInputs"
    //      -> nanoseconds spent inside Replicate-tick body per physics tick.
    //   2. ProfilerCategory.Memory, marker "GC.Alloc" -> bytes allocated
    //      per frame (aggregate across the whole frame, not motor-specific
    //      alone; still useful for detecting step struct-copy regressions).
    //
    // Samples every Update; reads per-frame values from recorder into a
    // pre-allocated ring buffer; emits [D-PERF HEARTBEAT] every
    // _heartbeatFrames frames with avg / p99 / max aggregates. All sort /
    // aggregation buffers are pre-allocated at Awake -> no per-frame alloc.
    //
    // The motor's ProfilerMarker is created inside BuddahPredictedMotor.cs
    // behind the SAME #if BUDDAH_PREDICTION_PERF_PROBE guard. When the define
    // is undefined, the marker + this probe both compile out -> real path is
    // byte-identical. Reviewer G1 bind.
    //
    // Pre-refactor baseline capture: define enabled, run 60-120s @ N Buddahs
    // on 3d-tip, save digest to agent-exchange/console/2026-04-19-phase4-
    // baseline-v13.log. Post-4a rerun under identical scene + input; compare
    // rep-avg-ms, rep-p99-ms, gc-alloc-avg-b across runs. PASS thresholds in
    // audit Addendum B §B.3.4.
    //
    // IMPORTANT — singleton discipline (reviewer Clarification 1):
    // ProfilerRecorder with SumAllSamplesInFrame returns the sum of ALL motor
    // instances' marker time in the frame. Attaching this probe per-Buddah
    // would produce duplicate heartbeat lines with the same sum value and no
    // added information. This probe MUST be scene-singleton. The Awake guard
    // below aborts any secondary instance with an error log so misplacement
    // surfaces loudly in the first playmode session after placement.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionPerfProbe : MonoBehaviour
    {
        public const string MotorReplicateMarkerName = "BuddahPredictedMotor.RunInputs";
        private const string GcAllocMarkerName = "GC.Alloc";

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

        private ProfilerRecorder _motorReplicateRecorder;
        private ProfilerRecorder _gcAllocRecorder;

        // Pre-allocated ring + sort buffers. Replicate ticks are nanoseconds
        // (long); GC.Alloc are bytes (long). Both long[] for ProfilerRecorder
        // sample values.
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

            _motorReplicateRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts,
                MotorReplicateMarkerName,
                _repNsBuffer.Length,
                ProfilerRecorderOptions.SumAllSamplesInFrame);

            _gcAllocRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                GcAllocMarkerName,
                _gcBytesBuffer.Length,
                ProfilerRecorderOptions.SumAllSamplesInFrame);
        }

        private void OnDisable()
        {
            if (_motorReplicateRecorder.Valid)
                _motorReplicateRecorder.Dispose();
            if (_gcAllocRecorder.Valid)
                _gcAllocRecorder.Dispose();
        }

        private void Update()
        {
            // Defensive: singleton guard may have left buffers null.
            if (_repNsBuffer == null || _gcBytesBuffer == null)
                return;

            // ProfilerRecorder.LastValue returns the value for the most recently
            // completed profiler frame. With SumAllSamplesInFrame, this sums
            // every Begin/End pair of the marker within that frame — which for
            // the motor marker aggregates all Replicate calls (forward + any
            // replay re-runs in the same frame) into one number.
            if (_motorReplicateRecorder.Valid)
                _repNsBuffer[_bufferIndex] = _motorReplicateRecorder.LastValue;
            else
                _repNsBuffer[_bufferIndex] = 0L;

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
