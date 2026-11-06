using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BuddahGo.Tests
{
    public class YuanqiContextTests
    {
        private const double TickDelta = 0.02;
        private GameObject _host;
        private SkillStateBoard _board;
        private uint _now;

        [SetUp]
        public void SetUp()
        {
            _now = 100u;
            _host = new GameObject("SkillStateBoardTest");
            _board = _host.AddComponent<SkillStateBoard>();
            _board.BindClock(() => _now, TickDelta);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        // Each part or case gets a new board on a new host, as a separate test would.
        private void Renew() { TearDown(); SetUp(); }
        private (string name, Action check) Fresh(string name, Action body) => (name, () => { Renew(); body(); });

        [Test]
        public void BoardTurnsWindowSecondsIntoTicksAndClearEndsEveryEntry()
        {
            EveryCase.Parts(
                Fresh("window seconds become ticks on the board clock", () =>
                {
                    SkillStateEntry curtain = _board.Begin("blackcurtain", 2, new SkillActiveWindow(1f, 10f), null);
                    Assert.AreEqual(150u, curtain.StartTick);
                    Assert.AreEqual(650u, curtain.UntilTick);
                    Assert.IsFalse(curtain.IsActivated);

                    Assert.IsNull(_board.Begin("acceleration", 0, new SkillActiveWindow(0f, 0f), null), "no duration, no entry");
                    Assert.IsNull(_board.Begin("acceleration", 0, new SkillActiveWindow(0f, -1f), null));
                }),
                Fresh("Clear ends every entry", () =>
                {
                    _board.Begin("acceleration", 0, new SkillActiveWindow(0f, 3f), null);
                    _board.Begin("slowtrap", 1, new SkillActiveWindow(0f, 6f), null);
                    int ended = 0;
                    _board.Ended += e => ended++;
                    _board.Clear("respawn");
                    Assert.AreEqual(2, ended);
                    Assert.AreEqual(0, _board.Entries.Count);
                }));
        }

        [Test]
        public void ConsumeCallsTheHandleAndEndsTheEntryOnce()
        {
            EveryCase.Parts(
                Fresh("consume calls the handle once and ends the entry once", () =>
                {
                    var handle = new CountingHandle("acceleration");
                    SkillStateEntry a = _board.Begin("acceleration", 0, new SkillActiveWindow(0f, 3f), handle);
                    SkillStateEntry b = _board.Begin("slowtrap", 1, new SkillActiveWindow(0f, 6f), null);
                    int ended = 0;
                    _board.Ended += e => ended++;

                    var ctx = new YuanqiContext(null, null, a, b, _now, _board);
                    ctx.ConsumeA("frost_lock");
                    ctx.ConsumeA("frost_lock");

                    Assert.AreEqual(1, handle.ConsumeCount, "the handle is consumed once");
                    Assert.AreEqual("frost_lock", handle.LastReason);
                    Assert.AreEqual(1, ended, "the entry ends once");
                    Assert.IsTrue(a.IsEnded);
                    Assert.IsFalse(b.IsEnded);
                    Assert.IsFalse(_board.TryGetActive("acceleration", out _));
                    Assert.IsTrue(_board.TryGetActive("slowtrap", out SkillStateEntry stillActive));
                    Assert.AreSame(b, stillActive);

                    ctx.ConsumeB("frost_lock");
                    Assert.IsTrue(b.IsEnded, "an entry without a handle still ends");
                    Assert.AreEqual(2, ended);
                }),
                Fresh("consuming an entry that already ended does nothing", () =>
                {
                    var handle = new CountingHandle("giant");
                    SkillStateEntry a = _board.Begin("giant", 0, new SkillActiveWindow(0f, 6f), handle);
                    SkillStateEntry b = _board.Begin("push_projectile_hands", 1, new SkillActiveWindow(0f, 15f), null);
                    _board.End(a.EntryId, "expired");

                    var ctx = new YuanqiContext(null, null, a, b, _now, _board);
                    ctx.ConsumeA("giant_palm");
                    Assert.AreEqual(0, handle.ConsumeCount);
                }),
                Fresh("a throwing handle still ends the entry", () =>
                {
                    SkillStateEntry a = _board.Begin("acceleration", 0, new SkillActiveWindow(0f, 3f), new ThrowingHandle());
                    SkillStateEntry b = _board.Begin("slowtrap", 1, new SkillActiveWindow(0f, 6f), null);
                    var ctx = new YuanqiContext(null, null, a, b, _now, _board);

                    LogAssert.Expect(LogType.Exception, new Regex("boom"));
                    ctx.ConsumeA("frost_lock");
                    Assert.IsTrue(a.IsEnded, "the board stays consistent when a skill fails to end its effect");
                }),
                ("ConsumeConfigured follows the table flags", () => EveryCase.Check(new[]
                {
                    (consumeA: true, consumeB: true),
                    (consumeA: true, consumeB: false),
                    (consumeA: false, consumeB: true),
                    (consumeA: false, consumeB: false),
                }, c =>
                {
                    Renew();
                    var handleA = new CountingHandle("acceleration");
                    var handleB = new CountingHandle("slowtrap");
                    SkillStateEntry a = _board.Begin("acceleration", 0, new SkillActiveWindow(0f, 3f), handleA);
                    SkillStateEntry b = _board.Begin("slowtrap", 1, new SkillActiveWindow(0f, 6f), handleB);
                    var action = ScriptableObject.CreateInstance<YuanqiTestAction>();
                    try
                    {
                        action.consumeA = c.consumeA;
                        action.consumeB = c.consumeB;
                        var ctx = new YuanqiContext(null, action, a, b, _now, _board);
                        ctx.ConsumeConfigured("yuanqi:test");
                        ctx.ConsumeConfigured("yuanqi:test");

                        Assert.AreEqual(c.consumeA ? 1 : 0, handleA.ConsumeCount, "skillA consumed only when consumeA");
                        Assert.AreEqual(c.consumeB ? 1 : 0, handleB.ConsumeCount, "skillB consumed only when consumeB");
                        Assert.AreEqual(c.consumeA, a.IsEnded);
                        Assert.AreEqual(c.consumeB, b.IsEnded);
                    }
                    finally { Object.DestroyImmediate(action); }
                }, c => $"consumeA={c.consumeA} consumeB={c.consumeB}")),
                Fresh("ConsumeConfigured without an action consumes nothing", () =>
                {
                    var handle = new CountingHandle("acceleration");
                    SkillStateEntry a = _board.Begin("acceleration", 0, new SkillActiveWindow(0f, 3f), handle);
                    SkillStateEntry b = _board.Begin("slowtrap", 1, new SkillActiveWindow(0f, 6f), null);
                    new YuanqiContext(null, null, a, b, _now, _board).ConsumeConfigured("yuanqi:test");
                    Assert.AreEqual(0, handle.ConsumeCount);
                    Assert.IsFalse(a.IsEnded || b.IsEnded);
                }));
        }

        private sealed class CountingHandle : ISkillRuntimeHandle
        {
            public CountingHandle(string skillId) { SkillId = skillId; }
            public string SkillId { get; }
            public bool IsActive => ConsumeCount == 0;
            public int ConsumeCount;
            public string LastReason;
            public void Consume(string reason) { ConsumeCount++; LastReason = reason; }
        }

        private sealed class ThrowingHandle : ISkillRuntimeHandle
        {
            public string SkillId => "acceleration";
            public bool IsActive => true;
            public void Consume(string reason) => throw new InvalidOperationException("boom");
        }
    }
}
