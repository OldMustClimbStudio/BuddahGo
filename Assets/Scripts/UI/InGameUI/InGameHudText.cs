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
/// InGameHudTests); 冻 (冻结) and 起 (缘起) are not in the subset today, so those render their ASCII text until the
/// subset is re-baked. The keys are optional UI/Text rows: without a row the preferred text is used.
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
    internal static readonly HudLabel YuanqiToast = new HudLabel("hud.yuanqi.toast", "缘起", "Yuanqi");
    internal static readonly HudLabel YuanqiLoadoutHeader = new HudLabel("hud.yuanqi.loadout_header", "缘起", "Yuanqi");
    internal static readonly HudLabel YuanqiLoadoutNone = new HudLabel("hud.yuanqi.loadout_none", "无", "none");

    internal static readonly HudLabel[] All =
    {
        ObsessionValue, BackfireChance, BackfirePenalty, SlotBackfireLock, SlotFrozen, SlotSurcharge,
        YuanqiToast, YuanqiLoadoutHeader, YuanqiLoadoutNone
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

/// <summary>A 缘起 the chosen loadout can trigger, with names already resolved for the font.</summary>
internal readonly struct YuanqiHudEntry
{
    public YuanqiHudEntry(string name, string skillAName, string skillBName)
    {
        Name = name ?? string.Empty;
        SkillAName = skillAName ?? string.Empty;
        SkillBName = skillBName ?? string.Empty;
    }

    public string Name { get; }
    public string SkillAName { get; }
    public string SkillBName { get; }
}

internal static class YuanqiHudText
{
    /// <summary>Caster toast: "缘起 禅足冰封".</summary>
    internal static string Toast(string label, string name) => string.IsNullOrEmpty(label) ? name ?? string.Empty : label + " " + name;

    /// <summary>
    /// Selection screen list: empty while fewer than two skills are chosen (nothing can pair yet), "header: none" when
    /// the loadout triggers nothing, else one "name (A + B)" line per 缘起 under "header:".
    /// </summary>
    internal static string LoadoutList(string header, string noneLabel, IReadOnlyList<YuanqiHudEntry> entries, int chosenSkillCount)
    {
        if (chosenSkillCount < 2)
            return string.Empty;
        if (entries == null || entries.Count == 0)
            return header + ": " + noneLabel;

        var builder = new StringBuilder(header).Append(':');
        for (int i = 0; i < entries.Count; i++)
            builder.Append('\n').Append(entries[i].Name).Append(" (").Append(entries[i].SkillAName)
                .Append(" + ").Append(entries[i].SkillBName).Append(')');
        return builder.ToString();
    }
}
