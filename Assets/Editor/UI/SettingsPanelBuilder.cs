// Builds the main menu's settings panel (Canvas/MainMenuRoot/SettingsPanel) and the "设置" button on the home panel in
// Assets/Scenes/MainMenu.unity, in the same plain uGUI style as the other panels (white Image with alpha 0.392,
// white Image buttons with a TMP label). Re-running it replaces the panel and the button and re-wires MainMenuUI.
// Menu: Tools/BuddahGo/UI/Build Settings Panel. Saves the scene.
using System.Linq;
using SteamMultiplayer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BuddahGo.EditorTools.UI
{
    public static class SettingsPanelBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string CjkFontGuid = "d1435b2966a18f545980c7420362dc97";   // Assets/Fonts/SoloCJK/BuddahGoSoloCJK.asset
        static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.392f);
        static readonly Color ButtonColor = Color.white;
        static readonly Color TextColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        [MenuItem("Tools/BuddahGo/UI/Build Settings Panel")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var menu = Object.FindObjectOfType<MainMenuUI>(true);
            if (menu == null)
                return "FAILED: no MainMenuUI in " + ScenePath;
            var root = menu.HomePanel.transform.parent;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(CjkFontGuid));
            if (font == null)
                return "FAILED: CJK font asset " + CjkFontGuid;

            // Panel, sized like the other panels (they stretch to the root).
            var existing = root.Find("SettingsPanel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            // The other panels stretch to MainMenuRoot (448 x 281 units, canvas pixels) plus a sizeDelta; the same here,
            // taller because of the option list. Children are placed in canvas pixels inside that rect.
            var panel = Image("SettingsPanel", root, PanelColor);
            Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);
            panel.rectTransform.sizeDelta = new Vector2(1000f, 560f);   // 1448 x 841 px; MainMenuRoot sits 79 px above the centre

            var title = Label("Title", panel.transform, font, "设置", 44, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(600f, 70f));

            var section = Label("SectionGraphics", panel.transform, font, "画面", 32, TextAlignmentOptions.Left);
            Anchor(section.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -84f), new Vector2(300f, 50f));

            // Preset row: label + one button per preset (instantiated at runtime from the template).
            var presetLabel = Label("PresetLabel", panel.transform, font, "预设：", 28, TextAlignmentOptions.Left);
            Anchor(presetLabel.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -136f), new Vector2(300f, 44f));
            var presetButtons = new GameObject("PresetButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            presetButtons.SetParent(panel.transform, false);
            Anchor(presetButtons, new Vector2(0f, 1f), new Vector2(380f, -136f), new Vector2(700f, 44f));
            var presetLayout = presetButtons.GetComponent<HorizontalLayoutGroup>();
            presetLayout.spacing = 12f;
            presetLayout.childForceExpandWidth = false;
            presetLayout.childForceExpandHeight = true;
            presetLayout.childAlignment = TextAnchor.MiddleLeft;
            var presetTemplate = TextButton("PresetButtonTemplate", presetButtons, font, "preset", 24, new Vector2(150f, 44f));
            presetTemplate.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;

            // Scrollable option rows.
            var scroll = Image("Rows", panel.transform, new Color(1f, 1f, 1f, 0.25f));
            Anchor(scroll.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(1320f, 480f));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            var viewport = Image("Viewport", scroll.transform, Color.white);
            Stretch(viewport.rectTransform, 8f, 8f, 8f, 8f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            var header = Label("GroupHeaderTemplate", content, font, "group", 26, TextAlignmentOptions.Left);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            header.fontStyle = FontStyles.Bold;

            var row = new GameObject("RowTemplate", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            row.GetComponent<LayoutElement>().preferredHeight = 46f;
            var rowLabel = Label("Label", row, font, "option", 26, TextAlignmentOptions.Left);
            Anchor(rowLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(420f, 42f));
            rowLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            var previous = TextButton("Previous", row, font, "<", 26, new Vector2(48f, 42f));
            Anchor((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(560f, 0f), new Vector2(48f, 42f));
            var value = Label("Value", row, font, "level", 26, TextAlignmentOptions.Center);
            Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(620f, 0f), new Vector2(450f, 42f));   // between the two buttons
            var next = TextButton("Next", row, font, ">", 26, new Vector2(48f, 42f));
            Anchor((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(1080f, 0f), new Vector2(48f, 42f));

            var status = Label("Status", panel.transform, font, "", 22, TextAlignmentOptions.Center);
            Anchor(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(1300f, 32f));

            var apply = TextButton("Apply", panel.transform, font, "应用", 26, new Vector2(180f, 52f));
            Anchor((RectTransform)apply.transform, new Vector2(0.5f, 0f), new Vector2(-300f, 24f), new Vector2(180f, 52f));
            var revert = TextButton("Revert", panel.transform, font, "撤销", 26, new Vector2(180f, 52f));
            Anchor((RectTransform)revert.transform, new Vector2(0.5f, 0f), new Vector2(-100f, 24f), new Vector2(180f, 52f));
            var reset = TextButton("ResetToPreset", panel.transform, font, "恢复预设", 26, new Vector2(180f, 52f));
            Anchor((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(100f, 24f), new Vector2(180f, 52f));
            var back = TextButton("Back", panel.transform, font, "返回", 26, new Vector2(180f, 52f));
            Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(300f, 24f), new Vector2(180f, 52f));

            var ui = panel.gameObject.AddComponent<SettingsPanelUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("presetLabel").objectReferenceValue = presetLabel;
            so.FindProperty("presetButtonsParent").objectReferenceValue = presetButtons;
            so.FindProperty("presetButtonTemplate").objectReferenceValue = presetTemplate;
            so.FindProperty("rowsParent").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = row.gameObject;
            so.FindProperty("groupHeaderTemplate").objectReferenceValue = header.gameObject;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("applyButton").objectReferenceValue = apply;
            so.FindProperty("revertButton").objectReferenceValue = revert;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("mainMenu").objectReferenceValue = menu;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);

            // Home button, below Quit, in the home panel's style, wired to MainMenuUI.ShowSettings.
            var home = menu.HomePanel.transform;
            var old = home.Find("Settings");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var quit = home.Find("Quit");
            var settingsButton = TextButton("Settings", home, font, "设置", 20, new Vector2(160f, 30f));
            var settingsRect = (RectTransform)settingsButton.transform;
            if (quit != null)
            {
                var quitRect = (RectTransform)quit;
                settingsRect.anchorMin = quitRect.anchorMin;
                settingsRect.anchorMax = quitRect.anchorMax;
                settingsRect.pivot = quitRect.pivot;
                settingsRect.sizeDelta = quitRect.sizeDelta;
                settingsRect.localScale = quitRect.localScale;
                settingsRect.anchoredPosition = quitRect.anchoredPosition + new Vector2(0f, -(quitRect.sizeDelta.y + 10f));
                var quitImage = quit.GetComponent<Image>();
                var image = settingsButton.GetComponent<Image>();
                if (quitImage != null && image != null)
                {
                    image.sprite = quitImage.sprite;
                    image.color = quitImage.color;
                    image.type = quitImage.type;
                }
                var quitText = quit.GetComponentInChildren<TMP_Text>();
                var text = settingsButton.GetComponentInChildren<TMP_Text>();
                if (quitText != null && text != null)
                {
                    text.fontSize = quitText.fontSize;
                    text.color = quitText.color;
                }
                settingsButton.transform.SetSiblingIndex(quit.GetSiblingIndex());
            }
            UnityEventTools.AddPersistentListener(settingsButton.onClick, menu.ShowSettings);

            var menuSo = new SerializedObject(menu);
            var settingsProperty = menuSo.FindProperty("settingsPanel");
            if (settingsProperty != null)
                settingsProperty.objectReferenceValue = panel.gameObject;
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            return "built SettingsPanel and the home Settings button; saved=" + saved;
        }

        static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = TextColor;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;   // Noto CJK lines are about 1.45 em tall; Ellipsis would hide a label whose rect is a little short
            return label;
        }

        static Button TextButton(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 sizeDelta)
        {
            var image = Image(name, parent, ButtonColor);
            image.rectTransform.sizeDelta = sizeDelta;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label("Text (TMP)", image.transform, font, text, size, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            return button;
        }

        static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
