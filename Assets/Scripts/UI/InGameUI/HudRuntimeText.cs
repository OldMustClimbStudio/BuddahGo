using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Whether a TMP font, with its fallback chain (own table, TMP_Settings.fallbackFontAssets, default font), has every
/// glyph of a text. The HUD fonts are static atlases (LiberationSans SDF; the BuddahGoSoloCJK subset that
/// SoloUIBootstrap adds as a global fallback), so nothing is ever added to an atlas here. Results are cached per font
/// and text and dropped whenever the global fallback list changes size (SoloUIBootstrap may add SoloCJK after a HUD
/// text was first checked).
/// </summary>
internal static class HudGlyphCoverage
{
    private static readonly Dictionary<(int, string), bool> Cache = new Dictionary<(int, string), bool>();
    private static int _globalFallbackCount = -1;

    internal static bool Covers(TMP_FontAsset font, string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;
        if (font == null)
            return IsPrintableAscii(text);

        int fallbackCount = GlobalFallbackCount();
        if (fallbackCount != _globalFallbackCount)
        {
            Cache.Clear();
            _globalFallbackCount = fallbackCount;
        }

        var key = (font.GetInstanceID(), text);
        if (Cache.TryGetValue(key, out bool covered))
            return covered;

        // Line breaks and other control characters are layout, not glyphs.
        string glyphs = StripControlCharacters(text);
        covered = glyphs.Length == 0 || font.HasCharacters(glyphs, out uint[] _, searchFallbacks: true, tryAddCharacter: false);
        Cache[key] = covered;
        return covered;
    }

    /// <summary>The label's text (UI/Text row if any, else its preferred text) when the font covers it, else its ASCII text.</summary>
    internal static string Resolve(TMP_FontAsset font, HudLabel label)
        => HudText.PickCovered(DesignTableOverlay.Text(label.Key, label.Preferred), label.Ascii, text => Covers(font, text));

    /// <summary>A name (skill / 缘起 display name) when the font covers it, else its id.</summary>
    internal static string NameOrId(TMP_FontAsset font, string displayName, string id)
        => HudText.PickCovered(displayName, id, text => Covers(font, text));

    internal static int GlobalFallbackCount()
    {
        if (TMP_Settings.instance == null)
            return 0;
        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
        return fallbacks != null ? fallbacks.Count : 0;
    }

    private static bool IsPrintableAscii(string text)
    {
        foreach (char c in text)
            if (!char.IsControl(c) && (c < ' ' || c > '~'))
                return false;
        return true;
    }

    private static string StripControlCharacters(string text)
    {
        bool hasControl = false;
        foreach (char c in text)
            if (char.IsControl(c)) { hasControl = true; break; }
        if (!hasControl)
            return text;
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            if (!char.IsControl(c)) builder.Append(c);
        return builder.ToString();
    }
}

/// <summary>Runtime-created TMP labels for the HUD (no prefab edits): reuse the HUD's own font.</summary>
internal static class HudRuntimeText
{
    /// <summary>The font of the first TMP text under the anchor's root canvas, else TMP's default font asset.</summary>
    internal static TMP_FontAsset ResolveFont(Component anchor)
    {
        if (anchor != null)
        {
            Canvas canvas = anchor.GetComponentInParent<Canvas>(true);
            Transform root = canvas != null ? canvas.rootCanvas.transform : anchor.transform.root;
            TMP_Text existing = root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
            if (existing != null && existing.font != null)
                return existing.font;
        }
        return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
    }

    internal static TextMeshProUGUI Create(RectTransform parent, string name, TMP_FontAsset font, float fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = string.Empty;
        return text;
    }

    /// <summary>Optional SDF outline so ink-coloured labels stay readable over the track (instances the material).</summary>
    internal static void ApplyOutline(TextMeshProUGUI text, float width, Color color)
    {
        if (text == null || width <= 0f)
            return;
        text.outlineWidth = width;
        text.outlineColor = color;
    }

    internal static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;
        value ??= string.Empty;
        if (!string.Equals(target.text, value, System.StringComparison.Ordinal))
            target.text = value;
        bool visible = value.Length > 0;
        if (target.gameObject.activeSelf != visible)
            target.gameObject.SetActive(visible);
    }
}

/// <summary>
/// The SkillDatabase asset for HUD lookups (缘起 names, skill names). HUD components have no serialized reference
/// (no prefab edits in A6), so this finds the loaded asset: racers (SkillExecutor, SkillLoadout) and the selection
/// manager reference it, so it is in memory wherever the HUD needs it.
/// </summary>
internal static class HudSkillDatabase
{
    private static SkillDatabase _cached;

    internal static SkillDatabase Resolve()
    {
        if (_cached != null)
            return _cached;
        SkillDatabase[] loaded = Resources.FindObjectsOfTypeAll<SkillDatabase>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] == null)
                continue;
            // Prefer the asset that lists 缘起 pairs (the project has one database; tests may create empty ones).
            if (_cached == null || (_cached.yuanqis.Count == 0 && loaded[i].yuanqis.Count > 0))
                _cached = loaded[i];
        }
        return _cached;
    }

    /// <summary>The skill's display name (table text, else the SO name, else the id) when the font covers it, else the id.</summary>
    internal static string SkillName(SkillDatabase database, TMP_FontAsset font, string skillId)
    {
        string id = (skillId ?? string.Empty).Trim();
        if (database == null || id.Length == 0)
            return id;
        database.TryGet(id, out SkillAction skill);
        return HudGlyphCoverage.NameOrId(font, database.GetResolvedDisplayName(id, skill), id);
    }

    /// <summary>The 缘起 display name (yuanqi.&lt;id&gt;.name, else displayName) when the font covers it, else the id.</summary>
    internal static string YuanqiName(SkillDatabase database, TMP_FontAsset font, string yuanqiId)
    {
        string id = (yuanqiId ?? string.Empty).Trim();
        if (database == null || id.Length == 0)
            return id;
        return HudGlyphCoverage.NameOrId(font, database.GetYuanqiDisplayName(id), id);
    }
}
