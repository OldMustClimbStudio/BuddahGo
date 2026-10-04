#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BuddahGo.AI
{
    // Explicitly controlled effect matrix. Never counted as natural racing or AI decision evidence.
    public sealed class AISkillEffectHarness : MonoBehaviour
    {
        public string Status { get; private set; }
        private AISkillRaceHarness _race;
        private bool _backlash;
        private string _directory, _case;
        private StreamWriter _samples, _events;
        private RacerIdentity[] _racers;
        private Keyboard _keyboard;
        private SkillExecutor _source;
        private double _caseStart;
        private float _oldPmax, _oldKeep;
        private ObsessionFigure _changedObsession;
        private int _executions;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static AISkillEffectHarness Begin(string directory, bool backlash)
        {
            var race = AISkillRaceHarness.Begin(directory, SoloDifficulty.Normal, false, true);
            var probe = race.gameObject.AddComponent<AISkillEffectHarness>();
            probe._race = race; probe._directory = directory; probe._backlash = backlash; probe.Status = "waiting-go";
            probe._samples = new StreamWriter(Path.Combine(directory, "effect-samples.jsonl"));
            probe._events = new StreamWriter(Path.Combine(directory, "effect-events.jsonl")) { AutoFlush = true };
            probe.StartCoroutine(probe.Run());
            return probe;
        }

        private IEnumerator Run()
        {
            while (_race.Status != "racing")
            {
                if (_race.Status == "operator-error" || _race.Status == "timeout") { Status = "setup-failed"; yield break; }
                yield return null;
            }
            _racers = RacerDirectory.Current.All.OrderBy(r => r.Id.Value).ToArray();
            foreach (var racer in _racers) if (racer.IsAI) racer.GetComponent<AISkillCaster>().enabled = false;
            _keyboard = InputSystem.AddDevice<Keyboard>("AISkillEffectAcceptanceKeyboard");
            yield return new WaitForSeconds(2f);
            // Both sources cover self effects and the human->AI / AI->human / AI->AI target relationships.
            foreach (int sourceId in new[] { 10000, 0 })
            {
                _source = _racers.Single(r => r.Id.Value == sourceId).GetComponent<SkillExecutor>();
                _source.ServerCastChanged += Cast;
                foreach (string skill in AISkillCatalog.SkillIds)
                {
                    _case = (_backlash ? "backlash-" : "normal-") + sourceId + "-" + skill;
                    Status = _case; _executions = 0;
                    PrepareFixture(skill);
                    yield return new WaitForSeconds(.3f);
                    _caseStart = Time.timeAsDouble;
                    bool accepted = _source.CastSlotServer(0);
                    Write("request", "accepted=" + accepted + ";direct-shared-authority;fixture-only");
                    if (accepted) Write("immediate-repeat", "accepted=" + _source.CastSlotServer(0));
                    bool fired = false;
                    float nextSample = Time.time;
                    while (Time.timeAsDouble - _caseStart < 18.5d)
                    {
                        if (Time.time >= nextSample) { nextSample = Time.time + .05f; Sample(); }
                        if (!fired && !_backlash && skill == "push_projectile_hands" && Time.timeAsDouble - _caseStart > 2d)
                        {
                            fired = true;
                            if (sourceId >= RacerId.AIBase) Write("buff-push", "server-key-accepted=" + _source.GetComponent<BuddahHandControl>().InjectServerPush(true));
                            else { InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W)); Write("buff-push", "human-virtual-key-W"); }
                        }
                        else if (fired && sourceId == 0) InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                        yield return null;
                    }
                    Write("case-end", "executions=" + _executions);
                    RestoreObsession(); _samples.Flush();
                }
                _source.ServerCastChanged -= Cast;
            }
            Status = "matrix-complete";
            Write("complete", "12 controlled cases; see samples for actual effect/recovery; no natural race claim");
            _race.Complete("controlled-effect-matrix-complete");
            Cleanup();
        }

        private void PrepareFixture(string skill)
        {
            var loadout = new[] { skill }.Concat(AISkillCatalog.SkillIds.Where(id => id != skill).Take(2)).ToArray();
            _source.GetComponent<SkillLoadout>().SetSlotsServer(loadout);
            var track = TrackSplineRef.Instance;
            if (!track.TryEvaluateWorldPoseAtProgress01(.1f, out var centre, out var forward)) throw new InvalidOperationException("Fixture needs track pose.");
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            int targetIndex = 0;
            foreach (var racer in _racers)
            {
                bool source = racer.Id.Value == _source.GetComponent<RacerIdentity>().Id.Value;
                Vector3 offset = Vector3.zero;
                if (!source)
                {
                    float distance = skill == "slowtrap" && !_backlash ? -4f : skill == "push_projectile_hands" ? (_backlash ? -25f : 25f) : 20f;
                    offset = forward * (distance + targetIndex * 8f) + side * ((targetIndex % 3 - 1) * 3f);
                    targetIndex++;
                }
                racer.GetComponent<SkillExecutor>().ResetActiveSkillEffectsServer();
                racer.GetComponent<BuddahPredictedMotor>().TryApplyServerAuthoritativeTeleport(centre + offset + Vector3.up * 2f,
                    rotation, .1f, BuddahPredictedTeleportSourceType.Manual, "controlled-skill-effect-fixture",
                    true, true, true, true, true, true, true);
            }
            _changedObsession = _source.GetComponent<ObsessionFigure>();
            _oldPmax = (float)typeof(ObsessionFigure).GetField("backfirePMaxPercent", Private).GetValue(_changedObsession);
            _oldKeep = (float)typeof(ObsessionFigure).GetField("backfireKeepPMaxAtOrAboveX", Private).GetValue(_changedObsession);
            typeof(ObsessionFigure).GetField("backfirePMaxPercent", Private).SetValue(_changedObsession, _backlash ? 100f : 0f);
            if (_backlash) typeof(ObsessionFigure).GetField("backfireKeepPMaxAtOrAboveX", Private).SetValue(_changedObsession, 0f);
            Write("fixture", "runtime-only Pmax=" + (_backlash ? 100 : 0) + ";source=" + _source.GetComponent<RacerIdentity>().Id.Value + ";skill=" + skill + ";authority teleports;product assets unchanged");
        }
        private void Cast(SkillExecutor.CastEvent cast)
        {
            if (cast.Stage == SkillExecutor.CastStage.Executed) _executions++;
            Write("cast", cast.Stage + ";slot=" + cast.Slot + ";id=" + cast.SkillId + ";backlash=" + cast.IsBacklash);
        }
        private void Sample()
        {
            var rows = _racers.Select(r => new RacerSample { RacerId = r.Id.Value,
                Stats = r.GetComponent<BuddahPredictedMotor>().CurrentComputedStats, Position = r.transform.position,
                Velocity = r.GetComponent<Rigidbody>().velocity, Scale = r.transform.localScale,
                PerceivedSign = r.GetComponent<AIRacerDriver>().PerceivedSteeringSign,
                VisionUntil = r.GetComponent<SkillPerceptionState>().VisionImpairedUntilTick,
                BuffLeft = r.GetComponent<BuddahHandControl>().ProjectileBuffSecondsLeft,
                HasTrap = r.GetComponent<MovementSlowTrapZoneEffect>() != null }).ToArray();
            _samples.WriteLine(JsonUtility.ToJson(new SampleRow { Case = _case, Elapsed = Time.timeAsDouble - _caseStart,
                Tick = _source.TimeManager.LocalTick, Racers = rows }));
        }
        private void Write(string kind, string detail) => _events?.WriteLine(JsonUtility.ToJson(new EventRow { Case = _case,
            Event = kind, Detail = detail, Tick = _source != null && _source.TimeManager != null ? _source.TimeManager.LocalTick : 0,
            Elapsed = Time.timeAsDouble - _caseStart }));
        private void RestoreObsession()
        {
            if (_changedObsession == null) return;
            typeof(ObsessionFigure).GetField("backfirePMaxPercent", Private).SetValue(_changedObsession, _oldPmax);
            typeof(ObsessionFigure).GetField("backfireKeepPMaxAtOrAboveX", Private).SetValue(_changedObsession, _oldKeep);
            _changedObsession = null;
        }
        private void Cleanup()
        {
            RestoreObsession();
            if (_source != null) _source.ServerCastChanged -= Cast;
            if (_keyboard != null) { InputSystem.RemoveDevice(_keyboard); _keyboard = null; }
            _samples?.Dispose(); _events?.Dispose(); _samples = _events = null;
        }
        private void OnDestroy() => Cleanup();
        [Serializable] private struct RacerSample
        {
            public int RacerId; public BuddahPredictedMotorComputedStats Stats; public Vector3 Position, Velocity, Scale;
            public float PerceivedSign, BuffLeft; public uint VisionUntil; public bool HasTrap;
        }
        [Serializable] private struct SampleRow { public string Case; public double Elapsed; public uint Tick; public RacerSample[] Racers; }
        [Serializable] private struct EventRow { public string Case, Event, Detail; public uint Tick; public double Elapsed; }
    }
}
#endif
