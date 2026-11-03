using System.Collections.Generic;
using UnityEngine;

// Draws the 一念光明 curtain for the camera it sits on (BlackCurtainViewController adds it): the H1 墨眼 opening over
// the table's expand time, then the full-black hold and the fade-out. Screen part: FX/CurtainEye, drawn by
// BlackCurtainRenderFeature while this plays; world part: the profile's world layer, following the caster. The anti
// skill uses the red palette and the red world layer. Who sees edges, racers and trails stays with the view controller.
public class BlackCurtainScreenEffect : MonoBehaviour
{
    private static readonly List<BlackCurtainScreenEffect> Playing = new List<BlackCurtainScreenEffect>();

    private BlackCurtainEyeProfile _profile;
    private Material _material;
    private Camera _camera;
    private Transform _caster;
    private Transform _scaleReference;
    private GameObject _world;
    private float _expand;
    private float _hold;
    private float _fade;
    private float _maxOpacity;
    private bool _anti;
    private float _startedAt;
    private bool _playing;
    private float _baseFov;
    private Vector3 _shakeOffset;

    public bool IsPlaying => _playing;
    public float Elapsed => _playing ? Time.time - _startedAt : 0f;
    public float ExpandSeconds => _expand;
    public float HoldSeconds => _hold;
    public float FadeSeconds => _fade;

    // The material BlackCurtainRenderFeature draws for this camera; false while no curtain is up.
    public static bool TryGetMaterial(Camera camera, out Material material)
    {
        for (int i = 0; i < Playing.Count; i++)
        {
            if (Playing[i]._camera == camera)
            {
                material = Playing[i]._material;
                return material != null;
            }
        }
        material = null;
        return false;
    }

    // caster: where the eye sits (in a race, the smoothed VisualRoot). scaleReference: whose scale grows the eye's
    // offset (the racer's root, which PlayerScaleEffect scales); null = the caster itself.
    public void Play(BlackCurtainEyeProfile profile, Transform caster, Transform scaleReference, float expandDuration,
        float holdDuration, float fadeOutDuration, float maxOpacity, bool anti)
    {
        Stop();
        _camera = GetComponent<Camera>();
        if (profile == null || profile.shader == null || _camera == null)
        {
            Debug.LogWarning("[BlackCurtainScreenEffect] Missing eye profile, shader or camera; no curtain drawn.");
            return;
        }
        if (_material == null || _material.shader != profile.shader)
        {
            if (_material != null)
                Destroy(_material);
            _material = new Material(profile.shader) { name = "CurtainEye (instance)", hideFlags = HideFlags.DontSave };
        }

        _profile = profile;
        _caster = caster;
        _scaleReference = scaleReference != null ? scaleReference : caster;
        _expand = Mathf.Max(0.05f, expandDuration);
        _hold = Mathf.Max(0f, holdDuration);
        _fade = Mathf.Max(0f, fadeOutDuration);
        _maxOpacity = Mathf.Clamp01(maxOpacity);
        _anti = anti;
        _baseFov = _camera.fieldOfView;
        _startedAt = Time.time;
        _playing = true;
        Playing.Add(this);
        SpawnWorldLayer(anti ? profile.worldLayerAnti : profile.worldLayer);
        Apply(0f, 0f);
    }

    public void Stop()
    {
        if (_playing && _camera != null && _profile != null && _profile.cameraKick)
            _camera.fieldOfView = _baseFov;
        RemoveShake();
        if (_world != null)
            Destroy(_world);
        _world = null;
        _playing = false;
        Playing.Remove(this);
    }

