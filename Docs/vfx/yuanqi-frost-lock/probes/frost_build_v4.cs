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

// ---- v4 preview: fix the "blurry" ice. Compared side by side: v1.5 | v4 (shaded facets, no blur) | v4 + edge lines
System.Func<string, UnityEngine.Material> loadMat = p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
System.Func<string, UnityEngine.Texture2D> loadTex = p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(p);
var crystalTex = loadTex("Assets/VFX/Shared/Piloto/Textures/CrystalNoise.png");
var noiseTex = loadTex("Assets/VFX/Shared/Piloto/Textures/Tx_Noise_20.png");
var veinTex = loadTex("Assets/VFX/Shared/Piloto/Textures/Noise_TilingSoftWebbing.png");
var crackTex = loadTex("Assets/Piloto Studio/Textures/GroundCrack_RadialPostExplody.png");   // pack copy (gitignored), crisp crack lines
var flareTex = loadTex("Assets/Plugins/Piloto Studio/Textures/Flare_Cross.png");
var alphaSrc = loadMat("Assets/VFX/Shared/Piloto/Materials/Smoke_screen_cloud_sharp_fb_1_alpha_soft.mat");
var addSrc = loadMat("Assets/VFX/Shared/Piloto/Materials/Ring_Halo_faded_add_soft.mat");
if (crystalTex == null || veinTex == null || flareTex == null || alphaSrc == null || addSrc == null) return "FAILED: missing texture or source material";
if (crackTex == null) crackTex = veinTex;

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

// ---- v1.5 (the committed pick), rebuilt exactly as frost_build_v3.cs
var back = fx(alphaSrc, "FrostSwatch_v15_Back", crystalTex, 0.3f, 3000);
back.SetTextureOffset("_MainTex", new UnityEngine.Vector2(0.37f, 0.61f));
ramp(back, new UnityEngine.Color(0.16f, 0.19f, 0.22f, 0.18f), new UnityEngine.Color(0.48f, 0.55f, 0.60f, 0.30f), new UnityEngine.Color(1.1f, 1.15f, 1.2f, 0.45f), 0.4f, 0.8f);
fres(back, new UnityEngine.Color(0.5f, 0.55f, 0.6f, 1f), 0.8f, 3f, false);
var veins = fx(alphaSrc, "FrostSwatch_v15_Veins", veinTex, 1.4f, 3001);
ramp(veins, new UnityEngine.Color(0f, 0f, 0f, 0f), new UnityEngine.Color(0.7f, 0.75f, 0.8f, 0.08f), new UnityEngine.Color(1.4f, 1.45f, 1.5f, 0.5f), 0.55f, 0.82f);
fres(veins, new UnityEngine.Color(0f, 0f, 0f, 1f), 0f, 1f, false);
veins.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.12f, 0, 0, 0));
var core = fx(addSrc, "FrostSwatch_v15_Core", UnityEngine.Texture2D.whiteTexture, 1f, 3002);
ramp(core, new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), new UnityEngine.Color(0.03f, 0.032f, 0.035f, 1f), 0.4f, 0.8f);
fres(core, new UnityEngine.Color(0.22f, 0.23f, 0.25f, 1f), 1.0f, 1.5f, true);
var surf = fx(alphaSrc, "FrostSwatch_v15_Surface", crystalTex, 0.3f, 3003);
ramp(surf, new UnityEngine.Color(0.12f, 0.14f, 0.16f, 0.15f), new UnityEngine.Color(0.70f, 0.75f, 0.80f, 0.40f), new UnityEngine.Color(1.8f, 1.85f, 1.9f, 0.85f), 0.45f, 0.78f);
fres(surf, new UnityEngine.Color(2.0f, 2.05f, 2.1f, 1f), 1.8f, 3.5f, false);
surf.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.05f, 0, 0, 0));

