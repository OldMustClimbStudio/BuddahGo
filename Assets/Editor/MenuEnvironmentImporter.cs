#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rebuilds the menu environment (the cliff-top shrine) from the Blender export in
/// Assets/Sources/MenuEnvironment. The Blender pipeline (text block "unity_export.py" in
/// 游戏外场景搭建.blend) writes the FBX files, baked URP textures and Data/MenuEnvironmentLayout.json;
/// this tool turns them into materials, prefabs and the MenuEnvironmentSetting scene.
/// </summary>
public static class MenuEnvironmentImporter
{
    private const string Root = "Assets/Sources/MenuEnvironment";
    private const string LayoutPath = Root + "/Data/MenuEnvironmentLayout.json";
    private const string MaterialFolder = Root + "/Materials";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string ScenePath = "Assets/Scenes/MenuEnvironmentSetting.unity";
    private const string SceneRootName = "MenuEnvironment";
    private const int MaxTextureSize = 2048;
    // Blender camera whose position, rotation and lens the Main Camera copies.
    private const string MainCameraMarker = "CAM_EmaCheck";
    // Hand-picked camera pose saved from the Scene view: the skill-select pose (技能选择).
    private const string CameraPosePath = Root + "/Data/MainCameraPose.json";
    private const string SkillSelectPoseName = "CamPose_SkillSelect_技能选择";
    // Room view (房间): the closed main-hall door seen from the top of the terrace, where the Start
    // button's camera move ends and rooms are created or joined. A pose saved from the Scene view
    // (RoomPosePath) wins over the default framing below.
    private const string RoomPosePath = Root + "/Data/RoomCameraPose.json";
    private const string RoomPoseName = "CamPose_Room_房间";
    // Close on the bell rope (left third) with the closed doors behind it and the veranda opening on the right.
    private static readonly Vector3 RoomCameraPosition = new Vector3(4.7f, 3.15f, -8.6f);
    private static readonly Vector3 RoomLookAt = new Vector3(3.55f, 3.4f, -11.9f);
    private const float RoomFieldOfView = 50f;
    // Hinged hall doors (modelled closed, pivot on the hinge) and the view inside the hall (房间内)
    // that "创建房间" walks into through the doorway.
    private static readonly string[] HallDoors = { "Arch_Honden_Door_L", "Arch_Honden_Door_R" };
    private const string RoomInsidePoseName = "CamPose_RoomInside_房间内";
    // Lobby view: from the front west corner, above head height, looking diagonally across the low sand
    // table to the row of players and the opening to the balcony (the player spots are laid out for it).
    private static readonly Vector3 RoomInsidePosition = new Vector3(6.2f, 4.5f, -12.5f);
    private static readonly Vector3 RoomInsideLookAt = new Vector3(3.4f, 2.5f, -15.8f);
    private const float RoomInsideFieldOfView = 62f;
    private static readonly Vector3 RoomInteriorFillPosition = new Vector3(4.0f, 4.7f, -14.2f);

    [Serializable] private class CameraPose { public Vector3 position; public Vector3 euler; public float fieldOfView; }

