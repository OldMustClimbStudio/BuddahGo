using System.Reflection;
using System.Text.RegularExpressions;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BuddahGo.Tests
{
    /// <summary>
    /// The pair classes consume exactly what their Skill/Yuanqi row names (consumeA / consumeB), whichever skill the row
    /// lists first: a table edit is never overridden by a hard-coded consume. The caster is a SkillExecutor that is not
    /// server-initialized, so freeze, burst and impact are no-ops and only the consumes are observed.
    /// </summary>
    public class YuanqiPairConsumeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const double TickDelta = 0.02;
        private const uint Now = 100u;

        private GameObject _racer;
        private SkillExecutor _caster;
        private SkillStateBoard _board;
        private YuanqiAction _pair;

        [SetUp]
        public void SetUp()
        {
            _racer = new GameObject("YuanqiPairConsumeCaster");
            _racer.SetActive(false); // never found by the frost-lock target scan
            _caster = _racer.AddComponent<SkillExecutor>();
            NetworkObject networkObject = _racer.GetComponent<NetworkObject>();
            if (networkObject == null)
                networkObject = _racer.AddComponent<NetworkObject>();
            typeof(NetworkBehaviour).GetField("_networkObjectCache", Flags).SetValue(_caster, networkObject);
            _board = _racer.AddComponent<SkillStateBoard>();
            _board.BindClock(() => Now, TickDelta);
        }

        [TearDown]
        public void TearDown()
        {
            if (_pair != null) Object.DestroyImmediate(_pair);
            if (_racer != null) Object.DestroyImmediate(_racer);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void FrostLockConsumesWhatTheRowNames(bool consumeA, bool consumeB)
        {
            var frost = Pair<Yuanqi_FrostLock>("frost_lock", "acceleration", "slowtrap", consumeA, consumeB);
            var acceleration = new CountingHandle("acceleration");
            var slowTrap = new CountingHandle("slowtrap");

            frost.OnTriggerServer(Context(frost, acceleration, slowTrap));

            Assert.AreEqual(consumeA ? 1 : 0, acceleration.ConsumeCount, "神足通 consumed only when consumeA");
            Assert.AreEqual(consumeB ? 1 : 0, slowTrap.ConsumeCount, "禅定缓流 consumed only when consumeB");
        }

        [Test]
        public void GiantChargeShippedRowConsumesOnlyTheAcceleration()
        {
            var charge = Pair<Yuanqi_GiantCharge>("giant_charge", "acceleration", "giant", true, false);
            var acceleration = new CountingHandle("acceleration");
            var giant = new CountingHandle("giant");

            ExpectMissingHandsWarning();
            charge.OnTriggerServer(Context(charge, acceleration, giant));

            Assert.AreEqual(1, acceleration.ConsumeCount);
            Assert.AreEqual(0, giant.ConsumeCount, "法相金身 keeps running");
        }

        // The pair is unordered: listing 法相金身 first must not end it in place of 神足通.
        [Test]
        public void GiantChargeRowListingTheGiantFirstStillConsumesTheAcceleration()
        {
            var charge = Pair<Yuanqi_GiantCharge>("giant_charge", "giant", "acceleration", false, true);
            var giant = new CountingHandle("giant");
            var acceleration = new CountingHandle("acceleration");

            ExpectMissingHandsWarning();
            charge.OnTriggerServer(Context(charge, giant, acceleration));

            Assert.AreEqual(0, giant.ConsumeCount, "consumeA = false keeps 法相金身");
            Assert.AreEqual(1, acceleration.ConsumeCount, "consumeB = true ends 神足通");
        }

        [Test]
        public void GiantChargeRowConsumingNothingKeepsBothSkills()
        {
            var charge = Pair<Yuanqi_GiantCharge>("giant_charge", "acceleration", "giant", false, false);
            var acceleration = new CountingHandle("acceleration");
            var giant = new CountingHandle("giant");

            ExpectMissingHandsWarning();
            charge.OnTriggerServer(Context(charge, acceleration, giant));

            Assert.AreEqual(0, acceleration.ConsumeCount);
            Assert.AreEqual(0, giant.ConsumeCount);
        }

        private T Pair<T>(string id, string skillA, string skillB, bool consumeA, bool consumeB) where T : YuanqiAction
        {
            var pair = ScriptableObject.CreateInstance<T>();
            pair.yuanqiId = id;
            pair.skillA = skillA;
            pair.skillB = skillB;
            pair.consumeA = consumeA;
            pair.consumeB = consumeB;
            _pair = pair;
            return pair;
        }

        // EntryA belongs to skillA, as YuanqiPairTable.TryMatch orders them.
        private YuanqiContext Context(YuanqiAction pair, CountingHandle handleA, CountingHandle handleB)
        {
            SkillStateEntry a = _board.Begin(pair.skillA, 0, new SkillActiveWindow(0f, 3f), handleA);
            SkillStateEntry b = _board.Begin(pair.skillB, 1, new SkillActiveWindow(0f, 6f), handleB);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            return new YuanqiContext(_caster, pair, a, b, Now, _board);
        }

        // The test racer has no BuddahHandControl: the impact stays unarmed with a warning (not an error).
        private static void ExpectMissingHandsWarning()
            => LogAssert.Expect(LogType.Warning, new Regex("has no BuddahHandControl"));

        private sealed class CountingHandle : ISkillRuntimeHandle
        {
            public CountingHandle(string skillId) { SkillId = skillId; }
            public string SkillId { get; }
            public bool IsActive => ConsumeCount == 0;
            public int ConsumeCount;
            public void Consume(string reason) => ConsumeCount++;
        }
    }
}
