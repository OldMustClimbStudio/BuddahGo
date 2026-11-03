// FX/InkFluid3D: ray-marches the InkFluid3D density volume (Docs/vfx/palm-mesh-tripo.md section 19) as wet ink.
// Drawn on the hidden world box InkFluidVolume keeps at the grid; the box is axis-aligned so the ray / box test is a
// plain slab test. Density -> threshold -> contrast -> extinction (Beer-Lambert), front to back; wash colour where
// thin, ink where thick. Stops at scene depth (needs the pipeline's Depth Texture). Premultiplied output.
// Cost: the ray is clipped to the occupied AABB the simulation records (_Bounds, cells), so pixels over the empty part
// of the box discard at once and the steps are spent on the ink; the step count follows the clipped length (at least
// _MinStepCells cells per step) up to _Steps. Docs/vfx/push-projectile-hands.md section 15.
Shader "FX/InkFluid3D"
{
    Properties
    {
        _InkColor("Ink", Color) = (0.11, 0.11, 0.094, 1)
        _WashColor("Wash", Color) = (0.22, 0.204, 0.157, 1)
        _Threshold("Threshold (density below this is not drawn)", Range(0, 0.5)) = 0.03
        _Contrast("Contrast", Range(0.3, 3)) = 1.1
        _Softness("Edge softness (density range over which the threshold fades in)", Range(0.005, 0.3)) = 0.06
        _Extinction("Extinction per world unit at full density", Float) = 0.35
        _Steps("Ray steps (max)", Range(16, 160)) = 64
        _MinStepCells("Shortest step (cells): fewer steps on short ray segments", Range(0.5, 4)) = 1.5
        _EdgeFade("Border fade (fraction of the box)", Range(0.01, 0.4)) = 0.08
        _FxAlpha("Layer alpha (timeline)", Range(0, 1)) = 1
        _FxDissolve("Layer dissolve (timeline)", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE3D(_DensityTex); SAMPLER(sampler_DensityTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _InkColor;
        half4 _WashColor;
        half _Threshold;
        half _Contrast;
        half _Softness;
        float _Extinction;
        float _Steps;
        float _MinStepCells;
        half _EdgeFade;
        half _FxAlpha;
        half _FxDissolve;
    CBUFFER_END

    float3 _BoxMin;     // per instance (property block)
    float3 _BoxSize;

    struct Attributes { float4 positionOS : POSITION; };
    struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

    Varyings Vert(Attributes input)
    {
        Varyings o;
        VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
        o.positionCS = p.positionCS;
        o.positionWS = p.positionWS;
        return o;
    }

    float IGN(float2 px) { return frac(52.9829189 * frac(0.06711056 * px.x + 0.00583715 * px.y)); }

    float3 _GridSize;   // cells per axis (property block)
    StructuredBuffer<uint> _Bounds;     // occupied cells: [0..2] min, [3..5] max; min > max = no ink (property block)

    // Trilinear sampling with a smoothed fraction (Quilez): C1-continuous, so the 2-unit grid does not show as creases
    // and stair-steps at the silhouette when the ink is drawn light.
    float SampleDensitySmooth(float3 uvw)
    {
        float3 g = max(_GridSize, 1.0);
        float3 c = uvw * g - 0.5;
        float3 i = floor(c), f = frac(c);
        f = f * f * (3.0 - 2.0 * f);
        return SAMPLE_TEXTURE3D_LOD(_DensityTex, sampler_DensityTex, (i + f + 0.5) / g, 0).r;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        float3 cam = _WorldSpaceCameraPos;
        float3 dir = normalize(i.positionWS - cam);

        // slab test against the axis-aligned box
        float3 boxMax = _BoxMin + _BoxSize;
        float3 invDir = 1.0 / (dir + 1e-6 * (dir == 0.0));
        float3 t0 = (_BoxMin - cam) * invDir, t1 = (boxMax - cam) * invDir;
        float3 tmin = min(t0, t1), tmax = max(t0, t1);
        float tNear = max(max(tmin.x, tmin.y), max(tmin.z, 0.0));
        float tFar = min(min(tmax.x, tmax.y), tmax.z);

        // clip to the ink's own AABB (padded a cell for the trilinear footprint and the soft threshold)
        uint3 bLo = uint3(_Bounds[0], _Bounds[1], _Bounds[2]);
        uint3 bHi = uint3(_Bounds[3], _Bounds[4], _Bounds[5]);
        if (any(bLo > bHi)) discard;
        float3 cellWorld = _BoxSize / max(_GridSize, 1.0);
        float3 inkMin = _BoxMin + (float3(bLo) - 1.0) * cellWorld;
        float3 inkMax = _BoxMin + (float3(bHi) + 2.0) * cellWorld;
        float3 u0 = (inkMin - cam) * invDir, u1 = (inkMax - cam) * invDir;
        float3 umin = min(u0, u1), umax = max(u0, u1);
        tNear = max(tNear, max(max(umin.x, umin.y), umin.z));
        tFar = min(tFar, min(min(umax.x, umax.y), umax.z));

        // stop at the scene
        float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
        float eye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        float3 camFwd = -UNITY_MATRIX_V[2].xyz;
        float sceneT = eye / max(dot(dir, camFwd), 1e-3);
        tFar = min(tFar, sceneT);
        if (tFar <= tNear) discard;

        float minStep = max(min(cellWorld.x, min(cellWorld.y, cellWorld.z)) * _MinStepCells, 1e-3);
        int steps = clamp((int)ceil((tFar - tNear) / minStep), 4, (int)_Steps);
        float stepLen = (tFar - tNear) / steps;
        float t = tNear + stepLen * IGN(i.positionCS.xy);
        float3 colour = 0.0;
        float T = 1.0;
        [loop] for (int s = 0; s < steps; s++)
        {
            float3 p = cam + dir * t;
            float3 uvw = (p - _BoxMin) / _BoxSize;
            float d = SampleDensitySmooth(uvw);
            float3 e = min(uvw, 1.0 - uvw);
            d *= smoothstep(0.0, _EdgeFade, min(e.x, min(e.y, e.z)));
            // soft threshold: a fade over _Softness instead of a hard cut, so the silhouette is not a staircase
            d = smoothstep(_Threshold, _Threshold + _Softness, d) * saturate((d - _Threshold) / (1.0 - _Threshold));
            d = pow(d, _Contrast) * (1.0 - _FxDissolve);
            if (d > 0.0)
            {
                float a = 1.0 - exp(-d * _Extinction * stepLen);
                float3 c = lerp(_WashColor.rgb, _InkColor.rgb, saturate(d * 2.0));
                colour += T * a * c;
                T *= 1.0 - a;
                if (T < 0.01) break;
            }
            t += stepLen;
        }
        float alpha = (1.0 - T) * _FxAlpha;
        return half4(colour * _FxAlpha, alpha);
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
            Name "InkFluid3D"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    Fallback Off
}
