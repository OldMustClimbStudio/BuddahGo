using System;
using BuddahGo.Match;
using FishNet.Managing.Timing;
using SteamMultiplayer.Network;
using UnityEngine;

namespace BuddahGo.AI
{
    // Counters, decision observations and per-frame work accounting for one AI skill caster. Pure bookkeeping:
    // nothing here decides, injects or casts. Harnesses read it and the verbose log mirrors the observations.
    public sealed class AISkillTelemetry
    {
        [Serializable]
        public struct Observation
        {
            public uint Tick;
            public int RacerId, Slot, TargetId;
            public string Personality, Event, Reason, Skill;
            public SoloDifficulty Difficulty;
            public AISkillSituation Situation;
            public float Opportunity, ValidSeconds, Obsession, BacklashProbability, Utility;
            public bool TargetImpaired, Backlash;
        }
        public event Action<Observation> Observed;
        public int Requests { get; internal set; }
        public int Accepted { get; internal set; }
        public int Executed { get; internal set; }
        public int Backlashes { get; internal set; }
        public int Cancelled { get; internal set; }
        public int Keys { get; internal set; }
        public int BuffShots { get; internal set; }

        // AI.Skill work of every caster in the current rendered frame; FishNet may run several ticks per frame.
        public static long WorkTicksThisFrame => _workFrame == Time.frameCount ? _workTicksThisFrame : 0;
        // Portion of WorkTicksThisFrame spent inside AI.Skill.Input (key injection, server pushes, spawns).
        public static long InputTicksThisFrame => _workFrame == Time.frameCount ? _inputTicksThisFrame : 0;
        private static long _workTicksThisFrame, _inputTicksThisFrame;
        private static int _workFrame = -1;

        private int _racerId;
        private AISkillPersonality _personality;
        private AISkillDifficulty _difficulty;
        private ObsessionFigure _obsession;
        private TimeManager _time;

        internal void Bind(int racerId, AISkillPersonality personality, AISkillDifficulty difficulty, ObsessionFigure obsession, TimeManager time)
        {
            _racerId = racerId; _personality = personality; _difficulty = difficulty; _obsession = obsession; _time = time;
        }

        internal static void AccumulateFrame(long workTicks, long inputTicks)
        {
            if (_workFrame != Time.frameCount) { _workFrame = Time.frameCount; _workTicksThisFrame = 0; _inputTicksThisFrame = 0; }
            _workTicksThisFrame += workTicks;
            _inputTicksThisFrame += inputTicks;
        }

        internal void Emit(string kind, string reason, int slot, AISkillSituation situation, int fallbackTarget,
            AISkillOpportunity opportunity = default, float utility = 0f, bool backlash = false)
        {
            if (Observed == null && !NetDebug.EnableVerboseLog) return;
            var observation = new Observation { Tick = _time.LocalTick, RacerId = _racerId, Personality = _personality.Id,
                Difficulty = _difficulty.Difficulty, Event = kind, Reason = reason, Slot = slot, TargetId = opportunity.Score > 0f ? opportunity.TargetId : fallbackTarget,
                Skill = slot >= 0 ? _personality.Loadout[slot] : string.Empty, Situation = situation,
                Opportunity = opportunity.Score, ValidSeconds = opportunity.ValidSeconds, TargetImpaired = opportunity.ImpairedTarget,
                Obsession = _obsession.Current, BacklashProbability = _obsession.CurrentBackfireProbabilityPercent * .01f,
                Utility = float.IsNegativeInfinity(utility) ? -999f : utility, Backlash = backlash };
            Observed?.Invoke(observation);
            if (NetDebug.EnableVerboseLog) GameLog.Verbose("[AI.Skill] " + JsonUtility.ToJson(observation));
        }
    }
}
