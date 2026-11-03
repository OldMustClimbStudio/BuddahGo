// Builds the RaceMap zen landscape from the Blender export (Assets/Sources/RaceMapZen):
//   Models/RaceMapZen_Landscape.fbx  one object per unique mesh ("ZX_<mesh>") at the origin
//   Data/RaceMapZen_Layout.json      every placement as a Unity world matrix, its group and its Blender material names
// Meshes that already exist as game-ready menu-environment library prefabs are placed from there ("lib:").
// Blender material names are mapped to the menu-environment URP materials; the few zen-only materials are created
// under Materials/. Rerunning rebuilds the RaceMapZen_Landscape root in the open scene and the prefabs.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.RaceMapZen.Editor
{
    public static class RaceMapZenImporter
    {
        const string Root = "Assets/Sources/RaceMapZen";
        const string LandscapeFbx = Root + "/Models/RaceMapZen_Landscape.fbx";
        const string LayoutJson = Root + "/Data/RaceMapZen_Layout.json";
        const string PrefabDir = Root + "/Prefabs";
        const string MatDir = Root + "/Materials";
        const string MenuMatDir = "Assets/Sources/MenuEnvironment/Materials";
        const string LibDir = "Assets/Sources/MenuEnvironment/Prefabs/Library";
        public const string SceneRootName = "RaceMapZen_Landscape";

        [System.Serializable] class Layout { public int version; public List<Instance> instances; }
        [System.Serializable] class Instance { public string name; public string group; public string src; public float[] m; public string[] mats; }

        // Blender material name -> project material (menu library name, or a RaceMapZen material created below)
        static readonly Dictionary<string, string> Explicit = new Dictionary<string, string>
        {
            { "Zen_Trk_PlinthGranite", "M_Arch_GranitePaving" },
            { "Zen_Trk_MossIsland", "M_Ground_RiverPebbles" },   // round 17: moss islands became grey stone
            { "Zen_Trk_M_Rock_MainStone_A", "M_Rock_Boulder_A" },
            { "M_Rock_MainStone_A", "M_Rock_Boulder_A" },
            { "Zen_R_ToriiVermilion", "M_Arch_WoodVermilion" },
            { "Zen_R_ToriiBlack", "M_Arch_LacquerBlack" },
            { "Zen_R_KidanStone", "M_Arch_TerraceStone_Scan" },
            { "Zen_R_Terrace", "M_Arch_TerraceStone_Scan" },
            { "Zen_R_DripGravel", "M_Ground_RiverPebbles" },
            { "Zen_R_PondWater", "M_Water_Pond" },
            { "Zen_R_PondShore_Moss", "M_Ground_MossCushion" },
            { "Zen_R_PondShore_Pebble", "M_Ground_RiverPebbles" },
            { "Zen_R_PondShore_PebbleDark", "M_Ground_SabiGravel" },
            { "M_Arch_WoodDark", "M_Arch_WoodGreyWeathered" },
            { "M_Arch_LatticeWall_C", "M_Arch_LatticeWall_B" },
            { "Zen_R_Mortar", "RaceMapZen_Mortar" },
            { "Zen_Trk_Contact", "RaceMapZen_ContactShadow" },
            { "Zen_Trk_House_Shadow", "RaceMapZen_HouseShadow" },   // window / door openings of the town houses
        };

        [MenuItem("BuddahGo/RaceMap Zen/Import Landscape")]
        public static void ImportLandscape()
        {
            ConfigureModel(LandscapeFbx);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutJson));
            EnsureZenMaterials();
            var fbxChildren = LoadFbxChildren(LandscapeFbx);
            var prefabs = new Dictionary<string, GameObject>();
            var scene = EditorSceneManager.GetActiveScene();
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SceneRootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(SceneRootName);
            Undo.RegisterCreatedObjectUndo(root, "RaceMap zen landscape");
            var groups = new Dictionary<string, Transform>();
            int placed = 0, missing = 0;
            var unmapped = new HashSet<string>();
            foreach (var it in layout.instances)
            {
                GameObject prefab;
                if (!prefabs.TryGetValue(it.src, out prefab))
                {
                    prefab = it.src.StartsWith("lib:")
                        ? AssetDatabase.LoadAssetAtPath<GameObject>(LibDir + "/" + it.src.Substring(4) + ".prefab")
                        : BuildMeshPrefab(it.src.Substring(4), fbxChildren, it.mats, unmapped);
                    prefabs[it.src] = prefab;
                }
                if (prefab == null) { missing++; continue; }
                if (!groups.TryGetValue(it.group, out var g))
                {
                    g = new GameObject(it.group).transform; g.SetParent(root.transform, false); groups[it.group] = g;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
                go.name = it.name;
                var m = new Matrix4x4();
                for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] = it.m[r * 4 + c];
                go.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                go.transform.localScale = m.lossyScale;
                placed++;
            }
            // occluder fade: the spans over the track, all torii and the low wall bays at the garden openings
            var fade = root.AddComponent<BuddahGo.Rendering.OccluderFade>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null || !groups.ContainsValue(t.parent)) continue;   // instances only
                string n = t.name;
                bool span = n.StartsWith("R_Torii") || n.StartsWith("R_Gate") || n.StartsWith("R_Watari_Span");
                bool lowWall = (n.StartsWith("R_RectWall") || n.StartsWith("R_WallPier")) && t.localScale.y < 5f;   // low bays: height x0.45
                if (span || lowWall) fade.occluders.Add(t.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[RaceMapZen] occluder fade on {fade.occluders.Count} objects");
            BuildGround();   // the ground lives under the same root, which was just rebuilt
            Debug.Log($"[RaceMapZen] placed {placed} instances in {groups.Count} groups, {prefabs.Count} prefabs, missing {missing}" +
                      (unmapped.Count > 0 ? ", unmapped materials: " + string.Join(", ", unmapped) : ""));
        }

        // ------------------------------------------------------------------ ground
        const string FieldTex = Root + "/Textures/RaceMapZen_GroundField.exr";
        const string FieldJson = Root + "/Data/RaceMapZen_GroundField.json";
        [System.Serializable] class FieldInfo { public float[] origin_m; public float res_m; public int[] size; }

        [MenuItem("BuddahGo/RaceMap Zen/Build Ground")]
        public static void BuildGround()
        {
            ConfigureTexture(FieldTex, false, true);
            ConfigureTexture(Root + "/Textures/T_Gravel_Shirakawa_BaseColor.png", true, false);
            ConfigureTexture(Root + "/Textures/T_Gravel_Shirakawa_Roughness.png", false, false);
            ConfigureTexture(Root + "/Textures/T_Gravel_Shirakawa_Height.png", false, false);
            var info = JsonUtility.FromJson<FieldInfo>(File.ReadAllText(FieldJson));
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");
            var matPath = MatDir + "/RaceMapZen_Ground.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("BuddahGo/ZenGround")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = Shader.Find("BuddahGo/ZenGround");
            mat.SetTexture("_Field", AssetDatabase.LoadAssetAtPath<Texture2D>(FieldTex));
            mat.SetTexture("_SandMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_Gravel_Shirakawa_BaseColor.png"));
            mat.SetTexture("_SandRough", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_Gravel_Shirakawa_Roughness.png"));
            mat.SetTexture("_SandHeight", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_Gravel_Shirakawa_Height.png"));
            mat.SetVector("_FieldParams", new Vector4(info.origin_m[0], info.origin_m[1], 1f / (info.size[0] * info.res_m), 1f / (info.size[1] * info.res_m)));
            EditorUtility.SetDirty(mat);

            // flat grid, 4.8 km square around the origin (the old 沙地 extent); the shader works in world space
            var meshPath = Root + "/Models/RaceMapZen_Ground.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = new Mesh { name = "RaceMapZen_Ground" }; AssetDatabase.CreateAsset(mesh, meshPath); }
            const int n = 48; const float half = 2400f;
            var v = new Vector3[(n + 1) * (n + 1)]; var nr = new Vector3[v.Length]; var tri = new int[n * n * 6];
            for (int z = 0, k = 0; z <= n; z++) for (int x = 0; x <= n; x++, k++) { v[k] = new Vector3(-half + 2 * half * x / n, 0f, -half + 2 * half * z / n); nr[k] = Vector3.up; }
            for (int z = 0, t = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x; tri[t++] = a; tri[t++] = a + n + 1; tri[t++] = a + 1; tri[t++] = a + 1; tri[t++] = a + n + 1; tri[t++] = a + n + 2;
            }
            mesh.Clear(); mesh.vertices = v; mesh.normals = nr; mesh.triangles = tri; mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SceneRootName);
            if (root == null) { root = new GameObject(SceneRootName); Undo.RegisterCreatedObjectUndo(root, "zen root"); }
            var old = root.transform.Find("Ground"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject("Ground"); Undo.RegisterCreatedObjectUndo(go, "zen ground");
            go.transform.SetParent(root.transform, false); go.transform.position = new Vector3(0f, 0.02f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
            // the old sand plane keeps its RespawnFloor collider; only its picture is replaced
            var sand = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "沙地");
            if (sand != null && sand.TryGetComponent<MeshRenderer>(out var sr)) { Undo.RecordObject(sr, "hide old sand"); sr.enabled = false; }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RaceMapZen] ground built");
        }

        static void ConfigureTexture(string path, bool srgb, bool isField)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogWarning("[RaceMapZen] missing texture " + path); return; }
            var wrap = isField ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            bool changed = ti.sRGBTexture != srgb || ti.wrapMode != wrap || ti.maxTextureSize != 2048
                           || isField && (ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled || ti.filterMode != FilterMode.Bilinear);
            if (!changed) return;   // the EXR and the gravel maps are large: reimport only when a setting differs
            ti.sRGBTexture = srgb; ti.wrapMode = wrap; ti.maxTextureSize = 2048;
            if (isField) { ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.filterMode = FilterMode.Bilinear; }   // uncompressed EXR -> RGBAHalf
            ti.SaveAndReimport();
        }

        public static void ConfigureModel(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = imp.materialImportMode != ModelImporterMaterialImportMode.ImportStandard
                           || imp.materialLocation != ModelImporterMaterialLocation.InPrefab
                           || imp.importAnimation || imp.animationType != ModelImporterAnimationType.None
                           || imp.importCameras || imp.importLights
                           || imp.meshCompression != ModelImporterMeshCompression.Off
                           || imp.importNormals != ModelImporterNormals.Import
                           || imp.indexFormat != ModelImporterIndexFormat.Auto;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;   // only to read Blender material names per sub-mesh
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
            imp.importCameras = false; imp.importLights = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.indexFormat = ModelImporterIndexFormat.Auto;
            if (changed) imp.SaveAndReimport();
        }

        static Dictionary<string, Transform> LoadFbxChildren(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var d = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) d[t.name] = t;
            return d;
        }

        static GameObject BuildMeshPrefab(string objName, Dictionary<string, Transform> fbx, string[] blenderMats, HashSet<string> unmapped)
        {
            if (!fbx.TryGetValue(objName, out var src)) { Debug.LogWarning("[RaceMapZen] mesh not in FBX: " + objName); return null; }
            var mf = src.GetComponent<MeshFilter>(); var mr = src.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return null;
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder(Root, "Prefabs");
            var tmp = new GameObject(objName.Substring(3));
            var child = new GameObject("Mesh");
            child.transform.SetParent(tmp.transform, false);
            child.transform.localRotation = src.localRotation; child.transform.localScale = src.localScale;   // FBX axis/unit node
            child.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = child.AddComponent<MeshRenderer>();
            // sub-mesh order follows the imported (Blender) materials; map each by its name
            var mats = mr.sharedMaterials.Select(m => Map(m != null ? m.name : "", unmapped)).ToArray();
            r.sharedMaterials = mats;
            r.shadowCastingMode = objName.Contains("Contact") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            var path = PrefabDir + "/" + tmp.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            Object.DestroyImmediate(tmp);
            return prefab;
        }

        static Material Map(string blenderName, HashSet<string> unmapped)
        {
            string n = blenderName;
            int dot = n.IndexOf('.'); if (dot > 0 && n.Length - dot == 4 && char.IsDigit(n[dot + 1])) n = n.Substring(0, dot);
            if (!Explicit.TryGetValue(n, out var target))
            {
                if (n.StartsWith("Zen_Trk_Wall_")) target = "M_Arch_" + n.Substring("Zen_Trk_Wall_".Length);
                else if (n.StartsWith("Zen_Trk_M_")) target = "M_" + n.Substring("Zen_Trk_M_".Length);
                else target = n;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(MenuMatDir + "/" + target + ".mat")
                    ?? AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + target + ".mat");
            if (m == null) { unmapped.Add(blenderName); m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/RaceMapZen_Fallback.mat"); }
            return m;
        }

        static void EnsureZenMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name, Color c, float smooth)
            {
                var path = MatDir + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
                EditorUtility.SetDirty(m);
                return m;
            }
            Make("RaceMapZen_Mortar", new Color(0.11f, 0.105f, 0.095f), 0.05f);
            Make("RaceMapZen_Fallback", new Color(0.5f, 0.5f, 0.5f), 0.2f);
            Make("RaceMapZen_HouseShadow", new Color(0.03f, 0.026f, 0.022f), 0.05f);
            // soft dark disc under props: transparent Lit with a radial falloff texture
            var tex = RadialTexture(MatDir + "/RaceMapZen_ContactShadow.png");
            var cm = Make("RaceMapZen_ContactShadow", new Color(0.10f, 0.095f, 0.08f, 0.55f), 0f);
            cm.SetTexture("_BaseMap", tex);
            cm.SetFloat("_Surface", 1f); cm.SetFloat("_Blend", 0f); cm.SetFloat("_ZWrite", 0f);
            cm.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); cm.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            cm.SetOverrideTag("RenderType", "Transparent"); cm.renderQueue = (int)RenderQueue.Transparent;
            cm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); cm.SetShaderPassEnabled("ShadowCaster", false);
            cm.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(cm);
            AssetDatabase.SaveAssets();
        }

        static Texture2D RadialTexture(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - Mathf.InverseLerp(0.35f, 1f, r)); a *= a;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
