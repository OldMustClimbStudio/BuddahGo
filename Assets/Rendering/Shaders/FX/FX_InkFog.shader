// FX/InkFog: a hand made of churning ink fog (Docs/vfx/palm-mesh-tripo.md section 9). The mesh is only the skeleton
// the fog gathers on; nothing of its surface is drawn. Same picture language as the Piloto Realistic Ying Yang pack:
// black wet ink with a grey wash, white gaps, no colour.
// - Fog: the pack's cloud noise sampled triplanar in object space, domain-warped by a second noise and scrolling
//   up (+Y) so it keeps boiling; a second octave breaks the blots up. The warped value goes through a soft threshold
//   whose position is the local density, so dense places get solid ink and thin places only the strongest wisps.
//   Textures are sRGB, so the sampled values are linear; _NoiseLow / _NoiseHigh map the useful linear range to 0..1.
// - Density: palm side (object +Z normals) dense, back side thin (_PalmDensity / _BackDensity); inside each form
//   the middle is hollow and the ink gathers toward the silhouette (_CoreDensity -> 1 with the view Fresnel), like a
//   shell of smoke; past the silhouette the threshold rises again (_EdgeFray) so the outline frays into wisps.
// - Volume: the mesh is drawn three times, each shell pushed further out along the normals (_ShellSpacing) with less
//   density (_ShellFalloff) and a different noise phase, so wisps float outside the silhouette and the layers slide
//   against each other as the camera moves. Outer shells draw first (SRPDefaultUnlit), the inner one last.
// - Churn: vertices are pushed along their normals by scrolling noise (_Wobble), so the outline never settles and the
//   mesh is never read as a surface. Front faces only, Alpha blend, no ZWrite, no depth texture.
// - Driven: _FxAlpha fades the whole thing; _FxDissolve raises the threshold everywhere (the fog thins out and goes).
Shader "FX/InkFog"
{
    Properties
    {
        [Header(Ink)]
        _InkColor("Ink", Color) = (0.01, 0.01, 0.015, 1)
        _WashColor("Wash (thin ink)", Color) = (0.3, 0.3, 0.33, 1)
        _Opacity("Max opacity", Range(0, 1)) = 1
        _WashOpacity("Wash opacity", Range(0, 1)) = 0.3

        [Header(Noise)]
        [NoScaleOffset] _InkTex("Cloud noise (R)", 2D) = "gray" {}
        [NoScaleOffset] _WarpTex("Warp noise (R)", 2D) = "gray" {}
        _Tiling("Cloud tiling (per object unit)", Float) = 0.02
        _DetailScale("Detail octave scale", Float) = 2.5
        _DetailWeight("Detail octave weight", Range(0, 1)) = 0.5
        _NoiseLow("Cloud value (linear) mapped to 0", Range(0, 1)) = 0.22
        _NoiseHigh("Cloud value (linear) mapped to 1", Range(0, 1)) = 0.68
        _WarpTiling("Warp tiling", Float) = 0.008
        _Warp("Warp strength (object units)", Float) = 12
        _Rise("Rise speed (object units per second)", Float) = 8
        _WarpSpeed("Warp drift speed", Float) = 0.04

        [Header(Density)]
        _Density("Overall density", Range(0, 1)) = 0.75
        _Softness("Threshold softness", Range(0.01, 1)) = 0.2
        _PalmDensity("Palm side density", Range(0, 1)) = 1
        _BackDensity("Back side density", Range(0, 1)) = 0.3
        _CoreDensity("Density facing the camera (hollow middle)", Range(0, 1)) = 0.45
        _ShellPower("Gathering toward the silhouette", Range(0.3, 6)) = 1.5
        _EdgeFray("Fraying past the silhouette", Range(0, 1)) = 0.3
        _EdgeFrayPower("Fray sharpness", Range(1, 12)) = 5

        [Header(Volume shells)]
        _ShellSpacing("Shell spacing (object units)", Float) = 3
        _ShellFalloff("Density per outer shell", Range(0, 1)) = 0.6

        [Header(Churn)]
        _Wobble("Vertex wobble along the normal (object units)", Float) = 2.5
        _WobbleTiling("Wobble tiling", Float) = 0.02
        _WobbleSpeed("Wobble speed", Float) = 0.5

        [Header(Driven)]
        _FxAlpha("Alpha (driven)", Range(0, 1)) = 1
        _FxDissolve("Dissolve (driven)", Range(0, 1)) = 0
        _Debug("Debug view (0 off, 1 noise, 2 density, 3 palm/fresnel/cloud)", Float) = 0
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"

    TEXTURE2D(_InkTex); SAMPLER(sampler_InkTex);
    TEXTURE2D(_WarpTex); SAMPLER(sampler_WarpTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _InkColor;
        half4 _WashColor;
        half _Opacity;
        half _WashOpacity;
        float _Tiling;
        float _DetailScale;
        half _DetailWeight;
        half _NoiseLow;
        half _NoiseHigh;
        float _WarpTiling;
        float _Warp;
        float _Rise;
        float _WarpSpeed;
        half _Density;
        half _Softness;
        half _PalmDensity;
        half _BackDensity;
        half _CoreDensity;
        half _ShellPower;
        half _EdgeFray;
        half _EdgeFrayPower;
        float _ShellSpacing;
        half _ShellFalloff;
        float _Wobble;
        float _WobbleTiling;
        float _WobbleSpeed;
        half _FxAlpha;
        half _FxDissolve;
        half _Debug;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionOS : TEXCOORD0;
        float3 normalOS : TEXCOORD1;
        float3 normalWS : TEXCOORD2;
        float3 viewDirWS : TEXCOORD3;
        float fogFactor : TEXCOORD4;
    };

    float3 TriWeights(float3 n)
    {
        float3 w = pow(abs(n), 3.0);
        return w / max(w.x + w.y + w.z, 1e-4);
    }

    half TriSample(TEXTURE2D_PARAM(tex, s), float3 p, float3 w)
    {
        half x = SAMPLE_TEXTURE2D(tex, s, p.zy).r;
        half y = SAMPLE_TEXTURE2D(tex, s, p.xz).r;
        half z = SAMPLE_TEXTURE2D(tex, s, p.xy).r;
        return x * w.x + y * w.y + z * w.z;
    }

    half TriSampleLod(TEXTURE2D_PARAM(tex, s), float3 p, float3 w)
    {
        half x = SAMPLE_TEXTURE2D_LOD(tex, s, p.zy, 0).r;
        half y = SAMPLE_TEXTURE2D_LOD(tex, s, p.xz, 0).r;
        half z = SAMPLE_TEXTURE2D_LOD(tex, s, p.xy, 0).r;
        return x * w.x + y * w.y + z * w.z;
    }

    // Warp noise is sRGB too: its linear mean sits near 0.3, so centre there.
    #define WARP_MID 0.3

    Varyings InkFogVertShell(Attributes input, float shell)
    {
        Varyings output;
        float3 normalOS = normalize(input.normalOS);
        float3 w = TriWeights(normalOS);
        float3 wp = (input.positionOS.xyz + float3(0.0, -_Time.y * _WobbleSpeed * 40.0, 0.0)) * _WobbleTiling + shell * 0.23;
        half wobble = TriSampleLod(TEXTURE2D_ARGS(_WarpTex, sampler_WarpTex), wp, w) - WARP_MID;
        float3 positionOS = input.positionOS.xyz + normalOS * (wobble * _Wobble + shell * _ShellSpacing);

        VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
        output.positionCS = positions.positionCS;
        output.positionOS = positionOS;
        output.normalOS = normalOS;
        output.normalWS = TransformObjectToWorldNormal(normalOS);
        output.viewDirWS = GetWorldSpaceNormalizeViewDir(positions.positionWS);
        output.fogFactor = ComputeFogFactor(positions.positionCS.z);
        return output;
    }

    half4 InkFogFragShell(Varyings input, float shell)
    {
        float3 normalOS = normalize(input.normalOS);
        float3 normalWS = normalize(input.normalWS);
        float3 viewDirWS = normalize(input.viewDirWS);
        float3 w = TriWeights(normalOS);
        float t = _Time.y;
        float phase = shell * 0.37;

        // Domain warp: a slow second noise bends the sampling position, the cloud scrolls up through it.
        float3 warpP = (input.positionOS + float3(0.0, -t * _Rise * 0.5, 0.0)) * _WarpTiling + t * _WarpSpeed + phase;
        half warpA = TriSample(TEXTURE2D_ARGS(_WarpTex, sampler_WarpTex), warpP, w) - WARP_MID;
        half warpB = TriSample(TEXTURE2D_ARGS(_WarpTex, sampler_WarpTex), warpP.yzx + 0.31, w) - WARP_MID;
        float3 p = input.positionOS + float3(warpA, warpB, -warpA) * _Warp + float3(0.0, -t * _Rise, 0.0);
        half cloud = TriSample(TEXTURE2D_ARGS(_InkTex, sampler_InkTex), p * _Tiling + phase, w);
        half detail = TriSample(TEXTURE2D_ARGS(_InkTex, sampler_InkTex), p * _Tiling * _DetailScale + 0.47 - phase, w);
        half value = lerp(cloud, cloud * 0.5 + detail * 0.5, _DetailWeight);
        value = saturate((value - _NoiseLow) / max(_NoiseHigh - _NoiseLow, 1e-3));

        // Local density: palm side vs back, hollow middle gathering toward the silhouette, fraying past it, thinner
        // on each outer shell.
        half palm = saturate(normalOS.z * 0.5 + 0.5);
        half side = lerp(_BackDensity, _PalmDensity, palm);
        half ndv = saturate(dot(normalWS, viewDirWS));
        half fresnel = 1.0 - ndv;
        half gather = lerp(_CoreDensity, 1.0, pow(fresnel, _ShellPower));
        half fray = pow(fresnel, _EdgeFrayPower) * _EdgeFray;
        half density = saturate(_Density * side * gather * pow(_ShellFalloff, shell) - fray) * (1.0 - _FxDissolve);

        // Threshold: dense -> low threshold (solid ink), thin -> only the brightest noise survives as wisps.
        half threshold = 1.0 - density;
        half ink = smoothstep(threshold, threshold + _Softness, value);
        half wash = smoothstep(threshold - _Softness, threshold, value);

        half alpha = max(ink * _Opacity, wash * _WashOpacity) * _FxAlpha;
        half3 color = lerp(_WashColor.rgb, _InkColor.rgb, ink);
        if (_Debug > 0.5) return half4(_Debug < 1.5 ? value.xxx : (_Debug < 2.5 ? density.xxx : half3(palm, fresnel, cloud)), 1.0);
        return half4(MixFog(color, input.fogFactor), alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "InkFogShell2"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            Varyings Vert(Attributes input) { return InkFogVertShell(input, 2.0); }
            half4 Frag(Varyings input) : SV_Target { return InkFogFragShell(input, 2.0); }
            ENDHLSL
        }

        Pass
        {
            Name "InkFogShell1"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            Varyings Vert(Attributes input) { return InkFogVertShell(input, 1.0); }
            half4 Frag(Varyings input) : SV_Target { return InkFogFragShell(input, 1.0); }
            ENDHLSL
        }

        Pass
        {
            Name "InkFogShell0"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            Varyings Vert(Attributes input) { return InkFogVertShell(input, 0.0); }
            half4 Frag(Varyings input) : SV_Target { return InkFogFragShell(input, 0.0); }
            ENDHLSL
        }
    }
    Fallback Off
}
