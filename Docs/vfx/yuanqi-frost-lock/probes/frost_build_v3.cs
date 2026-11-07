// Frost-lock ice swatches: three DontSave stand-ins (Buddha proxy + faceted crystal shell), one material each.
// Nothing is saved: objects are HideFlags.DontSave in the open RaceMap; materials/meshes are in-memory.
// C# 6 (CodeDom). Re-run safe: deletes the previous FrostSwatch_* objects first.
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/RaceMap.unity") return "FAILED: active scene is " + scene.path;
if (scene.isDirty) return "FAILED: RaceMap has unsaved changes; not touching it";
foreach (var old in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if (old != null && old.name.StartsWith("FrostSwatch") && old.scene.IsValid()) UnityEngine.Object.DestroyImmediate(old);

// ---- Buddha proxy mesh (same bake as RaceMapDuskColorStudy.CreatePlayerProxy)
var root = UnityEditor.PrefabUtility.LoadPrefabContents(UnityEditor.AssetDatabase.GUIDToAssetPath("c2b8c569b26585545904610506c355cd"));
UnityEngine.Mesh body; UnityEngine.Material bodyMat; float clearance;
try {
    var smr = root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>(true);
    UnityEngine.CapsuleCollider cap = null;
    foreach (var c in root.GetComponents<UnityEngine.CapsuleCollider>()) if (!c.isTrigger) { cap = c; break; }
    body = new UnityEngine.Mesh { name = "FrostSwatch_Body", hideFlags = UnityEngine.HideFlags.DontSave };
    smr.BakeMesh(body, true);
    var toRoot = root.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
    var vs = body.vertices;
    for (int i = 0; i < vs.Length; i++) vs[i] = toRoot.MultiplyPoint3x4(vs[i]);
    body.vertices = vs; body.RecalculateNormals(); body.RecalculateBounds();
    bodyMat = smr.sharedMaterial;
    clearance = cap.height * 0.5f - cap.center.y;
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
var bb = body.bounds;

// ---- Faceted crystal shell: a tall irregular bipyramid around the body + tilted shards round the base. Flat shaded.
System.Func<int, UnityEngine.Mesh> buildCrystal = seed => {
    var rnd = new System.Random(seed);
    System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
    var pos = new System.Collections.Generic.List<UnityEngine.Vector3>();
    var tris = new System.Collections.Generic.List<int>();
    // prism: n-gon ring at base, ring at shoulder, tip; axis tilted
    System.Action<UnityEngine.Vector3, UnityEngine.Vector3, float, float, float, int> prism = (basePt, axis, radius, height, tipLen, sides) => {
        axis.Normalize();
        var side = UnityEngine.Vector3.Cross(axis, UnityEngine.Mathf.Abs(axis.y) > 0.9f ? UnityEngine.Vector3.right : UnityEngine.Vector3.up).normalized;
        var fwd = UnityEngine.Vector3.Cross(axis, side);
        var ringA = new UnityEngine.Vector3[sides]; var ringB = new UnityEngine.Vector3[sides];
        float rot = R(0f, 6.28f);
        for (int k = 0; k < sides; k++) {
            float ang = rot + k * 6.2832f / sides + R(-0.15f, 0.15f);
            var dir = side * UnityEngine.Mathf.Cos(ang) + fwd * UnityEngine.Mathf.Sin(ang);
            ringA[k] = basePt + dir * radius * R(0.85f, 1.1f);
            ringB[k] = basePt + axis * height * R(0.9f, 1.05f) + dir * radius * R(0.8f, 1.05f);
        }
        var tip = basePt + axis * (height + tipLen) + side * R(-0.1f, 0.1f) * radius;
        var bottom = basePt - axis * radius * 0.6f;
        for (int k = 0; k < sides; k++) {
            int n = (k + 1) % sides;
            System.Action<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector3> tri = (a, b, c) => { int s = pos.Count; pos.Add(a); pos.Add(b); pos.Add(c); tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); };
            tri(ringA[k], ringB[k], ringB[n]); tri(ringA[k], ringB[n], ringA[n]);
            tri(ringB[k], tip, ringB[n]);
            tri(ringA[k], ringA[n], bottom);
        }
    };
    var c0 = bb.center; float w = UnityEngine.Mathf.Max(bb.extents.x, bb.extents.z); float h = bb.size.y;
    // main block: encloses the body
    prism(new UnityEngine.Vector3(c0.x, bb.min.y - 0.05f, c0.z), new UnityEngine.Vector3(R(-0.08f, 0.08f), 1f, R(-0.08f, 0.08f)), w * 1.35f, h * 0.95f, h * 0.45f, 7);
    // shards
    int count = 9;
    for (int i = 0; i < count; i++) {
        float a = i * 6.2832f / count + R(-0.25f, 0.25f);
        var outDir = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(a), 0f, UnityEngine.Mathf.Sin(a));
        var basePt = new UnityEngine.Vector3(c0.x, bb.min.y, c0.z) + outDir * w * R(0.9f, 1.4f);
        var axis = (UnityEngine.Vector3.up * R(1.2f, 2.2f) + outDir).normalized;
        float sh = h * R(0.25f, 0.65f);
        prism(basePt, axis, w * R(0.18f, 0.32f), sh, sh * R(0.3f, 0.6f), rnd.Next(4, 7));
    }
    var m = new UnityEngine.Mesh { name = "FrostSwatch_Crystal", hideFlags = UnityEngine.HideFlags.DontSave };
    m.SetVertices(pos);
    m.SetTriangles(tris, 0);
    m.RecalculateNormals();
    // per-face planar UVs (dominant axis), ~0.6 m per tile; uv.zw = 0 so particle-style dissolve stays off
    var nrm = m.normals; var uv = new System.Collections.Generic.List<UnityEngine.Vector4>(); var col = new UnityEngine.Color[pos.Count];
    for (int i = 0; i < pos.Count; i++) {
        var n = nrm[i]; var p = pos[i]; UnityEngine.Vector2 u;
        float ax = UnityEngine.Mathf.Abs(n.x), ay = UnityEngine.Mathf.Abs(n.y), az = UnityEngine.Mathf.Abs(n.z);
        if (ax >= ay && ax >= az) u = new UnityEngine.Vector2(p.z, p.y); else if (ay >= az) u = new UnityEngine.Vector2(p.x, p.z); else u = new UnityEngine.Vector2(p.x, p.y);
        u /= 0.6f * h;
        uv.Add(new UnityEngine.Vector4(u.x, u.y, 0f, 0f));
        col[i] = UnityEngine.Color.white;
    }
    m.SetUVs(0, uv); m.colors = col; m.RecalculateBounds(); m.RecalculateTangents();
    return m;
};
var crystal = buildCrystal(7);

