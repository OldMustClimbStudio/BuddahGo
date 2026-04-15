using System.Collections.Generic;
using System.Text;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Validation
{
    [DisallowMultipleComponent]
    public class BuddahPredictionCompatibilityRegistry : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;

        public void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
        }

        public void RefreshRegistry()
        {
            ResolveReferences();
            if (bootstrap == null)
                return;

            BuddahPredictionDebugState state = bootstrap.DebugState;
            bool predictionActive = bootstrap.IsPredictionModeActive();

            state.primaryTrack = predictionActive ? "PredictionV2" : "Legacy";
            state.systemMapSummary = predictionActive
                ? "Main=V2 locomotion/modifiers/impulse/teleport/handoff | Compat=mode/skill/respawn/handoff/visual/camera/presentation | Legacy kept as fallback"
                : "Main=Legacy movement/camera/presentation | V2 remains isolated but available";
            state.compatibilityLayerCount = 0;

            List<string> activeCompat = new();
            AddCompat(activeCompat, state, "ModeSwitcher", bootstrap.ModeSwitcher != null, true);
            AddCompat(activeCompat, state, "SkillBridge", bootstrap.SkillMovementBridge != null, true);
            AddCompat(activeCompat, state, "RespawnBridge", bootstrap.RespawnBridge != null, true);
            AddCompat(activeCompat, state, "HandoffBridge", bootstrap.HandoffBridge != null, true);
            AddCompat(activeCompat, state, "VisualBridge", bootstrap.VisualRootBridge != null, true);
            AddCompat(activeCompat, state, "CameraBridge", bootstrap.CameraBridge != null, true);
            AddCompat(activeCompat, state, "PresentationBridge", bootstrap.PresentationBridge != null, true);
            AddCompat(activeCompat, state, "LegacyMode", true, true);
            AddCompat(activeCompat, state, "SkillExecutorDualPath", true, true);
            AddCompat(activeCompat, state, "CombatRoutingFallback", true, true);

            state.compatibilitySummary = activeCompat.Count > 0 ? string.Join(", ", activeCompat) : "none";
            state.runtimeAutoAddSummary = BuildRuntimeAutoAddSummary();
            state.fallbackSummary = "Legacy mode, SkillExecutor dual-path, combat routing fallback remain armed";
            state.legacyResponsibilitySummary = BuildLegacyResponsibilitySummary();
            state.validationChecklistSummary = BuddahPredictionValidationChecklist.BuildFocusSummary();
            state.movementChecklistSummary = BuddahPredictionValidationChecklist.BuildMovementChecklist();
            state.combatChecklistSummary = BuddahPredictionValidationChecklist.BuildCombatChecklist();
            state.presentationChecklistSummary = BuddahPredictionValidationChecklist.BuildPresentationChecklist();
            state.legacyChecklistSummary = BuddahPredictionValidationChecklist.BuildLegacyChecklist();
            state.highRiskFlagsSummary = BuildHighRiskSummary(predictionActive);
        }

        private void AddCompat(List<string> activeCompat, BuddahPredictionDebugState state, string name, bool active, bool countWhenActive)
        {
            if (!active)
                return;

            activeCompat.Add(name);
            if (countWhenActive)
                state.compatibilityLayerCount++;
        }

        private string BuildRuntimeAutoAddSummary()
        {
            List<string> autoAdded = new();

            if (bootstrap.WasVisualRootBridgeAutoAdded)
                autoAdded.Add("VisualBridge");
            if (bootstrap.WasCameraBridgeAutoAdded)
                autoAdded.Add("CameraBridge");
            if (bootstrap.WasPresentationBridgeAutoAdded)
                autoAdded.Add("PresentationBridge");
            if (bootstrap.WasCompatibilityRegistryAutoAdded)
                autoAdded.Add("CompatRegistry");
            if (bootstrap.WasRuntimeHealthReportAutoAdded)
                autoAdded.Add("HealthReport");

            return autoAdded.Count > 0 ? string.Join(", ", autoAdded) : "none";
        }

        private string BuildLegacyResponsibilitySummary()
        {
            StringBuilder builder = new();
            builder.Append("BuddahMovement=input/control shell; ");
            builder.Append("BuddahRespawn=target pose calculation; ");
            builder.Append("SkillExecutor=dual-path skill dispatch; ");
            builder.Append("PlayerCamera/Cinemachine=follow/presentation; ");
            builder.Append("PlayerScaleEffect=visual scale; ");
            builder.Append("HandControl/PushHitbox/projectile runtimes=attack spawning/hit auth");
            return builder.ToString();
        }

        private string BuildHighRiskSummary(bool predictionActive)
        {
            List<string> risks = new();

            if (bootstrap.WasVisualRootBridgeAutoAdded || bootstrap.WasCameraBridgeAutoAdded || bootstrap.WasPresentationBridgeAutoAdded)
                risks.Add("runtime auto-add bridge");

            if (predictionActive && bootstrap.VisualRootBridge != null)
            {
                Transform movementRoot = bootstrap.VisualRootBridge.GetMovementRoot();
                Transform visualRoot = bootstrap.VisualRootBridge.GetVisualRoot();
                if (movementRoot == visualRoot)
                    risks.Add("visual root defaults to movement root");
            }

            if (predictionActive && bootstrap.HandoffBridge == null)
                risks.Add("handoff bridge not serialized");
            if (predictionActive && bootstrap.RespawnBridge == null)
                risks.Add("respawn bridge not serialized");

            risks.Add("presentation deep target semantics");
            risks.Add("host/client path divergence");

            return risks.Count > 0 ? string.Join(" | ", risks) : "none";
        }
    }
}
