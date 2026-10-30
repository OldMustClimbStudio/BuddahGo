// FX/InkFluidSheet: draws the InkFluid density field as wet ink on the camera-facing sheet (Docs/vfx/palm-mesh-tripo.md
// section 14). Density -> black with a grey wash where thin; the sheet's border fades so the grid edge never shows.
Shader "FX/InkFluidSheet"
{
    Properties
    {
        [NoScaleOffset] _DensityTex("Density (R)", 2D) = "black" {}
        _InkColor("Ink", Color) = (0.01, 0.01, 0.015, 1)
        _WashColor("Wash", Color) = (0.45, 0.45, 0.48, 1)
        _Opacity("Opacity", Range(0, 2)) = 1.2
        _Contrast("Contrast", Range(0.3, 3)) = 1.1
        _Threshold("Threshold", Range(0, 0.5)) = 0.02
        _EdgeFade("Border fade (fraction of the sheet)", Range(0.01, 0.5)) = 0.12
        [Toggle(_SHEET_SOFT)] _SoftOn("Soft against geometry (needs Depth Texture)", Float) = 1
        _SoftDistance("Soft distance (world units)", Float) = 8
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE2D(_DensityTex); SAMPLER(sampler_DensityTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _InkColor;
        half4 _WashColor;
        half _Opacity;
        half _Contrast;
        half _Threshold;
        half _EdgeFade;
        float _SoftDistance;
    CBUFFER_END

    struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
    struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

    Varyings Vert(Attributes input)
    {
        Varyings o;
        VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
        o.positionCS = p.positionCS;
        o.uv = input.uv;
        o.fogFactor = ComputeFogFactor(p.positionCS.z);
        return o;
    }

    half4 Frag(Varyings input) : SV_Target
    {
        half d = SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, input.uv).r;
        d = saturate((d - _Threshold) / (1.0 - _Threshold));
        d = pow(d, _Contrast);
        float2 e = min(input.uv, 1.0 - input.uv);
        half border = smoothstep(0.0, _EdgeFade, min(e.x, e.y));
        half alpha = saturate(d * _Opacity) * border;
        half3 color = lerp(_WashColor.rgb, _InkColor.rgb, saturate(d * 2.0));
#if defined(_SHEET_SOFT)
        float2 depthUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float sceneEye = LinearEyeDepth(SampleSceneDepth(depthUV), _ZBufferParams);
        float fragEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
        alpha *= saturate((sceneEye - fragEye) / max(_SoftDistance, 1e-3));
#endif
        return half4(MixFog(color, input.fogFactor), alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "InkFluidSheet"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _SHEET_SOFT
            ENDHLSL
        }
    }
    Fallback Off
}
