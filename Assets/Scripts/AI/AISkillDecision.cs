using System;
using UnityEngine;

namespace BuddahGo.AI
{
    public struct AISkillRacerSnapshot
    {
        public int RacerId;
        public float Progress, TrackDistance, Speed, AlongSpeed, Lateral;
        public Vector3 Position, Forward, Velocity;
        public bool Available, Rooted, Inverted, Suppressed, Accelerating, Slowed, PushProtected, VisionImpaired;
        public float Scale, PreviousSpeed, ImpairedSecondsLeft, InvertSecondsLeft;
        public float StraightFraction, StraightSeconds, CornerFraction, CornerSeconds;
        public float LastCastSeconds;
        public bool IsImpaired(float speedDrop) => Rooted || Inverted || Slowed || Scale < .99f
            || (PreviousSpeed > 5f && Speed < PreviousSpeed * (1f - speedDrop));
    }

    // Fixed-capacity frames are reused only after the maximum reaction window has passed.
    public sealed class AISkillSnapshot
    {
        public readonly AISkillRacerSnapshot[] Racers = new AISkillRacerSnapshot[6];
        public int Count;
        public uint Tick;
        public float TickDelta;
        public float ReverseSeconds, CurtainSeconds;
        public int IndexOf(int racerId)
        {
            for (int i = 0; i < Count; i++) if (Racers[i].RacerId == racerId) return i;
            return -1;
        }
    }

    public readonly struct AISkillOpportunity
    {
        public readonly float Score, ValidSeconds, StateSeconds;
        public readonly int TargetId;
        public readonly bool ImpairedTarget;
        public AISkillOpportunity(float score, float validSeconds, int targetId = -1, bool impaired = false, float stateSeconds = 0f)
        { Score = Mathf.Clamp01(score); ValidSeconds = Mathf.Max(0f, validSeconds); TargetId = targetId; ImpairedTarget = impaired; StateSeconds = stateSeconds; }
    }

    public static class AISkillDecision
    {
        public static AISkillSituation Situation(AISkillSnapshot frame, int selfIndex, AISkillTuning tuning)
        {
            ref var self = ref frame.Racers[selfIndex];
            int nearby = 0; float ahead = float.PositiveInfinity, behind = float.PositiveInfinity;
            bool pursued = false, anyoneAhead = false;
            for (int i = 0; i < frame.Count; i++)
            {
                if (i == selfIndex || !frame.Racers[i].Available) continue;
                ref var other = ref frame.Racers[i];
                float gap = other.Progress - self.Progress;
                if (Mathf.Abs(gap) <= tuning.NearDistance) nearby++;
                if (gap > 0f) { ahead = Mathf.Min(ahead, gap); anyoneAhead = true; }
                else if (-gap < behind) { behind = -gap; pursued = other.AlongSpeed > self.AlongSpeed; }
            }
            if (nearby >= 2) return AISkillSituation.Pack;
            if (behind <= tuning.NearDistance && pursued && ahead > tuning.FarDistance) return AISkillSituation.Pursued;
            if (ahead <= tuning.FarDistance) return AISkillSituation.Chasing;
            return anyoneAhead ? AISkillSituation.TrailingAlone : AISkillSituation.LeadingAlone;
        }

        public static AISkillOpportunity Evaluate(AISkillKind kind, AISkillSnapshot frame, int selfIndex,
            AISkillTuning tuning, AITargetPreference preference, float handYaw, float rotationSpeed)
        {
            ref var self = ref frame.Racers[selfIndex];
            if (!self.Available || self.Rooted) return default;
            if (kind == AISkillKind.Reverse && frame.ReverseSeconds > 0f) return default;
            if (kind == AISkillKind.Curtain && frame.CurtainSeconds > 0f) return default;
            if (kind == AISkillKind.Acceleration)
            {
                if (self.Accelerating || self.StraightSeconds < 3f) return default;
                int target = SelectStateWindow(frame, selfIndex, tuning, preference, out float stateSeconds);
                float validity = self.StraightSeconds;
                return new AISkillOpportunity(self.StraightFraction, validity, target, stateSeconds > 0f, stateSeconds);
            }

            AISkillOpportunity best = default;
            float bestPreference = float.NegativeInfinity, globalScore = 0f, contactValidity = 0f;
            int contacts = 0;
            for (int i = 0; i < frame.Count; i++)
            {
                if (i == selfIndex) continue;
                ref var other = ref frame.Racers[i];
                if (!other.Available) continue;
                float gap = other.Progress - self.Progress;
                float relative = other.AlongSpeed - self.AlongSpeed;
                float score = 0f, validity = 0f;
                switch (kind)
                {
                    case AISkillKind.SlowTrap:
                        if (gap >= 0f || -gap > tuning.TrapDistance || relative <= .1f || Mathf.Abs(self.Lateral - other.Lateral) > tuning.LaneWidth) continue;
                        score = .3f + .7f * (1f + gap / tuning.TrapDistance);
                        validity = Mathf.Min(6f, -gap / relative);
                        break;
                    case AISkillKind.Giant:
                        if (Mathf.Abs(gap) > tuning.ContactDistance || Vector3.Distance(self.Position, other.Position) > tuning.ContactDistance * 1.5f) continue;
                        validity = WindowSeconds(gap, relative, -tuning.ContactDistance, tuning.ContactDistance);
                        if (validity < 2f) continue;
                        contacts++;
                        contactValidity = Mathf.Max(contactValidity, validity);
                        score = .5f;
                        break;
                    case AISkillKind.Hands:
                        if (gap <= 0f || gap > tuning.HandsDistance || other.PushProtected) continue;
                        Vector3 direction = other.Position - self.Position; direction.y = 0f;
                        if (direction.sqrMagnitude > tuning.HandsDistance * tuning.HandsDistance * 2.25f) continue;
                        float yaw = Vector3.SignedAngle(self.Forward, direction, Vector3.up);
                        if (Mathf.Abs(yaw) > 110f || Mathf.Abs(Mathf.DeltaAngle(handYaw, yaw)) / Mathf.Max(1f, rotationSpeed) > tuning.AimHorizonSeconds) continue;
                        score = (.35f + .65f * (1f - gap / tuning.HandsDistance)) * (1f - .5f * Mathf.Abs(yaw) / 180f);
                        validity = WindowSeconds(gap, relative, 0f, tuning.HandsDistance);
                        break;
                    case AISkillKind.Reverse:
                    case AISkillKind.Curtain:
                        float distanceWeight = 1f / (1f + Mathf.Abs(gap) / tuning.FarDistance);
                        score = other.CornerFraction * distanceWeight;
                        if (kind == AISkillKind.Curtain && Mathf.Abs(gap) < tuning.NearDistance) score += .25f;
                        validity = Mathf.Max(2f, other.CornerSeconds);
                        globalScore += score;
                        break;
                }
                if (score <= 0f) continue;
                float targetPreference = TargetScore(frame, i, selfIndex, preference, tuning);
                if (targetPreference <= bestPreference) continue;
                bestPreference = targetPreference;
                bool impaired = other.IsImpaired(tuning.SpeedDropFraction) && other.ImpairedSecondsLeft > 0f;
                best = new AISkillOpportunity(score, validity, other.RacerId, impaired, other.ImpairedSecondsLeft);
            }
            if (kind == AISkillKind.Giant && contacts > 0)
                return new AISkillOpportunity(Mathf.Min(1f, contacts * .5f), contactValidity, best.TargetId, best.ImpairedTarget);
            if (kind == AISkillKind.Reverse)
                return new AISkillOpportunity(globalScore * self.StraightFraction, best.ValidSeconds, best.TargetId, best.ImpairedTarget);
            if (kind == AISkillKind.Curtain)
                return new AISkillOpportunity(globalScore, best.ValidSeconds, best.TargetId, best.ImpairedTarget);
            return best;
        }

