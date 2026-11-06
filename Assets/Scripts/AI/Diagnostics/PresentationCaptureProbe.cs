using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BuddahGo.AI
{
    /// <summary>
    /// Diagnostic probe (off unless started): renders before/after frames of 缘起 presentation from its own camera while
    /// a Practice harness scenario runs. Every rendered frame is kept with its real time since the take started
    /// (&lt;mode&gt;_&lt;take&gt;_times.txt); game time is not stepped, because the motor's freeze and burst run on network ticks
    /// that follow real time. Frames go to &lt;dir&gt;/&lt;mode&gt;_&lt;take&gt;_&lt;frame&gt;.png with a caption line in captions.txt;
    /// a script samples them on a 1/15 s grid into the labelled GIF (Docs/gameplay/practice-verification.md, 2026-11-06).
    /// Modes: "frost" (S9: from the frost_lock trigger, ground + ice blocks), "palm" (S10: each charged palm of the
    /// human from its spawn), "charge" (S8: from each giant_charge trigger, through the game camera's own pose and FOV
    /// every frame, so the take is what the caster sees). Option: oldPalmRule (palm = body scale as before 2026-11-06:
    /// in-memory SO values, restored when the probe ends).
    /// Started from execute_code after MechanicsPracticeHarness.Begin.
    /// </summary>
    public sealed class PresentationCaptureProbe : MonoBehaviour
    {
        private const int Width = 640, Height = 360;
        private const float FrostSeconds = 2.4f, PalmSeconds = 1.2f, ChargeSeconds = 2.4f;
        private const int MaxTakes = 2;

        public static string Status { get; private set; } = "idle";

        private string _dir, _mode;
        private bool _oldPalmRule;
        private SkillExecutor _human;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;
        private int _takes;
        private bool _capturing;
        private bool _followGameCamera;
        private readonly HashSet<int> _seenProjectiles = new HashSet<int>();
        private readonly List<Action> _restore = new List<Action>();
        private StreamWriter _captions;

        public static PresentationCaptureProbe Begin(string dir, string mode, bool oldPalmRule)
        {
            Directory.CreateDirectory(dir);
            var go = new GameObject("PresentationCaptureProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<PresentationCaptureProbe>();
            probe._dir = dir;
            probe._mode = mode;
            probe._oldPalmRule = oldPalmRule;
            probe._captions = new StreamWriter(Path.Combine(dir, "captions.txt")) { AutoFlush = true };
            Status = "waiting for the human racer";
            return probe;
        }

        private void Update()
        {
            if (_capturing)
                return;
            if (_human == null && !TryBindHuman())
                return;
            if (_takes >= MaxTakes)
                return;
            if (_mode == "palm")
                WatchPalm();
        }

        // After the camera brain has placed the game camera this frame (both run in LateUpdate; a one-frame lag at most).
        private void LateUpdate()
        {
            Camera main = Camera.main;
            if (!_followGameCamera || _camera == null || main == null)
                return;
            _camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            _camera.fieldOfView = main.fieldOfView;
        }

        private bool TryBindHuman()
        {
            foreach (SkillExecutor executor in FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None))
            {
                if (!executor.IsOwner || executor.StateBoard == null)
                    continue;
                _human = executor;
                if (_mode == "frost" || _mode == "charge")
                    _human.YuanqiObserved += OnYuanqi;
                if (_oldPalmRule)
                    ApplyOldPalmRule();
                // Warm up: the capture camera's first renders stall for about a second (pipeline / shader setup), so it
                // renders from now on, before any take starts.
                EnsureCamera();
                _camera.transform.SetPositionAndRotation(executor.transform.position + Vector3.up * 30f, Quaternion.LookRotation(Vector3.down + executor.transform.forward));
                _camera.enabled = true;
                Status = "bound to " + executor.name;
                return true;
            }
            return false;
        }

        private void OnYuanqi(string yuanqiId, YuanqiPresentationPayload payload, bool localIsCaster)
        {
            if (_capturing || _takes >= MaxTakes)
                return;
            if (_mode == "charge" && yuanqiId == "giant_charge")
            {
                var scale = _human.GetComponent<PlayerScaleEffect>();
                StartCoroutine(Capture(Vector3.zero, Vector3.forward, ChargeSeconds,
                    "giant_charge body x" + (scale != null ? scale.CurrentScaleMultiplier : 1f).ToString("0.##") + " at the trigger", true));
                return;
            }
            if (yuanqiId != "frost_lock" || _mode != "frost")
                return;
            Transform caster = _human.transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(caster.forward, Vector3.up).normalized;
            Vector3 eye = payload.Center - flatForward * 26f + Vector3.up * 30f;
            StartCoroutine(Capture(eye, payload.Center + flatForward * 8f, FrostSeconds,
                "frost_lock R " + payload.Radius.ToString("0.#") + " m, N " + payload.Seconds.ToString("0.##") + " s"));
        }

        private void WatchPalm()
        {
            foreach (ChargedHandProjectileRuntime projectile in FindObjectsByType<ChargedHandProjectileRuntime>(FindObjectsSortMode.None))
            {
                if (!_seenProjectiles.Add(projectile.GetInstanceID()))
                    continue;
                // One shot spawns the server hitbox plus visual copies; only the hitbox carries the attacker.
                FieldInfo attacker = typeof(ChargedHandProjectileRuntime).GetField("_attacker", BindingFlags.Instance | BindingFlags.NonPublic);
                if (attacker == null || !ReferenceEquals(attacker.GetValue(projectile), _human.NetworkObject))
                    continue;
                var scale = _human.GetComponent<PlayerScaleEffect>();
                float body = scale != null ? scale.CurrentScaleMultiplier : 1f;
                float palm = scale != null ? scale.CurrentPalmSizeMultiplier : 1f;
                Transform t = _human.transform;
                Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                // Same wide framing for every shot (the x4 giant is about 34 m tall), so the palm sizes compare directly.
                Vector3 eye = t.position - right * 70f - forward * 45f + Vector3.up * 55f;
                string rule = _oldPalmRule ? "old rule: palm = body" : "new rule";
                StartCoroutine(Capture(eye, t.position + forward * 30f + Vector3.up * 15f, PalmSeconds,
                    rule + ", body x" + body.ToString("0.##") + ", palm x" + (_oldPalmRule ? body : palm).ToString("0.##")));
                return;
            }
        }

        private IEnumerator Capture(Vector3 eye, Vector3 lookAt, float seconds, string caption, bool gameCamera = false)
        {
            _capturing = true;
            _followGameCamera = gameCamera;
            int take = ++_takes;
            EnsureCamera();
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye, Vector3.up));
            _camera.enabled = true;
            _captions.WriteLine(take + "\t" + caption);
            Status = "capturing take " + take;
            float started = Time.realtimeSinceStartup;
            var times = new StreamWriter(Path.Combine(_dir, $"{_mode}_{take}_times.txt"));
            for (int i = 0; Time.realtimeSinceStartup - started < seconds; i++)
            {
                yield return new WaitForEndOfFrame();
                times.WriteLine(i + "	" + (Time.realtimeSinceStartup - started).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                RenderTexture active = RenderTexture.active;
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                _readback.Apply(false);
                RenderTexture.active = active;
                File.WriteAllBytes(Path.Combine(_dir, $"{_mode}_{take}_{i:000}.png"), _readback.EncodeToPNG());
            }
            times.Dispose();
            _capturing = false;
            _followGameCamera = false;
            Status = take >= MaxTakes ? "done" : "captured take " + take;
            if (take >= MaxTakes)
                Finish();
        }

        private void EnsureCamera()
        {
            if (_camera != null)
                return;
            _target = new RenderTexture(Width, Height, 24) { name = "PresentationCaptureProbe" };
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var go = new GameObject("PresentationCaptureProbe Camera");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.targetTexture = _target;
            _camera.fieldOfView = 50f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 2000f;
            Camera main = Camera.main;
            if (main != null)
            {
                _camera.clearFlags = main.clearFlags;
                _camera.backgroundColor = main.backgroundColor;
                _camera.cullingMask = main.cullingMask;
                // Same post, AA and volume mask as the game camera, so the takes are not washed out.
                var source = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (source != null)
                {
                    var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_camera);
                    data.renderPostProcessing = source.renderPostProcessing;
                    data.antialiasing = source.antialiasing;
                    data.antialiasingQuality = source.antialiasingQuality;
                    data.dithering = source.dithering;
                    data.renderShadows = source.renderShadows;
                    data.volumeLayerMask = source.volumeLayerMask;
                    data.volumeTrigger = main.transform;
                }
            }
            _camera.enabled = false;
        }

        // Palm = body scale, base size 1: what the palm did before 2026-11-06. In-memory only, restored in Finish.
        private void ApplyOldPalmRule()
        {
            FieldInfo dbField = typeof(SkillExecutor).GetField("database", BindingFlags.Instance | BindingFlags.NonPublic);
            var db = dbField != null ? dbField.GetValue(_human) as SkillDatabase : null;
            if (db == null)
                return;
            Override(db, "push_projectile_hands", "projectileSizeMultiplier", 1f);
            Override(db, "push_projectile_hands_anti", "projectileSizeMultiplier", 1f);
            if (db.TryGet("giant", out SkillAction giant))
                Override(db, "giant", "palmSizeMultiplier", ((Skill_Giant)giant).scaleMultiplier);
            if (db.TryGet("giant_anti", out SkillAction anti))
                Override(db, "giant_anti", "palmSizeMultiplier", ((Skill_Giant_Anti)anti).scaleMultiplier);
        }

        private void Override(SkillDatabase db, string skillId, string field, float value)
        {
            if (!db.TryGet(skillId, out SkillAction skill) || skill == null)
                return;
            FieldInfo f = skill.GetType().GetField(field);
            if (f == null)
                return;
            object old = f.GetValue(skill);
            f.SetValue(skill, value);
            _restore.Add(() => f.SetValue(skill, old));
        }

        private void Finish()
        {
            for (int i = _restore.Count - 1; i >= 0; i--)
                _restore[i]();
            _restore.Clear();
            if (_human != null)
                _human.YuanqiObserved -= OnYuanqi;
            _captions?.Dispose();
            _captions = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            for (int i = _restore.Count - 1; i >= 0; i--)
                _restore[i]();
            _restore.Clear();
            _captions?.Dispose();
            if (_target != null)
                Destroy(_target);
        }
    }
}
