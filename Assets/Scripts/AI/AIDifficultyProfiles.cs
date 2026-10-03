using BuddahGo.Match;
using UnityEngine;

namespace BuddahGo.AI
{
    // Product mapping from the Solo difficulty to the AI driving profile. The three assets live in
    // Resources/AI (Easy, Normal, Hard) and are generated from Tools/ai/difficulty-*.json by the
    // AIDifficultyAssetTool editor command, so the raced profiles and the shipped ones are the same data.
    public static class AIDifficultyProfiles
    {
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
            // Per-racer personality: a seed that differs per match and racer, a speed bias and a line offset.
            int seed = unchecked(System.Environment.TickCount * 31 + racerIndex * 7919 + (int)difficulty * 104729);
            if (seed == 0) seed = 1;
            var random = new System.Random(seed);
            profile.NoiseSeed = seed;
            profile.TargetSpeed = Mathf.Clamp(profile.TargetSpeed + (float)(random.NextDouble() * 2.0 - 1.0) * profile.SpeedNoise, 20f, 80f);
            profile.LateralOffset += (float)(random.NextDouble() * 2.0 - 1.0) * profile.LateralOffsetNoise;
            return profile;
        }
    }
}
