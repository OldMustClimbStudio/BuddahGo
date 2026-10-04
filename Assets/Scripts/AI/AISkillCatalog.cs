using System;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using UnityEngine;

namespace BuddahGo.AI
{
    public enum AISkillKind { Acceleration, SlowTrap, Giant, Hands, Reverse, Curtain }
    public enum AISkillSituation { Pursued, LeadingAlone, Chasing, Pack, TrailingAlone }
    public enum AITargetPreference { Ahead, Behind, PackCentre, EnteringCorner, Impaired }

    [Serializable]
    public sealed class AISkillPersonality
    {
        public string Id;
        public string DisplayName;
        public string[] Loadout;
        // Indexed by AISkillKind and AISkillSituation, independent of driving difficulty.
        public float[] Weights;
        public float[] SituationBias;
        public float Patience;
        public float TempoSeconds;
        public AITargetPreference Target;
        public float ImpairedTargetMultiplier = 1f;
        public int HandsShots;
    }

    [Serializable]
    public sealed class AISkillDifficulty
    {
        public SoloDifficulty Difficulty;
        public float RiskWeight;
        public float SevereStopProbability = 1.01f;
        public float MildStopProbability = 1.01f;
        public float DecisionSeconds;
        public float ReactionMinSeconds;
        public float ReactionMaxSeconds;
        public float KeyMistakeProbability;
        public float AimToleranceDegrees;
        public float AdaptSeconds;
        public float ReadaptSeconds;
        public float ImpairedLookaheadMultiplier;
        public float ImpairedMistakeAddition;
        public int ImpairedReactionTicks;
        public float DecisionNoise = .05f;
    }

    [Serializable]
    public sealed class AISkillTuning
    {
        public float SnapshotSeconds = .15f;
        public float SituationHysteresisSeconds = 1.5f;
        public float NearDistance = 60f;
        public float FarDistance = 300f;
        public float TrapDistance = 65f;
        public float ContactDistance = 35f;
        public float HandsDistance = 300f;
        public float LaneWidth = 14f;
        public float StraightCurvature = .0035f;
        public float CornerCurvature = .006f;
        public float KeyMinSeconds = .2f;
        public float KeyMaxSeconds = .3f;
        public float AimHorizonSeconds = 2f;
        public float SpeedDropFraction = .2f;
        public float[] BacklashSeverity = { 1f, .1f, .5f, .3f, .9f, .4f };
    }

    [Serializable]
    public sealed class AISkillCatalog
    {
        public AISkillPersonality[] Personalities;
        public AISkillDifficulty[] Difficulties;
        public AISkillTuning Tuning;
        private static AISkillCatalog _current;
        public static AISkillCatalog Current => _current ??= Load();
        public static readonly string[] SkillIds =
            { "acceleration", "slowtrap", "giant", "push_projectile_hands", "reverseturn", "blackcurtain" };

        private static AISkillCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("AI/SkillPersonalities");
            if (asset == null) throw new InvalidOperationException("Missing AI/SkillPersonalities configuration.");
            var catalog = JsonUtility.FromJson<AISkillCatalog>(asset.text);
            if (!ProjectConfigRuntime.TryGetSelectionRuleRepository(out var rules))
                throw new InvalidOperationException("AI skill selection rules are unavailable.");
            var pool = rules.GetSkillIdsForPool(ProjectConfigConstants.DefaultRuleSetId,
                ProjectConfigConstants.DefaultModeTag, string.Empty, SkillSelectionPoolType.Selectable);
            catalog.Validate(pool.Contains);
            return catalog;
        }

        public AISkillDifficulty ForDifficulty(SoloDifficulty difficulty)
        {
            foreach (var profile in Difficulties) if (profile.Difficulty == difficulty) return profile;
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }

        public static AISkillKind Kind(string id)
        {
            int index = Array.IndexOf(SkillIds, id);
            if (index < 0) throw new ArgumentException("Unknown AI skill: " + id);
            return (AISkillKind)index;
        }

