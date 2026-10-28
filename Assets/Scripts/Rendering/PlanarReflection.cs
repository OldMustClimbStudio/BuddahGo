using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering
{
    // Planar reflection for a flat glossy floor (RaceMap's lane top and end-field floor), placed on the floor's object.
    // The plane is the receivers' top surface (their bounds), so it follows the map when it moves. For each base game camera that
    // sees the receivers from above, a mirrored camera renders the scene above the plane into a texture, and the
    // receivers' BuddahGo/Lit material (Planar Reflection on, keyword _PLANAR_REFLECTION) samples it in screen space in
    // place of the reflection probe. Screen-space reflections speckled and broke off on the lane at every resolution; a
    // mirror render is exact for a plane and costs one extra scene draw without shadows or post-processing.
    // The mirrored view uses the "URP-PlanarReflection-Renderer" (no SSAO, SSR or overlay passes) when the current
    // pipeline lists it, else the default renderer. A resolution scale of 0 turns it off and leaves the probe reflection;
    // the graphics settings set the scale through SettingsScale.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PlanarReflection : MonoBehaviour
    {
        public const string RendererName = "URP-PlanarReflection-Renderer";

        static readonly int s_TextureId = Shader.PropertyToID("_PlanarReflectionTexture");
        static readonly int s_ParamsId = Shader.PropertyToID("_PlanarReflectionParams");
        static readonly FieldInfo s_RendererDataList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);

        [Tooltip("Renderers whose top surface is the reflecting plane; their BuddahGo/Lit material has Planar Reflection on.")]
        [SerializeField] Renderer[] _receivers = new Renderer[0];
        [Tooltip("Reflection texture size relative to the camera's render size. The graphics settings override it; 0 is off.")]
        [SerializeField, Range(0f, 1f)] float _resolutionScale = 1f;
        [Tooltip("Blur, in mip levels per unit of perceptual roughness. The lane (smoothness 0.93) stays nearly sharp.")]
        [SerializeField, Min(0f)] float _roughnessMips = 8f;
        [Tooltip("Screen-space offset per unit of normal-map tilt, so the normal map bends the reflection.")]
        [SerializeField, Min(0f)] float _distortion = 0.04f;
        [Tooltip("Height above the plane where the mirrored view starts clipping, so the receivers never reflect themselves.")]
        [SerializeField, Min(0f)] float _clipOffset = 0.01f;
        [Tooltip("Layers the mirrored view draws.")]
        [SerializeField] LayerMask _reflectLayers = ~(1 << 5);

        /// <summary>Resolution scale chosen in the graphics settings (0 = off); null keeps each component's own scale.</summary>
        public static float? SettingsScale { get; set; }

        Camera _mirror;
        RenderTexture _texture;
        MaterialPropertyBlock _block;
        static UniversalRenderPipelineAsset s_IndexedAsset;
        static int s_RendererIndex = -1;

        float _planeHeight;

        float Scale => Mathf.Clamp01(SettingsScale ?? _resolutionScale);

        void Reset()
        {
            if (TryGetComponent(out Renderer own))
                _receivers = new[] { own };
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Bind(null, 0f);
            foreach (var receiver in _receivers)
                if (receiver != null)
                    receiver.SetPropertyBlock(null);
            if (_texture != null)
            {
                _texture.Release();
                DestroyImmediate(_texture);
                _texture = null;
            }
            if (_mirror != null)
            {
                DestroyImmediate(_mirror.gameObject);
                _mirror = null;
            }
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _mirror)
                return;
            // Every camera sets the receivers' state before it draws them: the scene view and cameras that do not see the
            // plane fall back to the probe, so none of them samples another camera's mirror image.
            if (!ShouldReflect(camera))
            {
                Bind(null, 0f);
                return;
            }
            RenderMirror(context, camera);
            Bind(_texture, 1f);
        }

        bool ShouldReflect(Camera camera)
        {
            if (Scale <= 0f || camera.cameraType != CameraType.Game || _receivers.Length == 0)
                return false;
            if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType != CameraRenderType.Base)
                return false;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            bool visible = false;
            foreach (var receiver in _receivers)
            {
                if (receiver == null || !receiver.enabled || !GeometryUtility.TestPlanesAABB(planes, receiver.bounds))
                    continue;
                _planeHeight = visible ? Mathf.Max(_planeHeight, receiver.bounds.max.y) : receiver.bounds.max.y;
                visible = true;
            }
            return visible && camera.transform.position.y > _planeHeight;
        }

        void RenderMirror(ScriptableRenderContext context, Camera camera)
        {
            var asset = UniversalRenderPipeline.asset;
            float renderScale = asset != null ? asset.renderScale : 1f;
            int width = Mathf.Max(16, Mathf.RoundToInt(camera.pixelWidth * renderScale * Scale));
            int height = Mathf.Max(16, Mathf.RoundToInt(camera.pixelHeight * renderScale * Scale));
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                {
                    _texture.Release();
                    DestroyImmediate(_texture);
                }
                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = "PlanarReflection " + name,
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = true,
                    autoGenerateMips = true,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _texture.Create();
            }

            var mirror = Mirror();
            mirror.CopyFrom(camera);
            mirror.enabled = false;
            mirror.cameraType = CameraType.Game;
            mirror.targetTexture = _texture;
            mirror.cullingMask = camera.cullingMask & _reflectLayers;
            mirror.allowMSAA = false;
            mirror.allowHDR = true;
            mirror.useOcclusionCulling = false;
            mirror.rect = new Rect(0f, 0f, 1f, 1f);

            // Mirror the view about the plane y = h, and clip everything below the plane with an oblique near plane.
            var reflection = Matrix4x4.identity;
            reflection.m11 = -1f;
            reflection.m13 = 2f * _planeHeight;
            mirror.worldToCameraMatrix = camera.worldToCameraMatrix * reflection;
            var position = camera.transform.position;
            mirror.transform.SetPositionAndRotation(new Vector3(position.x, 2f * _planeHeight - position.y, position.z), camera.transform.rotation);
            var view = mirror.worldToCameraMatrix;
            var planePoint = view.MultiplyPoint(new Vector3(0f, _planeHeight + _clipOffset, 0f));
            var planeNormal = view.MultiplyVector(Vector3.up).normalized;
            mirror.projectionMatrix = camera.CalculateObliqueMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planePoint, planeNormal)));

            var data = mirror.GetComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(RendererIndex(asset));

            // The mirrored matrix flips triangle winding.
            GL.invertCulling = true;
            try
            {
#pragma warning disable CS0618 // RenderSingleCamera is the URP 14 API for an extra camera inside beginCameraRendering.
                UniversalRenderPipeline.RenderSingleCamera(context, mirror);
#pragma warning restore CS0618
            }
            finally
            {
                GL.invertCulling = false;
            }
        }

        Camera Mirror()
        {
            if (_mirror != null)
                return _mirror;
            var go = new GameObject("PlanarReflectionCamera " + name) { hideFlags = HideFlags.HideAndDontSave };
            _mirror = go.AddComponent<Camera>();
            _mirror.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.volumeLayerMask = 0;
            return _mirror;
        }

        void Bind(Texture texture, float strength)
        {
            _block ??= new MaterialPropertyBlock();
            var parameters = new Vector4(strength, _planeHeight, _roughnessMips, _distortion);
            foreach (var receiver in _receivers)
            {
                if (receiver == null)
                    continue;
                receiver.GetPropertyBlock(_block);
                _block.SetTexture(s_TextureId, texture != null ? texture : Texture2D.blackTexture);
                _block.SetVector(s_ParamsId, parameters);
                receiver.SetPropertyBlock(_block);
            }
        }

        // Index of the planar-reflection renderer in the current pipeline (the graphics settings may have swapped in a
        // runtime copy, whose renderer data keep the name with a suffix); -1 is the default renderer.
        static int RendererIndex(UniversalRenderPipelineAsset asset)
        {
            if (asset == s_IndexedAsset)
                return s_RendererIndex;
            s_IndexedAsset = asset;
            s_RendererIndex = -1;
            if (asset != null && s_RendererDataList?.GetValue(asset) is ScriptableRendererData[] list)
                for (int i = 0; i < list.Length; i++)
                    if (list[i] != null && list[i].name.StartsWith(RendererName, System.StringComparison.Ordinal))
                        s_RendererIndex = i;
            return s_RendererIndex;
        }
    }
}
