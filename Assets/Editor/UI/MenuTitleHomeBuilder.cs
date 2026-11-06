using System.Collections.Generic;
using SteamMultiplayer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Builds the title screen and home menu ("一笔", v0.2 mockups 01 and 02) into the open menu environment scene
/// as its own root UI_TitleHome, which Tools/Menu Environment/2 Build Scene leaves alone. Positions are the
/// mockups' 1920×1080 pixels. Running it again replaces the previous build.
/// </summary>
public static class MenuTitleHomeBuilder
{
    private const string RootName = "UI_TitleHome";
    private const string SkillRootName = "UI_SkillSelect";
    private const string SlotsRootName = "UI_HokoraSlots";
    private const string DojoRootName = "UI_Dojo";
    private const string SkinRootName = "UI_SkinSelect";
    private const string GradeRootName = "UI_MenuGrade";
    private const string ConfirmRootName = "UI_Confirm";
    // The menu's own grade over the shared dusk profile; made once, then left for tuning in the editor.
    private const string GradeProfilePath = "Assets/UI/MainMenu/MenuGrade_Profile.asset";
    // Skins offered in the skin close-up: the model's own material first, then the skin materials in the project.
    // Names confirmed by the user on 2026-11-06. art: Assets/UI/MainMenu/Art/skin_name_<art>.png (asset A16).
    private const string HokoraName = "SM_Arch_Hokora_A__TerraceWest";
    // The slots sit on the front of the hokora's wooden base: measured on the hokora mesh, its carved panel is 0.099 m
    // in front of the hokora's centre (along its facing), spans 0.92–1.04 m in height and ±0.2 m across, between two
    // corner blocks whose fronts are at 0.16 m. The canvas is flush with the blocks so they do not cut the outer rings.
    private const float SlotsForward = 0.165f;
    private const float SlotsHeight = 0.98f;
    private const float SlotSpacing = 0.15f;
    private const string SkillArtDir = "Assets/UI/UI资源/文字PNG/New/";
    // Descriptions follow the GitHub README ("当前已实现技能") and Docs/gameplay/README.md 6.2 (the backfire of each skill: its _anti variant; the 5 s lock is appended by MenuSkillPage).
    private static readonly (string id, string art, string desc, string backfire)[] SkillArts =
    {
        ("acceleration", "神足通", "自身加速一段时间。", "自身减速"),
        ("slowtrap", "禅定缓流", "在身边布下减速区域，碰到的对手会被减速。", "自己先被短暂定住，随后加速"),
        ("blackcurtain", "一念光明", "黑幕笼罩全场，对手看不清赛道和其他人。", "只遮住自己的视野"),
        ("giant", "法像金身", "放大自身形态，能把对手撞开。", "自身缩小"),
        ("push_projectile_hands", "如来神掌", "向前方发出如来神掌，推开挡路的对手。", "向身后发出反向的神掌"),
        ("reverseturn", "逆姻缘", "让其他所有对手的左右转向颠倒。", "反转自己的转向"),
    };
    private const string SpriteDir = "Assets/UI/MainMenu/Sprites/";
    // Final art (Docs/ui/asset-list.md), drawn at 2× the 1080p size; imported at 200 pixels per unit so sliced
    // borders come out at display size. Where an entry's file is there it replaces the placeholder of the same use.
    private const string ArtDir = "Assets/UI/MainMenu/Art/";
    private static readonly Dictionary<string, string> ArtFor = new Dictionary<string, string>
    {
        { "Menu_title_v", "menu_title_vertical" },
        { "Menu_title", "menu_title_horizontal" },
        { "Menu_seal", "menu_seal" },
        { "Menu_ink", "menu_ink_full" },           // ArtSource/MainMenu/compose.py: head + body + tail
        { "Menu_hairline", "menu_line_long" },     // compose.py: end + body + end
        { "Menu_hairline_short", "menu_line_short" },
        { "Menu_enso_bold", "menu_enso_bold" },
        { "Menu_enso_thin", "menu_enso_thin" },
        { "Menu_brushbar", "menu_brush_bar" },
        { "Menu_chevron", "menu_chevron_r" },
        { "Menu_dot", "menu_dot" },                // A18: the vermilion dot (selection, ready); the slot backing keeps the round placeholder
        { "Menu_arc", "menu_arc" },                // A19: the skin page's arc
    };