// ---- v4: same five layers, made crisp
// surface: per-face brightness comes from vertex colour (shaded mesh below); texture only adds a faint facet grain,
// ramp nearly flat so a face reads as one tone; no UV wobble; rim kept <= ~1.3 so bloom does not smear the faces
var surf4 = fx(alphaSrc, "FrostSwatch_v4_Surface", crystalTex, 0.3f, 3003);
ramp(surf4, new UnityEngine.Color(0.80f, 0.84f, 0.88f, 0.48f), new UnityEngine.Color(0.88f, 0.91f, 0.94f, 0.58f), new UnityEngine.Color(1.0f, 1.0f, 1.0f, 0.7f), 0.45f, 0.85f);
fres(surf4, new UnityEngine.Color(1.3f, 1.32f, 1.35f, 1f), 1.2f, 4f, false);
// inner walls: same idea, darker and thinner
var back4 = fx(alphaSrc, "FrostSwatch_v4_Back", crystalTex, 0.3f, 3000);
back4.SetTextureOffset("_MainTex", new UnityEngine.Vector2(0.37f, 0.61f));
ramp(back4, new UnityEngine.Color(0.45f, 0.5f, 0.55f, 0.18f), new UnityEngine.Color(0.55f, 0.6f, 0.65f, 0.24f), new UnityEngine.Color(0.7f, 0.75f, 0.8f, 0.3f), 0.45f, 0.85f);
fres(back4, new UnityEngine.Color(0f, 0f, 0f, 1f), 0f, 1f, false);
// fractures: crisp crack lines instead of the soft webbing, no wobble
var veins4 = fx(alphaSrc, "FrostSwatch_v4_Cracks", crackTex, 0.35f, 3001);
ramp(veins4, new UnityEngine.Color(0f, 0f, 0f, 0f), new UnityEngine.Color(0.9f, 0.92f, 0.95f, 0f), new UnityEngine.Color(1.1f, 1.12f, 1.15f, 0.5f), 0.7f, 0.88f);
fres(veins4, new UnityEngine.Color(0f, 0f, 0f, 1f), 0f, 1f, false);
// core glow: no base fill, only the centre
var core4 = fx(addSrc, "FrostSwatch_v4_Core", UnityEngine.Texture2D.whiteTexture, 1f, 3002);
ramp(core4, UnityEngine.Color.black, UnityEngine.Color.black, UnityEngine.Color.black, 0.4f, 0.8f);
fres(core4, new UnityEngine.Color(0.2f, 0.21f, 0.22f, 1f), 1.0f, 2.2f, true);
// edge lines (additive white strips along every crystal edge)
var edgeMat = fx(addSrc, "FrostSwatch_v4_Edges", UnityEngine.Texture2D.whiteTexture, 1f, 3004);
ramp(edgeMat, new UnityEngine.Color(0.55f, 0.57f, 0.6f, 0.6f), new UnityEngine.Color(0.55f, 0.57f, 0.6f, 0.6f), new UnityEngine.Color(0.55f, 0.57f, 0.6f, 0.6f), 0.4f, 0.8f);
fres(edgeMat, UnityEngine.Color.black, 0f, 1f, false);
// glints
var glint = new UnityEngine.Material(addSrc) { name = "FrostSwatch_Glint", hideFlags = UnityEngine.HideFlags.DontSave };
glint.SetTexture("_MainTex", flareTex);
glint.DisableKeyword("_USERAMP"); glint.SetFloat("_USERAMP", 0f); glint.DisableKeyword("_FRESNEL"); glint.SetFloat("_FRESNEL", 0f);
glint.DisableKeyword("_USESOFTALPHA"); glint.SetFloat("_USESOFTALPHA", 0f);
glint.EnableKeyword("_DISABLEEROSION"); glint.SetFloat("_DISABLEEROSION", 1f);
glint.SetVector("_MainTextureChannel", new UnityEngine.Vector4(1, 1, 1, 0));
glint.SetVector("_MainAlphaChannel", new UnityEngine.Vector4(1, 0, 0, 0));
glint.SetTextureScale("_MainTex", UnityEngine.Vector2.one); glint.SetTextureOffset("_MainTex", UnityEngine.Vector2.zero);
glint.SetVector("_MainTexturePanning", UnityEngine.Vector4.zero);
glint.renderQueue = 3005;

