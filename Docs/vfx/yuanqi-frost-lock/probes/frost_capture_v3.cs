// Captures the FrostSwatch_* stand-ins: gameplay view, close-ups, and an orbit sequence (per-swatch frames).
// Output: OUT dir PNGs. Camera copies Camera.main (post, AA, volume mask) like RaceMapDuskColorStudy.
const string OUT = "C:/Users/dwh88/AppData/Local/Temp/claude/8c2c1692109b2b8d/fe9b93cc-ec71-4261-a832-8d4d98536bbc/scratchpad/frost_frames_v3";
const int ORBIT_FRAMES = 36;
System.IO.Directory.CreateDirectory(OUT);
var main = UnityEngine.Camera.main;
if (main == null) return "FAILED: no MainCamera";
var swatches = new System.Collections.Generic.List<UnityEngine.GameObject>();
foreach (var n in new[] { "FrostSwatch_v1", "FrostSwatch_v15", "FrostSwatch_v2" }) {
    UnityEngine.GameObject found = null;
    foreach (var g in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>()) if (g.name == n && g.scene.IsValid()) { found = g; break; }
    if (found == null) return "FAILED: missing " + n;
    swatches.Add(found);
}
var camGo = new UnityEngine.GameObject("FrostSwatchCamera") { hideFlags = UnityEngine.HideFlags.DontSave };
var report = new System.Text.StringBuilder();
try {
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.CopyFrom(main); cam.enabled = false;
    var src = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    if (src != null) {
        data.renderPostProcessing = src.renderPostProcessing; data.antialiasing = src.antialiasing; data.antialiasingQuality = src.antialiasingQuality;
        data.dithering = src.dithering; data.renderShadows = src.renderShadows; data.volumeLayerMask = src.volumeLayerMask; data.volumeTrigger = null;
        data.requiresColorOption = src.requiresColorOption; data.requiresDepthOption = src.requiresDepthOption;
    }
    System.Action<string, int, int> render = (path, w, h) => {
        var target = UnityEngine.RenderTexture.GetTemporary(new UnityEngine.RenderTextureDescriptor(w, h, UnityEngine.RenderTextureFormat.DefaultHDR, 24) { msaaSamples = 4 });
        var readback = UnityEngine.RenderTexture.GetTemporary(new UnityEngine.RenderTextureDescriptor(w, h, UnityEngine.RenderTextureFormat.ARGB32, 0) { sRGB = true });
        var prev = UnityEngine.RenderTexture.active;
        var tex = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGB24, false);
        try {
            foreach (var sw in swatches) foreach (var tr in sw.GetComponentsInChildren<UnityEngine.Transform>(true)) if (tr.name.StartsWith("IceGlint")) tr.rotation = UnityEngine.Quaternion.LookRotation(tr.position - cam.transform.position);
            cam.targetTexture = target; UnityEngine.Rendering.Universal.CameraExtensions.UpdateVolumeStack(cam);
            for (int i = 0; i < 3; i++) cam.Render();
            UnityEngine.Graphics.Blit(target, readback);
            UnityEngine.RenderTexture.active = readback;
            tex.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        } finally {
            UnityEngine.RenderTexture.active = prev; cam.targetTexture = null;
            UnityEngine.RenderTexture.ReleaseTemporary(target); UnityEngine.RenderTexture.ReleaseTemporary(readback);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    };
    // 1) gameplay camera (same as RaceMapViewpointCapture 01_start_grid), centred on the middle swatch
    var view = BuddahGo.Rendering.Editor.RaceMapViewpointCapture.Viewpoints[0];
    view.PlayerPosition = swatches[1].transform.position;
    BuddahGo.Rendering.Editor.RaceMapViewpointCapture.PlaceCamera(cam, view);
    render(OUT + "/gameplay.png", 1920, 1080);
    report.Append("gameplay ok\n");
    // 2) near camera: all three in one frame, the distance a chase view would get at most
    var mid = swatches[1].transform.position;
    var fwd = swatches[1].transform.forward;
    cam.fieldOfView = 35f;
    cam.transform.position = mid + fwd * 52f + UnityEngine.Vector3.up * 18f;
    cam.transform.LookAt(mid + UnityEngine.Vector3.up * 3.5f);
    render(OUT + "/near.png", 1920, 1080);
    // 3) orbit: per swatch, 36 frames around it (elevation 15 deg, 4.2 m)
    cam.fieldOfView = 32f;
    for (int s = 0; s < swatches.Count; s++) {
        var c = swatches[s].transform.position + UnityEngine.Vector3.up * 3.6f;
        for (int f = 0; f < ORBIT_FRAMES; f++) {
            float a = f * 360f / ORBIT_FRAMES;
            var dir = UnityEngine.Quaternion.Euler(15f, a, 0f) * UnityEngine.Vector3.back;
            cam.transform.position = c + dir * 24f;
            cam.transform.LookAt(c);
            // hide the neighbours so they do not cross the frame
            for (int o = 0; o < swatches.Count; o++) swatches[o].SetActive(o == s);
            render(OUT + "/orbit_" + (s + 1) + "_" + f.ToString("00") + ".png", 720, 720);
        }
    }
    for (int o = 0; o < swatches.Count; o++) swatches[o].SetActive(true);
    report.Append("orbit ok\n");
} finally {
    UnityEngine.Object.DestroyImmediate(camGo);
}
report.Append("sceneDirty=" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty);
return report.ToString();
