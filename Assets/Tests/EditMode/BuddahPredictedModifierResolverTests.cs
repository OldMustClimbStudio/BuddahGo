using NUnit.Framework;
using NewBuddah.PredictionV2.Config;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Resolver order: base → scale → additive accel / post-root accel / burst → max-speed penalty
    // → root/freeze. Config defaults (Awake does not run in EditMode): forwardForce 50,
    // turnTorque 30, maxSpeed 80.
    public class BuddahPredictedModifierResolverTests
    {
        private const uint Now = 100u;
        private GameObject _host;
        private BuddahPredictedMotorConfig _config;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ResolverTestConfig");
            _config = _host.AddComponent<BuddahPredictedMotorConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        private BuddahPredictedMotorComputedStats Resolve(BuddahPredictedModifierState state, uint tick = Now)
        {
            return BuddahPredictedModifierResolver.Resolve(state, _config, tick);
        }

        [Test]
        public void BaseValuesAndRootFreezeFlags()
        {
            EveryCase.Parts(
                ("no modifiers keep config base values", () =>
                {
                    var stats = Resolve(default);
                    Assert.That(stats.FinalForwardForce, Is.EqualTo(50f));
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo(80f));
                    Assert.That(stats.FinalTurnTorque, Is.EqualTo(30f));
                    Assert.That(stats.IsFrozen || stats.IsBurstActive || stats.IsMaxSpeedPenaltyActive || stats.IsRooted, Is.False);
                }),
                ("freeze stops force, speed and torque and raises root and steering flags", () =>
                {
                    var stats = Resolve(new BuddahPredictedModifierState { FreezeUntilTick = Now + 10 });
                    Assert.That(stats.FinalForwardForce, Is.Zero);
                    Assert.That(stats.FinalMaxSpeed, Is.Zero);
                    Assert.That(stats.FinalTurnTorque, Is.Zero);
                    Assert.That(stats.IsFrozen, Is.True);
                    Assert.That(stats.IsRooted, Is.True, "existing IsRooted consumers (motor stop step, AI) must see a freeze");
                    Assert.That(stats.IsSteeringSuppressed, Is.True);
                }),
                ("root alone keeps turn torque and is not a freeze", () =>
                {
                    var stats = Resolve(new BuddahPredictedModifierState { RootUntilTick = Now + 10 });
                    Assert.That(stats.IsRooted, Is.True);
                    Assert.That(stats.IsFrozen, Is.False);
                    Assert.That(stats.IsSteeringSuppressed, Is.False);
                    Assert.That(stats.FinalTurnTorque, Is.EqualTo(30f));
                }),
                ("modifiers are inactive on their deadline tick", () =>
                {
                    var state = new BuddahPredictedModifierState
                    {
                        FreezeUntilTick = Now, BurstUntilTick = Now, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f,
                        MaxSpeedPenaltyUntilTick = Now, MaxSpeedPenaltyMultiplier = 0.85f
                    };
                    var stats = Resolve(state);
                    Assert.That(stats.IsFrozen, Is.False);
                    Assert.That(stats.IsRooted, Is.False);
                    Assert.That(stats.IsSteeringSuppressed, Is.False);
                    Assert.That(stats.IsBurstActive, Is.False);
                    Assert.That(stats.IsMaxSpeedPenaltyActive, Is.False);
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo(80f));
                    Assert.That(stats.FinalTurnTorque, Is.EqualTo(30f));

                    var oneTickEarlier = Resolve(state, Now - 1);
                    Assert.That(oneTickEarlier.IsFrozen, Is.True);
                    Assert.That(oneTickEarlier.IsBurstActive, Is.False, "a frozen racer is not charging");
                    Assert.That(oneTickEarlier.IsMaxSpeedPenaltyActive, Is.True);
                }),
                ("a null config still applies the flags", () =>
                {
                    var stats = BuddahPredictedModifierResolver.Resolve(
                        new BuddahPredictedModifierState { FreezeUntilTick = Now + 1, MaxSpeedPenaltyUntilTick = Now + 1, MaxSpeedPenaltyMultiplier = 0.85f },
                        null, Now);
                    Assert.That(stats.IsFrozen && stats.IsRooted && stats.IsMaxSpeedPenaltyActive, Is.True);
                    Assert.That(stats.FinalMaxSpeed, Is.Zero);
                }));
        }

        [Test]
        public void BurstExtraMaxSpeedDecaysWithEaseOut()
        {
            EveryCase.Parts(
                // Burst of +60 over 48 ticks, applied at tick 100 → BurstUntilTick 148.
                // extra = 60 × remaining01², remaining01 = (148 − tick) / 48.
                ("ease-out decay", () => EveryCase.Check(new[]
                {
                    (tick: 100u, maxSpeed: 140f, active: true),                       // start: remaining01 = 1
                    (tick: 124u, maxSpeed: 95f, active: true),                        // middle: 0.5² × 60 = 15
                    (tick: 136u, maxSpeed: 83.75f, active: true),                     // 0.25² × 60 = 3.75
                    (tick: 147u, maxSpeed: 80f + 60f / (48f * 48f), active: true),    // last active tick
                    (tick: 148u, maxSpeed: 80f, active: false),                       // end: back to the base cap
                }, c =>
                {
                    var state = new BuddahPredictedModifierState { BurstUntilTick = 148, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f };
                    var stats = Resolve(state, c.tick);
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo(c.maxSpeed).Within(1e-4f));
                    Assert.That(stats.IsBurstActive, Is.EqualTo(c.active));
                    Assert.That(stats.FinalForwardForce, Is.EqualTo(50f), "burst only raises the cap");
                }, c => $"tick={c.tick}")),
                ("remaining is clamped and never divides by zero", () =>
                {
                    // Duration 0 is treated as 1 tick; a deadline farther away than the duration clamps to 1.
                    var zeroDuration = new BuddahPredictedModifierState { BurstUntilTick = Now + 1, BurstDurationTicks = 0, BurstExtraMaxSpeed = 60f };
                    Assert.That(Resolve(zeroDuration).FinalMaxSpeed, Is.EqualTo(140f).Within(1e-4f));
                    var farDeadline = new BuddahPredictedModifierState { BurstUntilTick = Now + 500, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f };
                    Assert.That(Resolve(farDeadline).FinalMaxSpeed, Is.EqualTo(140f).Within(1e-4f));
                }));
        }

        [Test]
        public void OwnerReplayOfABurstMatchesTheServerCapOnEveryReplayedTick()
        {
            // Server: +60 over 48 ticks applied at server tick 5000. The owner reconciles the snapshot of server
            // tick 5010 (paired with its own tick 3010) and replays up to, excluding, its live tick 3019.
            const uint serverApply = 5000u, serverState = 5010u, clientState = 3010u, liveTick = 3019u;
            var serverModifiers = new BuddahPredictedModifierState
            {
                BurstUntilTick = serverApply + 48u, BurstDurationTicks = 48u, BurstExtraMaxSpeed = 60f
            };
            BuddahPredictedModifierState local = BuddahTickMath.ModifiersToLocal(serverModifiers, serverState, clientState);

            for (uint step = 1u; clientState + step < liveTick; step++)
            {
                uint clientReplay = clientState + step, serverReplay = serverState + step;
                float serverCap = Resolve(serverModifiers, serverReplay).FinalMaxSpeed;
                float expected = 80f + 60f * Mathf.Pow((serverApply + 48u - serverReplay) / 48f, 2f);
                Assert.That(serverCap, Is.EqualTo(expected).Within(1e-3f), "server ease-out at " + serverReplay);

                uint tick = BuddahTickMath.ModifierResolveTick(false, true, liveTick, clientReplay);
                Assert.That(Resolve(local, tick).FinalMaxSpeed, Is.EqualTo(serverCap).Within(1e-4f), "replayed tick " + clientReplay);
                Assert.That(Resolve(local, liveTick).FinalMaxSpeed, Is.LessThan(serverCap),
                    "resolving every replayed tick at the live tick under-predicts the decaying cap");
            }
        }

        [Test]
        public void MaxSpeedPenaltyMultipliesLastAndRootFreezeStillWin()
        {
            EveryCase.Parts(
                ("penalty multiplies after accel, post-root and burst additions", () =>
                {
                    var state = new BuddahPredictedModifierState
                    {
                        AccelUntilTick = Now + 10, AccelExtraForwardForce = 5f, AccelExtraMaxSpeed = 20f,
                        PostRootAccelUntilTick = Now + 10, PostRootAccelExtraMaxSpeed = 10f,
                        BurstUntilTick = Now + 48, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f,
                        MaxSpeedPenaltyUntilTick = Now + 600, MaxSpeedPenaltyMultiplier = 0.85f
                    };
                    var stats = Resolve(state);
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo((80f + 20f + 10f + 60f) * 0.85f).Within(1e-4f));
                    Assert.That(stats.FinalForwardForce, Is.EqualTo(55f), "the penalty only touches max speed");
                    Assert.That(stats.IsMaxSpeedPenaltyActive, Is.True);
                }),
                ("penalty alone scales the base cap", () =>
                {
                    var stats = Resolve(new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = Now + 600, MaxSpeedPenaltyMultiplier = 0.85f });
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo(68f).Within(1e-4f));
                }),
                ("penalty multiplier is clamped", () => EveryCase.Check(new[]
                {
                    (multiplier: 0f, maxSpeed: 8f),     // clamped up to 0.1
                    (multiplier: 2f, maxSpeed: 80f),    // clamped down to 1 (a penalty never raises the cap)
                }, c =>
                {
                    var stats = Resolve(new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = Now + 1, MaxSpeedPenaltyMultiplier = c.multiplier });
                    Assert.That(stats.FinalMaxSpeed, Is.EqualTo(c.maxSpeed).Within(1e-4f));
                }, c => $"multiplier={c.multiplier}")),
                ("root and freeze still win over the penalty", () =>
                {
                    var penalty = new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = Now + 600, MaxSpeedPenaltyMultiplier = 0.85f };
                    var rooted = penalty;
                    rooted.RootUntilTick = Now + 5;
                    Assert.That(Resolve(rooted).FinalMaxSpeed, Is.Zero);
                    var frozen = penalty;
                    frozen.FreezeUntilTick = Now + 5;
                    Assert.That(Resolve(frozen).FinalMaxSpeed, Is.Zero);
                    Assert.That(Resolve(frozen).IsMaxSpeedPenaltyActive, Is.True);
                }),
                ("freeze overrides burst", () =>
                {
                    var state = new BuddahPredictedModifierState
                    {
                        FreezeUntilTick = Now + 5,
                        BurstUntilTick = Now + 48, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f
                    };
                    var stats = Resolve(state);
                    Assert.That(stats.FinalMaxSpeed, Is.Zero);
                    Assert.That(stats.FinalForwardForce, Is.Zero);
                    Assert.That(stats.IsFrozen, Is.True);
                    Assert.That(stats.IsBurstActive, Is.False, "a frozen body does not deal burst impacts");
                }));
        }
    }
}
