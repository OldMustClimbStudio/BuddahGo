using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    [RequireComponent(typeof(NetworkObject))]
    public class ResultDecisionManager : NetworkBehaviour
    {
        public static ResultDecisionManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField, Min(1)] private int decisionDurationSeconds = 60;
        [SerializeField] private string nextScenePropertySelection = "PropertySelection";
        [SerializeField] private string returnSceneMainMenu = "MainMenu";

        public readonly SyncList<ResultPlayerDecision> PlayerDecisions = new SyncList<ResultPlayerDecision>();

        private readonly SyncVar<int> _remainingSeconds = new SyncVar<int>();
        private readonly SyncVar<bool> _decisionActive = new SyncVar<bool>();
        private readonly SyncVar<ResultFinalDecision> _finalDecision = new SyncVar<ResultFinalDecision>();

        private readonly HashSet<int> _trackedParticipantIds = new HashSet<int>();
        private Coroutine _decisionCountdownRoutine;
        private bool _serverInitializedDecisions;

        public int RemainingSeconds => _remainingSeconds.Value;
        public bool IsDecisionActive => _decisionActive.Value;
        public ResultFinalDecision FinalDecision => _finalDecision.Value;
        public string NextScenePropertySelection => nextScenePropertySelection;
        public string ReturnSceneMainMenu => returnSceneMainMenu;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeDecisionServer();

            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;

            if (_decisionCountdownRoutine != null)
            {
                StopCoroutine(_decisionCountdownRoutine);
                _decisionCountdownRoutine = null;
            }

            _trackedParticipantIds.Clear();
            _serverInitializedDecisions = false;
            PlayerDecisions.Clear();
            _remainingSeconds.Value = 0;
            _decisionActive.Value = false;
            _finalDecision.Value = ResultFinalDecision.None;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitChoiceServerRpc(ResultPlayerChoice choice, NetworkConnection caller = null)
        {
            if (!IsServerInitialized || caller == null || !caller.IsAuthenticated)
                return;

            if (!_decisionActive.Value || _finalDecision.Value != ResultFinalDecision.None)
                return;

            if (!_trackedParticipantIds.Contains(caller.ClientId))
                return;

            int decisionIndex = FindDecisionIndex(caller.ClientId);
            if (decisionIndex < 0)
                return;

            ResultPlayerDecision decision = PlayerDecisions[decisionIndex];
            decision.Choice = choice;
            decision.HasResponded = choice != ResultPlayerChoice.None;
            PlayerDecisions[decisionIndex] = decision;

            EvaluateDecisionServer($"choice:{caller.ClientId}:{choice}");
        }

        public bool TryGetLocalDecision(out ResultPlayerDecision decision)
        {
            int localClientId = GetLocalClientId();
            if (localClientId >= 0)
                return TryGetDecision(localClientId, out decision);

            decision = default;
            return false;
        }

        public bool TryGetDecision(int clientId, out ResultPlayerDecision decision)
        {
            int index = FindDecisionIndex(clientId);
            if (index >= 0)
            {
                decision = PlayerDecisions[index];
                return true;
            }

            decision = default;
            return false;
        }

        public string BuildDecisionSummary()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < PlayerDecisions.Count; i++)
            {
                ResultPlayerDecision decision = PlayerDecisions[i];
                string choiceLabel = decision.HasResponded ? decision.Choice.ToString() : "Waiting";
                sb.AppendLine($"{decision.PlayerName}: {choiceLabel}");
            }

            return sb.ToString();
        }

        [Server]
        private void InitializeDecisionServer()
        {
            if (_serverInitializedDecisions)
                return;

            _serverInitializedDecisions = true;
            _trackedParticipantIds.Clear();
            PlayerDecisions.Clear();
            _remainingSeconds.Value = Mathf.Max(1, decisionDurationSeconds);
            _decisionActive.Value = true;
            _finalDecision.Value = ResultFinalDecision.None;

            if (RoomStateManager.Instance != null)
            {
                for (int i = 0; i < RoomStateManager.Instance.Players.Count; i++)
                {
                    RoomPlayerState player = RoomStateManager.Instance.Players[i];
                    _trackedParticipantIds.Add(player.PlayerId);
                    PlayerDecisions.Add(new ResultPlayerDecision
                    {
                        ClientId = player.PlayerId,
                        PlayerName = string.IsNullOrWhiteSpace(player.PlayerName) ? $"Player {player.PlayerId}" : player.PlayerName,
                        Choice = ResultPlayerChoice.None,
                        HasResponded = false
                    });
                }
            }

            _decisionCountdownRoutine = StartCoroutine(DecisionCountdownCoroutine());
        }

        [Server]
        private IEnumerator DecisionCountdownCoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(1f);
            while (_decisionActive.Value && _finalDecision.Value == ResultFinalDecision.None && _remainingSeconds.Value > 0)
            {
                yield return wait;

                if (!_decisionActive.Value || _finalDecision.Value != ResultFinalDecision.None)
                    yield break;

                _remainingSeconds.Value = Mathf.Max(0, _remainingSeconds.Value - 1);
                if (_remainingSeconds.Value <= 0)
                {
                    FinalizeDecisionServer(ResultFinalDecision.ReturnToRoom, "timeout");
                    yield break;
                }
            }
        }

        [Server]
        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (!_decisionActive.Value || _finalDecision.Value != ResultFinalDecision.None || conn == null)
                return;

            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            if (!_trackedParticipantIds.Contains(conn.ClientId))
                return;

            FinalizeDecisionServer(ResultFinalDecision.ReturnToRoom, $"disconnect:{conn.ClientId}");
        }

        [Server]
        private void EvaluateDecisionServer(string reason)
        {
            if (!_decisionActive.Value || _finalDecision.Value != ResultFinalDecision.None)
                return;

            bool anyReturnToRoom = false;
            bool allStartNewGame = PlayerDecisions.Count > 0;

            for (int i = 0; i < PlayerDecisions.Count; i++)
            {
                ResultPlayerDecision decision = PlayerDecisions[i];
                if (decision.Choice == ResultPlayerChoice.ReturnToRoom)
                {
                    anyReturnToRoom = true;
                    break;
                }

                if (decision.Choice != ResultPlayerChoice.StartNewGame)
                    allStartNewGame = false;
            }

            if (anyReturnToRoom)
            {
                FinalizeDecisionServer(ResultFinalDecision.ReturnToRoom, reason);
                return;
            }

            if (allStartNewGame)
                FinalizeDecisionServer(ResultFinalDecision.StartNewGame, reason);
        }

        [Server]
        private void FinalizeDecisionServer(ResultFinalDecision finalDecision, string reason)
        {
            if (_finalDecision.Value != ResultFinalDecision.None)
                return;

            _finalDecision.Value = finalDecision;
            _decisionActive.Value = false;
            _remainingSeconds.Value = Mathf.Max(0, _remainingSeconds.Value);

            if (_decisionCountdownRoutine != null)
            {
                StopCoroutine(_decisionCountdownRoutine);
                _decisionCountdownRoutine = null;
            }

            Debug.Log($"[ResultDecisionManager] Finalized result decision={finalDecision} reason={reason}");

            if (RoomStateManager.Instance == null)
            {
                Debug.LogWarning("[ResultDecisionManager] RoomStateManager.Instance is null. Cannot execute final result decision.");
                return;
            }

            switch (finalDecision)
            {
                case ResultFinalDecision.StartNewGame:
                    RoomStateManager.Instance.StartNextGameFromResultServer(nextScenePropertySelection);
                    break;
                case ResultFinalDecision.ReturnToRoom:
                default:
                    RoomStateManager.Instance.ReturnToRoomMenuKeepingSessionServer(returnSceneMainMenu);
                    break;
            }
        }

        private int FindDecisionIndex(int clientId)
        {
            for (int i = 0; i < PlayerDecisions.Count; i++)
            {
                if (PlayerDecisions[i].ClientId == clientId)
                    return i;
            }

            return -1;
        }

        private int GetLocalClientId()
        {
            if (GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection == null)
                return -1;

            return GameNetworkManager.Instance.FishNetManager.ClientManager.Connection.ClientId;
        }
    }
}
