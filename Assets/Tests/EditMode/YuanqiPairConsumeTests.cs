using System;
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

        // Each case gets a new caster, board and pair, as a separate test would.
        private void Renew() { TearDown(); SetUp(); }

        [Test]
        public void PairsConsumeWhatTheRowNames()
        {
            EveryCase.Parts(
                ("frost lock consumes what the row names", () => EveryCase.Check(new[]
                {
                    (consumeA: true, consumeB: true),
                    (consumeA: true, consumeB: false),
                    (consumeA: false, consumeB: true),
                    (consumeA: false, consumeB: false),
                }, c =>
                {
                    Renew();
                    var frost = Pair<Yuanqi_FrostLock>("frost_lock", "acceleration", "slowtrap", c.consumeA, c.consumeB);
                    var acceleration = new CountingHandle("acceleration");
                    var slowTrap = new CountingHandle("slowtrap");

                    frost.OnTriggerServer(Context(frost, acceleration, slowTrap));

                    Assert.AreEqual(c.consumeA ? 1 : 0, acceleration.ConsumeCount, "神足通 consumed only when consumeA");
                    Assert.AreEqual(c.consumeB ? 1 : 0, slowTrap.ConsumeCount, "禅定缓流 consumed only when consumeB");
                }, c => $"frost_lock consumeA={c.consumeA} consumeB={c.consumeB}")),
                // The pair is unordered: listing 法相金身 first must not end it in place of 神足通.
                ("giant charge consumes what the row names, whichever skill it lists first", () => EveryCase.Check(new[]
                {
                    (name: "shipped row consumes only the acceleration", skillA: "acceleration", skillB: "giant", consumeA: true, consumeB: false, accelerationConsumed: 1, giantConsumed: 0),
                    (name: "row listing the giant first still consumes the acceleration", skillA: "giant", skillB: "acceleration", consumeA: false, consumeB: true, accelerationConsumed: 1, giantConsumed: 0),
                    (name: "row consuming nothing keeps both skills", skillA: "acceleration", skillB: "giant", consumeA: false, consumeB: false, accelerationConsumed: 0, giantConsumed: 0),
                }, c =>
                {
                    Renew();
                    var charge = Pair<Yuanqi_GiantCharge>("giant_charge", c.skillA, c.skillB, c.consumeA, c.consumeB);
                    var handleA = new CountingHandle(c.skillA);
                    var handleB = new CountingHandle(c.skillB);
                    CountingHandle acceleration = c.skillA == "acceleration" ? handleA : handleB;
                    CountingHandle giant = c.skillA == "giant" ? handleA : handleB;

                    ExpectMissingHandsWarning();
                    charge.OnTriggerServer(Context(charge, handleA, handleB));

                    Assert.AreEqual(c.accelerationConsumed, acceleration.ConsumeCount, "神足通 consumes");
                    Assert.AreEqual(c.giantConsumed, giant.ConsumeCount, "法相金身 consumes (it keeps running unless its own flag is set)");
                }, c => "giant_charge " + c.name)));
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