        public static float Utility(AISkillKind kind, AISkillOpportunity opportunity, AISkillSituation situation,
            AISkillPersonality personality, AISkillDifficulty difficulty, AISkillTuning tuning, float backlashProbability,
            float requiredSeconds, float opportunityAge, float noise = 1f)
        {
            if (opportunity.Score <= 0f || opportunity.ValidSeconds - opportunityAge < requiredSeconds) return float.NegativeInfinity;
            float severity = tuning.BacklashSeverity[(int)kind];
            if (backlashProbability >= (severity >= .5f ? difficulty.SevereStopProbability : difficulty.MildStopProbability))
                return float.NegativeInfinity;
            float value = personality.Weights[(int)kind] * opportunity.Score * personality.SituationBias[(int)situation] * noise;
            if ((kind == AISkillKind.Acceleration || kind == AISkillKind.Hands) && opportunity.ImpairedTarget
                && opportunity.StateSeconds - opportunityAge >= requiredSeconds)
                value *= personality.ImpairedTargetMultiplier;
            return value - difficulty.RiskWeight * backlashProbability * severity;
        }

        public static float CastProbability(float utility, float patience, float elapsed, float tempo)
            => 1f - Mathf.Exp(-Mathf.Max(0f, utility - patience) * Mathf.Max(0f, elapsed) / Mathf.Max(.01f, tempo));

        private static float WindowSeconds(float gap, float relative, float min, float max)
            => Mathf.Clamp(Mathf.Abs(relative) < .1f ? 6f : (relative > 0 ? max - gap : gap - min) / Mathf.Abs(relative), 0f, 6f);

        private static int SelectStateWindow(AISkillSnapshot frame, int selfIndex, AISkillTuning tuning,
            AITargetPreference preference, out float seconds)
        {
            int target = -1; seconds = 0f; float best = float.NegativeInfinity;
            for (int i = 0; i < frame.Count; i++)
            {
                if (i == selfIndex || !frame.Racers[i].Available) continue;
                ref var other = ref frame.Racers[i];
                float gap = other.Progress - frame.Racers[selfIndex].Progress;
                if (gap > tuning.FarDistance || gap < -tuning.NearDistance) continue;
                float score = TargetScore(frame, i, selfIndex, preference, tuning);
                if (score <= best) continue;
                best = score; target = other.RacerId;
                seconds = other.IsImpaired(tuning.SpeedDropFraction) ? other.ImpairedSecondsLeft : 0f;
            }
            return target;
        }

        private static float TargetScore(AISkillSnapshot frame, int index, int selfIndex, AITargetPreference preference, AISkillTuning tuning)
        {
            ref var target = ref frame.Racers[index];
            float gap = target.Progress - frame.Racers[selfIndex].Progress;
            float score = 1f / (1f + Mathf.Abs(gap));
            if (preference == AITargetPreference.Impaired && target.IsImpaired(tuning.SpeedDropFraction)) score += 2f;
            if (preference == AITargetPreference.Ahead && gap > 0) score += 1f;
            if (preference == AITargetPreference.Behind && gap < 0) score += 1f;
            if (preference == AITargetPreference.EnteringCorner) score += target.CornerFraction;
            if (preference == AITargetPreference.PackCentre)
                for (int i = 0; i < frame.Count; i++)
                    if (i != index && frame.Racers[i].Available && Mathf.Abs(frame.Racers[i].Progress - target.Progress) < tuning.NearDistance) score += .25f;
            return score;
        }
    }
}
