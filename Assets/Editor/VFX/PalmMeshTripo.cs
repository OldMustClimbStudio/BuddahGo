using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Generates the palm white model for 如来神掌 through Tripo's OpenAPI v3 (Docs/vfx/palm-mesh-tripo.md).
    // The API key is read inside the editor from the MCP for Unity secure key store (provider id "tripo"); it is
    // never logged, written to disk or returned to the MCP bridge. Network work runs on a thread pool thread and
    // reports through a JSON status file; the GLB → Mesh asset step runs on the main thread (ImportGlb).
    public static class PalmMeshTripo
    {
        const string BaseUrl = "https://openapi.tripo3d.com/v3";
        const string V2Base = "https://api.tripo3d.ai/v2/openapi";
        public const string OutputDir = "Assets/VFX/如来神掌/Mesh";

        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        // Submits a multiview (or single front image) generation job. Empty view paths are skipped.
        // Writes {phase, task_id, progress, error, glb, preview} to statusPath as it goes.
        public static void Start(string statusPath, string front, string left, string back, string right,
            int faceLimit, string model, string version, bool smartLowPoly, string api)
        {
            string key;
            if (!MCPForUnity.Editor.Security.SecureKeyStore.Current.TryGet("tripo", out key) || string.IsNullOrEmpty(key))
            {
                WriteStatus(statusPath, "error", null, 0, "No Tripo key in the MCP for Unity secure key store.", null, null);
                return;
            }
            key = key.Trim();
            Directory.CreateDirectory(OutputDir);
            string glbPath = Path.GetFullPath(Path.Combine(OutputDir, "Palm_Tripo_" + version + ".glb"));
            string previewPath = Path.GetFullPath(Path.Combine(OutputDir, "Palm_Tripo_" + version + "_preview.png"));
            WriteStatus(statusPath, "starting", null, 0, null, null, null);

            Task.Run(() => Run(statusPath, key, new[] { front, left, back, right }, faceLimit, model, smartLowPoly, glbPath, previewPath, api == "v2"));
        }

        static void Run(string statusPath, string key, string[] views, int faceLimit, string model, bool smartLowPoly,
            string glbPath, string previewPath, bool v2)
        {
            string taskId = null;
            try
            {
                string[] names = { "front", "left", "back", "right" };
                var inputs = new JArray();   // v3: [{front: token}, ...]
                var files = new JArray();    // v2: [front, left, back, right] file objects, {} for a missing view
                int viewCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (string.IsNullOrEmpty(views[i])) { files.Add(new JObject()); continue; }
                    WriteStatus(statusPath, "uploading " + names[i], null, 0, null, null, null);
                    string token = Upload(key, views[i], v2);
                    inputs.Add(new JObject { [names[i]] = token });
                    files.Add(new JObject { ["type"] = "png", ["file_token"] = token });
                    viewCount++;
                }

                JObject body;
                if (v2)
                {
                    body = new JObject
                    {
                        ["type"] = viewCount > 1 ? "multiview_to_model" : "image_to_model",
                        ["model_version"] = model,
                        ["texture"] = false,
                        ["pbr"] = false,
                    };
                    if (viewCount > 1) body["files"] = files; else body["file"] = files[0];
                }
                else
                {
                    body = new JObject
                    {
                        ["inputs"] = inputs,
                        ["model"] = model,
                        ["texture"] = false,
                        ["pbr"] = false,
                    };
                }
                if (faceLimit > 0) body["face_limit"] = faceLimit;
                if (smartLowPoly) body["smart_low_poly"] = true;
                File.WriteAllText(statusPath + ".request.json", body.ToString());

                WriteStatus(statusPath, "submitting", null, 0, null, null, null);
                JObject submit = PostJson(key, v2 ? V2Base + "/task" : BaseUrl + "/generation/multiview-to-model", body);
                taskId = submit["data"]?["task_id"]?.ToString();
                if (string.IsNullOrEmpty(taskId)) throw new Exception("no task_id: " + Truncate(submit.ToString()));
                WriteStatus(statusPath, "queued", taskId, 0, null, null, null);

                string modelUrl = null;
                string previewUrl = null;
                for (int i = 0; i < 360; i++)
                {
                    Thread.Sleep(5000);
                    JObject task = GetJson(key, v2 ? V2Base + "/task/" + taskId : BaseUrl + "/tasks/" + taskId);
                    JObject data = task["data"] as JObject ?? new JObject();
                    string status = data["status"]?.ToString() ?? "";
                    int progress = data["progress"]?.Type == JTokenType.Integer ? data["progress"].Value<int>() : 0;
                    if (status == "success")
                    {
                        JObject output = data["output"] as JObject ?? new JObject();
                        modelUrl = Url(output["model_url"]) ?? Url(output["model"]) ?? Url(output["base_model"]) ?? Url(output["pbr_model"]);
                        previewUrl = Url(output["rendered_image_url"]) ?? Url(output["rendered_image"]);
                        File.WriteAllText(statusPath + ".task.json", Scrub(task.ToString(), key));
                        if (string.IsNullOrEmpty(modelUrl)) throw new Exception("success without model url: " + Truncate(output.ToString()));
                        break;
                    }
                    if (status == "failed" || status == "cancelled" || status == "banned" || status == "expired")
                        throw new Exception("task " + status + ": " + data["error_code"] + " " + data["error_message"]);
                    WriteStatus(statusPath, status, taskId, progress, null, null, null);
                }
                if (modelUrl == null) throw new Exception("timed out waiting for task " + taskId);

                WriteStatus(statusPath, "downloading", taskId, 100, null, null, null);
                File.WriteAllBytes(glbPath, Http.GetByteArrayAsync(modelUrl).Result);
                if (!string.IsNullOrEmpty(previewUrl))
                {
                    try { File.WriteAllBytes(previewPath, Http.GetByteArrayAsync(previewUrl).Result); }
                    catch (Exception) { previewPath = null; }
                }
                else previewPath = null;
                WriteStatus(statusPath, "downloaded", taskId, 100, null, glbPath, previewPath);
            }
            catch (Exception e)
            {
                WriteStatus(statusPath, "error", taskId, 0, Scrub(e.ToString(), key), null, null);
            }
        }

        // One synchronous poll of an existing task (survives domain reloads, unlike the background thread in Start):
        // writes the status file and, on success, downloads the GLB and preview. Returns the status JSON.
        public static string Poll(string statusPath, string taskId, string version, string api)
        {
            string key;
            if (!MCPForUnity.Editor.Security.SecureKeyStore.Current.TryGet("tripo", out key) || string.IsNullOrEmpty(key)) return "no key";
            key = key.Trim();
            bool v2 = api == "v2";
            Directory.CreateDirectory(OutputDir);
            string glbPath = Path.GetFullPath(Path.Combine(OutputDir, "Palm_Tripo_" + version + ".glb"));
            string previewPath = Path.GetFullPath(Path.Combine(OutputDir, "Palm_Tripo_" + version + "_preview.png"));
            try
            {
                JObject task = GetJson(key, v2 ? V2Base + "/task/" + taskId : BaseUrl + "/tasks/" + taskId);
                JObject data = task["data"] as JObject ?? new JObject();
                string status = data["status"]?.ToString() ?? "";
                int progress = data["progress"]?.Type == JTokenType.Integer ? data["progress"].Value<int>() : 0;
                if (status == "success")
                {
                    JObject output = data["output"] as JObject ?? new JObject();
                    string modelUrl = Url(output["model_url"]) ?? Url(output["model"]) ?? Url(output["base_model"]) ?? Url(output["pbr_model"]);
                    string previewUrl = Url(output["rendered_image_url"]) ?? Url(output["rendered_image"]);
                    File.WriteAllText(statusPath + ".task.json", Scrub(task.ToString(), key));
                    if (string.IsNullOrEmpty(modelUrl)) throw new Exception("success without model url: " + Truncate(output.ToString()));
                    if (!File.Exists(glbPath)) File.WriteAllBytes(glbPath, Http.GetByteArrayAsync(modelUrl).Result);
                    if (!string.IsNullOrEmpty(previewUrl) && !File.Exists(previewPath))
                    {
                        try { File.WriteAllBytes(previewPath, Http.GetByteArrayAsync(previewUrl).Result); } catch (Exception) { }
                    }
                    WriteStatus(statusPath, "downloaded", taskId, 100, null, glbPath, File.Exists(previewPath) ? previewPath : null);
                }
                else if (status == "failed" || status == "cancelled" || status == "banned" || status == "expired")
                    WriteStatus(statusPath, "error", taskId, 0, "task " + status + ": " + data["error_code"] + " " + data["error_message"], null, null);
                else
                    WriteStatus(statusPath, status, taskId, progress, null, null, null);
            }
            catch (Exception e)
            {
                WriteStatus(statusPath, "error", taskId, 0, Scrub(e.ToString(), key), null, null);
            }
            return File.ReadAllText(statusPath);
        }

        // Probes both API domains with the stored key; returns status codes and scrubbed bodies, never the key.
        public static string CheckKey()
        {
            string key;
            if (!MCPForUnity.Editor.Security.SecureKeyStore.Current.TryGet("tripo", out key) || string.IsNullOrEmpty(key)) return "no key stored";
            key = key.Trim();
            var sb = new StringBuilder("key length " + key.Length + ", prefix " + key.Substring(0, Math.Min(4, key.Length)) + "\n");
            foreach (string url in new[] { V2Base + "/user/balance", BaseUrl + "/user/balance", BaseUrl + "/balance" })
            {
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        HttpResponseMessage res = Http.SendAsync(req).Result;
                        sb.Append(url + " -> " + (int)res.StatusCode + " " + Scrub(Truncate(res.Content.ReadAsStringAsync().Result), key) + "\n");
                    }
                }
                catch (Exception e) { sb.Append(url + " -> " + Scrub(e.Message, key) + "\n"); }
            }
            return sb.ToString();
        }

        static string Upload(string key, string path, bool v2)
        {
            // The request owns the form content; disposing both trips a Mono NullReferenceException in MultipartContent.
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(File.ReadAllBytes(path));
            file.Headers.ContentType = new MediaTypeHeaderValue(path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
            form.Add(file, "file", Path.GetFileName(path));
            using (var req = new HttpRequestMessage(HttpMethod.Post, v2 ? V2Base + "/upload" : BaseUrl + "/files"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                req.Content = form;
                JObject json = Send(req, key);
                string token = json["data"]?["file_token"]?.ToString() ?? json["data"]?["image_token"]?.ToString();
                if (string.IsNullOrEmpty(token)) throw new Exception("upload returned no file_token: " + Truncate(json.ToString()));
                return token;
            }
        }

        static JObject PostJson(string key, string url, JObject body)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                req.Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                return Send(req, key);
            }
        }

        static JObject GetJson(string key, string url)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                return Send(req, key);
            }
        }

        static JObject Send(HttpRequestMessage req, string key)
        {
            HttpResponseMessage res = Http.SendAsync(req).Result;
            string text = res.Content.ReadAsStringAsync().Result;
            JObject json = null;
            try { json = JObject.Parse(text); } catch (Exception) { }
            int code = json?["code"]?.Type == JTokenType.Integer ? json["code"].Value<int>() : -1;
            if (!res.IsSuccessStatusCode || code != 0)
                throw new Exception("HTTP " + (int)res.StatusCode + " code " + code + ": " + Scrub(Truncate(text), key));
            return json;
        }

        static string Url(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.String) return string.IsNullOrEmpty(t.ToString()) ? null : t.ToString();
            var o = t as JObject;
            string u = o?["url"]?.ToString();
            return string.IsNullOrEmpty(u) ? null : u;
        }

        static string Truncate(string s) => s == null ? "" : (s.Length > 600 ? s.Substring(0, 600) + "…" : s);
        static string Scrub(string s, string key) => string.IsNullOrEmpty(key) || s == null ? s : s.Replace(key, "***");

        static readonly object StatusLock = new object();
        static void WriteStatus(string path, string phase, string taskId, int progress, string error, string glb, string preview)
        {
            var o = new JObject
            {
                ["phase"] = phase,
                ["task_id"] = taskId,
                ["progress"] = progress,
                ["error"] = error,
                ["glb"] = glb,
                ["preview"] = preview,
                ["time"] = DateTime.UtcNow.ToString("o"),
            };
            lock (StatusLock) File.WriteAllText(path, o.ToString());
        }

        // ---- GLB → Mesh asset (main thread) ----

        // Reads a binary glTF, merges every primitive (node transforms applied), converts glTF's right-handed frame
        // to Unity by negating X and reversing winding, and saves a Mesh asset next to the GLB. Returns the asset path.
        public static string ImportGlb(string glbPath, string assetName)
        {
            byte[] bytes = File.ReadAllBytes(glbPath);
            if (BitConverter.ToUInt32(bytes, 0) != 0x46546C67u) throw new Exception("not a GLB: " + glbPath);
            int offset = 12;
            string jsonText = null;
            byte[] bin = null;
            while (offset < bytes.Length)
            {
                int len = BitConverter.ToInt32(bytes, offset);
                uint type = BitConverter.ToUInt32(bytes, offset + 4);
                offset += 8;
                if (type == 0x4E4F534Au) jsonText = Encoding.UTF8.GetString(bytes, offset, len);
                else if (type == 0x004E4942u) { bin = new byte[len]; Buffer.BlockCopy(bytes, offset, bin, 0, len); }
                offset += len;
            }
            if (jsonText == null || bin == null) throw new Exception("GLB missing JSON or BIN chunk");
            JObject gltf = JObject.Parse(jsonText);

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            bool allNormals = true;

            JArray nodes = gltf["nodes"] as JArray ?? new JArray();
            JArray scenes = gltf["scenes"] as JArray;
            var roots = new List<int>();
            if (scenes != null && scenes.Count > 0)
                foreach (JToken n in scenes[0]["nodes"] as JArray ?? new JArray()) roots.Add(n.Value<int>());
            else
                for (int i = 0; i < nodes.Count; i++) roots.Add(i);

            var stack = new Stack<KeyValuePair<int, Matrix4x4>>();
            foreach (int r in roots) stack.Push(new KeyValuePair<int, Matrix4x4>(r, Matrix4x4.identity));
            int visited = 0;
            while (stack.Count > 0)
            {
                var kv = stack.Pop();
                JObject node = nodes[kv.Key] as JObject;
                Matrix4x4 m = kv.Value * NodeMatrix(node);
                visited++;
                if (node["mesh"] != null)
                {
                    JObject mesh = gltf["meshes"][node["mesh"].Value<int>()] as JObject;
                    foreach (JObject prim in mesh["primitives"] as JArray)
                    {
                        int mode = prim["mode"]?.Value<int>() ?? 4;
                        if (mode != 4) continue;
                        int baseIndex = positions.Count;
                        Vector3[] p = ReadVec3(gltf, bin, prim["attributes"]["POSITION"].Value<int>());
                        Vector3[] n = prim["attributes"]["NORMAL"] != null ? ReadVec3(gltf, bin, prim["attributes"]["NORMAL"].Value<int>()) : null;
                        if (n == null) allNormals = false;
                        for (int i = 0; i < p.Length; i++)
                        {
                            Vector3 wp = m.MultiplyPoint3x4(p[i]);
                            positions.Add(new Vector3(-wp.x, wp.y, wp.z));
                            if (n != null)
                            {
                                Vector3 wn = m.MultiplyVector(n[i]);
                                normals.Add(new Vector3(-wn.x, wn.y, wn.z).normalized);
                            }
                        }
                        int[] idx = prim["indices"] != null ? ReadIndices(gltf, bin, prim["indices"].Value<int>()) : Sequence(p.Length);
                        for (int i = 0; i + 2 < idx.Length; i += 3)
                        {
                            // reversed winding after the X flip
                            indices.Add(baseIndex + idx[i]);
                            indices.Add(baseIndex + idx[i + 2]);
                            indices.Add(baseIndex + idx[i + 1]);
                        }
                    }
                }
                foreach (JToken c in node["children"] as JArray ?? new JArray())
                    stack.Push(new KeyValuePair<int, Matrix4x4>(c.Value<int>(), m));
            }

            var result = new Mesh { name = assetName };
            result.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            result.SetVertices(positions);
            result.SetTriangles(indices, 0);
            if (allNormals && normals.Count == positions.Count) result.SetNormals(normals);
            else result.RecalculateNormals();
            result.RecalculateBounds();

            string assetPath = Path.Combine(Path.GetDirectoryName(glbPath), assetName + ".asset").Replace('\\', '/');
            int assetsIndex = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
            assetPath = assetPath.Substring(assetsIndex);
            AssetDatabase.CreateAsset(result, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PalmMeshTripo] " + assetPath + ": " + positions.Count + " verts, " + indices.Count / 3 + " tris, bounds " + result.bounds.size + " centre " + result.bounds.center + ", nodes " + visited);
            return assetPath;
        }

        // Renders front (+Z side, camera looking along -Z), side (+X) and back (-Z) views of a mesh asset into one PNG
        // through PreviewRenderUtility, so the open scene is untouched. Flat grey URP Lit material, white background.
        public static string RenderViews(string meshAssetPath, string pngPath, int size)
        {
            return RenderViews(meshAssetPath, pngPath, size, null);
        }

        // materialPath null = flat grey URP Lit; otherwise that material is drawn (every pass, so two-pass glass works).
        public static string RenderViews(string meshAssetPath, string pngPath, int size, string materialPath)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (mesh == null) throw new Exception("mesh not found: " + meshAssetPath);
            Material mat = materialPath != null ? AssetDatabase.LoadAssetAtPath<Material>(materialPath) : null;
            bool ownMaterial = mat == null;
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.82f, 0.82f, 0.82f) };
                mat.SetFloat("_Smoothness", 0.25f);
            }

            string[] labels = { "FRONT (+Z)", "SIDE (+X)", "BACK (-Z)" };
            Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back };
            var sheet = new Texture2D(size * 3, size, TextureFormat.RGB24, false);
            float radius = mesh.bounds.extents.magnitude;
            for (int v = 0; v < 3; v++)
            {
                var pru = new PreviewRenderUtility();
                try
                {
                    pru.camera.backgroundColor = Color.white;
                    pru.camera.clearFlags = CameraClearFlags.SolidColor;
                    pru.camera.orthographic = true;
                    pru.camera.orthographicSize = radius * 1.05f;
                    pru.camera.nearClipPlane = 0.01f;
                    pru.camera.farClipPlane = radius * 10f;
                    pru.camera.transform.position = mesh.bounds.center + dirs[v] * radius * 4f;
                    pru.camera.transform.LookAt(mesh.bounds.center, Vector3.up);
                    pru.lights[0].intensity = 1.2f;
                    // key light follows the camera so every view is lit from its own front-top-left
                    pru.lights[0].transform.rotation = pru.camera.transform.rotation * Quaternion.Euler(35f, -25f, 0f);
                    pru.lights[1].intensity = 0.6f;
                    pru.ambientColor = new Color(0.35f, 0.35f, 0.35f);
                    pru.BeginStaticPreview(new Rect(0, 0, size, size));
                    pru.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
                    pru.Render(true);
                    Texture2D view = pru.EndStaticPreview();
                    sheet.SetPixels(size * v, 0, size, size, view.GetPixels());
                }
                finally { pru.Cleanup(); }
            }
            sheet.Apply();
            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            if (ownMaterial) UnityEngine.Object.DestroyImmediate(mat);
            UnityEngine.Object.DestroyImmediate(sheet);
            return string.Join(" | ", labels) + " -> " + pngPath;
        }

        // Renders a scene camera (by GameObject name) into a PNG without touching its settings permanently.
        public static string SnapshotCamera(string cameraName, string pngPath, int width, int height)
        {
            var go = GameObject.Find(cameraName);
            if (go == null) throw new Exception("camera not found: " + cameraName);
            var cam = go.GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            return cameraName + " -> " + pngPath;
        }

        static Matrix4x4 NodeMatrix(JObject node)
        {
            if (node["matrix"] is JArray ma && ma.Count == 16)
            {
                var m = new Matrix4x4();
                for (int i = 0; i < 16; i++) m[i % 4, i / 4] = ma[i].Value<float>();
                return m;
            }
            Vector3 t = Vector3.zero, s = Vector3.one;
            Quaternion r = Quaternion.identity;
            if (node["translation"] is JArray ta) t = new Vector3(ta[0].Value<float>(), ta[1].Value<float>(), ta[2].Value<float>());
            if (node["scale"] is JArray sa) s = new Vector3(sa[0].Value<float>(), sa[1].Value<float>(), sa[2].Value<float>());
            if (node["rotation"] is JArray ra) r = new Quaternion(ra[0].Value<float>(), ra[1].Value<float>(), ra[2].Value<float>(), ra[3].Value<float>());
            return Matrix4x4.TRS(t, r, s);
        }

        static Vector3[] ReadVec3(JObject gltf, byte[] bin, int accessorIndex)
        {
            JObject acc = gltf["accessors"][accessorIndex] as JObject;
            JObject view = gltf["bufferViews"][acc["bufferView"].Value<int>()] as JObject;
            if (acc["componentType"].Value<int>() != 5126) throw new Exception("expected float accessor");
            int count = acc["count"].Value<int>();
            int stride = view["byteStride"]?.Value<int>() ?? 12;
            int start = (view["byteOffset"]?.Value<int>() ?? 0) + (acc["byteOffset"]?.Value<int>() ?? 0);
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int o = start + i * stride;
                result[i] = new Vector3(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4), BitConverter.ToSingle(bin, o + 8));
            }
            return result;
        }

        static int[] ReadIndices(JObject gltf, byte[] bin, int accessorIndex)
        {
            JObject acc = gltf["accessors"][accessorIndex] as JObject;
            JObject view = gltf["bufferViews"][acc["bufferView"].Value<int>()] as JObject;
            int count = acc["count"].Value<int>();
            int type = acc["componentType"].Value<int>();
            int size = type == 5125 ? 4 : type == 5123 ? 2 : 1;
            int stride = view["byteStride"]?.Value<int>() ?? size;
            int start = (view["byteOffset"]?.Value<int>() ?? 0) + (acc["byteOffset"]?.Value<int>() ?? 0);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int o = start + i * stride;
                result[i] = size == 4 ? (int)BitConverter.ToUInt32(bin, o) : size == 2 ? BitConverter.ToUInt16(bin, o) : bin[o];
            }
            return result;
        }

        static int[] Sequence(int n)
        {
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = i;
            return r;
        }
    }
}