    private static CameraPose SceneViewPose()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null) { Debug.LogWarning("[MenuEnvironment] No Scene view to copy from."); return null; }
        return new CameraPose
        {
            position = sv.camera.transform.position,
            euler = sv.camera.transform.eulerAngles,
            fieldOfView = sv.cameraSettings.fieldOfView,
        };
    }

    private static void SavePose(string path, CameraPose pose)
    {
        File.WriteAllText(path, JsonUtility.ToJson(pose, true));
        AssetDatabase.ImportAsset(path);
    }

    [MenuItem("Tools/Menu Environment/Set Room Pose From Scene View")]
    public static void SetRoomPoseFromSceneView()
    {
        CameraPose pose = SceneViewPose();
        if (pose == null) return;
        SavePose(RoomPosePath, pose);
        GameObject marker = GameObject.Find(RoomPoseName);
        if (marker != null)
        {
            Undo.RecordObject(marker.transform, "Set Room Pose From Scene View");
            marker.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
            EditorSceneManager.MarkSceneDirty(marker.scene);
        }
        Debug.Log($"[MenuEnvironment] Room pose saved: {pose.position} {pose.euler}");
    }

    [MenuItem("Tools/Menu Environment/Set Main Camera From Scene View")]
    public static void SetMainCameraFromSceneView()
    {
        CameraPose pose = SceneViewPose();
        if (pose == null) return;
        SavePose(CameraPosePath, pose);
        GameObject marker = GameObject.Find(SkillSelectPoseName);
        if (marker != null)
        {
            Undo.RecordObject(marker.transform, "Set Main Camera From Scene View");
            marker.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
        }

        Camera main = Camera.main;
        if (main != null)
        {
            Undo.RecordObject(main.transform, "Set Main Camera From Scene View");
            Undo.RecordObject(main, "Set Main Camera From Scene View");
            main.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
            main.fieldOfView = pose.fieldOfView;
            EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        }
        Debug.Log($"[MenuEnvironment] Main Camera pose saved: {pose.position} {pose.euler} fov {pose.fieldOfView}");
    }

    [Serializable] private class Layout { public int version; public string source; public AssetEntry[] assets; public MaterialEntry[] materials; public ObjectEntry[] objects; public Point[] cameraPath; }
    [Serializable] private class Point { public float x, y, z; }
    [Serializable] private class AssetEntry { public string name, model, folder; public string[] materials; }
    [Serializable]
    private class MaterialEntry
    {
        public string name, mode, baseMap, normalMap, maskMap;
        public float tile, roughness, metallic, emissionStrength, normalStrength, transmission;
        public float[] baseColor, emission;
        public bool baseLinked, alphaClip;
    }
    [Serializable]
    private class ObjectEntry
    {
        public string name, type, asset, parent, light;
        public string[] path;
        public float[] pos, fwd, up, scale, color;
        public float energy, size, spot, lens;
    }

    [MenuItem("Tools/Menu Environment/Import Assets And Build Scene")]
    public static void ImportAndBuild()
    {
        ImportAssets();
        BuildScene();
    }

    [MenuItem("Tools/Menu Environment/1 Import Assets (Textures, Materials, Prefabs)")]
    public static void ImportAssets()
    {
        Layout layout = LoadLayout();
        ConfigureTextures();
        Dictionary<string, Material> materials = CreateMaterials(layout.materials);
        ConfigureModels(layout.assets, materials);
        CreatePrefabs(layout.assets);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MenuEnvironment] Imported {layout.assets.Length} assets and {materials.Count} materials.");
    }

    [MenuItem("Tools/Menu Environment/2 Build Scene")]
    public static void BuildScene()
    {
        Layout layout = LoadLayout();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            // The scene was created from the default template; its camera and light are replaced.
            if (go.name == SceneRootName || go.name == "Main Camera" || go.name == "Directional Light" || go.name == "Global Volume"
                || go.name == "TEMP_MenuCanvas" || go.name == "EventSystem")
                UnityEngine.Object.DestroyImmediate(go);
        }

        var root = new GameObject(SceneRootName).transform;
        var groups = new Dictionary<string, Transform>();
        var byName = new Dictionary<string, Transform>();
        Light sun = null;
        int missing = 0;

        // Empties first so meshes can be parented to them (the Buddha skin anchor).
        foreach (ObjectEntry o in layout.objects)
        {
            if (o.type != "empty") continue;
            var go = new GameObject(o.name);
            go.transform.SetParent(Group(root, groups, o.path), false);
            ApplyWorld(go.transform, o);
            byName[o.name] = go.transform;
        }

        foreach (ObjectEntry o in layout.objects)
        {
            switch (o.type)
            {
                case "mesh":
                {
                    Transform parent = !string.IsNullOrEmpty(o.parent) && byName.TryGetValue(o.parent, out Transform p) ? p : Group(root, groups, o.path);
                    GameObject go = CreateMesh(layout, o, parent);
                    if (go == null) { missing++; continue; }
                    byName[o.name] = go.transform;
                    break;
                }
                case "light":
                {
                    Light l = CreateLight(o, Group(root, groups, o.path));
                    if (l != null && l.type == LightType.Directional) sun = l;
                    break;
                }
                case "camera":
                {
                    var marker = new GameObject(o.name);
                    marker.transform.SetParent(Group(root, groups, new[] { "95_Camera", "Markers" }), false);
                    ApplyWorld(marker.transform, o);
                    break;
                }
            }
        }

        var pathRoot = Group(root, groups, new[] { "95_Camera", "CameraPath_ToriiToHonden" });
        for (int i = 0; i < layout.cameraPath.Length; i++)
        {
            var pt = new GameObject($"Point_{i:00}").transform;
            pt.SetParent(pathRoot, false);
            pt.position = new Vector3(layout.cameraPath[i].x, layout.cameraPath[i].y, layout.cameraPath[i].z);
        }

        CreateMainCamera(layout);
        ApplyLightingRig(Group(root, groups, new[] { "90_Lighting" }), sun);
        SetupEnvironment(sun);
        CreateGlobalVolume();
        MarkStatic(root.gameObject);
        SetupEmaInspector(root);
        SetupProtoCameraSwitcher();
        SetupMenuSkinView(PlaceMenuBuddhaSkin());
        SetupSkillLoadout(root);
        SetupRoomView();
        SetupLobby();
        SetupSandTableFocus();
        SetupEmaWallShortcut();
        CreateSkinHalo();
        SetupMenuClickShortcuts();
        SetupHoverHighlighter();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.Copy(LayoutPath, AppliedLayoutPath, true);
        Debug.Log($"[MenuEnvironment] Scene built with {layout.objects.Length} entries ({missing} missing prefabs).");
    }

    // The layout the scene was last built or synced from; Sync compares the new export against it.
    private const string AppliedLayoutPath = Root + "/Data/MenuEnvironmentLayout.applied.json";

    /// <summary>
    /// Brings only what changed in Blender since the last build or sync into the scene: new or changed
    /// assets are imported (materials, model settings, prefabs), new or moved objects are placed, and
    /// objects gone from the export are removed. Placed meshes are updated in place, and the Unity-side
    /// setup that depends on an attached object is re-applied (ReapplyAttachedSetup). Everything else in
    /// the scene, hand edits included, is left alone.
    /// </summary>
    [MenuItem("Tools/Menu Environment/3 Sync Changed Objects")]
    public static void SyncChanged()
    {
        if (!File.Exists(AppliedLayoutPath)) { Debug.LogWarning($"[MenuEnvironment] No {AppliedLayoutPath}; run Build Scene once first."); return; }
        Layout layout = LoadLayout();
        Layout applied = JsonUtility.FromJson<Layout>(File.ReadAllText(AppliedLayoutPath));

        var oldMaterials = applied.materials.ToDictionary(m => m.name, JsonUtility.ToJson);
        var oldAssets = applied.assets.ToDictionary(a => a.name, JsonUtility.ToJson);
        var oldObjects = applied.objects.ToDictionary(o => o.name, JsonUtility.ToJson);
        MaterialEntry[] changedMaterials = layout.materials
            .Where(m => !oldMaterials.TryGetValue(m.name, out string j) || j != JsonUtility.ToJson(m)).ToArray();
        AssetEntry[] changedAssets = layout.assets
            .Where(a => !oldAssets.TryGetValue(a.name, out string j) || j != JsonUtility.ToJson(a)
                        || a.materials.Any(n => changedMaterials.Any(m => m.name == n))).ToArray();
        ObjectEntry[] changedObjects = layout.objects
            .Where(o => !oldObjects.TryGetValue(o.name, out string j) || j != JsonUtility.ToJson(o)
                        || (o.type == "mesh" && changedAssets.Any(a => a.name == o.asset))).ToArray();
        var current = new HashSet<string>(layout.objects.Select(o => o.name));
        string[] removed = applied.objects.Select(o => o.name).Where(n => !current.Contains(n)).ToArray();

        if (changedAssets.Length > 0 || changedMaterials.Length > 0)
        {
            Dictionary<string, Material> materials = CreateMaterials(changedMaterials);
            foreach (MaterialEntry m in layout.materials)
                if (!materials.ContainsKey(m.name) && AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{m.name}.mat") is Material mat)
                    materials[m.name] = mat;
            ConfigureModels(changedAssets, materials);
            CreatePrefabs(changedAssets);
            AssetDatabase.SaveAssets();
        }

        // Work in the open scene when it is this one, so unsaved edits there are kept (and saved with the sync).
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Transform root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SceneRootName)?.transform;
        if (root == null) { Debug.LogWarning("[MenuEnvironment] Scene root missing; run Build Scene."); return; }
        var byName = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;

        var appliedObjects = applied.objects.ToDictionary(o => o.name);
        // The placeholder statue on the skin anchor was replaced by the menu Buddha of the same name.
        foreach (string n in removed)
            if (appliedObjects[n].parent != SkinAnchorName && byName.TryGetValue(n, out Transform t) && t != null)
                UnityEngine.Object.DestroyImmediate(t.gameObject);

        int missing = 0;
        // Empties first so meshes can be parented to them.
        foreach (ObjectEntry o in changedObjects.OrderBy(o => o.type == "empty" ? 0 : 1))
        {
            if (o.type == "mesh" && o.parent == SkinAnchorName) continue;
            byName.TryGetValue(o.name, out Transform existing);
            Transform parent = o.type == "camera" ? FindOrCreateGroup(root, new[] { "95_Camera", "Markers" })
                : o.type == "mesh" && !string.IsNullOrEmpty(o.parent) && byName.TryGetValue(o.parent, out Transform p) ? p
                : FindOrCreateGroup(root, o.path);
            GameObject go;
            switch (o.type)
            {
                case "empty":
                case "camera":
                    go = existing != null ? existing.gameObject : new GameObject(o.name);
                    go.transform.SetParent(parent, true);
                    ApplyWorld(go.transform, o);
                    break;
                case "mesh":
                    if (existing != null)
                    {
                        // Updated in place, so what Unity added to an attached object survives (see UpdateMesh).
                        // Unity may have moved it under its own object (the bells under the rope): it only
                        // follows Blender to a new parent when the export changed where it hangs.
                        bool sameHierarchy = appliedObjects.TryGetValue(o.name, out ObjectEntry old)
                                             && old.parent == o.parent && (old.path ?? Array.Empty<string>()).SequenceEqual(o.path ?? Array.Empty<string>());
                        var oldPose = new Pose(existing.position, existing.rotation);
                        go = UpdateMesh(layout, o, existing.gameObject, sameHierarchy ? existing.parent : parent);
                        if (go == null) { missing++; continue; }
                        if (o.name.StartsWith("绘马_") && o.name.EndsWith(HokoraSuffix)) MoveLoadoutSlot(go.transform, oldPose);
                        byName[o.name] = go.transform;
                        continue;
                    }
                    go = CreateMesh(layout, o, parent);
                    if (go == null) { missing++; continue; }
                    if (IsSkillEma(o.name)) Debug.LogWarning($"[MenuEnvironment] New skill ema {o.name}: run Build Scene to make it clickable.");
                    break;
                case "light":
                    if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                    Light l = CreateLight(o, parent);
                    if (l == null) continue;
                    go = l.gameObject;
                    break;
                default: continue;
            }
            byName[o.name] = go.transform;
            // A moved empty keeps its flags: the skin anchor and what hangs under it must stay non-static.
            if (existing == null || o.type != "empty") MarkStatic(go);
        }
        ReapplyAttachedSetup(new HashSet<string>(changedObjects.Select(o => o.name)));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.Copy(LayoutPath, AppliedLayoutPath, true);
        AssetDatabase.ImportAsset(AppliedLayoutPath);
        Debug.Log($"[MenuEnvironment] Synced {changedAssets.Length} assets, {changedMaterials.Length} materials, " +
                  $"{changedObjects.Length} objects ({missing} missing prefabs), removed {removed.Length}: " +
                  string.Join(", ", changedObjects.Select(o => o.name).Concat(removed.Select(n => "-" + n))));
    }

    private static GameObject CreateMesh(Layout layout, ObjectEntry o, Transform parent)
    {
        AssetEntry asset = Array.Find(layout.assets, a => a.name == o.asset);
        GameObject prefab = asset != null ? AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(asset)) : null;
        if (prefab == null) { Debug.LogWarning($"[MenuEnvironment] Missing prefab for {o.name} ({o.asset})"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = o.name;
        go.transform.SetParent(parent, false);
        ApplyWorld(go.transform, o);
        return go;
    }

    /// <summary>
    /// Brings a placed mesh up to date without rebuilding it: the instance follows its re-saved prefab (a
    /// changed asset swaps the prefab under it), then takes the new pose. The components and children
    /// Unity added, its parent and every reference to it are kept. Static flags follow the root's, as
    /// Build Scene sets them per object.
    /// </summary>
    private static GameObject UpdateMesh(Layout layout, ObjectEntry o, GameObject go, Transform parent)
    {
        AssetEntry asset = Array.Find(layout.assets, a => a.name == o.asset);
        GameObject prefab = asset != null ? AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(asset)) : null;
        if (prefab == null) { Debug.LogWarning($"[MenuEnvironment] Missing prefab for {o.name} ({o.asset})"); return null; }
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            UnityEngine.Object.DestroyImmediate(go);
            go = CreateMesh(layout, o, parent);
            MarkStatic(go);
            return go;
        }
        if (PrefabUtility.GetCorrespondingObjectFromSource(go) != prefab)
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, prefab, InteractionMode.AutomatedAction);
        go.name = o.name;   // the swap renames the instance after the new prefab
        go.transform.SetParent(parent, true);
        ApplyWorld(go.transform, o);
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(t.gameObject))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        return go;
    }

    private static bool IsSkillEma(string name) => name.StartsWith("绘马_") && !name.Contains("普通") && !name.EndsWith(HokoraSuffix);

    /// <summary>The loadout slot marking a hokora ema copy (made at its pose by SetupSkillLoadout) follows the copy.</summary>
    private static void MoveLoadoutSlot(Transform copy, Pose oldPose)
    {
        if (copy.parent == null) return;
        foreach (Transform slot in copy.parent)
            if (slot.name.StartsWith("LoadoutSlot_") && Vector3.Distance(slot.position, oldPose.position) < 0.001f
                && Quaternion.Angle(slot.rotation, oldPose.rotation) < 0.1f)
            {
                slot.SetPositionAndRotation(copy.position, copy.rotation);
                return;
            }
    }

    /// <summary>
    /// Re-applies the parts of Build Scene's setup that depend on the shape or pose of the attached objects
    /// Sync just updated (ADR 0005), without creating the Unity-only objects again.
    /// </summary>
    private static void ReapplyAttachedSetup(HashSet<string> changed)
    {
        // Bell rope: the bells hang under the rope group, which sits on their hook.
        if (changed.Contains(BellClusterName) && GameObject.Find(BellClusterName) is GameObject bells
            && GameObject.Find(BellRopeGroupName) is GameObject group
            && group.GetComponentInChildren<SteamMultiplayer.UI.MenuBellRope>(true) is SteamMultiplayer.UI.MenuBellRope rope)
        {
            foreach (Transform t in bells.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            Vector3 hook = bells.transform.position;
            if (bells.transform.parent != group.transform) bells.transform.SetParent(group.transform, true);
            group.transform.position = hook;
            bells.transform.position = hook;
            var (anchorLocal, length, wallPoint) = BellRopeHang(bells.transform);
            var so = new SerializedObject(rope);
            so.FindProperty("bells").objectReferenceValue = bells.transform;
            so.FindProperty("anchorLocal").vector3Value = anchorLocal;
            so.FindProperty("length").floatValue = length;
            so.FindProperty("wallPoint").vector3Value = wallPoint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Skill ema and their cords move at runtime and are picked by their colliders; the hokora copies stay hidden.
        foreach (string n in changed)
        {
            GameObject go = GameObject.Find(n) ?? FindInactive(n);
            if (go == null) continue;
            if ((n.StartsWith("绘马_") || n.StartsWith("红绳_")) && n.EndsWith(HokoraSuffix)) { go.SetActive(false); continue; }
            bool cord = n.StartsWith("红绳_") && IsSkillEma("绘马_" + n.Substring("红绳_".Length));
            if (!IsSkillEma(n) && !cord) continue;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            if (!cord) FitEmaColliders(go.transform);
        }

        // Ema wall: no static batching on it and what hangs on it (hover outline), and the click proxy fits the wall.
        if (EmaWallParts().Any(t => changed.Contains(t.name)))
        {
            ClearEmaWallBatching();
            if (GameObject.Find(EmaWallName) is GameObject wall && wall.transform.Find("EmaWallClickProxy") is Transform proxy)
                FitEmaWallProxy(proxy.GetComponent<BoxCollider>(), wall);
        }

        // Main hall (main-menu shortcut to Start): outlined, so no static batching; the click box follows the building.
        if (HondenShortcutParts.Any(changed.Contains))
        {
            Transform[] parts = HondenParts();
            foreach (Transform t in parts) ClearBatching(t);
            if (GameObject.Find(HondenClickProxyName) is GameObject hallProxy)
                FitWorldBox(hallProxy.GetComponent<BoxCollider>(), RenderBounds(parts));
        }

        // Sand table: hover outline shares its mesh, so no static batching.
        if (changed.Contains(SandTableName) && GameObject.Find(SandTableName) is GameObject sandTable) ClearBatching(sandTable.transform);

        // Skin: the menu Buddha stands on the anchor, the halo lies on the plinth's sand, the skin shot frames it.
        if ((changed.Contains(SkinAnchorName) || changed.Contains(SkinPlinthName)) && GameObject.Find(SkinAnchorName) is GameObject anchor)
        {
            GameObjectUtility.SetStaticEditorFlags(anchor, 0);
            if (anchor.transform.Find("SkinTurntable") is Transform table && table.Find(MenuSkinName) is Transform skin)
                FitMenuBuddhaSkin(anchor.transform, table, skin);
            if (anchor.transform.Find(SkinClickProxyName) is Transform skinProxy && anchor.transform.Find("SkinTurntable") is Transform turntable)
                FitWorldBox(skinProxy.GetComponent<BoxCollider>(), RenderBounds(new[] { turntable }));
            if (anchor.transform.Find("FX_SkinHalo") is Transform halo) PlaceSkinHalo(halo, anchor.transform);
            if (GameObject.Find(SkinPoseName) is GameObject marker)
            {
                SkinCameraPose(anchor.transform, out Vector3 position, out Quaternion rotation);
                marker.transform.SetPositionAndRotation(position, rotation);
            }
        }
    }

    private static GameObject FindInactive(string name) =>
        Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == name && t.gameObject.scene.IsValid())?.gameObject;

    private static Transform FindOrCreateGroup(Transform root, string[] path)
    {
        Transform t = root;
        foreach (string p in path ?? Array.Empty<string>())
        {
            Transform child = t.Find(p);
            if (child == null) { child = new GameObject(p).transform; child.SetParent(t, false); }
            t = child;
        }
        return t;
    }

    // ---------------------------------------------------------------- import

    private static Layout LoadLayout()
    {
        string json = File.ReadAllText(LayoutPath);
        return JsonUtility.FromJson<Layout>(json);
    }

    private static void ConfigureTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" });
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                string n = Path.GetFileNameWithoutExtension(path);
                bool normal = n.EndsWith("_Normal") || n.EndsWith("_nor") || n.Contains("RakeNormal");
                bool linear = normal || n.Contains("MetallicGloss") || n.Contains("Roughness") || n.Contains("_rough") || n.Contains("Height")
                              || n.Contains("_disp") || n.Contains("_AO") || n.Contains("RakeField") || n.EndsWith("_Alpha") || n.Contains("Metallic");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !linear;
                bool sky = n.StartsWith("T_Sky_");
                ti.maxTextureSize = sky ? 4096 : MaxTextureSize;
                ti.mipmapEnabled = !sky;
                ti.alphaIsTransparency = n.Contains("Leaves") || n.Contains("Fern") || n.Contains("Foam");
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    private static Dictionary<string, Material> CreateMaterials(IEnumerable<MaterialEntry> entries)
    {
        EnsureFolder(MaterialFolder);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var result = new Dictionary<string, Material>();
        foreach (MaterialEntry m in entries)
        {
            string path = $"{MaterialFolder}/{m.name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = lit;
            SetupLit(mat, m);
            EditorUtility.SetDirty(mat);
            result[m.name] = mat;
        }
        return result;
    }

    private static void SetupLit(Material mat, MaterialEntry m)
    {
        mat.shaderKeywords = Array.Empty<string>();
        Texture2D baseMap = LoadTex(m.baseMap), normalMap = LoadTex(m.normalMap), maskMap = LoadTex(m.maskMap);
        Color baseColor = new Color(m.baseColor[0], m.baseColor[1], m.baseColor[2], 1f).gamma;

        mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", baseMap != null ? Color.white : baseColor);

        // Box-projected Blender materials were exported with metric UVs; one baked tile covers `tile` metres.
        float scale = m.tile > 0.0001f ? 1f / m.tile : 1f;
        mat.SetTextureScale("_BaseMap", new Vector2(scale, scale));

        mat.SetTexture("_BumpMap", normalMap);
        mat.SetFloat("_BumpScale", Mathf.Clamp(m.normalStrength, 0.2f, 2f));
        if (normalMap != null) mat.EnableKeyword("_NORMALMAP");

        if (maskMap != null)
        {
            mat.SetTexture("_MetallicGlossMap", maskMap);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - m.roughness));
        }
        mat.SetFloat("_Metallic", Mathf.Clamp01(m.metallic));

        bool leafy = m.alphaClip;
        mat.SetFloat("_AlphaClip", leafy ? 1f : 0f);
        mat.SetFloat("_Cutoff", 0.4f);
        mat.SetFloat("_Cull", leafy ? 0f : 2f);
        mat.renderQueue = leafy ? (int)RenderQueue.AlphaTest : -1;
        if (leafy) mat.EnableKeyword("_ALPHATEST_ON");

        bool emissive = m.emissionStrength > 0f && (m.emission[0] + m.emission[1] + m.emission[2]) > 0f;
        if (emissive)
        {
            var e = new Color(m.emission[0], m.emission[1], m.emission[2]).gamma * Mathf.Min(m.emissionStrength, 12f) * 0.4f;
            mat.SetColor("_EmissionColor", e);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        // Opaque defaults
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.SetOverrideTag("RenderType", leafy ? "TransparentCutout" : "Opaque");

        if (m.name.StartsWith("M_Ground_GravelRaked"))
        {
            // UV0 spans the whole plaza (rake normal baked in Blender); the gravel tiles as a detail map.
            mat.SetTexture("_BaseMap", null);
            mat.SetColor("_BaseColor", new Color(0.64f, 0.64f, 0.63f));
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTexture("_BumpMap", LoadTex("Textures/Source/T_Ground_GravelPlaza_RakeNormal.png"));
            mat.SetFloat("_BumpScale", 1.2f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetTexture("_DetailAlbedoMap", LoadTex("Textures/Source/T_Gravel_Shirakawa_BaseColor.png"));
            mat.SetFloat("_DetailAlbedoMapScale", 1f);
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(15.16f / 0.5f, 12.8f / 0.5f));
            mat.EnableKeyword("_DETAIL_MULX2");
        }
        else if (m.name.StartsWith("M_Water_Pond"))
        {
            mat.SetColor("_BaseColor", new Color(0.05f, 0.16f, 0.18f, 0.78f));
            mat.SetFloat("_Smoothness", 0.96f);
            mat.SetFloat("_Metallic", 0f);
            MakeTransparent(mat);
        }
        else if (m.name.StartsWith("M_Water_Foam") || m.name.StartsWith("M_FX_"))
        {
            MakeTransparent(mat);
        }
    }

    private static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Texture2D LoadTex(string rel) =>
        string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{rel}");

    private static void ConfigureModels(IEnumerable<AssetEntry> assets, Dictionary<string, Material> materials)
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (AssetEntry a in assets)
            {
                if (!(AssetImporter.GetAtPath($"{Root}/{a.model}") is ModelImporter mi)) { Debug.LogWarning($"[MenuEnvironment] No model at {a.model}"); continue; }
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importBlendShapes = false;
                mi.importVisibility = false;
                mi.isReadable = false;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.generateSecondaryUV = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (string matName in a.materials)
                {
                    if (string.IsNullOrEmpty(matName) || !materials.TryGetValue(matName, out Material mat)) continue;
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
                }
                mi.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    private static string PrefabPath(AssetEntry a) => $"{PrefabFolder}/{a.folder}/{a.name}.prefab";

    private static void CreatePrefabs(IEnumerable<AssetEntry> assets)
    {
        foreach (AssetEntry a in assets)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{a.model}");
            if (model == null) continue;
            string path = PrefabPath(a);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = a.name;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool foliage = a.name.Contains("Veg_") || a.name.Contains("Fern");
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                if (a.name.StartsWith("Water_") || a.name.StartsWith("SM_FX_")) r.shadowCastingMode = ShadowCastingMode.Off;
                if (foliage) r.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    // ---------------------------------------------------------------- scene

    private static Transform Group(Transform root, Dictionary<string, Transform> groups, string[] path)
    {
        Transform t = root;
        string key = "";
        foreach (string p in path ?? Array.Empty<string>())
        {
            key += "/" + p;
            if (!groups.TryGetValue(key, out Transform child))
            {
                child = new GameObject(p).transform;
                child.SetParent(t, false);
                groups[key] = child;
            }
            t = child;
        }
        return t;
    }

    private static void ApplyWorld(Transform t, ObjectEntry o)
    {
        var fwd = new Vector3(o.fwd[0], o.fwd[1], o.fwd[2]);
        var up = new Vector3(o.up[0], o.up[1], o.up[2]);
        t.SetPositionAndRotation(new Vector3(o.pos[0], o.pos[1], o.pos[2]), Quaternion.LookRotation(fwd, up));
        Vector3 s = new Vector3(o.scale[0], o.scale[1], o.scale[2]);
        Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(s.x / ps.x, s.y / ps.y, s.z / ps.z);
    }

    private static Light CreateLight(ObjectEntry o, Transform parent)
    {
        // The big area light only fakes skylight in Blender; Unity uses ambient light for that.
        if (o.name.Contains("SkyFill")) return null;
        var go = new GameObject(o.name);
        go.transform.SetParent(parent, false);
        ApplyWorld(go.transform, o);
        var l = go.AddComponent<Light>();
        l.color = new Color(o.color[0], o.color[1], o.color[2]);
        switch (o.light)
        {
            case "SUN":
                l.type = LightType.Directional;
                l.intensity = o.energy * 0.36f;
                l.shadows = LightShadows.Soft;
                break;
            case "SPOT":
                l.type = LightType.Spot;
                l.spotAngle = Mathf.Clamp(o.spot * Mathf.Rad2Deg, 5f, 170f);
                l.intensity = o.energy / 300f;
                l.range = 12f;
                l.shadows = LightShadows.None;
                break;
            case "AREA":
                l.type = LightType.Point;
                l.intensity = o.energy / 120f;
                l.range = Mathf.Clamp(Mathf.Sqrt(o.energy) * 0.8f, 3f, 14f);
                l.shadows = LightShadows.None;
                break;
            default:
                l.type = LightType.Point;
                l.intensity = o.energy / 18f;
                l.range = Mathf.Clamp(Mathf.Sqrt(o.energy) * 1.1f, 2f, 9f);
                l.shadows = LightShadows.None;
                break;
        }
        return l;
    }

    /// <summary>
    /// Unity-side lighting on top of the converted Blender lights. The menu camera looks along -Z
    /// (torii → main hall), so the sun comes from behind it and slightly right: the torii, hall front
    /// and ema faces are lit instead of back-lit. A shadowless cool fill and a soft key on the ema
    /// wall keep the shaded sides readable.
    /// </summary>
    private static void ApplyLightingRig(Transform lighting, Light sun)
    {
        if (sun != null)
        {
            // Low dusk sun: long shadows, warm rim on the roofs, the scene sits mostly in soft shade.
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.42f, -0.30f, -0.86f).normalized, Vector3.up);
            sun.color = new Color(1f, 0.72f, 0.50f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowNormalBias = 0.6f;
        }

        var fill = new GameObject("Light_Fill_Sky").AddComponent<Light>();
        fill.transform.SetParent(lighting, false);
        fill.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.6f, 0.66f).normalized, Vector3.up);
        fill.type = LightType.Directional;
        fill.color = new Color(0.62f, 0.70f, 1f);
        fill.intensity = 0.32f;
        fill.shadows = LightShadows.None;

        // Stone lanterns become the warm accents of the scene.
        foreach (Light l in lighting.GetComponentsInChildren<Light>())
        {
            if (!l.name.StartsWith("Light_Lantern__")) continue;
            l.color = new Color(1f, 0.66f, 0.38f);
            l.intensity *= 1.7f;
            l.range *= 1.25f;
        }

        var emaWall = GameObject.Find("SM_Arch_EmaWall_A__TerraceWest");
        if (emaWall != null)
        {
            Bounds b = emaWall.GetComponentInChildren<Renderer>().bounds;
            Vector3 front = emaWall.transform.forward;   // the ema faces look along the wall's forward axis
            var key = new GameObject("Light_Key_EmaWall").AddComponent<Light>();
            key.transform.SetParent(lighting, false);
            key.transform.position = b.center + front * 3.2f + Vector3.up * 1.6f - emaWall.transform.right * 0.8f;
            key.transform.rotation = Quaternion.LookRotation(b.center - key.transform.position, Vector3.up);
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.intensity = 7f;
            key.range = 9f;
            key.spotAngle = 100f;
            key.innerSpotAngle = 55f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.45f;

            // Shadowless fill from the viewer's side so the ema faces and the hokora never go muddy.
            var fillEma = new GameObject("Light_Fill_EmaWall").AddComponent<Light>();
            fillEma.transform.SetParent(lighting, false);
            fillEma.transform.position = b.center + front * 2.4f + emaWall.transform.right * 1.6f + Vector3.up * 1.0f;
            fillEma.type = LightType.Point;
            fillEma.color = new Color(1f, 0.95f, 0.90f);
            fillEma.intensity = 3.2f;
            fillEma.range = 6.5f;
            fillEma.shadows = LightShadows.None;
        }
    }

    /// <summary>Empty at the visual centre of a placed object, used as a mouse-look focus target.</summary>
    private static Transform CreateFocusPoint(string name, string targetName)
    {
        GameObject target = GameObject.Find(targetName);
        GameObject parent = GameObject.Find("95_Camera");
        if (target == null) return null;
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        var point = new GameObject(name).transform;
        if (parent != null) point.SetParent(parent.transform, false);
        point.position = b.center;
        return point;
    }

    private static void CreateMainCamera(Layout layout)
    {
        ObjectEntry start = Array.Find(layout.objects, o => o.name == MainCameraMarker);
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var look = go.AddComponent<SteamMultiplayer.UI.MenuCameraMouseLook>();
        look.SetFocusTargets(CreateFocusPoint("Focus_EmaWall", "SM_Arch_EmaWall_A__TerraceWest"),
                             CreateFocusPoint("Focus_Hokora", "SM_Arch_Hokora_A__TerraceWest"));
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1500f;
        if (start != null)
        {
            ApplyWorld(go.transform, start);
            go.transform.localScale = Vector3.one;
            float hfov = 2f * Mathf.Atan(36f / (2f * Mathf.Max(start.lens, 1f)));
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(hfov / 2f) / (16f / 9f)) * Mathf.Rad2Deg;
        }
        if (File.Exists(CameraPosePath))
        {
            var pose = JsonUtility.FromJson<CameraPose>(File.ReadAllText(CameraPosePath));
            go.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
            cam.fieldOfView = pose.fieldOfView;
        }

        // Pose markers and the Start → room camera move (its UI is wired in SetupRoomView).
        Transform poses = GameObject.Find("95_Camera") is GameObject camGroup ? camGroup.transform : null;
        CreatePoseMarker(SkillSelectPoseName, poses, go.transform.position, go.transform.rotation);
        Transform room = CreatePoseMarker(RoomPoseName, poses, RoomCameraPosition,
            Quaternion.LookRotation(RoomLookAt - RoomCameraPosition, Vector3.up));
        float roomFov = RoomFieldOfView;
        if (File.Exists(RoomPosePath))
        {
            var pose = JsonUtility.FromJson<CameraPose>(File.ReadAllText(RoomPosePath));
            room.SetPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
            roomFov = pose.fieldOfView;
        }

        var director = go.AddComponent<SteamMultiplayer.UI.MenuCameraDirector>();
        director.Configure(cam, look, room, roomFov, null, null, null);
        CreateTemporaryMenuCanvas();
    }

    // Main menu (title / idle) shot: the garden Buddha (skin) as the foreground, the main hall centred
    // behind it, the ema wall and hokora in the middle distance. Position, look-at target, field of view;
    // a non-null marker takes the pose of that Blender camera instead.
    private static readonly (string label, string marker, Vector3 position, Vector3 lookAt, float fov)[] TitleShots =
    {
        ("主菜单", null, new Vector3(1.6f, 1.5f, 4.4f), new Vector3(2.4f, 2.3f, -8f), 55f),
    };

    /// <summary>
    /// Proto panel (top left): title-screen candidates plus the skill-select view, then the Start button
    /// (camera move to the hall door); the skin view adds its "皮肤" button below. The game starts on the
    /// first title candidate. The whole panel hides away from the main menu and in the skin view.
    /// </summary>
    private static void SetupProtoCameraSwitcher()
    {
        Camera cam = Camera.main;
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        if (cam == null || canvas == null) return;
        Transform poses = GameObject.Find("95_Camera")?.transform;

        var shots = new List<SteamMultiplayer.UI.MenuCameraProtoSwitcher.Shot>();
        foreach (var t in TitleShots)
        {
            Transform pose;
            if (t.marker != null && GameObject.Find(t.marker) is GameObject m)
                pose = CreatePoseMarker("CamPose_Title_" + t.label, poses, m.transform.position, m.transform.rotation);
            else
                pose = CreatePoseMarker("CamPose_Title_" + t.label, poses, t.position, Quaternion.LookRotation(t.lookAt - t.position, Vector3.up));
            shots.Add(new SteamMultiplayer.UI.MenuCameraProtoSwitcher.Shot { label = t.label, pose = pose, fieldOfView = t.fov });
        }
        Transform skill = GameObject.Find(SkillSelectPoseName)?.transform;
        if (skill != null) shots.Add(new SteamMultiplayer.UI.MenuCameraProtoSwitcher.Shot { label = "技能选择", pose = skill, fieldOfView = cam.fieldOfView, useFocusTargets = true });

        // Panel with one button per shot.
        var panel = new GameObject("TEMP_CameraProtoPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(32f, -32f);
        panelRect.sizeDelta = new Vector2(240f, 0f);
        // Height follows the buttons, so later setup steps can add their own.
        panel.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.35f);
        var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panelRect, "Proto", 26f, 30f);
        foreach (var shot in shots)
            shot.button = CreateProtoButton(panelRect, "Btn_" + shot.label, shot.label);
        if (cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>() is SteamMultiplayer.UI.MenuCameraDirector director)
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                CreateProtoButton(panelRect, "TEMP_StartButton", "Start").onClick, director.PlayToRoom);

        // Full-screen fade used by the cut transition (kept above the panel, below nothing else).
        var fadeGo = new GameObject("TEMP_CameraFade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var fadeRect = fadeGo.GetComponent<RectTransform>();
        fadeRect.SetParent(canvas.transform, false);
        fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
        var fade = fadeGo.GetComponent<UnityEngine.UI.Image>();
        fade.color = new Color(0.02f, 0.015f, 0.02f, 0f);
        fade.raycastTarget = false;

        var switcher = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuCameraProtoSwitcher>();
        switcher.Configure(cam, cam.GetComponent<SteamMultiplayer.UI.MenuCameraMouseLook>(), cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>(),
            cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>(), shots, 0, fade,
            GameObject.Find("Focus_EmaWall")?.transform, GameObject.Find("Focus_Hokora")?.transform);
        EditorUtility.SetDirty(switcher);
    }

    /// <summary>One button of the proto panel (its layout group sizes and stacks it).</summary>
    private static UnityEngine.UI.Button CreateProtoButton(RectTransform panel, string name, string text)
    {
        var b = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
        var rect = b.GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        b.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 50f;
        b.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.08f, 0.06f, 0.72f);
        CreateLabel(rect, text, 24f, 0f);
        return b.GetComponent<UnityEngine.UI.Button>();
    }

    // Proto labels use Unity's built-in dynamic font, which falls back to the OS CJK fonts; the shipped
    // TMP font (BuddahGoSoloCJK) is a fixed subset and does not cover these temporary strings.
    private static void CreateLabel(RectTransform parent, string text, float size, float preferredHeight)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (preferredHeight > 0f) go.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = preferredHeight;
        var label = go.GetComponent<UnityEngine.UI.Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = Mathf.RoundToInt(size);
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.92f, 0.78f);
        label.raycastTarget = false;
    }

    /// <summary>
    /// Skill ema (every ema that is not a plain "普通" one) become clickable: a box collider for picking,
    /// their red cord as an attachment, and no static flags so they can move at runtime.
    /// </summary>
    private static void SetupEmaInspector(Transform root)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var items = new List<SteamMultiplayer.UI.MenuEmaInspector.Inspectable>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("绘马_") || t.name.Contains("普通")) continue;
            // The copies hanging in the hokora become loadout slots (see SetupSkillLoadout).
            if (t.name.EndsWith(HokoraSuffix)) continue;
            var attachments = new List<Transform>();
            if (GameObject.Find("红绳_" + t.name.Substring("绘马_".Length)) is GameObject cord) attachments.Add(cord.transform);
            foreach (Transform moving in new[] { t }.Concat(attachments))
                foreach (Transform c in moving.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
            FitEmaColliders(t);
            items.Add(new SteamMultiplayer.UI.MenuEmaInspector.Inspectable { item = t, attachments = attachments.ToArray() });
        }
        var inspector = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuEmaInspector>();
        inspector.SetInspectables(items);
        var so = new SerializedObject(inspector);
        so.FindProperty("targetCamera").objectReferenceValue = cam;
        so.FindProperty("mouseLook").objectReferenceValue = cam.GetComponent<SteamMultiplayer.UI.MenuCameraMouseLook>();
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[MenuEnvironment] {items.Count} skill ema are inspectable.");
    }

    /// <summary>A box collider on every mesh of a skill ema, fitted to that mesh.</summary>
    private static void FitEmaColliders(Transform ema)
    {
        foreach (MeshFilter mf in ema.GetComponentsInChildren<MeshFilter>(true))
        {
            Collider c = mf.GetComponent<Collider>();
            if (c == null) c = mf.gameObject.AddComponent<BoxCollider>();
            if (c is BoxCollider box && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
        }
    }

    private const string HokoraSuffix = "__Hokora";
    // Ema names (Blender) -> skill ids of the design tables.
    private static readonly Dictionary<string, string> EmaSkillIds = new Dictionary<string, string>
    {
        { "神足通", "acceleration" }, { "禅定缓流", "slowtrap" }, { "一念光明", "blackcurtain" },
        { "法相金身", "giant" }, { "如来神掌", "push_projectile_hands" }, { "逆姻缘", "reverseturn" },
    };

    /// <summary>
    /// Skill loadout on the hokora. The ema copies modelled inside the hokora mark the slots (their pose)
    /// and the starting loadout; they are hidden, and the wall ema themselves move into the slots, so a
    /// skill is never shown twice.
    /// </summary>
    private static void SetupSkillLoadout(Transform root)
    {
        Camera cam = Camera.main;
        var inspector = cam != null ? cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>() : null;
        if (inspector == null) return;
        Transform poses = GameObject.Find("95_Camera")?.transform;

        var slots = new List<Transform>();
        var initial = new List<string>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("绘马_") || !t.name.EndsWith(HokoraSuffix)) continue;
            string skillName = t.name.Substring("绘马_".Length, t.name.Length - "绘马_".Length - HokoraSuffix.Length);
            slots.Add(CreatePoseMarker($"LoadoutSlot_{slots.Count:00}", t.parent, t.position, t.rotation));
            initial.Add(EmaSkillIds.TryGetValue(skillName, out string id) ? id : "");
            t.gameObject.SetActive(false);
            if (GameObject.Find("红绳_" + skillName + HokoraSuffix) is GameObject cord) cord.SetActive(false);
        }

        var emas = new List<SteamMultiplayer.UI.MenuSkillLoadout.SkillEma>();
        SerializedProperty items = new SerializedObject(inspector).FindProperty("inspectables");
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty e = items.GetArrayElementAtIndex(i);
            if (!(e.FindPropertyRelative("item").objectReferenceValue is Transform item)) continue;
            SerializedProperty att = e.FindPropertyRelative("attachments");
            var attachments = new Transform[att.arraySize];
            for (int a = 0; a < att.arraySize; a++) attachments[a] = att.GetArrayElementAtIndex(a).objectReferenceValue as Transform;
            EmaSkillIds.TryGetValue(item.name.Substring("绘马_".Length), out string id);
            emas.Add(new SteamMultiplayer.UI.MenuSkillLoadout.SkillEma { skillId = id ?? item.name, item = item, attachments = attachments });
        }

        Renderer[] hokora = GameObject.Find("SM_Arch_Hokora_A__TerraceWest") is GameObject h ? h.GetComponentsInChildren<Renderer>() : new Renderer[0];
        var loadout = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuSkillLoadout>();
        loadout.Configure(cam, inspector, cam.GetComponent<SteamMultiplayer.UI.MenuSkinView>(), cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>(),
            cam.GetComponent<SteamMultiplayer.UI.MenuCameraMouseLook>(), emas, slots.ToArray(), initial.ToArray(), hokora);
        inspector.SetLoadout(loadout);
        EditorUtility.SetDirty(loadout); EditorUtility.SetDirty(inspector);
        Debug.Log($"[MenuEnvironment] Loadout: {slots.Count} slots, {emas.Count} skill ema, start [{string.Join(", ", initial)}].");
    }

    private static Transform CreatePoseMarker(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        var marker = new GameObject(name).transform;
        if (parent != null) marker.SetParent(parent, false);
        marker.SetPositionAndRotation(position, rotation);
        return marker;
    }

    // The race character; only its visuals (VisualRoot: skinned model, skeleton, Animator) are used in the menu.
    private const string PlayerBuddhaPrefabPath = "Assets/Character/Prefab/Buddah.prefab";
    private const string MenuBuddhaSkinPath = PrefabFolder + "/Skins/PF_MenuBuddha_Default.prefab";
    private const string SkinAnchorName = "Anchor_Skin_Buddha";
    private const string SkinPlinthName = "Arch_BuddhaPlinth_Garden";
    private const string MenuSkinName = "SKIN_Buddha_Default";
    // Largest size the skin may take on the garden plinth (metres).
    private const float SkinMaxHeight = 2.0f;
    private const float SkinMaxFootprint = 2.0f;

    /// <summary>Visual-only copy of the player Buddha: no networking, physics, skills or race effects.</summary>
    private static GameObject EnsureMenuBuddhaSkinPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBuddhaPrefabPath);
        if (source == null) { Debug.LogWarning($"[MenuEnvironment] Missing {PlayerBuddhaPrefabPath}"); return null; }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform visual = instance.transform.Find("VisualRoot");
        visual.SetParent(null, true);
        UnityEngine.Object.DestroyImmediate(instance);
        foreach (string effect in new[] { "沙砾", "技能Particle", "反噬Particle", "FeelVFX", "Trails" })
            if (visual.Find(effect) is Transform t) UnityEngine.Object.DestroyImmediate(t.gameObject);
        visual.name = "PF_MenuBuddha_Default";
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        var animator = visual.GetComponent<Animator>();
        if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        EnsureFolder(Path.GetDirectoryName(MenuBuddhaSkinPath).Replace('\\', '/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(visual.gameObject, MenuBuddhaSkinPath);
        UnityEngine.Object.DestroyImmediate(visual.gameObject);
        return prefab;
    }

    /// <summary>
    /// Replaces the placeholder statue under the skin anchor with the player Buddha, sized to the plinth,
    /// standing on the anchor (plinth top) and turned towards the main menu camera.
    /// </summary>
    private static SteamMultiplayer.UI.MenuBuddhaTurntable PlaceMenuBuddhaSkin()
    {
        GameObject anchor = GameObject.Find(SkinAnchorName);
        GameObject prefab = EnsureMenuBuddhaSkinPrefab();
        if (anchor == null || prefab == null) return null;
        for (int i = anchor.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(anchor.transform.GetChild(i).gameObject);

        var skin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor.transform);
        skin.name = MenuSkinName;
        // Turntable pivot on the plinth centre: slow idle, follows the pointer, turns when dragged.
        var table = new GameObject("SkinTurntable").transform;
        table.SetParent(anchor.transform, false);
        FitMenuBuddhaSkin(anchor.transform, table, skin.transform);
        var turntable = table.gameObject.AddComponent<SteamMultiplayer.UI.MenuBuddhaTurntable>();
        turntable.Configure(skin.GetComponent<Animator>(), Camera.main);

        GameObjectUtility.SetStaticEditorFlags(anchor, 0);
        foreach (Transform t in table.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        return turntable;
    }

    /// <summary>Stands the skin under the turntable on the anchor, sized to the plinth and facing the main menu camera.</summary>
    private static void FitMenuBuddhaSkin(Transform anchor, Transform table, Transform skin)
    {
        table.SetPositionAndRotation(anchor.position, Quaternion.identity);
        skin.SetParent(anchor, false);
        Vector3 facing = Vector3.back;
        if (TitleShots.Length > 0)
        {
            facing = TitleShots[0].position - anchor.position;
            facing.y = 0f;
        }
        Quaternion rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        skin.SetPositionAndRotation(anchor.position, rotation);
        skin.localScale = Vector3.one;

        // Fit against the whole idle animation, not the bind pose: the idle moves the head and hands.
        Bounds b = AnimatedBounds(skin.gameObject);
        float scale = Mathf.Min(SkinMaxHeight / Mathf.Max(b.size.y, 1e-3f), SkinMaxFootprint / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 1e-3f));
        Vector3 pivot = skin.position;
        Vector3 baseCentre = new Vector3(b.center.x, b.min.y, b.center.z);
        skin.localScale = Vector3.one * scale;
        // Lowest point of the motion rests on the plinth top, the motion is centred over the anchor.
        skin.position = anchor.position + (pivot - baseCentre) * scale;
        skin.SetParent(table, true);
    }

    // Emissive halo on the sand around the garden plinth (plinth corner radius is about 1.75 m).
    private const string HaloTexturePath = Root + "/Textures/Baked/T_FX_SkinHalo.png";
    private const string HaloMaterialPath = MaterialFolder + "/M_FX_SkinHalo.mat";
    private const float HaloRadius = 1.95f;        // radius of the bright ring line
    private const float HaloRingInTexture = 0.86f; // where the ring line sits in the texture (0..1 of the half size)
    private static readonly Color HaloColor = new Color(2.2f, 1.5f, 0.7f, 1f);

    private static void CreateSkinHalo()
    {
        GameObject anchor = GameObject.Find(SkinAnchorName);
        if (anchor == null) return;

        if (!File.Exists(HaloTexturePath))
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float line = Mathf.Exp(-Mathf.Pow((r - HaloRingInTexture) / 0.012f, 2f));
                float glow = Mathf.Exp(-Mathf.Pow((r - HaloRingInTexture) / 0.07f, 2f)) * 0.45f;
                float fill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, HaloRingInTexture, r)) * (r < HaloRingInTexture ? 0.12f : 0f);
                float a = Mathf.Clamp01(line + glow + fill) * (r < 1f ? 1f : 0f);
                byte b = (byte)Mathf.RoundToInt(a * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(HaloTexturePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(HaloTexturePath);
            if (AssetImporter.GetAtPath(HaloTexturePath) is TextureImporter ti)
            {
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(HaloMaterialPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, HaloMaterialPath); }
        mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(HaloTexturePath));
        mat.SetColor("_BaseColor", HaloColor);
        // Additive transparent: the ring only adds light, so it reads as a glow on the sand.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FX_SkinHalo";
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(anchor.transform, false);
        PlaceSkinHalo(quad.transform, anchor.transform);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(quad, 0);
    }

    /// <summary>The halo lies flat on the sand around the plinth, centred under the anchor.</summary>
    private static void PlaceSkinHalo(Transform halo, Transform anchor)
    {
        GameObject plinth = GameObject.Find(SkinPlinthName);
        float groundY = plinth != null ? plinth.transform.position.y : anchor.position.y - 0.32f;
        halo.SetPositionAndRotation(new Vector3(anchor.position.x, groundY + 0.015f, anchor.position.z), Quaternion.Euler(90f, 0f, 0f));
        float half = HaloRadius / HaloRingInTexture;
        Vector3 ps = anchor.lossyScale;
        halo.localScale = new Vector3(2f * half / ps.x, 2f * half / ps.y, 1f / ps.z);
    }

    private const string EmaWallName = "SM_Arch_EmaWall_A__TerraceWest";

    /// <summary>The ema wall plus every ema and red cord hanging on it (not the hidden hokora copies).</summary>
    private static Transform[] EmaWallParts()
    {
        GameObject wall = GameObject.Find(EmaWallName);
        if (wall == null) return new Transform[0];
        var parts = new List<Transform> { wall.transform };
        foreach (Transform t in wall.transform.parent.GetComponentsInChildren<Transform>(false))
            if ((t.name.StartsWith("绘马_") || t.name.StartsWith("红绳_")) && !t.name.EndsWith(HokoraSuffix))
                parts.Add(t);
        return parts.ToArray();
    }

    /// <summary>
    /// Main menu → skill select: on the main-menu shot the ema wall (with everything on it) is one
    /// button that switches to the skill-select shot; single ema react only on the skill-select shot.
    /// </summary>
    private static void SetupEmaWallShortcut()
    {
        Camera cam = Camera.main;
        var switcher = cam != null ? cam.GetComponent<SteamMultiplayer.UI.MenuCameraProtoSwitcher>() : null;
        GameObject wall = GameObject.Find(EmaWallName);
        if (switcher == null || wall == null) return;
        ClearEmaWallBatching();
        var proxyGo = new GameObject("EmaWallClickProxy");
        proxyGo.transform.SetParent(wall.transform, false);
        var proxy = proxyGo.AddComponent<BoxCollider>();
        FitEmaWallProxy(proxy, wall);
        proxy.enabled = false;
        switcher.ConfigureWallShortcut(EmaWallParts(), proxy, switcher.IndexOf("技能选择"), cam.GetComponent<SteamMultiplayer.UI.MenuSkinView>());
        EditorUtility.SetDirty(switcher);
        if (cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>() is SteamMultiplayer.UI.MenuEmaInspector inspector)
        {
            inspector.SetShotGate(switcher);
            EditorUtility.SetDirty(inspector);
        }
        if (cam.GetComponent<SteamMultiplayer.UI.MenuSkillLoadout>() is SteamMultiplayer.UI.MenuSkillLoadout loadout)
        {
            loadout.SetShotGate(switcher);
            EditorUtility.SetDirty(loadout);
        }
    }

    // The hover outline shares each part's mesh: a static-batched mesh is already in world space and
    // would be drawn transformed twice (huge glowing shells across the screen), so no batching here.
    private static void ClearEmaWallBatching()
    {
        foreach (Transform part in EmaWallParts()) ClearBatching(part);
    }

    /// <summary>Keeps every other static flag; only static batching goes (hover outlines share the mesh).</summary>
    private static void ClearBatching(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                GameObjectUtility.GetStaticEditorFlags(t.gameObject) & ~StaticEditorFlags.BatchingStatic);
    }

    /// <summary>One box over the wall and what hangs on it (the boards alone leave gaps between them).</summary>
    private static void FitEmaWallProxy(BoxCollider proxy, GameObject wall)
    {
        if (proxy == null) return;
        Bounds local = wall.GetComponent<MeshFilter>().sharedMesh.bounds;
        proxy.center = local.center;
        proxy.size = local.size + Vector3.one * 0.1f;
    }

    // Main-menu shortcuts: the hall building presses Start, the garden Buddha presses 皮肤.
    // The hall outline covers the building itself (walls, doors, roof, deck, verandas, shimenawa), not
    // the stone terrace, stairs, ema wall or hokora: about 230k vertices, drawn only while hovered.
    private static readonly string[] HondenShortcutParts =
    {
        "Arch_Honden_Wall_Front", "Arch_Honden_Wall_Back", "Arch_Honden_Wall_West", "Arch_Honden_Wall_East",
        "Arch_Honden_Door_L", "Arch_Honden_Door_R", "SM_Arch_Roof_Shrine_B__Honden_Main", "SM_Arch_Roof_Shrine_B_Trim__Honden_Main",
        "Arch_Honden_Deck", "SM_Arch_Boardwalk_A_Base__Honden_FrontVeranda", "SM_Arch_Boardwalk_A_Railing__Honden_Front_L",
        "SM_Arch_Boardwalk_A_Railing__Honden_Front_R", "SM_Arch_Boardwalk_A_Full__Honden_SideVeranda_W",
        "SM_Arch_Boardwalk_A_Full__Honden_SideVeranda_E", "Prop_Shimenawa",
        "SM_Prop_Shide_A__Honden_01", "SM_Prop_Shide_A__Honden_02", "SM_Prop_Shide_A__Honden_03", "SM_Prop_Shide_A__Honden_04",
    };
    private const string HondenClickProxyName = "HondenClickProxy";
    private const string SkinClickProxyName = "SkinClickProxy";

    private static Transform[] HondenParts() =>
        HondenShortcutParts.Select(n => GameObject.Find(n)).Where(g => g != null).Select(g => g.transform).ToArray();

    private static Bounds RenderBounds(IEnumerable<Transform> roots)
    {
        Bounds b = default;
        bool any = false;
        foreach (Transform root in roots)
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "HoverOutline") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
        return b;
    }

    /// <summary>Box collider (on an unrotated child) covering the given world-space bounds.</summary>
    private static void FitWorldBox(BoxCollider box, Bounds world)
    {
        if (box == null) return;
        Transform t = box.transform;
        t.rotation = Quaternion.identity;
        t.position = world.center;
        Vector3 s = t.lossyScale;
        box.center = Vector3.zero;
        box.size = new Vector3(world.size.x / s.x, world.size.y / s.y, world.size.z / s.z) + Vector3.one * 0.1f;
    }

    private static BoxCollider CreateClickProxy(string name, Transform parent, Bounds world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        var box = go.AddComponent<BoxCollider>();
        box.enabled = false;   // MenuClickShortcuts enables it only while the click is available
        FitWorldBox(box, world);
        return box;
    }

    /// <summary>
    /// Main menu: clicking the hall presses Start (glide to the door), clicking the garden Buddha presses
    /// 皮肤 (skin close-up). Both press the button itself; see <see cref="SteamMultiplayer.UI.MenuClickShortcuts"/>.
    /// </summary>
    private static void SetupMenuClickShortcuts()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var list = new List<SteamMultiplayer.UI.MenuClickShortcuts.Shortcut>();

        Transform[] hall = HondenParts();
        if (hall.Length > 0 && GameObject.Find("TEMP_StartButton") is GameObject start)
        {
            foreach (Transform t in hall) ClearBatching(t);   // the hover outline shares their meshes
            BoxCollider proxy = CreateClickProxy(HondenClickProxyName, hall[0].parent, RenderBounds(hall));
            list.Add(new SteamMultiplayer.UI.MenuClickShortcuts.Shortcut
                { label = "主殿 → Start", button = start.GetComponent<UnityEngine.UI.Button>(), root = hall[0], proxy = proxy });
        }
        else Debug.LogWarning("[MenuEnvironment] Hall shortcut: hall parts or Start button missing.");

        GameObject anchor = GameObject.Find(SkinAnchorName);
        Transform turntable = anchor != null ? anchor.transform.Find("SkinTurntable") : null;
        if (turntable != null && GameObject.Find("TEMP_SkinButton") is GameObject skin)
        {
            // Beside the turntable, not on it, so the skin view's own picking of the model is unaffected.
            BoxCollider proxy = CreateClickProxy(SkinClickProxyName, anchor.transform, RenderBounds(new[] { turntable }));
            list.Add(new SteamMultiplayer.UI.MenuClickShortcuts.Shortcut
                { label = "佛像 → 皮肤", button = skin.GetComponent<UnityEngine.UI.Button>(), root = turntable, proxy = proxy });
        }
        else Debug.LogWarning("[MenuEnvironment] Buddha shortcut: skin turntable or 皮肤 button missing.");

        var shortcuts = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuClickShortcuts>();
        shortcuts.Configure(cam, cam.GetComponent<SteamMultiplayer.UI.MenuCameraProtoSwitcher>(), cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>(),
            cam.GetComponent<SteamMultiplayer.UI.MenuSkinView>(), cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>(), list);
        EditorUtility.SetDirty(shortcuts);
    }

    /// <summary>Outline targets for the main-menu shortcuts: the hall's other parts and each proxy glow and pick with the root.</summary>
    private static IEnumerable<SteamMultiplayer.UI.MenuHoverHighlighter.Target> ShortcutTargets(SteamMultiplayer.UI.MenuClickShortcuts shortcuts)
    {
        foreach (SteamMultiplayer.UI.MenuClickShortcuts.Shortcut s in shortcuts.Shortcuts)
        {
            if (s.root == null) continue;
            var extras = new List<Transform>();
            if (s.root.name == HondenShortcutParts[0]) extras.AddRange(HondenParts().Skip(1));
            if (s.proxy != null) extras.Add(s.proxy.transform);
            yield return new SteamMultiplayer.UI.MenuHoverHighlighter.Target
                { root = s.root, kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.MenuShortcut, radial = 0.2f, extras = extras.ToArray() };
        }
    }

    private const string HoverShaderName = "BuddahGo/HoverOutline";
    private const string HoverMaterialPath = MaterialFolder + "/M_FX_HoverOutline.mat";

    /// <summary>Hover outline on every clickable/draggable object: the skill ema, the skin model and the bell rope.</summary>
    private static void SetupHoverHighlighter()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Shader shader = Shader.Find(HoverShaderName);
        if (shader == null) { Debug.LogWarning($"[MenuEnvironment] Missing shader {HoverShaderName}"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HoverMaterialPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, HoverMaterialPath); }
        mat.shader = shader;
        mat.SetColor("_Color", new Color(2.4f, 1.7f, 0.8f, 1f));
        mat.SetFloat("_Width", 4f);
        mat.SetFloat("_Pulse", 0.25f);
        EditorUtility.SetDirty(mat);

        var targets = new List<SteamMultiplayer.UI.MenuHoverHighlighter.Target>();
        var inspector = cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>();
        if (inspector != null)
        {
            SerializedProperty items = new SerializedObject(inspector).FindProperty("inspectables");
            for (int i = 0; i < items.arraySize; i++)
                if (items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue is Transform item)
                    targets.Add(new SteamMultiplayer.UI.MenuHoverHighlighter.Target { root = item, kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.SkillEma, radial = 0.7f });
        }
        if (GameObject.Find("SkinTurntable") is GameObject table)
            targets.Add(new SteamMultiplayer.UI.MenuHoverHighlighter.Target { root = table.transform, kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.SkinModel, radial = 0f });
        if (GameObject.Find(BellRopeGroupName) is GameObject bellRope)
            targets.Add(new SteamMultiplayer.UI.MenuHoverHighlighter.Target { root = bellRope.transform, kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.BellRope, radial = 0f });
        Transform[] wallParts = EmaWallParts();
        if (wallParts.Length > 0)
            targets.Add(new SteamMultiplayer.UI.MenuHoverHighlighter.Target { root = wallParts[0], kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.EmaWall,
                radial = 0.3f, extras = wallParts.Skip(1).ToArray() });
        if (cam.GetComponent<SteamMultiplayer.UI.MenuClickShortcuts>() is SteamMultiplayer.UI.MenuClickShortcuts clickShortcuts)
            targets.AddRange(ShortcutTargets(clickShortcuts));
        if (GameObject.Find(SandTableName) is GameObject sandTable)
            targets.Add(new SteamMultiplayer.UI.MenuHoverHighlighter.Target { root = sandTable.transform, kind = SteamMultiplayer.UI.MenuHoverHighlighter.TargetKind.SandTable,
                radial = 0.3f, rootOnly = true });

        var highlighter = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuHoverHighlighter>();
        highlighter.Configure(cam, inspector, cam.GetComponent<SteamMultiplayer.UI.MenuSkinView>(), cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>(), mat, targets);
        highlighter.SetShotGate(cam.GetComponent<SteamMultiplayer.UI.MenuCameraProtoSwitcher>());
        highlighter.SetSandTable(cam.GetComponent<SteamMultiplayer.UI.MenuSandTableFocus>());
        highlighter.SetShortcuts(cam.GetComponent<SteamMultiplayer.UI.MenuClickShortcuts>());
        EditorUtility.SetDirty(highlighter);
        Debug.Log($"[MenuEnvironment] Hover outline on {targets.Count} objects.");
    }

    private const string SkinPoseName = "CamPose_Skin_皮肤";
    // Skin view framing relative to the plinth top: look-at height, camera distance in front, extra camera height.
    private const float SkinLookHeight = 0.75f, SkinCameraDistance = 3.1f, SkinCameraRise = 0.2f, SkinFieldOfView = 40f;

    /// <summary>"皮肤" button: glide to a close-up of the garden Buddha, drag the model to inspect it, "返回" to go back.</summary>
    private static void SetupMenuSkinView(SteamMultiplayer.UI.MenuBuddhaTurntable turntable)
    {
        Camera cam = Camera.main;
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        GameObject anchor = GameObject.Find(SkinAnchorName);
        GameObject panel = GameObject.Find("TEMP_CameraProtoPanel");
        if (turntable == null || cam == null || canvas == null || anchor == null || panel == null) return;

        SkinCameraPose(anchor.transform, out Vector3 camPos, out Quaternion camRot);
        Transform poses = GameObject.Find("95_Camera")?.transform;
        Transform pose = CreatePoseMarker(SkinPoseName, poses, camPos, camRot);

        var enter = CreateProtoButton(panel.GetComponent<RectTransform>(), "TEMP_SkinButton", "皮肤");
        var back = CreateTempButton(canvas.transform, "TEMP_SkinBackButton", "返回", new Vector2(-64f, 64f));
        // Start and 皮肤 sit in the proto panel, so hiding the panel hides them too.
        var hide = new List<GameObject> { panel };

        var view = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuSkinView>();
        view.Configure(cam, cam.GetComponent<SteamMultiplayer.UI.MenuCameraMouseLook>(), cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>(),
            turntable, pose, SkinFieldOfView, enter, back, hide.ToArray());
        EditorUtility.SetDirty(view);
    }

    /// <summary>Skin shot: in front of the garden Buddha, on the side of the main menu camera, looking at its chest.</summary>
    private static void SkinCameraPose(Transform anchor, out Vector3 position, out Quaternion rotation)
    {
        Vector3 facing = TitleShots.Length > 0 ? TitleShots[0].position - anchor.position : Vector3.back;
        facing.y = 0f; facing.Normalize();
        Vector3 target = anchor.position + Vector3.up * SkinLookHeight;
        position = target + facing * SkinCameraDistance + Vector3.up * SkinCameraRise;
        rotation = Quaternion.LookRotation(target - position, Vector3.up);
    }

    private static UnityEngine.UI.Button CreateTempButton(Transform canvas, string name, string text, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(260f, 84f);
        go.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.08f, 0.06f, 0.78f);
        var button = go.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
        colors.pressedColor = new Color(0.8f, 0.6f, 0.4f);
        button.colors = colors;
        CreateLabel(rect, text, 36f, 0f);
        return button;
    }

    /// <summary>
    /// World bounds of the skin over its idle clip (sampled), measured at unit scale. Leaves the bones
    /// back in their prefab pose afterwards.
    /// </summary>
    private static Bounds AnimatedBounds(GameObject skin)
    {
        var animator = skin.GetComponent<Animator>();
        AnimationClip idle = null;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c.name.Contains("Idle")) { idle = c; break; }

        var renderers = skin.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var baked = new Mesh();
        bool first = true;
        Bounds result = new Bounds(skin.transform.position, Vector3.zero);
        int samples = idle != null ? 24 : 1;
        for (int i = 0; i < samples; i++)
        {
            if (idle != null) idle.SampleAnimation(skin, idle.length * i / samples);
            foreach (SkinnedMeshRenderer r in renderers)
            {
                r.BakeMesh(baked, false);
                // A baked skinned mesh (useScale off) is already at world size relative to the renderer.
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (first) { result = new Bounds(w, Vector3.zero); first = false; } else result.Encapsulate(w);
                }
            }
        }
        UnityEngine.Object.DestroyImmediate(baked);
        // Undo the sampled pose on the bones (keep the root's own transform).
        Vector3 pos = skin.transform.position; Quaternion rot = skin.transform.rotation; Vector3 scl = skin.transform.localScale;
        string name = skin.name;
        PrefabUtility.RevertPrefabInstance(skin, InteractionMode.AutomatedAction);
        skin.name = name;
        skin.transform.SetPositionAndRotation(pos, rot);
        skin.transform.localScale = scl;
        return result;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in renderers)
        {
            r.updateWhenOffscreen = true;
            Bounds rb = r.bounds;
            if (first) { b = rb; first = false; } else b.Encapsulate(rb);
        }
        return b;
    }

    /// <summary>
    /// Room view (房间): Start moves the camera to the closed hall door, where temporary "创建房间" and
    /// "加入房间" buttons stand in for the room UI; "返回" flies back. "创建房间" opens the doors and walks
    /// into the hall (房间内, its own "返回" walks back out); the lobby itself is not wired up yet.
    /// </summary>
    private static void SetupRoomView()
    {
        Camera cam = Camera.main;
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        var director = cam != null ? cam.GetComponent<SteamMultiplayer.UI.MenuCameraDirector>() : null;
        if (director == null || canvas == null) return;

        var panel = new GameObject("TEMP_RoomPanel", typeof(RectTransform));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        // Placeholders: the lobby flow (CreateRoomUI / RoomJoinUI) is not hooked up yet.
        var create = CreateTempButton(panelRect, "TEMP_CreateRoomButton", "创建房间", new Vector2(-64f, 272f));
        CreateTempButton(panelRect, "TEMP_JoinRoomButton", "加入房间", new Vector2(-64f, 168f));
        var back = CreateTempButton(panelRect, "TEMP_RoomBackButton", "返回", new Vector2(-64f, 64f));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(back.onClick, director.ReturnFromRoom);

        // Inside the hall: the doors swing (so they must not be static) and a back button walks out again.
        var doors = HallDoors.Select(n => GameObject.Find(n)).Where(g => g != null).ToArray();
        if (doors.Length != HallDoors.Length) Debug.LogWarning("[MenuEnvironment] Hall doors not found.");
        foreach (GameObject door in doors)
            foreach (Transform t in door.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        Transform poses = GameObject.Find("95_Camera")?.transform;
        Transform inside = CreatePoseMarker(RoomInsidePoseName, poses, RoomInsidePosition,
            Quaternion.LookRotation(RoomInsideLookAt - RoomInsidePosition, Vector3.up));
        var insidePanel = new GameObject("TEMP_RoomInsidePanel", typeof(RectTransform));
        var insideRect = insidePanel.GetComponent<RectTransform>();
        insideRect.SetParent(canvas.transform, false);
        insideRect.anchorMin = Vector2.zero; insideRect.anchorMax = Vector2.one;
        insideRect.offsetMin = insideRect.offsetMax = Vector2.zero;
        var leave = CreateTempButton(insideRect, "TEMP_RoomLeaveButton", "返回", new Vector2(-64f, 64f));
        // Soft warm fill so the closed hall is not a black box from the inside view.
        var fill = new GameObject("Light_Honden_InteriorFill").AddComponent<Light>();
        fill.transform.SetParent(GameObject.Find("90_Lighting")?.transform, false);
        fill.transform.position = RoomInteriorFillPosition;
        fill.type = LightType.Point;
        fill.color = new Color(1f, 0.82f, 0.62f);
        fill.intensity = 2.2f;
        fill.range = 7.5f;
        fill.shadows = LightShadows.None;
        var roomView = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuRoomView>();
        roomView.Configure(cam, director, doors.Select(d => d.transform).ToArray(), inside, RoomInsideFieldOfView,
            GameObject.Find("TEMP_CameraFade")?.GetComponent<UnityEngine.UI.Image>(), create, panel, insidePanel, leave);
        EditorUtility.SetDirty(roomView);
        SetupBellRope(cam, director, roomView);

        // Start and 皮肤 sit in the proto panel.
        var hide = new[] { "TEMP_CameraProtoPanel" }
            .Select(n => GameObject.Find(n)).Where(g => g != null).ToArray();
        var disable = new Behaviour[] { cam.GetComponent<SteamMultiplayer.UI.MenuEmaInspector>() }.Where(b => b != null).ToArray();
        director.Configure(cam, cam.GetComponent<SteamMultiplayer.UI.MenuCameraMouseLook>(), GameObject.Find(RoomPoseName).transform,
            RoomPoseFieldOfView(), panel, hide, disable);
        EditorUtility.SetDirty(director);
    }

    private const string SandTableName = "Prop_SandTable_Honden";
    private const string LobbySlotPrefix = "Anchor_LobbySlot_";
    private const int LobbySlotCount = 6;
    // The race map drawn on the sand: the in-game minimap art (track outline on transparent).
    private const string RaceMapTexturePath = "Assets/UI/UI资源/地图UI-PNG/地图 1.png";
    // Sand bed of the table (inside the rim) and its height above the table origin.
    private const float SandBedWidth = 1.52f, SandBedDepth = 1.29f, SandBedTop = 0.457f;
    // The race map lies flat on the sand; its top edge points to world -Z.
    private static readonly Quaternion SandMapRotation = Quaternion.Euler(90f, 180f, 0f);

    /// <summary>
    /// Room lobby: the race map on the sand table, six player spots (empties from Blender; the host takes
    /// spot 1 on entering the room) and two temporary buttons in the inside view that simulate other
    /// players joining and leaving.
    /// </summary>
    private static void SetupLobby()
    {
        Camera cam = Camera.main;
        GameObject table = GameObject.Find(SandTableName);
        GameObject insidePanel = GameObject.Find("TEMP_RoomInsidePanel");
        if (cam == null || table == null || insidePanel == null) { Debug.LogWarning("[MenuEnvironment] Lobby pieces missing."); return; }
        CreateSandTableMap(table.transform);

        var slots = new List<Transform>();
        for (int i = 1; i <= LobbySlotCount; i++)
            if (GameObject.Find(LobbySlotPrefix + i) is GameObject slot) slots.Add(slot.transform);

        var rect = insidePanel.GetComponent<RectTransform>();
        var join = CreateTempButton(rect, "TEMP_LobbyJoinButton", "模拟加入", new Vector2(-64f, 272f));
        var leave = CreateTempButton(rect, "TEMP_LobbyLeaveButton", "模拟退出", new Vector2(-64f, 168f));
        var countArea = new GameObject("TEMP_LobbyCount", typeof(RectTransform)).GetComponent<RectTransform>();
        countArea.SetParent(rect, false);
        countArea.anchorMin = countArea.anchorMax = countArea.pivot = new Vector2(1f, 0f);
        countArea.anchoredPosition = new Vector2(-64f, 364f);
        countArea.sizeDelta = new Vector2(260f, 48f);
        CreateLabel(countArea, "玩家 0/6", 30f, 0f);
        var count = countArea.GetComponentInChildren<UnityEngine.UI.Text>();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuddhaSkinPath);
        var lobby = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuLobby>();
        lobby.Configure(slots.ToArray(), prefab, join, leave, count, cam.GetComponent<SteamMultiplayer.UI.MenuRoomView>());
        EditorUtility.SetDirty(lobby);
        foreach (Transform slot in slots) CreateLobbyPreview(slot, prefab);
    }

    private const string SandTablePoseName = "CamPose_SandTable_沙盘";
    // Sand table shot: camera distance from the sand bed centre, its elevation angle, and field of view.
    private const float SandTableCameraDistance = 3.0f, SandTableCameraElevation = 55f, SandTableFieldOfView = 45f;

    /// <summary>
    /// Click the sand table in the lobby view: glide to a view over it (沙盘; the marker sets the angle,
    /// the distance is fitted to the table at runtime) with a soft depth of field;
    /// "返回", Esc or right-click glide back. The lobby buttons hide while focused.
    /// </summary>
    private static void SetupSandTableFocus()
    {
        Camera cam = Camera.main;
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        GameObject table = GameObject.Find(SandTableName);
        GameObject insidePanel = GameObject.Find("TEMP_RoomInsidePanel");
        var roomView = cam != null ? cam.GetComponent<SteamMultiplayer.UI.MenuRoomView>() : null;
        if (canvas == null || table == null || roomView == null) { Debug.LogWarning("[MenuEnvironment] Sand table focus pieces missing."); return; }

        ClearBatching(table.transform);   // the hover outline shares the table mesh
        SandTableCameraPose(table.transform, out Vector3 camPos, out Quaternion camRot);
        Transform pose = CreatePoseMarker(SandTablePoseName, GameObject.Find("95_Camera")?.transform, camPos, camRot);
        var back = CreateTempButton(canvas.transform, "TEMP_SandTableBackButton", "返回", new Vector2(-64f, 64f));
        var hide = insidePanel != null ? new[] { insidePanel } : new GameObject[0];

        var focus = cam.gameObject.AddComponent<SteamMultiplayer.UI.MenuSandTableFocus>();
        focus.Configure(cam, roomView, table.GetComponent<Renderer>(), pose, SandTableFieldOfView, back, hide);
        EditorUtility.SetDirty(focus);
    }

    /// <summary>
    /// Sand table shot: square to the table edge from which the race map reads upright (closer than
    /// from a corner, where the diagonal runs along the view), looking down at the sand bed centre.
    /// </summary>
    private static void SandTableCameraPose(Transform table, out Vector3 position, out Quaternion rotation)
    {
        Vector3 target = table.position + Vector3.up * SandBedTop;
        Vector3 side = -(SandMapRotation * Vector3.up);   // the map's top points away from the camera
        side.y = 0f; side.Normalize();
        float elevation = SandTableCameraElevation * Mathf.Deg2Rad;
        position = target + side * (SandTableCameraDistance * Mathf.Cos(elevation)) + Vector3.up * (SandTableCameraDistance * Mathf.Sin(elevation));
        rotation = Quaternion.LookRotation(target - position, Vector3.up);
    }

    private const float LobbyPlayerHeight = 1.0f;

    /// <summary>
    /// Editor-only stand-in on a player spot so the spots can be arranged in the Scene view (move and turn
    /// the Anchor_LobbySlot_N parent, not the stand-in: players spawn on the parent's pose). MenuLobby
    /// removes it when the game runs; EditorOnly keeps it out of builds.
    /// </summary>
    private static void CreateLobbyPreview(Transform slot, GameObject prefab)
    {
        if (prefab == null) return;
        var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        preview.name = SteamMultiplayer.UI.MenuLobby.PreviewPrefix + slot.name.Substring(LobbySlotPrefix.Length);
        preview.transform.SetParent(slot, false);
        preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        // Same sizing as MenuLobby at runtime: scale the prefab's own root scale to the wanted height.
        var renderers = preview.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            preview.transform.localScale *= LobbyPlayerHeight / Mathf.Max(b.size.y, 1e-3f);
        }
        preview.tag = "EditorOnly";
        foreach (Transform t in preview.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }

    private const string SandTableTrackTexturePath = Root + "/Textures/Baked/T_FX_SandTableTrack.png";

    /// <summary>
    /// The minimap art draws the track as a faint fill (about 20% alpha) between two solid edge lines;
    /// pressed into sand it needs a solid fill, so the alpha is boosted into a baked copy.
    /// </summary>
    private static Texture2D BakeSandTableTrack()
    {
        var src = new Texture2D(2, 2);
        src.LoadImage(File.ReadAllBytes(RaceMapTexturePath));
        Color32[] px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++) px[i].a = (byte)Mathf.Min(255, px[i].a * 4);
        src.SetPixels32(px);
        File.WriteAllBytes(SandTableTrackTexturePath, src.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset(SandTableTrackTexturePath);
        if (AssetImporter.GetAtPath(SandTableTrackTexturePath) is TextureImporter ti)
        {
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SandTableTrackTexturePath);
    }

    /// <summary>Two stacked quads on the sand: the track pressed in dark, then a warm additive glow on its edges.</summary>
    private static void CreateSandTableMap(Transform table)
    {
        if (AssetImporter.GetAtPath(RaceMapTexturePath) == null) { Debug.LogWarning("[MenuEnvironment] Race map texture missing."); return; }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RaceMapTexturePath);
        Texture2D track = BakeSandTableTrack();
        float aspect = (float)tex.width / tex.height;
        float depth = Mathf.Min(SandBedDepth, SandBedWidth / aspect) * 0.96f;
        var layers = new (string name, Texture2D map, Color color, bool additive, float lift)[]
        {
            ("FX_SandTableMap_Track", track, new Color(0.10f, 0.065f, 0.04f, 0.92f), false, 0.002f),
            ("FX_SandTableMap_Glow", tex, new Color(1.3f, 0.85f, 0.38f, 0.55f), true, 0.004f),
        };
        foreach (var layer in layers)
        {
            string path = $"{MaterialFolder}/M_{layer.name.Substring(3)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetTexture("_BaseMap", layer.map);
            mat.SetColor("_BaseColor", layer.color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", layer.additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(layer.additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + (layer.additive ? 1 : 0);
            EditorUtility.SetDirty(mat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = layer.name;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(table, false);
            // Lying on the sand, the top of the map towards the back of the hall.
            quad.transform.SetPositionAndRotation(table.position + Vector3.up * (SandBedTop + layer.lift), SandMapRotation);
            Vector3 ps = table.lossyScale;
            quad.transform.localScale = new Vector3(depth * aspect / ps.x, depth / ps.y, 1f / ps.z);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(quad, 0);
        }
    }

    private const string BellClusterName = "Prop_Suzu_Honden";
    private const string BellRopeGroupName = "BellRope_Honden";
    // Front face of the hall (doors, sill) the rope must stay in front of, and the floor it lies on: the
    // front veranda boards, which sit 8 cm above the hall floor.
    private const float HallFrontZ = -12.1f, HallFrontClearance = 0.13f, HallFloorY = 2.345f, BellRopeSlack = 0.3f;

    /// <summary>
    /// Prayer bell rope (鈴緒) in front of the doors: the bell cluster from Blender (origin on its hook)
    /// plus a simulated red-and-white rope hanging from below the middle bell down onto the floor.
    /// </summary>
    private static void SetupBellRope(Camera cam, SteamMultiplayer.UI.MenuCameraDirector director, SteamMultiplayer.UI.MenuRoomView roomView)
    {
        GameObject bells = GameObject.Find(BellClusterName);
        if (bells == null) { Debug.LogWarning($"[MenuEnvironment] {BellClusterName} not found."); return; }
        foreach (Transform t in bells.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var group = new GameObject(BellRopeGroupName).transform;
        group.SetParent(bells.transform.parent, false);
        group.position = bells.transform.position;
        bells.transform.SetParent(group, true);

        var ropeGo = new GameObject("BellRope", typeof(MeshFilter), typeof(MeshRenderer), typeof(AudioSource));
        ropeGo.transform.SetParent(group, false);
        var mr = ropeGo.GetComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { RopeMaterial("M_Prop_BellRope_Red", new Color(0.55f, 0.05f, 0.04f)),
                                     RopeMaterial("M_Prop_BellRope_White", new Color(0.86f, 0.83f, 0.76f)) };
        var audio = ropeGo.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.6f;
        audio.minDistance = 3f;
        audio.volume = 0.8f;

        var rope = ropeGo.AddComponent<SteamMultiplayer.UI.MenuBellRope>();
        var (anchorLocal, length, wallPoint) = BellRopeHang(bells.transform);
        rope.Configure(cam, director, roomView, bells.transform, anchorLocal, length, HallFloorY,
            wallPoint, Vector3.forward, audio, new[] { cam.transform });
        // Pulling the rope down rings the bells and then does what "创建房间" does.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(rope.Pulled, roomView.Enter);
        CreateRopeHint(rope);
        EditorUtility.SetDirty(rope);
    }

    /// <summary>
    /// Where the rope hangs from the bells: just below the lowest point of the cluster (the big middle
    /// bell, in the bells' local space), long enough to reach the floor with some slack, and the point on
    /// the hall front it must stay in front of.
    /// </summary>
    private static (Vector3 anchorLocal, float length, Vector3 wallPoint) BellRopeHang(Transform bells)
    {
        Bounds b = bells.GetComponentInChildren<Renderer>().bounds;
        Vector3 hook = bells.position;
        Vector3 anchorWorld = new Vector3(hook.x, b.min.y - 0.005f, hook.z);
        return (bells.InverseTransformPoint(anchorWorld), anchorWorld.y - HallFloorY + BellRopeSlack,
                new Vector3(hook.x, HallFloorY, HallFrontZ + HallFrontClearance));
    }

    /// <summary>"按住下拉 摇铃" next to the pointer over the rope, with a bar that fills as the rope is pulled.</summary>
    private static void CreateRopeHint(SteamMultiplayer.UI.MenuBellRope rope)
    {
        GameObject canvas = GameObject.Find("TEMP_MenuCanvas");
        if (canvas == null) return;
        var go = new GameObject("TEMP_RopeHint", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(210f, 70f);
        var bg = go.GetComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.08f, 0.05f, 0.04f, 0.62f);
        bg.raycastTarget = false;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        var labelRect = new GameObject("LabelArea", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = new Vector2(0f, 0.35f); labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        CreateLabel(labelRect, "按住下拉 摇铃 ↓", 24f, 0f);

        var track = new GameObject("Track", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        track.SetParent(rect, false);
        track.anchorMin = new Vector2(0.08f, 0.14f); track.anchorMax = new Vector2(0.92f, 0.28f);
        track.offsetMin = track.offsetMax = Vector2.zero;
        var trackImage = track.GetComponent<UnityEngine.UI.Image>();
        trackImage.color = new Color(1f, 0.92f, 0.78f, 0.18f); trackImage.raycastTarget = false;

        var fillRect = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        fillRect.SetParent(track, false);
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        var fill = fillRect.GetComponent<UnityEngine.UI.Image>();
        // a filled image needs a sprite; the built-in UI sprite keeps it a plain bar
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.color = new Color(1f, 0.72f, 0.32f, 0.95f);
        fill.raycastTarget = false;

        rope.SetHint(rect, fill, group);
    }

    private static Material RopeMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.22f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static float RoomPoseFieldOfView() =>
        File.Exists(RoomPosePath) ? JsonUtility.FromJson<CameraPose>(File.ReadAllText(RoomPosePath)).fieldOfView : RoomFieldOfView;

    /// <summary>Temporary overlay canvas for the menu buttons (Start lives in the proto panel) and the event system.</summary>
    private static void CreateTemporaryMenuCanvas()
    {
        var canvasGo = new GameObject("TEMP_MenuCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    private static void SetupEnvironment(Light sun)
    {
        string skyPath = $"{MaterialFolder}/SKY_MenuEnvironment.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        // Equirectangular render of the Blender world (sky-mirror gradient with the pink horizon band).
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.shader = Shader.Find("Skybox/Panoramic");
        string skyTexPath = Root + "/Textures/Baked/T_Sky_MenuEnvironment.png";
        if (AssetImporter.GetAtPath(skyTexPath) is TextureImporter skyImporter && skyImporter.maxTextureSize != 4096)
        {
            skyImporter.maxTextureSize = 4096;
            skyImporter.mipmapEnabled = false;
            skyImporter.wrapModeU = TextureWrapMode.Repeat;
            skyImporter.wrapModeV = TextureWrapMode.Clamp;
            skyImporter.SaveAndReimport();
        }
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(skyTexPath));
        sky.SetFloat("_Mapping", 1f);
        sky.SetFloat("_ImageType", 0f);
        sky.SetFloat("_Exposure", 1f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.69f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.26f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        // Lavender-rose haze that eats the far rim and the cliff base, for depth.
        RenderSettings.fogColor = new Color(0.58f, 0.54f, 0.64f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 210f;
        RenderSettings.reflectionIntensity = 0.6f;
    }

    private static void CreateGlobalVolume()
    {
        string profilePath = Root + "/Data/VP_MenuEnvironment.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        // Dusk grade: darker exposure, firmer contrast, cool shadows / warm highlights, lantern bloom.
        Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
        var bloom = Get<Bloom>(profile);
        bloom.intensity.Override(1.1f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.82f, 0.66f));
        var color = Get<ColorAdjustments>(profile);
        color.postExposure.Override(0.45f); color.contrast.Override(8f); color.saturation.Override(-4f);
        color.colorFilter.Override(new Color(1f, 0.95f, 0.92f));
        var split = Get<SplitToning>(profile);
        split.shadows.Override(new Color(0.46f, 0.50f, 0.58f)); split.highlights.Override(new Color(0.64f, 0.55f, 0.47f)); split.balance.Override(-25f);
        var smh = Get<ShadowsMidtonesHighlights>(profile);
        smh.shadows.Override(new Vector4(0.95f, 0.97f, 1.04f, -0.04f));
        var vig = Get<Vignette>(profile);
        vig.intensity.Override(0.2f); vig.smoothness.Override(0.55f); vig.color.Override(new Color(0.08f, 0.06f, 0.10f));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        var go = new GameObject("Global Volume");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T c))
        {
            c = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        c.active = true;
        return c;
    }

    private static void MarkStatic(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
