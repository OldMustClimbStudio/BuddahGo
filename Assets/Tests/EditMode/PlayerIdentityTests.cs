using System;
using System.Linq;
using FishNet.Connection;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class PlayerIdentityTests
    {
        [Test]
        public void TransportAddressRequiresConnectionAndPreservesNonBlankAddress()
        {
            var go = new GameObject("Identity transport fixture");
            try
            {
                var transport = go.AddComponent<IdentityTestTransport>();
                Assert.That(PlayerIdentity.GetSteamIdForConnection(transport, null), Is.Empty);
                Assert.That(transport.AddressCalls, Is.Zero);
                var connection = new NetworkConnection();
                foreach (string address in new[] { null, "", "  ", "76561198000000000", " address " })
                {
                    transport.Address = address;
                    Assert.That(PlayerIdentity.GetSteamIdForConnection(transport, connection),
                        Is.EqualTo(string.IsNullOrWhiteSpace(address) ? string.Empty : address));
                    Assert.That(transport.LastConnectionId, Is.EqualTo(connection.ClientId));
                }
                Assert.That(transport.AddressCalls, Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FallbackNamesAndSentinelsCoverMissingIdentity()
        {
            EveryCase.Parts(
                ("fallback names preserve the client id", () =>
                    EveryCase.Check(new[] { (id: -1, expected: "Player -1"), (id: 0, expected: "Player 0"), (id: 42, expected: "Player 42") }, c =>
                    {
                        Assert.That(PlayerIdentity.FallbackName(c.id), Is.EqualTo(c.expected));
                        // Steamworks uses platform-specific DLL names. Reflect this nullable-Lobby boundary
                        // so the pure fallback test does not hardcode one native platform's assembly reference.
                        Type lobbyType = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType("Steamworks.Data.Lobby")).FirstOrDefault(t => t != null);
                        Assert.That(lobbyType, Is.Not.Null, "Steamworks Lobby type must be loaded.");
                        var resolve = typeof(PlayerIdentity).GetMethod("ResolvePlayerName",
                            new[] { typeof(string), typeof(int), typeof(Nullable<>).MakeGenericType(lobbyType) });
                        Assert.That(resolve, Is.Not.Null, "Resolve the exact nullable-Lobby overload.");
                        Assert.That(resolve.Invoke(null, new object[] { null, c.id, null }), Is.EqualTo(c.expected));
                    })),
                ("a missing connection and transport use the existing sentinels", () =>
                {
                    Assert.That(PlayerIdentity.GetLocalClientId(null), Is.EqualTo(-1));
                    Assert.That(PlayerIdentity.GetSteamIdForConnection(null, null), Is.Empty);
                    Assert.That(PlayerIdentity.GetLocalClientId(new NetworkConnection()), Is.EqualTo(-1));
                }));
        }
    }

    // No sockets or Steam startup: exercise the actual transport boundary deterministically.
    public sealed class IdentityTestTransport : Transport
    {
        public string Address;
        public int AddressCalls;
        public int LastConnectionId;
        public override string GetConnectionAddress(int id)
        {
            AddressCalls++;
            LastConnectionId = id;
            return Address;
        }
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        public override void HandleClientConnectionState(ClientConnectionStateArgs args) => OnClientConnectionState?.Invoke(args);
        public override void HandleServerConnectionState(ServerConnectionStateArgs args) => OnServerConnectionState?.Invoke(args);
        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs args) => OnRemoteConnectionState?.Invoke(args);
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs args) => OnClientReceivedData?.Invoke(args);
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs args) => OnServerReceivedData?.Invoke(args);
        public override LocalConnectionState GetConnectionState(bool server) => LocalConnectionState.Stopped;
        public override RemoteConnectionState GetConnectionState(int id) => RemoteConnectionState.Stopped;
        public override void SendToServer(byte channel, ArraySegment<byte> data) { }
        public override void SendToClient(byte channel, ArraySegment<byte> data, int id) { }
        public override void IterateIncoming(bool server) { }
        public override void IterateOutgoing(bool server) { }
        public override bool StartConnection(bool server) => false;
        public override bool StopConnection(bool server) => true;
        public override bool StopConnection(int id, bool immediately) => true;
        public override void Shutdown() { }
        public override int GetMTU(byte channel) => 1200;
    }
}
