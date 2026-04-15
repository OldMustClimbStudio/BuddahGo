using NewBuddah.PredictionV2.Bootstrap;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionPresentationBridge : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private string currentExternalControlSource = "none";
        [SerializeField] private bool presentationControlActive;

        public string CurrentExternalControlSource => currentExternalControlSource;
        public bool IsPresentationControlActive => presentationControlActive;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
        }

        public void ReportPresentationExternalControl(bool active, string source)
        {
            if (active)
            {
                presentationControlActive = true;
                currentExternalControlSource = string.IsNullOrWhiteSpace(source) ? "presentation" : source;
            }
            else
            {
                presentationControlActive = false;
                currentExternalControlSource = bootstrap != null && bootstrap.DebugState.introControlActive
                    ? "intro"
                    : "none";
            }

            if (bootstrap != null)
            {
                bootstrap.DebugState.presentationControlActive = presentationControlActive;
                bootstrap.DebugState.externalControlSource = currentExternalControlSource;
                bootstrap.LogVerbose($"presentation external control active={presentationControlActive} source={currentExternalControlSource}");
            }
        }
    }
}