// ---- meshes: inside-out copy, a face-shaded copy (vertex colour from a fixed fake light), an edge-strip mesh
var inside = UnityEngine.Object.Instantiate(crystal); inside.name = "FrostSwatch_CrystalBack"; inside.hideFlags = UnityEngine.HideFlags.DontSave;
var tri2 = inside.triangles;
for (int t = 0; t < tri2.Length; t += 3) { int tmp = tri2[t + 1]; tri2[t + 1] = tri2[t + 2]; tri2[t + 2] = tmp; }
inside.triangles = tri2; inside.RecalculateNormals();

var lightDir = new UnityEngine.Vector3(-0.45f, 0.75f, 0.5f).normalized;
System.Func<UnityEngine.Mesh, float, float, UnityEngine.Mesh> shaded = (src, dark, bright) => {
    var m = UnityEngine.Object.Instantiate(src); m.hideFlags = UnityEngine.HideFlags.DontSave;
    var nn = m.normals; var cc = new UnityEngine.Color[nn.Length];
    for (int k = 0; k < nn.Length; k++) {
        float d = UnityEngine.Vector3.Dot(nn[k], lightDir) * 0.5f + 0.5f;
        // three bands: back-lit / side / lit, so every face is one flat tone
        float band = d < 0.4f ? 0f : (d < 0.72f ? 0.5f : 1f);
        float v = UnityEngine.Mathf.Lerp(dark, bright, band);
        cc[k] = new UnityEngine.Color(v, v * 1.01f, v * 1.03f, 1f);
    }
    m.colors = cc;
    return m;
};
var crystalShaded = shaded(crystal, 0.18f, 1.0f);
var insideShaded = shaded(inside, 0.35f, 0.8f);

// edges: unique edges whose two faces bend by more than ~12 deg (skip coplanar splits); each edge = two crossed thin quads
var cv = crystal.vertices; var ct = crystal.triangles; var cn = crystal.normals;
var edgeFaces = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UnityEngine.Vector3>>();
var edgeEnds = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector3[]>();
System.Func<UnityEngine.Vector3, string> keyOf = v => UnityEngine.Mathf.RoundToInt(v.x * 100f) + "," + UnityEngine.Mathf.RoundToInt(v.y * 100f) + "," + UnityEngine.Mathf.RoundToInt(v.z * 100f);
for (int t = 0; t < ct.Length; t += 3) {
    var fn = cn[ct[t]];
    for (int e = 0; e < 3; e++) {
        var a = cv[ct[t + e]]; var b = cv[ct[t + (e + 1) % 3]];
        string ka = keyOf(a), kb = keyOf(b);
        string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka;
        System.Collections.Generic.List<UnityEngine.Vector3> list;
        if (!edgeFaces.TryGetValue(key, out list)) { list = new System.Collections.Generic.List<UnityEngine.Vector3>(); edgeFaces[key] = list; edgeEnds[key] = new[] { a, b }; }
        list.Add(fn);
    }
}
var ep = new System.Collections.Generic.List<UnityEngine.Vector3>();
var et = new System.Collections.Generic.List<int>();
var euv = new System.Collections.Generic.List<UnityEngine.Vector4>();
float edgeWidth = bb.size.y * 0.0045f;
int edgeCount = 0;
foreach (var kv in edgeFaces) {
    var f = kv.Value;
    bool sharp = f.Count < 2 || UnityEngine.Vector3.Dot(f[0], f[1]) < 0.978f;
    if (!sharp) continue;
    var a = edgeEnds[kv.Key][0]; var b = edgeEnds[kv.Key][1];
    var dir = (b - a).normalized;
    var n1 = f[0]; var n2 = f.Count > 1 ? f[1] : UnityEngine.Vector3.Cross(dir, n1);
    foreach (var side0 in new[] { n1, n2 }) {
        var side = UnityEngine.Vector3.Cross(dir, side0).normalized * edgeWidth;
        int s = ep.Count;
        ep.Add(a - side); ep.Add(a + side); ep.Add(b + side); ep.Add(b - side);
        for (int q = 0; q < 4; q++) euv.Add(UnityEngine.Vector4.zero);
        et.Add(s); et.Add(s + 1); et.Add(s + 2); et.Add(s); et.Add(s + 2); et.Add(s + 3);
        et.Add(s); et.Add(s + 2); et.Add(s + 1); et.Add(s); et.Add(s + 3); et.Add(s + 2);   // both sides
    }
    edgeCount++;
}
var edgeMesh = new UnityEngine.Mesh { name = "FrostSwatch_Edges", hideFlags = UnityEngine.HideFlags.DontSave };
edgeMesh.SetVertices(ep); edgeMesh.SetTriangles(et, 0); edgeMesh.SetUVs(0, euv);
var ecol = new UnityEngine.Color[ep.Count]; for (int k = 0; k < ecol.Length; k++) ecol[k] = UnityEngine.Color.white; edgeMesh.colors = ecol;
edgeMesh.RecalculateNormals(); edgeMesh.RecalculateBounds();

