using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // 金足破阵 (缘起 #4) release slot, first pass (Docs/vfx/yuanqi-giant-charge.md, user 2026-11-06: "放在 vfx test 里面
    // 后续我来调整。你做好预览"). Rebuilds the slot content from two reused sources and places a looping preview in
    // VFXtests; the open items (line timing, residue, brightness, sizes) are tuned there by hand afterwards.
    //  - FX_GiantCharge_Imp_C_Flash_01, rear blast: 神足通 accel_release's Ying Yang Burst Shockwave (it already opens
    //    backwards), larger and behind the giant.
    //  - FX_GiantCharge_Imp_C_Decal_01, ink route: the pack's Frontal Pulses sweep stretched to the ~80 m dash plus its
    //    white-edged ink slab ("Tree_pulse_floor"), laid once at the trigger and left in place.
    // Both layers are CasterRelative with casterScaling None: authored in world units for the x4 giant, so they keep
    // their size in either cast order (the body is still x1 when 神足通 was cast first). Needs the pack imported
    // (Assets/Piloto Studio, local only); run "Copy Used Pack Assets Into Project" before committing the prefab.
    public static class GiantChargeProto
    {
        public const string VfxId = "giant_charge_release";
        const string PackPrefab = "Assets/Piloto Studio/Super Realistic FX Bundle 02/Realistic Ying Yang/Ying Yang Frontal Pulses.prefab";
        const string AccelPrefab = "Assets/VFX/神足通/Release/VFX_Accel_Release.prefab";
        const string AccelBlast = "Scaled/FX_Accel_Imp_C_Flash_01/Ying Yang Burst Shockwave";
        const string BlastLayer = "FX_GiantCharge_Imp_C_Flash_01";
        const string RouteLayer = "FX_GiantCharge_Imp_C_Decal_01";

        // The Frontal Pulses systems that make the forward sweep (ink stroke, white flow lines, streaks, splat, flecks).
        static readonly HashSet<string> SweepSystems = new HashSet<string>
        {
            "ink_short_Dark_Shockwave_05_Distorted_alpha", "yy_Shockwave_1_Distorted_short_alpha",
            "Explosion_Shockwave_04_Distorted_M", "yy_Liquid_Splat_Frontal_3_alpha", "Flecks_Shiny_Additive",
        };
        const string SlabSystem = "Tree_pulse_floor_ink_1_alpha";

        // First-pass values from the 2026-11-06 bench (race camera, x4 giant). Positions in the layer frame: x right,
        // y up from the track, z along the dash, metres.
        static readonly Vector3 BlastPosition = new Vector3(0f, 13f, -16f);
        const float BlastScale = 40f;            // accel_release's own blast on a x4 body is 32
        static readonly Vector3 SweepScale = new Vector3(16f, 6f, 6f);   // pack X = along the dash: ~80 m, ~25 m wide
        const float SweepSpeed = 2.5f;           // the white front covers the 80 m in ~0.12 s
        static readonly Vector3 SlabPosition = new Vector3(0f, 0.3f, 40f);
        static readonly Vector3 SlabScale = new Vector3(8f, 3f, 3f);
        const float SlabSpeed = 4f;              // the slab's 6 s life plays in 1.5 s (README 3.3 residue cap)
        const float GroundOffsetY = -0.3f;       // track below the pivot at body scale 1 (README section 4)

        static SkillReleaseProto.Skill Slot()
        {
            foreach (SkillReleaseProto.Skill skill in SkillReleaseProto.Skills)
                if (skill.vfxId == VfxId)
                    return skill;
            throw new System.InvalidOperationException(VfxId + " is not in SkillReleaseProto.Skills");
        }

        [MenuItem("Tools/BuddahGo/VFX/Proto/金足破阵/Rebuild Slot Content (pack + accel blast)")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            SkillReleaseProto.Skill slot = Slot();
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefab);
            var accel = AssetDatabase.LoadAssetAtPath<GameObject>(AccelPrefab);
            if (pack == null)
                return "import the Piloto pack first (" + PackPrefab + " missing)";
            if (accel == null || accel.transform.Find(AccelBlast) == null)
                return AccelPrefab + " has no " + AccelBlast;
            string log = SkillReleaseProto.Build(slot, false);

            GameObject root = PrefabUtility.LoadPrefabContents(slot.PrefabPath);
            try
            {
                Transform scaled = root.transform.Find("Scaled");
                foreach (SkillVfxLayer old in scaled.GetComponentsInChildren<SkillVfxLayer>(true))
                    Object.DestroyImmediate(old.gameObject);

                SkillVfxLayer blast = NewLayer(scaled, BlastLayer, SkillVfxFunction.Flash, false);
                GameObject wave = Copy(accel.transform.Find(AccelBlast).gameObject, blast.transform, BlastPosition, Vector3.zero, Vector3.one * BlastScale);
                // The pack's group roots draw nothing (the blast root even emits invisible 5 s particles that would keep
                // the slot alive); the layer plays every system itself, so the groups become plain transforms.
                StripEmitter(wave);
                StripEmitter(wave.transform.Find("Burst").gameObject);

                SkillVfxLayer route = NewLayer(scaled, RouteLayer, SkillVfxFunction.Decal, true);
                GameObject sweep = Copy(pack.transform.Find("Pulses").gameObject, route.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0f, 90f, 0f), SweepScale);
                sweep.name = "Sweep (Frontal Pulses)";
                KeepOnly(sweep, SweepSystems);
                EmitOnce(sweep, SweepSpeed);
                GameObject slab = Copy(pack.transform.Find("Loop").gameObject, route.transform, SlabPosition, new Vector3(0f, 90f, 0f), SlabScale);
                slab.name = "Slab (Frontal Pulses ink floor)";
                KeepOnly(slab, new HashSet<string> { SlabSystem });
                EmitOnce(slab, SlabSpeed);
                StripEmitter(sweep);
                StripEmitter(slab);

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.playOnAwake = false;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                PrefabUtility.SaveAsPrefabAsset(root, slot.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return log + "; rebuilt " + slot.PrefabPath + " (" + BlastLayer + ", " + RouteLayer + ")";
        }

        static SkillVfxLayer NewLayer(Transform scaled, string name, SkillVfxFunction function, bool gameplayInfo)
        {
            var layer = SkillReleaseProto.Child(scaled, name).gameObject.AddComponent<SkillVfxLayer>();
            layer.phase = SkillVfxPhase.Imp;
            layer.tier = SkillVfxTier.Core;
            layer.function = function;
            layer.priority = 1;
            layer.gameplayInfo = gameplayInfo;
            // Laid once at the trigger at the caster's feet, facing the dash, then left in place.
            layer.anchor = SkillVfxAnchor.CasterRelative;
            layer.casterScaling = SkillVfxCasterScaling.None;
            layer.casterOffset = new Vector3(0f, GroundOffsetY, 0f);
            layer.casterEuler = Vector3.zero;
            layer.startUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.start = 0f;
            layer.endUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.end = -1f;
            layer.timeScaling = SkillVfxTimeScaling.Stretch;
            layer.particlesDieNaturally = true;
            layer.transform.localPosition = layer.casterOffset;
            return layer;
        }

        static GameObject Copy(GameObject source, Transform parent, Vector3 position, Vector3 euler, Vector3 scale)
        {
            GameObject copy = Object.Instantiate(source, parent, false);
            copy.name = source.name;
            copy.SetActive(true);
            copy.transform.localPosition = position;
            copy.transform.localEulerAngles = euler;
            copy.transform.localScale = scale;
            return copy;
        }

        // Removes every leaf particle system not named in keep (the group roots stay).
        static void KeepOnly(GameObject group, HashSet<string> keep)
        {
            var drop = new List<GameObject>();
            foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
                if (t != group.transform && t.childCount == 0 && !keep.Contains(t.name))
                    drop.Add(t.gameObject);
            foreach (GameObject go in drop)
                Object.DestroyImmediate(go);
        }

        static void StripEmitter(GameObject group)
        {
            var renderer = group.GetComponent<ParticleSystemRenderer>();
            var system = group.GetComponent<ParticleSystem>();
            if (system != null)
                Object.DestroyImmediate(system);
            if (renderer != null)
                Object.DestroyImmediate(renderer);
        }

        // One pulse per trigger: the pack loops its pulses every 1-1.5 s.
        static void EmitOnce(GameObject group, float simulationSpeed)
        {
            foreach (ParticleSystem ps in group.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.simulationSpeed = simulationSpeed;
                ParticleSystem.EmissionModule emission = ps.emission;
                if (emission.burstCount == 0)
                    continue;
                emission.rateOverTimeMultiplier = 0f;
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++)
                    bursts[i].cycleCount = 1;
                emission.SetBursts(bursts);
            }
        }

        // VFXtests (local, not in the repository): creates the scene when it is missing, then adds what is missing of
        // a x4 giant Player on a floor, an idle slot instance for hand tuning ("Write Release Pose" writes it back), a
        // race camera and the looping SkillVfxPreviewDash. Press Play to watch; switch the PreviewDash's
        // bodyScaleAtTrigger to 1 for the 神足通-first order.
        [MenuItem("Tools/BuddahGo/VFX/Proto/金足破阵/Place Preview In VFXtests")]
        public static void PlaceMenu() { Debug.Log(PlaceInVfxTests()); }

        public static string PlaceInVfxTests()
        {
            string path = PalmFrontalProto.StagePath;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != path)
            {
                if (active.isDirty)
                    return active.path + " has unsaved changes";
                if (System.IO.File.Exists(path))
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                else
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, path);
                }
            }
            var log = new System.Text.StringBuilder();

            Transform player = Root("Player");
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "Player";
                PalmFrontalProto.StripNetworking(go);
                player = go.transform;
                player.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                log.Append("added Player; ");
            }
            player.localScale = Vector3.one * 4f;

            if (Root("Floor") == null)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.position = player.position + Vector3.up * (GroundOffsetY * 4f) + player.forward * 120f;
                floor.transform.localScale = new Vector3(40f, 1f, 60f);
                log.Append("added Floor; ");
            }

            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Slot().PrefabPath);
            string name = System.IO.Path.GetFileNameWithoutExtension(Slot().PrefabPath);
            if (Root(name) == null)
            {
                var idle = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
                idle.name = name;
                Quaternion yaw = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
                idle.transform.SetPositionAndRotation(player.position, yaw);
                // Edit-time pose: the layers stand where the game lays them for a x4 body.
                foreach (SkillVfxLayer layer in idle.GetComponentsInChildren<SkillVfxLayer>(true))
                    layer.transform.localPosition = layer.casterOffset * 4f;
                log.Append("placed " + name + "; ");
            }

            Transform camRoot = Root("Cam_Race");
            if (camRoot == null)
            {
                var cam = new GameObject("Cam_Race").AddComponent<Camera>();
                cam.gameObject.AddComponent<AudioListener>().enabled = false;
                camRoot = cam.transform;
                log.Append("added Cam_Race; ");
            }

            Transform previewRoot = Root("PreviewDash");
            var preview = previewRoot != null ? previewRoot.GetComponent<SkillVfxPreviewDash>() : null;
            if (preview == null)
                preview = new GameObject("PreviewDash").AddComponent<SkillVfxPreviewDash>();
            preview.slotPrefab = slotPrefab;
            preview.caster = player;
            preview.raceCamera = camRoot.GetComponent<Camera>();
            camRoot.GetComponent<Camera>().depth = 10f;

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            return log + "saved " + path + " (press Play: the slot loops on a x4 giant dashing 140→80, seen from Cam_Race)";
        }

        static Transform Root(string name)
        {
            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name)
                    return go.transform;
            return null;
        }
    }
}
