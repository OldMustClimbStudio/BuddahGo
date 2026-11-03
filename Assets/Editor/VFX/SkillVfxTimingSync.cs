using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // 时长同步 (Docs/vfx/README.md section 4): every skill effect gets its length from the gameplay values of the
    // design tables, and a slot rescales its content by live length / reference length (the length it was tuned at).
    // This lists, per skill, the length each effect gets in the race now, the slot's reference and what the slot does
    // with the difference, so a balance change in Skill.xlsx shows at once which effects it speeds up or slows down.
    public static class SkillVfxTimingSync
    {
        public const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";
        public const string VfxDatabaseAsset = "Assets/Skills_Assets/SkillVfxDatabase.asset";
        public const string CastVfxId = "cast";
        public const string CastAntiVfxId = "cast_anti";
        // Beyond this the content runs at under half or over double its tuned speed: worth re-tuning at the new length.
        public const float RetuneBelow = 0.5f;
        public const float RetuneAbove = 2f;

        public class LayerScale
        {
            public string name;
            public SkillVfxTimeScaling scaling;
            public float k;
        }

        public class Row
        {
            public string skillId;
            public string vfxId;
            public float seconds;
            public string source;
            public bool overridden;
            public GameObject prefab;
            public SkillVfxPhaseController slot;
            public float reference;
            public readonly List<LayerScale> layers = new List<LayerScale>();

            public bool InSync => slot != null && reference > 0f && Mathf.Abs(seconds - reference) < 0.01f;
            public bool NeedsRetune => layers.Any(l => l.scaling != SkillVfxTimeScaling.Fixed && (l.k < RetuneBelow || l.k > RetuneAbove));
        }

        // Rows at the current design tables: skill SOs are copied and the tables applied onto the copies, as in play mode.
        public static List<Row> Build()
        {
            var rows = new List<Row>();
            var source = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            var vfx = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(VfxDatabaseAsset);
            if (source == null)
                return rows;

            var temp = new List<Object>();
            var scratch = ScriptableObject.CreateInstance<SkillDatabase>();
            temp.Add(scratch);
            try
            {
                var copies = new List<SkillAction>();
                Copy(source.normalSkills, scratch.normalSkills, copies, temp);
                Copy(source.antiSkills, scratch.antiSkills, copies, temp);
                Copy(source.skills, scratch.skills, copies, temp);
                scratch.ApplyDesignTablesNow();

                var slots = new List<SkillAction.VfxSlotTiming>();
                foreach (SkillAction skill in copies)
                {
                    bool anti = skill.skillId.EndsWith("_anti");
                    slots.Clear();
                    slots.Add(new SkillAction.VfxSlotTiming
                    {
                        vfxId = anti ? CastAntiVfxId : CastVfxId,
                        seconds = CastConfirmDelay(skill.skillId),
                        source = "Skill.castConfirmDelaySeconds",
                    });
                    skill.CollectVfxSlots(slots);
                    foreach (SkillAction.VfxSlotTiming timing in slots)
                        rows.Add(MakeRow(skill.skillId, timing, vfx));
                }
            }
            finally
            {
                foreach (Object o in temp)
                    Object.DestroyImmediate(o);
            }
            return rows;
        }

        // Rows of one effect, for the timing preview of its prefab.
        public static List<Row> ForVfx(string vfxId)
        {
            return Build().Where(r => r.vfxId == vfxId).ToList();
        }

        // "I tuned this effect at this length": the slot's Imp reference becomes the live length, so k is 1 again.
        public static void SetReference(Row row)
        {
            if (row.slot == null)
                return;
            var so = new SerializedObject(row.slot);
            SerializedProperty references = so.FindProperty("referenceSeconds");
            int index = -1;
            for (int i = 0; i < references.arraySize; i++)
            {
                if (references.GetArrayElementAtIndex(i).FindPropertyRelative("phase").enumValueIndex == (int)SkillVfxPhase.Imp)
                    index = i;
            }
            if (index < 0)
            {
                index = references.arraySize;
                references.arraySize++;
                references.GetArrayElementAtIndex(index).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
            }
            references.GetArrayElementAtIndex(index).FindPropertyRelative("seconds").floatValue = row.seconds;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(row.slot);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkillVfxTimingSync] " + row.vfxId + " reference Imp = " + row.seconds.ToString("0.##") + " s (" + AssetDatabase.GetAssetPath(row.slot) + ")");
        }

        public static string Status(Row row)
        {
            if (row.prefab == null)
                return "SkillVfxDatabase 里没有这个 id";
            if (row.slot == null)
                return "旧特效，不是插槽：只按时长停";
            string text;
            if (row.reference <= 0f)
                text = "没有参考时长：内容不缩放";
            else if (row.InSync)
                text = "同步";
            else
                text = string.Join("；", row.layers.Select(Describe));
            if (row.NeedsRetune)
                text += "；速度变化超过一倍，建议按 " + row.seconds.ToString("0.##") + " s 重调后设为参考";
            if (row.overridden)
                text += "；SO 写死了 vfxDurationSeconds，不跟配表";
            return text;
        }

        static string Describe(LayerScale layer)
        {
            switch (layer.scaling)
            {
                case SkillVfxTimeScaling.Stretch: return layer.name + " 整体 " + (1f / layer.k).ToString("0.##") + " 倍速";
                case SkillVfxTimeScaling.Rhythm: return layer.name + " 节奏 ×" + layer.k.ToString("0.##") + "，粒子原速";
                default: return layer.name + " 内容原速，只有窗口变";
            }
        }

        static Row MakeRow(string skillId, SkillAction.VfxSlotTiming timing, SkillVfxDatabase vfx)
        {
            var row = new Row
            {
                skillId = skillId,
                vfxId = timing.vfxId,
                seconds = timing.seconds,
                source = timing.source,
                overridden = timing.overridden,
            };
            if (vfx != null && vfx.TryGetPrefab(timing.vfxId, out GameObject prefab))
                row.prefab = prefab;
            row.slot = row.prefab != null ? row.prefab.GetComponent<SkillVfxPhaseController>() : null;
            if (row.slot == null)
                return row;
            row.reference = row.slot.ReferenceSeconds(SkillVfxPhase.Imp);
            foreach (SkillVfxLayer layer in row.slot.GetComponentsInChildren<SkillVfxLayer>(true))
            {
                if (layer.phase == SkillVfxPhase.Imp)
                    row.layers.Add(new LayerScale { name = layer.name, scaling = layer.timeScaling, k = row.slot.LayerTimeScale(layer, row.seconds) });
            }
            return row;
        }

        static float CastConfirmDelay(string skillId)
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            SkillRecord record = db != null && db.skills != null ? db.skills.Find(r => r != null && r.skillId == skillId) : null;
            return record != null ? record.castConfirmDelaySeconds : 0f;
        }

        static void Copy(List<SkillAction> from, List<SkillAction> to, List<SkillAction> copies, List<Object> temp)
        {
            foreach (SkillAction action in from)
            {
                if (action == null)
                    continue;
                SkillAction copy = Object.Instantiate(action);
                copy.name = action.name;
                temp.Add(copy);
                to.Add(copy);
                copies.Add(copy);
            }
        }
    }

    public class SkillVfxTimingWindow : EditorWindow
    {
        List<SkillVfxTimingSync.Row> _rows;
        Vector2 _scroll;

        [MenuItem("Tools/BuddahGo/VFX/Skill Timing Sync (特效时长同步)")]
        static void Open()
        {
            GetWindow<SkillVfxTimingWindow>("特效时长同步").Refresh();
        }

        void OnFocus()
        {
            Refresh();
        }

        void Refresh()
        {
            _rows = SkillVfxTimingSync.Build();
            Repaint();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    Refresh();
                GUILayout.FlexibleSpace();
                GUILayout.Label("时长 = 配表的玩法时长（Play Mode 同款）；参考 = 插槽调参时的时长；速度 = 参考 / 时长", EditorStyles.miniLabel);
            }
            if (_rows == null)
                Refresh();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string skill = null;
            foreach (SkillVfxTimingSync.Row row in _rows)
            {
                if (row.skillId != skill)
                {
                    skill = row.skillId;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(skill, EditorStyles.boldLabel);
                }
                DrawRow(row);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(SkillVfxTimingSync.Row row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                GUILayout.Label(row.vfxId, GUILayout.Width(110));
                GUILayout.Label(row.seconds.ToString("0.##") + " s", GUILayout.Width(50));
                GUILayout.Label(row.reference > 0f ? "参考 " + row.reference.ToString("0.##") + " s" : "参考 —", GUILayout.Width(70));
                Color previous = GUI.color;
                if (row.NeedsRetune || row.overridden || (row.slot != null && row.reference <= 0f))
                    GUI.color = new Color(1f, 0.75f, 0.4f);
                else if (row.InSync)
                    GUI.color = new Color(0.6f, 1f, 0.6f);
                GUILayout.Label(new GUIContent(SkillVfxTimingSync.Status(row), row.source), EditorStyles.wordWrappedMiniLabel);
                GUI.color = previous;
                using (new EditorGUI.DisabledScope(row.slot == null || row.InSync))
                {
                    if (GUILayout.Button(new GUIContent("设为参考", "已经按 " + row.seconds.ToString("0.##") + " s 调好了这个特效：参考时长改成它，速度回到 1"), GUILayout.Width(64))
                        && EditorUtility.DisplayDialog("设为参考", row.vfxId + " 的参考时长改成 " + row.seconds.ToString("0.##") + " s？\n只在你已经按这个时长调好特效之后这么做；否则配表变化不会再让它变速。", "改", "取消"))
                    {
                        SkillVfxTimingSync.SetReference(row);
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(row.prefab == null))
                {
                    if (GUILayout.Button("选中", GUILayout.Width(40)))
                    {
                        Selection.activeObject = row.prefab;
                        EditorGUIUtility.PingObject(row.prefab);
                    }
                }
            }
        }
    }
}
