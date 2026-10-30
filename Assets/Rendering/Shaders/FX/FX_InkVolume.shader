// FX/InkVolume: the hand as a ray-marched volume of ink vapour (Docs/vfx/palm-mesh-tripo.md section 10). Drawn on
// a box (back faces, so it still works with the camera inside); every pixel marches through the box and sums ink.
// - Shape: a baked signed distance field of the hand (PalmVolumeBaker.BakeSdf). The fog lives in a band around the
//   surface: full at _BandCentre (slightly inside), gone _BandWidth away on either side, so the inside stays hollow
//   and the fog spills past the surface with a soft edge. Beyond the band only the strongest noise survives, which is
//   where the wisps come from.
// - Sides: the SDF gradient gives the surface direction; density is scaled from _BackDensity (object -Z) to 1 (+Z).
// - Vapour: a tiling 3D fbm noise (BakeNoise3D), domain-warped by itself and rising along +Y, thresholded softly so
//   the band breaks into curling tendrils that keep moving. Thin fog is grey, thick fog black (Beer-Lambert sum).
// - No depth texture: the volume does not clip against scene geometry; fine for a hand in the air.
// - Driven: _FxAlpha scales the extinction; _FxDissolve raises the noise threshold until nothing is left.
// - Cost (Docs/vfx/push-projectile-hands.md section 15): the SDF gradient (six samples) is taken per step only until
//   the ray first meets fog; after that the entry normal serves every step. Steps grow with the camera distance
//   (_LodNear.._LodFar, up to _LodStepScale x), since a far hand covers few pixels and cannot show finer steps.
Shader "FX/InkVolume"
{
    Properties
    {
        [Header(Volume)]
        [NoScaleOffset] _Sdf("Signed distance field (RHalf, object units)", 3D) = "" {}
        _VolumeOrigin("Volume origin (object space)", Vector) = (-42, -57, -29, 0)
        _VolumeSize("Volume size (object space)", Vector) = (84, 114, 58, 0)
        _SdfRange("SDF encoded range (object units, 0..1 = -range/2..+range/2)", Float) = 36
        _Steps("Max march steps", Range(8, 160)) = 96
        _StepMin("Step inside the band (object units)", Float) = 0.8
        _StepMax("Step away from the band (object units)", Float) = 6
        _Jitter("Step jitter", Range(0, 1)) = 0.5
        _LodNear("LOD: camera distance (world) where steps start to grow", Float) = 150
        _LodFar("LOD: camera distance (world) where steps reach the full scale", Float) = 700
        _LodStepScale("LOD: step scale at the far distance", Range(1, 6)) = 3
        _WristFade("Wrist fade (y from, y to)", Vector) = (-47, -25, 0, 0)

        [Header(Ink)]
        _InkColor("Ink", Color) = (0.01, 0.01, 0.015, 1)
        _ThinColor("Thin vapour", Color) = (0.5, 0.5, 0.53, 1)
        _Extinction("Extinction per unit (overall density)", Float) = 0.25
        _BandCentre("Band centre (SDF, negative = inside)", Float) = -1.5
        _BandWidth("Band width (object units)", Float) = 7
        _Spill("Spill: density just outside the band", Range(0, 1)) = 0.3
        _SpillFalloff("Spill falloff distance (object units)", Float) = 4
        _EdgeFade("Fade distance at the volume faces", Float) = 6
        _BackDensity("Back side (-Z) density", Range(0, 1)) = 0.3
        _Fill("Fill: 0 = shell around the surface, 1 = whole solid", Range(0, 1)) = 0
        _FillDepth("Depth the fog reaches inside at Fill 1 (object units)", Float) = 40
        _ContourFloor("Contour: minimum vapour along the silhouette", Range(0, 1)) = 0.5
        _ContourBoost("Contour: density boost along the silhouette", Range(0, 4)) = 1.5
        _RimPower("Contour sharpness", Range(0.5, 8)) = 2
        _LayerSpacing("Inner contour layers: spacing (object units)", Float) = 6
        _LayerWidth("Inner contour layers: thickness", Float) = 1.2
        _LayerStrength("Inner contour layers: density", Range(0, 1)) = 0.8
        _InnerSolid("Inside: 0 = contour layers only, 1 = solid fog", Range(0, 1)) = 0.2
        _NoiseStretch("Noise stretch (x, y, z frequency scale)", Vector) = (1, 0.35, 1, 0)

        [Header(Vapour noise)]
        [NoScaleOffset] _Noise("3D noise (R, tiling)", 3D) = "" {}
        _NoiseTiling("Noise tiling (per object unit)", Float) = 0.02
        _NoiseDetail("Detail tiling multiplier", Float) = 2.7
        _NoiseDetailWeight("Detail weight", Range(0, 1)) = 0.4
        _NoiseLow("Noise value mapped to 0", Range(0, 1)) = 0.35
        _NoiseHigh("Noise value mapped to 1", Range(0, 1)) = 0.75
        _Threshold("Vapour threshold", Range(0, 1)) = 0.35
        _Softness("Threshold softness", Range(0.01, 1)) = 0.4
        _Warp("Domain warp (object units)", Float) = 10
        _Rise("Rise speed (object units per second)", Float) = 6
        _Churn("Warp drift speed", Float) = 0.05

        [Header(Driven)]
        _FxAlpha("Alpha (driven)", Range(0, 1)) = 1
        _FxDissolve("Dissolve (driven)", Range(0, 1)) = 0
        [Header(Form)]
        _ShadeStrength("Shading by surface normal (0 = flat, 1 = full light / shadow side)", Range(0, 1)) = 0.6
        _ShadeLightDir("Light direction (object space, for the shading)", Vector) = (0.4, 0.8, 0.5, 0)
        _RimDark("Rim darkening (silhouette edges go dark so the form reads)", Range(0, 1)) = 0.5
        _RimDarkPower("Rim darkening width (higher = thinner edge)", Range(0.5, 8)) = 2
        [Header(Emission)]
        [HDR] _EmissionColor("Emission colour (HDR; > 1 feeds bloom)", Color) = (1, 0.74, 0.42, 1)
        _EmissionStrength("Emission strength (0 = off)", Range(0, 8)) = 0
        _EmissionInner("Emission depth: how deep inside the hand (object units) the glow reaches full strength; the skin stays dark", Range(1, 40)) = 12

        [Header(Scene depth)]
        [Toggle(_INKVOLUME_DEPTHCLIP)] _DepthClipOn("Clip the march at scene depth (needs the pipeline's Depth Texture)", Float) = 1

        [Header(Heat haze)]
        [Toggle(_INKVOLUME_HAZE)] _HazeOn("Heat haze (needs the pipeline's Opaque Texture)", Float) = 0
        _HazeStrength("Haze amount per unit of fog thickness", Float) = 0.15
        _HazeScale("Haze offset (screen fraction)", Float) = 0.02
        _HazeTiling("Haze noise tiling (per object unit)", Float) = 0.06
        _HazeSpeed("Haze rise speed (object units per second)", Float) = 25
        _Debug("Debug: 1 = shape only", Float) = 0
        _TimeOffset("Time offset (for stills)", Float) = 0
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE3D(_Sdf); SAMPLER(sampler_Sdf);
    TEXTURE3D(_Noise); SAMPLER(sampler_Noise);

    CBUFFER_START(UnityPerMaterial)
        float4 _VolumeOrigin;
        float4 _VolumeSize;
        float _SdfRange;
        float _Steps;
        float _StepMin;
        float _StepMax;
        float _Jitter;
        float _LodNear;
        float _LodFar;
        float _LodStepScale;
        float4 _WristFade;
        half4 _InkColor;
        half4 _ThinColor;
        float _Extinction;
        float _BandCentre;
        float _BandWidth;
        half _Spill;
        float _SpillFalloff;
        float _EdgeFade;
        half _BackDensity;
        half _Fill;
        float _FillDepth;
        half _ContourFloor;
        half _ContourBoost;
        half _RimPower;
        float _LayerSpacing;
        float _LayerWidth;
        half _LayerStrength;
        half _InnerSolid;
        float4 _NoiseStretch;
        float _NoiseTiling;
        float _NoiseDetail;
        half _NoiseDetailWeight;
        half _NoiseLow;
        half _NoiseHigh;
        half _Threshold;
        half _Softness;
        float _Warp;
        float _Rise;
        float _Churn;
        half _FxAlpha;
        half _FxDissolve;
        half4 _EmissionColor;
        half _EmissionStrength;
        half _EmissionInner;
        half _ShadeStrength;
        float4 _ShadeLightDir;
        half _RimDark;
        half _RimDarkPower;
        float _HazeStrength;
        float _HazeScale;
        float _HazeTiling;
        float _HazeSpeed;
        half _Debug;
        float _TimeOffset;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionOS : TEXCOORD0;
        float3 cameraOS : TEXCOORD1;
        float fogFactor : TEXCOORD2;
        float lodScale : TEXCOORD3;
    };

    // The box mesh is a unit cube (-0.5..0.5); the material maps it onto the volume by origin + size in the hand's
    // object units. All marching happens in hand units ("volume space").
    float3 BoxToVolume(float3 p) { return _VolumeOrigin.xyz + (p + 0.5) * _VolumeSize.xyz; }

    Varyings InkVolumeVert(Attributes input)
    {
        Varyings output;
        VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = positions.positionCS;
        output.positionOS = BoxToVolume(input.positionOS.xyz);
        output.cameraOS = BoxToVolume(TransformWorldToObject(GetCameraPositionWS()));
        output.fogFactor = ComputeFogFactor(positions.positionCS.z);
        float camDist = length(GetCameraPositionWS() - TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
        output.lodScale = lerp(1.0, max(_LodStepScale, 1.0), smoothstep(_LodNear, max(_LodFar, _LodNear + 1.0), camDist));
        return output;
    }

    float SampleSdf(float3 p)
    {
        float3 uv = (p - _VolumeOrigin.xyz) / _VolumeSize.xyz;
        return (SAMPLE_TEXTURE3D_LOD(_Sdf, sampler_Sdf, uv, 0).r - 0.5) * _SdfRange;
    }

    half SampleNoise(float3 p) { return SAMPLE_TEXTURE3D_LOD(_Noise, sampler_Noise, p, 0).r; }

    // Ray / axis-aligned box in volume space; returns (tNear, tFar).
    float2 RayBox(float3 ro, float3 rd, float3 bmin, float3 bmax)
    {
        float3 inv = 1.0 / rd;
        float3 t0 = (bmin - ro) * inv;
        float3 t1 = (bmax - ro) * inv;
        float3 tmin = min(t0, t1);
        float3 tmax = max(t0, t1);
        return float2(max(max(tmin.x, tmin.y), tmin.z), min(min(tmax.x, tmax.y), tmax.z));
    }

    float Density(float3 p, float sdf, float3 rd, float t, float3 n)
    {
        float outside = max(sdf - (_BandCentre + _BandWidth), 0.0);
        if (outside > _SpillFalloff * 4.0) return 0.0;

        // Shape: a band around the surface, then an exponential spill outside where only the strongest noise
        // survives (the threshold climbs with distance), so wisps leave the surface and thin out. Inward the band
        // reaches _BandWidth at _Fill 0 (hollow) and _BandWidth + _FillDepth at _Fill 1 (the whole solid is fog).
        float d = sdf - _BandCentre;
        float inner = _BandWidth + _Fill * _FillDepth;
        // Surface shell, then inside it (as deep as _Fill allows) either nested iso-shells of the SDF, every
        // _LayerSpacing units and _LayerWidth thick (contour strokes through the whole solid), or solid fog, blended
        // by _InnerSolid.
        float shell = d > 0.0 ? saturate(1.0 - d / _BandWidth) : saturate(1.0 + d / _BandWidth);
        float depthMask = d < 0.0 ? saturate(1.0 + d / inner) : 0.0;
        float layerDist = abs(frac(-d / _LayerSpacing + 0.5) - 0.5) * _LayerSpacing;
        float layers = saturate(1.0 - layerDist / _LayerWidth) * step(_BandWidth, -d) * _LayerStrength;
        float innerFog = lerp(layers, 1.0, _InnerSolid) * depthMask;
        float band = max(shell, innerFog);
        band = band * band * (3.0 - 2.0 * band);
        float spill = _Spill * exp(-outside / _SpillFalloff) * (1.0 - band) * step(0.0, sdf - _BandCentre);
        float shape = band + spill;
        float farness = saturate(outside / (_SpillFalloff * 2.0));
        // Fade at the volume's own faces so nothing is cut flat at the box.
        float3 toEdge = min(p - _VolumeOrigin.xyz, _VolumeOrigin.xyz + _VolumeSize.xyz - p);
        shape *= saturate(min(min(toEdge.x, toEdge.y), toEdge.z) / _EdgeFade);
        // The wrist is a flat cut: toward it the vapour tears into tendrils (threshold climbs) and thins a little,
        // instead of ending in a slab.
        float wristness = 1.0 - smoothstep(_WristFade.x, _WristFade.y, p.y);
        shape *= 1.0 - wristness * 0.5;

        // Surface direction from the SDF gradient (n, taken by the caller): z says palm (+) or back (-); the whole
        // vector against the ray gives the silhouette (grazing = contour), which keeps the form readable without
        // drawing a surface.
        float palm = saturate(n.z * 0.5 + 0.5);
        float side = lerp(_BackDensity, 1.0, palm);
        float rim = pow(1.0 - abs(dot(n, rd)), _RimPower);
        float contour = band * rim;

        // Vapour: warped, rising fbm noise through a soft threshold.
        float3 flow = float3(0.0, -t * _Rise, 0.0);
        float3 wp = (p + flow * 0.5) * _NoiseTiling * 0.5 + t * _Churn;
        float3 warp = float3(SampleNoise(wp), SampleNoise(wp.yzx + 0.37), SampleNoise(wp.zxy + 0.71)) - 0.5;
        // Streaks: the noise is stretched along _NoiseStretch (features elongate where the value is small).
        float3 np = (p + flow + warp * _Warp) * _NoiseTiling * _NoiseStretch.xyz;
        float n2 = SampleNoise(np);
        float dn = SampleNoise(np * _NoiseDetail + 0.23);
        n2 = lerp(n2, n2 * 0.5 + dn * 0.5, _NoiseDetailWeight);
        n2 = saturate((n2 - _NoiseLow) / max(_NoiseHigh - _NoiseLow, 1e-3));
        float threshold = _Threshold + saturate(farness * 0.8 + wristness * 0.7 + _FxDissolve) * (1.0 - _Threshold);
        float vapour = smoothstep(threshold, threshold + _Softness, n2);
        // Contour: along the silhouette the fog never fully breaks up, and it is boosted so the outline reads.
        // (the wrist cut gets no contour: its rim would draw a ring)
        contour *= 1.0 - wristness;
        vapour = max(vapour, contour * _ContourFloor);
        float boost = 1.0 + contour * _ContourBoost;
        if (_Debug > 0.5) return shape * side;

        return shape * side * vapour * boost;
    }

    half4 InkVolumeFrag(Varyings input) : SV_Target
    {
        float3 ro = input.cameraOS;
        float3 rd = normalize(input.positionOS - ro);
        float3 bmin = _VolumeOrigin.xyz;
        float3 bmax = _VolumeOrigin.xyz + _VolumeSize.xyz;
        float2 tb = RayBox(ro, rd, bmin, bmax);
        float tStart = max(tb.x, 0.0);
        float tEnd = tb.y;
#if defined(_INKVOLUME_DEPTHCLIP)
        // The pass uses ZTest Always (its back faces may sit behind the floor), so occlusion is done here: stop the
        // march where the scene's depth is, converted from world distance to volume units along this ray.
        float2 depthUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float sceneEye = LinearEyeDepth(SampleSceneDepth(depthUV), _ZBufferParams);
        float3 rdWS = normalize(mul((float3x3)unity_ObjectToWorld, rd / _VolumeSize.xyz));
        float sceneDist = sceneEye / max(dot(rdWS, -UNITY_MATRIX_V[2].xyz), 1e-4);
        float worldPerUnit = length(mul((float3x3)unity_ObjectToWorld, rd / _VolumeSize.xyz));
        tEnd = min(tEnd, sceneDist / max(worldPerUnit, 1e-5));
#endif
        if (tEnd <= tStart) discard;

        // Adaptive march: small steps inside the band (_StepMin), long ones away from it (up to _StepMax), so a
        // thin shell is sampled finely without paying for the empty box.
        int steps = (int)_Steps;
        float stepMin = _StepMin * input.lodScale, stepMax = _StepMax * input.lodScale;
        float jitter = frac(sin(dot(input.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453) * _Jitter;
        float t = tStart + stepMin * jitter;
        float time = _Time.y + _TimeOffset;

        float transmittance = 1.0;
        float thick = 0.0;
        float glow = 0.0;
        float3 entryN = 0.0;
        bool entrySet = false;
        float3 hazeP = ro + rd * (tStart + tEnd) * 0.5;
        bool hazeSet = false;
        for (int i = 0; i < steps; i++)
        {
            if (t >= tEnd) break;
            float3 p = ro + rd * t;
            float sdf = SampleSdf(p);
            if (!hazeSet && abs(sdf - _BandCentre) < _BandWidth * 3.0) { hazeP = p; hazeSet = true; }
            float toBand = max(abs(sdf - _BandCentre) - _BandWidth, 0.0);
            float stepLen = clamp(toBand * 0.6, stepMin, stepMax);
            stepLen = min(stepLen, tEnd - t);
            // SDF normal: per step until the ray first meets fog and whenever the sample is near the surface band (the
            // silhouette contour and the palm / back split need the local normal there: with a frozen normal the form
            // went flat); elsewhere (deep inside, far outside) the entry normal is reused, which is also the one normal
            // the whole pixel is shaded by.
            float3 n = entryN;
            bool nearSurface = abs(sdf - _BandCentre) < _BandWidth * 2.5;
            if (!entrySet || nearSurface)
            {
                float3 g = float3(SampleSdf(p + float3(1.5, 0, 0)) - SampleSdf(p - float3(1.5, 0, 0)),
                                  SampleSdf(p + float3(0, 1.5, 0)) - SampleSdf(p - float3(0, 1.5, 0)),
                                  SampleSdf(p + float3(0, 0, 1.5)) - SampleSdf(p - float3(0, 0, 1.5)));
                n = normalize(g + 1e-5);
            }
            float dens = Density(p, sdf, rd, time, n);
            if (!entrySet && dens > 0.05) { entryN = n; entrySet = true; }
            float ext = dens * _Extinction * _FxAlpha * stepLen;
            // emission-absorption: fog deep inside the hand radiates, seen through whatever fog is in front of it, so
            // the dense skin and the thin fingers stay ink-dark and the thick core glows (外黑内亮)
            // emissive radiance per unit length: full deep inside, half at the skin, fading out through the spill
            float em = saturate(-sdf / max(_EmissionInner, 0.01));   // 0 at the skin, 1 at depth: the glow sits inside
            glow += transmittance * (1.0 - exp(-ext)) * em * em;   // <= alpha; strength > 1 pushes it into HDR
            transmittance *= exp(-ext);
            thick += dens * stepLen;
            t += stepLen;
            if (transmittance < 0.01) break;
        }

        half alpha = saturate(1.0 - transmittance);
        // Thin fog reads grey, thick fog black.
        half darkness = saturate(thick * _Extinction * 0.6);
        half3 color = lerp(_ThinColor.rgb, _InkColor.rgb, darkness);
        // Form: light / shadow side from the entry normal, and dark silhouette edges; neither touches the emission.
        if (entrySet)
        {
            float3 L = normalize(_ShadeLightDir.xyz + 1e-5);
            half lambert = saturate(dot(entryN, L));
            color *= lerp(1.0, 0.45 + 0.75 * lambert, _ShadeStrength);
            half rimDark = pow(saturate(1.0 - abs(dot(entryN, rd))), _RimDarkPower);
            color *= 1.0 - _RimDark * rimDark;
        }
        // Self-emission accumulated in the march above (glow <= alpha). HDR colour x strength so bloom can catch a hot core.
        // additive, HDR: not limited by the fog's alpha, so it spills past the body and feeds bloom
        half3 emis = _EmissionColor.rgb * (_EmissionStrength * glow * _FxAlpha);
        color = MixFog(color, input.fogFactor);
#if defined(_INKVOLUME_HAZE)
        // Heat haze: the scene behind the fog is read through a rising noise offset (like the air above a fire) and the
        // ink is composited over it; the box then writes the (identical) scene colour where there is no fog.
        float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float haze = saturate(thick * _HazeStrength) * _FxAlpha;
        float3 hp = hazeP * _HazeTiling + float3(0.0, -time * _HazeSpeed * _HazeTiling, 0.0);
        float2 offset = (float2(SampleNoise(hp), SampleNoise(hp + 0.5)) - 0.5) * _HazeScale * haze;
        float3 scene = SampleSceneColor(screenUV + offset);
        // Write only where there is ink or haze; elsewhere alpha stays 0 so the box leaves the frame untouched.
        float cover = max(alpha, haze);
        return half4(lerp(scene, color, alpha / max(cover, 1e-4)) * cover + emis, cover);   // premultiplied
#else
        return half4(color * alpha + emis, alpha);   // premultiplied
#endif
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            Name "InkVolume"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex InkVolumeVert
            #pragma fragment InkVolumeFrag
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _INKVOLUME_HAZE
            #pragma shader_feature_local_fragment _INKVOLUME_DEPTHCLIP
            #pragma target 3.5
            ENDHLSL
        }
    }
    Fallback Off
}