// ---- v2 materials (all UberFXSG; no opaque-texture dependence)
System.Func<string, UnityEngine.Material> loadMat = p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
System.Func<string, UnityEngine.Texture2D> loadTex = p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(p);
var crystalTex = loadTex("Assets/VFX/Shared/Piloto/Textures/CrystalNoise.png");
var noiseTex = loadTex("Assets/VFX/Shared/Piloto/Textures/Tx_Noise_20.png");
var veinTex = loadTex("Assets/VFX/Shared/Piloto/Textures/Noise_TilingSoftWebbing.png");
var flareTex = loadTex("Assets/Plugins/Piloto Studio/Textures/Flare_Cross.png");
var alphaSrc = loadMat("Assets/VFX/Shared/Piloto/Materials/Smoke_screen_cloud_sharp_fb_1_alpha_soft.mat");
var addSrc = loadMat("Assets/VFX/Shared/Piloto/Materials/Ring_Halo_faded_add_soft.mat");
if (crystalTex == null || veinTex == null || flareTex == null || alphaSrc == null || addSrc == null) return "FAILED: missing texture or source material";

System.Func<UnityEngine.Material, string, UnityEngine.Texture2D, float, int, UnityEngine.Material> fx = (src, name, tex, tiling, queue) => {
    var m = new UnityEngine.Material(src) { name = name, hideFlags = UnityEngine.HideFlags.DontSave };
    m.SetTexture("_MainTex", tex);
    m.SetTextureScale("_MainTex", new UnityEngine.Vector2(tiling, tiling)); m.SetTextureOffset("_MainTex", UnityEngine.Vector2.zero);
    m.SetVector("_MainTexturePanning", UnityEngine.Vector4.zero);
    m.SetVector("_MainTextureChannel", new UnityEngine.Vector4(1, 1, 1, 0));
    m.SetVector("_MainAlphaChannel", new UnityEngine.Vector4(1, 0, 0, 0));
    m.EnableKeyword("_USERAMP"); m.SetFloat("_USERAMP", 1f);
    m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 0f); m.SetFloat("_BUILTIN_AlphaClip", 0f);
    m.EnableKeyword("_DISABLEEROSION"); m.SetFloat("_DISABLEEROSION", 1f);
    m.DisableKeyword("_USEALPHAOVERRIDE"); m.SetFloat("_USEALPHAOVERRIDE", 0f);
    m.DisableKeyword("_USESOFTALPHA"); m.SetFloat("_USESOFTALPHA", 0f);
    m.EnableKeyword("_FRESNEL"); m.SetFloat("_FRESNEL", 1f);
    m.SetTexture("_DetailNoise", noiseTex);
    m.SetVector("_DetailDistortionChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailMultiplyChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailAdditiveChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailVertexOffsetChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailNoisePanning", UnityEngine.Vector4.zero);
    m.SetFloat("_Desaturate", 0f);
    m.renderQueue = queue;
    return m;
};
System.Action<UnityEngine.Material, UnityEngine.Color, UnityEngine.Color, UnityEngine.Color, float, float> ramp = (m, lo, mid, hi, pLo, pHi) => {
    m.SetColor("_LastColor", lo); m.SetColor("_MidColor", mid); m.SetColor("_WhiteColor", hi);
    m.SetFloat("_MiddlePointPos1", pLo); m.SetFloat("_MiddlePointPos", pHi);
};
System.Action<UnityEngine.Material, UnityEngine.Color, float, float, bool> fres = (m, c, scale, power, invert) => {
    m.SetColor("_FresnelColor", c); m.SetFloat("_FresnelScale", scale); m.SetFloat("_FresnelPower", power); m.SetFloat("_Invert_Fresnel", invert ? 1f : 0f);
};
// v1 = the swatch the user picked, unchanged
var v1 = fx(alphaSrc, "FrostSwatch_v1", crystalTex, 0.45f, 3000);
ramp(v1, new UnityEngine.Color(0.10f, 0.12f, 0.14f, 0.35f), new UnityEngine.Color(0.62f, 0.68f, 0.72f, 0.7f), new UnityEngine.Color(1.6f, 1.65f, 1.7f, 0.9f), 0.35f, 0.7f);
fres(v1, new UnityEngine.Color(1.4f, 1.45f, 1.5f, 1f), 1.6f, 2.5f, false);
v1.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.08f, 0, 0, 0));
v1.SetVector("_DetailNoisePanning", new UnityEngine.Vector4(0.02f, 0.03f, 0, 0));

