using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using SteamMultiplayer.Network;
using SteamMultiplayer.UI;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuddahGo.Match
{
    // Pumped by the persistent GameNetworkManager; stopping never occurs in an RPC stack.
    public sealed class SessionLauncher : ISessionControl
    {
        private readonly NetworkManager _network;
        private int _stopRequestedFrame = -1;
        private float _startupDeadline;
        private SoloMatchSettings _failedSoloSettings;
        public SoloMatchSettings Settings { get; private set; }
        public bool IsStarting { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool IsOnlineAvailable => SteamClient.IsValid
            && _network != null
            && _network.TransportManager?.Transport is Multipass mp
            && mp.GetTransport(0) is FishyFacepunch.FishyFacepunch steam
            && steam.IsSteamAvailable;

        public const string SteamUnavailableMessage = "Steam 不可用，请启动 Steam 后重开游戏";

        public SessionLauncher(NetworkManager network) { _network = network; }

        public bool StartSoloHost(SoloMatchSettings settings)
        {
            if (settings == null) return Fail("单机设置缺失。");
            return StartHost(1, new SoloMatchRules(settings), settings);
        }

        public bool StartOnlineHost()
        {
            if (!IsOnlineAvailable) return Fail(SteamUnavailableMessage);
            return StartHost(0, new OnlineMatchRules(), null);
        }

        private bool TryGetTransport(out Multipass transport)
        {
            transport = _network != null ? _network.TransportManager?.Transport as Multipass : null;
            if (transport == null || transport.Transports.Count != 2 || !transport.GlobalServerActions)
                return Fail("传输层配置无效：需要 FishyFacepunch + Yak Multipass。");
            if (transport.GetTransport(0).GetType().Name != "FishyFacepunch"
                || !(transport.GetTransport(1) is FishNet.Transporting.Yak.Yak))
                return Fail("传输层顺序无效。");
            if (IsStarting || _stopRequestedFrame >= 0 || _network.ServerManager.Started || _network.ClientManager.Started)
                return Fail("会话仍在运行或关闭中，请稍后重试。");
            return true;
        }

        private bool StartHost(int index, IMatchRules rules, SoloMatchSettings settings)
        {
            if (!TryGetTransport(out Multipass transport)) return false;
            _failedSoloSettings = null;
            LastError = string.Empty;
            MatchRules.Current = rules;
            Settings = settings;
            SessionControl.SoloSettings = settings;
            try
            {
                // ServerManager subscribes to transport state events. Never start every child transport.
                if (!transport.StartConnection(true, index)) return Rollback("无法启动本地服务器。");
                transport.SetClientTransport(index);
                if (!_network.ClientManager.StartConnection()) return Rollback("无法连接本地服务器。");
                IsStarting = true;
                _startupDeadline = Time.realtimeSinceStartup + 20f;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SessionLauncher] Startup failed: " + exception.Message);
                return Rollback("启动失败，请重试。" );
            }
        }

        public bool StartOnlineClient(string hostSteamId)
        {
            if (!IsOnlineAvailable) return Fail(SteamUnavailableMessage);
            if (string.IsNullOrWhiteSpace(hostSteamId)) return Fail("主机地址为空。");
            if (!TryGetTransport(out Multipass transport)) return false;
            _failedSoloSettings = null;
            LastError = string.Empty;
            MatchRules.Reset();
            Settings = null;
            SessionControl.SoloSettings = null;
            try
            {
                transport.SetClientTransport(0);
                transport.SetClientAddress(hostSteamId, 0);
                if (!_network.ClientManager.StartConnection()) return Rollback("无法连接主机。");
                IsStarting = true;
                _startupDeadline = Time.realtimeSinceStartup + 30f;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SessionLauncher] Startup failed: " + exception.Message);
                return Rollback("连接失败，请重试。");
            }
        }

        public void RequestStopSession()
        {
            if (_stopRequestedFrame < 0) _stopRequestedFrame = Time.frameCount;
        }

        public void Pump()
        {
            if (_stopRequestedFrame >= 0 && Time.frameCount > _stopRequestedFrame)
            {
                _stopRequestedFrame = -1;
                StopTransportAndReset();
                SceneFadeController.ResetSessionTransition();
                SceneManager.LoadScene(SceneNames.MainMenu);
                return;
            }
            if (!IsStarting) return;
            // Local transport connection precedes room authentication and scene loading.
            // Keep the Solo deadline armed until its selection screen is actually ready.
            var selection = PropertiesSelectionManager.Instance;
            bool ready = _network.ClientManager.Started && (Settings == null ||
                (selection != null && selection.IsClientInitialized && selection.IsStageCountdownActive));
            if (ready) IsStarting = false;
            else if (Time.realtimeSinceStartup >= _startupDeadline) Rollback("连接超时，请重试。");
        }

        public SoloMatchSettings TakeFailedSoloSettings()
        {
            var settings = _failedSoloSettings;
            _failedSoloSettings = null;
            return settings;
        }

        private bool Rollback(string message)
        {
            if (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            {
                _failedSoloSettings = Settings;
                // Authentication/selection readiness can fail after the menu unloaded.
                // Return through the same next-frame stop boundary as an explicit quit.
                IsStarting = false;
                RequestStopSession();
            }
            else
            {
                StopTransportAndReset();
                SceneFadeController.ResetSessionTransition();
            }
            return Fail(message);
        }

        private bool Fail(string message) { LastError = message; return false; }

        private void StopTransportAndReset()
        {
            IsStarting = false;
            // Also stop Starting connections; Started alone misses a partially started transport.
            _network?.ClientManager?.StopConnection();
            _network?.ServerManager?.StopConnection(true);
            Settings = null;
            ResetMatchGlobals();
        }

        // Every per-session static (rules, Solo request, input block, clock/timing/end policy,
        // racer directory, selection cache) is cleared here and nowhere else.
        internal static void ResetMatchGlobals()
        {
            MatchRules.Reset();
            SessionControl.SoloSettings = null;
            ResolvedPropertySelectionCache.Clear();
            LocalInputBlock.Current = null;
            MatchServices.Reset();
        }
    }
}
