using BuddahGo.Match;
using UnityEngine;

namespace BuddahGo.AI
{
    // Product mapping from the Solo difficulty to the AI driving profile. The three assets live in
    // Resources/AI (Easy, Normal, Hard) and are generated from Tools/ai/difficulty-*.json by the
    // AIDifficultyAssetTool editor command, so the raced profiles and the shipped ones are the same data.
    public static class AIDifficultyProfiles
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit acceptance-run seed; never consulted in the ordinary release product.
        public static int? DiagnosticMatchSeed;
#endif
        public const string ResourcePath = "AI/";

        public static AIDifficultyProfile Load(SoloDifficulty difficulty)
            => Resources.Load<AIDifficultyProfile>(ResourcePath + difficulty);

        // Each racer gets its own instance (the driver owns and destroys it) with a small lateral-offset
        // variation so that five AI with one profile do not run nose to tail on the same line.
        public static AIDifficultyProfile Resolve(SoloDifficulty difficulty, int racerIndex)
        {
            var source = Load(difficulty);
            if (source == null)
            {
                Debug.LogError($"[AI] No Resources/{ResourcePath}{difficulty} profile asset; AI will drive a blank default profile.");
                return ScriptableObject.CreateInstance<AIDifficultyProfile>();
            }
            var profile = Object.Instantiate(source);
            profile.name = source.name + " #" + racerIndex;
            ApplyDesignTables(profile, difficulty, out float speedMin, out float speedMax);
            // Per-racer personality: a seed that differs per match and racer, a speed bias and a line offset.
            int seed = unchecked(System.Environment.TickCount * 31 + racerIndex * 7919 + (int)difficulty * 104729);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DiagnosticMatchSeed.HasValue) seed = unchecked(DiagnosticMatchSeed.Value * 31 + racerIndex * 7919 + (int)difficulty * 104729);
#endif
            if (seed == 0) seed = 1;
            var random = new System.Random(seed);
            profile.NoiseSeed = seed;
            profile.TargetSpeed = Mathf.Clamp(profile.TargetSpeed + (float)(random.NextDouble() * 2.0 - 1.0) * profile.SpeedNoise, speedMin, speedMax);
            profile.LateralOffset += (float)(random.NextDouble() * 2.0 - 1.0) * profile.LateralOffsetNoise;
            return profile;
        }

        // AI/DrivingTier overlays the asset values (camelCase headers match the PascalCase fields case-insensitively);
        // AI/DrivingTuning feeds the speed clamp and the planner recovery thresholds. Without table rows the asset
        // and the literals below stay in effect.
        private static void ApplyDesignTables(AIDifficultyProfile profile, SoloDifficulty difficulty, out float speedMin, out float speedMax)
        {
            speedMin = 20f; speedMax = 80f;
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null) return;
            string tier = difficulty.ToString();
            DrivingTierRecord row = DesignTableOverlay.Single(db.drivingTiers, r => r.difficulty == tier);
            if (row != null)
            {
                DesignTableOverlay.Apply(profile, row);
                switch (row.rolloutTail) { case "Anchor": profile.RolloutTail = 0; break; case "KeepCandidate": profile.RolloutTail = 1; break; case "FullThrust": profile.RolloutTail = 2; break; }
            }
            DrivingTuningRecord tuning = DesignTableOverlay.DefaultRow(db.drivingTunings, r => r.profileId);
            if (tuning == null) return;
            speedMin = tuning.targetSpeedMin;
            speedMax = tuning.targetSpeedMax;
            ThrustVectorPlanner.RecoverySpeed = tuning.recoverySpeed;
            ThrustVectorPlanner.RecoveryVelocityErrorDegrees = tuning.recoveryVelocityErrorDegrees;
        }
    }
}
