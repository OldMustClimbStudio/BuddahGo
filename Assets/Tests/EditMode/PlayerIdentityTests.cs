using FishNet.Connection;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class PlayerIdentityTests
    {
        [TestCase(-1, "Player -1")]
        [TestCase(0, "Player 0")]
        [TestCase(42, "Player 42")]
        public void FallbackNamesPreserveClientId(int id, string expected)
        {
            Assert.That(PlayerIdentity.FallbackName(id), Is.EqualTo(expected));
            // Steamworks uses platform-specific DLL names. Reflect this nullable-Lobby boundary
            // so the pure fallback test does not hardcode one native platform's assembly reference.
            var resolve = typeof(PlayerIdentity).GetMethod("ResolvePlayerName");
            Assert.That(resolve.Invoke(null, new object[] { null, id, null }), Is.EqualTo(expected));
        }

        [Test]
        public void MissingConnectionAndTransportUseExistingSentinels()
        {
            Assert.That(PlayerIdentity.GetLocalClientId(null), Is.EqualTo(-1));
            Assert.That(PlayerIdentity.GetSteamIdForConnection(null, null), Is.Empty);
            Assert.That(PlayerIdentity.GetSteamIdForConnection(null, new NetworkConnection()), Is.Empty);
        }
    }
}
