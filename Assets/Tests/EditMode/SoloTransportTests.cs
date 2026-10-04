using System.Collections;
using BuddahGo.Match;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace BuddahGo.Tests
{
    public class SoloTransportTests
    {
        private sealed class StateObserver
        {
            public bool ServerStarted, ClientStarted;
            public void Server(ServerConnectionStateArgs args) { ServerStarted |= args.ConnectionState == LocalConnectionState.Started; }
            public void Client(ClientConnectionStateArgs args) { ClientStarted |= args.ConnectionState == LocalConnectionState.Started; }
        }

        [UnityTest]
        public IEnumerator YakOnlyHost_InitializesManagersAndStopsCleanly()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            yield return new EnterPlayMode();
            NetworkManager network = Object.FindFirstObjectByType<NetworkManager>();
            Assert.That(network, Is.Not.Null);
            for (int i = 0; i < 120 && !network.Initialized; i++) yield return null;
            Assert.That(network.Initialized, Is.True, "NetworkManager must complete Awake initialization.");
            Multipass mp = network.TransportManager.Transport as Multipass;
            Assert.That(mp, Is.Not.Null);
            Assert.That(mp.GlobalServerActions, Is.True);
            // Do not capture iterator locals in closures across EnterPlayMode's domain reload.
            var observer = new StateObserver();
            network.ServerManager.OnServerConnectionState += observer.Server;
            network.ClientManager.OnClientConnectionState += observer.Client;
            Assert.That(mp.StartConnection(true, 1), Is.True);
            mp.SetClientTransport(1);
            Assert.That(network.ClientManager.StartConnection(), Is.True);
            for (int i = 0; i < 300 && !network.ClientManager.Started; i++) yield return null;
            Assert.That(observer.ServerStarted && network.ServerManager.Started, Is.True, "ServerManager must observe Yak state events.");
            Assert.That(observer.ClientStarted && network.ClientManager.Started, Is.True, "ClientManager must authenticate the local client.");
            Assert.That(mp.GetTransport(0).GetConnectionState(true), Is.EqualTo(LocalConnectionState.Stopped), "Steam server must never start.");
            Assert.That(mp.GetTransport(1).GetConnectionState(true), Is.EqualTo(LocalConnectionState.Started));
            network.ClientManager.StopConnection();
            network.ServerManager.StopConnection(true);
            yield return null;
            Assert.That(network.ClientManager.Started || network.ServerManager.Started, Is.False);
            yield return new ExitPlayMode();
        }
    }
}
