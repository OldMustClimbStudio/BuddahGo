#if BUDDAH_PREDICTION_VISUAL_PROBE
using System;
using FishNet.Object;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    // Phase 4 V3 gameplay-shake probe — observation only, gated behind
    // BUDDAH_PREDICTION_VISUAL_PROBE. Samples the pinned visual root's world-space
    // pose every LateUpdate (post-animation, post-camera), maintains a pre-allocated
    // ring buffer of per-frame deltas, and emits a [D-VIS HEARTBEAT] line every
    // _heartbeatFrames frames with dmax / dp99 / davg aggregates for both position
    // (meters) and rotation (degrees).
    //
    // Gating: when BUDDAH_PREDICTION_VISUAL_PROBE is undefined, the entire file
    // compiles out -> real path is byte-identical. Reviewer G1 bind.
    //
    // Ownership tag (owner / spectator) resolves via a serialized NetworkObject
    // reference. Visual root Transform must be pinned in inspector (reviewer V3
    // bind: no GetComponentInParent or runtime scanning).
    //
    // L15 defensive normalize: Quaternion.Angle(default, default) returns 180°
    // (dot=0 -> 2*acos(0) = 180). When _lastRot or visual-root rotation is the
    // zero quaternion (0,0,0,0) rather than identity (0,0,0,1), the normalized
    // helper below folds it to identity before the angle compare. Rule per
    // Docs/lessons-log.md L15.
    [DisallowMultipleComponent]
    public sealed class BuddahPredictionVisualShakeProbe : MonoBehaviour
    {
        [Header("Pinned references (reviewer V3 bind: explicit inspector assignment)")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private NetworkObject _networkObject;

        [Header("Capture cadence")]
        [Tooltip("Frames per heartbeat emit. Also the ring-buffer window size for p99 computation.")]
        [SerializeField, Min(16)] private int _heartbeatFrames = 60;

        [Header("Logging")]
        [Tooltip("Prefix for emitted lines. Keep as [D-VIS] so existing digest parsers pick it up alongside [D-LOC].")]
        [SerializeField] private string _logPrefix = "[D-VIS HEARTBEAT]";

        // Ring buffers. Sized at Awake; never re-allocated. One write + one read
        // per LateUpdate, one Array.Copy + one Array.Sort per heartbeat. No per-
        // frame allocation -> G1 compliant.
        private float[] _posDeltaBuffer;
        private float[] _rotDeltaBuffer;
        private float[] _sortBuffer;
        private int _bufferIndex;
        private int _samplesCollected;

        // Previous-frame pose, cached between LateUpdates.
        private Vector3 _lastPos;
        private Quaternion _lastRot;
        private bool _hasLastSample;

        private int _framesSinceHeartbeat;

        private void Awake()
        {
            int window = Mathf.Max(16, _heartbeatFrames);
            _posDeltaBuffer = new float[window];
            _rotDeltaBuffer = new float[window];
            _sortBuffer = new float[window];
            _bufferIndex = 0;
            _samplesCollected = 0;
            _hasLastSample = false;
            _framesSinceHeartbeat = 0;
        }

        private void LateUpdate()
        {
            if (_visualRoot == null)
                return;

            Vector3 currentPos = _visualRoot.position;
            Quaternion currentRot = _visualRoot.rotation;

            if (!_hasLastSample)
            {
                _lastPos = currentPos;
                _lastRot = currentRot;
                _hasLastSample = true;
                return;
            }

            float posDelta = (currentPos - _lastPos).magnitude;
            float rotDelta = Quaternion.Angle(
                NormalizeZeroQuatToIdentity(_lastRot),
                NormalizeZeroQuatToIdentity(currentRot));

            int window = _posDeltaBuffer.Length;
            _posDeltaBuffer[_bufferIndex] = posDelta;
            _rotDeltaBuffer[_bufferIndex] = rotDelta;
            _bufferIndex = (_bufferIndex + 1) % window;
            if (_samplesCollected < window)
                _samplesCollected++;

            _lastPos = currentPos;
            _lastRot = currentRot;

            _framesSinceHeartbeat++;
            if (_framesSinceHeartbeat < _heartbeatFrames || _samplesCollected < window)
                return;

            EmitHeartbeat();
            _framesSinceHeartbeat = 0;
        }

        private void EmitHeartbeat()
        {
            int window = _posDeltaBuffer.Length;
            bool isOwner = _networkObject != null && _networkObject.IsOwner;

            // Position aggregates
            Array.Copy(_posDeltaBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            float posDmax = _sortBuffer[window - 1];
            float posDp99 = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            float posDavg = Avg(_posDeltaBuffer);

            // Rotation aggregates
            Array.Copy(_rotDeltaBuffer, _sortBuffer, window);
            Array.Sort(_sortBuffer);
            float rotDmax = _sortBuffer[window - 1];
            float rotDp99 = _sortBuffer[Mathf.Clamp(Mathf.FloorToInt(window * 0.99f), 0, window - 1)];
            float rotDavg = Avg(_rotDeltaBuffer);

            Debug.Log(
                $"{_logPrefix} frame={Time.frameCount} owner={isOwner} window={window}\n" +
                $"  pos-dmax={posDmax:F5} pos-dp99={posDp99:F5} pos-davg={posDavg:F5}\n" +
                $"  rot-dmax={rotDmax:F3} rot-dp99={rotDp99:F3} rot-davg={rotDavg:F3}",
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

        private static Quaternion NormalizeZeroQuatToIdentity(Quaternion q)
        {
            if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f)
                return Quaternion.identity;
            return q;
        }
    }
}
#endif
