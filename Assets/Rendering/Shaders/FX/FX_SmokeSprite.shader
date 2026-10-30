// FX/SmokeSprite: a smoke particle sprite that is shaped by noise instead of a flipbook (Docs/vfx/palm-mesh-tripo.md
// section 12). Each particle is a soft radial gradient whose edge is eroded by tiling noise; the noise is offset per
// particle (Custom1.y, a random constant) and drifts and rotates with the particle's age (Custom1.x, 0..1), and the
// erosion threshold rises with age, so a puff is born as a soft blob, grows curling edges and finally tears into
// filaments while it fades. The motion itself (laminar rise, then curl-noise turbulence) is the particle system's job.
// Vertex streams: Position, Color, UV, Custom1.xyzw (TEXCOORD0 = uv.xy + custom1.xy; TEXCOORD1 = custom1.zw).
Shader "FX/SmokeSprite"
{
    Properties
    {
        _InkColor("Ink", Color) = (0.02, 0.02, 0.025, 1)
        _ThinColor("Thin edge", Color) = (0.45, 0.45, 0.48, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.8
        [NoScaleOffset] _NoiseTex("Tiling noise (R)", 2D) = "gray" {}
        _NoiseTiling("Noise tiling", Float) = 1.6
        _NoiseLow("Noise value (linear) mapped to 0", Range(0, 1)) = 0.2
        _NoiseHigh("Noise value (linear) mapped to 1", Range(0, 1)) = 0.7
        _Warp("Edge warp", Range(0, 1)) = 0.35
        _ErodeStart("Erosion at birth", Range(0, 1)) = 0.15
        _ErodeEnd("Erosion at death", Range(0, 1)) = 0.85
        _Softness("Erosion softness", Range(0.01, 1)) = 0.3
        _Spin("Noise spin over life (turns)", Float) = 0.35
        _Drift("Noise drift over life", Float) = 0.4
        _CoreHardness("Core hardness", Range(0.5, 4)) = 1.5
        [Toggle(_SMOKE_SOFT)] _SoftOn("Soft particles (needs the pipeline's Depth Texture)", Float) = 1
        _SoftDistance("Soft particle fade distance (world units)", Float) = 6
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _InkColor;
        half4 _ThinColor;
        half _Opacity;
        float _NoiseTiling;
        half _NoiseLow;
        half _NoiseHigh;
        half _Warp;
        half _ErodeStart;
        half _ErodeEnd;
        half _Softness;
        float _Spin;
        float _Drift;
        half _CoreHardness;
        float _SoftDistance;
    CBUFFER_END

    FxParticleVaryings SmokeVert(FxParticleAttributes input)
    {
        FxParticleVaryings output;
        VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = positions.positionCS;
        output.color = input.color;
        output.uv = input.texcoord0.xy;
        output.custom = FxCustomData(input, 1.0);
        output.fogFactor = ComputeFogFactor(positions.positionCS.z);
        return output;
    }

    half Noise(float2 uv) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r; }

    half4 SmokeFrag(FxParticleVaryings input) : SV_Target
    {
        float age = saturate(input.custom.x);
        float seed = input.custom.y;

        // Per-particle noise frame: offset by the seed, spinning and drifting with age.
        float2 c = input.uv - 0.5;
        float ang = (seed * 6.2832) + age * _Spin * 6.2832;
        float2 r = float2(c.x * cos(ang) - c.y * sin(ang), c.x * sin(ang) + c.y * cos(ang));
        float2 nuv = r * _NoiseTiling + seed * 7.31 + float2(0.0, -age * _Drift);

        // Warp the gradient's edge with a low-frequency noise, then erode it with a finer one.
        float2 w = float2(Noise(nuv * 0.5 + 0.17), Noise(nuv * 0.5 + 0.61)) - 0.4;
        float radius = length(c + w * _Warp * 0.5) * 2.0;
        half core = pow(saturate(1.0 - radius), _CoreHardness);
        half n = Noise(nuv);
        n = saturate((n - _NoiseLow) / max(_NoiseHigh - _NoiseLow, 1e-3));
        half mask = core * (0.35 + 0.65 * n);
        half erode = lerp(_ErodeStart, _ErodeEnd, age);
        half alpha = smoothstep(erode * 0.6, erode * 0.6 + _Softness, mask);

        half3 color = lerp(_ThinColor.rgb, _InkColor.rgb, saturate(alpha * 1.5)) * input.color.rgb;
        alpha *= _Opacity * input.color.a;
#if defined(_SMOKE_SOFT)
        // Fade where the sprite meets geometry (the floor) instead of a hard cut.
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SmokeSprite"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex SmokeVert
            #pragma fragment SmokeFrag
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _SMOKE_SOFT
            ENDHLSL
        }
    }
    Fallback Off
}
