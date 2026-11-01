using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Third-party VFX pack rule (Docs/vfx/README.md section 7, user 2026-10-28): the pack's source folder stays local
    // and git-ignored; only what our effect prefabs use is copied into the project, and every copy is listed in
    // Docs/vfx/asset-sources.md. This tool does that for a set of effect prefabs:
    //   1. unpacks nested pack prefabs inside them (their objects become ours),
    //   2. copies every asset they still use from the pack folder into Assets/VFX/Shared/Piloto/<type>/
    //      (a source already copied before is reused, recorded in sources.json),
    //   3. rewrites the GUIDs in our prefabs and in the copies so nothing points into the pack folder,
    //   4. regenerates the source table and reports anything left pointing into the pack.
    public static class VfxPackVendor
    {
        public const string PackRoot = "Assets/Piloto Studio/";
        public const string PackName = "Piloto Studio「Realistic Ying Yang VFX」(Unity Asset Store)";
        public const string CopyRoot = "Assets/VFX/Shared/Piloto";
        public const string RegistryPath = CopyRoot + "/sources.json";
        public const string SourceTablePath = "Docs/vfx/asset-sources.md";

        [System.Serializable]
        class Entry
        {
            public string copy;
            public string source;
            public string sourceGuid;
        }

        [System.Serializable]
        class Registry
        {
            public List<Entry> entries = new List<Entry>();
        }

        static readonly string[] TextExtensions = { ".mat", ".prefab", ".asset", ".shadergraph", ".shadersubgraph", ".controller", ".anim" };

        // The effect prefabs that hold content today. Add each new effect prefab here (or pass it to Vendor).
        public static readonly string[] EffectPrefabs =
        {
            "Assets/VFX/如来神掌/Release/VFX_Palm_Release.prefab",
            "Assets/VFX/如来神掌/Release/VFX_Palm_Release_Anti.prefab",
            "Assets/VFX/巨大化/Release/VFX_Giant_Release.prefab",
            "Assets/VFX/转向反转/Release/VFX_Reverse_Release.prefab",
            "Assets/VFX/转向反转/Hit/VFX_Reverse_Hit.prefab",
            "Assets/VFX/禅定缓流/Release/VFX_Slow_Release.prefab",
            "Assets/VFX/一念光明/Release/VFX_Curtain_Release.prefab",
            "Assets/VFX/一念光明/Hit/VFX_Curtain_Hit.prefab",
            "Assets/VFX/神足通/Release/VFX_Accel_Release.prefab",
            "Assets/VFX/通用前摇/Cast/VFX_Common_Cast.prefab",
            "Assets/VFX/通用前摇/Cast/VFX_Common_Cast_Anti.prefab",
        };

        [MenuItem("Tools/BuddahGo/VFX/Copy Used Pack Assets Into Project")]
        public static void VendorMenu() { Debug.Log(Vendor(EffectPrefabs)); }

        public static string Vendor(string[] prefabPaths)
        {
            var log = new StringBuilder();
            string[] prefabs = prefabPaths.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null).ToArray();

            // 1. Unpack nested prefab instances (pack prefabs dragged into a slot).
            foreach (string path in prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                var nested = new List<GameObject>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.gameObject != root && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                        nested.Add(t.gameObject);
                foreach (GameObject go in nested)
                {
                    log.AppendLine("unpacked " + go.name + " in " + Path.GetFileName(path));
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                if (nested.Count > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();

            // 2. Copy what they use from the pack.
            Registry registry = LoadRegistry();
            var used = new SortedSet<string>();
            foreach (string path in prefabs)
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                    if (dep.StartsWith(PackRoot))
                        used.Add(dep);

            var scripts = used.Where(d => d.EndsWith(".cs")).ToList();
            foreach (string s in scripts)
                used.Remove(s);

            var remap = new Dictionary<string, string>();   // pack GUID → copy GUID
            var copied = new List<string>();
            foreach (string source in used)
            {
                string sourceGuid = AssetDatabase.AssetPathToGUID(source);
                Entry entry = registry.entries.FirstOrDefault(e => e.sourceGuid == sourceGuid);
                if (entry == null || AssetDatabase.LoadMainAssetAtPath(entry.copy) == null)
                {
                    string copy = FreeCopyPath(source);
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    AssetDatabase.Refresh();
                    if (!AssetDatabase.CopyAsset(source, copy))
                    {
                        log.AppendLine("COPY FAILED " + source);
                        continue;
                    }
                    registry.entries.RemoveAll(e => e.sourceGuid == sourceGuid);
                    entry = new Entry { copy = copy, source = source, sourceGuid = sourceGuid };
                    registry.entries.Add(entry);
                    copied.Add(copy);
                }
                remap[sourceGuid] = AssetDatabase.AssetPathToGUID(entry.copy);
            }
            log.AppendLine("pack assets used: " + used.Count + ", newly copied: " + copied.Count);

            // 3. Point our prefabs and every copy at the copies.
            var rewrite = new List<string>(prefabs);
            rewrite.AddRange(registry.entries.Select(e => e.copy));
            int rewritten = 0;
            foreach (string path in rewrite.Distinct())
                if (RewriteGuids(path, remap))
                    rewritten++;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            log.AppendLine("files repointed: " + rewritten);

            SaveRegistry(registry);
            WriteSourceTable(registry, prefabs);

            // 4. Check.
            var left = new SortedSet<string>();
            foreach (string path in prefabs)
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                    if (dep.StartsWith(PackRoot))
                        left.Add(Path.GetFileName(path) + " -> " + dep);
            log.AppendLine(left.Count == 0 ? "OK: no effect prefab depends on " + PackRoot : "STILL IN PACK:\n  " + string.Join("\n  ", left.ToArray()));
            foreach (string s in scripts)
                log.AppendLine("SCRIPT from the pack still referenced (remove the component): " + s);
            return log.ToString();
        }

        static string FreeCopyPath(string source)
        {
            string ext = Path.GetExtension(source).ToLowerInvariant();
            string folder;
            switch (ext)
            {
                case ".mat": folder = "Materials"; break;
                case ".png": case ".tga": case ".jpg": case ".jpeg": case ".psd": case ".exr": case ".tif": folder = "Textures"; break;
                case ".fbx": case ".obj": case ".asset": folder = "Meshes"; break;
                case ".shadergraph": case ".shadersubgraph": case ".shader": case ".hlsl": folder = "Shaders"; break;
                default: folder = "Other"; break;
            }
            string name = Path.GetFileNameWithoutExtension(source);
            string candidate = CopyRoot + "/" + folder + "/" + name + Path.GetExtension(source);
            for (int i = 2; AssetDatabase.LoadMainAssetAtPath(candidate) != null || File.Exists(candidate); i++)
                candidate = CopyRoot + "/" + folder + "/" + name + "_" + i + Path.GetExtension(source);
            return candidate;
        }

        static bool RewriteGuids(string path, Dictionary<string, string> remap)
        {
            if (!TextExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) || !File.Exists(path))
                return false;
            string text = File.ReadAllText(path);
            if (!text.StartsWith("%YAML") && !text.TrimStart().StartsWith("{"))
                return false;   // binary-serialized asset: nothing to rewrite
            string result = Regex.Replace(text, "[0-9a-f]{32}", m => remap.TryGetValue(m.Value, out string to) ? to : m.Value);
            if (result == text)
                return false;
            File.WriteAllText(path, result);
            return true;
        }

        static Registry LoadRegistry()
        {
            if (!File.Exists(RegistryPath))
                return new Registry();
            return JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath)) ?? new Registry();
        }

        static void SaveRegistry(Registry registry)
        {
            registry.entries.Sort((a, b) => string.CompareOrdinal(a.copy, b.copy));
            Directory.CreateDirectory(CopyRoot);
            File.WriteAllText(RegistryPath, JsonUtility.ToJson(registry, true));
            AssetDatabase.ImportAsset(RegistryPath);
        }

        static void WriteSourceTable(Registry registry, string[] prefabs)
        {
            var users = new Dictionary<string, List<string>>();
            foreach (string prefab in prefabs)
                foreach (string dep in AssetDatabase.GetDependencies(prefab, true))
                {
                    if (!users.TryGetValue(dep, out List<string> list))
                        users[dep] = list = new List<string>();
                    list.Add(Path.GetFileNameWithoutExtension(prefab));
                }

            var md = new StringBuilder();
            md.AppendLine("# 第三方特效资产来源表");
            md.AppendLine();
            md.AppendLine("由 `VfxPackVendor`（菜单 `Tools/BuddahGo/VFX/Copy Used Pack Assets Into Project`）生成，不要手改。规则见 [README.md 第 7 节](README.md)：第三方包的源目录只留在本地、不进仓库；特效用到的资产拷进 `" + CopyRoot + "/`，每一份副本都列在这里。");
            md.AppendLine();
            md.AppendLine("来源包：" + PackName + "，按 Asset Store 标准 EULA（Single Entity）在本项目内使用，不得单独再分发。");
            md.AppendLine();
            md.AppendLine("| 副本 | 包内原路径 | 用于 |");
            md.AppendLine("|---|---|---|");
            foreach (Entry e in registry.entries)
            {
                string use = users.TryGetValue(e.copy, out List<string> list) ? string.Join("、", list.Distinct().ToArray()) : "（当前没有特效在用）";
                md.AppendLine("| `" + e.copy.Substring(CopyRoot.Length + 1) + "` | `" + e.source.Substring(PackRoot.Length) + "` | " + use + " |");
            }
            File.WriteAllText(SourceTablePath, md.ToString());
        }
    }
}
