using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Timing preview of a skill effect prefab (Docs/vfx/README.md section 4, "时长缩放"): type a phase length the
    // design table might give and see, for every layer and particle system, when it starts emitting, stops emitting
    // and when its last particle is gone, in seconds and as a share of the phase, with the layer's time scaling
    // applied. Also converts a phase's Seconds windows into fractions of the reference length, so they move with
    // the gameplay length.
    [CustomEditor(typeof(SkillVfxPhaseController))]
    public class SkillVfxTimingInspector : UnityEditor.Editor
    {
        static bool _open = true;
        static SkillVfxPhase _phase = SkillVfxPhase.Imp;
        static float _liveSeconds = -1f;
        static readonly Color Emit = new Color(0.35f, 0.75f, 1f, 0.9f);
        static readonly Color Tail = new Color(0.35f, 0.75f, 1f, 0.3f);
        static readonly Color WindowColor = new Color(1f, 0.8f, 0.3f, 0.8f);
        static readonly Color PhaseEnd = new Color(1f, 1f, 1f, 0.6f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (SkillVfxPhaseController)target;
            EditorGUILayout.Space();
            _open = EditorGUILayout.Foldout(_open, "Timing preview (时长缩放试算)", true);
            if (!_open)
                return;

            _phase = (SkillVfxPhase)EditorGUILayout.EnumPopup("Phase", _phase);
            float reference = controller.ReferenceSeconds(_phase);
            if (_liveSeconds < 0f)
                _liveSeconds = reference > 0f ? reference : 10f;
            using (new EditorGUILayout.HorizontalScope())
            {
                _liveSeconds = Mathf.Max(0.05f, EditorGUILayout.FloatField("Live length (s)", _liveSeconds));
                if (reference > 0f)
                {
                    if (GUILayout.Button("×0.5", GUILayout.Width(44))) _liveSeconds = reference * 0.5f;
                    if (GUILayout.Button("×1", GUILayout.Width(36))) _liveSeconds = reference;
                    if (GUILayout.Button("×2", GUILayout.Width(36))) _liveSeconds = reference * 2f;
                }
            }
            DrawGameLengths(controller);
            EditorGUILayout.HelpBox(reference > 0f
                ? "Reference " + reference.ToString("0.##") + " s. Bars: solid = emitting, faint = last particles fading, orange = layer window, white line = phase end."
                : "No reference length for " + _phase + ": nothing rescales. Add it under Reference Seconds.", MessageType.None);

            var layers = new List<SkillVfxLayer>();
            foreach (SkillVfxLayer layer in controller.GetComponentsInChildren<SkillVfxLayer>(true))
                if (layer.phase == _phase)
                    layers.Add(layer);
            if (layers.Count == 0)
            {
                EditorGUILayout.LabelField("No layer in this phase.");
                return;
            }

            // Rows first, so the bars share one time axis.
            var rows = new List<Row>();
            float axis = _liveSeconds;
            foreach (SkillVfxLayer layer in layers)
            {
                controller.LayerWindow(layer, _liveSeconds, out float start, out float end);
                float k = controller.LayerTimeScale(layer, _liveSeconds);
                rows.Add(new Row { label = layer.name + "  [" + layer.timeScaling + (k != 1f ? " k=" + k.ToString("0.##") : "") + "]", start = start, end = end, tail = end, isLayer = true });
                axis = Mathf.Max(axis, end);
                float simulation = layer.timeScaling == SkillVfxTimeScaling.Stretch ? k : 1f;
                float schedule = layer.timeScaling == SkillVfxTimeScaling.Rhythm ? k : 1f;
                foreach (ParticleSystem ps in layer.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.gameObject.activeSelf)
                        continue;
                    ParticleSystem.MainModule main = ps.main;
                    float delay = main.startDelay.constantMax * schedule * simulation;
                    float emitStart = start + delay;
                    float emitEnd = main.loop ? end : Mathf.Min(end, emitStart + main.duration * schedule * simulation);
                    float tail = emitEnd + main.startLifetime.constantMax * simulation;
                    if (emitStart >= end)
                        emitEnd = tail = emitStart;   // never starts inside the window
                    rows.Add(new Row { label = "   " + ps.name, start = emitStart, end = emitEnd, tail = tail });
                    axis = Mathf.Max(axis, tail);
                }
            }

            foreach (Row row in rows)
                DrawRow(row, axis, _liveSeconds);

            if (reference > 0f)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Convert " + _phase + " Seconds windows to fractions of " + reference.ToString("0.##") + " s"))
                    ConvertWindows(layers, reference);
            }
        }

        // The lengths the race gives this effect at the current design tables (SkillVfxTimingSync), one button per
        // skill that plays it. Rebuilt on demand: building copies every skill SO.
        static string _gameLengthsFor;
        static List<SkillVfxTimingSync.Row> _gameLengths;

        static void DrawGameLengths(SkillVfxPhaseController controller)
        {
            if (string.IsNullOrEmpty(controller.VfxId))
                return;
            if (_gameLengthsFor != controller.VfxId || _gameLengths == null)
            {
                _gameLengthsFor = controller.VfxId;
                _gameLengths = SkillVfxTimingSync.ForVfx(controller.VfxId);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Game length (配表)", "Length the race gives this effect now, per skill. Opens Tools/BuddahGo/VFX/Skill Timing Sync for the whole list."));
                if (_gameLengths.Count == 0)
                    GUILayout.Label("no skill plays " + controller.VfxId, EditorStyles.miniLabel);
                var seen = new HashSet<float>();
                foreach (SkillVfxTimingSync.Row row in _gameLengths)
                {
                    if (!seen.Add(row.seconds))
                        continue;
                    if (GUILayout.Button(new GUIContent(row.seconds.ToString("0.##") + " s", row.skillId + ": " + row.source), GUILayout.Width(56)))
                    {
                        _phase = SkillVfxPhase.Imp;
                        _liveSeconds = row.seconds;
                    }
                }
                if (GUILayout.Button("↻", GUILayout.Width(24)))
                    _gameLengths = null;
            }
        }

        struct Row
        {
            public string label;
            public float start, end, tail;
            public bool isLayer;
        }

        static void DrawRow(Row row, float axis, float phaseSeconds)
        {
            Rect line = EditorGUILayout.GetControlRect(false, 16f);
            float labelWidth = Mathf.Min(260f, line.width * 0.45f);
            var labelRect = new Rect(line.x, line.y, labelWidth, line.height);
            EditorGUI.LabelField(labelRect, row.label, row.isLayer ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            var bar = new Rect(line.x + labelWidth + 4f, line.y + 3f, line.width - labelWidth - 120f, line.height - 6f);
            EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.15f));
            float X(float t) => bar.x + bar.width * Mathf.Clamp01(t / Mathf.Max(1e-4f, axis));
            if (row.isLayer)
                EditorGUI.DrawRect(new Rect(X(row.start), bar.y, Mathf.Max(1f, X(row.end) - X(row.start)), bar.height), WindowColor);
            else
            {
                EditorGUI.DrawRect(new Rect(X(row.end), bar.y, Mathf.Max(0f, X(row.tail) - X(row.end)), bar.height), Tail);
                EditorGUI.DrawRect(new Rect(X(row.start), bar.y, Mathf.Max(1f, X(row.end) - X(row.start)), bar.height), Emit);
            }
            EditorGUI.DrawRect(new Rect(X(phaseSeconds), line.y, 1f, line.height), PhaseEnd);
            string Pct(float t) => Mathf.RoundToInt(100f * t / Mathf.Max(1e-4f, phaseSeconds)) + "%";
            string text = row.isLayer
                ? Pct(row.start) + "–" + Pct(row.end)
                : Pct(row.start) + "–" + Pct(row.end) + " ›" + Pct(row.tail);
            EditorGUI.LabelField(new Rect(bar.xMax + 4f, line.y, 116f, line.height), text, EditorStyles.miniLabel);
        }

        static void ConvertWindows(List<SkillVfxLayer> layers, float reference)
        {
            int changed = 0;
            foreach (SkillVfxLayer layer in layers)
            {
                Undo.RecordObject(layer, "Convert VFX windows to fractions");
                bool any = false;
                if (layer.startUnit == SkillVfxWindowUnit.Seconds)
                {
                    layer.start /= reference;
                    layer.startUnit = SkillVfxWindowUnit.PhaseFraction;
                    any = true;
                }
                if (layer.endUnit == SkillVfxWindowUnit.Seconds && layer.end >= 0f)
                {
                    layer.end /= reference;
                    layer.endUnit = SkillVfxWindowUnit.PhaseFraction;
                    any = true;
                }
                if (any)
                {
                    EditorUtility.SetDirty(layer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
                    changed++;
                }
            }
            Debug.Log("[SkillVfxTimingInspector] windows converted on " + changed + " layer(s)");
        }
    }
}
