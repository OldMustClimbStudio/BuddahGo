using FishNet.Object;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Visual;
using TMPro;
using UnityEngine;

namespace BuddahGo.Match
{
    // World-space text above a Practice training dummy: its current skill effects with remaining seconds, the last
    // impulse it received and the pending reset countdown. Reads the server motor directly, which is valid because
    // Practice is always a local host; presentation only, it writes no game state. Anchored like
    // LocalPlayerOverheadMarker (visual root + height, faces Camera.main, roughly constant on-screen size). The text
    // is a separate scene object so model scale/roll and the black-curtain renderer sweep never touch it.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class TrainingDummyReadout : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float heightAboveVisualRoot = 10f;
        [SerializeField, Min(0.1f)] private float fontSize = 10f;
        [SerializeField] private Color textColor = new Color(1f, 0.92f, 0.35f, 1f);
        [SerializeField, Min(1f)] private float targetPixelLineHeight = 22f;
        [SerializeField, Min(0.1f)] private float minimumScale = 1f;
        [SerializeField, Min(0.1f)] private float maximumScale = 6f;
        [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.1f;

        // TextMeshPro (world) renders one em as fontSize / 10 world units.
        private const float WorldUnitsPerFontPoint = 0.1f;

        private TrainingDummy _dummy;
        private NetworkObject _networkObject;
        private BuddahPredictionVisualRootBridge _visualBridge;
        private BuddahPredictedMotor _motor;
        private Camera _camera;
        private TextMeshPro _text;
        private float _nextRefreshTime;
        private string _shownText;

        private void Awake()
        {
            _dummy = GetComponent<TrainingDummy>();
            _networkObject = GetComponent<NetworkObject>();
            _visualBridge = GetComponent<BuddahPredictionVisualRootBridge>();
            _motor = GetComponent<BuddahPredictedMotor>();
        }

        private void LateUpdate()
        {
            if (_dummy == null || _networkObject == null || !_networkObject.IsClientInitialized || !_dummy.IsServerActive)
            {
                SetVisible(false);
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            if (_camera == null)
            {
                SetVisible(false);
                return;
            }

            Transform anchor = _visualBridge != null ? _visualBridge.GetVisualRoot() : null;
            if (anchor == null)
                anchor = transform;
            float bodyScale = _motor != null ? _motor.CurrentScaleMultiplier : 1f;
            Vector3 position = anchor.position + Vector3.up * (heightAboveVisualRoot * bodyScale);
            float depth = Vector3.Dot(position - _camera.transform.position, _camera.transform.forward);
            if (depth <= _camera.nearClipPlane)
            {
                SetVisible(false);
                return;
            }

            EnsureText();
            if (_text == null)
                return;

            float viewHeight = _camera.orthographic ? 2f * _camera.orthographicSize
                : 2f * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float lineHeight = fontSize * WorldUnitsPerFontPoint;
            float scale = Mathf.Clamp(viewHeight * targetPixelLineHeight / Mathf.Max(1, _camera.pixelHeight) / Mathf.Max(0.01f, lineHeight),
                minimumScale, Mathf.Max(minimumScale, maximumScale));
            Transform textTransform = _text.transform;
            textTransform.SetPositionAndRotation(position, _camera.transform.rotation);
            textTransform.localScale = Vector3.one * scale;

            if (Time.time >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.time + refreshIntervalSeconds;
                _dummy.TryCaptureEffects(out TrainingDummyEffects effects);
                string text = TrainingDummyReadoutText.Build(_dummy.DisplayName, effects, _dummy.VisibleImpulseMagnitude, _dummy.SecondsUntilReset);
                if (text != _shownText)
                {
                    _shownText = text;
                    _text.text = text;
                }
            }
            SetVisible(true);
        }

        private void EnsureText()
        {
            if (_text != null)
                return;
            var textObject = new GameObject(name + " Readout");
            // Lives and unloads with the race scene, never in whatever scene happens to be active.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(textObject, gameObject.scene);
            _text = textObject.AddComponent<TextMeshPro>();
            _text.fontSize = fontSize;
            _text.color = textColor;
            _text.fontStyle = FontStyles.Bold;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.enableWordWrapping = false;
            _text.rectTransform.pivot = new Vector2(0.5f, 0f);
            _text.rectTransform.sizeDelta = new Vector2(30f, 10f);
            _shownText = null;
        }

        private void SetVisible(bool visible)
        {
            if (_text != null && _text.gameObject.activeSelf != visible)
                _text.gameObject.SetActive(visible);
        }

        private void OnDisable() => SetVisible(false);

        private void OnDestroy()
        {
            if (_text != null)
                Destroy(_text.gameObject);
        }
    }
}