        public void Validate(Func<string, bool> selectable)
        {
            if (Personalities == null || Personalities.Length != RacerId.MaxAI || Difficulties == null || Difficulties.Length != 3 || Tuning == null)
                throw new ArgumentException("AI skills require five personalities, three difficulties and opportunity tuning.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var p in Personalities)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)
                    || p.Loadout == null || p.Loadout.Length != SkillLoadout.SlotCount
                    || p.Weights == null || p.Weights.Length != 6 || p.SituationBias == null || p.SituationBias.Length != 5)
                    throw new ArgumentException("Invalid skill personality identity or array sizes.");
                if (!LoadoutRules.ValidateSubmission(p.Loadout, 3, selectable, () => false, out var error)
                    || !LoadoutRules.HasCompleteSelection(p.Loadout, selectable, () => false))
                    throw new ArgumentException("Invalid personality loadout " + p.Id + ": " + error);
                foreach (string skill in p.Loadout) Kind(skill);
                foreach (float value in p.Weights) Range(value, 0f, 1.5f);
                foreach (float value in p.SituationBias) Range(value, 0f, 1.5f);
                Range(p.Patience, 0f, .6f); Range(p.TempoSeconds, .5f, 4f);
                Range(p.ImpairedTargetMultiplier, 1f, 2f);
                if (!Enum.IsDefined(typeof(AITargetPreference), p.Target) || p.HandsShots < 0 || p.HandsShots > 6)
                    throw new ArgumentException("Invalid target preference or hand shot limit.");
            }
            var tiers = new System.Collections.Generic.HashSet<SoloDifficulty>();
            foreach (var d in Difficulties)
            {
                if (d == null || !tiers.Add(d.Difficulty) || !Enum.IsDefined(typeof(SoloDifficulty), d.Difficulty))
                    throw new ArgumentException("Duplicate or missing AI skill difficulty.");
                Range(d.RiskWeight, 0f, 2f); Range(d.SevereStopProbability, 0f, 1.01f); Range(d.MildStopProbability, 0f, 1.01f);
                Range(d.DecisionSeconds, .1f, 2f); Range(d.ReactionMinSeconds, 0f, 1f); Range(d.ReactionMaxSeconds, d.ReactionMinSeconds, 1.5f);
                Range(d.KeyMistakeProbability, 0f, 1f); Range(d.AimToleranceDegrees, 1f, 45f);
                Range(d.AdaptSeconds, 0f, 5f); Range(d.ReadaptSeconds, 0f, 5f);
                Range(d.ImpairedLookaheadMultiplier, .1f, 1f); Range(d.ImpairedMistakeAddition, 0f, 1f);
                Range(d.DecisionNoise, 0f, .5f);
                if (d.ImpairedReactionTicks < 0 || d.ImpairedReactionTicks > 30) throw new ArgumentException("Invalid impairment delay.");
            }
            Range(Tuning.SnapshotSeconds, .05f, .5f); Range(Tuning.KeyMinSeconds, .05f, .3f); Range(Tuning.KeyMaxSeconds, Tuning.KeyMinSeconds, .34f);
            Range(Tuning.SituationHysteresisSeconds, 0f, 5f); Range(Tuning.NearDistance, 1f, 500f); Range(Tuning.FarDistance, Tuning.NearDistance, 2000f);
            Range(Tuning.TrapDistance, 1f, 500f); Range(Tuning.ContactDistance, 1f, 100f); Range(Tuning.HandsDistance, 1f, 1800f);
            Range(Tuning.LaneWidth, 1f, 100f); Range(Tuning.StraightCurvature, .00001f, .1f); Range(Tuning.CornerCurvature, Tuning.StraightCurvature, 1f);
            Range(Tuning.AimHorizonSeconds, .1f, 5f); Range(Tuning.SpeedDropFraction, .01f, 1f);
            if (Tuning.BacklashSeverity == null || Tuning.BacklashSeverity.Length != 6) throw new ArgumentException("Missing backlash severities.");
            foreach (float value in Tuning.BacklashSeverity) Range(value, 0f, 1f);
        }

        private static void Range(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentException("AI skill configuration outside supported range.");
        }
    }
}
