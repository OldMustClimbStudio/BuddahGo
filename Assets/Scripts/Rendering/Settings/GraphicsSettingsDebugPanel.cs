using System.Linq;
using UnityEngine;

namespace BuddahGo.Rendering.Settings
{
    // Temporary in-game graphics panel, toggled with F9, for comparing settings in a player build until the settings menu
    // exists. It drives GraphicsSettingsService the way the menu will: every change applies and saves at once. IMGUI, so
    // it needs no font asset (the OS font covers the Chinese labels) and works with either input backend.
    // Remove it, or limit it to development builds, when the settings menu ships (Docs/rendering/graphics-settings.md).
    public sealed class GraphicsSettingsDebugPanel : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;
        static readonly string[] s_Groups = { "display", "quality", "effects", "performance" };

        bool _open;
        Vector2 _scroll;
        CursorLockMode _previousLock;
        bool _previousVisible;
        float _fps, _fpsTime;
        int _fpsFrames;

        public static void Install()
        {
            if (FindAnyObjectByType<GraphicsSettingsDebugPanel>() != null)
                return;
            var go = new GameObject(nameof(GraphicsSettingsDebugPanel));
            DontDestroyOnLoad(go);
            go.AddComponent<GraphicsSettingsDebugPanel>();
        }

        void Update()
        {
            _fpsFrames++;
            _fpsTime += Time.unscaledDeltaTime;
            if (_fpsTime >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTime;
                _fpsFrames = 0;
                _fpsTime = 0f;
            }
            // Races hide and lock the cursor; keep it usable while the panel is open.
            if (_open && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                _previousLock = Cursor.lockState;
                _previousVisible = Cursor.visible;
            }
            else
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousVisible;
            }
        }

        void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F9)
            {
                Toggle();
                e.Use();
            }
            if (!_open || !GraphicsSettingsService.IsReady)
                return;

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(16f, 16f, 560f, ReferenceHeight - 32f), GUI.skin.box);
            GUILayout.Label("画面设置（F9 关闭）— 临时调试面板，正式设置菜单未做");
            GUILayout.Label(string.Format("{0:0} fps   VSync {1}   帧率上限 {2}   {3}x{4} {5}   {6}",
                _fps, QualitySettings.vSyncCount, Application.targetFrameRate, Screen.width, Screen.height, Screen.fullScreenMode,
                QualitySettings.names[QualitySettings.GetQualityLevel()]));

            var catalog = GraphicsSettingsService.Catalog;
            GUILayout.BeginHorizontal();
            GUILayout.Label("预设：" + Text(GraphicsSettingsService.PresetLabelKey, GraphicsSettingsService.PresetId), GUILayout.Width(150f));
            foreach (var preset in catalog.Presets)
            {
                bool current = !GraphicsSettingsService.IsCustom && GraphicsSettingsService.PresetId == preset.Id;
                if (GUILayout.Toggle(current, Text(preset.LabelKey, preset.Id), GUI.skin.button) && !current)
                {
                    GraphicsSettingsService.SetPreset(preset.Id);
                    GraphicsSettingsService.Apply();
                }
            }
            if (GUILayout.Button("恢复预设"))
            {
                GraphicsSettingsService.ResetToPreset();
                GraphicsSettingsService.Apply();
            }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var option in catalog.Options.OrderBy(o => System.Array.IndexOf(s_Groups, o.GroupId)).ThenBy(o => o.SortOrder))
            {
                var levels = option.Levels.Where(l => GraphicsSettingsService.IsAvailable(option.Id, l.Id)).ToList();
                if (levels.Count == 0)
                    continue;
                int index = Mathf.Max(0, levels.FindIndex(l => l.Id == GraphicsSettingsService.Get(option.Id)));
                GUILayout.BeginHorizontal();
                GUILayout.Label(Text(option.LabelKey, option.Id), GUILayout.Width(170f));
                int next = index;
                if (GUILayout.Button("<", GUILayout.Width(32f)))
                    next = (index + levels.Count - 1) % levels.Count;
                string label = Text(levels[index].LabelKey, levels[index].Id) + (GraphicsSettingsService.IsOverridden(option.Id) ? "  *" : string.Empty);
                GUILayout.Label(label, GUILayout.Width(220f));
                if (GUILayout.Button(">", GUILayout.Width(32f)))
                    next = (index + 1) % levels.Count;
                GUILayout.EndHorizontal();
                if (next != index)
                {
                    GraphicsSettingsService.Set(option.Id, levels[next].Id);
                    GraphicsSettingsService.Apply();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("* 与预设不同。改动立即生效并保存在本机。");
            GUILayout.EndArea();
        }

        static string Text(string key, string fallback) => DesignTableOverlay.Text(key, fallback);
    }
}
