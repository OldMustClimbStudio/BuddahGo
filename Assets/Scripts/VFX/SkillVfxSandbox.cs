#if UNITY_EDITOR
using System.Collections.Generic;
using BuddahGo.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace BuddahGo.VFX
{
    // Editor-only VFX debug stage: the scene Assets/VFX/Sandbox/VFX_Sandbox.unity (Tools/BuddahGo/VFX/Open VFX Sandbox).
    // In Play Mode a panel lists every SkillVfxDatabase entry and the extra prefabs. Projectile effects run through
    // SkillVfxPreviewDriver with the timings of their Push table row, effects with a phase controller attach to the
    // caster as buff layers, anything else spawns at the caster. The panel also sets the time scale, pauses, steps one
    // frame, switches cameras and the effects quality tier, and shows live particle, draw call and frame time counts.
    // Keys: 1-9 play, R replay, Space pause, . step, C clear, Tab next camera; the free camera orbits with the right
    // mouse button and zooms with the wheel.
    public class SkillVfxSandbox : MonoBehaviour
    {
        [System.Serializable]
        public class ProjectileBinding
        {
            public string vfxId;
            [Tooltip("Push table row (Skill.xlsx / Push): build-up, lifetime, speed, offsets, collider, count, spacing.")]
            public string pushId;
            [Tooltip("Buff layer vfx id attached to the caster during the cast; empty for none.")]
            public string buffVfxId;
        }

        enum Kind { Projectile, Buff, Generic }

        class Item
        {
            public string label;
            public string vfxId;
            public GameObject prefab;
            public Kind kind;
            public ProjectileBinding binding;
        }

        public SkillVfxDatabase database;
        public List<ProjectileBinding> projectiles = new List<ProjectileBinding>();
        [Tooltip("Prefabs outside the database (old effects, scene effects) to compare against.")]
        public List<GameObject> extraPrefabs = new List<GameObject>();
        public Transform caster;
        public Camera[] cameras;
        public Vector3 freeCameraTarget = new Vector3(0f, 25f, 150f);
        public float buffSeconds = 6f;
        public float genericSeconds = 6f;

        readonly List<Item> _items = new List<Item>();
        readonly List<SkillVfxPreviewDriver> _drivers = new List<SkillVfxPreviewDriver>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        Item _last;
        bool _loop;
        float _timeScale = 1f;
        float _freezeAt = -1f;
        string _freezeText = "-1";
        int _stepFrames;
        int _camera;
        float _idleSince = -1f;
        Vector2 _scroll;
        bool _collapsed;

        float _frameMs;
        int _shurikenParticles;
        int _graphParticles;
        int _controllers;
        float _statsAt;

        float _orbitYaw = 200f;
        float _orbitPitch = 18f;
        float _orbitDistance = 420f;

        void Awake()
        {
            BuildItems();
            SelectCamera(0);
        }

        void BuildItems()
        {
            _items.Clear();
            var seen = new HashSet<GameObject>();
            if (database != null)
            {
                foreach (SkillVfxDatabase.Entry entry in database.entries)
                {
                    if (entry == null || entry.prefab == null)
                        continue;
                    seen.Add(entry.prefab);
                    ProjectileBinding binding = projectiles.Find(p => p.vfxId == entry.vfxId);
                    Kind kind = binding != null ? Kind.Projectile
                        : entry.prefab.GetComponent<SkillVfxPhaseController>() != null ? Kind.Buff : Kind.Generic;
                    _items.Add(new Item { label = entry.vfxId, vfxId = entry.vfxId, prefab = entry.prefab, kind = kind, binding = binding });
                }
            }
            foreach (GameObject prefab in extraPrefabs)
            {
                if (prefab == null || !seen.Add(prefab))
                    continue;
                _items.Add(new Item { label = prefab.name, prefab = prefab, kind = Kind.Generic });
            }
        }

        // ---- playing

        void Play(Item item)
        {
            _last = item;
            _idleSince = -1f;
            switch (item.kind)
            {
                case Kind.Projectile: PlayProjectile(item); break;
                case Kind.Buff: PlayBuff(item.prefab); break;
                default: PlayGeneric(item.prefab); break;
            }
        }

        void PlayProjectile(Item item)
        {
            var go = new GameObject("Driver_" + item.vfxId);
            go.SetActive(false);
            var driver = go.AddComponent<SkillVfxPreviewDriver>();
            driver.projectilePrefab = item.prefab;
            driver.buffPrefab = FindPrefab(item.binding.buffVfxId);
            driver.caster = caster;
            driver.repeat = false;
            driver.freezeAt = _freezeAt;
            driver.timeScale = Time.timeScale;

            PushRecord row = FindPush(item.binding.pushId);
            if (row != null)
            {
                driver.buildUpSeconds = row.windupSeconds;
                driver.lifetimeSeconds = row.lifetimeSeconds;
                driver.speed = row.speed;
                driver.heightOffset = row.heightOffset;
                if (row.colliderSize.sqrMagnitude > 0f)
                    driver.colliderSize = row.colliderSize;
                driver.burstCount = Mathf.Max(1, row.projectileCount);
                if (driver.burstCount > 1)
                {
                    driver.burstSpacing = row.projectileSpacing;
                    driver.burstForwardExtra = row.forwardOffset;
                }
                else
                {
                    driver.forwardOffset = row.forwardOffset;
                }
            }
            else
            {
                Debug.LogWarning("[SkillVfxSandbox] no Push row '" + item.binding.pushId + "', using the driver defaults");
            }
            go.SetActive(true);
            _drivers.Add(driver);
        }

        void PlayBuff(GameObject prefab)
        {
            var controller = Instantiate(prefab, caster).GetComponent<SkillVfxPhaseController>();
            controller.transform.localPosition = Vector3.zero;
            controller.Configure(new SkillVfxPhaseController.Context { caster = caster, isLocalCaster = true });
            controller.ExtendBuff(buffSeconds);
            controller.DestroyWhenFinished(0f);
        }

        // Spawned under an inactive holder so networked prefabs can lose their FishNet components before they wake up
        // (there is no NetworkManager here).
        void PlayGeneric(GameObject prefab)
        {
            var holder = new GameObject("Spawn_" + prefab.name);
            holder.SetActive(false);
            holder.transform.SetParent(caster, false);
            GameObject go = Instantiate(prefab, holder.transform);
            StripNetworking(go);
            holder.SetActive(true);
            foreach (VisualEffect effect in go.GetComponentsInChildren<VisualEffect>(true))
                effect.Play();
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.transform.parent == null || system.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                    system.Play(true);
            }
            _spawned.Add(holder);
            Destroy(holder, genericSeconds);
        }

        // Network behaviours first: the NetworkObject is required by them.
        static void StripNetworking(GameObject go)
        {
            var networked = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && IsNetworked(behaviour.GetType()))
                    networked.Add(behaviour);
            }
            networked.Sort((a, b) => (a.GetType().Name == "NetworkObject").CompareTo(b.GetType().Name == "NetworkObject"));
            foreach (MonoBehaviour behaviour in networked)
                DestroyImmediate(behaviour);
        }

        static bool IsNetworked(System.Type type)
        {
            for (; type != null; type = type.BaseType)
            {
                if (type.Namespace != null && type.Namespace.StartsWith("FishNet"))
                    return true;
            }
            return false;
        }

        GameObject FindPrefab(string vfxId)
        {
            if (database == null || string.IsNullOrEmpty(vfxId))
                return null;
            SkillVfxDatabase.Entry entry = database.entries.Find(e => e != null && e.vfxId == vfxId);
            return entry != null ? entry.prefab : null;
        }

        static PushRecord FindPush(string pushId)
        {
            if (string.IsNullOrEmpty(pushId) || !ProjectConfigRuntime.EnsureInitialized())
                return null;
            return ProjectConfigRuntime.Database.pushes.Find(p => p.pushId == pushId);
        }

        void Clear()
        {
            foreach (SkillVfxPreviewDriver driver in _drivers)
            {
                if (driver != null)
                    Destroy(driver.gameObject);
            }
            _drivers.Clear();
            foreach (SkillVfxPhaseController controller in FindObjectsOfType<SkillVfxPhaseController>())
                Destroy(controller.gameObject);
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                    Destroy(go);
            }
            _spawned.Clear();
            _idleSince = -1f;
        }

        bool AnythingPlaying()
        {
            _drivers.RemoveAll(d => d == null || d.Finished && DestroyDriver(d));
            _spawned.RemoveAll(g => g == null);
            return _drivers.Count > 0 || _spawned.Count > 0 || _controllers > 0;
        }

        static bool DestroyDriver(SkillVfxPreviewDriver driver)
        {
            Destroy(driver.gameObject);
            return true;
        }

        // ---- time

        bool Paused => Time.timeScale == 0f;

        void TogglePause()
        {
            Time.timeScale = Paused ? _timeScale : 0f;
        }

        // Runs one frame at the chosen speed: the scale set now applies to the next frame's delta time and Update
        // sets it back to zero during that frame, so exactly one frame advances.
        void Step()
        {
            Time.timeScale = _timeScale;
            _stepFrames = 1;
        }

        // ---- cameras

        void SelectCamera(int index)
        {
            if (cameras == null || cameras.Length == 0)
                return;
            _camera = (index + cameras.Length) % cameras.Length;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                    continue;
                cameras[i].enabled = i == _camera;
                // faceCamera layers turn toward Camera.main.
                cameras[i].tag = i == _camera ? "MainCamera" : "Untagged";
            }
        }

        bool FreeCameraActive => cameras != null && cameras.Length > 0 && cameras[_camera] != null && cameras[_camera].name == "Cam_Free";

        void UpdateFreeCamera()
        {
            if (!FreeCameraActive)
                return;
            if (Input.GetMouseButton(1))
            {
                _orbitYaw += Input.GetAxisRaw("Mouse X") * 4f;
                _orbitPitch = Mathf.Clamp(_orbitPitch - Input.GetAxisRaw("Mouse Y") * 4f, -5f, 85f);
            }
            _orbitDistance = Mathf.Clamp(_orbitDistance * (1f - Input.mouseScrollDelta.y * 0.1f), 20f, 3000f);
            Transform t = cameras[_camera].transform;
            t.rotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            t.position = freeCameraTarget - t.forward * _orbitDistance;
        }

        // ---- loop

        void Update()
        {
            if (_stepFrames > 0 && --_stepFrames == 0)
                Time.timeScale = 0f;

            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Period)) Step();
            if (Input.GetKeyDown(KeyCode.C)) Clear();
            if (Input.GetKeyDown(KeyCode.R) && _last != null) Play(_last);
            if (Input.GetKeyDown(KeyCode.Tab)) SelectCamera(_camera + 1);
            for (int i = 0; i < Mathf.Min(9, _items.Count); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Play(_items[i]);
            }

            UpdateFreeCamera();
            UpdateStats();

            if (_loop && _last != null && !Paused)
            {
                if (AnythingPlaying())
                    _idleSince = -1f;
                else if (_idleSince < 0f)
                    _idleSince = Time.unscaledTime;
                else if (Time.unscaledTime - _idleSince > 0.5f)
                    Play(_last);
            }
            else
            {
                AnythingPlaying();
            }
        }

        void UpdateStats()
        {
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, 0.1f);
            if (Time.unscaledTime < _statsAt)
                return;
            _statsAt = Time.unscaledTime + 0.25f;
            _shurikenParticles = 0;
            foreach (ParticleSystem system in FindObjectsOfType<ParticleSystem>())
                _shurikenParticles += system.particleCount;
            _graphParticles = 0;
            foreach (VisualEffect effect in FindObjectsOfType<VisualEffect>())
                _graphParticles += effect.aliveParticleCount;
            _controllers = FindObjectsOfType<SkillVfxPhaseController>().Length;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
        }

        // ---- panel

        void OnGUI()
        {
            const float width = 320f;
            GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height - 20f), GUI.skin.box);
            if (GUILayout.Button(_collapsed ? "▶ 特效调试场" : "▼ 特效调试场"))
                _collapsed = !_collapsed;
            if (_collapsed)
            {
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("特效（数字键 1-9）");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(Screen.height * 0.4f));
            for (int i = 0; i < _items.Count; i++)
            {
                Item item = _items[i];
                string key = i < 9 ? (i + 1) + ". " : "    ";
                string kind = item.kind == Kind.Projectile ? "投射" : item.kind == Kind.Buff ? "状态" : "通用";
                if (GUILayout.Button(key + item.label + "  [" + kind + "]", GUI.skin.button))
                    Play(item);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.enabled = _last != null;
            if (GUILayout.Button("重播 (R)")) Play(_last);
            GUI.enabled = true;
            if (GUILayout.Button("清空 (C)")) Clear();
            _loop = GUILayout.Toggle(_loop, "循环");
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("时间  " + (Paused ? "已暂停" : "×" + _timeScale.ToString("0.00")));
            float scale = GUILayout.HorizontalSlider(_timeScale, 0.05f, 2f);
            if (!Mathf.Approximately(scale, _timeScale))
            {
                _timeScale = scale;
                if (!Paused)
                    Time.timeScale = _timeScale;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.1×")) SetScale(0.1f);
            if (GUILayout.Button("0.25×")) SetScale(0.25f);
            if (GUILayout.Button("1×")) SetScale(1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Paused ? "继续 (Space)" : "暂停 (Space)")) TogglePause();
            if (GUILayout.Button("单帧 (.)")) Step();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("施放后冻结于 s（-1 关）", GUILayout.Width(170f));
            _freezeText = GUILayout.TextField(_freezeText);
            if (!float.TryParse(_freezeText, out _freezeAt))
                _freezeAt = -1f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("机位（Tab）");
            GUILayout.BeginHorizontal();
            for (int i = 0; cameras != null && i < cameras.Length; i++)
            {
                if (cameras[i] != null && GUILayout.Toggle(i == _camera, cameras[i].name.Replace("Cam_", ""), GUI.skin.button))
                {
                    if (i != _camera)
                        SelectCamera(i);
                }
            }
            GUILayout.EndHorizontal();
            if (FreeCameraActive)
                GUILayout.Label("右键拖动旋转，滚轮缩放");

            GUILayout.Space(6f);
            GUILayout.Label("特效质量（下一次播放生效）：" + VfxQuality.Current);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Low")) VfxQuality.Set(VfxQualityTier.Low);
            if (GUILayout.Button("Medium")) VfxQuality.Set(VfxQualityTier.Medium);
            if (GUILayout.Button("High")) VfxQuality.Set(VfxQualityTier.High);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _controllers > 0;
            if (GUILayout.Button("命中（占位接口）"))
            {
                foreach (SkillVfxPhaseController controller in FindObjectsOfType<SkillVfxPhaseController>())
                    controller.NotifyHit(controller.transform.position);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("帧耗时 " + _frameMs.ToString("0.0") + " ms");
            GUILayout.Label("粒子 " + _shurikenParticles + "（Shuriken） / " + _graphParticles + "（VFX Graph）");
            GUILayout.Label("特效控制器 " + _controllers);
            GUILayout.Label("Batches " + UnityStats.batches + "  SetPass " + UnityStats.setPassCalls + "  三角形 " + UnityStats.triangles);
            GUILayout.EndArea();
        }

        void SetScale(float scale)
        {
            _timeScale = scale;
            if (!Paused)
                Time.timeScale = scale;
        }
    }
}
#endif
