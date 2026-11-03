using System.Collections.Generic;

namespace BuddahGo.VFX
{
    // Reads the visual-only numbers of the VFX table (Skill.xlsx / VfxRegistry, Docs/vfx/README.md section 5).
    // Gameplay numbers (charge time, lifetime, collider) never come from here: callers pass them from the skill's
    // own tables.
    public static class SkillVfxTable
    {
        public static bool TryGetRecord(string vfxId, out VfxRegistryRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(vfxId) || !ProjectConfigRuntime.EnsureInitialized())
                return false;
            List<VfxRegistryRecord> rows = ProjectConfigRuntime.Database.vfxRegistries;
            if (rows == null)
                return false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && rows[i].vfxId == vfxId)
                {
                    record = rows[i];
                    return true;
                }
            }
            return false;
        }

        public static SkillVfxVisualValues Resolve(string vfxId)
        {
            return TryGetRecord(vfxId, out VfxRegistryRecord record) ? FromRecord(record) : SkillVfxVisualValues.Defaults;
        }

        internal static SkillVfxVisualValues FromRecord(VfxRegistryRecord record)
        {
            SkillVfxVisualValues d = SkillVfxVisualValues.Defaults;
            return new SkillVfxVisualValues
            {
                chgRatio = record.chgRatio > 0f ? record.chgRatio : d.chgRatio,
                impFrames = record.impFrames > 0 ? record.impFrames : d.impFrames,
                disSeconds = record.disSeconds > 0f ? record.disSeconds : d.disSeconds,
                linSeconds = record.linSeconds,
                bufFadeSeconds = record.bufFadeSeconds,
                trailSeconds = record.trailSeconds > 0f ? record.trailSeconds : d.trailSeconds,
                scaleMultiplier = record.scaleMultiplier > 0f ? record.scaleMultiplier : d.scaleMultiplier,
                lightSeconds = record.lightSeconds,
                lightIntensity = record.lightIntensity,
            };
        }
    }
}
