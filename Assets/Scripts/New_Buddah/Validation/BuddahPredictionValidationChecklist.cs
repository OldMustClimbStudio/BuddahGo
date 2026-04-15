namespace NewBuddah.PredictionV2.Validation
{
    public static class BuddahPredictionValidationChecklist
    {
        public static string BuildFocusSummary()
        {
            return "Host: movement/modifiers/combat/respawn/handoff | Client: correction/camera/presentation | HighLatency: melee/projectile/respawn";
        }

        public static string BuildMovementChecklist()
        {
            return "locomotion, gate block, modifiers, push grace, respawn, wrong-way, handoff";
        }

        public static string BuildCombatChecklist()
        {
            return "melee push, projectile, charged projectile, impulse route, host double-hit, client correction";
        }

        public static string BuildPresentationChecklist()
        {
            return "giant/shrink, camera compensation, spectator, result, timeline, external control source";
        }

        public static string BuildLegacyChecklist()
        {
            return "legacy locomotion, skills, respawn, intro, camera, result";
        }
    }
}
