using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public class ResultAreaInteractionGate : MonoBehaviour
    {
        public static ResultAreaInteractionGate Instance { get; private set; }

        [Header("Result Area Rules")]
        [SerializeField] private bool allowMovementInResultArea = true;
        [SerializeField] private bool allowSkillsInResultArea = true;
        [SerializeField] private bool enableDebugLogs;

        public bool AllowMovementInResultArea => allowMovementInResultArea;
        public bool AllowSkillsInResultArea => allowSkillsInResultArea;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static bool ShouldAllowMovementInput(GameObject actor)
        {
            RoomStateManager room = RoomStateManager.Instance;
            PlayerFinishPresentationController presentation = ResolvePresentation(actor);
            if (presentation != null && presentation.BlocksMovementInput)
                return false;

            if (room == null)
                return true;

            if (room.IsResultPhaseActive)
                return Instance == null || Instance.allowMovementInResultArea;

            return room.IsMatchPhaseActive && room.IsRaceStarted;
        }

        public static bool ShouldAllowSkillInput(GameObject actor)
        {
            RoomStateManager room = RoomStateManager.Instance;
            PlayerFinishPresentationController presentation = ResolvePresentation(actor);
            if (presentation != null && presentation.BlocksSkillInput)
                return false;

            if (room == null)
                return true;

            if (room.IsResultPhaseActive)
                return Instance == null || Instance.allowSkillsInResultArea;

            return room.IsMatchPhaseActive && room.IsRaceStarted;
        }

        public static bool ShouldProcessRaceProgress(GameObject actor)
        {
            RoomStateManager room = RoomStateManager.Instance;
            if (room == null || !room.IsMatchPhaseActive || !room.IsRaceStarted)
                return false;

            MatchResultPresentationCoordinator coordinator = MatchResultPresentationCoordinator.Instance;
            if (coordinator != null && coordinator.IsGlobalResultPresentationActive)
                return false;

            PlayerFinishPresentationController presentation = ResolvePresentation(actor);
            return presentation == null || !presentation.BlocksRaceProgression;
        }

        private static PlayerFinishPresentationController ResolvePresentation(GameObject actor)
        {
            if (actor == null)
                return null;

            PlayerFinishPresentationController presentation = actor.GetComponent<PlayerFinishPresentationController>();
            if (presentation == null)
                presentation = actor.GetComponentInParent<PlayerFinishPresentationController>();
            if (presentation == null)
                presentation = actor.GetComponentInChildren<PlayerFinishPresentationController>(true);

            return presentation;
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[ResultAreaInteractionGate] {message}");
        }
    }
}
