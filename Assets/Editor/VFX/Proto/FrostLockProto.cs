using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // 禅足冰封 (缘起 #2) slots, first pass (Docs/vfx/yuanqi-frost-lock.md). Rebuilds the two slots, their meshes and
    // materials, and places a looping preview in VFXtests; the user tunes the details there afterwards (the 金足破阵
    // habit: ship defaults, tune in VFXtests).
    //  - frost_lock_hit, the ice block on every frozen racer (FrozenIceShell plays it on FrozenChanged). The crystal and
    //    the v1.5 ice the user picked on 2026-11-06 (Docs/vfx/yuanqi-frost-lock/probes/frost_build_v3.cs: backMid,
    //    veinsMid, coreMid, surfMid): inner walls (the crystal inside out), internal fractures, a weak core glow and the
    //    outer surface, as MeshRenderers like the swatch (on one-particle mesh systems the same materials rendered far
    //    fainter); SkillVfxMeshAlpha turns the layer's alpha curve into their fade in, grow and melt. The layer scales
    //    with the body (a giant gets a x4 block). Plus twinkling glints and flecks thrown off at the thaw.
    //  - frost_lock_release, the frozen ground under the caster (Yuanqi_FrostLock.PlayObservers): a frost sheet and
    //    radial cracks that spread to R in 0.12 s, a ring that flashes out to R and stays as its rim, ground glints.
    //    Laid once at the trigger and left in place, in world units (casterScaling None): R does not grow with the body.
    // Pack textures used here (GroundCrack_RadialPostExplody) need the pack imported (Assets/Piloto Studio, local only);
    // run "Copy Used Pack Assets Into Project" before committing.
    public static class FrostLockProto
    {
        public const string GroundVfxId = "frost_lock_release";
        public const string IceVfxId = "frost_lock_hit";
        const string Folder = "Assets/VFX/禅足冰封";
        const string MeshFolder = Folder + "/Meshes";
        const string MaterialFolder = Folder + "/Materials";
        const string BuddahPrefab = "Assets/Character/Prefab/Buddah.prefab";
        const string StreamSourcePrefab = "Assets/VFX/神足通/Release/VFX_Accel_Release.prefab";

        const string IceLayer = "FX_FrostLock_Imp_C_Mesh_01";
        const string IceAccentLayer = "FX_FrostLock_Imp_A_Sparks_01";
        const string GroundLayer = "FX_FrostLock_Imp_C_Decal_01";
        const string GroundAccentLayer = "FX_FrostLock_Imp_A_Sparks_02";

        // ---- Defaults (2026-11-06). Times are shares of the window (reference 1.5 s = YuanqiEffectFrostLock.freezeSeconds).
        const float ReferenceSeconds = 1.5f;
        const int CrystalSeed = 7;                // the swatch crystal the user saw
        const float IcePopFrom = 0.88f;           // the block grows from 88 % ...
        const float IcePopShare = 0.06f;          // ... to full size in the first 6 % (0.09 s)
        const float IceMeltShare = 0.15f;         // and melts away in the last 15 % (0.22 s)
        const float ShardBurstAt = 1.3f;          // reference seconds: flecks break off as the melt starts
        const int ShardCount = 24;
        const float GlintRate = 6f;               // per second
        static readonly Vector2 GlintSize = new Vector2(1.1f, 1.8f);   // metres at body scale 1
        const float GroundRadius = 12f;           // R, YuanqiEffectFrostLock.radiusMeters (a test keeps them equal)
        const float GroundSpreadShare = 0.08f;    // 0.12 s, the placeholder's "瞬间生成"
        const float GroundFadeShare = 0.25f;
        const float GroundOffsetY = -0.3f;        // track below the pivot at body scale 1 (README section 4)
        // How much of each ground quad's width actually shows (the pack textures are sampled linear, so their
        // mid-greys fall away early): the quads are sized so what shows reaches R.
        const float SheetVisibleShare = 0.5f;
        const float CracksVisibleShare = 0.45f;
        const float RimShare = 0.93f;             // FX_Ring_01_T's ring radius in the texture

        static SkillReleaseProto.Skill Slot(string vfxId)
        {
            foreach (SkillReleaseProto.Skill skill in SkillReleaseProto.Skills)
                if (skill.vfxId == vfxId)
                    return skill;
            throw new System.InvalidOperationException(vfxId + " is not in SkillReleaseProto.Skills");
        }

        [MenuItem("Tools/BuddahGo/VFX/Proto/禅足冰封/Rebuild Slots (ice block + frozen ground)")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            var crack = AssetDatabase.LoadAssetAtPath<Texture2D>(PilotoTexture("GroundCrack_RadialPostExplody.png"));
            if (crack == null)
                return "import the Piloto pack first (GroundCrack_RadialPostExplody missing)";
            var log = new System.Text.StringBuilder();
            System.IO.Directory.CreateDirectory(MeshFolder);
            System.IO.Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            Bounds body = BodyBounds();
            Mesh crystal = SaveMesh(BuildCrystal(body, CrystalSeed), "Mesh_FX_FrostLock_Crystal");
            Mesh inside = SaveMesh(InsideOut(crystal), "Mesh_FX_FrostLock_CrystalBack");
            Mesh glintPoints = SaveMesh(GlintPoints(crystal, 6, 3f), "Mesh_FX_FrostLock_GlintPoints");
            log.Append("body bounds " + body.size.ToString("F1") + "; ");

            Materials m = BuildMaterials(crack, Load<Texture2D>("Assets/VFX/Shared/Piloto/Textures/Gradient_maker_simple_round_radial.png"), Load<Texture2D>(PilotoTexture("FX_Ring_01_T.png")));
            // ---- ice block (hit slot, on the frozen racer)
            SkillReleaseProto.Skill iceSlot = Slot(IceVfxId);
            log.Append(SkillReleaseProto.Build(iceSlot, false)).Append("; ");
            GameObject root = PrefabUtility.LoadPrefabContents(iceSlot.PrefabPath);
            try
            {
                Transform scaled = ClearLayers(root);
                SkillVfxLayer ice = NewLayer(scaled, IceLayer, SkillVfxFunction.Mesh, SkillVfxTier.Core, 1, true, SkillVfxAnchor.CasterAttached, SkillVfxCasterScaling.Whole, Vector3.zero);
                // The block fades in with the pop and melts at the end (SkillVfxMeshAlpha reads this curve); a freeze
                // that ends early (reset, respawn) ends the window, which hides the meshes at once.
                ice.alpha = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(IcePopShare, 1f), new Keyframe(1f - IceMeltShare, 1f), new Keyframe(1f, 0f));
                // Back to front, as the swatch: inner walls, fractures (0.9 shell), core glow (0.8 shell), surface.
                IceShell(ice.transform, "IceBack", inside, m.back, 1f, body.center);
                IceShell(ice.transform, "IceVeins", crystal, m.veins, 0.9f, body.center);
                IceShell(ice.transform, "IceCore", crystal, m.core, 0.8f, body.center);
                IceShell(ice.transform, "IceSurface", crystal, m.surface, 1f, body.center);

                SkillVfxLayer accent = NewLayer(scaled, IceAccentLayer, SkillVfxFunction.Sparks, SkillVfxTier.Accent, 2, false, SkillVfxAnchor.CasterAttached, SkillVfxCasterScaling.Whole, Vector3.zero);
                accent.clearOnDissipation = true;
                Glints(accent.transform, glintPoints, m.glint);
                Shards(accent.transform, crystal, m.shard);
                SetReference(root, ReferenceSeconds);
                Finish(root);
                PrefabUtility.SaveAsPrefabAsset(root, iceSlot.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // ---- frozen ground (release slot, laid at the caster)
            SkillReleaseProto.Skill groundSlot = Slot(GroundVfxId);
            log.Append(SkillReleaseProto.Build(groundSlot, false)).Append("; ");
            root = PrefabUtility.LoadPrefabContents(groundSlot.PrefabPath);
            try
            {
                Transform scaled = ClearLayers(root);
                var offset = new Vector3(0f, GroundOffsetY, 0f);
                SkillVfxLayer ground = NewLayer(scaled, GroundLayer, SkillVfxFunction.Decal, SkillVfxTier.Core, 1, true, SkillVfxAnchor.CasterRelative, SkillVfxCasterScaling.None, offset);
                float diameter = GroundRadius * 2f;
                GroundDisc(ground.transform, "FrostSheet", m.sheet, diameter / SheetVisibleShare, 0.04f,
                    Grow(0.15f, GroundSpreadShare),
                    Fade(0.03f, 1f, 1f - GroundFadeShare, 1f));
                GroundDisc(ground.transform, "Cracks", m.cracks, diameter / CracksVisibleShare, 0.07f,
                    Grow(0.2f, GroundSpreadShare * 1.5f),
                    Fade(0.05f, 1f, 1f - GroundFadeShare, 1f));
                // The rim at R: flashes as it runs out (空间瞬时膨胀) and stays as the edge of the frozen zone.
                GroundDisc(ground.transform, "Rim", m.rim, diameter / RimShare, 0.1f,
                    Grow(0.1f, GroundSpreadShare),
                    RimFade());

                SkillVfxLayer sparkle = NewLayer(scaled, GroundAccentLayer, SkillVfxFunction.Sparks, SkillVfxTier.Accent, 2, false, SkillVfxAnchor.CasterRelative, SkillVfxCasterScaling.None, offset);
                GroundGlints(sparkle.transform, m.glint);
                SetReference(root, ReferenceSeconds);
                Finish(root);
                PrefabUtility.SaveAsPrefabAsset(root, groundSlot.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return log.Append("rebuilt ").Append(iceSlot.PrefabPath).Append(" and ").Append(groundSlot.PrefabPath).ToString();
        }

        static string PilotoTexture(string file)
        {
            string vendored = "Assets/VFX/Shared/Piloto/Textures/" + file;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(vendored) != null ? vendored : VfxPackVendor.PackRoot + "Textures/" + file;
        }

        // ---- slot structure ------------------------------------------------------------------------------------------

        static Transform ClearLayers(GameObject root)
        {
            Transform scaled = root.transform.Find("Scaled");
            foreach (SkillVfxLayer old in scaled.GetComponentsInChildren<SkillVfxLayer>(true))
                Object.DestroyImmediate(old.gameObject);
            return scaled;
        }

        static SkillVfxLayer NewLayer(Transform scaled, string name, SkillVfxFunction function, SkillVfxTier tier, int priority,
            bool gameplayInfo, SkillVfxAnchor anchor, SkillVfxCasterScaling scaling, Vector3 offset)
        {
            var layer = SkillReleaseProto.Child(scaled, name).gameObject.AddComponent<SkillVfxLayer>();
            layer.phase = SkillVfxPhase.Imp;
            layer.tier = tier;
            layer.function = function;
            layer.priority = priority;
            layer.gameplayInfo = gameplayInfo;
            layer.anchor = anchor;
            layer.casterScaling = scaling;
            layer.casterOffset = offset;
            layer.casterEuler = Vector3.zero;
            layer.startUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.start = 0f;
            layer.endUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.end = -1f;
            // The freeze length comes from the table: the whole layer runs faster or slower with it.
            layer.timeScaling = SkillVfxTimeScaling.Stretch;
            layer.particlesDieNaturally = true;
            layer.transform.localPosition = offset;
            return layer;
        }

        static void SetReference(GameObject root, float seconds)
        {
            var so = new SerializedObject(root.GetComponent<SkillVfxPhaseController>());
            SerializedProperty references = so.FindProperty("referenceSeconds");
            references.arraySize = 1;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Finish(GameObject root)
        {
            int layer = LayerMask.NameToLayer(SkillVfxPhaseController.CharacterLayerName);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (layer >= 0)
                    t.gameObject.layer = layer;
            List<ParticleSystemVertexStream> streams = VertexStreams();
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                // Every particle renderer feeds UberFXSG the pack's own streams.
                ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(streams);
            }
        }

        // ---- particle systems ----------------------------------------------------------------------------------------

        static ParticleSystem NewSystem(Transform parent, string name, float lifetime, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = ReferenceSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            // The ground quads are 25-32 m: the default cap (half the view) shrank them in close views.
            renderer.maxParticleSize = 10f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        static void Burst(ParticleSystem ps, float time, int count)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count, (short)count, 1, 0.01f) });
        }

        // One crystal shell as a MeshRenderer (the swatch's setup): the mesh is authored around the body (visual root
        // frame), a shell scale s shrinks it about the body centre. SkillVfxMeshAlpha fades and grows it with the layer.
        static void IceShell(Transform layer, string name, Mesh mesh, Material material, float shellScale, Vector3 bodyCentre)
        {
            var go = new GameObject(name);
            go.transform.SetParent(layer, false);
            go.transform.localPosition = bodyCentre * (1f - shellScale);
            go.transform.localScale = Vector3.one * shellScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var fade = go.AddComponent<SkillVfxMeshAlpha>();
            fade.popFrom = IcePopFrom;
            fade.pivot = bodyCentre;
        }

        static void Glints(Transform layer, Mesh points, Material material)
        {
            ParticleSystem ps = NewSystem(layer, "IceGlints", 0.4f, 24);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(GlintSize.x, GlintSize.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = GlintRate;
            Burst(ps, 0f, 4);
            main.duration = ReferenceSeconds * (1f - IceMeltShare);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
            shape.mesh = points;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        // Flecks breaking off the block as it thaws: bright specks thrown off its surface, falling.
        static void Shards(Transform layer, Mesh crystal, Material material)
        {
            ParticleSystem ps = NewSystem(layer, "ThawFlecks", 0.6f, ShardCount);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 2f;
            Burst(ps, ShardBurstAt, ShardCount);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh = crystal;
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.02f, 1f, 0.55f, 1f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        static void GroundDisc(Transform layer, string name, Material material, float size, float lift, AnimationCurve grow, ParticleSystem.MinMaxGradient fade)
        {
            ParticleSystem ps = NewSystem(layer, name, ReferenceSeconds, 1);
            ps.transform.localPosition = new Vector3(0f, lift, 0f);
            ParticleSystem.MainModule main = ps.main;
            main.startSize = size;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(ps, 0f, 1);
            ParticleSystem.SizeOverLifetimeModule growth = ps.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, grow);
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = fade;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
        }

        static void GroundGlints(Transform layer, Material material)
        {
            ParticleSystem ps = NewSystem(layer, "GroundGlints", 0.5f, 24);
            ps.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.04f, 10, 10, 1, 0.01f),
                new ParticleSystem.Burst(0.5f, 6, 6, 1, 0.01f),
            });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = GroundRadius * 0.92f;
            shape.radiusThickness = 0.8f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        // Size: from `from` at the start to full size at `share` of the life (ease out), then held.
        static AnimationCurve Grow(float from, float share)
        {
            return new AnimationCurve(new Keyframe(0f, from, 0f, (1f - from) / share * 2f), new Keyframe(share, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        }

        // Alpha 0 → 1 by `inEnd`, held, then 1 → 0 from `outStart` to `outEnd`.
        static ParticleSystem.MinMaxGradient Fade(float inEnd, float peak, float outStart, float outEnd)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inEnd), new GradientAlphaKey(peak, outStart), new GradientAlphaKey(0f, outEnd) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxGradient RimFade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.03f), new GradientAlphaKey(0.45f, 0.2f),
                    new GradientAlphaKey(0.45f, 1f - GroundFadeShare), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        // The pack's particle streams (position, normal, colour, UV, custom data), so UberFXSG reads what it expects.
        static List<ParticleSystemVertexStream> VertexStreams()
        {
            var streams = new List<ParticleSystemVertexStream>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(StreamSourcePrefab);
            if (source != null)
            {
                foreach (ParticleSystemRenderer r in source.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (r.renderMode != ParticleSystemRenderMode.Mesh || r.activeVertexStreamsCount == 0)
                        continue;
                    r.GetActiveVertexStreams(streams);
                    break;
                }
            }
            if (streams.Count == 0)
                streams.AddRange(new[] { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            if (!streams.Contains(ParticleSystemVertexStream.Normal))
                streams.Insert(1, ParticleSystemVertexStream.Normal);
            return streams;
        }

        // ---- materials (UberFXSG, no opaque texture: the body is not in it, README / yuanqi-frost-lock.md section 2) ----

        struct Materials
        {
            public Material back, veins, core, surface, glint, shard, sheet, cracks, rim;
        }

        static Materials BuildMaterials(Texture2D crack, Texture2D disc, Texture2D ring)
        {
            Texture2D crystalTex = Load<Texture2D>("Assets/VFX/Shared/Piloto/Textures/CrystalNoise.png");
            Texture2D noiseTex = Load<Texture2D>("Assets/VFX/Shared/Piloto/Textures/Tx_Noise_20.png");
            Texture2D veinTex = Load<Texture2D>("Assets/VFX/Shared/Piloto/Textures/Noise_TilingSoftWebbing.png");
            Texture2D flareTex = Load<Texture2D>("Assets/Plugins/Piloto Studio/Textures/Flare_Cross.png");
            Texture2D fleckTex = Load<Texture2D>("Assets/Plugins/Piloto Studio/Textures/Fleks_Shiny.png");
            Material alphaSrc = Load<Material>("Assets/VFX/Shared/Piloto/Materials/Smoke_screen_cloud_sharp_fb_1_alpha_soft.mat");
            Material addSrc = Load<Material>("Assets/VFX/Shared/Piloto/Materials/Ring_Halo_faded_add_soft.mat");

            var m = new Materials();
            // v1.5 (frost_build_v3.cs), back to front.
            m.back = Fx(alphaSrc, "IceBack", crystalTex, noiseTex, 0.3f, 3000);
            m.back.SetTextureOffset("_MainTex", new Vector2(0.37f, 0.61f));
            Ramp(m.back, new Color(0.16f, 0.19f, 0.22f, 0.18f), new Color(0.48f, 0.55f, 0.60f, 0.30f), new Color(1.1f, 1.15f, 1.2f, 0.45f), 0.4f, 0.8f);
            Fresnel(m.back, new Color(0.5f, 0.55f, 0.6f, 1f), 0.8f, 3f, false);

            m.veins = Fx(alphaSrc, "IceVeins", veinTex, noiseTex, 1.4f, 3001);
            Ramp(m.veins, new Color(0f, 0f, 0f, 0f), new Color(0.7f, 0.75f, 0.8f, 0.08f), new Color(1.4f, 1.45f, 1.5f, 0.5f), 0.55f, 0.82f);
            Fresnel(m.veins, new Color(0f, 0f, 0f, 1f), 0f, 1f, false);
            m.veins.SetVector("_DetailDistortionChannel", new Vector4(0.12f, 0, 0, 0));

            // Main texture left empty: UberFXSG's default is white, so the alpha is 1 and only the inverted Fresnel shapes it.
            m.core = Fx(addSrc, "IceCore", null, noiseTex, 1f, 3002);
            Ramp(m.core, new Color(0.03f, 0.032f, 0.035f, 1f), new Color(0.03f, 0.032f, 0.035f, 1f), new Color(0.03f, 0.032f, 0.035f, 1f), 0.4f, 0.8f);
            Fresnel(m.core, new Color(0.22f, 0.23f, 0.25f, 1f), 1.0f, 1.5f, true);

            m.surface = Fx(alphaSrc, "IceSurface", crystalTex, noiseTex, 0.3f, 3003);
            Ramp(m.surface, new Color(0.12f, 0.14f, 0.16f, 0.15f), new Color(0.70f, 0.75f, 0.80f, 0.40f), new Color(1.8f, 1.85f, 1.9f, 0.85f), 0.45f, 0.78f);
            Fresnel(m.surface, new Color(2.0f, 2.05f, 2.1f, 1f), 1.8f, 3.5f, false);
            m.surface.SetVector("_DetailDistortionChannel", new Vector4(0.05f, 0, 0, 0));

            m.glint = Fx(addSrc, "Glint", flareTex, noiseTex, 1f, 3004);
            m.glint.DisableKeyword("_USERAMP"); m.glint.SetFloat("_USERAMP", 0f);
            m.glint.DisableKeyword("_FRESNEL"); m.glint.SetFloat("_FRESNEL", 0f);

            m.shard = Fx(addSrc, "ThawFlecks", fleckTex, noiseTex, 1f, 3004);
            m.shard.DisableKeyword("_USERAMP"); m.shard.SetFloat("_USERAMP", 0f);
            m.shard.DisableKeyword("_FRESNEL"); m.shard.SetFloat("_FRESNEL", 0f);
            m.shard.SetVector("_MainAlphaChannel", new Vector4(0, 0, 0, 1));   // Fleks_Shiny is white, shapes in alpha

            // Ground: frost sheet (crystal facets in a soft round mask), radial cracks, the rim at R (the pack's thin
            // double ring). White and grey only.
            m.sheet = Fx(alphaSrc, "GroundSheet", crystalTex, noiseTex, 4f, 3000);
            m.sheet.EnableKeyword("_USEALPHAOVERRIDE"); m.sheet.SetFloat("_USEALPHAOVERRIDE", 1f);
            m.sheet.SetTexture("_AlphaOverride", disc);
            m.sheet.SetVector("_AlphaOverrideChannel", new Vector4(1, 0, 0, 0));
            Ramp(m.sheet, new Color(0.45f, 0.5f, 0.55f, 0.35f), new Color(0.85f, 0.9f, 0.95f, 0.6f), new Color(1.5f, 1.55f, 1.6f, 0.85f), 0.4f, 0.75f);
            // The pack's masks are sampled linear (mid-greys read ~0.2): weighted channels widen what shows.
            m.sheet.SetVector("_AlphaOverrideChannel", new Vector4(4f, 0, 0, 0));
            m.sheet.SetVector("_MainAlphaChannel", new Vector4(2f, 0, 0, 0));
            Fresnel(m.sheet, Color.black, 0f, 1f, false);
            m.sheet.DisableKeyword("_FRESNEL"); m.sheet.SetFloat("_FRESNEL", 0f);

            m.cracks = Fx(alphaSrc, "GroundCracks", crack, noiseTex, 1f, 3001);
            Ramp(m.cracks, new Color(0f, 0f, 0f, 0f), new Color(0.9f, 0.93f, 0.96f, 0.9f), new Color(2.2f, 2.25f, 2.3f, 1f), 0.02f, 0.12f);
            m.cracks.SetVector("_MainAlphaChannel", new Vector4(6f, 0, 0, 0));
            m.cracks.DisableKeyword("_FRESNEL"); m.cracks.SetFloat("_FRESNEL", 0f);

            m.rim = Fx(addSrc, "GroundRim", ring, noiseTex, 1f, 3002);
            Ramp(m.rim, new Color(0f, 0f, 0f, 0f), new Color(0.9f, 0.95f, 1f, 0.8f), new Color(2.5f, 2.55f, 2.6f, 1f), 0.01f, 0.08f);
            m.rim.SetVector("_MainAlphaChannel", new Vector4(10f, 0, 0, 0));
            m.rim.DisableKeyword("_FRESNEL"); m.rim.SetFloat("_FRESNEL", 0f);

            m.back = SaveMaterial(m.back); m.veins = SaveMaterial(m.veins); m.core = SaveMaterial(m.core);
            m.surface = SaveMaterial(m.surface); m.glint = SaveMaterial(m.glint); m.shard = SaveMaterial(m.shard);
            m.sheet = SaveMaterial(m.sheet); m.cracks = SaveMaterial(m.cracks); m.rim = SaveMaterial(m.rim);
            return m;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("missing " + path);
            return asset;
        }

        // The swatch's base setup: colour ramp on, erosion / alpha override / soft particles off, Fresnel on.
        static Material Fx(Material source, string name, Texture2D tex, Texture2D noise, float tiling, int queue)
        {
            var m = new Material(source) { name = "Mat_FX_FrostLock_" + name };
            m.SetTexture("_MainTex", tex);
            m.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
            m.SetTextureOffset("_MainTex", Vector2.zero);
            m.SetVector("_MainTexturePanning", Vector4.zero);
            m.SetVector("_MainTextureChannel", new Vector4(1, 1, 1, 0));
            m.SetVector("_MainAlphaChannel", new Vector4(1, 0, 0, 0));
            m.EnableKeyword("_USERAMP"); m.SetFloat("_USERAMP", 1f);
            m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 0f); m.SetFloat("_BUILTIN_AlphaClip", 0f);
            m.EnableKeyword("_DISABLEEROSION"); m.SetFloat("_DISABLEEROSION", 1f);
            m.DisableKeyword("_USEALPHAOVERRIDE"); m.SetFloat("_USEALPHAOVERRIDE", 0f);
            m.SetTexture("_AlphaOverride", null);
            m.DisableKeyword("_USESOFTALPHA"); m.SetFloat("_USESOFTALPHA", 0f);
            m.EnableKeyword("_FRESNEL"); m.SetFloat("_FRESNEL", 1f);
            m.SetTexture("_DetailNoise", noise);
            m.SetVector("_DetailDistortionChannel", Vector4.zero);
            m.SetVector("_DetailMultiplyChannel", Vector4.zero);
            m.SetVector("_DetailAdditiveChannel", Vector4.zero);
            m.SetVector("_DetailVertexOffsetChannel", Vector4.zero);
            m.SetVector("_DetailNoisePanning", Vector4.zero);
            m.SetFloat("_Desaturate", 0f);
            m.renderQueue = queue;
            return m;
        }

        static void Ramp(Material m, Color lo, Color mid, Color hi, float pLo, float pHi)
        {
            m.SetColor("_LastColor", lo); m.SetColor("_MidColor", mid); m.SetColor("_WhiteColor", hi);
            m.SetFloat("_MiddlePointPos1", pLo); m.SetFloat("_MiddlePointPos", pHi);
        }

        static void Fresnel(Material m, Color c, float scale, float power, bool invert)
        {
            m.SetColor("_FresnelColor", c); m.SetFloat("_FresnelScale", scale); m.SetFloat("_FresnelPower", power); m.SetFloat("_Invert_Fresnel", invert ? 1f : 0f);
        }

        // Writes over an existing material so its GUID (and every reference) stays.
        static Material SaveMaterial(Material built)
        {
            string path = MaterialFolder + "/" + built.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.shader = built.shader;
            existing.CopyPropertiesFromMaterial(built);
            existing.shaderKeywords = built.shaderKeywords;
            existing.renderQueue = built.renderQueue;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        // ---- meshes --------------------------------------------------------------------------------------------------

        // The Buddah body (head + hands, default pose) in its visual root's frame at body scale 1: where the slot's
        // CasterAttached layers stand (SkillVfxPhaseController follows the visual root, scale from the racer root).
        static Bounds BodyBounds()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BuddahPrefab);
            try
            {
                var bridge = root.GetComponent<NewBuddah.PredictionV2.Visual.BuddahPredictionVisualRootBridge>();
                Transform visual = bridge != null && bridge.GetVisualRoot() != null ? bridge.GetVisualRoot() : root.transform;
                Matrix4x4 toLayer = Matrix4x4.TRS(visual.position, visual.rotation, Vector3.one).inverse;
                var bounds = new Bounds();
                bool any = false;
                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    Matrix4x4 m = toLayer * smr.transform.localToWorldMatrix;
                    foreach (Vector3 v in baked.vertices)
                    {
                        Vector3 p = m.MultiplyPoint3x4(v);
                        if (any) bounds.Encapsulate(p); else bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    Object.DestroyImmediate(baked);
                    break;   // the swatch measured the first skinned renderer (the body)
                }
                if (!any)
                    throw new System.InvalidOperationException(BuddahPrefab + " has no skinned body");
                return bounds;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The swatch crystal (frost_build_v3.cs): a tall irregular prism around the body plus nine tilted shards round
        // its base; flat shaded, per-face planar UVs (~0.6 x body height per tile).
        static Mesh BuildCrystal(Bounds bb, int seed)
        {
            var rnd = new System.Random(seed);
            System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            var pos = new List<Vector3>();
            var tris = new List<int>();
            System.Action<Vector3, Vector3, float, float, float, int> prism = (basePt, axis, radius, height, tipLen, sides) =>
            {
                axis.Normalize();
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 fwd = Vector3.Cross(axis, side);
                var ringA = new Vector3[sides];
                var ringB = new Vector3[sides];
                float rot = R(0f, 6.28f);
                for (int k = 0; k < sides; k++)
                {
                    float ang = rot + k * 6.2832f / sides + R(-0.15f, 0.15f);
                    Vector3 dir = side * Mathf.Cos(ang) + fwd * Mathf.Sin(ang);
                    ringA[k] = basePt + dir * radius * R(0.85f, 1.1f);
                    ringB[k] = basePt + axis * height * R(0.9f, 1.05f) + dir * radius * R(0.8f, 1.05f);
                }
                Vector3 tip = basePt + axis * (height + tipLen) + side * R(-0.1f, 0.1f) * radius;
                Vector3 bottom = basePt - axis * radius * 0.6f;
                System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) =>
                {
                    int s = pos.Count;
                    pos.Add(a); pos.Add(b); pos.Add(c);
                    tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                };
                for (int k = 0; k < sides; k++)
                {
                    int n = (k + 1) % sides;
                    tri(ringA[k], ringB[k], ringB[n]); tri(ringA[k], ringB[n], ringA[n]);
                    tri(ringB[k], tip, ringB[n]);
                    tri(ringA[k], ringA[n], bottom);
                }
            };
            Vector3 c0 = bb.center;
            float w = Mathf.Max(bb.extents.x, bb.extents.z);
            float h = bb.size.y;
            prism(new Vector3(c0.x, bb.min.y - 0.05f, c0.z), new Vector3(R(-0.08f, 0.08f), 1f, R(-0.08f, 0.08f)), w * 1.35f, h * 0.95f, h * 0.45f, 7);
            const int count = 9;
            for (int i = 0; i < count; i++)
            {
                float a = i * 6.2832f / count + R(-0.25f, 0.25f);
                var outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 basePt = new Vector3(c0.x, bb.min.y, c0.z) + outDir * w * R(0.9f, 1.4f);
                Vector3 axis = (Vector3.up * R(1.2f, 2.2f) + outDir).normalized;
                float sh = h * R(0.25f, 0.65f);
                prism(basePt, axis, w * R(0.18f, 0.32f), sh, sh * R(0.3f, 0.6f), rnd.Next(4, 7));
            }
            return Finalise("Mesh_FX_FrostLock_Crystal", pos, tris, 0.6f * h);
        }

        static Mesh Finalise(string name, List<Vector3> pos, List<int> tris, float tile)
        {
            var m = new Mesh { name = name };
            m.SetVertices(pos);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            Vector3[] nrm = m.normals;
            var uv = new Vector2[pos.Count];
            for (int i = 0; i < pos.Count; i++)
            {
                Vector3 n = nrm[i], p = pos[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                Vector2 u = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                uv[i] = u / tile;
            }
            m.uv = uv;
            var col = new Color[pos.Count];
            for (int i = 0; i < col.Length; i++)
                col[i] = Color.white;
            m.colors = col;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Mesh InsideOut(Mesh source)
        {
            Mesh m = Object.Instantiate(source);
            m.name = "Mesh_FX_FrostLock_CrystalBack";
            int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                int tmp = t[i + 1];
                t[i + 1] = t[i + 2];
                t[i + 2] = tmp;
            }
            m.triangles = t;
            m.RecalculateNormals();
            return m;
        }

        // The highest crystal points at least `spacing` apart, one tiny triangle each (the shape module needs triangles).
        static Mesh GlintPoints(Mesh crystal, int count, float spacing)
        {
            Vector3[] verts = crystal.vertices;
            var order = new List<int>();
            for (int k = 0; k < verts.Length; k++)
                order.Add(k);
            order.Sort((x, y) => verts[y].y.CompareTo(verts[x].y));
            var picked = new List<Vector3>();
            foreach (int k in order)
            {
                bool near = false;
                foreach (Vector3 q in picked)
                    if ((q - verts[k]).sqrMagnitude < spacing * spacing) { near = true; break; }
                if (near)
                    continue;
                picked.Add(verts[k]);
                if (picked.Count >= count)
                    break;
            }
            var pos = new List<Vector3>();
            var tris = new List<int>();
            foreach (Vector3 p in picked)
            {
                int s = pos.Count;
                pos.Add(p); pos.Add(p + new Vector3(0.01f, 0f, 0f)); pos.Add(p + new Vector3(0f, 0f, 0.01f));
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            var m = new Mesh { name = "Mesh_FX_FrostLock_GlintPoints" };
            m.SetVertices(pos);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Writes over an existing mesh asset so its GUID stays.
        static Mesh SaveMesh(Mesh built, string name)
        {
            built.name = name;
            string path = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        // ---- VFXtests preview ----------------------------------------------------------------------------------------

        // VFXtests (local, not in the repository): adds what is missing of a caster, three frozen racers around it
        // inside R (one a x4 giant), a race camera and the looping SkillVfxPreviewFrostLock, then gives the scene
        // RaceMap's look (VfxTestsRaceMapLook). Press Play to watch; the slots and the preview's fields are tuned there.
        // Re-run safe.
        [MenuItem("Tools/BuddahGo/VFX/Proto/禅足冰封/Place Preview In VFXtests")]
        public static void PlaceMenu() { Debug.Log(PlaceInVfxTests()); }

        public static string PlaceInVfxTests()
        {
            string path = PalmFrontalProto.StagePath;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path != path)
            {
                if (active.isDirty)
                    return active.path + " has unsaved changes";
                if (!System.IO.File.Exists(path))
                    return path + " is missing: it is the user's local test scene";
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            var log = new System.Text.StringBuilder();

            Transform preview = Root("FrostLockPreview");
            if (preview == null)
            {
                preview = new GameObject("FrostLockPreview").transform;
                preview.position = new Vector3(0f, 0f, 400f);   // clear of the 金足破阵 dash lane at the origin
                log.Append("added FrostLockPreview; ");
            }
            var driver = preview.GetComponent<SkillVfxPreviewFrostLock>();
            if (driver == null)
                driver = preview.gameObject.AddComponent<SkillVfxPreviewFrostLock>();
            // FishNet gives a root a NetworkObject when racers sit under it, and switches unspawned scene network objects
            // off in Play mode: the racers stand at the scene root (as 金足破阵's Player) and the root keeps none.
            foreach (string old in new[] { "Caster", "Frozen_A", "Frozen_B", "Frozen_Giant" })
            {
                Transform child = preview.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            PalmFrontalProto.StripNetworking(preview.gameObject);

            Transform caster = Racer(preview, "Caster", Vector3.zero, 0f, 1f, log);
            // Inside R = 12 m, as S9's dummies (9.3 m, 9.1 m); the giant shows the x4 block.
            var victims = new[]
            {
                Racer(preview, "Frozen_A", new Vector3(-9.3f, 0f, 2f), 20f, 1f, log),
                Racer(preview, "Frozen_B", new Vector3(8.9f, 0f, -2.5f), -15f, 1f, log),
                Racer(preview, "Frozen_Giant", new Vector3(2f, 0f, 60f), 180f, 4f, log),
            };

            Transform floor = preview.Find("Floor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
                floor.name = "Floor";
                floor.SetParent(preview, false);
                floor.localPosition = new Vector3(0f, GroundOffsetY, 30f);
                floor.localScale = new Vector3(20f, 1f, 20f);
                log.Append("added Floor; ");
            }

            Transform cam = preview.Find("Cam_Race");
            if (cam == null)
            {
                var camera = new GameObject("Cam_Race").AddComponent<Camera>();
                camera.transform.SetParent(preview, false);
                camera.depth = 11f;
                cam = camera.transform;
                log.Append("added Cam_Race; ");
            }

            driver.groundSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Slot(GroundVfxId).PrefabPath);
            driver.iceSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Slot(IceVfxId).PrefabPath);
            driver.caster = caster;
            driver.frozen = victims;
            driver.raceCamera = cam.GetComponent<Camera>();
            EditorUtility.SetDirty(driver);

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            // Judge the ice in the race's light, sky, fog and post.
            log.Append(VfxTestsRaceMapLook.Apply()).Append("; ");
            return log + "(press Play: FrostLockPreview loops the trigger, seen from its Cam_Race)";
        }

        static Transform Racer(Transform parent, string name, Vector3 position, float yaw, float scale, System.Text.StringBuilder log)
        {
            name = "FrostLock_" + name;
            Transform racer = Root(name);
            if (racer == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuddahPrefab);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                PalmFrontalProto.StripNetworking(go);
                racer = go.transform;
                racer.SetPositionAndRotation(parent.TransformPoint(position), parent.rotation * Quaternion.Euler(0f, yaw, 0f));
                log.Append("added " + name + "; ");
            }
            racer.localScale = Vector3.one * scale;
            return racer;
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