// ---- place v1.5 | v4 | v4+edges, 16 m apart at the start grid
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
    s.transform.localPosition = bodyCentre * (1f - scale);
    s.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
    var r = s.AddComponent<UnityEngine.MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    return s;
};
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
var swatchNames = new string[] { "FrostSwatch_v15", "FrostSwatch_v4", "FrostSwatch_v4edges" };
var sb = new System.Text.StringBuilder();
for (int i = 0; i < 3; i++) {
    var p = view.PlayerPosition + right * (i - 1) * 16f;
    float ground = p.y; UnityEngine.RaycastHit hit;
    if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(p.x, p.y + 30f, p.z), UnityEngine.Vector3.down, out hit, 80f, mask, UnityEngine.QueryTriggerInteraction.Ignore)) ground = hit.point.y;
    var go = new UnityEngine.GameObject(swatchNames[i]) { hideFlags = UnityEngine.HideFlags.DontSave };
    go.transform.SetPositionAndRotation(new UnityEngine.Vector3(p.x, ground + clearance, p.z), yawRot);
    var bo = new UnityEngine.GameObject("Body"); bo.hideFlags = UnityEngine.HideFlags.DontSave; bo.layer = vfxLayer; bo.transform.SetParent(go.transform, false);
    bo.AddComponent<UnityEngine.MeshFilter>().sharedMesh = body;
    bo.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = bodyMat;
    if (i == 0) {
        shell(go.transform, "IceBack", inside, back, 1f);
        shell(go.transform, "IceVeins", crystal, veins, 0.9f);
        shell(go.transform, "IceCore", crystal, core, 0.8f);
        shell(go.transform, "IceSurface", crystal, surf, 1f);
    } else {
        shell(go.transform, "IceBack", insideShaded, back4, 1f);
        shell(go.transform, "IceCracks", crystal, veins4, 0.9f);
        shell(go.transform, "IceCore", crystal, core4, 0.8f);
        shell(go.transform, "IceSurface", crystalShaded, surf4, 1f);
        if (i == 2) shell(go.transform, "IceEdges", edgeMesh, edgeMat, 1f);
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
    sb.Append(go.name).Append(" @ ").Append(go.transform.position.ToString("F1")).Append("\n");
}
sb.Append("edges=").Append(edgeCount).Append(" crack=").Append(crackTex.name).Append(" sceneDirty=").Append(scene.isDirty);
return sb.ToString();
