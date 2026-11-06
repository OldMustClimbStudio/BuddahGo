// Frost-lock ice swatches: three DontSave stand-ins (Buddha proxy + faceted crystal shell), one material each.
// Nothing is saved: objects are HideFlags.DontSave in the open RaceMap; materials/meshes are in-memory.
// C# 6 (CodeDom). Re-run safe: deletes the previous FrostSwatch_* objects first.
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/RaceMap.unity") return "FAILED: active scene is " + scene.path;
if (scene.isDirty) return "FAILED: RaceMap has unsaved changes; not touching it";
foreach (var old in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if (old != null && old.name.StartsWith("FrostSwatch_") && old.scene.IsValid()) UnityEngine.Object.DestroyImmediate(old);

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

// ---- Materials
System.Func<string, UnityEngine.Material> loadMat = p => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
var crystalTex = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/VFX/Shared/Piloto/Textures/CrystalNoise.png");
var noiseTex = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/VFX/Shared/Piloto/Textures/Tx_Noise_20.png");
var alphaSrc = loadMat("Assets/VFX/Shared/Piloto/Materials/Smoke_screen_cloud_sharp_fb_1_alpha_soft.mat");
if (crystalTex == null || alphaSrc == null) return "FAILED: missing CrystalNoise or alpha source material";

System.Func<UnityEngine.Material, string, UnityEngine.Material> fx = (src, name) => {
    var m = new UnityEngine.Material(src) { name = name, hideFlags = UnityEngine.HideFlags.DontSave };
    m.SetTexture("_MainTex", crystalTex);
    m.SetTextureScale("_MainTex", new UnityEngine.Vector2(0.45f, 0.45f)); m.SetTextureOffset("_MainTex", UnityEngine.Vector2.zero);
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
    m.SetVector("_DetailDistortionChannel", new UnityEngine.Vector4(0.08f, 0, 0, 0));
    m.SetVector("_DetailMultiplyChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailAdditiveChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailVertexOffsetChannel", UnityEngine.Vector4.zero);
    m.SetVector("_DetailNoisePanning", new UnityEngine.Vector4(0.02f, 0.03f, 0, 0));
    m.SetFloat("_Desaturate", 0f);
    return m;
};
// 1) FX ice: light-on-ink language, white highs, cool grey mids, white rim
var m1 = fx(alphaSrc, "FrostSwatch_1_FxIce");
m1.SetColor("_LastColor", new UnityEngine.Color(0.10f, 0.12f, 0.14f, 0.35f));
m1.SetColor("_MidColor", new UnityEngine.Color(0.62f, 0.68f, 0.72f, 0.7f));
m1.SetColor("_WhiteColor", new UnityEngine.Color(1.6f, 1.65f, 1.7f, 0.9f));
m1.SetFloat("_MiddlePointPos1", 0.35f); m1.SetFloat("_MiddlePointPos", 0.7f);
m1.SetColor("_FresnelColor", new UnityEngine.Color(1.4f, 1.45f, 1.5f, 1f));
m1.SetFloat("_FresnelScale", 1.6f); m1.SetFloat("_FresnelPower", 2.5f); m1.SetFloat("_Invert_Fresnel", 0f);

// 3) Ink ice: ink shell, light from inside (inverted fresnel = bright core, dark edges), white facet seams
var m3 = fx(alphaSrc, "FrostSwatch_3_InkIce");
m3.SetColor("_LastColor", new UnityEngine.Color(0f, 0f, 0f, 0.92f));
m3.SetColor("_MidColor", new UnityEngine.Color(0.08f, 0.08f, 0.09f, 0.85f));
m3.SetColor("_WhiteColor", new UnityEngine.Color(1.8f, 1.8f, 1.8f, 1f));
m3.SetFloat("_MiddlePointPos1", 0.8f); m3.SetFloat("_MiddlePointPos", 0.97f);
m3.SetColor("_FresnelColor", new UnityEngine.Color(0.3f, 0.3f, 0.32f, 1f));
m3.SetFloat("_FresnelScale", 0.7f); m3.SetFloat("_FresnelPower", 2.5f); m3.SetFloat("_Invert_Fresnel", 1f);

// 2) Lit ice: Shader Graph sample (URP Lit, 3-layer parallax + fresnel), neutral white tint (no blue)
var iceSrc = loadMat("Assets/_FrostSwatchLocal/Ice/Ice Shader.mat");
UnityEngine.Material m2 = null;
if (iceSrc != null) {
    m2 = new UnityEngine.Material(iceSrc) { name = "FrostSwatch_2_LitIce", hideFlags = UnityEngine.HideFlags.DontSave };
    if (m2.HasProperty("_Ice_Color")) m2.SetColor("_Ice_Color", new UnityEngine.Color(0.82f, 0.86f, 0.88f, 1f));
    if (m2.HasProperty("_Inner_Color")) m2.SetColor("_Inner_Color", new UnityEngine.Color(0.95f, 0.97f, 1f, 1f));
    if (m2.HasProperty("_Refraction_Color")) m2.SetColor("_Refraction_Color", new UnityEngine.Color(0.9f, 0.93f, 0.95f, 1f));
}

// ---- Place three stand-ins across the start grid, lateral 4 m apart
var view = BuddahGo.Rendering.Editor.RaceMapViewpointCapture.Viewpoints[0];
var yawRot = UnityEngine.Quaternion.Euler(0f, view.PlayerYaw, 0f);
var right = yawRot * UnityEngine.Vector3.right;
var mats = new UnityEngine.Material[] { m1, m2, m3 };
var names = new string[] { "FrostSwatch_1_FxIce", "FrostSwatch_2_LitIce", "FrostSwatch_3_InkIce" };
int mask = UnityEngine.Physics.DefaultRaycastLayers & ~UnityEngine.LayerMask.GetMask("Buddah", "TrackEdge", "RollbackBoundingBox");
var sb = new System.Text.StringBuilder();
for (int i = 0; i < 3; i++) {
    var p = view.PlayerPosition + right * (i - 1) * 16f;
    float ground = p.y; UnityEngine.RaycastHit hit;
    if (UnityEngine.Physics.Raycast(new UnityEngine.Vector3(p.x, p.y + 30f, p.z), UnityEngine.Vector3.down, out hit, 80f, mask, UnityEngine.QueryTriggerInteraction.Ignore)) ground = hit.point.y;
    var go = new UnityEngine.GameObject(names[i]) { hideFlags = UnityEngine.HideFlags.DontSave };
    go.transform.SetPositionAndRotation(new UnityEngine.Vector3(p.x, ground + clearance, p.z), yawRot);
    var b = new UnityEngine.GameObject("Body"); b.hideFlags = UnityEngine.HideFlags.DontSave; b.transform.SetParent(go.transform, false);
    b.layer = UnityEngine.LayerMask.NameToLayer("Buddah");
    b.AddComponent<UnityEngine.MeshFilter>().sharedMesh = body;
    b.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = bodyMat;
    if (mats[i] != null) {
        var s = new UnityEngine.GameObject("Ice"); s.hideFlags = UnityEngine.HideFlags.DontSave; s.transform.SetParent(go.transform, false);
        s.layer = UnityEngine.LayerMask.NameToLayer("Buddah"); // skill VFX slots live on layer 6, drawn by RenderBuddah / RenderSkillVfx
        s.AddComponent<UnityEngine.MeshFilter>().sharedMesh = crystal;
        var r = s.AddComponent<UnityEngine.MeshRenderer>(); r.sharedMaterial = mats[i];
        r.shadowCastingMode = i == 1 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    sb.Append(names[i]).Append(" @ ").Append(go.transform.position.ToString("F1")).Append(" mat=").Append(mats[i] == null ? "MISSING" : mats[i].shader.name).Append("\n");
}
sb.Append("m1 kw=").Append(string.Join(",", m1.shaderKeywords)).Append(" ramp=").Append(m1.IsKeywordEnabled("_USERAMP")).Append(" lk=");
foreach (var k in m1.shader.keywordSpace.keywordNames) sb.Append(k).Append(";");
sb.Append(" | ");
sb.Append("body bounds ").Append(bb.size.ToString("F2")).Append(" clearance ").Append(clearance.ToString("F2")).Append(" sceneDirty=").Append(scene.isDirty);
return sb.ToString();
