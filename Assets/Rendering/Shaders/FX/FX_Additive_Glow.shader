// FX/Additive_Glow: the glow and flash functional layers (Docs/vfx/README.md section 3.2). Additive (premultiplied, with
// _Occlusion 1 it becomes plain alpha blending for text that must not glow), unlit, no depth write. A mask texture times an HDR colour, optionally wrapped into a ring with polar UVs (halo, sutra ring), with a
// noise-driven UV wobble and a dissolve. Alpha comes from vertex colour × _FxAlpha and only scales the added light.
Shader "FX/Additive_Glow"
{
    Properties
    {
        _MainTex("Mask", 2D) = "white" {}
        [HDR] _Color("Colour", Color) = (0.69, 0.45, 0.10, 1)
        _Intensity("Intensity", Range(0, 16)) = 1
        _Occlusion("Occlusion (0 additive, 1 alpha blended)", Range(0, 1)) = 0
        _ScrollSpeed("Mask scroll (xy, per second)", Vector) = (0, 0, 0, 0)

        [Header(Noise)]
        [NoScaleOffset] _NoiseTex("Packed noise (R noise, B dissolve)", 2D) = "gray" {}
        _NoiseTiling("Noise tiling", Float) = 1
        _NoiseScroll("Noise scroll (xy, per second)", Vector) = (0, 0.2, 0, 0)
        _NoiseStrength("UV wobble", Range(0, 0.3)) = 0

        [Header(Ring)]
        [Toggle(_FX_POLAR)] _Polar("Polar UV (ring)", Float) = 0
        _RingRadius("Ring centre radius", Range(0, 1)) = 0.8
        _RingWidth("Ring half width", Range(0.005, 1)) = 0.08
        _RingRepeat("Mask repeats around the ring", Float) = 1
        _RingSpin("Spin (turns per second)", Float) = 0.05
        _RingSoft("Soft ring falloff (0 = crisp band)", Range(0, 1)) = 1

        [Toggle] _UseCustomData("Read particle Custom1 stream", Float) = 0

        [Header(Driven)]
        _FxAlpha("Alpha (driven)", Range(0, 1)) = 1
        _FxProgress("Progress (driven)", Range(0, 1)) = 1
        _FxDissolve("Dissolve (driven)", Range(0, 1)) = 0
        _RevealByProgress("Reveal around the ring by progress", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FXAdditiveGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _FX_POLAR

            #include "FXCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _Occlusion;
                half _RingSoft;
                float4 _ScrollSpeed;
                float _NoiseTiling;
                float4 _NoiseScroll;
                half _NoiseStrength;
                half _RingRadius;
                half _RingWidth;
                float _RingRepeat;
                float _RingSpin;
                float _UseCustomData;
                half _FxAlpha;
                half _FxProgress;
                half _FxDissolve;
                half _RevealByProgress;
            CBUFFER_END

            FxParticleVaryings Vert(FxParticleAttributes input)
            {
                FxParticleVaryings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = input.texcoord0.xy;
                output.custom = FxCustomData(input, _UseCustomData);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(FxParticleVaryings input) : SV_Target
            {
                float time = _Time.y + input.custom.z * 10.0;
                float2 noiseUV = input.uv * _NoiseTiling + _NoiseScroll.xy * time;
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);
                float2 uv = input.uv + (noise.r - 0.5) * _NoiseStrength;
                half shape = 1;

                #if defined(_FX_POLAR)
                    float2 polar = FxPolar(uv);
                    float ringDistance = abs(polar.x - _RingRadius) / _RingWidth;
                    shape = saturate(1.0 - ringDistance);
                    shape = lerp(step(ringDistance, 1.0), shape * shape, _RingSoft);
                    float reveal = lerp(1.0, _FxProgress + input.custom.x, _RevealByProgress);
                    shape *= smoothstep(reveal, reveal - 0.05, polar.y);
                    uv = float2(polar.y * _RingRepeat + _RingSpin * time, saturate(0.5 + (polar.x - _RingRadius) / (2.0 * _RingWidth)));
                #endif

                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex) + _ScrollSpeed.xy * time);
                float2 dissolve = FxDissolve(noise.b, saturate(_FxDissolve + input.custom.y), 0.15);

                half alpha = mask.a * mask.r * shape * dissolve.x * input.color.a * _FxAlpha;
                half3 color = _Color.rgb * input.color.rgb * _Intensity * alpha;
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, alpha * _Occlusion);
            }
            ENDHLSL
        }
    }
}
