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
        // 建议（实现采用）: scales the 缘起 setup / follow-up value (AISkillCaster.Decide); 0 = no 缘起 decisions.
        public float YuanqiAffinity = 1f;
    }

    [Serializable]
    public sealed class AISkillDifficulty
    {
        public SoloDifficulty Difficulty;
        public float RiskWeight;
        // One stop threshold for every skill (A7, 建议（实现采用）): the backfire penalty is the same whichever skill
        // backfires, so a backfire probability at or above this refuses every cast (AISkillDecision.Utility).
        public float StopProbability = 1.01f;
        public float DecisionSeconds;
        public float ReactionMinSeconds;
        public float ReactionMaxSeconds;
        public float AimToleranceDegrees;
        public float AdaptSeconds;
        public float ReadaptSeconds;
        public float ImpairedLookaheadMultiplier;
        public float ImpairedMistakeAddition;
        public int ImpairedReactionTicks;            // extra skill-cast reaction while vision is impaired (AISkillCaster)
        public int ImpairedDrivingReactionTicks;     // extra driving reaction while vision is impaired (AIRacerDriver)
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
        public float AimHorizonSeconds = 2f;
        public float SpeedDropFraction = .2f;
        // Unified backfire (A7, 建议（实现采用）): the risk term of AISkillDecision.Utility is
        // RiskWeight × P(backfire) × BackfireCost for every skill (Obsession.backfireLockSeconds lock, 5 s since 2026-10-31; ×0.85 max speed; ×0.8 obsession at the end; plus the skill's own _anti variant).
        public float BackfireCost = 1f;
        // 缘起 decision parameters (A7, 建议（实现采用）; AIYuanqiPlanner + AISkillCaster.Decide).
        public float YuanqiSetupMultiplier = 1.2f;        // value multiplier of the first skill of a pair whose second can follow in time
        public float YuanqiFollowUpScore = .8f;          // minimum opportunity score of a follow-up candidate
        public float YuanqiFollowUpMultiplier = 1.5f;    // value multiplier of a follow-up candidate
        public float YuanqiFollowUpTempoSeconds = .3f;   // sampling tempo of a follow-up (patience 0, so the window is not missed)
        public float YuanqiLateWindowSeconds = .6f;      // a consumed first skill is followed up only within confirm + this before it ends
        // Indexed by AISkillKind: do not stack while the same global effect is visible (Reverse, Curtain).
        public bool[] BlockWhenGlobalVisible = { false, false, false, false, true, true };
        // Opportunity predicate constants (AI/SkillOpportunity); defaults equal the former literals.
        public float AccelerationMinStraightSeconds = 3f;
        public float TrapScoreBase = .3f, TrapScoreSlope = .7f, TrapMaxValiditySeconds = 6f;
        public float GiantEuclidMultiplier = 1.5f, GiantMinValiditySeconds = 2f, GiantScorePerContact = .5f;
        public float HandsEuclidMultiplier = 1.5f, HandsMaxYawDegrees = 110f, HandsScoreBase = .35f, HandsScoreSlope = .65f, HandsYawPenalty = .5f;
        public float ReverseMinValiditySeconds = 2f, CurtainMinValiditySeconds = 2f, CurtainPackBonus = .25f;
        // Snapshot / scoring constants (AI/SkillGlobal); defaults equal the former literals.
        public float SpeedDropImpairSeconds = .5f, SpeedDropMinSpeed = 5f;
        public float StraightLookaheadSeconds = 10f, CornerLookaheadSeconds = 2f, CornerValidityBase = 2f, CornerValidityScale = 2f;
        public float TargetBonusImpaired = 2f, TargetBonusAhead = 1f, TargetBonusBehind = 1f, TargetBonusPackCentrePerNeighbour = .25f;
        public int PackMinNeighbours = 2;
        public float MaxWindowSeconds = 6f, MinRelativeSpeed = .1f, MaxElapsedSeconds = 1f;
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
            catalog.ApplyDesignTables();
            if (!ProjectConfigRuntime.TryGetSelectionRuleRepository(out var rules))
                throw new InvalidOperationException("AI skill selection rules are unavailable.");
            var pool = rules.GetSkillIdsForPool(ProjectConfigConstants.DefaultRuleSetId,
                ProjectConfigConstants.DefaultModeTag, string.Empty, SkillSelectionPoolType.Selectable);
            catalog.Validate(pool.Contains);
            return catalog;
        }

        // AI/SkillDifficulty, AI/Personality (+SituationBias), AI/SkillTuning and AI/SkillOpportunity overlay the JSON
        // values; the JSON stays the fallback when a table has no rows. Sheet headers are camelCase and match the
        // PascalCase fields case-insensitively (DesignTableOverlay.FindField).
        private void ApplyDesignTables()
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null) return;
            if (Difficulties != null && db.skillDifficulties != null)
                foreach (var d in Difficulties)
                {
                    string tier = d.Difficulty.ToString();
                    DesignTableOverlay.Apply(d, DesignTableOverlay.Single(db.skillDifficulties, r => r.difficulty == tier));
                }
            if (Personalities != null && db.personalities != null)
                foreach (var p in Personalities)
                {
                    PersonalityRecord row = DesignTableOverlay.Single(db.personalities, r => r.personalityId == p.Id);
                    if (row == null) continue;
                    p.DisplayName = DesignTableOverlay.Text(row.displayNameKey, p.DisplayName);
                    p.Loadout = new[] { row.slot0SkillId, row.slot1SkillId, row.slot2SkillId };
                    p.Patience = row.patience; p.TempoSeconds = row.tempoSeconds; p.HandsShots = row.handsShots;
                    p.ImpairedTargetMultiplier = row.impairedTargetMultiplier;
                    p.YuanqiAffinity = row.yuanqiAffinity;
                    if (Enum.TryParse(row.target, true, out AITargetPreference target)) p.Target = target;
                    // Weights are indexed by AISkillKind; only loadout skills are ever cast, so the three slot weights fill the array.
                    p.Weights = new float[SkillIds.Length];
                    float[] slotWeights = { row.slot0Weight, row.slot1Weight, row.slot2Weight };
                    for (int i = 0; i < p.Loadout.Length && i < slotWeights.Length; i++)
                    {
                        int ki = Array.IndexOf(SkillIds, p.Loadout[i]);
                        if (ki >= 0) p.Weights[ki] = slotWeights[i];
                    }
                    if (db.personalitySituationBiases != null)
                        foreach (var b in db.personalitySituationBiases)
                            if (b != null && b.personalityId == p.Id && Enum.TryParse(b.situation, true, out AISkillSituation si) && p.SituationBias != null && (int)si < p.SituationBias.Length)
                                p.SituationBias[(int)si] = b.bias;
                }
            Tuning ??= new AISkillTuning();
            SkillTuningRecord tuning = DesignTableOverlay.DefaultRow(db.skillTunings, r => r.profileId);
            if (tuning != null)
                DesignTableOverlay.Apply(Tuning, tuning);
            if (db.skillOpportunities != null)
                foreach (var o in db.skillOpportunities)
                {
                    if (o == null) continue;
                    int k = Array.IndexOf(SkillIds, o.skillId);
                    if (k < 0) continue;
                    if (Tuning.BlockWhenGlobalVisible != null && k < Tuning.BlockWhenGlobalVisible.Length) Tuning.BlockWhenGlobalVisible[k] = o.blockWhenGlobalVisible;
                    // Each skill reads only its own columns, and those cells are the values (0 included); the
                    // columns a skill does not use stay blank in the sheet and are never read for it.
                    switch ((AISkillKind)k)
                    {
                        case AISkillKind.Acceleration: Tuning.AccelerationMinStraightSeconds = o.minStraightSeconds; break;
                        case AISkillKind.SlowTrap: Tuning.TrapScoreBase = o.scoreBase; Tuning.TrapScoreSlope = o.scoreSlope; Tuning.TrapMaxValiditySeconds = o.maxValiditySeconds; break;
                        case AISkillKind.Giant: Tuning.GiantEuclidMultiplier = o.euclidMultiplier; Tuning.GiantMinValiditySeconds = o.minValiditySeconds; Tuning.GiantScorePerContact = o.scorePerContact; break;
                        case AISkillKind.Hands: Tuning.HandsEuclidMultiplier = o.euclidMultiplier; Tuning.HandsMaxYawDegrees = o.maxYawDegrees; Tuning.HandsScoreBase = o.scoreBase; Tuning.HandsScoreSlope = o.scoreSlope; Tuning.HandsYawPenalty = o.yawPenalty; break;
                        case AISkillKind.Reverse: Tuning.ReverseMinValiditySeconds = o.minValiditySeconds; break;
                        case AISkillKind.Curtain: Tuning.CurtainMinValiditySeconds = o.minValiditySeconds; Tuning.CurtainPackBonus = o.packBonus; break;
                    }
                }
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
                Range(p.ImpairedTargetMultiplier, 1f, 2f); Range(p.YuanqiAffinity, 0f, 1.5f);
                if (!Enum.IsDefined(typeof(AITargetPreference), p.Target) || p.HandsShots < 0 || p.HandsShots > 6)
                    throw new ArgumentException("Invalid target preference or hand shot limit.");
            }
            var tiers = new System.Collections.Generic.HashSet<SoloDifficulty>();
            foreach (var d in Difficulties)
            {
                if (d == null || !tiers.Add(d.Difficulty) || !Enum.IsDefined(typeof(SoloDifficulty), d.Difficulty))
                    throw new ArgumentException("Duplicate or missing AI skill difficulty.");
                Range(d.RiskWeight, 0f, 2f); Range(d.StopProbability, 0f, 1.01f);
                Range(d.DecisionSeconds, .1f, 2f); Range(d.ReactionMinSeconds, 0f, 1f); Range(d.ReactionMaxSeconds, d.ReactionMinSeconds, 1.5f);
                Range(d.AimToleranceDegrees, 1f, 45f);
                Range(d.AdaptSeconds, 0f, 5f); Range(d.ReadaptSeconds, 0f, 5f);
                Range(d.ImpairedLookaheadMultiplier, .1f, 1f); Range(d.ImpairedMistakeAddition, 0f, 1f);
                Range(d.DecisionNoise, 0f, .5f);
                if (d.ImpairedReactionTicks < 0 || d.ImpairedReactionTicks > 30) throw new ArgumentException("Invalid impairment delay.");
                if (d.ImpairedDrivingReactionTicks < 0 || d.ImpairedDrivingReactionTicks > 30) throw new ArgumentException("Invalid driving impairment delay.");
            }
            Range(Tuning.SnapshotSeconds, .05f, .5f);
            Range(Tuning.SituationHysteresisSeconds, 0f, 5f); Range(Tuning.NearDistance, 1f, 500f); Range(Tuning.FarDistance, Tuning.NearDistance, 2000f);
            Range(Tuning.TrapDistance, 1f, 500f); Range(Tuning.ContactDistance, 1f, 100f); Range(Tuning.HandsDistance, 1f, 1800f);
            Range(Tuning.LaneWidth, 1f, 100f); Range(Tuning.StraightCurvature, .00001f, .1f); Range(Tuning.CornerCurvature, Tuning.StraightCurvature, 1f);
            Range(Tuning.AimHorizonSeconds, .1f, 5f); Range(Tuning.SpeedDropFraction, .01f, 1f);
            Range(Tuning.BackfireCost, 0f, 3f);
            Range(Tuning.YuanqiSetupMultiplier, 0f, 3f); Range(Tuning.YuanqiFollowUpScore, 0f, 1f); Range(Tuning.YuanqiFollowUpMultiplier, 0f, 3f);
            Range(Tuning.YuanqiFollowUpTempoSeconds, .05f, 4f); Range(Tuning.YuanqiLateWindowSeconds, 0f, 5f);
        }

        private static void Range(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentException("AI skill configuration outside supported range.");
        }
    }
}
