using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.VFX.EditorTools
{
    // Builds the 如来神掌 effect prefabs, their materials and the shared template from code, so the layer sheet in
    // Docs/vfx/push-projectile-hands.md sections 4-5 (revised after the 2026-10-28 review, section 12) maps one-to-one
    // onto named GameObjects and can be rebuilt after a change. Units: the projectile prefabs are authored for the reference collider 60 × 90 × 20 (world units; the
    // character is ~8 tall); SkillVfxPhaseController rescales them to the live collider. Caster-relative layers (foot
    // decal, buff ring) sit outside the scaled root.
    internal static class PalmVfxBuilder
    {
        const string Folder = "Assets/VFX/如来神掌";
        const string MaterialFolder = Folder + "/Materials";
        const string SharedFolder = "Assets/VFX/Shared";
        internal const string PalmPrefabPath = Folder + "/VFX_Palm.prefab";
        internal const string AntiPrefabPath = Folder + "/VFX_Palm_Anti.prefab";
        internal const string BufPrefabPath = Folder + "/VFX_Palm_Buf.prefab";
        internal const string TemplatePrefabPath = SharedFolder + "/VFX_Template.prefab";

        // Palette (Docs/vfx/README.md section 2 and palm doc 4 after the second review), sRGB.
        static readonly Color CoreGold = Hex(0x8A5A14);
        static readonly Color RimGold = Hex(0xF2C14E);
        static readonly Color WarmWhite = Hex(0xFFF3C4);
        static readonly Color Amber = Hex(0xC8842A);
        static readonly Color InkSoft = Hex(0x15151A);
        static readonly Color Cinnabar = Hex(0xC0392B);
        static readonly Color AntiPurple = Hex(0x5B2A6E);
        static readonly Color AntiRed = Hex(0x7A1F1F);
        static readonly Color BrokenSeal = Hex(0xB8B2AA);

        // Length / width of the scanned stroke (T_FX_Brush_Stroke is 1024 × 256): strokes keep it.
        const float StrokeAspect = 4f;
        // The sutra ring quad: ring diameter ≈ twice the character's height (~8), glyphs ≤ 1/6 of it.
        const float SutraRingSize = 18f;

        // The character's pivot is mid-body; the buff layers sit on its chest, a little in front (Buddah is ~8 tall).
        static readonly Vector3 ChestOffset = new Vector3(0f, 1.5f, 3f);

        static Mesh _quad;
        static Mesh Quad => _quad != null ? _quad : (_quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx"));

        [MenuItem("Tools/BuddahGo/VFX/Build Palm VFX")]
        internal static void BuildAll()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(SharedFolder);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FxTextureGenerator.NoisePacked) == null)
                FxTextureGenerator.GenerateAll();
            Mesh palm = AssetDatabase.LoadAssetAtPath<Mesh>(PalmMeshBaker.OutputPath);
            if (palm == null)
                palm = PalmMeshBaker.Bake();

            BuildTemplate();
            BuildPalm(palm, false);
            BuildPalm(palm, true);
            BuildBuff();
            AssetDatabase.SaveAssets();
            Debug.Log("[PalmVfxBuilder] built " + PalmPrefabPath + ", " + AntiPrefabPath + ", " + BufPrefabPath + ", " + TemplatePrefabPath);
        }

        // ---------------------------------------------------------------- prefabs

        // The layer sheet after the second review (palm doc 4, 5 and 13): the body is light (a self-lit gold energy
        // form); ink lives only in the outer layers as short pieces of a real brush scan at its own proportions, with a
        // gold rim and embers; nothing black on the ground; a cinnabar seal standing free in the air.
        static void BuildPalm(Mesh palmMesh, bool anti)
        {
            string skill = anti ? "PalmAnti" : "Palm";
            var root = new GameObject(anti ? "VFX_Palm_Anti" : "VFX_Palm");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            SetController(controller, anti ? "palm_anti" : "palm", scaled, bodyFit);

            Material body = BodyMaterial(skill, palmMesh.bounds, anti, false);
            Material pulse = BodyMaterial(skill + "_Pulse", palmMesh.bounds, anti, true);
            Color spark = anti ? AntiRed : RimGold;
            // Half size while charging so the hand does not cover the caster; full size from the launch on.
            AnimationCurve chargeScale = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 0.5f);

            // ---- Chg: 佛手显形 (Core, gameplay info): the light comes in tip first and rises from dark gold.
            SkillVfxLayer chgBody = Layer(bodyFit, Name(skill, "Chg", "C", "Mesh"), SkillVfxPhase.Chg, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
            MeshBody(chgBody.gameObject, palmMesh, body);
            chgBody.driveProgress = true;
            chgBody.useChargeProgress = true;
            chgBody.driveScale = true;
            chgBody.scale = chargeScale;

            if (!anti)
            {
                // ---- Chg: 经文环 (Secondary glow, 0.1-1.0 × charge, 0.3 s tail): the Heart Sutra in plain gold round the
                // caster's feet; ring diameter ≈ twice the character, glyphs ≤ 1/6 of the character, no glow. No ink
                // on the ground.
                SkillVfxLayer sutra = Layer(root.transform, Name(skill, "Chg", "S", "Glow"), SkillVfxPhase.Chg, SkillVfxTier.Secondary, SkillVfxFunction.Glow, 2, false);
                sutra.anchor = SkillVfxAnchor.CasterFeet;
                sutra.start = 0.1f;
                sutra.end = 1f;
                sutra.tailFadeSeconds = 0.3f;
                sutra.fadeInSeconds = 0.15f;
                sutra.driveProgress = true;
                sutra.progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                FlatQuad(sutra.transform, "Quad", SutraRingSize, 0.08f, SutraMaterial(skill + "_SutraRing"));

                // ---- Chg: 聚拢金点 (Accent sparks, 0.2-0.9 × charge, <= 20): gold points drawn into the palm.
                SkillVfxLayer motes = Layer(scaled, Name(skill, "Chg", "A", "Sparks"), SkillVfxPhase.Chg, SkillVfxTier.Accent, SkillVfxFunction.Sparks, 3, false);
                motes.start = 0.2f;
                motes.end = 0.9f;
                GatherMotes(motes.gameObject, GlowMaterial(skill + "_Mote", Tex(FxTextureGenerator.InkDot), RimGold, 1.5f));

                // ---- Chg: 蓄力点光 (0.3-1.0 × charge, rising with progress; priority 3, off on Low).
                SkillVfxLayer chargeLight = Layer(scaled, Name(skill, "Chg", "S", "Light"), SkillVfxPhase.Chg, SkillVfxTier.Secondary, SkillVfxFunction.Light, 3, false);
                chargeLight.start = 0.3f;
                chargeLight.end = 1f;
                chargeLight.lightIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                PointLight(chargeLight.gameObject, RimGold, 2.5f, 90f);
            }

            // ---- Imp: 出掌闪光 (Core flash, impFrames): the body itself flares to warm white (an additive copy of its own
            // light), the one near-white moment. Anti: grey-white.
            SkillVfxLayer flash = Layer(bodyFit, Name(skill, "Imp", "C", "Flash"), SkillVfxPhase.Imp, SkillVfxTier.Core, SkillVfxFunction.Flash, 1, false);
            flash.startUnit = flash.endUnit = SkillVfxWindowUnit.ImpFrames;
            flash.start = 0f;
            flash.end = 1f;
            flash.alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            MeshBody(flash.gameObject, palmMesh, pulse);

            // ---- Imp: 劲风 (Secondary smoke, 0-0.35 s): two brush strokes thrown backward, each no wider than half the
            // hand, drawn in the first half and breaking off from the root, gold rim and embers. Shared once per burst.
            SkillVfxLayer gust = Layer(scaled, Name(skill, "Imp", "S", "Smoke"), SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            gust.startUnit = gust.endUnit = SkillVfxWindowUnit.Seconds;
            gust.end = 0.35f;
            gust.anchor = SkillVfxAnchor.DetachAtStart;
            gust.burstShared = anti;
            gust.driveProgress = true;
            gust.progress = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.45f, 1f), new Keyframe(1f, 1f));
            gust.driveDissolve = true;
            gust.dissolve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f));
            GustStrokes(gust.transform, InkMaterial(skill + "_Gust", Tex(FxTextureGenerator.BrushStroke), spark, false));

            // ---- Imp -> Lin: 朱砂印记 (Secondary decal, 0-0.8 s): one seal standing free in the air beside the launch
            // point, off the body, burnt away by noise. Anti: the grey-white broken seal, shared once per burst.
            SkillVfxLayer seal = Layer(scaled, Name(skill, "Imp", "S", "Decal"), SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Decal, 2, false);
            seal.startUnit = seal.endUnit = SkillVfxWindowUnit.Seconds;
            seal.end = 0.8f;
            seal.anchor = SkillVfxAnchor.DetachAtStart;
            seal.burstShared = anti;
            seal.driveDissolve = true;
            seal.dissolve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0f), new Keyframe(1f, 1f));
            seal.transform.localPosition = new Vector3(-48f, 18f, -12f);
            Upright(seal.transform, "Quad", new Vector2(18f, 18f), SealMaterial(skill + "_Seal",
                Tex(anti ? FxTextureGenerator.PalmPrintBroken : FxTextureGenerator.PalmPrint), anti ? BrokenSeal : Cinnabar), false);

            // ---- Imp: 发射点光 (lightSeconds; priority 3).
            SkillVfxLayer launchLight = Layer(scaled, Name(skill, "Imp", "S", "Light"), SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Light, 3, false);
            launchLight.startUnit = launchLight.endUnit = SkillVfxWindowUnit.LightSeconds;
            launchLight.end = 1f;
            launchLight.anchor = SkillVfxAnchor.DetachAtStart;
            launchLight.burstShared = anti;
            launchLight.lightIntensity = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            PointLight(launchLight.gameObject, anti ? AntiRed : RimGold, 6f, 220f);

            // ---- Imp: 施法者本地后期脉冲 (Secondary post, <= 0.15 s, local caster only, via SkillFeelRouter).
            SkillVfxLayer post = Layer(root.transform, Name(skill, "Imp", "S", "Post"), SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Post, 2, false);
            post.startUnit = post.endUnit = SkillVfxWindowUnit.Seconds;
            post.end = 0.15f;
            post.burstShared = anti;
            post.feelEventId = anti ? "palm_anti_launch_local" : "palm_launch_local";

            // ---- Prj: 佛手主体 + 前导边缘 (Core, gameplay info; the leading edge is in FX/Mesh_Energy).
            SkillVfxLayer prjBody = Layer(bodyFit, Name(skill, "Prj", "C", "Mesh"), SkillVfxPhase.Prj, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
            MeshBody(prjBody.gameObject, palmMesh, body);
            prjBody.driveProgress = true;
            prjBody.progress = AnimationCurve.Constant(0f, 1f, 1f);

            if (!anti)
            {
                // ---- Prj: 金色碎羽 / 流线 (Secondary sparks, <= 60): stretched gold grains thrown off the body; the
                // opening burst is the launch frame's splash.
                SkillVfxLayer feathers = Layer(bodyFit, Name(skill, "Prj", "S", "Sparks"), SkillVfxPhase.Prj, SkillVfxTier.Secondary, SkillVfxFunction.Sparks, 2, false);
                Feathers(feathers.gameObject, palmMesh, GlowMaterial(skill + "_Feather", Tex(FxTextureGenerator.InkDot), RimGold, 2.2f));
            }

            // ---- Prj: 墨烟拖尾 (Secondary smoke, kept at distance): pieces of the brush stroke left behind the hand,
            // each drawn in and broken off within 0.4 s; gold rim and embers. Never touching the body.
            SkillVfxLayer trail = Layer(scaled, Name(skill, "Prj", "S", "Smoke"), SkillVfxPhase.Prj, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, true);
            Material trailInk = InkMaterial(skill + "_Trail", Tex(FxTextureGenerator.BrushStroke), spark, true);
            InkTrail(trail.gameObject, trailInk, anti ? 0.5f : 1f);

            // ---- Prj: 拖尾火星 (Accent sparks, <= 100): gold points scattered through the ink.
            SkillVfxLayer embers = Layer(scaled, Name(skill, "Prj", "A", "Sparks"), SkillVfxPhase.Prj, SkillVfxTier.Accent, SkillVfxFunction.Sparks, 3, false);
            Embers(embers.gameObject, GlowMaterial(skill + "_Ember", Tex(FxTextureGenerator.InkDot), spark, 2.4f), anti ? 50 : 100);

            // ---- Dis: 实体熄灭 (Core, disSeconds): the light drops to dark gold, then a hard noise dissolve.
            SkillVfxLayer disBody = Layer(bodyFit, Name(skill, "Dis", "C", "Mesh"), SkillVfxPhase.Dis, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, false);
            MeshBody(disBody.gameObject, palmMesh, body);
            disBody.driveProgress = true;
            disBody.progress = AnimationCurve.Constant(0f, 1f, 1f);
            disBody.driveDissolve = true;
            disBody.dissolve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            // ---- Imp (hit) and Lin: placeholders only this round (palm doc 4.6).
            Child(root.transform, "Placeholder_Hit_Imp_C_Flash");
            Child(root.transform, "Placeholder_Hit_Imp_S_Smoke");
            Child(root.transform, "Placeholder_Hit_Lin_S_Decal");

            Save(root, anti ? AntiPrefabPath : PalmPrefabPath);
        }

        static void BuildBuff()
        {
            var root = new GameObject("VFX_Palm_Buf");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            SetController(controller, "palm_buf", null, null);

            // ---- Buf: 掌心光轮 (Secondary glow, whole buff, 0.3 s in/out): a thin slow gold sutra ring on the chest.
            SkillVfxLayer halo = Layer(root.transform, Name("Palm", "Buf", "S", "Glow"), SkillVfxPhase.Buf, SkillVfxTier.Secondary, SkillVfxFunction.Glow, 2, false);
            halo.transform.localPosition = ChestOffset;
            Upright(halo.transform, "Quad", new Vector2(9f, 9f), SutraMaterial("Palm_Halo"), false);

            // ---- Buf: 施法确认爆发 (Secondary smoke, 0-0.4 s): the brush circle drawn open in the air at the chest,
            // gold rim and embers, then eroded away. (Placeholder for the ink-smoke burst of palm doc 4.1.)
            SkillVfxLayer bloom = Layer(root.transform, Name("Palm", "Buf", "S", "Smoke"), SkillVfxPhase.Buf, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            bloom.transform.localPosition = ChestOffset;
            bloom.startUnit = bloom.endUnit = SkillVfxWindowUnit.Seconds;
            bloom.end = 0.4f;
            bloom.faceCamera = true;
            bloom.driveProgress = true;
            bloom.progress = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 1f));
            bloom.alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
            Billboard(bloom.transform, "Quad", new Vector2(12f, 12f), InkMaterial("Palm_BufEnso", Tex(FxTextureGenerator.EnsoMask), RimGold, false));

            Save(root, BufPrefabPath);
        }

        static void BuildTemplate()
        {
            var root = new GameObject("VFX_Template");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            SetController(controller, "", scaled, bodyFit);
            string[] phases = { "Buf", "Chg", "Prj", "Imp", "Dis", "Lin" };
            SkillVfxPhase[] values = { SkillVfxPhase.Buf, SkillVfxPhase.Chg, SkillVfxPhase.Prj, SkillVfxPhase.Imp, SkillVfxPhase.Dis, SkillVfxPhase.Lin };
            for (int i = 0; i < phases.Length; i++)
            {
                Transform slot = Child(scaled, "Phase_" + phases[i]);
                SkillVfxLayer placeholder = Layer(slot, "FX_Skill_" + phases[i] + "_C_Mesh_01", values[i], SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, false);
                placeholder.end = -1f;
            }
            Save(root, TemplatePrefabPath);
        }

        // ---------------------------------------------------------------- element helpers

        static string Name(string skill, string phase, string tier, string function)
        {
            return "FX_" + skill + "_" + phase + "_" + tier + "_" + function + "_01";
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

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

        static void SetController(SkillVfxPhaseController controller, string vfxId, Transform scaled, Transform bodyFit)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = vfxId;
            so.FindProperty("referenceColliderSize").vector3Value = PalmMeshBaker.ReferenceCollider;
            so.FindProperty("scaledRoot").objectReferenceValue = scaled;
            so.FindProperty("bodyFitRoot").objectReferenceValue = bodyFit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MeshBody(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            NoShadows(renderer);
        }

        static void FlatQuad(Transform parent, string name, float size, float height, Material material)
        {
            Transform quad = Child(parent, name);
            quad.localPosition = new Vector3(0f, height, 0f);
            quad.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.localScale = new Vector3(size, size, 1f);
            AddQuad(quad.gameObject, material);
        }

        // A quad standing in the layer's XY plane, facing the flight direction (+Z) or, flipped, facing back (-Z).
        static void Upright(Transform parent, string name, Vector2 size, Material material, bool faceForward)
        {
            Transform quad = Child(parent, name);
            quad.localRotation = faceForward ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            quad.localScale = new Vector3(size.x, size.y, 1f);
            AddQuad(quad.gameObject, material);
        }

        // Child quad of a faceCamera layer: the layer turns its +Z away from the camera, the quad faces it.
        static void Billboard(Transform parent, string name, Vector2 size, Material material)
        {
            Transform quad = Child(parent, name);
            quad.localScale = new Vector3(size.x, size.y, 1f);
            AddQuad(quad.gameObject, material);
        }

        static void AddQuad(GameObject go, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            NoShadows(renderer);
        }

        static void NoShadows(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        static void PointLight(GameObject go, Color color, float intensity, float range)
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = false;
        }

        static void Trail(GameObject go, float width, Material material)
        {
            go.transform.localPosition = new Vector3(0f, 0f, -6f);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.25f;
            trail.minVertexDistance = 6f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0.05f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.emitting = false;
            trail.autodestruct = false;
            NoShadows(trail);
        }

        static ParticleSystem NewSystem(GameObject go, Material material, int maxParticles)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            NoShadows(renderer);
            return ps;
        }

        static void GatherMotes(GameObject go, Material material)
        {
            ParticleSystem ps = NewSystem(go, material, 20);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.7f;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 28f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 55f;
            shape.radiusThickness = 0.2f;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(-120f);
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Gradient2(RimGold, WarmWhite, 0f, 1f));
        }

        // Two brush strokes behind the hand, lying along the flight axis (head at the palm, tail backward), each at the
        // scan's own proportions and no wider than half the hand; rolled apart so they read from behind and the side.
        static void GustStrokes(Transform parent, Material material)
        {
            float[] rolls = { 30f, -40f };
            float[] lengths = { 96f, 76f };
            for (int i = 0; i < rolls.Length; i++)
            {
                float length = lengths[i], width = length / StrokeAspect;
                Transform quad = Child(parent, "Stroke_" + (i + 1));
                quad.localPosition = new Vector3(i == 0 ? 6f : -8f, i == 0 ? 10f : -6f, -length * 0.5f - 8f);
                quad.localRotation = Quaternion.AngleAxis(rolls[i], Vector3.forward) * Quaternion.Euler(0f, 90f, 0f);
                quad.localScale = new Vector3(length, width, 1f);
                AddQuad(quad.gameObject, material);
            }
        }

        // Ink pieces dropped behind the hand as it flies (one every ~16 units of travel), each a stretched particle at
        // the scan's proportions that is drawn in, then breaks off from the root within its 0.3-0.4 s life (Custom1.x
        // draw, .y erase). World space, so the pieces stay where they were laid down.
        static void InkTrail(GameObject go, Material material, float scale)
        {
            go.transform.localPosition = new Vector3(0f, 0f, -16f);
            ParticleSystem ps = NewSystem(go, material, scale < 1f ? 20 : 30);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(12f * scale, 22f * scale);
            main.startColor = Color.white;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverDistance = 1f / 16f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 34f, 2f) * scale;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-70f, -40f);
            ParticleSystem.CustomDataModule custom = ps.customData;
            custom.enabled = true;
            custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 3);
            custom.SetVector(ParticleSystemCustomData.Custom1, 0, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1f)).ToMinMax(1f));
            custom.SetVector(ParticleSystemCustomData.Custom1, 1, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.55f, 0f), new Keyframe(1f, 1f)).ToMinMax(1f));
            custom.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(0f, 1f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = StrokeAspect;
            renderer.velocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.OldestInFront;
            CustomStreams(renderer);
        }

        static void Feathers(GameObject go, Mesh mesh, Material material)
        {
            ParticleSystem ps = NewSystem(go, material, 60);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(60f, 160f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 12f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh = mesh;
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Gradient2(Color.white, Amber, 1f, 1f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.06f;
        }

        static void Embers(GameObject go, Material material, int count)
        {
            go.transform.localPosition = new Vector3(0f, 0f, -24f);
            ParticleSystem ps = NewSystem(go, material, count);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = count * 0.6f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 40f, 30f);
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOut());
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f).ToMinMax(1f);
        }

        static void CustomStreams(ParticleSystemRenderer renderer)
        {
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW,
            });
        }

        static Gradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Gradient Gradient2(Color a, Color b, float alphaStart, float alphaEnd)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(alphaStart, 0f), new GradientAlphaKey(alphaEnd, 0.3f), new GradientAlphaKey(alphaEnd, 0.8f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ---------------------------------------------------------------- materials

        // Self-lit gold energy body (palm doc 13.3 Q2) or, with pulse, the additive copy the Imp flash draws over it.
        static Material BodyMaterial(string name, Bounds meshBounds, bool anti, bool pulse)
        {
            Material m = MaterialAsset("Mat_FX_" + name + (pulse ? "" : "_Body"), "FX/Mesh_Energy");
            m.SetColor("_CoreColor", Linear(anti ? AntiPurple : CoreGold) * 1.1f);
            m.SetColor("_RimColor", Linear(anti ? AntiRed : RimGold) * (anti ? 1.6f : 1.9f));
            m.SetFloat("_RimPower", 1.6f);
            m.SetFloat("_TipStart", 0.7f);
            m.SetFloat("_ChargeStart", 0.45f);
            m.SetFloat("_FilamentTiling", 0.035f);
            m.SetFloat("_FilamentBands", 6f);
            m.SetFloat("_FilamentWidth", 0.12f);
            m.SetFloat("_FilamentSpeed", 0.25f);
            m.SetFloat("_FilamentAmount", 0.55f);
            m.SetColor("_LeadColor", Linear(anti ? AntiRed : WarmWhite) * (anti ? 1.4f : 2.2f));
            m.SetFloat("_LeadStart", 0.75f);
            m.SetFloat("_LeadFacing", 2f);
            m.SetVector("_HeightRange", new Vector4(meshBounds.min.y, meshBounds.max.y, 0f, 0f));
            m.SetVector("_DepthRange", new Vector4(meshBounds.min.z, meshBounds.max.z, 0f, 0f));
            m.SetTexture("_NoiseTex", Tex(FxTextureGenerator.NoisePacked));
            m.SetFloat("_NoiseTiling", 0.03f);
            m.SetColor("_EdgeColor", Linear(anti ? AntiRed : RimGold) * 1.8f);
            m.SetFloat("_EdgeWidth", 0.02f);
            m.SetFloat("_PulseOnly", pulse ? 1f : 0f);
            m.SetColor("_PulseColor", Linear(anti ? BrokenSeal : WarmWhite) * (anti ? 1.0f : 1.4f));
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)(pulse ? BlendMode.One : BlendMode.Zero));
            m.SetFloat("_ZWrite", pulse ? 0f : 1f);
            m.renderQueue = pulse ? (int)RenderQueue.Transparent : -1;
            return Done(m);
        }

        // A piece of ink (FX/Alpha_Smoke): ink #15151A, opaque core, feathered edge, a gold rim on the head side, gold
        // embers inside. Particle pieces read draw / erase from Custom1 (the material's own values stay 0).
        static Material InkMaterial(string name, Texture mask, Color sparkColor, bool particles)
        {
            Material m = MaterialAsset("Mat_FX_" + name, "FX/Alpha_Smoke");
            m.SetTexture("_MainTex", mask);
            m.SetColor("_Color", InkSoft);
            m.SetFloat("_Opacity", 0.9f);
            m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_Feather", 0.15f);
            m.SetFloat("_ClipAt", 0.01f);
            Color rim = Linear(sparkColor) * 1.8f;
            rim.a = 1f;
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimWidth", 0.12f);
            Color front = Linear(sparkColor) * 2f;
            front.a = 1f;
            m.SetColor("_EdgeColor", front);
            m.SetFloat("_EdgeWidth", 0.04f);
            m.SetColor("_EmberColor", Linear(sparkColor) * 2.5f);
            m.SetFloat("_EmberTiling", 3f);
            m.SetFloat("_EmberAmount", 0.06f);
            m.SetFloat("_NoiseErase", 0f);
            m.SetTexture("_NoiseTex", Tex(FxTextureGenerator.NoisePacked));
            m.SetFloat("_NoiseTiling", 2f);
            m.SetFloat("_SwapUV", 0f);
            m.SetFloat("_UseCustomData", particles ? 1f : 0f);
            m.SetFloat("_FxProgress", particles ? 0f : 1f);
            m.SetFloat("_DepthOffset", 0f);
            return Done(m);
        }

        // The seal (FX/Decal): a hard-edged cutout burnt away by noise with a gold edge.
        static Material SealMaterial(string name, Texture mask, Color color)
        {
            Material m = MaterialAsset("Mat_FX_" + name, "FX/Decal");
            m.SetTexture("_MainTex", mask);
            m.SetColor("_Color", color);
            m.SetFloat("_Opacity", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Feather", 0f);
            m.SetFloat("_ClipAt", 0.5f);
            m.SetColor("_RimColor", Color.clear);
            m.SetColor("_EmberColor", Color.clear);
            Color front = Linear(RimGold) * 1.6f;
            front.a = 1f;
            m.SetColor("_EdgeColor", front);
            m.SetFloat("_EdgeWidth", 0.04f);
            m.SetFloat("_NoiseErase", 1f);
            m.SetTexture("_NoiseTex", Tex(FxTextureGenerator.NoisePacked));
            m.SetFloat("_NoiseTiling", 2f);
            m.SetFloat("_DepthOffset", 0f);
            return Done(m);
        }

        // The Heart Sutra ring: plain gold text, alpha blended, crisp band (no glow, no bloom); glyph height is
        // 2 × ring half width of the quad.
        static Material SutraMaterial(string name)
        {
            Material m = RingMaterial(name, RimGold, 0.95f, 0.88f, 0.075f, 1f, 0.03f, true);
            m.SetFloat("_Occlusion", 1f);
            m.SetFloat("_RingSoft", 0f);
            m.SetFloat("_NoiseStrength", 0f);
            return Done(m);
        }

        static Material RingMaterial(string name, Color gold, float intensity, float radius, float width, float repeat, float spin, bool reveal)
        {
            Material m = MaterialAsset("Mat_FX_" + name, "FX/Additive_Glow");
            m.SetTexture("_MainTex", Tex(FxTextureGenerator.SutraGlyph));
            m.SetColor("_Color", Linear(gold));
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Polar", 1f);
            m.EnableKeyword("_FX_POLAR");
            m.SetFloat("_RingRadius", radius);
            m.SetFloat("_RingWidth", width);
            m.SetFloat("_RingRepeat", repeat);
            m.SetFloat("_RingSpin", spin);
            m.SetFloat("_RevealByProgress", reveal ? 1f : 0f);
            m.SetTexture("_NoiseTex", Tex(FxTextureGenerator.NoisePacked));
            m.SetFloat("_NoiseStrength", 0.01f);
            return Done(m);
        }

        static Material GlowMaterial(string name, Texture mask, Color color, float intensity)
        {
            Material m = MaterialAsset("Mat_FX_" + name, "FX/Additive_Glow");
            m.SetTexture("_MainTex", mask);
            m.SetColor("_Color", Linear(color));
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Polar", 0f);
            m.DisableKeyword("_FX_POLAR");
            m.SetTexture("_NoiseTex", Tex(FxTextureGenerator.NoisePacked));
            return Done(m);
        }

        static Material MaterialAsset(string name, string shaderName)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }
            return m;
        }

        static Material Done(Material m)
        {
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- misc

        static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static Texture2D Tex(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        // HDR material colours are stored linear.
        static Color Linear(Color c)
        {
            return c.linear;
        }

        static ParticleSystem.MinMaxCurve ToMinMax(this AnimationCurve curve, float multiplier)
        {
            return new ParticleSystem.MinMaxCurve(multiplier, curve);
        }
    }
}