    /// <summary>The final art for a placeholder name when it has been delivered, else the placeholder.</summary>
    private static Sprite LoadSprite(string placeholder)
    {
        if (ArtFor.TryGetValue(placeholder, out string art)
            && AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + art + ".png") is Sprite final)
            return final;
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + placeholder + ".png");
    }
    private const string FontPath = "Assets/Fonts/MenuSerif/NotoSerifSC-VF.ttf";
    private const string FontAssetPath = "Assets/Fonts/MenuSerif/MenuSerif SDF.asset";
    private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
    private static readonly Color Vermilion = new Color32(0xB8, 0x35, 0x2A, 0xFF);

    private static readonly (string label, string desc, float x)[] Items =
    {
        // Centred on the screen: the five items span 705 … 1215 (centre 960), 115 apart.
        ("技能", "在绘马墙前配置技能", 705f),
        ("道场", "与道友同场竞速　创建或加入房间", 820f),
        ("皮肤", "挑选佛像形象", 935f),
        ("设置", "画面　声音　操作", 1050f),
        ("离去", "离开游戏", 1165f),
    };

    /// <summary>How far the bottom menu (baseline, ink, dot, items, description) sits below the v0.2 mockup.</summary>
    private const float MenuDrop = 50f;

    [MenuItem("Tools/Menu Environment/Build Title + Home UI")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("MenuEnvironmentSetting.unity"))
        {
            Debug.LogError("[MenuTitleHome] Open Assets/Scenes/MenuEnvironmentSetting.unity first.");
            return;
        }
        ImportSprites();
        TMP_FontAsset font = LoadOrCreateFont();
        _keySprites = BuildKeySprites();

        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == RootName || go.name == SkillRootName || go.name == SlotsRootName || go.name == DojoRootName || go.name == SkinRootName || go.name == GradeRootName || go.name == ConfirmRootName) Object.DestroyImmediate(go);
        BuildGrade();

        var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // under TEMP_MenuCanvas, so its camera fade covers this UI too
        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)rootGo.transform;

        // Title state.
        // The game view is brighter than the mockup captures and UI blends in linear space, so the darkening is
        // stronger than the mockup's CSS values: a light veil over the whole scene plus the edge gradients.
        Veil(root, 0.15f);

        // Opening black: fades out while the camera travels in, and blocks clicks on the scene until the menu is up.
        var blackGo = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
        var blackRect = (RectTransform)blackGo.transform;
        blackRect.SetParent(root, false);
        blackRect.anchorMin = Vector2.zero; blackRect.anchorMax = Vector2.one; blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;
        Image blackout = blackGo.GetComponent<Image>();
        blackout.color = new Color(0.02f, 0.015f, 0.015f, 1f);

        // Title (reference: vertical calligraphy in the middle, seal at its foot, BUDDAHGO between two short
        // lines, "按任意键开始" under it).
        CanvasGroup title = Group(root, "Title");
        Shade(title.transform, "ShadeBottom", false, 0.38f, 0.7f);
        Shade(title.transform, "ShadeTop", false, 0.3f, 0.45f, true);
        // A faint bloom behind the calligraphy: its ink blurred (ArtSource/MainMenu/glow.py, padded by 150 px of the art),
        // shown at the title's scale so it sits round the strokes.
        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_title_vertical_glow.png");
        if (glow != null)
        {
            const float pad = 150f, sx = 172f / 560f, sy = 582f / 2000f;
            Image g = Img(title.transform, "TitleGlow", "Menu_title_v", new Vector2(874 - pad * sx, 140 - pad * sy),
                new Vector2(172 + 2 * pad * sx, 582 + 2 * pad * sy), new Color(Paper.r, Paper.g, Paper.b, 0.06f)); // just visible
            g.sprite = glow;
        }
        Img(title.transform, "TitleVertical", "Menu_title_v", new Vector2(874, 140), new Vector2(172, 582), Color.white);
        Img(title.transform, "Seal", "Menu_seal", new Vector2(1046, 664), new Vector2(30, 30), Color.white);
        TextMeshProUGUI latin = Txt(title.transform, "Latin", "BUDDAHGO", font, 20, Vector2.zero, 0.8f, 90);
        latin.alignment = TextAlignmentOptions.Center;
        Centre(latin.rectTransform, new Vector2(9, 540 - 772)); // the trailing letter spacing pushes the word left
        latin.rectTransform.sizeDelta = new Vector2(400, 32);
        Img(title.transform, "LineLeft", "Menu_hairline_short", new Vector2(696, 766), new Vector2(110, 12), new Color(1, 1, 1, 0.7f));
        Img(title.transform, "LineRight", "Menu_hairline_short", new Vector2(1114, 766), new Vector2(110, 12), new Color(1, 1, 1, 0.7f));
        TextMeshProUGUI anyKey = Txt(title.transform, "PressAnyKey", "按任意键开始", font, 20, Vector2.zero, 0f, 60);
        anyKey.alignment = TextAlignmentOptions.Center;
        Centre(anyKey.rectTransform, new Vector2(6, 540 - 900));
        anyKey.rectTransform.sizeDelta = new Vector2(600, 40);
        TextMeshProUGUI studio = Txt(title.transform, "Studio", "OMC STUDIO", font, 14, Vector2.zero, 0.42f, SpacingNormal);
        studio.alignment = TextAlignmentOptions.BottomRight;
        studio.rectTransform.anchorMin = studio.rectTransform.anchorMax = studio.rectTransform.pivot = new Vector2(1f, 0f);
        studio.rectTransform.anchoredPosition = new Vector2(-96, 54); // safe area corner, like the hint bar

        // Home state.
        CanvasGroup home = Group(root, "Home");
        Shade(home.transform, "ShadeBottom", false, 0.4f, 0.85f);
        Shade(home.transform, "ShadeTop", false, 0.24f, 0.6f, true);
        CanvasGroup logo = Group(home.transform, "Logo");
        Img(logo.transform, "TitleCalligraphy", "Menu_title", new Vector2(91, 50), new Vector2(342, 91), Color.white); // the art has 29 px of margin: ink starts at 120
        Img(logo.transform, "TitleLine", "Menu_hairline", new Vector2(120, 144), new Vector2(284, 12), new Color(1, 1, 1, 0.5f)); // under the ink
        // The bottom menu, in its own group so the settings page can hide it.
        CanvasGroup bar = Group(home.transform, "MenuBar");
        Img(bar.transform, "Baseline", "Menu_hairline", new Vector2(60, 952 + MenuDrop), new Vector2(1800, 12), new Color(1, 1, 1, 0.55f));
        Image ink = Img(bar.transform, "Ink", "Menu_ink", Vector2.zero, new Vector2(0, 24), new Color(1, 1, 1, 0.85f));
        ink.type = Image.Type.Sliced;
        BottomLeft(ink.rectTransform, new Vector2(0f, 0.5f), new Vector2(60, 122 - MenuDrop));
        Image dot = Img(bar.transform, "Dot", "Menu_dot", Vector2.zero, new Vector2(12, 12), Vermilion);
        BottomLeft(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60, 122 - MenuDrop));
        TextMeshProUGUI desc = Txt(bar.transform, "Description", "", font, 18, Vector2.zero, 0.42f, SpacingNormal);
        desc.alignment = TextAlignmentOptions.BottomLeft;
        BottomLeft(desc.rectTransform, Vector2.zero, new Vector2(120, 134 - MenuDrop));
        desc.rectTransform.sizeDelta = new Vector2(440, 30);

        var items = new List<MenuTitleHome.Item>();
        foreach (var (label, d, x) in Items)
        {
            TextMeshProUGUI t = Txt(bar.transform, "Item_" + label, label, font, T3, Vector2.zero, 1f, SpacingWide);
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.raycastTarget = true;
            BottomLeft(t.rectTransform, Vector2.zero, new Vector2(x, 134 - MenuDrop));
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(label).x + 4f, 40);
            items.Add(new MenuTitleHome.Item { label = label, description = d, text = t });
        }
        items[0].target = Find<Button>("Btn_技能选择");
        items[1].target = Find<Button>("TEMP_StartButton");
        items[2].target = Find<Button>("TEMP_SkinButton");
        items[3].opensSettings = true;
        items[4].quits = true;

        // Object markers: a label at the end of a long leader line beside each object that can be clicked on the
        // main menu, placed over the object each frame and shown while it glows under the pointer. Each line starts
        // off the object's edge and runs up and out, so the label clears it.
        Camera cam = Camera.main;
        MenuHoverHighlighter highlighter = cam != null ? cam.GetComponent<MenuHoverHighlighter>() : null;
        var markers = new List<MenuTitleHome.Marker>();
        BoxCollider hallProxy = Find<BoxCollider>("HondenClickProxy");
        if (hallProxy != null)
            markers.Add(Marker(home.transform, font, "本殿", "点击进入", Find<Transform>("Arch_Honden_Wall_Front"),
                hallProxy.transform, hallProxy.center, new Vector2(150, 120), 18f, 240f));
        BoxCollider skinProxy = Find<BoxCollider>("SkinClickProxy");
        if (skinProxy != null)
            markers.Add(Marker(home.transform, font, "佛像", "点击挑选皮肤", Find<Transform>("SkinTurntable"),
                skinProxy.transform, skinProxy.center, new Vector2(60, 200), 24f, 170f));
        Transform wall = Find<Transform>("SM_Arch_EmaWall_A__TerraceWest");
        if (wall != null)
        {
            Bounds b = new Bounds(wall.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in wall.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "HoverOutline") continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            markers.Add(Marker(home.transform, font, "绘马墙", "点击配置技能", wall,
                wall, wall.InverseTransformPoint(b.center), new Vector2(40, 110), 20f, 200f));
        }
        if (markers.Count < 3) Debug.LogWarning("[MenuTitleHome] Some object markers were not built: their objects were not found.");

        // Opening camera move: from the torii pose along the first points of the torii → hall path
        // (it then ends on the main-menu shot).
        var path = new List<Transform>();
        GameObject torii = GameObject.Find("CAM_Torii_Start");
        if (torii != null) path.Add(torii.transform);
        GameObject toriiPath = GameObject.Find("CameraPath_ToriiToHonden");
        if (toriiPath != null)
            foreach (string point in new[] { "Point_01", "Point_02", "Point_03" })
            {
                Transform t = toriiPath.transform.Find(point);
                if (t != null) path.Add(t);
            }
        if (torii == null) Debug.LogWarning("[MenuTitleHome] CAM_Torii_Start not found: the opening camera move is off.");

        var comp = rootGo.AddComponent<MenuTitleHome>();
        comp.Configure(rootGo.GetComponent<CanvasGroup>(), title, home, anyKey, blackout, torii != null ? path.ToArray() : new Transform[0],
            items.ToArray(), ink.rectTransform, dot.rectTransform, desc, markers.ToArray(), cam,
            cam != null ? cam.GetComponent<MenuCameraProtoSwitcher>() : null,
            cam != null ? cam.GetComponent<MenuCameraDirector>() : null,
            cam != null ? cam.GetComponent<MenuSkinView>() : null,
            cam != null ? cam.GetComponent<MenuEmaInspector>() : null,
            cam != null ? cam.GetComponent<MenuCameraMouseLook>() : null,
            highlighter,
            null); // the proto panel is removed below (RemoveProtoPanel)
        comp.SetSettingsPage(BuildSettings(root, font), logo, bar);
        // The bell rope's bells are effects: their volume follows 音效 in the audio settings.
        if (GameObject.Find("BellRope") is GameObject rope && rope.GetComponent<AudioSource>() != null
            && rope.GetComponent<BuddahGo.Audio.AudioChannelVolume>() == null)
            rope.AddComponent<BuddahGo.Audio.AudioChannelVolume>().Channel = BuddahGo.Audio.AudioChannel.Sfx;
        comp.SetSkillPage(BuildSkillPage(font, cam, highlighter));
        ApplyEmaBacks(cam);
        comp.SetDojoPage(BuildDojo(font, cam));
        comp.SetSkinPage(BuildSkinPage(font, cam));
        BuildConfirm(font);
        var cursor = new SerializedObject(rootGo.AddComponent<MenuCursor>());
        cursor.FindProperty("art").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "menu_cursor.png");
        cursor.ApplyModifiedPropertiesWithoutUndo();
        var so = new SerializedObject(comp);
        so.FindProperty("itemBaseY").floatValue = 134f - MenuDrop;
        so.ApplyModifiedPropertiesWithoutUndo();
        RemoveProtoPanel();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MenuTitleHome] Built " + RootName + ".");
    }

    /// <summary>
    /// One object marker: the line starts at `start` from the object's point (canvas pixels, y up) and runs `length`
    /// at `angle` degrees; the name and hint sit just past its end.
    /// </summary>
    private static MenuTitleHome.Marker Marker(Transform parent, TMP_FontAsset font, string name, string hint, Transform hoverRoot,
                                               Transform anchor, Vector3 anchorLocal, Vector2 start, float angle, float length)
    {
        CanvasGroup group = Group(parent, "Marker_" + name);
        var rect = (RectTransform)group.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400, 400);
        rect.anchoredPosition = Vector2.zero;
        group.alpha = 0f;

        Image leader = Img(group.transform, "Leader", "Menu_hairline", Vector2.zero, new Vector2(length, 8), new Color(1, 1, 1, 0f));
        Centre(leader.rectTransform, start);
        leader.rectTransform.pivot = new Vector2(0f, 0.5f);
        leader.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        leader.type = Image.Type.Filled;
        leader.fillMethod = Image.FillMethod.Horizontal;
        leader.fillOrigin = (int)Image.OriginHorizontal.Left;
        leader.fillAmount = 0f;

        float rad = angle * Mathf.Deg2Rad;
        Vector2 end = start + length * new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        CanvasGroup text = Group(group.transform, "Text");
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = end + new Vector2(14, 0);
        text.alpha = 0f;
        TextMeshProUGUI title = Txt(text.transform, "Name", name, font, T4, Vector2.zero, 1f, SpacingNormal);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        title.rectTransform.anchoredPosition = new Vector2(0, 12);
        TextMeshProUGUI sub = Txt(text.transform, "Hint", hint, font, 13, Vector2.zero, 0.42f, SpacingNormal);
        sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        sub.rectTransform.pivot = new Vector2(0f, 0.5f);
        sub.rectTransform.anchoredPosition = new Vector2(0, -14);

        return new MenuTitleHome.Marker
        {
            label = name, group = group, hoverRoot = hoverRoot, anchor = anchor, anchorLocal = anchorLocal,
            leader = leader, text = text,
        };
    }

    /// <summary>Settings page (v0.2 mockup 08): over the home menu, which stays on the baseline below it.</summary>
    private static MenuSettingsPage BuildSettings(RectTransform root, TMP_FontAsset font)
    {
        CanvasGroup page = Group(root, "Settings");
        // Transparent blocker: keeps the pointer off the scene's clickables (and their glow) while the page is open.
        var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        var blockerRect = (RectTransform)blocker.transform;
        blockerRect.SetParent(page.transform, false);
        blockerRect.anchorMin = Vector2.zero; blockerRect.anchorMax = Vector2.one; blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        // Scene first (style guide 1): a light veil and one deep gradient from the left edge, no solid slab.
        Veil(page.transform, 0.2f);
        Shade(page.transform, "ShadeLeft", true, 0.46f, 0.82f);

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        var panel = (RectTransform)panelGo.transform;
        panel.SetParent(page.transform, false);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;

        // One narrow column at x = 120: title, tabs, then the rows held between two hairlines (the list's frame),
        // the status note just under it. Key hints sit apart, in the bottom-right corner.
        const float column = 480f, rowsTop = 248f, rowsHeight = 9 * 52f;
        Txt(panel, "Title", "设置", font, T1, new Vector2(120, 96), 1f, SpacingWide);

        var tabs = new List<TextMeshProUGUI>();
        foreach (var (label, x) in new[] { ("画面", 120f), ("音频", 226f), ("操作", 332f) })
        {
            TextMeshProUGUI t = Txt(panel, "Tab_" + label, label, font, T3, new Vector2(x, 160), MinTextAlpha, SpacingWide);
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(label).x, 34);
            tabs.Add(t);
        }
        // The selected tab's ink: the drawn stroke with its start and dry tail scaled down to a tab's width.
        Image tabInk = Img(panel, "TabInk", "Menu_ink", new Vector2(116, 196), new Vector2(0, 12), new Color(1, 1, 1, 0.85f));
        tabInk.type = Image.Type.Sliced;
        tabInk.pixelsPerUnitMultiplier = 5f;

        Img(panel, "LineTop", "Menu_hairline", new Vector2(120, rowsTop - 22), new Vector2(column, 10), new Color(1, 1, 1, 0.35f));
        var viewGo = new GameObject("Rows", typeof(RectTransform), typeof(RectMask2D));
        var view = (RectTransform)viewGo.transform;
        view.SetParent(panel, false);
        view.anchorMin = view.anchorMax = view.pivot = new Vector2(0f, 1f);
        view.anchoredPosition = new Vector2(120, -rowsTop);
        view.sizeDelta = new Vector2(column, rowsHeight);
        viewGo.GetComponent<RectMask2D>().softness = new Vector2Int(0, 20);
        var contentGo = new GameObject("Content", typeof(RectTransform));
        var content = (RectTransform)contentGo.transform;
        content.SetParent(view, false);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        Img(panel, "LineBottom", "Menu_hairline", new Vector2(120, rowsTop + rowsHeight + 12), new Vector2(column, 10), new Color(1, 1, 1, 0.35f));

        Image dot = Img(panel, "Dot", "Menu_dot", new Vector2(104, rowsTop + 26), new Vector2(9, 9), Vermilion, new Vector2(0.5f, 0.5f)); // hangs left of the column
        TextMeshProUGUI empty = Txt(panel, "Placeholder", "", font, T3, new Vector2(120, rowsTop + 12), MinTextAlpha, SpacingNormal);
        TextMeshProUGUI note = Txt(panel, "Note", "", font, T5, new Vector2(120, rowsTop + rowsHeight + 36), MinTextAlpha, SpacingNormal);
        note.rectTransform.sizeDelta = new Vector2(column + 120, 50);
        TextMeshProUGUI[] hints = HintBar(panel, font, new[] { ("Apply", "enter", "应用"), ("Reset", "r", "恢复预设"), ("Back", "esc", "返回") });
        TextMeshProUGUI apply = hints[0], reset = hints[1], back = hints[2];

        var comp = page.gameObject.AddComponent<MenuSettingsPage>();
        comp.Configure(page, panel, tabs.ToArray(), tabInk.rectTransform, view, content, dot.rectTransform, empty, note,
            apply, reset, back, font, LoadSprite("Menu_hairline"));
        return comp;
    }

    /// <summary>Skill page (v0.2 mockup 05) on its own canvas: it shows on the skill-select shot, where UI_TitleHome is hidden.</summary>
    private static MenuSkillPage BuildSkillPage(TMP_FontAsset font, Camera cam, MenuHoverHighlighter highlighter)
    {
        var rootGo = new GameObject(SkillRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)rootGo.transform;
        var ink = new Color(0.02f, 0.015f, 0.015f, 1f);

        // Along the top and bottom edges always; dark on the right only while the skill or the loadout is open.
        Shade(root, "ShadeBottom", false, 0.28f, 0.7f);
        Shade(root, "ShadeTop", false, 0.22f, 0.55f, true);
        // One deep gradient from the right edge (no solid slab), as on the settings page.
        CanvasGroup rightShade = Group(root, "RightShade");
        Shade(rightShade.transform, "ShadeRight", true, 0.52f, 0.85f);
        var fade = (RectTransform)rightShade.transform.Find("ShadeRight");
        fade.anchorMin = new Vector2(0.48f, 0f); fade.anchorMax = Vector2.one;
        SetEdge(fade, UIEdgeShade.Edge.Right);

        Txt(root, "Title", "技能", font, 30, new Vector2(120, 96), 1f, SpacingWide);
        Txt(root, "Subtitle", "绘马墙　挑三面挂入小祠", font, 15, new Vector2(120, 150), 0.45f, SpacingNormal);
        TextMeshProUGUI back = HintBar(root, font, new[] { ("Back", "esc", "返回") })[0];

        // Right panel, shown while an ema is inspected: the skill.
        CanvasGroup panel = Group(root, "Panel");
        CanvasGroup nameGroup = Group(panel.transform, "Focus");
        Splash(nameGroup.transform, "menu_splash_01", new Vector2(1584, 309), 430f);
        // One centred column on x = 1584 (1344 … 1824, ending on the safe area): name, text, backfire and stats share its axis.
        Image nameImage = Img(nameGroup.transform, "Name", "Menu_title", new Vector2(1349, 230), new Vector2(470, 158), Color.white);
        nameImage.preserveAspect = true;
        Img(nameGroup.transform, "Rule", "Menu_hairline_short", new Vector2(1584, 404), new Vector2(200, 8), new Color(1, 1, 1, 0.45f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI desc = Txt(nameGroup.transform, "Description", "", font, 19, new Vector2(1344, 424), 0.85f, SpacingNormal);
        desc.alignment = TextAlignmentOptions.Top;
        desc.rectTransform.sizeDelta = new Vector2(480, 60);
        desc.enableWordWrapping = true;
        TextMeshProUGUI backfireText = Txt(nameGroup.transform, "Backfire", "", font, 15, new Vector2(1344, 490), 0.5f, SpacingNormal);
        backfireText.alignment = TextAlignmentOptions.Top;
        backfireText.rectTransform.sizeDelta = new Vector2(480, 44);
        backfireText.enableWordWrapping = true;
        TextMeshProUGUI stats = Txt(nameGroup.transform, "Stats", "", font, 15, new Vector2(1344, 540), 0.5f, SpacingNormal);
        stats.alignment = TextAlignmentOptions.Top;
        stats.rectTransform.sizeDelta = new Vector2(480, 26);
        // How to use the inspect view: in the bottom-right hint bar, in place of 返回 (which gives way while inspecting).
        string usageText = "拖动翻看　" + Key("left") + Key("right") + " 切换　点击空白处或 " + Key("esc") + " 挂回";
        TextMeshProUGUI usage = Txt(panel.transform, "Usage", usageText, font, T5, Vector2.zero, 0.8f, SpacingNormal);
        usage.alignment = TextAlignmentOptions.Center;
        usage.rectTransform.anchorMin = usage.rectTransform.anchorMax = Vector2.zero;
        usage.rectTransform.pivot = new Vector2(0.5f, 0f);
        usage.rectTransform.sizeDelta = new Vector2(usage.GetPreferredValues(usageText).x + 4f, 34);
        usage.rectTransform.anchoredPosition = new Vector2(1584, 54); // under the column it explains, not in the screen corner

        Sprite filled = LoadSprite("Menu_enso_thin"); // slots are functional: thin ensō, the state shows in its alpha
        Sprite empty = LoadSprite("Menu_enso_thin");
        var slots = new List<MenuSkillPage.Slot>();
        CanvasGroup loadoutGroup = BuildHokoraSlots(font, cam, slots);

        // Thin enso around the ema under the pointer, with its vermilion dot.
        var ringGo = new GameObject("HoverRing", typeof(RectTransform), typeof(CanvasGroup));
        var ringRect = (RectTransform)ringGo.transform;
        ringRect.SetParent(root, false);
        ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(170, 170);
        Image hoverEnso = Img(ringRect, "Enso", "Menu_enso_thin", Vector2.zero, new Vector2(170, 170), new Color(1, 1, 1, 0.75f));
        Centre(hoverEnso.rectTransform, Vector2.zero);
        Image hoverDot = Img(ringRect, "Dot", "Menu_dot", Vector2.zero, new Vector2(10, 10), Vermilion);
        Centre(hoverDot.rectTransform, new Vector2(74, -55));

        // Hokora note: over an ema hanging in the hokora, a leader line out of its ring to the upper left (the hokora sits
        // on the right of the shot) with the skill's name and how to take it down, right-aligned at the line's end.
        CanvasGroup note = Group(root, "SlotNote");
        var noteRect = (RectTransform)note.transform;
        noteRect.anchorMin = noteRect.anchorMax = noteRect.pivot = new Vector2(0.5f, 0.5f);
        noteRect.sizeDelta = new Vector2(400, 400);
        note.alpha = 0f;
        const float noteAngle = 160f, noteLength = 150f;
        var noteStart = new Vector2(-88, 30); // just outside the hover ring (170 px across)
        Image noteLeader = Img(note.transform, "Leader", "Menu_hairline", Vector2.zero, new Vector2(noteLength, 8), new Color(1, 1, 1, 0f));
        Centre(noteLeader.rectTransform, noteStart);
        noteLeader.rectTransform.pivot = new Vector2(0f, 0.5f);
        noteLeader.rectTransform.localEulerAngles = new Vector3(0, 0, noteAngle);
        noteLeader.type = Image.Type.Filled;
        noteLeader.fillMethod = Image.FillMethod.Horizontal;
        noteLeader.fillOrigin = (int)Image.OriginHorizontal.Left;
        noteLeader.fillAmount = 0f;
        float noteRad = noteAngle * Mathf.Deg2Rad;
        Vector2 noteEnd = noteStart + noteLength * new Vector2(Mathf.Cos(noteRad), Mathf.Sin(noteRad));
        CanvasGroup noteText = Group(note.transform, "Text");
        var noteTextRect = (RectTransform)noteText.transform;
        noteTextRect.anchorMin = noteTextRect.anchorMax = noteTextRect.pivot = new Vector2(0.5f, 0.5f);
        noteTextRect.sizeDelta = Vector2.zero;
        noteTextRect.anchoredPosition = noteEnd + new Vector2(-14, 0);
        noteText.alpha = 0f;
        TextMeshProUGUI noteName = Txt(noteText.transform, "Name", "", font, T4, Vector2.zero, 1f, SpacingNormal);
        noteName.alignment = TextAlignmentOptions.MidlineRight;
        noteName.rectTransform.anchorMin = noteName.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        noteName.rectTransform.pivot = new Vector2(1f, 0.5f);
        noteName.rectTransform.sizeDelta = new Vector2(320, 30);
        noteName.rectTransform.anchoredPosition = new Vector2(0, 14);
        string noteHint = Key("mouse_right") + " 取下　挂回绘马墙";
        TextMeshProUGUI noteSub = Txt(noteText.transform, "Hint", noteHint, font, T5, Vector2.zero, 0.8f, SpacingNormal);
        noteSub.alignment = TextAlignmentOptions.MidlineRight;
        noteSub.rectTransform.anchorMin = noteSub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        noteSub.rectTransform.pivot = new Vector2(1f, 0.5f);
        noteSub.rectTransform.sizeDelta = new Vector2(320, 34);
        noteSub.rectTransform.anchoredPosition = new Vector2(0, -16);

        var arts = new List<MenuSkillPage.SkillArt>();
        foreach (var (id, art, text, backfire) in SkillArts)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkillArtDir + art + "-白.png");
            if (sprite == null) Debug.LogWarning("[MenuTitleHome] No calligraphy for skill " + id + " at " + SkillArtDir + art + "-白.png");
            arts.Add(new MenuSkillPage.SkillArt { skillId = id, name = sprite, description = text, backfire = backfire, title = art });
        }

        // Slot-key panel on the left, shown after a click on the hokora (layout after the reference: small category,
        // title, text, then dark boxes): one row per slot with the slotted skill's name on the left and its keys on the right.
        CanvasGroup leftShade = Group(root, "LeftShade");
        leftShade.transform.SetSiblingIndex(0); // under the title and 返回, which sit on the left too
        Shade(leftShade.transform, "ShadeLeft", true, 0.52f, 0.85f);
        CanvasGroup keys = Group(root, "Combos");
        Txt(keys.transform, "Category", "", font, 15, new Vector2(120, 236), 0.5f, SpacingNormal);
        TextMeshProUGUI comboHeader = keys.transform.Find("Category").GetComponent<TextMeshProUGUI>();
        comboHeader.rectTransform.sizeDelta = new Vector2(480, 26);
        Txt(keys.transform, "Title", "技能键", font, 34, new Vector2(120, 264), 1f, SpacingWide);
        TextMeshProUGUI how = Txt(keys.transform, "Text", "比赛中按下对应的键，就放出这一槽的技能。同一时间只能有一个技能在前摇。", font, 17, new Vector2(120, 326), 0.75f, SpacingNormal);
        how.rectTransform.sizeDelta = new Vector2(470, 56);
        how.enableWordWrapping = true;
        // 执念 and 反噬 in a few lines under the rows (numbers from Skill/Obsession.csv: repeatWindowSeconds 5, repeatGainMultiplier 1.15, backfireLockSeconds 5).
        TextMeshProUGUI rules = Txt(keys.transform, "Rules",
            "<color=#B8352A>执念</color>　每放一次技能，执念就上升，越高越容易反噬。\n同一技能冷却结束后 5 秒内再放，执念多涨 15%。\n<color=#B8352A>反噬</color>　放出该技能的反噬版本，并锁住全部技能 5 秒。",
            font, 15, new Vector2(120, 696), 0.6f, SpacingNormal);
        rules.rectTransform.sizeDelta = new Vector2(480, 96);
        rules.enableWordWrapping = true;
        var rows = new List<RectTransform>();
        var rowArts = new List<Image>();
        var rowEmpty = new List<TextMeshProUGUI>();
        for (int i = 0; i < slots.Count; i++)
        {
            float y = 420 + i * 84;
            var boxGo = new GameObject("Row_" + i, typeof(RectTransform), typeof(Image));
            var box = (RectTransform)boxGo.transform;
            box.SetParent(keys.transform, false);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(120, -y);
            box.sizeDelta = new Vector2(480, 68);
            var boxImg = boxGo.GetComponent<Image>();
            boxImg.color = new Color(0f, 0f, 0f, 0f); // no box: rows are parted by hairlines, like the settings list
            boxImg.raycastTarget = false;
            Img(box, "Line", "Menu_hairline", new Vector2(0, 64), new Vector2(480, 8), new Color(1, 1, 1, 0.22f));
            TextMeshProUGUI index = Txt(box, "Index", (i + 1).ToString(), font, 15, new Vector2(0, 22), 0.45f, SpacingNormal);
            index.rectTransform.sizeDelta = new Vector2(20, 24);
            Image art = Img(box, "Art", "Menu_title", new Vector2(24, 15), new Vector2(114, 38), Color.white);
            art.preserveAspect = true;
            rowArts.Add(art);
            TextMeshProUGUI emptyText = Txt(box, "Empty", "空", font, 17, new Vector2(28, 20), 0.4f, SpacingNormal);
            emptyText.rectTransform.sizeDelta = new Vector2(60, 28);
            rowEmpty.Add(emptyText);
            var capsGo = new GameObject("Keys", typeof(RectTransform));
            var caps = (RectTransform)capsGo.transform;
            caps.SetParent(box, false);
            caps.anchorMin = caps.anchorMax = new Vector2(1f, 0.5f);
            caps.pivot = new Vector2(1f, 0.5f);
            caps.anchoredPosition = new Vector2(-16, 0);
            caps.sizeDelta = new Vector2(0, 34);
            rows.Add(caps);
        }
        // 收起 belongs to the combo column: on its left edge (120), on the hint bar's line.
        TextMeshProUGUI collapse = HintBar(keys.transform, font, new[] { ("Collapse", "esc", "收起") })[0];
        collapse.alignment = TextAlignmentOptions.MidlineLeft;
        collapse.rectTransform.anchorMin = collapse.rectTransform.anchorMax = collapse.rectTransform.pivot = Vector2.zero;
        collapse.rectTransform.anchoredPosition = new Vector2(120, 54);

        // Arrows beside the inspected ema (switch to the previous / next one); the page's only raycast targets.
        CanvasGroup arrows = Group(root, "Arrows");
        Button previous = Arrow(arrows.transform, "Previous", true);
        Button next = Arrow(arrows.transform, "Next", false);

        var page = rootGo.AddComponent<MenuSkillPage>();
        page.Configure(rootGo.GetComponent<CanvasGroup>(), cam,
            cam != null ? cam.GetComponent<MenuCameraProtoSwitcher>() : null,
            cam != null ? cam.GetComponent<MenuCameraDirector>() : null,
            cam != null ? cam.GetComponent<MenuSkinView>() : null,
            cam != null ? cam.GetComponent<MenuEmaInspector>() : null,
            cam != null ? cam.GetComponent<MenuSkillLoadout>() : null,
            highlighter, arts.ToArray(), rightShade, panel, loadoutGroup, nameGroup, nameImage, desc, backfireText, stats, slots.ToArray(), filled, empty,
            ringGo.GetComponent<CanvasGroup>(), back);
        page.SetArrows(arrows, previous, next);
        page.SetSlotNote(note, noteLeader, noteText, noteName);
        page.SetComboKeys(keys, leftShade, rows.ToArray(), rowArts.ToArray(), rowEmpty.ToArray(), comboHeader,
            LoadSprite("Menu_keycap"), KeyIcons(), font);
        return page;
    }

    /// <summary>
    /// The loadout's slots on a world-space canvas on the front of the hokora's wooden base, so it takes the scene's
    /// perspective and stays in view when the camera leans in to the hokora. One ensō per hokora slot, ordered left to
    /// right as the slots hang (seen from the skill shot), so each ring sits under its ema. 1 canvas unit = 1 mm.
    /// </summary>
    private static CanvasGroup BuildHokoraSlots(TMP_FontAsset font, Camera cam, List<MenuSkillPage.Slot> slots)
    {
        GameObject hokora = GameObject.Find(HokoraName);
        var loadout = cam != null ? cam.GetComponent<MenuSkillLoadout>() : null;
        if (hokora == null || loadout == null)
        {
            Debug.LogWarning("[MenuTitleHome] Hokora or the loadout not found: the slots are not built.");
            return null;
        }
        Transform h = hokora.transform;
        Vector3 facing = Vector3.ProjectOnPlane(h.forward, Vector3.up).normalized; // the hokora's front faces the skill shot
        Bounds hb = hokora.GetComponent<Renderer>().bounds;
        Vector3 centre = hb.center + facing * SlotsForward;
        centre.y = SlotsHeight;

        var rootGo = new GameObject(SlotsRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var rect = (RectTransform)rootGo.transform;
        rect.sizeDelta = new Vector2(460, 150);
        rect.localScale = Vector3.one * 0.001f;
        // Upright on the panel, facing out of the hokora.
        rect.SetPositionAndRotation(centre, Quaternion.LookRotation(-facing, Vector3.up));

        // Slot order left to right on screen = along the canvas's x (which points to the viewer's right).
        var serialized = new SerializedObject(loadout).FindProperty("slots");
        var order = new List<(int index, float x)>();
        for (int i = 0; i < serialized.arraySize; i++)
        {
            var t = serialized.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            order.Add((i, t != null ? Vector3.Dot(t.position - centre, rect.right) : i));
        }
        order.Sort((a, b) => a.x.CompareTo(b.x));
        var byIndex = new MenuSkillPage.Slot[order.Count];
        for (int k = 0; k < order.Count; k++)
        {
            var slotGo = new GameObject("Slot_" + order[k].index, typeof(RectTransform));
            var slotRect = (RectTransform)slotGo.transform;
            slotRect.SetParent(rect, false);
            slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2((k - (order.Count - 1) * 0.5f) * SlotSpacing * 1000f, 0f);
            slotRect.sizeDelta = new Vector2(140, 140);
            Image backing = Img(slotRect, "Backing", "Menu_dot", Vector2.zero, new Vector2(126, 126), new Color(0.02f, 0.015f, 0.015f, 0.55f));
            backing.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "Menu_dot.png"); // a round disc, not the brush dot (A18)
            Centre(backing.rectTransform, Vector2.zero);
            Image ring = Img(slotRect, "Ring", "Menu_enso_thin", Vector2.zero, new Vector2(140, 140), new Color(1, 1, 1, 0.3f));
            Centre(ring.rectTransform, Vector2.zero);
            Image art = Img(slotRect, "Art", "Menu_title", Vector2.zero, new Vector2(118, 40), Color.white);
            Centre(art.rectTransform, Vector2.zero);
            art.preserveAspect = true;
            TextMeshProUGUI plus = Txt(slotRect, "Plus", "＋", font, 34, Vector2.zero, 0.4f, SpacingNormal);
            plus.alignment = TextAlignmentOptions.Center;
            Centre(plus.rectTransform, Vector2.zero);
            plus.rectTransform.sizeDelta = new Vector2(80, 80);
            byIndex[order[k].index] = new MenuSkillPage.Slot { rect = slotRect, ring = ring, art = art, plus = plus };
        }
        slots.AddRange(byIndex);
        return rootGo.GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Online pages (v0.2 mockups 06 and 07) on their own canvas: the door page at the closed hall door and the lobby
    /// page inside the hall. UI_TitleHome is hidden in both views.
    /// </summary>
    private static MenuDojoPage BuildDojo(TMP_FontAsset font, Camera cam)
    {
        var rootGo = new GameObject(DojoRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // under TEMP_MenuCanvas, so its camera fade covers this UI too
        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)rootGo.transform;

        // ---- door page ----
        CanvasGroup door = Group(root, "Door");
        // Title at the top left; the menu on the door's lower right panel (below the windows, right of the rope).
        // Deep gradients from the edges behind both: top and left for the title, right and bottom for the menu.
        Shade(door.transform, "ShadeTop", false, 0.34f, 0.92f, true);
        Shade(door.transform, "ShadeLeft", true, 0.32f, 0.75f);
        Shade(door.transform, "ShadeRight", true, 0.46f, 0.9f);
        var doorRight = (RectTransform)door.transform.Find("ShadeRight");
        SetEdge(doorRight, UIEdgeShade.Edge.Right);
        doorRight.anchorMin = new Vector2(0.54f, 0f); doorRight.anchorMax = Vector2.one;
        Shade(door.transform, "ShadeBottom", false, 0.45f, 0.92f);
        Txt(door.transform, "Title", "联机道场", font, 30, new Vector2(120, 96), 1f, SpacingWide);
        Txt(door.transform, "Subtitle", "最多六人同场竞速", font, 16, new Vector2(120, 150), 0.85f, SpacingNormal);
        // The menu (items, dot, ink, description) in its own group: it gives way to the join panel.
        CanvasGroup doorMenu = Group(door.transform, "Menu");
        Image doorInk = Img(doorMenu.transform, "Ink", "Menu_ink", new Vector2(1372, 716), new Vector2(0, 12), new Color(1, 1, 1, 0.85f));
        doorInk.type = Image.Type.Sliced;
        Image doorDot = Img(doorMenu.transform, "Dot", "Menu_dot", new Vector2(1354, 703), new Vector2(9, 9), Vermilion, new Vector2(0.5f, 0.5f));
        var doorItems = new List<MenuDojoPage.DoorItem>();
        foreach (var (label, desc, button, opensJoin, soon) in new[]
        {
            ("创建房间", "开门迎客，最多六人", "TEMP_CreateRoomButton", false, false),
            ("加入房间", "凭房号入门，或从列表里挑一个道场", (string)null, true, false),
        })
        {
            // An item that is not open yet carries 即将开放 in T5 after its name (like the settings' 需要重启 tag).
            string shown = soon ? label + ComingSoonTag : label;
            TextMeshProUGUI t = Txt(doorMenu.transform, "Item_" + label, shown, font, 23, new Vector2(1376, 686), 1f, SpacingNormal);
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(shown).x + 8f, 30);
            doorItems.Add(new MenuDojoPage.DoorItem { text = t, description = desc, target = button != null ? Find<Button>(button) : null, opensJoin = opensJoin, comingSoon = soon });
        }
        TextMeshProUGUI doorDesc = Txt(doorMenu.transform, "Description", "", font, 14, new Vector2(1376, 736), 0.85f, SpacingNormal);
        doorDesc.rectTransform.sizeDelta = new Vector2(320, 24);

        // Join panel, in the menu's place: room number field, then the open rooms.
        CanvasGroup join = Group(door.transform, "Join");
        Txt(join.transform, "Title", "加入房间", font, 23, new Vector2(1376, 590), 1f, SpacingNormal);
        Img(join.transform, "TitleInk", "Menu_ink", new Vector2(1372, 620), new Vector2(118, 12), new Color(1, 1, 1, 0.85f)).type = Image.Type.Sliced;
        Txt(join.transform, "CodeLabel", "房号", font, 15, new Vector2(1376, 656), 0.75f, SpacingNormal);
        var codeFieldGo = new GameObject("CodeField", typeof(RectTransform));
        var codeField = (RectTransform)codeFieldGo.transform;
        codeField.SetParent(join.transform, false);
        codeField.anchorMin = codeField.anchorMax = codeField.pivot = new Vector2(0f, 1f);
        codeField.anchoredPosition = new Vector2(1440, -640);
        codeField.sizeDelta = new Vector2(4 * 52, 54);
        var cells = new List<TextMeshProUGUI>();
        var lines = new List<Image>();
        for (int c = 0; c < 4; c++)
        {
            TextMeshProUGUI cell = Txt(codeField, "Cell_" + c, "", font, 28, new Vector2(c * 52, 0), 1f, SpacingNormal);
            cell.alignment = TextAlignmentOptions.Center;
            cell.rectTransform.sizeDelta = new Vector2(40, 44);
            cells.Add(cell);
            lines.Add(Img(codeField, "Line_" + c, "Menu_hairline_short", new Vector2(c * 52, 46), new Vector2(40, 8), new Color(1, 1, 1, 0.45f)));
        }
        Txt(join.transform, "ListLabel", "房间列表", font, 15, new Vector2(1376, 726), 0.75f, SpacingNormal);
        var rooms = new List<MenuDojoPage.JoinRoom>();
        // Example rooms until the open rooms come from the network (MenuDojoPage.JoinRequested is the hook).
        var openRooms = new (string, int)[] { ("明月寺", 3), ("竹林小院", 1), ("听松阁", 5) };
        if (openRooms.Length == 0) Txt(join.transform, "NoRooms", "暂无房间", font, T4, new Vector2(1376, 762), MinTextAlpha, SpacingNormal);
        foreach (var (name, players) in openRooms)
        {
            var rowGo = new GameObject("Room_" + name, typeof(RectTransform), typeof(Image));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(join.transform, false);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(1362, -(756 + rooms.Count * 46));
            row.sizeDelta = new Vector2(300, 40);
            var back = rowGo.GetComponent<Image>();
            back.color = new Color(0.02f, 0.015f, 0.015f, 0f);
            back.raycastTarget = false;
            TextMeshProUGUI nameText = Txt(row, "Name", name, font, 19, Vector2.zero, 0.75f, SpacingNormal);
            nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            nameText.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameText.rectTransform.sizeDelta = new Vector2(200, 30);
            nameText.rectTransform.anchoredPosition = new Vector2(14, 0); // the hover band starts 14 px early; the name is on 1376
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            TextMeshProUGUI info = Txt(row, "Info", "", font, 15, Vector2.zero, 0.6f, SpacingNormal);
            info.rectTransform.anchorMin = info.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            info.rectTransform.pivot = new Vector2(1f, 0.5f);
            info.rectTransform.anchoredPosition = new Vector2(-12, 0);
            info.rectTransform.sizeDelta = new Vector2(100, 30);
            info.alignment = TextAlignmentOptions.MidlineRight;
            rooms.Add(new MenuDojoPage.JoinRoom { name = name, players = players, capacity = 6, row = row, back = back, nameText = nameText, infoText = info });
        }
        TextMeshProUGUI joinHint = Txt(join.transform, "Hint", "", font, 14, new Vector2(1376, 900), 0.7f, SpacingNormal);
        joinHint.rectTransform.sizeDelta = new Vector2(320, 24);
        TextMeshProUGUI doorBack = HintBar(door.transform, font, new[] { ("Back", "esc", "返回") })[0];
        // It belongs to the door menu's column (left edge 1376), not to the screen corner.
        doorBack.alignment = TextAlignmentOptions.MidlineLeft;
        doorBack.rectTransform.anchorMin = doorBack.rectTransform.anchorMax = doorBack.rectTransform.pivot = Vector2.zero;
        doorBack.rectTransform.anchoredPosition = new Vector2(1376, 54);

        // ---- lobby page ----
        CanvasGroup lobbyGroup = Group(root, "Lobby");
        // Deep gradients from the top and bottom edges behind the header and the bottom bar.
        Shade(lobbyGroup.transform, "ShadeTop", false, 0.3f, 0.94f, true);
        Shade(lobbyGroup.transform, "ShadeBottom", false, 0.32f, 0.92f);
        // The room's name at the top left, its number, visibility and head count at the top right, one hairline under both.
        // No room name: the room's number, visibility and head count lead the header on the left.
        // No header line: the room's number stands alone at the top left.
        TextMeshProUGUI roomInfo = Txt(lobbyGroup.transform, "RoomInfo", "", font, 22, new Vector2(120, 88), 0.9f, SpacingNormal);
        roomInfo.rectTransform.sizeDelta = new Vector2(600, 34);
        CanvasGroup seats = Group(lobbyGroup.transform, "Seats");
        TextMeshProUGUI sand = Txt(lobbyGroup.transform, "SandTable", "沙盘　<color=#F4EFE6>枯山水环道</color>", font, 15, Vector2.zero, 0.8f, SpacingNormal);
        sand.alignment = TextAlignmentOptions.Center;
        Centre(sand.rectTransform, Vector2.zero);
        sand.rectTransform.sizeDelta = new Vector2(320, 26);
        Img(lobbyGroup.transform, "Baseline", "Menu_hairline", new Vector2(60, 1006), new Vector2(1800, 12), new Color(1, 1, 1, 0.6f));
        string leaveText = Key("esc") + " 离开房间";
        TextMeshProUGUI leave = Txt(lobbyGroup.transform, "Hint_Leave", leaveText, font, 16, new Vector2(120, 974), 0.85f, SpacingNormal);
        leave.rectTransform.sizeDelta = new Vector2(leave.GetPreferredValues(leaveText).x + 4f, 28);
        var lobbyItems = new List<TextMeshProUGUI>();
        foreach (var (label, x) in new[] { ("形象", 1146f), ("技能", 1236f) })
        {
            TextMeshProUGUI t = Txt(lobbyGroup.transform, "Item_" + label, label, font, 20, new Vector2(x, 970), 0.8f, SpacingWide);
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(label).x + 4f, 32);
            lobbyItems.Add(t);
        }
        var startGo = new GameObject("Start", typeof(RectTransform));
        var start = (RectTransform)startGo.transform;
        start.SetParent(lobbyGroup.transform, false);
        start.anchorMin = start.anchorMax = new Vector2(0f, 1f);
        start.pivot = new Vector2(0.5f, 0.5f);
        start.anchoredPosition = new Vector2(1702, -986); // right edge on 1800, mirroring the 120 margin
        start.sizeDelta = new Vector2(196, 60);
        // The screen's one confirm button: a torn washi tag (A12) with ink text, falling back to the brush bar.
        Sprite tag = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_paper_tag.png");
        Image bar = Img(start, "Bar", "Menu_brushbar", Vector2.zero, new Vector2(196, 60), Color.white);
        if (tag != null) { bar.sprite = tag; bar.type = Image.Type.Sliced; }
        Centre(bar.rectTransform, Vector2.zero);
        TextMeshProUGUI startText = Txt(start, "Label", "开始修行", font, T3, Vector2.zero, 1f, SpacingNormal);
        startText.color = new Color32(0x1E, 0x1A, 0x18, 0xFF);
        startText.alignment = TextAlignmentOptions.Center;
        Centre(startText.rectTransform, new Vector2(3, 0)); // the trailing letter spacing pushes the word left
        startText.rectTransform.sizeDelta = new Vector2(196, 40);
        // While the solo race starts: a thin ensō turning inside the tag, after its text.
        Image spinner = Img(start, "Spinner", "Menu_enso_thin", Vector2.zero, new Vector2(22, 22), new Color32(0x1E, 0x1A, 0x18, 0x00));
        Centre(spinner.rectTransform, new Vector2(76, 0));

        // Solo (only the host in the room): the three difficulties left of the tag, tab style, the current one with
        // its ink on the baseline; a T5 line over the tag (who races, or why the start failed).
        CanvasGroup soloGroup = Group(lobbyGroup.transform, "Solo");
        var difficulty = new List<TextMeshProUGUI>();
        foreach (var (label, x) in new[] { ("简单", 1356f), ("普通", 1436f), ("困难", 1516f) })
        {
            TextMeshProUGUI t = Txt(soloGroup.transform, "Difficulty_" + label, label, font, T3, new Vector2(x, 970), MinTextAlpha, SpacingWide);
            t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(label).x, 32);
            difficulty.Add(t);
        }
        // 练习: no AI, three training dummies on the track (Match/ModeRule Practice). Left of 形象, in the solo group only.
        TextMeshProUGUI practice = Txt(soloGroup.transform, "Practice", "练习", font, 20, new Vector2(1056, 970), 0.65f, SpacingWide);
        practice.rectTransform.sizeDelta = new Vector2(practice.GetPreferredValues("练习").x + 4f, 32);
        Image difficultyInk = Img(soloGroup.transform, "DifficultyInk", "Menu_ink", new Vector2(1352, 1000), new Vector2(0, 12), new Color(1, 1, 1, 0.85f));
        difficultyInk.type = Image.Type.Sliced;
        difficultyInk.pixelsPerUnitMultiplier = 5f;
        TextMeshProUGUI startNote = Txt(lobbyGroup.transform, "StartNote", "", font, T5, Vector2.zero, 0.8f, SpacingNormal);
        startNote.alignment = TextAlignmentOptions.Bottom;
        startNote.rectTransform.anchorMin = startNote.rectTransform.anchorMax = new Vector2(0f, 1f);
        startNote.rectTransform.pivot = new Vector2(0.5f, 0f);
        startNote.rectTransform.sizeDelta = new Vector2(420, 26);
        startNote.rectTransform.anchoredPosition = new Vector2(1702, -948); // centred over the tag

        // A short notice for what is not wired yet.
        TextMeshProUGUI notice = Txt(root, "Notice", "", font, 17, Vector2.zero, 0f, SpacingNormal);
        notice.alignment = TextAlignmentOptions.Center;
        Centre(notice.rectTransform, new Vector2(0, -320));
        notice.rectTransform.sizeDelta = new Vector2(900, 30);

        // Bell rope hint (follows the pointer over the rope; its line fills as the rope is pulled), in this page's style.
        var hintGo = new GameObject("RopeHint", typeof(RectTransform), typeof(CanvasGroup));
        var hint = (RectTransform)hintGo.transform;
        hint.SetParent(root, false);
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
        hint.pivot = new Vector2(0f, 0.5f);
        hint.sizeDelta = new Vector2(230, 84);
        var hintBack = new GameObject("Back", typeof(RectTransform), typeof(Image));
        var hintBackRect = (RectTransform)hintBack.transform;
        hintBackRect.SetParent(hint, false);
        hintBackRect.anchorMin = Vector2.zero; hintBackRect.anchorMax = Vector2.one;
        hintBackRect.offsetMin = new Vector2(-6, 0); hintBackRect.offsetMax = Vector2.zero;
        hintBack.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.015f, 0.62f);
        hintBack.GetComponent<Image>().raycastTarget = false;
        Image hintTick = Img(hint, "Tick", "Menu_hairline_short", new Vector2(-34, 30), new Vector2(30, 8), new Color(1, 1, 1, 0.7f));
        Txt(hint, "Label", "拉动铃绳", font, 19, new Vector2(16, 10), 1f, SpacingNormal);
        Txt(hint, "Sub", "按住往下拉　摇铃开门", font, 13, new Vector2(18, 40), 0.75f, SpacingNormal);
        Img(hint, "Track", "Menu_hairline", new Vector2(16, 64), new Vector2(196, 8), new Color(1, 1, 1, 0.25f));
        Image hintFill = Img(hint, "Fill", "Menu_hairline", new Vector2(16, 64), new Vector2(196, 8), Color.white);
        hintFill.type = Image.Type.Filled;
        hintFill.fillMethod = Image.FillMethod.Horizontal;
        hintFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hintFill.fillAmount = 0f;
        hintGo.GetComponent<CanvasGroup>().alpha = 0f;
        var rope = Object.FindObjectOfType<MenuBellRope>();
        if (rope != null)
        {
            rope.SetHint(hint, hintFill, hintGo.GetComponent<CanvasGroup>());
            EditorUtility.SetDirty(rope);
        }

        var page = rootGo.AddComponent<MenuDojoPage>();
        GameObject sandProp = GameObject.Find("Prop_SandTable_Honden");
        page.Configure(cam, cam != null ? cam.GetComponent<MenuCameraDirector>() : null, Object.FindObjectOfType<MenuRoomView>(),
            Object.FindObjectOfType<MenuLobby>(), Object.FindObjectOfType<MenuSandTableFocus>(),
            sandProp != null ? sandProp.GetComponent<Renderer>() : null, font,
            LoadSprite("Menu_dot"));
        page.SetDoor(door, doorItems.ToArray(), doorDesc, doorDot.rectTransform, doorInk.rectTransform, doorBack);
        page.SetJoin(doorMenu, join, cells.ToArray(), lines.ToArray(), codeField, rooms.ToArray(), joinHint);
        page.SetLobby(lobbyGroup, null, roomInfo, (RectTransform)seats.transform, sand.rectTransform, leave, lobbyItems.ToArray(), start, notice,
            GameObject.Find("TEMP_RoomPanel"),
            new[] { GameObject.Find("TEMP_RoomLeaveButton"), GameObject.Find("TEMP_LobbyCount"), GameObject.Find("TEMP_RopeHint") });
        page.SetSolo(soloGroup, difficulty.ToArray(), difficultyInk.rectTransform, startText, startNote, spinner, practice);
        return page;
    }

    /// <summary>Skin page (v0.2 mockup 04) on its own canvas, shown in the skin close-up of the garden Buddha.</summary>
    private static MenuSkinPage BuildSkinPage(TMP_FontAsset font, Camera cam)
    {
        var rootGo = new GameObject(SkinRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)rootGo.transform;

        // Deep from the left (the names and the arc are there) and along the top and bottom edges.
        Shade(root, "ShadeLeft", true, 0.46f, 0.88f);
        Shade(root, "ShadeTop", false, 0.26f, 0.75f, true);
        Shade(root, "ShadeBottom", false, 0.26f, 0.8f);
        Txt(root, "Title", "皮肤", font, 30, new Vector2(120, 96), 1f, SpacingWide);
        Txt(root, "Subtitle", "挑选佛像形象", font, 15, new Vector2(120, 150), 0.8f, SpacingNormal);
        Img(root, "Arc", "Menu_arc", new Vector2(575, 105), new Vector2(200, 880), new Color(1, 1, 1, 0.85f));
        CanvasGroup items = Group(root, "ArcItems");
        CanvasGroup chosen = Group(root, "Chosen");
        Splash(chosen.transform, "menu_splash_02", new Vector2(265, 614), 380f);
        TextMeshProUGUI big = Txt(chosen.transform, "Name", "", font, 84, new Vector2(120, 560), 1f, SpacingNormal);
        big.rectTransform.sizeDelta = new Vector2(420, 120);
        // The name's calligraphy (900 × 400 art, shown at 300 × 133) in the same place; it replaces the typeset name.
        Image bigArt = Img(chosen.transform, "NameArt", "Menu_dot", new Vector2(115, 548), new Vector2(300, 133), Color.white); // art margin 5 px: ink on 120
        bigArt.sprite = null;
        bigArt.preserveAspect = true;
        bigArt.enabled = false;
        TextMeshProUGUI line = Txt(chosen.transform, "Line", "", font, 18, new Vector2(120, 700), 0.85f, SpacingNormal);
        line.rectTransform.sizeDelta = new Vector2(420, 70);
        line.enableWordWrapping = true;
        TextMeshProUGUI back = HintBar(root, font, new[] { ("Back", "esc", "返回") })[0];

        GameObject turntable = GameObject.Find("SkinTurntable");
        // The skins themselves come from Resources/Match/SelectionCatalog at runtime (shared with the race).
        var skins = new List<MenuSkinPage.Skin>();

        var page = rootGo.AddComponent<MenuSkinPage>();
        page.Configure(rootGo.GetComponent<CanvasGroup>(), cam != null ? cam.GetComponent<MenuSkinView>() : null,
            turntable != null ? turntable.transform : null, skins.ToArray(), (RectTransform)items.transform, font,
            LoadSprite("Menu_dot"), chosen, big, line, back, GameObject.Find("TEMP_SkinBackButton"));
        page.SetNameArt(bigArt);
        page.SetHoverPaper(AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_paper_tag.png")); // the names on the arc
        return page;
    }

    /// <summary>
    /// The confirm dialog (v0.2 mockup 10, page template 4) on its own canvas over every page: a deep gradient from the
    /// right edge and one column centred on x = 1510 (1230 … 1790): title, the text between two hairlines (1250 … 1770),
    /// then the buttons (confirm, an optional second action, the Esc cancel) with one washi tag behind the focused one,
    /// laid out centred by MenuConfirmDialog. Anchored to the
    /// right edge, so it holds on wider screens. Hidden until a page opens it.
    /// </summary>
    private static MenuConfirmDialog BuildConfirm(TMP_FontAsset font)
    {
        var rootGo = new GameObject(ConfirmRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1; // over the menu pages (-1) and TEMP_MenuCanvas
        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)rootGo.transform;

        // Transparent blocker: keeps the pointer off the scene's clickables (and their glow) while the dialog is up.
        var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        var blockerRect = (RectTransform)blocker.transform;
        blockerRect.SetParent(root, false);
        blockerRect.anchorMin = Vector2.zero; blockerRect.anchorMax = Vector2.one; blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        // A light veil over the whole screen (style guide 2: at most 0.2) and one deep gradient from the right edge.
        Veil(root, 0.15f);
        Shade(root, "ShadeRight", true, 0.62f, 0.9f);
        var fade = (RectTransform)root.Find("ShadeRight");
        fade.anchorMin = new Vector2(0.38f, 0f); fade.anchorMax = Vector2.one;
        SetEdge(fade, UIEdgeShade.Edge.Right);

        var columnGo = new GameObject("Column", typeof(RectTransform));
        var column = (RectTransform)columnGo.transform;
        column.SetParent(root, false);
        column.anchorMin = column.anchorMax = new Vector2(1f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        column.anchoredPosition = new Vector2(-(1920f - 1510f), 0f);
        column.sizeDelta = new Vector2(560, 1080);

        TextMeshProUGUI title = Txt(column, "Title", "", font, T1, Vector2.zero, 1f, SpacingWide);
        title.alignment = TextAlignmentOptions.Center;
        OnAxis(title.rectTransform, 360, new Vector2(560, 48), 4f); // the trailing letter spacing pushes the word left
        OnAxis(Img(column, "LineTop", "Menu_hairline", Vector2.zero, new Vector2(520, 12), new Color(1, 1, 1, 0.6f)).rectTransform, 430, new Vector2(520, 12));
        TextMeshProUGUI body = Txt(column, "Body", "", font, T4, Vector2.zero, 0.85f, SpacingNormal);
        body.alignment = TextAlignmentOptions.Center;
        body.enableWordWrapping = true;
        OnAxis(body.rectTransform, 446, new Vector2(520, 104)); // centred between the two lines, one line or two
        OnAxis(Img(column, "LineBottom", "Menu_hairline", Vector2.zero, new Vector2(520, 12), new Color(1, 1, 1, 0.6f)).rectTransform, 554, new Vector2(520, 12));

        var rowGo = new GameObject("Buttons", typeof(RectTransform));
        var row = (RectTransform)rowGo.transform;
        row.SetParent(column, false);
        OnAxis(row, 606, new Vector2(560, 64)); // the tag just wraps the Enter key
        // One torn washi tag (A12, falling back to the brush bar) behind the focused button; MenuConfirmDialog moves it.
        var tagGo = new GameObject("Tag", typeof(RectTransform));
        var tagRect = (RectTransform)tagGo.transform;
        tagRect.SetParent(row, false);
        tagRect.sizeDelta = new Vector2(200, 64);
        Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_paper_tag.png");
        Image tagImage = Img(tagRect, "Bar", "Menu_brushbar", Vector2.zero, Vector2.zero, Color.white);
        if (paper != null) { tagImage.sprite = paper; tagImage.type = Image.Type.Sliced; }
        tagImage.rectTransform.anchorMin = Vector2.zero; tagImage.rectTransform.anchorMax = Vector2.one;
        tagImage.rectTransform.offsetMin = tagImage.rectTransform.offsetMax = Vector2.zero;
        // The buttons, all alike (T3): confirm, the optional second action, and the cancel (Esc).
        var labels = new List<TextMeshProUGUI>();
        foreach (string name in new[] { "Confirm", "Alt", "Cancel" })
        {
            TextMeshProUGUI t = Txt(row, "Button_" + name, "", font, T3, Vector2.zero, MinTextAlpha, SpacingNormal);
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(120, 64);
            labels.Add(t);
        }

        var dialog = rootGo.AddComponent<MenuConfirmDialog>();
        dialog.Configure(rootGo.GetComponent<CanvasGroup>(), column, title, body, row, tagRect, labels[0], labels[1], labels[2]);
        return dialog;
    }

    /// <summary>Centres a rect on its parent's vertical axis, its top `top` px below the parent's top (shifted by `x`).</summary>
    private static void OnAxis(RectTransform rt, float top, Vector2 size, float x = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -top);
        rt.sizeDelta = size;
    }

    /// <summary>A brush chevron button (the sprite points right; the left one is mirrored).</summary>
    private static Button Arrow(Transform parent, string name, bool left)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(34, 58);
        var img = go.GetComponent<Image>();
        // The final art has its own left chevron (drawn, not mirrored); the placeholder is turned round.
        Sprite drawnLeft = left ? AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_chevron_l.png") : null;
        img.sprite = drawnLeft != null ? drawnLeft : LoadSprite("Menu_chevron");
        img.color = new Color(1, 1, 1, 0.75f);
        img.raycastTarget = true;
        if (left && drawnLeft == null) img.transform.localEulerAngles = new Vector3(0, 0, 180);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        return button;
    }

    /// <summary>
    /// Removes the importer's proto panel (shot buttons, Start, 皮肤) from the screen. Three of its buttons are still the
    /// actions behind the home menu items and the hall / Buddha click shortcuts (MenuClickShortcuts, MenuSkinView and
    /// MenuCameraProtoSwitcher press or listen to them), so they move into MenuActions, an invisible holder on
    /// TEMP_MenuCanvas that takes no clicks; the rest of the panel is deleted. Tools/Menu Environment/2 Build Scene
    /// brings the panel back, so run this builder again after it.
    /// </summary>
    private static void RemoveProtoPanel()
    {
        GameObject panel = GameObject.Find("TEMP_CameraProtoPanel");
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        if (panel == null || canvas == null) return;
        Transform holder = canvas.transform.Find("MenuActions");
        if (holder == null)
        {
            var go = new GameObject("MenuActions", typeof(RectTransform), typeof(CanvasGroup));
            holder = go.transform;
            holder.SetParent(canvas.transform, false);
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = true; // the buttons must stay pressable from code
        }
        foreach (string name in new[] { "Btn_技能选择", "TEMP_StartButton", "TEMP_SkinButton" })
        {
            Transform button = panel.transform.Find(name);
            if (button != null) button.SetParent(holder, false);
        }
        Object.DestroyImmediate(panel);
    }

    private const string KeySpritesPath = "Assets/UI/MainMenu/MenuKeys.asset";
    private const int KeyCell = 128;

    /// <summary>A drawn key icon inline in text (asset K01/K02, e.g. "esc", "enter", "left"), tinted like the text.</summary>
    private static string Key(string key) => "<sprite name=\"key_" + key + "\" tint=1>";

    /// <summary>The drawn key icons (Art/key_*.png).</summary>
    private static Sprite[] KeyIcons()
    {
        var icons = new List<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("key_ t:Sprite", new[] { ArtDir.TrimEnd('/') }))
            if (AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)) is Sprite s) icons.Add(s);
        return icons.ToArray();
    }

    /// <summary>
    /// Packs the drawn key icons into a TMP sprite asset, so hints can show them inline with
    /// &lt;sprite name="key_esc"&gt;. Each icon sits on the text's baseline at the height of the font's ascender, a little below it.
    /// </summary>
    private static TMP_SpriteAsset BuildKeySprites()
    {
        var files = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("key_ t:Texture2D", new[] { ArtDir.TrimEnd('/') }))
            files.Add(AssetDatabase.GUIDToAssetPath(guid));
        if (files.Count == 0) return null;
        files.Sort();
        const int columns = 4;
        int rows = (files.Count + columns - 1) / columns;
        var atlas = new Texture2D(columns * KeyCell, rows * KeyCell, TextureFormat.RGBA32, false) { name = "MenuKeys Atlas" };
        atlas.SetPixels32(new Color32[atlas.width * atlas.height]);

        AssetDatabase.DeleteAsset(KeySpritesPath);
        var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        asset.name = "MenuKeys";
        for (int i = 0; i < files.Count; i++)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(System.IO.File.ReadAllBytes(files[i]));
            int x = (i % columns) * KeyCell, y = (rows - 1 - i / columns) * KeyCell;
            atlas.SetPixels(x, y, KeyCell, KeyCell, source.GetPixels(0, 0, KeyCell, KeyCell));
            Object.DestroyImmediate(source);
            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = new GlyphMetrics(KeyCell, KeyCell, 0, KeyCell * 0.75f, KeyCell * 1.08f),
                glyphRect = new GlyphRect(x, y, KeyCell, KeyCell),
                scale = 1.7f, // about 30 px beside 16 px text, so the key names stay legible
                atlasIndex = 0,
            };
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = System.IO.Path.GetFileNameWithoutExtension(files[i]) });
        }
        atlas.Apply(false);
        asset.spriteSheet = atlas;
        var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "MenuKeys Material" };
        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        asset.material = material;
        AssetDatabase.CreateAsset(asset, KeySpritesPath);
        AssetDatabase.AddObjectToAsset(atlas, asset);
        AssetDatabase.AddObjectToAsset(material, asset);
        // Marks the tables as current; otherwise TMP "upgrades" the asset on load and clears them.
        var so = new SerializedObject(asset);
        so.FindProperty("m_Version").stringValue = "1.1.0";
        so.ApplyModifiedPropertiesWithoutUndo();
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>
    /// The key hint bar (style guide 4): hints in a row, left to right, ending at the bottom-right corner of the safe
    /// area, anchored there so it holds on any aspect. Each hint is a drawn key and its action in T5.
    /// </summary>
    private static TextMeshProUGUI[] HintBar(Transform parent, TMP_FontAsset font, (string name, string key, string action)[] hints)
    {
        const float right = 96f, bottom = 54f, gap = 40f;
        var texts = new TextMeshProUGUI[hints.Length];
        float x = right;
        for (int i = hints.Length - 1; i >= 0; i--)
        {
            string text = Key(hints[i].key) + " " + hints[i].action;
            TextMeshProUGUI t = Txt(parent, "Hint_" + hints[i].name, text, font, T5, Vector2.zero, 0.8f, SpacingNormal);
            t.alignment = TextAlignmentOptions.MidlineRight;
            float w = t.GetPreferredValues(text).x + 4f;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(1f, 0f);
            t.rectTransform.sizeDelta = new Vector2(w, 34);
            t.rectTransform.anchoredPosition = new Vector2(-x, bottom);
            x += w + gap;
            texts[i] = t;
        }
        return texts;
    }

    private static T Find<T>(string name) where T : Component
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<T>() : null;
    }

    private static CanvasGroup Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.GetComponent<CanvasGroup>();
    }

    /// <summary>Image placed by its top-left corner in mockup pixels (y down), or by `pivot` when given.</summary>
    private static Image Img(Transform parent, string name, string sprite, Vector2 mockup, Vector2 size, Color color, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot ?? new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(mockup.x, -mockup.y);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(sprite);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Type scale (Docs/ui/style-guide.md 3): T1 page title … T5 key hints, the smallest size in the game.
    private const float T1 = 30f, T2 = 26f, T3 = 22f, T4 = 18f, T5 = 16f;
    // Letter spacing, in TMP units (1/100 em): wide for titles and menu items, normal for text and sentences.
    private const float SpacingWide = 25f, SpacingNormal = 8f;
    // Lowest alpha for text that is shown at rest (secondary, unselected).
    private const float MinTextAlpha = 0.65f;

    /// <summary>The style guide's tier nearest to a layout size; sizes above 40 are display type and stay.</summary>
    private static float Tier(float size) =>
        size > 40f ? size : size >= 29f ? T1 : size >= 24f ? T2 : size >= 20f ? T3 : size >= 17f ? T4 : T5;

    private static TMP_SpriteAsset _keySprites;
    /// <summary>Marks what is not open yet, in T5 after the item's name (style guide 6: unavailable says why).</summary>
    private const string ComingSoonTag = "<size=73%>　即将开放</size>";

    private static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Vector2 mockup, float alpha, float spacing)
    {
        // Every text snaps to the type scale. Spacing is SpacingWide (page titles, tabs, navigation) or SpacingNormal;
        // only the Latin wordmark has its own.
        size = Tier(size);
        if (alpha > 0f && alpha < MinTextAlpha) alpha = MinTextAlpha;
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(mockup.x, -mockup.y);
        rt.sizeDelta = new Vector2(600, size * 1.6f);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (_keySprites != null) t.spriteAsset = _keySprites;
        t.text = text;
        t.fontSize = size;
        t.characterSpacing = spacing;
        t.color = new Color(Paper.r, Paper.g, Paper.b, alpha);
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void BottomLeft(RectTransform rt, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    private static void Centre(RectTransform rt, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }

    private static void Veil(Transform parent, float alpha)
    {
        var go = new GameObject("Veil", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.04f, 0.03f, 0.03f, alpha);
        img.raycastTarget = false;
    }

    /// <summary>Gradient darkening from one screen edge: `extent` of the screen, `strength` alpha at the edge.</summary>
    private static void Shade(Transform parent, string name, bool fromLeft, float extent, float strength, bool fromTop = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (fromLeft) { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(extent, 1f); }
        else if (fromTop) { rt.anchorMin = new Vector2(0f, 1f - extent); rt.anchorMax = Vector2.one; }
        else { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, extent); }
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.04f, 0.03f, 0.03f, strength);
        img.raycastTarget = false;
        UIEdgeShade.Edge edge = fromLeft ? UIEdgeShade.Edge.Left : fromTop ? UIEdgeShade.Edge.Top : UIEdgeShade.Edge.Bottom;
        go.AddComponent<UIEdgeShade>().From = edge;
        Grain(go.transform, edge);
    }

    private const string EmaTextureDir = "Assets/Sources/MenuEnvironment/Textures/Source/";
    private const string EmaMaterialDir = "Assets/Sources/MenuEnvironment/Materials/";

    /// <summary>
    /// Gives each skill ema its own copy of the shared wood material (slot 0, M_Ema_A), whose texture has the
    /// skill's text inked on the board's back (T_Ema_&lt;mesh suffix&gt;_Back.png, written by ArtSource/MainMenu/ema_back.py).
    /// The textures get the shared texture's import settings. An ema without its texture keeps the shared material.
    /// </summary>
    private static void ApplyEmaBacks(Camera cam)
    {
        var loadout = cam != null ? cam.GetComponent<MenuSkillLoadout>() : null;
        if (loadout == null) return;
        var source = (TextureImporter)AssetImporter.GetAtPath(EmaTextureDir + "T_Ema_A_BaseColor.png");
        var emas = new SerializedObject(loadout).FindProperty("emas");
        for (int i = 0; i < emas.arraySize; i++)
        {
            var item = emas.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue as Transform;
            var renderer = item != null ? item.GetComponent<Renderer>() : null;
            var mesh = item != null ? item.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (renderer == null || mesh == null || !mesh.name.StartsWith("SM_Ema_")) continue;
            string suffix = mesh.name.Substring("SM_Ema_".Length);
            string texPath = EmaTextureDir + "T_Ema_" + suffix + "_Back.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (importer == null) { Debug.LogWarning("[MenuTitleHome] No ema back texture " + texPath); continue; }
            if (source != null)
            {
                var settings = new TextureImporterSettings();
                source.ReadTextureSettings(settings);
                importer.SetTextureSettings(settings);
                importer.maxTextureSize = source.maxTextureSize;
                importer.textureCompression = source.textureCompression;
                importer.SaveAndReimport();
            }
            Material[] mats = renderer.sharedMaterials;
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(EmaMaterialDir + "M_Ema_A.mat");
            if (wood == null) wood = mats[0];
            string matPath = EmaMaterialDir + "M_Ema_" + suffix + "_Back.mat";
            var back = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (back == null)
            {
                back = new Material(wood);
                AssetDatabase.CreateAsset(back, matPath);
            }
            else back.CopyPropertiesFromMaterial(wood);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            back.SetTexture("_BaseMap", tex);
            back.mainTexture = tex;
            EditorUtility.SetDirty(back);
            mats[0] = back;
            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// The menu's grade (style guide 1, "the scene is the background"): a global volume above the scene's own that
    /// lowers saturation and exposure a little and darkens the corners, so the world steps back behind the UI.
    /// The shared RaceMap dusk profile is not touched.
    /// </summary>
    private static void BuildGrade()
    {
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(GradeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradeProfilePath);
            var colour = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            colour.saturation.Override(-14f);
            colour.postExposure.Override(-0.12f);
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color32(0x0A, 0x08, 0x08, 0xFF));
            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        var go = new GameObject(GradeRootName);
        var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    /// <summary>A faint ink splash (asset A14) behind a calligraphy name, centred on `centre` (mockup pixels, y down).</summary>
    private static void Splash(Transform parent, string art, Vector2 centre, float size)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + art + ".png");
        if (sprite == null) return;
        Image img = Img(parent, "Splash", "Menu_dot", centre, new Vector2(size, size), new Color(Paper.r, Paper.g, Paper.b, 0.13f), new Vector2(0.5f, 0.5f));
        img.sprite = sprite;
        img.preserveAspect = true;
    }

    /// <summary>How strongly the paper fibres (asset A13) show over a dark gradient or panel.</summary>
    private const float GrainAlpha = 0.018f; // UI blends in linear space: small values already read strongly

    /// <summary>
    /// Paper fibres over a dark gradient or panel, filling it and tiled at the art's display size; fading from `edge`
    /// like the gradient, or even when `edge` is null. A shade's grain follows its edge when the edge is changed later.
    /// </summary>
    private static void Grain(Transform shade, UIEdgeShade.Edge? edge)
    {
        Sprite fibre = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "menu_paper_fibre.png");
        if (fibre == null) return;
        var go = new GameObject("Grain", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(shade, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = fibre;
        img.type = Image.Type.Tiled;
        img.color = new Color(Paper.r, Paper.g, Paper.b, GrainAlpha);
        img.raycastTarget = false;
        if (edge.HasValue) go.AddComponent<UIEdgeShade>().From = edge.Value;
    }

    /// <summary>Turns a shade made by Shade() to fade from another edge, with its grain.</summary>
    private static void SetEdge(RectTransform shade, UIEdgeShade.Edge edge)
    {
        foreach (UIEdgeShade s in shade.GetComponentsInChildren<UIEdgeShade>()) s.From = edge;
    }

    private static void ImportSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("Menu_ t:Texture2D", new[] { SpriteDir.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            // The ink keeps its start cap and dry tail while its body stretches.
            ti.spriteBorder = path.EndsWith("Menu_ink.png") ? new Vector4(40, 0, 220, 0)
                            : path.EndsWith("Menu_keycap.png") ? new Vector4(16, 16, 16, 16) : Vector4.zero;
            ti.SaveAndReimport();
        }
        ImportArt();
    }

    /// <summary>Final art: 2× sprites at 200 pixels per unit, with mipmaps (they are shown at half size or less).</summary>
    private static void ImportArt()
    {
        if (!AssetDatabase.IsValidFolder(ArtDir.TrimEnd('/'))) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool cursor = path.EndsWith("menu_cursor.png");
            ti.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Sprite;
            if (!cursor)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 200f;
                // The ink keeps its pressed start and dry tail while its body stretches (compose.py: 160 + bodies + 480).
                // The paper tag (A12) keeps its torn edges while its even middle stretches.
                ti.spriteBorder = path.EndsWith("menu_ink_full.png") ? new Vector4(160, 0, 480, 0)
                                : path.EndsWith("menu_paper_tag.png") ? new Vector4(70, 34, 70, 34) : Vector4.zero;
            }
            bool tiled = path.EndsWith("menu_paper_grain.png") || path.EndsWith("menu_paper_fibre.png");
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = !cursor && !tiled;
            ti.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            if (cursor) ti.maxTextureSize = 32; // a hardware cursor is shown at its pixel size
            ti.SaveAndReimport();
        }
    }

    private static TMP_FontAsset LoadOrCreateFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        const string chars = "BUDDAHGOMCSTIN按任意键开始单人修行联机道场预设置离去与五位同竞速创建或加入房间形象技能画面声音操作游戏本殿点击进皮肤绘马墙前配挑选佛像尚未开放有未应用的改动按与预设不同后生效并保存在本机恢复返回音频操作需要重启标的将游戏自定义加入房间凭房号入门或从列表里挑一个道场明月寺竹林小院听松阁直接键入删除这个输四位挑选佛像形象白玉温润如静而不争银身通体辉映照四方雪岩色纹沉稳山织石交错细密锦赭红脉暗藏火已拥有按住拖动可旋转拉动铃绳按住往下摇铃开门挑三面挂入小祠细看装配点圆相取下已短暂一段时间自身减速布下区域碰到对手会被先住随后黑幕笼罩全场看不清赛道和其他人只遮住视野放大形态能把撞开缩小向前方发出推开挡路身后反向让所有左右转向颠倒反噬冷却秒持续拖动翻看空白处挂回右键收起↑↓←→联机道场最多六人同场竞速创建房间开门迎客加入好友去开着的列表看看哪些门输入房号凭四位入离开清风禅院号公开一二三四五六沙盘枯山水环道修行尚未接入流程比赛你主云游僧无尘明心慧觉了凡空位比赛中依次按出一槽的按键就放出这一槽的技能之间别停顿太久空祠总音量乐效切到后台时静开关默认改动立即今日到此为止？留下你是房主，会解散道友们也回殿门。未应用这些继续调整放弃执念每上升越高容易结束内再涨该版锁部靠摩擦滑固鼠掌施展练习带木桩玩家";
        if (existing != null)
        {
            existing.TryAddCharacters(chars);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = "MenuSerif SDF";
        AssetDatabase.CreateAsset(fa, FontAssetPath);
        fa.atlasTextures[0].name = "MenuSerif SDF Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = "MenuSerif SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(chars);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}
