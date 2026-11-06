#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Reflection;
using System.Text;
using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BuddahGo.AI
{
    // Development-build probe for the 神足通 + 如来神掌 whiteout (Docs/vfx/slots.md, "Whiteout"): starts an A1 Solo race,
    // then every cycle the local racer casts the given skills and fires a palm, and the main camera is rendered raw
    // (float target, no post) and with post every 0.1 s. Each sample logs NaN / Inf / max of the raw frame and the white
    // and black share of the final image; frames with NaN, Inf or a white-out are saved. Off unless the player is
    // started with --vfx-nan-probe <empty absolute dir> (optional --vfx-nan-skills a,b  --vfx-nan-cycles N).
    // Run it on another GPU with -force-device-index N.
    // --vfx-nan-plant: each cycle also plants a quad that writes Inf (its ring) and NaN (Inf x 0 elsewhere) between the
    // camera and the racer, alternates the race camera's Stop NaN off (even cycles) / on (odd), and saves a Game-view
    // screenshot of each, so the same GPU shows what an Inf / NaN pixel does with and without Stop NaN.
    public sealed class VfxNanProbe : MonoBehaviour
    {
        private const float CycleSeconds = 8f, SampleSeconds = 0.1f, FireDelay = 0.6f;
        private const int W = 640, H = 360, MaxSaved = 60;

        private string _dir;
        private string[] _skills = { "acceleration", "push_projectile_hands" };
        private int _cycles = 6, _cycle = -1, _saved, _sample;
        private bool _harnessStarted, _fired, _plant, _shot;
        private float _goAt = -1f, _cycleAt, _nextSample;
        private GameObject _planted;
        private StreamWriter _log;
        private RenderTexture _raw, _post;
        private Texture2D _rawTex, _postTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dir = HarnessArgs.Value(args, "--vfx-nan-probe");
            if (dir == null) return;
            var host = new GameObject("VFX NaN Probe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<VfxNanProbe>();
            probe._dir = dir;
            string skills = HarnessArgs.Value(args, "--vfx-nan-skills");
            if (!string.IsNullOrEmpty(skills)) probe._skills = skills.Split(',');
            if (int.TryParse(HarnessArgs.Value(args, "--vfx-nan-cycles"), out int cycles)) probe._cycles = Mathf.Max(1, cycles);
            probe._plant = HarnessArgs.Has(args, "--vfx-nan-plant");
        }

        private void Start()
        {
            Directory.CreateDirectory(_dir);
            _log = new StreamWriter(Path.Combine(_dir, "probe.log")) { AutoFlush = true };
            _log.WriteLine($"gpu={SystemInfo.graphicsDeviceName} vendor={SystemInfo.graphicsDeviceVendor} api={SystemInfo.graphicsDeviceType} driver={SystemInfo.graphicsDeviceVersion} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} skills={string.Join(",", _skills)}");
            _raw = new RenderTexture(W, H, 24, RenderTextureFormat.ARGBFloat);
            _post = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _rawTex = new Texture2D(W, H, TextureFormat.RGBAFloat, false);
            _postTex = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        private void Update()
        {
            try { Tick(); }
            catch (Exception e) { _log?.WriteLine("exception " + e); Finish(); }
        }

        private void Tick()
        {
            if (!_harnessStarted)
            {
                if (SceneManager.GetActiveScene().name != SceneNames.MainMenu || Time.realtimeSinceStartup < 3f) return;
                AITuningHarness.Begin(Path.Combine(_dir, "a1"), null, 1, 1);
                _harnessStarted = true;
                return;
            }
            RoomStateManager room = RoomStateManager.Instance;
            if (_goAt < 0f)
            {
                if (room != null && room.IsAuthoritativeGoIssued) { _goAt = Time.time; _cycleAt = _goAt + 2f; _log.WriteLine("go"); }
                return;
            }
            SkillExecutor local = LocalRacer();
            if (local == null) { _log.WriteLine("no local racer t=" + Time.time.ToString("0.00")); Finish(); return; }

            if (Time.time >= _cycleAt)
            {
                _cycle++;
                if (_cycle >= _cycles) { Finish(); return; }
                _cycleAt = Time.time + CycleSeconds;
                _fired = false;
                _shot = false;
                if (_plant) SetStopNaN(_cycle % 2 == 1);
                Cast(local);
            }
            if (_plant) Plant(local);
            if (!_fired && Time.time >= _cycleAt - CycleSeconds + FireDelay)
            {
                _fired = true;
                Fire(local);
            }
            if (Time.time >= _nextSample)
            {
                _nextSample = Time.time + SampleSeconds;
                Sample();
            }
        }

        private static SkillExecutor LocalRacer()
        {
            foreach (SkillExecutor e in FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None))
                if (e.IsOwner) return e;
            return null;
        }

        private void Cast(SkillExecutor local)
        {
            var db = typeof(SkillExecutor).GetField("database", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(local) as SkillDatabase;
            foreach (string id in _skills)
            {
                if (db != null && db.TryGet(id, out SkillAction skill))
                {
                    skill.ExecuteServer(local, 0);
                    skill.ExecuteObservers(local, 0, true);
                    _log.WriteLine($"cycle {_cycle} cast {id} t={Time.time - _goAt:0.00}");
                }
                else _log.WriteLine($"cycle {_cycle} missing {id}");
            }
        }

        private void Fire(SkillExecutor local)
        {
            var hand = local.GetComponent<BuddahHandControl>();
            if (hand == null) return;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(BuddahHandControl).GetField("_nextPushServerTimeLeft", flags)?.SetValue(hand, 0f);
            typeof(BuddahHandControl).GetField("_nextPushServerTimeRight", flags)?.SetValue(hand, 0f);
            object ok = typeof(BuddahHandControl).GetMethod("TryPushServer", flags)?.Invoke(hand, new object[] { false, hand.ServerHandYaw, hand.transform.position, true });
            _log.WriteLine($"cycle {_cycle} fire={ok} t={Time.time - _goAt:0.00}");
        }

        private void SetStopNaN(bool on)
        {
            var data = Camera.main != null ? Camera.main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (data != null) data.stopNaN = on;
            _log.WriteLine($"cycle {_cycle} stopNaN={on}");
        }

        // From 1 s to 4 s into the cycle the Inf / NaN quad stands a quarter of the way from the racer to the camera;
        // at 2.5 s the Game view is captured.
        private void Plant(SkillExecutor local)
        {
            float since = Time.time - (_cycleAt - CycleSeconds);
            Camera cam = Camera.main;
            bool show = cam != null && since >= 1f && since < 4f;
            if (_planted == null)
            {
                Shader shader = Shader.Find("FX/Additive_Glow");
                if (shader == null) { _log.WriteLine("FX/Additive_Glow not in the build"); _plant = false; return; }
                var material = new Material(shader);
                material.EnableKeyword("_FX_POLAR");
                material.SetFloat("_Polar", 1f);
                material.SetFloat("_Occlusion", 0f);
                material.SetFloat("_Intensity", 3e38f);
                material.SetColor("_Color", new Color(100f, 60f, 8f, 1f));
                _planted = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(_planted.GetComponent<Collider>());
                _planted.layer = LayerMask.NameToLayer("Buddah");
                var renderer = _planted.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _planted.transform.localScale = Vector3.one * 3f;
            }
            _planted.SetActive(show);
            if (!show) return;
            Vector3 racer = local.transform.position + Vector3.up * 6f;
            Vector3 at = racer + (cam.transform.position - racer) * 0.25f;
            _planted.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - cam.transform.position));
            if (!_shot && since >= 2.5f)
            {
                _shot = true;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                string file = Path.Combine(_dir, $"plant_cycle{_cycle}_stopnan-{(data != null && data.stopNaN ? "on" : "off")}.png");
                ScreenCapture.CaptureScreenshot(file);
                _log.WriteLine("screenshot " + file);
            }
        }

        private void Sample()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            bool post = data != null && data.renderPostProcessing;
            if (data != null) data.renderPostProcessing = false;
            cam.targetTexture = _raw; cam.Render();
            if (data != null) data.renderPostProcessing = post;
            cam.targetTexture = _post; cam.Render(); cam.targetTexture = null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _raw; _rawTex.ReadPixels(new Rect(0, 0, W, H), 0, 0); _rawTex.Apply();
            RenderTexture.active = _post; _postTex.ReadPixels(new Rect(0, 0, W, H), 0, 0); _postTex.Apply();
            RenderTexture.active = previous;

            Color[] raw = _rawTex.GetPixels();
            int nan = 0, inf = 0; float max = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                Color c = raw[i];
                if (float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsNaN(c.a)) { nan++; continue; }
                if (float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b)) { inf++; continue; }
                max = Mathf.Max(max, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            }
            Color32[] final = _postTex.GetPixels32();
            int white = 0, black = 0;
            for (int i = 0; i < final.Length; i++)
            {
                Color32 p = final[i];
                if (p.r > 230 && p.g > 200 && p.b > 150) white++;
                if (p.r < 12 && p.g < 12 && p.b < 12) black++;
            }
            float whiteShare = 100f * white / final.Length;
            _log.WriteLine($"s{_sample} cycle={_cycle} t={Time.time - _goAt:0.00} dt={Time.deltaTime:0.000} nan={nan} inf={inf} max={max:0} white%={whiteShare:0.0} black%={100f * black / final.Length:0.0}");
            bool bad = nan > 0 || inf > 0 || whiteShare > 5f;
            if ((bad && _saved < MaxSaved) || _sample % 20 == 0)
            {
                File.WriteAllBytes(Path.Combine(_dir, $"s{_sample:0000}{(bad ? "_bad" : "")}.png"), _postTex.EncodeToPNG());
                if (bad) _saved++;
            }
            _sample++;
        }

        private void Finish()
        {
            _log?.WriteLine("done");
            _log?.Dispose();
            _log = null;
            enabled = false;
            Application.Quit();
        }
    }
}
#endif