// v2 layers, back to front
// a) inner walls: the crystal turned inside out, darker cool grey, offset texture -> parallax against the outer facets
var back = fx(alphaSrc, "FrostSwatch_v2_Back", crystalTex, 0.3f, 3000);
back.SetTextureOffset("_MainTex", new UnityEngine.Vector2(0.37f, 0.61f));
ramp(back, new UnityEngine.Color(0.16f, 0.19f, 0.22f, 0.30f), new UnityEngine.Color(0.48f, 0.55f, 0.60f, 0.45f), new UnityEngine.Color(1.1f, 1.15f, 1.2f, 0.65f), 0.4f, 0.8f);
fres(back, new UnityEngine.Color(0.5f, 0.55f, 0.6f, 1f), 0.8f, 3f, false);
// b) internal fractures: inner shell with white vein lines, slight UV wobble
var veins = fx(alphaSrc, "FrostSwatch_v2_Veins", veinTex, 1.4f, 3001);
ramp(veins, new UnityEngine.Color(0f, 0f, 0f, 0f), new UnityEngine.Color(0.7f, 0.75f, 0.8f, 0.15f), new UnityEngine.Color(1.5f, 1.55f, 1.6f, 0.85f), 0.55f, 0.82f);
fres(veins, new UnityEngine.Color(0f, 0f, 0f, 1f), 0f, 1f, false);
veins.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.12f, 0, 0, 0));
// c) core glow (additive): inverted fresnel = bright towards the middle, low intensity
var core = fx(addSrc, "FrostSwatch_v2_Core", UnityEngine.Texture2D.whiteTexture, 1f, 3002);
ramp(core, new UnityEngine.Color(0.06f, 0.065f, 0.07f, 1f), new UnityEngine.Color(0.06f, 0.065f, 0.07f, 1f), new UnityEngine.Color(0.06f, 0.065f, 0.07f, 1f), 0.4f, 0.8f);
fres(core, new UnityEngine.Color(0.45f, 0.47f, 0.5f, 1f), 1.0f, 1.5f, true);
// d) outer surface: larger facets, lower body alpha so the inside shows, stronger HDR rim (bloom catches it)
var surf = fx(alphaSrc, "FrostSwatch_v2_Surface", crystalTex, 0.3f, 3003);
ramp(surf, new UnityEngine.Color(0.12f, 0.14f, 0.16f, 0.12f), new UnityEngine.Color(0.70f, 0.75f, 0.80f, 0.35f), new UnityEngine.Color(1.8f, 1.85f, 1.9f, 0.8f), 0.45f, 0.78f);
fres(surf, new UnityEngine.Color(2.2f, 2.25f, 2.3f, 1f), 1.8f, 3.5f, false);
surf.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.05f, 0, 0, 0));
// v2+ : same layers, stronger glow and fractures
var veinsPlus = new UnityEngine.Material(veins) { name = "FrostSwatch_v2p_Veins", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(veinsPlus, new UnityEngine.Color(0f, 0f, 0f, 0f), new UnityEngine.Color(1.0f, 1.05f, 1.1f, 0.45f), new UnityEngine.Color(2.6f, 2.65f, 2.7f, 1f), 0.4f, 0.65f);
var corePlus = new UnityEngine.Material(core) { name = "FrostSwatch_v2p_Core", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(corePlus, new UnityEngine.Color(0.14f, 0.15f, 0.16f, 1f), new UnityEngine.Color(0.14f, 0.15f, 0.16f, 1f), new UnityEngine.Color(0.14f, 0.15f, 0.16f, 1f), 0.4f, 0.8f);
fres(corePlus, new UnityEngine.Color(1.1f, 1.15f, 1.2f, 1f), 1.3f, 1.2f, true);
// v1.5 : between v1 and v2 (user pick 2026-11-06): same layers as v2, inside quieter, glow weaker
var backMid = new UnityEngine.Material(back) { name = "FrostSwatch_v15_Back", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(backMid, new UnityEngine.Color(0.16f, 0.19f, 0.22f, 0.18f), new UnityEngine.Color(0.48f, 0.55f, 0.60f, 0.30f), new UnityEngine.Color(1.1f, 1.15f, 1.2f, 0.45f), 0.4f, 0.8f);
var veinsMid = new UnityEngine.Material(veins) { name = "FrostSwatch_v15_Veins", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(veinsMid, new UnityEngine.Color(0f, 0f, 0f, 0f), new UnityEngine.Color(0.7f, 0.75f, 0.8f, 0.08f), new UnityEngine.Color(1.4f, 1.45f, 1.5f, 0.5f), 0.55f, 0.82f);
var coreMid = new UnityEngine.Material(core) { name = "FrostSwatch_v15_Core", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(coreMid, new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), 0.4f, 0.8f);
fres(coreMid, new UnityEngine.Color(0.22f, 0.23f, 0.25f, 1f), 1.0f, 1.5f, true);
var surfMid = new UnityEngine.Material(surf) { name = "FrostSwatch_v15_Surface", hideFlags = UnityEngine.HideFlags.DontSave };
ramp(surfMid, new UnityEngine.Color(0.12f, 0.14f, 0.16f, 0.15f), new UnityEngine.Color(0.70f, 0.75f, 0.80f, 0.40f), new UnityEngine.Color(1.8f, 1.85f, 1.9f, 0.85f), 0.45f, 0.78f);
fres(surfMid, new UnityEngine.Color(2.0f, 2.05f, 2.1f, 1f), 1.8f, 3.5f, false);
// e) glints (additive quads facing the camera at capture time)
var glint = new UnityEngine.Material(addSrc) { name = "FrostSwatch_v2_Glint", hideFlags = UnityEngine.HideFlags.DontSave };
glint.SetTexture("_MainTex", flareTex);
glint.DisableKeyword("_USERAMP"); glint.SetFloat("_USERAMP", 0f); glint.DisableKeyword("_FRESNEL"); glint.SetFloat("_FRESNEL", 0f);
glint.DisableKeyword("_USESOFTALPHA"); glint.SetFloat("_USESOFTALPHA", 0f);
glint.EnableKeyword("_DISABLEEROSION"); glint.SetFloat("_DISABLEEROSION", 1f);
glint.SetVector("_MainTextureChannel", new UnityEngine.Vector4(1, 1, 1, 0));
glint.SetVector("_MainAlphaChannel", new UnityEngine.Vector4(1, 0, 0, 0));
glint.SetTextureScale("_MainTex", UnityEngine.Vector2.one); glint.SetTextureOffset("_MainTex", UnityEngine.Vector2.zero);
glint.SetVector("_MainTexturePanning", UnityEngine.Vector4.zero);
glint.renderQueue = 3004;

// inside-out copy of the crystal
var inside = UnityEngine.Object.Instantiate(crystal); inside.name = "FrostSwatch_CrystalBack"; inside.hideFlags = UnityEngine.HideFlags.DontSave;
var tri2 = inside.triangles;
for (int t = 0; t < tri2.Length; t += 3) { int tmp = tri2[t + 1]; tri2[t + 1] = tri2[t + 2]; tri2[t + 2] = tmp; }
inside.triangles = tri2; inside.RecalculateNormals();

// ---- Place v1, v2, v2+ side by side (16 m apart) at the start grid
var view = BuddahGo.Rendering.Editor.RaceMapViewpointCapture.Viewpoints[0];
var yawRot = UnityEngine.Quaternion.Euler(0f, view.PlayerYaw, 0f);
var right = yawRot * UnityEngine.Vector3.right;
int mask = UnityEngine.Physics.DefaultRaycastLayers & ~UnityEngine.LayerMask.GetMask("Buddah", "TrackEdge", "RollbackBoundingBox");
int vfxLayer = UnityEngine.LayerMask.NameToLayer("Buddah");
var bodyCentre = bb.center;
System.Func<UnityEngine.Transform, string, UnityEngine.Mesh, UnityEngine.Material, float, UnityEngine.GameObject> shell = (parent, name, mesh, mat, scale) => {
    var s = new UnityEngine.GameObject(name); s.hideFlags = UnityEngine.HideFlags.DontSave; s.layer = vfxLayer;
    s.transform.SetParent(parent, false);
    s.transform.localScale = UnityEngine.Vector3.one * scale;
    s.transform.localPosition = bodyCentre * (1f - scale);   // scale about the body centre
    s.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
    var r = s.AddComponent<UnityEngine.MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    return s;
};
var sb = new System.Text.StringBuilder();
var swatchNames = new string[] { "FrostSwatch_v1", "FrostSwatch_v15", "FrostSwatch_v2" };
for (int i = 0; i < 3; i++) {
    var p = view.PlayerPosition + right * (i - 1) * 16f;
    float ground = p.y; UnityEngine.RaycastHit hit;
    if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(p.x, p.y + 30f, p.z), UnityEngine.Vector3.down, out hit, 80f, mask, UnityEngine.QueryTriggerInteraction.Ignore)) ground = hit.point.y;
    var go = new UnityEngine.GameObject(swatchNames[i]) { hideFlags = UnityEngine.HideFlags.DontSave };
    go.transform.SetPositionAndRotation(new UnityEngine.Vector3(p.x, ground + clearance, p.z), yawRot);
    var b = new UnityEngine.GameObject("Body"); b.hideFlags = UnityEngine.HideFlags.DontSave; b.layer = vfxLayer; b.transform.SetParent(go.transform, false);
    b.AddComponent<UnityEngine.MeshFilter>().sharedMesh = body;
    b.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = bodyMat;
    if (i == 0) {
        shell(go.transform, "Ice", crystal, v1, 1f);
    } else {
        bool m15 = i == 1;
        shell(go.transform, "IceBack", inside, m15 ? backMid : back, 1f);
        shell(go.transform, "IceVeins", crystal, m15 ? veinsMid : veins, 0.9f);
        shell(go.transform, "IceCore", crystal, m15 ? coreMid : core, 0.8f);
        shell(go.transform, "IceSurface", crystal, m15 ? surfMid : surf, 1f);
        // glints on the highest crystal vertices
        var verts = crystal.vertices;
        var picked = new System.Collections.Generic.List<UnityEngine.Vector3>();
        var order = new System.Collections.Generic.List<int>();
        for (int k = 0; k < verts.Length; k++) order.Add(k);
        order.Sort((x, y) => verts[y].y.CompareTo(verts[x].y));
        foreach (int k in order) {
            bool near = false;
            foreach (var q in picked) if ((q - verts[k]).sqrMagnitude < 9f) { near = true; break; }
            if (near) continue;
            picked.Add(verts[k]);
            if (picked.Count >= 6) break;
        }
        int gi = 0;
        foreach (var v in picked) {
            var g = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<UnityEngine.Collider>());
            g.name = "IceGlint" + (gi++); g.hideFlags = UnityEngine.HideFlags.DontSave; g.layer = vfxLayer;
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = v; g.transform.localScale = UnityEngine.Vector3.one * (gi % 2 == 0 ? 1.6f : 1.1f);
            var gr = g.GetComponent<UnityEngine.MeshRenderer>(); gr.sharedMaterial = glint; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    sb.Append(go.name).Append(" @ ").Append(go.transform.position.ToString("F1")).Append("\n");
}
sb.Append("sceneDirty=").Append(scene.isDirty);
return sb.ToString();
