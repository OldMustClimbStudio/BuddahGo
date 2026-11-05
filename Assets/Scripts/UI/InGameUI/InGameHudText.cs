using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// One HUD label: a UI/Text key (optional table override), the preferred text and an ASCII fallback used when the
/// font chain cannot render the preferred text (HudGlyphCoverage).
/// </summary>
internal readonly struct HudLabel
{
    public HudLabel(string key, string preferred, string ascii)
    {
        Key = key;
        Preferred = preferred;
        Ascii = ascii;
    }

    public string Key { get; }
    public string Preferred { get; }
    public string Ascii { get; }
}

/// <summary>
/// Default HUD labels (A6). Chinese only where the BuddahGoSoloCJK subset has every glyph (checked by
/// InGameHudTests); 冻 (冻结) is not in the subset today, so it renders its ASCII text until the subset is
/// re-baked. The keys are optional UI/Text rows: without a row the preferred text is used.
/// </summary>
internal static class InGameHudLabels
{
    internal static readonly HudLabel ObsessionValue = new HudLabel("hud.obsession.value", "执念", "Obsession");
    internal static readonly HudLabel BackfireChance = new HudLabel("hud.obsession.backfire_chance", "反噬", "Backfire");
    internal static readonly HudLabel BackfirePenalty = new HudLabel("hud.obsession.backfire_penalty", "反噬中", "Backfire");
    internal static readonly HudLabel SlotBackfireLock = new HudLabel("hud.skill.backfire_lock", "反噬", "LOCK");
    internal static readonly HudLabel SlotFrozen = new HudLabel("hud.skill.frozen", "冻结", "FROZEN");
    // Prefix of "x1.15 4.2s": the recast's obsession gain multiplier inside the same-skill surcharge window.
    internal static readonly HudLabel SlotSurcharge = new HudLabel("hud.skill.surcharge", "执念", string.Empty);

    internal static readonly HudLabel[] All =
    {
        ObsessionValue, BackfireChance, BackfirePenalty, SlotBackfireLock, SlotFrozen, SlotSurcharge
    };
}

internal static class HudText
{
    /// <summary>The preferred text when it is not blank and the font covers it (null check = covered), else the fallback.</summary>
    internal static string PickCovered(string preferred, string fallback, Func<string, bool> covers)
    {
        if (string.IsNullOrWhiteSpace(preferred))
            return fallback ?? string.Empty;
        return covers == null || covers(preferred) ? preferred : fallback ?? string.Empty;
    }
}

/// <summary>Obsession HUD text: the backfire chance is the primary number; the penalty time shows while it lasts.</summary>
internal static class ObsessionHudFormat
{
    private const float OneDecimalBelowPercent = 10f;

    /// <summary>"反噬 12%" (one decimal below 10%: "反噬 3.5%").</summary>
    internal static string Probability(string label, float percent)
    {
        float clamped = Math.Max(0f, percent);
        string format = Math.Round(clamped, 1) < OneDecimalBelowPercent ? "0.0" : "0";
        return Prefixed(label, clamped.ToString(format, CultureInfo.InvariantCulture) + "%");
    }

    /// <summary>"执念 512", plus "  反噬中 7.3s" while the backfire penalty lasts.</summary>
    internal static string Status(string obsessionLabel, float obsession, string penaltyLabel, bool penaltyActive, float penaltySecondsLeft)
    {
        string value = Prefixed(obsessionLabel, Math.Max(0f, obsession).ToString("0", CultureInfo.InvariantCulture));
        return penaltyActive ? value + "  " + Prefixed(penaltyLabel, SkillSlotHudFormat.Seconds(penaltySecondsLeft)) : value;
    }

    /// <summary>Change key at display precision (0.1 %, whole obsession, penalty tenths): text is rebuilt only when it changes.</summary>
    internal static int ChangeKey(float percent, float obsession, bool penaltyActive, float penaltySecondsLeft)
        => HashCode.Combine((int)Math.Round(Math.Max(0f, percent) * 10f), (int)Math.Round(Math.Max(0f, obsession)),
            penaltyActive, penaltyActive ? (int)Math.Ceiling(penaltySecondsLeft * 10f - 1e-3f) : 0);

    private static string Prefixed(string label, string value)
        => string.IsNullOrEmpty(label) ? value : label + " " + value;
}
