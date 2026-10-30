using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Reuse prototype (Docs/vfx/yingyang-vfx-analysis.md section 5): the palm effect assembled only from particle systems,
    // materials and textures of Piloto Studio's Realistic Ying Yang VFX, cloned out of its prefabs, re-timed and re-scaled,
    // and wired to SkillVfxPhaseController layers. The hand itself is our palm mesh drawn as one mesh particle with the
    // package's materials (it needs UVs, so the prototype keeps a copy with a planar front projection). Throwaway: lives
    // in Assets/VFX/如来神掌/Proto and is not registered anywhere.
    internal static class PalmYingYangProto
    {
        const string YY = "Assets/Piloto Studio/Super Realistic FX Bundle 02/Realistic Ying Yang/";
        internal const string Folder = "Assets/VFX/如来神掌/Proto";
        internal const string PrefabPath = Folder + "/VFX_Palm_YY.prefab";
        internal const string BufPrefabPath = Folder + "/VFX_Palm_YY_Buf.prefab";
        const string PalmUvPath = Folder + "/Mesh_FX_Palm_UV.asset";

        // Package effects are built around 10-15 units; the palm is 60 × 90.
        const float PackageToPalm = 6f;
        static readonly Color Gold = new Color(1f, 0.78f, 0.32f, 1f);
        static readonly Color WarmWhite = new Color(1f, 0.93f, 0.75f, 1f);

        [MenuItem("Tools/BuddahGo/VFX/Build Palm Ying Yang Proto")]
        internal static void Build()
        {
            Directory.CreateDirectory(Folder);
            Mesh palm = PalmUvMesh();

            var root = new GameObject("VFX_Palm_YY");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = "palm";
            so.FindProperty("referenceColliderSize").vector3Value = PalmMeshBaker.ReferenceCollider;
            so.FindProperty("scaledRoot").objectReferenceValue = scaled;
            so.FindProperty("bodyFitRoot").objectReferenceValue = bodyFit;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- Chg: the hand as light (Orb's flowing inner glow + Shield's Fresnel wall on the palm mesh), half size.
            SkillVfxLayer chgBody = Layer(bodyFit, "FX_PalmYY_Chg_C_Mesh_01", SkillVfxPhase.Chg, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
            chgBody.driveScale = true;
            chgBody.scale = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 0.5f);
            chgBody.clearOnDissipation = true;
            Body(chgBody.transform, palm, 1.0f);

            // ---- Chg: Vertical Explotion's inward radial disk + Orb's gathering smoke around the hand.
            SkillVfxLayer gather = Layer(scaled, "FX_PalmYY_Chg_S_Smoke_01", SkillVfxPhase.Chg, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            gather.clearOnDissipation = true;
            Transform disk = Clone(YY + "Ying Yang Vertical Explotion.prefab", "ant", gather.transform, "RadialDiskIn", PackageToPalm * 0.8f, Gold);
            disk.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Clone(YY + "Ying Yang Orb.prefab", "Orb_1/Loop/Smoke_loop_flipbook_thingy_Clean_alpha_soft", gather.transform, "GatherSmoke", PackageToPalm * 1.6f, Gold);

            // ---- Imp: Flash AoE's centre flash (warm white) + Frontal's long-cone ink spray thrown backward (the gust).
            SkillVfxLayer flash = Layer(scaled, "FX_PalmYY_Imp_C_Flash_01", SkillVfxPhase.Imp, SkillVfxTier.Core, SkillVfxFunction.Flash, 1, false);
            flash.startUnit = flash.endUnit = SkillVfxWindowUnit.Seconds;
            flash.end = 0.3f;
            flash.anchor = SkillVfxAnchor.DetachAtStart;
            Clone(YY + "Ying Yang Flash AoE.prefab", "Center", flash.transform, "CenterFlash", PackageToPalm, WarmWhite);

            SkillVfxLayer gust = Layer(scaled, "FX_PalmYY_Imp_S_Smoke_01", SkillVfxPhase.Imp, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            gust.startUnit = gust.endUnit = SkillVfxWindowUnit.Seconds;
            gust.end = 0.4f;
            gust.anchor = SkillVfxAnchor.DetachAtStart;
            Transform cone = Clone(YY + "Ying Yang Frontal.prefab", "holder_1", gust.transform, "BackwardInk", PackageToPalm, Gold, 2.5f);
            cone.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // ---- Prj: the hand (full size) + an ink-smoke trail of the 8×8 smoke flipbook laid down in world space, with
            // Frontal's flecks.
            SkillVfxLayer prjBody = Layer(bodyFit, "FX_PalmYY_Prj_C_Mesh_01", SkillVfxPhase.Prj, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, true);
            prjBody.clearOnDissipation = true;
            Body(prjBody.transform, palm, 3.0f);

            SkillVfxLayer trail = Layer(scaled, "FX_PalmYY_Prj_S_Smoke_01", SkillVfxPhase.Prj, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, true);
            Transform smoke = Clone(YY + "Ying Yang Frontal.prefab", "holder_1/Smoke_screen_cloud_sharp_fb_1_alpha_soft", trail.transform, "TrailSmoke", PackageToPalm * 1.2f, Gold);
            smoke.localPosition = new Vector3(0f, 0f, -10f);
            TrailEmitter(smoke.GetComponent<ParticleSystem>(), 1f / 9f, 0.6f);
            Transform flecks = Clone(YY + "Ying Yang Frontal Pulses.prefab", "Loop/Flecks_Shiny_Additive", trail.transform, "TrailFlecks", PackageToPalm, Gold);
            flecks.localPosition = new Vector3(0f, 0f, -10f);
            TrailEmitter(flecks.GetComponent<ParticleSystem>(), 1f / 3f, 0.5f);

            // ---- Dis: Burst Shockwave at the hand + the hand fading by the package's erosion.
            SkillVfxLayer dis = Layer(scaled, "FX_PalmYY_Dis_S_Smoke_01", SkillVfxPhase.Dis, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            dis.end = 1.25f;
            dis.anchor = SkillVfxAnchor.DetachAtStart;
            Clone(YY + "Ying Yang Burst Shockwave.prefab", "Burst", dis.transform, "Burst", PackageToPalm, Gold, 1.5f);

            SkillVfxLayer disBody = Layer(bodyFit, "FX_PalmYY_Dis_C_Mesh_01", SkillVfxPhase.Dis, SkillVfxTier.Core, SkillVfxFunction.Mesh, 1, false);
            disBody.end = 1.25f;
            Transform dyingHand = Body(disBody.transform, palm, 0.5f);
            foreach (ParticleSystem ps in dyingHand.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.CustomDataModule custom = ps.customData;
                if (custom.enabled)
                    custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f)));
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            BuildBuf();
            AssetDatabase.SaveAssets();
            Debug.Log("[PalmYingYangProto] built " + PrefabPath + " and " + BufPrefabPath);
        }

        // ---- Buf: Buff's burst (ink orb bursting, smoke rising) on the caster's chest, gold where it glows.
        static void BuildBuf()
        {
            var root = new GameObject("VFX_Palm_YY_Buf");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = "palm_buf";
            so.ApplyModifiedPropertiesWithoutUndo();
            SkillVfxLayer burst = Layer(root.transform, "FX_PalmYY_Buf_S_Smoke_01", SkillVfxPhase.Buf, SkillVfxTier.Secondary, SkillVfxFunction.Smoke, 2, false);
            burst.startUnit = burst.endUnit = SkillVfxWindowUnit.Seconds;
            burst.end = 1.0f;
            burst.transform.localPosition = new Vector3(0f, 1.5f, 3f);
            Clone(YY + "Ying Yang Buff.prefab", "Burst", burst.transform, "BuffBurst", 1f, Gold);
            PrefabUtility.SaveAsPrefabAsset(root, BufPrefabPath);
            Object.DestroyImmediate(root);
        }

        // The palm mesh drawn as one mesh particle per package material: Orb's flowing inner glow and Shield's Fresnel
        // wall, both tinted gold through the start colour.
        static Transform Body(Transform parent, Mesh palm, float lifetime)
        {
            var holder = new GameObject("Hand").transform;
            holder.SetParent(parent, false);
            Transform glow = Clone(YY + "Ying Yang Orb.prefab", "Orb_1/Loop/Mana_Gather_in_aura_quad_add_soft_desat", holder, "InnerGlow", 1f, Gold);
            Transform wall = Clone(YY + "Ying Yang Shield.prefab", null, holder, "FresnelWall", 1f, Gold, 1f, "yy_1_Wall_Frenel");
            foreach (Transform t in new[] { glow, wall })
            {
                if (t == null)
                    continue;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                var ps = t.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.startLifetime = lifetime;
                main.startDelay = 0f;
                main.startSize = 1f;
                main.startRotation = 0f;
                main.startSpeed = 0f;
                main.duration = lifetime;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                // The source systems are faint (the glow starts at alpha 0.04): the hand must read as solid light.
                main.startColor = new Color(Gold.r, Gold.g, Gold.b, t == glow ? 1f : 0.85f);
                ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
                fade.enabled = false;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.enabled = false;
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = false;
                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = false;
                var renderer = t.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = palm;
                renderer.alignment = ParticleSystemRenderSpace.Local;
            }
            return holder;
        }

        static void TrailEmitter(ParticleSystem ps, float perUnit, float lifetimeScale)
        {
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.duration = 3f;
            main.startDelay = 0f;
            main.startLifetimeMultiplier *= lifetimeScale;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = perUnit;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        // Copies one effect (a child of a package prefab, with its sub-systems) under parent, scaled through the
        // hierarchy; re-timed by speed (simulation speed); light-coloured additive systems take the given tint.
        // findByName picks the first child whose name contains it when childPath is null.
        static Transform Clone(string prefabPath, string childPath, Transform parent, string name, float scale, Color tint, float speed = 1f, string findByName = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[PalmYingYangProto] missing " + prefabPath);
                return null;
            }
            Transform source = prefab.transform;
            if (childPath != null)
                source = prefab.transform.Find(childPath);
            else if (findByName != null)
            {
                foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains(findByName))
                    {
                        source = t;
                        break;
                    }
                }
            }
            if (source == null)
            {
                Debug.LogWarning("[PalmYingYangProto] missing " + childPath + " in " + prefabPath);
                return null;
            }

            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = name;
            copy.SetActive(true);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localScale = source.localScale * scale;
            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(mb);
            foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpeed *= speed;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Material m = renderer.sharedMaterial;
                bool additive = m != null && m.HasProperty("_DstBlend") && Mathf.Approximately(m.GetFloat("_DstBlend"), 1f);
                Color start = main.startColor.color;
                bool light = start.r + start.g + start.b > 1.5f;
                if (additive || light)
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, start.a));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return copy.transform;
        }

        static Mesh PalmUvMesh()
        {
            Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(PalmMeshBaker.OutputPath);
            var mesh = Object.Instantiate(source);
            mesh.name = "Mesh_FX_Palm_UV";
            Vector3[] v = mesh.vertices;
            Bounds b = source.bounds;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
                uv[i] = new Vector2(Mathf.InverseLerp(b.min.x, b.max.x, v[i].x), Mathf.InverseLerp(b.min.y, b.max.y, v[i].y));
            mesh.uv = uv;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(PalmUvPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, PalmUvPath);
            return mesh;
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
    }
}
