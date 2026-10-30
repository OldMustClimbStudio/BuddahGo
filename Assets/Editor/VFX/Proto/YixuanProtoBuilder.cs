using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Design-session prototype (2026-10-28 晚): the palm rebuilt in the manner of ZZZ's Yixuan (a light body with the
    // ink in the outer layers) out of parts picked from Piloto Studio's "Realistic Ying Yang VFX" pack. Every layer is
    // a child particle system lifted from one of the pack prefabs and re-timed for our phase controller; only the
    // palm symbol texture is ours. Pack colours are kept for now (colour is tuned last). Rebuildable:
    // Tools → BuddahGo → VFX → Proto → Build Yixuan Piloto Proto.
    public static class YixuanProtoBuilder
    {
        const string Pack = "Assets/Piloto Studio/Super Realistic FX Bundle 02/Realistic Ying Yang/";
        const string Folder = "Assets/VFX/如来神掌/Proto/Yixuan";
        public const string PrefabPath = Folder + "/VFX_Palm_ProtoYX.prefab";
        const string PalmMask = "Assets/VFX/Shared/Textures/T_FX_PalmPrint_Mask.png";
        const string PalmMesh = "Assets/VFX/如来神掌/Mesh_FX_Palm.asset";
        internal static float OutlineScale = 1.06f;
        internal static Color HandFill = new Color(0.96f, 0.95f, 0.92f, 1f);
        internal static Color HandInk = new Color(0.05f, 0.05f, 0.07f, 1f);

        // The pack is authored at roughly 1 unit = 1 m; our character is 8 units tall and the palm collider 60 × 90.
        internal static float PackScale = 8f;

        // Tunables (pack units, before PackScale) so the next version only edits numbers here.
        internal static float SymbolSize = 11f;     // 11 × 8 ≈ 90 units: the collider height
        internal static float CoreFuzzSize = 5f;
        internal static float HaloSize = 7f;
        internal static float ShellSize = 15f;
        internal static float TailPuffSize = 2.0f;
        internal static float TailPuffLife = 0.5f;
        internal static float TailPuffRate = 30f;
        internal static float RibbonWidth = 3f;      // pack units, × PackScale
        internal static float FleckTailRate = 60f;
        internal static float ChargeFuzzSize = 1.5f;
        internal static float SymbolSizeFlight = 13f;

        [MenuItem("Tools/BuddahGo/VFX/Proto/Build Yixuan Piloto Proto")]
        internal static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            Directory.CreateDirectory(Folder);
            GameObject orb = Load("Ying Yang Orb");
            GameObject frontal = Load("Ying Yang Frontal");
            GameObject buff = Load("Ying Yang Buff");
            GameObject burst = Load("Ying Yang Burst Shockwave");
            GameObject flashAoe = Load("Ying Yang Flash AoE");
            GameObject smoke = Load("Ying Yang Smoke");
            if (orb == null || frontal == null || buff == null || burst == null || flashAoe == null || smoke == null)
                return "pack prefabs missing under " + Pack;

            var root = new GameObject("VFX_Palm_ProtoYX");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            SetController(controller, "palm", scaled, bodyFit);
            Transform pack = Child(bodyFit, "Pack");
            pack.localScale = Vector3.one * PackScale;
            // Caster-relative / world-detached layers live outside the body fit so collider fitting does not move them.
            Transform packScaled = Child(scaled, "PackScaled");
            packScaled.localScale = Vector3.one * PackScale;

            Material symbolMat = SymbolMaterial();

            // ---- Chg: the light gathers; the symbol appears
            {
                var layer = Layer(bodyFit, "FX_Palm_Chg_C_Mesh_01", SkillVfxPhase.Chg, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
                layer.driveScale = true;
                layer.scale = AnimationCurve.Linear(0f, 0.25f, 1f, 0.5f);
                AddInkHand(layer.transform);

                var glow = Layer(pack, "FX_Palm_Chg_C_Glow_01", SkillVfxPhase.Chg, SkillVfxTier.Core, SkillVfxFunction.Glow, 1, false);
                glow.alpha = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
                GameObject fuzz = Pick(frontal, "FuzzAdd_Soft", 0, glow.transform, "CoreFuzz");
                Tune(fuzz, true, 2f, false, ChargeFuzzSize, 1f);
                Steady(fuzz, 2f);

                var motes = Layer(pack, "FX_Palm_Chg_A_Sparks_01", SkillVfxPhase.Chg, SkillVfxTier.Accent, SkillVfxFunction.Sparks, 3, false);
                GameObject flecks = Pick(buff, "Flecks_Shiny_Additive", 0, motes.transform, "GatherFlecks");
                Tune(flecks, true, 1f, false, -1f, 2f);
            }

            // ---- Imp: flash, ink wave thrown back, ink splat left in the air, embers, warp
            {
                var flash = Layer(pack, "FX_Palm_Imp_C_Flash_01", SkillVfxPhase.Imp, SkillVfxTier.Core, SkillVfxFunction.Flash, 1, false);
                flash.endUnit = SkillVfxWindowUnit.Seconds; flash.end = 0.35f;
                GameObject lightFlash = Pick(burst, "LightFlash_Soft", 0, flash.transform, "LightFlash");
                Tune(lightFlash, false, 0.2f, false, 5f, 1f);

                var wave = Layer(packScaled, "FX_Palm_Imp_S_Smoke_01", SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
                wave.anchor = SkillVfxAnchor.DetachAtStart;
                wave.endUnit = SkillVfxWindowUnit.Seconds; wave.end = 1.2f;
                GameObject ring = Pick(flashAoe, "yy_Shockwave_1_alpha_soft", 0, wave.transform, "LaunchRing");
                Tune(ring, false, 1.0f, false, 3f, 1f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the ring expands across the flight axis
                GameObject whitePuff = Pick(burst, "Smoke_Explotion_Mom_sat_add_Soft", 0, wave.transform, "WhitePuff");
                Tune(whitePuff, false, 1.0f, false, 3f, 1f);

                var splat = Layer(packScaled, "FX_Palm_Imp_S_Decal_01", SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Decal, 2, false);
                splat.anchor = SkillVfxAnchor.DetachAtStart;
                splat.endUnit = SkillVfxWindowUnit.Seconds; splat.end = 1.0f;
                GameObject liquid = Pick(frontal, "yy_Liquid_Splat_Frontal_3_alpha", 0, splat.transform, "InkSplat");
                Tune(liquid, false, 1.0f, false, 3f, 1f);

                var embers = Layer(packScaled, "FX_Palm_Imp_A_Sparks_01", SkillVfxPhase.Imp, SkillVfxTier.Accent, SkillVfxFunction.Sparks, 3, false);
                embers.anchor = SkillVfxAnchor.DetachAtStart;
                embers.endUnit = SkillVfxWindowUnit.Seconds; embers.end = 1.0f;
                GameObject impFlecks = Pick(burst, "Flecks_Shiny_Additive", 0, embers.transform, "ImpFlecks");
                ParticleSystem impPs = Tune(impFlecks, false, 1.0f, false, -1f, 0.6f);
                SetLifetime(impPs, 0.6f);
                Spray(impPs, 25f, 25f);
                impFlecks.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // thrown back from the launch point
            }

            // ---- Prj: the body = symbol + white core + halo + a thin ink shell; the ink tail behind
            {
                var body = Layer(bodyFit, "FX_Palm_Prj_C_Mesh_01", SkillVfxPhase.Prj, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
                AddInkHand(body.transform);
                var splashLayer = Layer(pack, "FX_Palm_Prj_C_Mesh_02", SkillVfxPhase.Prj, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, false);
                // A still, distorted ink splash standing just behind the hand, facing the flight axis: the dark ground the
                // white hand reads against (Yixuan's "ink behind the light").
                GameObject shell = Pick(flashAoe, "yy_Shockwave_3_Distorted_still_alpha_soft", 0, splashLayer.transform, "InkSplash");
                Tune(shell, true, 3f, false, ShellSize, 1f);
                Steady(shell, 3f);
                shell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shell.transform.localPosition = new Vector3(0f, 0f, -0.6f);

                var glow = Layer(pack, "FX_Palm_Prj_C_Glow_01", SkillVfxPhase.Prj, SkillVfxTier.Core, SkillVfxFunction.Glow, 1, true);
                GameObject fuzz = Pick(frontal, "FuzzAdd_Soft", 0, glow.transform, "CoreFuzz");
                Tune(fuzz, true, 2f, false, CoreFuzzSize, 1f);
                Steady(fuzz, 2f);
                GameObject halo = Pick(orb, "Ring_Halo_faded_alpha_soft", 0, glow.transform, "Halo");
                Tune(halo, true, 1.5f, false, HaloSize, 1f);
                Steady(halo, 1.5f);

                var ribbon = Layer(pack, "FX_Palm_Prj_S_Smoke_02", SkillVfxPhase.Prj, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
                ribbon.trailSecondsScale = 1f;
                AddInkRibbon(ribbon.gameObject);

                var tail = Layer(pack, "FX_Palm_Prj_S_Smoke_01", SkillVfxPhase.Prj, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
                GameObject tailPuffs = Pick(orb, "Smoke_loop_flipbook_thingy_Clean_alpha_soft", 0, tail.transform, "InkTail");
                ParticleSystem tailPs = Tune(tailPuffs, true, 3f, true, TailPuffSize, 0f);
                SetLifetime(tailPs, TailPuffLife);
                SetRate(tailPs, TailPuffRate);
                Still(tailPs, 0.7f);
                GameObject vertical = Pick(buff, "Ink_Vertical_trail_alpha_soft", 0, tail.transform, "InkStreak");
                ParticleSystem vPs = Tune(vertical, true, 3f, true, -1f, 0f);
                SetLifetime(vPs, 0.5f);
                SetRate(vPs, 6f);
                Still(vPs, 1f);

                var sparks = Layer(pack, "FX_Palm_Prj_A_Sparks_01", SkillVfxPhase.Prj, SkillVfxTier.Accent, SkillVfxFunction.Sparks, 3, false);
                GameObject flecks = Pick(buff, "Flecks_Shiny_Additive", 0, sparks.transform, "TailFlecks");
                ParticleSystem fPs = Tune(flecks, true, 3f, true, -1f, 0f);
                SetLifetime(fPs, 0.8f);
                SetRate(fPs, FleckTailRate);
                Still(fPs, 1f);
            }

            // ---- Dis: the symbol goes, ink flakes burst outwards
            {
                var flakes = Layer(pack, "FX_Palm_Dis_S_Smoke_01", SkillVfxPhase.Dis, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
                flakes.endUnit = SkillVfxWindowUnit.Seconds; flakes.end = 1.5f;
                GameObject inkFlakes = Pick(frontal, "Ink_flakes_1_alpha_soft", 0, flakes.transform, "InkFlakes");
                Tune(inkFlakes, false, 1.5f, true, -1f, 1f);
                GameObject smudge = Pick(smoke, "Liquid_smudge_pb_3_alpha", 0, flakes.transform, "Smudge");
                Tune(smudge, false, 1.0f, true, -1f, 1f);
                GameObject disFlecks = Pick(smoke, "Flecks_Shiny_Additive", 0, flakes.transform, "DisFlecks");
                Tune(disFlecks, false, 1.0f, true, -1f, 1f);
            }

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startDelay = 0f;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return "built " + PrefabPath;
        }

        // ---------------------------------------------------------------- pack helpers

        static GameObject Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Pack + name + ".prefab");
        }

        // Instantiates a pack prefab, lifts out the nth child whose name starts with key (depth-first order) and
        // throws the rest away; the picked object is unpacked, renamed and parented under parent at the origin.
        static GameObject Pick(GameObject packPrefab, string key, int nth, Transform parent, string newName)
        {
            GameObject instance = Object.Instantiate(packPrefab);
            instance.name = packPrefab.name;
            var matches = new List<Transform>();
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                if (t != instance.transform && t.name.StartsWith(key))
                    matches.Add(t);
            if (matches.Count == 0)
            {
                Object.DestroyImmediate(instance);
                throw new System.Exception("'" + key + "' not found in " + packPrefab.name);
            }
            Transform picked = matches[Mathf.Clamp(nth, 0, matches.Count - 1)];
            picked.SetParent(null, false);
            Object.DestroyImmediate(instance);
            // Drop sub-emitters / children that came along: the prototype only wants the one system.
            for (int i = picked.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(picked.GetChild(i).gameObject);
            picked.name = newName;
            picked.SetParent(parent, false);
            picked.localPosition = Vector3.zero;
            picked.localRotation = Quaternion.identity;
            picked.localScale = Vector3.one;
            picked.gameObject.SetActive(true);
            foreach (MonoBehaviour mb in picked.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(mb);   // pack helper scripts (showcase, auto-parent) are not wanted
            return picked.gameObject;
        }

        // loop / duration / world-space / start size (pack units, < 0 keeps) / emission multiplier (0 keeps).
        static ParticleSystem Tune(GameObject go, bool loop, float duration, bool world, float startSize, float emissionMul)
        {
            var ps = go.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = loop;
            main.duration = duration;
            main.startDelay = 0f;
            main.playOnAwake = false;
            if (world)
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (startSize > 0f)
                main.startSize = new ParticleSystem.MinMaxCurve(startSize);
            if (emissionMul > 0f && emissionMul != 1f)
            {
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= emissionMul;
                for (int b = 0; b < emission.burstCount; b++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(b);
                    burst.count = new ParticleSystem.MinMaxCurve(Mathf.Round(burst.count.constantMax * emissionMul));
                    emission.SetBurst(b, burst);
                }
            }
            return ps;
        }

        // The hand as a brush drawing in 3D: the baked palm mesh with an unlit paper-white fill and a black inverted-hull
        // outline (the ink line). Authored at the reference collider, so it sits directly under the body-fit root.
        static void AddInkHand(Transform parent)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PalmMesh);
            if (mesh == null)
                throw new System.Exception("palm mesh missing: " + PalmMesh);
            GameObject fill = Child(parent, "HandFill").gameObject;
            fill.AddComponent<MeshFilter>().sharedMesh = mesh;
            var fr = fill.AddComponent<MeshRenderer>();
            fr.sharedMaterial = UnlitMaterial("Mat_Proto_HandFill", HandFill, 2);
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;
            GameObject line = Child(parent, "HandInkLine").gameObject;
            line.transform.localScale = Vector3.one * OutlineScale;
            line.AddComponent<MeshFilter>().sharedMesh = mesh;
            var lr = line.AddComponent<MeshRenderer>();
            lr.sharedMaterial = UnlitMaterial("Mat_Proto_HandInkLine", HandInk, 1);   // cull front = inverted hull
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        static Material UnlitMaterial(string name, Color color, int cull)
        {
            string path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cull", cull);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Launch embers: a cone spray along the system's +Z with the pack's own look.
        static void Spray(ParticleSystem ps, float speed, float coneAngle)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.2f);
            ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = coneAngle; shape.radius = 0.2f;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime; vel.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime; force.enabled = false;
            ParticleSystem.NoiseModule noise = ps.noise; noise.enabled = false;
        }

        // The ink ribbon behind the body: a TrailRenderer wearing the pack's wispy smoke trail material.
        static void AddInkRibbon(GameObject go)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = RibbonMaterial();
            trail.time = 0.25f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, RibbonWidth * PackScale, 1f, 0f);
            trail.minVertexDistance = 2f;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        static Material RibbonMaterial()
        {
            string path = Folder + "/Mat_Proto_InkRibbon.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            // The pack's UberFX needs particle vertex streams, so the ribbon wears the project's ink stroke shader
            // with the pack's wispy smoke trail texture as its mask.
            var mat = new Material(Shader.Find("FX/Alpha_Smoke"));
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Piloto Studio/Textures/Trail_WispyGeneric.png"));
            mat.SetColor("_Color", new Color(0.06f, 0.06f, 0.08f, 1f));
            mat.SetFloat("_Opacity", 0.9f);
            mat.SetFloat("_Feather", 0.3f);
            mat.SetFloat("_Cutoff", 0.3f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Trail pieces: emitted at the moving point and left where they are (no velocity, force, gravity, shape or noise).
        static void Still(ParticleSystem ps, float alpha)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            Color c = main.startColor.color; c.a = alpha; main.startColor = new ParticleSystem.MinMaxGradient(c);
            ParticleSystem.ShapeModule shape = ps.shape; shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime; vel.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime; force.enabled = false;
            ParticleSystem.NoiseModule noise = ps.noise; noise.enabled = false;
            ParticleSystem.InheritVelocityModule inherit = ps.inheritVelocity; inherit.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime; limit.enabled = false;
        }

        // Body parts: no fade-in over the pack's long lifetimes, full alpha, constant size.
        static void Steady(GameObject go, float lifetime)
        {
            var ps = go.GetComponent<ParticleSystem>();
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = false;
            ParticleSystem.SizeOverLifetimeModule siz = ps.sizeOverLifetime; siz.enabled = false;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime);
            Color c = main.startColor.color; c.a = 1f; main.startColor = new ParticleSystem.MinMaxGradient(c);
        }

        static void SetLifetime(ParticleSystem ps, float seconds)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(seconds * 0.8f, seconds * 1.2f);
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        static void SetMaterial(GameObject go, Material material)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        // The pack's symbol material with our palm mask in the main texture slot: a white, soft-edged hand.
        static Material SymbolMaterial()
        {
            string path = Folder + "/Mat_Proto_PalmSymbol.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Piloto Studio/Materials/ARPG Ying Yang/yy_2_symbol_alpha_soft.mat");
            var mat = new Material(source);
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(PalmMask);
            foreach (string name in new[] { "_MainTex", "_Main_Texture", "_MainTexture" })
                if (mat.HasProperty(name))
                    mat.SetTexture(name, mask);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- our helpers (same shape as PalmVfxBuilder)

        static SkillVfxLayer Layer(Transform parent, string name, SkillVfxPhase phase, SkillVfxTier tier, SkillVfxFunction function, int priority, bool gameplayInfo)
        {
            var layer = Child(parent, name).gameObject.AddComponent<SkillVfxLayer>();
            layer.phase = phase;
            layer.tier = tier;
            layer.function = function;
            layer.priority = priority;
            layer.gameplayInfo = gameplayInfo;
            layer.start = 0f;
            layer.end = -1f;
            return layer;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void SetController(SkillVfxPhaseController controller, string vfxId, Transform scaled, Transform bodyFit)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = vfxId;
            so.FindProperty("referenceColliderSize").vector3Value = new Vector3(60f, 90f, 20f);
            so.FindProperty("scaledRoot").objectReferenceValue = scaled;
            so.FindProperty("bodyFitRoot").objectReferenceValue = bodyFit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- review shots

        // Preview rig + driver for this prototype; enter Play Mode afterwards, then Capture.
        [MenuItem("Tools/BuddahGo/VFX/Proto/Preview Yixuan Proto (loop, press Play)")]
        internal static void PreviewMenu() { Debug.Log(Setup(-1f) + " — press Play; Tab is not needed, cameras: Cam_Caster is the main camera"); }

        public static string Setup(float freezeAt)
        {
            Time.timeScale = 1f;
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            Transform caster = PalmVfxPreview.BuildStage(null);
            var driverGo = new GameObject("PreviewDriver");
            var driver = driverGo.AddComponent<SkillVfxPreviewDriver>();
            driver.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            driver.buffPrefab = null;   // the proto has no buff layer yet
            driver.caster = caster;
            driver.freezeAt = freezeAt;
            driver.timeScale = 1f;
            return "proto preview ready (freezeAt=" + freezeAt + ")";
        }

        public static string Capture(string cameraName, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return PalmVfxPreview.Capture(cameraName, path, 1600, 900);
        }
    }
}