    private void SpawnWorldLayer(GameObject prefab)
    {
        if (prefab == null || _caster == null)
            return;
        _world = Instantiate(prefab, _caster.position, Quaternion.identity);
        _world.name = prefab.name;
        int characterLayer = LayerMask.NameToLayer(BuddahGo.VFX.SkillVfxPhaseController.CharacterLayerName);
        if (characterLayer >= 0)
        {
            foreach (Transform t in _world.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = characterLayer;
        }
        float speed = 1f / _expand;
        foreach (ParticleSystem ps in _world.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed *= speed;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    private void Update()
    {
        RemoveShake();
    }

    private void LateUpdate()
    {
        if (!_playing)
            return;
        float elapsed = Time.time - _startedAt;
        if (elapsed >= _expand + _hold + _fade)
        {
            Stop();
            return;
        }
        if (_world != null && _caster != null)
            _world.transform.position = _caster.position;
        Apply(elapsed / _expand, elapsed);
    }

    private void Apply(float p, float elapsed)
    {
        Material m = _material;
        m.SetVector(Ids.Center, ResolveCenter());
        m.SetFloat(Ids.Aspect, _camera.aspect);
        m.SetFloat(Ids.Time, Time.time);
        m.SetFloat(Ids.FlashCap, _profile.flashCap);
        m.SetFloat(Ids.AntiMode, _anti ? 1f : 0f);
        m.SetFloat(Ids.MaxOpacity, _maxOpacity);

        if (p >= 1f)
        {
            SetEye(m, default);
            float fadeStart = _expand + _hold;
            float black = elapsed < fadeStart || _fade <= 0f ? 1f : 1f - Mathf.Clamp01((elapsed - fadeStart) / _fade);
            m.SetFloat(Ids.Black, black);
            if (_profile.cameraKick)
                _camera.fieldOfView = _baseFov;
            return;
        }

        m.SetFloat(Ids.Black, 0f);
        SetEye(m, _profile.Evaluate(p, Time.time));
        if (_profile.cameraKick)
            ApplyCameraKick(p);
    }

    private static void SetEye(Material m, BlackCurtainEyeProfile.EyeParams e)
    {
        m.SetFloat(Ids.HoleRadius, e.holeRadius);
        m.SetFloat(Ids.Lens, e.lens);
        m.SetFloat(Ids.Twirl, e.twirl);
        m.SetFloat(Ids.RadialBlur, e.radialBlur);
        m.SetFloat(Ids.Bulge, e.bulge);
        m.SetFloat(Ids.Ring, e.ring);
        m.SetFloat(Ids.RingPhase, e.ringPhase);
        m.SetFloat(Ids.Spiral, e.spiral);
        m.SetFloat(Ids.Dispersion, e.dispersion);
        m.SetFloat(Ids.Edge, e.edge);
        m.SetFloat(Ids.CoreFlash, e.coreFlash);
    }

    // Caster on screen; behind the camera it is pushed to the screen edge in its direction.
    private Vector4 ResolveCenter()
    {
        if (_caster == null)
            return new Vector4(0.5f, 0.5f, 0f, 0f);
        float scale = _scaleReference != null ? Mathf.Abs(_scaleReference.lossyScale.y) : 1f;
        Vector3 v = _camera.WorldToViewportPoint(_caster.position + _profile.centerWorldOffset * scale);
        if (v.z < 0f)
        {
            Vector2 dir = new Vector2(0.5f - v.x, 0.5f - v.y);
            float k = 0.5f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y), 1e-4f);
            return new Vector4(0.5f + dir.x * k, 0.5f + dir.y * k, 0f, 0f);
        }
        return new Vector4(Mathf.Clamp(v.x, -0.1f, 1.1f), Mathf.Clamp(v.y, -0.1f, 1.1f), 0f, 0f);
    }

    private void ApplyCameraKick(float p)
    {
        float push = Smooth(0.15f, 0.55f, p) * _profile.pushDegrees;
        float kick = Bell(p, 0.55f, 0.58f, 0.75f) * _profile.pushDegrees * 0.75f;
        _camera.fieldOfView = _baseFov - push + kick;
        float amplitude = _profile.shakeAmplitude;
        float shake = p >= 0.56f && p < 0.7f ? amplitude * (1f - (p - 0.56f) / 0.14f) : 0f;
        if (shake > 0f)
        {
            _shakeOffset = _camera.transform.right * (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f) * 2f * shake
                         + _camera.transform.up * (Mathf.PerlinNoise(0f, Time.time * 40f) - 0.5f) * 2f * shake;
            _camera.transform.position += _shakeOffset;
        }
    }

    private void RemoveShake()
    {
        if (_shakeOffset == Vector3.zero || _camera == null)
            return;
        _camera.transform.position -= _shakeOffset;
        _shakeOffset = Vector3.zero;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Bell(float p, float a, float b, float c)
    {
        if (p < a || p > c)
            return 0f;
        return p < b ? Smooth(a, b, p) : 1f - Smooth(b, c, p);
    }

    private static class Ids
    {
        public static readonly int Center = Shader.PropertyToID("_EyeCenter");
        public static readonly int Aspect = Shader.PropertyToID("_EyeAspect");
        public static readonly int Time = Shader.PropertyToID("_EyeTime");
        public static readonly int HoleRadius = Shader.PropertyToID("_HoleRadius");
        public static readonly int Lens = Shader.PropertyToID("_Lens");
        public static readonly int Twirl = Shader.PropertyToID("_Twirl");
        public static readonly int RadialBlur = Shader.PropertyToID("_RadialBlur");
        public static readonly int Bulge = Shader.PropertyToID("_Bulge");
        public static readonly int Ring = Shader.PropertyToID("_Ring");
        public static readonly int RingPhase = Shader.PropertyToID("_RingPhase");
        public static readonly int Spiral = Shader.PropertyToID("_Spiral");
        public static readonly int Dispersion = Shader.PropertyToID("_Dispersion");
        public static readonly int Edge = Shader.PropertyToID("_Edge");
        public static readonly int CoreFlash = Shader.PropertyToID("_CoreFlash");
        public static readonly int Black = Shader.PropertyToID("_Black");
        public static readonly int FlashCap = Shader.PropertyToID("_FlashCap");
        public static readonly int AntiMode = Shader.PropertyToID("_AntiMode");
        public static readonly int MaxOpacity = Shader.PropertyToID("_MaxOpacity");
    }
}
