using System.Collections.Generic;
using BuddahGo.VFX;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Builds slot effects in code for the EditMode tests (a controller root, layers, particle systems) and destroys
    // everything it made in reverse order: layers a controller detached go before the controller, whose OnDestroy
    // would otherwise call Destroy on them in edit mode.
    internal sealed class SkillVfxTestRig : System.IDisposable
    {
        readonly List<GameObject> _objects = new List<GameObject>();

        public GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            _objects.Add(go);
            return go;
        }

        // A slot root. impReferenceSeconds > 0: the Imp length the effect was tuned at.
        public SkillVfxPhaseController Slot(float impReferenceSeconds = 0f)
        {
            var controller = New("Effect").AddComponent<SkillVfxPhaseController>();
            if (impReferenceSeconds > 0f)
                SetReference(controller, SkillVfxPhase.Imp, impReferenceSeconds);
            return controller;
        }

        // A layer for the Imp phase, by default over the whole phase (PhaseFraction 0 to the phase end).
        public SkillVfxLayer Layer(Transform parent, SkillVfxAnchor anchor = SkillVfxAnchor.Self, Vector3 casterOffset = default)
        {
            var layer = New("FX_Test_Imp_C_Smoke_01", parent).AddComponent<SkillVfxLayer>();
            layer.phase = SkillVfxPhase.Imp;
            layer.anchor = anchor;
            layer.casterOffset = casterOffset;
            return layer;
        }

        public ParticleSystem Particles(SkillVfxLayer layer, string name, float lifetime)
        {
            var ps = New(name, layer.transform).AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = lifetime;
            main.simulationSpeed = 1f;
            return ps;
        }

        public static void Window(SkillVfxLayer layer, SkillVfxWindowUnit unit, float start, float end)
        {
            layer.startUnit = unit;
            layer.start = start;
            layer.endUnit = unit;
            layer.end = end;
        }

        public static void SetReference(SkillVfxPhaseController controller, SkillVfxPhase phase, float seconds)
        {
            var so = new SerializedObject(controller);
            SerializedProperty references = so.FindProperty("referenceSeconds");
            references.arraySize = 1;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)phase;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetScaledRoot(SkillVfxPhaseController controller, Transform scaledRoot)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("scaledRoot").objectReferenceValue = scaledRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }
    }
}
